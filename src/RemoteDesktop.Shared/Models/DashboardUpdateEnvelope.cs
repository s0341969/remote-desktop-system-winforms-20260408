namespace RemoteDesktop.Shared.Models;

public sealed class DashboardUpdateEnvelope
{
    public string Type { get; init; } = string.Empty;

    public string? Reason { get; init; }

    public string? DeviceId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}
