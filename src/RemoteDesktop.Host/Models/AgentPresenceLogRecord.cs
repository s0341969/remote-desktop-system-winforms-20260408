namespace RemoteDesktop.Host.Models;

public sealed class AgentPresenceLogRecord
{
    public Guid PresenceId { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public DateTimeOffset ConnectedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }

    public DateTimeOffset? DisconnectedAt { get; init; }

    public string? DisconnectReason { get; init; }

    public long OnlineSeconds { get; init; }
}
