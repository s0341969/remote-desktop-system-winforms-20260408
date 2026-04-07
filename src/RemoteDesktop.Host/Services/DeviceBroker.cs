using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Services;

public sealed class DeviceBroker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, AgentSession> _agents = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeviceRepository _repository;
    private readonly ControlServerOptions _options;
    private readonly ILogger<DeviceBroker> _logger;

    public DeviceBroker(IDeviceRepository repository, IOptions<ControlServerOptions> options, ILogger<DeviceBroker> logger)
    {
        _repository = repository;
        _options = options.Value;
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
            DeviceId = message.DeviceId.Trim(),
            DeviceName = message.DeviceName.Trim(),
            HostName = message.HostName.Trim(),
            AgentVersion = message.AgentVersion.Trim(),
            ScreenWidth = message.ScreenWidth,
            ScreenHeight = message.ScreenHeight
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
        var presenceId = await _repository.StartPresenceAsync(descriptor, cancellationToken);

        var session = new AgentSession(socket, descriptor, presenceId);
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

    public async Task PublishFrameAsync(string deviceId, byte[] payload, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.ViewerSocket is null)
        {
            return;
        }

        if (session.ViewerSocket.State != WebSocketState.Open)
        {
            session.ViewerSocket = null;
            return;
        }

        await session.ViewerSocket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
        session.LastHeartbeatAt = DateTimeOffset.UtcNow;
    }

    public async Task<bool> AttachViewerAsync(string deviceId, string userName, WebSocket viewerSocket, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session))
        {
            return false;
        }

        if (session.ViewerSocket is not null && session.ViewerSocket.State == WebSocketState.Open)
        {
            return false;
        }

        session.ViewerSocket = viewerSocket;
        session.ViewerUserName = userName;

        var readyPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            type = "viewer-ready",
            deviceId = session.DeviceId,
            deviceName = session.DeviceName,
            hostName = session.HostName
        }, JsonOptions));

        await viewerSocket.SendAsync(readyPayload, WebSocketMessageType.Text, true, cancellationToken);
        return true;
    }

    public Task DetachViewerAsync(string deviceId)
    {
        if (_agents.TryGetValue(deviceId, out var session))
        {
            session.ViewerSocket = null;
            session.ViewerUserName = null;
        }

        return Task.CompletedTask;
    }

    public async Task ForwardViewerCommandAsync(string deviceId, string jsonPayload, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(deviceId, out var session) || session.AgentSocket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(jsonPayload);
        await session.AgentSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
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
        foreach (var entry in _agents.ToArray())
        {
            if (entry.Value.LastHeartbeatAt >= staleBefore)
            {
                continue;
            }

            await DisconnectAgentAsync(entry.Key, "heartbeat-timeout", cancellationToken);
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
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
        public AgentSession(WebSocket agentSocket, AgentDescriptor descriptor, Guid presenceId)
        {
            AgentSocket = agentSocket;
            PresenceId = presenceId;
            DeviceId = descriptor.DeviceId;
            DeviceName = descriptor.DeviceName;
            HostName = descriptor.HostName;
            LastHeartbeatAt = DateTimeOffset.UtcNow;
        }

        public WebSocket AgentSocket { get; }

        public Guid PresenceId { get; }

        public string DeviceId { get; }

        public string DeviceName { get; }

        public string HostName { get; }

        public WebSocket? ViewerSocket { get; set; }

        public string? ViewerUserName { get; set; }

        public DateTimeOffset LastHeartbeatAt { get; set; }

        public async Task CloseAsync(string reason, CancellationToken cancellationToken)
        {
            if (ViewerSocket is { State: WebSocketState.Open })
            {
                await ViewerSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken);
            }

            if (AgentSocket.State == WebSocketState.Open)
            {
                await AgentSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, cancellationToken);
            }
        }
    }
}
