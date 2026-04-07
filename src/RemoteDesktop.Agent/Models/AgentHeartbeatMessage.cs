namespace RemoteDesktop.Agent.Models;

public sealed class AgentHeartbeatMessage
{
    public string Type { get; init; } = "heartbeat";

    public int ScreenWidth { get; init; }

    public int ScreenHeight { get; init; }
}
