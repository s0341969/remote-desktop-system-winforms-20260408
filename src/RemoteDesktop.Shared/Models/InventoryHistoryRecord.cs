namespace RemoteDesktop.Shared.Models;

public sealed class InventoryHistoryRecord
{
    public Guid HistoryId { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string InventoryFingerprint { get; init; } = string.Empty;

    public string ChangeSummary { get; init; } = string.Empty;

    public DateTimeOffset CollectedAt { get; init; }

    public DateTimeOffset RecordedAt { get; init; }

    public AgentInventoryProfile Inventory { get; init; } = new();
}
