namespace RemoteDesktop.Shared.Models;

public sealed class DeviceRecord
{
    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }

    public bool IsOnline { get; init; }

    public bool IsAuthorized { get; init; }

    public DateTimeOffset? AuthorizedAt { get; init; }

    public string? AuthorizedBy { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset LastSeenAt { get; init; }

    public DateTimeOffset? LastConnectedAt { get; init; }

    public DateTimeOffset? LastDisconnectedAt { get; init; }
}

