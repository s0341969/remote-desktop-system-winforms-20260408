using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services.Security;

public sealed class ConsoleSessionTokenService
{
    private readonly ConcurrentDictionary<string, ConsoleSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeSpan _sessionLifetime;

    public ConsoleSessionTokenService(IOptions<ControlServerOptions> options)
    {
        _sessionLifetime = TimeSpan.FromHours(12);
    }

    public UserSessionDto IssueToken(UserSessionDto session)
    {
        var accessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTimeOffset.UtcNow.Add(_sessionLifetime);
        _sessions[accessToken] = new ConsoleSession(
            accessToken,
            session.UserName,
            session.DisplayName,
            session.Role,
            expiresAt);

        return new UserSessionDto
        {
            UserName = session.UserName,
            DisplayName = session.DisplayName,
            Role = session.Role,
            AccessToken = accessToken,
            AccessTokenExpiresAt = expiresAt
        };
    }

    public bool TryAuthenticate(HttpRequest request, out ConsoleSession session)
    {
        session = ConsoleSession.Empty;
        if (!TryReadBearerToken(request, out var token))
        {
            return false;
        }

        if (!_sessions.TryGetValue(token, out var current))
        {
            return false;
        }

        if (current.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        session = current;
        return true;
    }

    public bool IsInRole(ConsoleSession session, string role)
    {
        return string.Equals(session.Role, role, StringComparison.OrdinalIgnoreCase);
    }

    public bool CanControlRemote(ConsoleSession session)
    {
        return session.Role is "Administrator" or "Operator";
    }

    private static bool TryReadBearerToken(HttpRequest request, out string token)
    {
        token = string.Empty;
        if (!request.Headers.TryGetValue("Authorization", out var values))
        {
            return false;
        }

        var value = values.ToString();
        const string prefix = "Bearer ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = value[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}

public readonly record struct ConsoleSession(
    string AccessToken,
    string UserName,
    string DisplayName,
    string Role,
    DateTimeOffset ExpiresAt)
{
    public static ConsoleSession Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, DateTimeOffset.MinValue);
}
