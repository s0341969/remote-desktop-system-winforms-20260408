namespace RemoteDesktop.Shared.Models;

public sealed class UserAccountUpsertRequest
{
    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public string Password { get; init; } = string.Empty;
}
