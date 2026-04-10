namespace RemoteDesktop.Shared.Models;

public sealed class AgentDescriptor
{
    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }
}

