namespace RemoteDesktop.Host.Models;

public sealed class AgentHeartbeatMessage
{
    public string Type { get; init; } = string.Empty;

    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }
}
