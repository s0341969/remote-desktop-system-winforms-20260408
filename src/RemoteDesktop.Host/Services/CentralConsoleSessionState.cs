using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Services;

public sealed class CentralConsoleSessionState
{
    private readonly ControlServerOptions _options;
    private readonly object _syncRoot = new();
    private string? _accessToken;
    private DateTimeOffset? _accessTokenExpiresAt;
    private AuthenticatedUserSession? _currentUser;

    public CentralConsoleSessionState(IOptions<ControlServerOptions> options)
    {
        _options = options.Value;
    }

    public bool IsCentralMode => !string.IsNullOrWhiteSpace(_options.CentralServerUrl);

    public AuthenticatedUserSession? CurrentUser
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentUser;
            }
        }
    }

    public void SetAuthenticatedSession(AuthenticatedUserSession session)
    {
        lock (_syncRoot)
        {
            _currentUser = session;
            _accessToken = string.IsNullOrWhiteSpace(session.AccessToken) ? null : session.AccessToken;
            _accessTokenExpiresAt = session.AccessTokenExpiresAt;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _currentUser = null;
            _accessToken = null;
            _accessTokenExpiresAt = null;
        }
    }

    public void ApplyAuthorizationHeader(HttpRequestMessage request)
    {
        var token = GetRequiredAccessToken();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ApplyAuthorizationHeader(System.Net.WebSockets.ClientWebSocket socket)
    {
        var token = GetRequiredAccessToken();
        socket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
    }

    private string GetRequiredAccessToken()
    {
        lock (_syncRoot)
        {
            if (!IsCentralMode)
            {
                throw new InvalidOperationException("Central server session is not required in local mode.");
            }

            if (string.IsNullOrWhiteSpace(_accessToken))
            {
                throw new InvalidOperationException("尚未登入中央主控台。 / Central console sign-in is required.");
            }

            if (_accessTokenExpiresAt is not null && _accessTokenExpiresAt <= DateTimeOffset.UtcNow)
            {
                throw new InvalidOperationException("中央登入工作階段已過期，請重新登入。 / Central sign-in session has expired. Sign in again.");
            }

            return _accessToken;
        }
    }
}
