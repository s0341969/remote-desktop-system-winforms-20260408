using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteDesktop.Host;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services.Auditing;

namespace RemoteDesktop.Host.Services;

public sealed class DeviceBroker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, AgentSession> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeviceRepository _repository;
    private readonly ControlServerOptions _options;
    private readonly ILogger<DeviceBroker> _logger;
    private readonly AuditService _auditService;

    public DeviceBroker(IDeviceRepository repository, IOptions<ControlServerOptions> options, ILogger<DeviceBroker> logger, AuditService auditService)
    {
        _repository = repository;
        _options = options.Value;
        _logger = logger;
        _auditService = auditService;
    }

    public async Task<AgentRegistrationResult> RegisterAgentAsync(WebSocket socket, AgentHelloMessage message, CancellationToken cancellationToken)
    {
        if (!FixedTimeEquals(message.AccessKey, _options.SharedAccessKey))
        {
            return AgentRegistrationResult.CreateUnauthorized();
        }

        var descriptor = new AgentDescriptor
        {
            DeviceId = message.DeviceId?.Trim() ?? string.Empty,
            DeviceName = message.DeviceName?.Trim() ?? string.Empty,
            HostName = message.HostName?.Trim() ?? string.Empty,
            AgentVersion = message.AgentVersion?.Trim() ?? string.Empty,
            ScreenWidth = message.ScreenWidth,
            ScreenHeight = message.ScreenHeight,
            Inventory = message.Inventory
        };

        if (string.IsNullOrWhiteSpace(descriptor.DeviceId) || string.IsNullOrWhiteSpace(descriptor.DeviceName))
        {
            return AgentRegistrationResult.CreateInvalid();
        }

        if (_agents.TryRemove(descriptor.DeviceId, out var existing))
        {
            await existing.CloseAsync("replaced-by-new-agent", cancellationToken);
            await _repository.ClosePresenceAsync(existing.PresenceId, existing.DeviceId, "replaced-by-new-agent", cancellationToken);
        }

        await _repository.UpsertDeviceOnlineAsync(descriptor, cancellationToken);
        if (descriptor.Inventory is not null)
        {
            await _repository.UpdateInventoryAsync(descriptor.DeviceId, descriptor.Inventory, cancellationToken);
        }
        var deviceRecord = await _repository.GetDeviceAsync(descriptor.DeviceId, cancellationToken);
        var presenceId = await _repository.StartPresenceAsync(descriptor, cancellationToken);

        var session = new AgentSession(socket, descriptor, presenceId, deviceRecord?.IsAuthorized == true);
        _agents[descriptor.DeviceId] = session;
        _logger.LogInformation("Agent registered: {DeviceId} ({DeviceName})", descriptor.DeviceId, descriptor.DeviceName);
        return AgentRegistrationResult.Success(session);
    }

    public async Task TouchAgentAsync(string deviceId, int screenWidth, int screenHeight, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        session.LastHeartbeatAt = DateTimeOffset.UtcNow;
        await _repository.TouchPresenceAsync(session.PresenceId, session.DeviceId, screenWidth, screenHeight, cancellationToken);
    }

    public async Task UpdateInventoryAsync(string deviceId, AgentInventoryProfile inventory, CancellationToken cancellationToken)
    {
        if (_agents.TryGetValue(deviceId, out var session))
        {
            session.LastHeartbeatAt = DateTimeOffset.UtcNow;
        }

        await _repository.UpdateInventoryAsync(deviceId, inventory, cancellationToken);
    }

    public async Task PublishFrameAsync(string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        ViewerSession? viewerSession;
        lock (session.SyncRoot)
        {
            viewerSession = session.ViewerSession;
        }

        if (viewerSession is null)
        {
            return;
        }

        try
        {
            await viewerSession.PublishFrameAsync(payload, cancellationToken);
            session.LastHeartbeatAt = DateTimeOffset.UtcNow;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Viewer disconnected unexpectedly for device {DeviceId}.", deviceId);
            await DetachViewerAsync(deviceId);
        }
    }

    public async Task PublishViewerStatusAsync(string deviceId, AgentFileTransferStatusMessage status, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        ViewerSession? viewerSession;
        lock (session.SyncRoot)
        {
            viewerSession = session.ViewerSession;
        }

        if (viewerSession is null)
        {
            return;
        }

        try
        {
            await viewerSession.PublishStatusAsync(status, cancellationToken);

            string? actionPrefix = status.Direction?.ToLowerInvariant() switch
            {
                "download" => "file-download",
                "upload" => "file-upload",
                "move" => "file-move",
                _ => null
            };

            if (actionPrefix is null)
            {
                return;
            }

            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                _ = _auditService.WriteAsync(
                    $"{actionPrefix}-complete",
                    viewerSession.UserName,
                    viewerSession.UserName,
                    "device",
                    deviceId,
                    true,
                    status.Direction?.ToLowerInvariant() switch
                    {
                        "download" => HostUiText.Bi($"檔案「{status.StoredFileName}」已成功下載。", $"Downloaded '{status.StoredFileName}' successfully."),
                        "upload" => HostUiText.Bi($"檔案「{status.StoredFileName}」已成功上傳。", $"Uploaded '{status.StoredFileName}' successfully."),
                        "move" => HostUiText.Bi($"已成功移動「{status.StoredFileName}」。", $"Moved '{status.StoredFileName}' successfully."),
                        _ => status.Message
                    },
                    CancellationToken.None);
            }
            else if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                _ = _auditService.WriteAsync(
                    $"{actionPrefix}-complete",
                    viewerSession.UserName,
                    viewerSession.UserName,
                    "device",
                    deviceId,
                    false,
                    status.Message,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Viewer status delivery failed for device {DeviceId}.", deviceId);
            await DetachViewerAsync(deviceId);
        }
    }

    public async Task PublishViewerClipboardAsync(string deviceId, AgentClipboardMessage message, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        ViewerSession? viewerSession;
        lock (session.SyncRoot)
        {
            viewerSession = session.ViewerSession;
        }

        if (viewerSession is null)
        {
            return;
        }

        try
        {
            await viewerSession.PublishClipboardAsync(message, cancellationToken);

            if (string.Equals(message.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var action = string.Equals(message.Operation, "get", StringComparison.OrdinalIgnoreCase)
                    ? "clipboard-get-complete"
                    : "clipboard-set-complete";
                _ = _auditService.WriteAsync(
                    action,
                    viewerSession.UserName,
                    viewerSession.UserName,
                    "device",
                    deviceId,
                    true,
                    message.Message,
                    CancellationToken.None);
            }
            else if (string.Equals(message.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                var action = string.Equals(message.Operation, "get", StringComparison.OrdinalIgnoreCase)
                    ? "clipboard-get-complete"
                    : "clipboard-set-complete";
                _ = _auditService.WriteAsync(
                    action,
                    viewerSession.UserName,
                    viewerSession.UserName,
                    "device",
                    deviceId,
                    false,
                    message.Message,
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Viewer clipboard delivery failed for device {DeviceId}.", deviceId);
            await DetachViewerAsync(deviceId);
        }
    }

    public Task<bool> AttachViewerAsync(
        string deviceId,
        string userName,
        bool canControl,
        Func<byte[], CancellationToken, Task> publishFrameAsync,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task>? publishStatusAsync,
        Func<AgentClipboardMessage, CancellationToken, Task>? publishClipboardAsync,
        CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.AgentSocket.State != WebSocketState.Open)
        {
            _ = _auditService.WriteAsync(
                "viewer-session-open",
                userName,
                userName,
                "device",
                deviceId,
                false,
                HostUiText.Bi("Viewer 工作階段開啟失敗，因為裝置目前離線。", "Viewer session open failed because the device is offline."),
                CancellationToken.None);
            return Task.FromResult(false);
        }

        if (!session.IsAuthorized)
        {
            _ = _auditService.WriteAsync(
                "viewer-session-open",
                userName,
                userName,
                "device",
                deviceId,
                false,
                HostUiText.Bi("Viewer 工作階段開啟失敗，因為裝置仍在等待核准。", "Viewer session open failed because the device is waiting for approval."),
                CancellationToken.None);
            return Task.FromResult(false);
        }

        lock (session.SyncRoot)
        {
            if (session.ViewerSession is not null)
            {
                _ = _auditService.WriteAsync(
                    "viewer-session-open",
                    userName,
                    userName,
                    "device",
                    deviceId,
                    false,
                    HostUiText.Bi("Viewer 工作階段開啟失敗，因為已有其他 Viewer 連線。", "Viewer session open failed because another viewer is already attached."),
                    CancellationToken.None);
                return Task.FromResult(false);
            }

            session.ViewerSession = new ViewerSession(userName, canControl, publishFrameAsync, publishStatusAsync, publishClipboardAsync);
        }

        _logger.LogInformation("Viewer attached: {DeviceId} by {UserName}", deviceId, userName);
        _ = _auditService.WriteAsync(
            "viewer-session-open",
            userName,
            userName,
            "device",
            deviceId,
            true,
            HostUiText.Bi("Viewer 工作階段已成功開啟。", "Viewer session opened successfully."),
            CancellationToken.None);
        return Task.FromResult(true);
    }

    public async Task<bool> SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedByUserName, CancellationToken cancellationToken)
    {
        var device = await _repository.GetDeviceAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return false;
        }

        await _repository.SetDeviceAuthorizationAsync(deviceId, isAuthorized, changedByUserName, cancellationToken);

        if (_agents.TryGetValue(deviceId, out var session))
        {
            session.IsAuthorized = isAuthorized;
            if (!isAuthorized)
            {
                await DetachViewerAsync(deviceId);
            }
        }

        var action = isAuthorized ? "device-authorization-grant" : "device-authorization-revoke";
        var message = isAuthorized
            ? HostUiText.Bi($"已核准裝置「{device.DeviceName}」的無人值守存取。", $"Approved unattended access for device '{device.DeviceName}'.")
            : HostUiText.Bi($"已撤銷裝置「{device.DeviceName}」的無人值守存取。", $"Revoked unattended access for device '{device.DeviceName}'.");

        await _auditService.WriteAsync(
            action,
            changedByUserName,
            changedByUserName,
            "device",
            deviceId,
            true,
            message,
            cancellationToken);

        return true;
    }

    public Task DetachViewerAsync(string deviceId)
    {
        if (_agents.TryGetValue(deviceId, out var session))
        {
            ViewerSession? viewerSession;
            lock (session.SyncRoot)
            {
                viewerSession = session.ViewerSession;
                session.ViewerSession = null;
            }

            if (viewerSession is not null)
            {
                _ = _auditService.WriteAsync(
                    "viewer-session-close",
                    viewerSession.UserName,
                    viewerSession.UserName,
                    "device",
                    deviceId,
                    true,
                    HostUiText.Bi("Viewer 工作階段已關閉。", "Viewer session closed."),
                    CancellationToken.None);
            }
        }

        return Task.CompletedTask;
    }

    public async Task ForwardViewerCommandAsync(string deviceId, string jsonPayload, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.AgentSocket.State != WebSocketState.Open)
        {
            return;
        }

        ViewerSession? viewerSession;
        lock (session.SyncRoot)
        {
            viewerSession = session.ViewerSession;
        }

        if (viewerSession is null || !session.IsAuthorized || !viewerSession.CanControl)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        await session.SendCommandAsync(bytes, cancellationToken);

        var command = JsonSerializer.Deserialize<ViewerCommandMessage>(jsonPayload, JsonOptions);
        if (command is not null && string.Equals(command.Type, "file-upload-start", StringComparison.OrdinalIgnoreCase))
        {
            _ = _auditService.WriteAsync(
                "file-upload-start",
                viewerSession.UserName,
                viewerSession.UserName,
                "device",
                deviceId,
                true,
                HostUiText.Bi($"開始上傳檔案「{command.FileName}」。", $"Started uploading '{command.FileName}'."),
                CancellationToken.None);
            return;
        }

        if (command is not null && string.Equals(command.Type, "file-download-start", StringComparison.OrdinalIgnoreCase))
        {
            _ = _auditService.WriteAsync(
                "file-download-start",
                viewerSession.UserName,
                viewerSession.UserName,
                "device",
                deviceId,
                true,
                HostUiText.Bi($"開始下載檔案「{command.RemotePath}」。", $"Started downloading '{command.RemotePath}'."),
                CancellationToken.None);
            return;
        }

        if (command is not null && string.Equals(command.Type, "clipboard-set", StringComparison.OrdinalIgnoreCase))
        {
            _ = _auditService.WriteAsync(
                "clipboard-set-start",
                viewerSession.UserName,
                viewerSession.UserName,
                "device",
                deviceId,
                true,
                HostUiText.Bi("已開始將本機剪貼簿同步到遠端裝置。", "Started syncing local clipboard to the remote device."),
                CancellationToken.None);
            return;
        }

        if (command is not null && string.Equals(command.Type, "clipboard-get", StringComparison.OrdinalIgnoreCase))
        {
            _ = _auditService.WriteAsync(
                "clipboard-get-start",
                viewerSession.UserName,
                viewerSession.UserName,
                "device",
                deviceId,
                true,
                HostUiText.Bi("已要求取得遠端剪貼簿內容。", "Requested the remote clipboard contents."),
                CancellationToken.None);
            return;
        }

        if (!viewerSession.InputActivityLogged)
        {
            lock (session.SyncRoot)
            {
                if (!viewerSession.InputActivityLogged)
                {
                    viewerSession.InputActivityLogged = true;
                    _ = _auditService.WriteAsync(
                        "viewer-remote-control",
                        viewerSession.UserName,
                        viewerSession.UserName,
                        "device",
                        deviceId,
                        true,
                        HostUiText.Bi("已將遠端控制輸入送到裝置。", "Remote control input was sent to the device."),
                        CancellationToken.None);
                }
            }
        }
    }

    public Task ForwardViewerCommandAsync(string deviceId, ViewerCommandMessage command, CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(command, JsonOptions);
        return ForwardViewerCommandAsync(deviceId, jsonPayload, cancellationToken);
    }

    public async Task DisconnectAgentAsync(string deviceId, string reason, CancellationToken cancellationToken)
    {
        if (!_agents.TryRemove(deviceId, out var session))
        {
            return;
        }

        await session.CloseAsync(reason, cancellationToken);
        await _repository.ClosePresenceAsync(session.PresenceId, session.DeviceId, reason, cancellationToken);
        _logger.LogInformation("Agent disconnected: {DeviceId}, reason: {Reason}", deviceId, reason);
    }

    public async Task DisconnectStaleAgentsAsync(DateTimeOffset staleBefore, CancellationToken cancellationToken)
    {
        var staleAgentIds = _agents.ToArray()
            .Where(entry => entry.Value.LastHeartbeatAt < staleBefore)
            .Select(static entry => entry.Key)
            .ToArray();

        if (staleAgentIds.Length == 0)
        {
            return;
        }

        await Task.WhenAll(staleAgentIds.Select(deviceId => DisconnectAgentAsync(deviceId, "heartbeat-timeout", cancellationToken)));
    }

    private static bool FixedTimeEquals(string? left, string? right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left ?? string.Empty));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }

    public sealed class AgentRegistrationResult
    {
        private AgentRegistrationResult(bool accepted, bool unauthorized, bool invalid, AgentSession? session)
        {
            Accepted = accepted;
            Unauthorized = unauthorized;
            Invalid = invalid;
            Session = session;
        }

        public bool Accepted { get; }

        public bool Unauthorized { get; }

        public bool Invalid { get; }

        public AgentSession? Session { get; }

        public static AgentRegistrationResult Success(AgentSession session) => new(true, false, false, session);

        public static AgentRegistrationResult CreateUnauthorized() => new(false, true, false, null);

        public static AgentRegistrationResult CreateInvalid() => new(false, false, true, null);
    }

    public sealed class AgentSession
    {
        public AgentSession(WebSocket agentSocket, AgentDescriptor descriptor, Guid presenceId, bool isAuthorized)
        {
            AgentSocket = agentSocket;
            PresenceId = presenceId;
            DeviceId = descriptor.DeviceId;
            DeviceName = descriptor.DeviceName;
            HostName = descriptor.HostName;
            IsAuthorized = isAuthorized;
            LastHeartbeatAt = DateTimeOffset.UtcNow;
        }

        public object SyncRoot { get; } = new();

        public WebSocket AgentSocket { get; }

        public SemaphoreSlim SendLock { get; } = new(1, 1);

        public Guid PresenceId { get; }

        public string DeviceId { get; }

        public string DeviceName { get; }

        public string HostName { get; }

        public bool IsAuthorized { get; set; }

        public ViewerSession? ViewerSession { get; set; }

        public DateTimeOffset LastHeartbeatAt { get; set; }

        public async Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            await SendLock.WaitAsync(cancellationToken);
            try
            {
                if (AgentSocket.State == WebSocketState.Open)
                {
                    await AgentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken);
                }
            }
            finally
            {
                SendLock.Release();
            }
        }

        public async Task SendCommandAsync(byte[] payload, CancellationToken cancellationToken)
        {
            await SendLock.WaitAsync(cancellationToken);
            try
            {
                await AgentSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                SendLock.Release();
            }
        }
    }

    public sealed class ViewerSession
    {
        private readonly Func<byte[], CancellationToken, Task> _publishFrameAsync;
        private readonly Func<AgentFileTransferStatusMessage, CancellationToken, Task>? _publishStatusAsync;
        private readonly Func<AgentClipboardMessage, CancellationToken, Task>? _publishClipboardAsync;

        public ViewerSession(
            string userName,
            bool canControl,
            Func<byte[], CancellationToken, Task> publishFrameAsync,
            Func<AgentFileTransferStatusMessage, CancellationToken, Task>? publishStatusAsync,
            Func<AgentClipboardMessage, CancellationToken, Task>? publishClipboardAsync)
        {
            UserName = userName;
            CanControl = canControl;
            _publishFrameAsync = publishFrameAsync;
            _publishStatusAsync = publishStatusAsync;
            _publishClipboardAsync = publishClipboardAsync;
        }

        public string UserName { get; }
        public bool CanControl { get; }

        public bool InputActivityLogged { get; set; }

        public Task PublishFrameAsync(byte[] payload, CancellationToken cancellationToken)
        {
            return _publishFrameAsync(payload, cancellationToken);
        }

        public Task PublishStatusAsync(AgentFileTransferStatusMessage status, CancellationToken cancellationToken)
        {
            return _publishStatusAsync is null
                ? Task.CompletedTask
                : _publishStatusAsync(status, cancellationToken);
        }

        public Task PublishClipboardAsync(AgentClipboardMessage message, CancellationToken cancellationToken)
        {
            return _publishClipboardAsync is null
                ? Task.CompletedTask
                : _publishClipboardAsync(message, cancellationToken);
        }
    }
}

