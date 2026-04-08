namespace RemoteDesktop.Host.Models;

public sealed class AuditLogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public string ActorUserName { get; init; } = string.Empty;

    public string ActorDisplayName { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string TargetType { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string Details { get; init; } = string.Empty;
}
