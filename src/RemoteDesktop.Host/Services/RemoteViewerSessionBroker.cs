using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using ViewerTransportEnvelope = RemoteDesktop.Shared.Models.ViewerTransportEnvelope;

namespace RemoteDesktop.Host.Services;

public interface IRemoteViewerSessionBroker : IAsyncDisposable
{
    Task<bool> AttachViewerAsync(
        string deviceId,
        AuthenticatedUserSession viewer,
        Func<byte[], CancellationToken, Task> publishFrameAsync,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        Func<AgentClipboardMessage, CancellationToken, Task> publishClipboardAsync,
        CancellationToken cancellationToken);

    Task DetachViewerAsync(string deviceId, CancellationToken cancellationToken);

    Task ForwardViewerCommandAsync(string deviceId, ViewerCommandMessage command, CancellationToken cancellationToken);
}

public sealed class RemoteViewerSessionBrokerFactory
{
    private readonly DeviceBroker _deviceBroker;
    private readonly IOptions<ControlServerOptions> _options;
    private readonly CentralConsoleSessionState _sessionState;

    public RemoteViewerSessionBrokerFactory(DeviceBroker deviceBroker, IOptions<ControlServerOptions> options, CentralConsoleSessionState sessionState)
    {
        _deviceBroker = deviceBroker;
        _options = options;
        _sessionState = sessionState;
    }

    public IRemoteViewerSessionBroker Create()
    {
        var centralServerUrl = _options.Value.CentralServerUrl;
        if (string.IsNullOrWhiteSpace(centralServerUrl))
        {
            return new LocalRemoteViewerSessionBroker(_deviceBroker);
        }

        return new CentralServerRemoteViewerSessionBroker(centralServerUrl, _sessionState);
    }
}

internal sealed class LocalRemoteViewerSessionBroker : IRemoteViewerSessionBroker
{
    private readonly DeviceBroker _deviceBroker;

    public LocalRemoteViewerSessionBroker(DeviceBroker deviceBroker)
    {
        _deviceBroker = deviceBroker;
    }

    public Task<bool> AttachViewerAsync(
        string deviceId,
        AuthenticatedUserSession viewer,
        Func<byte[], CancellationToken, Task> publishFrameAsync,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        Func<AgentClipboardMessage, CancellationToken, Task> publishClipboardAsync,
        CancellationToken cancellationToken)
    {
        return _deviceBroker.AttachViewerAsync(
            deviceId,
            viewer.DisplayName,
            viewer.CanControlRemote,
            publishFrameAsync,
            publishStatusAsync,
            publishClipboardAsync,
            cancellationToken);
    }

    public Task DetachViewerAsync(string deviceId, CancellationToken cancellationToken)
    {
        return _deviceBroker.DetachViewerAsync(deviceId);
    }

    public Task ForwardViewerCommandAsync(string deviceId, ViewerCommandMessage command, CancellationToken cancellationToken)
    {
        return _deviceBroker.ForwardViewerCommandAsync(deviceId, command, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed class CentralServerRemoteViewerSessionBroker : IRemoteViewerSessionBroker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Uri _viewerEndpoint;
    private readonly CentralConsoleSessionState _sessionState;
    private readonly TaskCompletionSource<bool> _readySignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ClientWebSocket? _socket;
    private Task? _receiveLoopTask;
    private Func<byte[], CancellationToken, Task>? _publishFrameAsync;
    private Func<AgentFileTransferStatusMessage, CancellationToken, Task>? _publishStatusAsync;
    private Func<AgentClipboardMessage, CancellationToken, Task>? _publishClipboardAsync;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public CentralServerRemoteViewerSessionBroker(string centralServerUrl, CentralConsoleSessionState sessionState)
    {
        _viewerEndpoint = BuildViewerEndpoint(centralServerUrl);
        _sessionState = sessionState;
    }

    public async Task<bool> AttachViewerAsync(
        string deviceId,
        AuthenticatedUserSession viewer,
        Func<byte[], CancellationToken, Task> publishFrameAsync,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task> publishStatusAsync,
        Func<AgentClipboardMessage, CancellationToken, Task> publishClipboardAsync,
        CancellationToken cancellationToken)
    {
        if (_socket is not null)
        {
            throw new InvalidOperationException("Viewer session is already attached.");
        }

        _publishFrameAsync = publishFrameAsync;
        _publishStatusAsync = publishStatusAsync;
        _publishClipboardAsync = publishClipboardAsync;

        var socket = new ClientWebSocket();
        _sessionState.ApplyAuthorizationHeader(socket);
        var endpoint = BuildAttachUri(deviceId);
        await socket.ConnectAsync(endpoint, cancellationToken);
        _socket = socket;
        _receiveLoopTask = ReceiveLoopAsync(socket, cancellationToken);
        var completed = await Task.WhenAny(_readySignal.Task, _receiveLoopTask!);
        if (completed == _readySignal.Task)
        {
            return await _readySignal.Task;
        }

        return false;
    }

    public async Task DetachViewerAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        try
        {
            if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "viewer-detach", cancellationToken);
            }
        }
        catch
        {
        }
        finally
        {
            _socket.Dispose();
            _socket = null;
        }

        if (_receiveLoopTask is not null)
        {
            try
            {
                await _receiveLoopTask;
            }
            catch
            {
            }
        }
    }

    public async Task ForwardViewerCommandAsync(string deviceId, ViewerCommandMessage command, CancellationToken cancellationToken)
    {
        if (_socket is null || _socket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Central viewer channel is not connected.");
        }

        var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(command, JsonOptions));
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DetachViewerAsync(string.Empty, CancellationToken.None);
        _sendLock.Dispose();
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = await WebSocketMessageReader.ReadAsync(socket, cancellationToken);
            if (message.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (message.MessageType == WebSocketMessageType.Binary)
            {
                if (_publishFrameAsync is not null)
                {
                    await _publishFrameAsync(message.Payload.ToArray(), cancellationToken);
                }

                continue;
            }

            if (message.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(message.Payload);
            var envelope = JsonSerializer.Deserialize<ViewerTransportEnvelope>(json, JsonOptions);
            if (envelope is null)
            {
                continue;
            }

            switch (envelope.Type)
            {
                case "viewer-ready":
                    _readySignal.TrySetResult(true);
                    break;
                case "transfer-status" when envelope.TransferStatus is not null && _publishStatusAsync is not null:
                    await _publishStatusAsync(MapTransferStatus(envelope.TransferStatus), cancellationToken);
                    break;
                case "clipboard" when envelope.Clipboard is not null && _publishClipboardAsync is not null:
                    await _publishClipboardAsync(MapClipboard(envelope.Clipboard), cancellationToken);
                    break;
            }
        }

        _readySignal.TrySetResult(false);
    }

    private Uri BuildAttachUri(string deviceId)
    {
        var builder = new UriBuilder(_viewerEndpoint);
        builder.Query = $"deviceId={Uri.EscapeDataString(deviceId)}";
        return builder.Uri;
    }

    private static Uri BuildViewerEndpoint(string centralServerUrl)
    {
        var builder = new UriBuilder(centralServerUrl)
        {
            Scheme = centralServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = "/ws/viewer"
        };

        return builder.Uri;
    }

    private static AgentFileTransferStatusMessage MapTransferStatus(RemoteDesktop.Shared.Models.AgentFileTransferStatusMessage source)
    {
        return new AgentFileTransferStatusMessage
        {
            Type = source.Type,
            UploadId = source.UploadId,
            Status = source.Status,
            Direction = source.Direction,
            SequenceNumber = source.SequenceNumber,
            Message = source.Message,
            FileName = source.FileName,
            StoredFileName = source.StoredFileName,
            StoredFilePath = source.StoredFilePath,
            FileSize = source.FileSize,
            BytesTransferred = source.BytesTransferred,
            ChunkBase64 = source.ChunkBase64,
            DirectoryPath = source.DirectoryPath,
            ParentDirectoryPath = source.ParentDirectoryPath,
            CanNavigateUp = source.CanNavigateUp,
            EntriesTruncated = source.EntriesTruncated,
            Entries = source.Entries.Select(static item => new RemoteFileBrowserEntry
            {
                Name = item.Name,
                FullPath = item.FullPath,
                IsDirectory = item.IsDirectory,
                Size = item.Size,
                LastModifiedAt = item.LastModifiedAt
            }).ToList()
        };
    }

    private static AgentClipboardMessage MapClipboard(RemoteDesktop.Shared.Models.AgentClipboardMessage source)
    {
        return new AgentClipboardMessage
        {
            Operation = source.Operation,
            Status = source.Status,
            Text = source.Text,
            Truncated = source.Truncated,
            Message = source.Message
        };
    }
}
