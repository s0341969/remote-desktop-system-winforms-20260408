namespace RemoteDesktop.Shared.Models;

public sealed class LoginRequest
{
    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
