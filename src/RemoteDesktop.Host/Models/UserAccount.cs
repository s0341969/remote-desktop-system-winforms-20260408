namespace RemoteDesktop.Host.Models;

public sealed class UserAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public UserRole Role { get; init; } = UserRole.Operator;

    public bool IsEnabled { get; init; } = true;

    public string PasswordHash { get; init; } = string.Empty;

    public string PasswordSalt { get; init; } = string.Empty;

    public int PasswordIterations { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; init; }
}
