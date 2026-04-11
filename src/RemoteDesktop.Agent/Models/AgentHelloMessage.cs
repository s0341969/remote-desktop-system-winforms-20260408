namespace RemoteDesktop.Agent.Models;

public sealed class AgentHelloMessage
{
    public string Type { get; init; } = "hello";

    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public string HostName { get; init; } = string.Empty;

    public string AgentVersion { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }

    public AgentInventoryProfile? Inventory { get; init; }
}
