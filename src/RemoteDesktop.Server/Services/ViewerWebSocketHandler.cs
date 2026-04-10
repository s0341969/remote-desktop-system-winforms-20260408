using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteDesktop.Server.Services.Security;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services;

public sealed class ViewerWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DeviceBroker _broker;
    private readonly ConsoleSessionTokenService _sessionTokenService;

    public ViewerWebSocketHandler(DeviceBroker broker, ConsoleSessionTokenService sessionTokenService)
    {
        _broker = broker;
        _sessionTokenService = sessionTokenService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!_sessionTokenService.TryAuthenticate(context.Request, out var session))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var deviceId = context.Request.Query["deviceId"].ToString();
        var forceTakeover = bool.TryParse(context.Request.Query["forceTakeover"], out var parsedForceTakeover) && parsedForceTakeover;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Missing deviceId.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        string? viewerSessionId = null;
        var attached = await _broker.AttachViewerAsync(
            deviceId,
            session.DisplayName,
            _sessionTokenService.CanControlRemote(session),
            forceTakeover,
            async (payload, cancellationToken) =>
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
                }
            },
            async (status, cancellationToken) =>
            {
                var envelope = new ViewerTransportEnvelope
                {
                    Type = "transfer-status",
                    DeviceId = deviceId,
                    TransferStatus = status
                };

                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                }
            },
            async (clipboard, cancellationToken) =>
            {
                var envelope = new ViewerTransportEnvelope
                {
                    Type = "clipboard",
                    DeviceId = deviceId,
                    Clipboard = clipboard
                };

                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                }
            },
            async (viewerState, cancellationToken) =>
            {
                var envelope = new ViewerTransportEnvelope
                {
                    Type = "viewer-mode-updated",
                    DeviceId = deviceId,
                    CanControl = viewerState.CanControl,
                    ControllerDisplayName = viewerState.ControllerDisplayName,
                    Message = viewerState.Message
                };

                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                }
            },
            async (reason, cancellationToken) =>
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken);
                }
            },
            context.RequestAborted);

        if (!attached.Attached)
        {
            var rejected = new ViewerTransportEnvelope
            {
                Type = "viewer-rejected",
                DeviceId = deviceId,
                Message = attached.Message
            };
            var rejectedPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rejected, JsonOptions));
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(rejectedPayload, WebSocketMessageType.Text, true, context.RequestAborted);
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, attached.Message ?? "Device unavailable or busy.", context.RequestAborted);
            }
            return;
        }

        viewerSessionId = attached.ViewerSessionId;

        var readyEnvelope = new ViewerTransportEnvelope
        {
            Type = "viewer-ready",
            DeviceId = deviceId,
            Message = attached.Message ?? "viewer-ready",
            CanControl = attached.CanControl,
            ControllerDisplayName = attached.ControllerDisplayName
        };

        var readyPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(readyEnvelope, JsonOptions));
        if (socket.State == WebSocketState.Open)
        {
            await socket.SendAsync(readyPayload, WebSocketMessageType.Text, true, context.RequestAborted);
        }

        try
        {
            while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var message = await WebSocketMessageReader.ReadAsync(socket, context.RequestAborted);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (message.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var json = Encoding.UTF8.GetString(message.Payload);
                using var document = JsonDocument.Parse(json);
                var type = document.RootElement.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (string.Equals(type, "viewer-control-request", StringComparison.Ordinal))
                {
                    var requestForceTakeover = document.RootElement.TryGetProperty("forceTakeover", out var takeoverElement)
                        && takeoverElement.ValueKind == JsonValueKind.True;
                    var updatedState = await _broker.RequestViewerControlAsync(deviceId, viewerSessionId!, requestForceTakeover, context.RequestAborted);
                    var envelope = new ViewerTransportEnvelope
                    {
                        Type = "viewer-mode-updated",
                        DeviceId = deviceId,
                        CanControl = updatedState.CanControl,
                        ControllerDisplayName = updatedState.ControllerDisplayName,
                        Message = updatedState.Message
                    };

                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
                    if (socket.State == WebSocketState.Open)
                    {
                        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, context.RequestAborted);
                    }
                    continue;
                }

                await _broker.ForwardViewerCommandAsync(deviceId, viewerSessionId!, json, context.RequestAborted);
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (WebSocketException) when (context.RequestAborted.IsCancellationRequested || socket.State is WebSocketState.Aborted or WebSocketState.Closed)
        {
        }
        finally
        {
            await _broker.DetachViewerAsync(deviceId, viewerSessionId);
        }
    }
}
