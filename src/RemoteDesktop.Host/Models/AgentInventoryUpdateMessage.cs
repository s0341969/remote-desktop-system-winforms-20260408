namespace RemoteDesktop.Host.Models;

public sealed class AgentInventoryUpdateMessage
{
    public string Type { get; init; } = "inventory-update";

    public AgentInventoryProfile? Inventory { get; init; }
}
