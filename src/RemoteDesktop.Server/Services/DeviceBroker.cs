using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services;

public sealed class DeviceBroker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, AgentSession> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeviceRepository _repository;
    private readonly ControlServerOptions _options;
    private readonly DashboardUpdateHub _dashboardUpdateHub;
    private readonly ILogger<DeviceBroker> _logger;

    public DeviceBroker(IDeviceRepository repository, IOptions<ControlServerOptions> options, DashboardUpdateHub dashboardUpdateHub, ILogger<DeviceBroker> logger)
    {
        _repository = repository;
        _options = options.Value;
        _dashboardUpdateHub = dashboardUpdateHub;
        _logger = logger;
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
        var deviceRecord = await _repository.GetDeviceAsync(descriptor.DeviceId, cancellationToken);
        var presenceId = await _repository.StartPresenceAsync(descriptor, cancellationToken);

        var session = new AgentSession(socket, descriptor, presenceId, deviceRecord?.IsAuthorized == true);
        _agents[descriptor.DeviceId] = session;
        _dashboardUpdateHub.Publish("device-online", descriptor.DeviceId);
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

    public async Task PublishFrameAsync(string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        ViewerSession[] viewerSessions;
        lock (session.SyncRoot)
        {
            viewerSessions = session.ViewerSessions.Values.ToArray();
        }

        if (viewerSessions.Length == 0)
        {
            return;
        }

        foreach (var viewerSession in viewerSessions)
        {
            try
            {
                await viewerSession.PublishFrameAsync(payload, cancellationToken);
                session.LastHeartbeatAt = DateTimeOffset.UtcNow;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Viewer {ViewerSessionId} disconnected unexpectedly for device {DeviceId}.", viewerSession.SessionId, deviceId);
                await DetachViewerAsync(deviceId, viewerSession.SessionId);
            }
        }
    }

    public async Task PublishViewerStatusAsync(string deviceId, AgentFileTransferStatusMessage status, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        ViewerSession[] viewerSessions;
        lock (session.SyncRoot)
        {
            viewerSessions = session.ViewerSessions.Values.ToArray();
        }

        if (viewerSessions.Length == 0)
        {
            return;
        }

        foreach (var viewerSession in viewerSessions)
        {
            try
            {
                await viewerSession.PublishStatusAsync(status, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Viewer status delivery failed for session {ViewerSessionId} on device {DeviceId}.", viewerSession.SessionId, deviceId);
                await DetachViewerAsync(deviceId, viewerSession.SessionId);
            }
        }
    }

    public async Task PublishViewerClipboardAsync(string deviceId, AgentClipboardMessage message, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return;
        }

        ViewerSession[] viewerSessions;
        lock (session.SyncRoot)
        {
            viewerSessions = session.ViewerSessions.Values.ToArray();
        }

        if (viewerSessions.Length == 0)
        {
            return;
        }

        foreach (var viewerSession in viewerSessions)
        {
            try
            {
                await viewerSession.PublishClipboardAsync(message, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Viewer clipboard delivery failed for session {ViewerSessionId} on device {DeviceId}.", viewerSession.SessionId, deviceId);
                await DetachViewerAsync(deviceId, viewerSession.SessionId);
            }
        }
    }

    public async Task<ViewerAttachResult> AttachViewerAsync(
        string deviceId,
        string userName,
        bool canControl,
        bool forceTakeover,
        Func<byte[], CancellationToken, Task> publishFrameAsync,
        Func<AgentFileTransferStatusMessage, CancellationToken, Task>? publishStatusAsync,
        Func<AgentClipboardMessage, CancellationToken, Task>? publishClipboardAsync,
        Func<ViewerSessionState, CancellationToken, Task>? publishSessionStateAsync,
        Func<string, CancellationToken, Task>? closeViewerAsync,
        CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.AgentSocket.State != WebSocketState.Open)
        {
            return ViewerAttachResult.Rejected("device-unavailable", "The selected device is offline.");
        }

        if (!session.IsAuthorized)
        {
            return ViewerAttachResult.Rejected("device-pending-authorization", "The selected device is waiting for authorization.");
        }

        ViewerSession? displacedController = null;
        ViewerSession attachedViewer;
        ViewerSessionState attachedState;

        lock (session.SyncRoot)
        {
            var controller = GetControllingViewerSession(session);
            var sessionId = Guid.NewGuid().ToString("N");
            var grantedControl = false;

            if (canControl)
            {
                if (controller is null)
                {
                    grantedControl = true;
                }
                else if (forceTakeover)
                {
                    controller.CanControl = false;
                    displacedController = controller;
                    grantedControl = true;
                }
            }

            attachedViewer = new ViewerSession(
                sessionId,
                userName,
                grantedControl,
                publishFrameAsync,
                publishStatusAsync,
                publishClipboardAsync,
                publishSessionStateAsync,
                closeViewerAsync);

            session.ViewerSessions[sessionId] = attachedViewer;
            attachedState = BuildViewerSessionState(session, attachedViewer, grantedControl && displacedController is not null
                ? $"控制權已從「{displacedController.UserName}」移轉。 / Control was taken over from '{displacedController.UserName}'."
                : controller is not null && !grantedControl && canControl
                    ? $"目前由「{controller.UserName}」控制，已切換為僅觀看。 / Currently controlled by '{controller.UserName}'. Switched to observe-only."
                    : grantedControl
                        ? "已取得控制權。 / Control granted."
                        : "已加入僅觀看工作階段。 / Joined as observe-only.");
        }

        if (displacedController is not null)
        {
            await displacedController.PublishSessionStateAsync(
                new ViewerSessionState(
                    false,
                    attachedViewer.UserName,
                    $"控制權已被「{attachedViewer.UserName}」接管，已切換為僅觀看。 / Control was taken over by '{attachedViewer.UserName}'. Switched to observe-only."),
                cancellationToken);
        }

        _logger.LogInformation(
            "Viewer attached: {DeviceId} by {UserName}, mode={Mode}",
            deviceId,
            userName,
            attachedViewer.CanControl ? "control" : "observe");
        return ViewerAttachResult.Accepted(attachedViewer.SessionId, attachedViewer.CanControl, attachedState.ControllerDisplayName, attachedState.Message);
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

        _dashboardUpdateHub.Publish("device-authorization-changed", deviceId);
        return true;
    }

    public Task DetachViewerAsync(string deviceId)
    {
        return DetachViewerAsync(deviceId, viewerSessionId: null);
    }

    public async Task DetachViewerAsync(string deviceId, string? viewerSessionId)
    {
        if (_agents.TryGetValue(deviceId, out var session))
        {
            ViewerSession? detachedSession = null;
            ViewerSession? promotedObserver = null;

            lock (session.SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(viewerSessionId))
                {
                    foreach (var key in session.ViewerSessions.Keys.ToArray())
                    {
                        session.ViewerSessions.TryRemove(key, out _);
                    }

                    return;
                }

                if (!session.ViewerSessions.TryRemove(viewerSessionId, out detachedSession) || detachedSession is null)
                {
                    return;
                }

                if (detachedSession.CanControl)
                {
                    promotedObserver = session.ViewerSessions.Values
                        .OrderBy(static item => item.ConnectedAt)
                        .FirstOrDefault();

                    if (promotedObserver is not null)
                    {
                        promotedObserver.CanControl = true;
                    }
                }
            }

            if (promotedObserver is not null)
            {
                await promotedObserver.PublishSessionStateAsync(
                    BuildViewerSessionState(session, promotedObserver, "控制權已釋出，您現在可控制此裝置。 / Control was released and you can now control this device."),
                    CancellationToken.None);
            }
        }
    }

    public Task ForwardViewerCommandAsync(string deviceId, ViewerCommandMessage command, CancellationToken cancellationToken)
    {
        var jsonPayload = JsonSerializer.Serialize(command, JsonOptions);
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return Task.CompletedTask;
        }

        ViewerSession? controller;
        lock (session.SyncRoot)
        {
            controller = GetControllingViewerSession(session);
        }

        return controller is null
            ? Task.CompletedTask
            : ForwardViewerCommandAsync(deviceId, controller.SessionId, jsonPayload, cancellationToken);
    }

    public async Task<ViewerSessionState> RequestViewerControlAsync(string deviceId, string viewerSessionId, bool forceTakeover, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.AgentSocket.State != WebSocketState.Open)
        {
            return new ViewerSessionState(false, null, "裝置目前離線。 / The device is offline.");
        }

        ViewerSession? requestingViewer;
        ViewerSession? currentController;
        ViewerSession? displacedController = null;
        ViewerSessionState resultState;

        lock (session.SyncRoot)
        {
            if (!session.ViewerSessions.TryGetValue(viewerSessionId, out requestingViewer) || requestingViewer is null)
            {
                return new ViewerSessionState(false, null, "Viewer 工作階段不存在。 / Viewer session not found.");
            }

            currentController = GetControllingViewerSession(session);
            if (currentController is null || string.Equals(currentController.SessionId, requestingViewer.SessionId, StringComparison.Ordinal))
            {
                requestingViewer.CanControl = true;
                resultState = BuildViewerSessionState(session, requestingViewer, "已取得控制權。 / Control granted.");
            }
            else if (!forceTakeover)
            {
                requestingViewer.CanControl = false;
                resultState = BuildViewerSessionState(session, requestingViewer, $"目前由「{currentController.UserName}」控制。 / Currently controlled by '{currentController.UserName}'.");
            }
            else
            {
                currentController.CanControl = false;
                requestingViewer.CanControl = true;
                displacedController = currentController;
                resultState = BuildViewerSessionState(session, requestingViewer, $"已接管「{currentController.UserName}」的控制權。 / Took control from '{currentController.UserName}'.");
            }
        }

        if (displacedController is not null)
        {
            await displacedController.PublishSessionStateAsync(
                new ViewerSessionState(false, requestingViewer!.UserName, $"控制權已被「{requestingViewer.UserName}」接管，已切換為僅觀看。 / Control was taken over by '{requestingViewer.UserName}'. Switched to observe-only."),
                cancellationToken);
        }

        return resultState;
    }

    public async Task ForwardViewerCommandAsync(string deviceId, string viewerSessionId, string jsonPayload, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.AgentSocket.State != WebSocketState.Open)
        {
            return;
        }

        ViewerSession? viewerSession;
        lock (session.SyncRoot)
        {
            session.ViewerSessions.TryGetValue(viewerSessionId, out viewerSession);
        }

        if (viewerSession is null || !session.IsAuthorized || !viewerSession.CanControl)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        await session.SendCommandAsync(bytes, cancellationToken);
        viewerSession.InputActivityLogged = true;
    }

    private static ViewerSession? GetControllingViewerSession(AgentSession session)
    {
        return session.ViewerSessions.Values.FirstOrDefault(static item => item.CanControl);
    }

    private static ViewerSessionState BuildViewerSessionState(AgentSession session, ViewerSession currentViewer, string? message)
    {
        var controller = GetControllingViewerSession(session);
        return new ViewerSessionState(
            currentViewer.CanControl,
            controller?.UserName,
            message);
    }

    public async Task DisconnectAgentAsync(string deviceId, string reason, CancellationToken cancellationToken)
    {
        if (!_agents.TryRemove(deviceId, out var session))
        {
            return;
        }

        try
        {
            using var closeTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            closeTimeoutSource.CancelAfter(TimeSpan.FromSeconds(2));
            await session.CloseAsync(reason, closeTimeoutSource.Token);
        }
        catch (WebSocketException exception)
        {
            _logger.LogWarning(exception, "Closing agent socket failed for {DeviceId}. Continuing repository cleanup.", deviceId);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            session.Abort();
            _logger.LogWarning(exception, "Closing agent socket timed out for {DeviceId}. Socket was aborted and repository cleanup will continue.", deviceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unexpected error while closing agent socket for {DeviceId}. Continuing repository cleanup.", deviceId);
        }
        finally
        {
            await _repository.ClosePresenceAsync(session.PresenceId, session.DeviceId, reason, cancellationToken);
            _dashboardUpdateHub.Publish("device-offline", deviceId);
            _logger.LogInformation("Agent disconnected: {DeviceId}, reason: {Reason}", deviceId, reason);
        }
    }

    public Task SweepStaleAgentsAsync(CancellationToken cancellationToken)
    {
        var staleBefore = DateTimeOffset.UtcNow.AddSeconds(-_options.AgentHeartbeatTimeoutSeconds);
        return DisconnectStaleAgentsAsync(staleBefore, cancellationToken);
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
        public ConcurrentDictionary<string, ViewerSession> ViewerSessions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Guid PresenceId { get; }
        public string DeviceId { get; }
        public string DeviceName { get; }
        public string HostName { get; }
        public bool IsAuthorized { get; set; }
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

        public void Abort()
        {
            try
            {
                AgentSocket.Abort();
            }
            catch
            {
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
        private readonly Func<ViewerSessionState, CancellationToken, Task>? _publishSessionStateAsync;
        private readonly Func<string, CancellationToken, Task>? _closeViewerAsync;

        public ViewerSession(
            string sessionId,
            string userName,
            bool canControl,
            Func<byte[], CancellationToken, Task> publishFrameAsync,
            Func<AgentFileTransferStatusMessage, CancellationToken, Task>? publishStatusAsync,
            Func<AgentClipboardMessage, CancellationToken, Task>? publishClipboardAsync,
            Func<ViewerSessionState, CancellationToken, Task>? publishSessionStateAsync,
            Func<string, CancellationToken, Task>? closeViewerAsync)
        {
            SessionId = sessionId;
            UserName = userName;
            CanControl = canControl;
            _publishFrameAsync = publishFrameAsync;
            _publishStatusAsync = publishStatusAsync;
            _publishClipboardAsync = publishClipboardAsync;
            _publishSessionStateAsync = publishSessionStateAsync;
            _closeViewerAsync = closeViewerAsync;
            ConnectedAt = DateTimeOffset.UtcNow;
        }

        public string SessionId { get; }
        public string UserName { get; }
        public bool CanControl { get; set; }
        public DateTimeOffset ConnectedAt { get; }
        public bool InputActivityLogged { get; set; }
        public Func<ViewerSessionState, CancellationToken, Task>? PublishSessionStateAsyncDelegate => _publishSessionStateAsync;
        public Task PublishFrameAsync(byte[] payload, CancellationToken cancellationToken) => _publishFrameAsync(payload, cancellationToken);
        public Task PublishStatusAsync(AgentFileTransferStatusMessage status, CancellationToken cancellationToken) => _publishStatusAsync is null ? Task.CompletedTask : _publishStatusAsync(status, cancellationToken);
        public Task PublishClipboardAsync(AgentClipboardMessage message, CancellationToken cancellationToken) => _publishClipboardAsync is null ? Task.CompletedTask : _publishClipboardAsync(message, cancellationToken);
        public Task PublishSessionStateAsync(ViewerSessionState state, CancellationToken cancellationToken) => _publishSessionStateAsync is null ? Task.CompletedTask : _publishSessionStateAsync(state, cancellationToken);
        public Task CloseAsync(string reason, CancellationToken cancellationToken) => _closeViewerAsync is null ? Task.CompletedTask : _closeViewerAsync(reason, cancellationToken);
    }

    public sealed record ViewerAttachResult(bool Attached, string? ViewerSessionId, bool CanControl, string? ControllerDisplayName, string? Message, string? FailureCode)
    {
        public static ViewerAttachResult Accepted(string viewerSessionId, bool canControl, string? controllerDisplayName, string? message)
            => new(true, viewerSessionId, canControl, controllerDisplayName, message, null);

        public static ViewerAttachResult Rejected(string failureCode, string message)
            => new(false, null, false, null, message, failureCode);
    }

    public sealed record ViewerSessionState(bool CanControl, string? ControllerDisplayName, string? Message);
}




