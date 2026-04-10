namespace RemoteDesktop.Shared.Models;

public sealed class UserAccountDto
{
    public Guid Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }
}
