namespace RemoteDesktop.Shared.Models;

public sealed class AuditLogEntryDto
{
    public Guid Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public string ActorUserName { get; init; } = string.Empty;

    public string ActorDisplayName { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string TargetType { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public bool Succeeded { get; init; }

    public string Details { get; init; } = string.Empty;
}
