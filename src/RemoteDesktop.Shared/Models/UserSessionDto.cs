namespace RemoteDesktop.Shared.Models;

public sealed class UserSessionDto
{
    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public DateTimeOffset? AccessTokenExpiresAt { get; init; }
}
