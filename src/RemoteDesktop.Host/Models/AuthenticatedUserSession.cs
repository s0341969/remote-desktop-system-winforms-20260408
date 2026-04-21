namespace RemoteDesktop.Host.Models;

public sealed class AuthenticatedUserSession
{
    public required string UserName { get; init; }

    public required string DisplayName { get; init; }

    public required UserRole Role { get; init; }

    public string? AccessToken { get; init; }

    public DateTimeOffset? AccessTokenExpiresAt { get; init; }

    public bool CanManageSettings => Role == UserRole.Administrator;

    public bool CanManageUsers => Role == UserRole.Administrator;

    public bool CanViewAuditLogs => Role == UserRole.Administrator;

    public bool CanManageDeviceAuthorization => Role == UserRole.Administrator;

    public bool CanControlRemote => Role == UserRole.Administrator;

    public string RoleDisplayName => Role.ToString();
}
