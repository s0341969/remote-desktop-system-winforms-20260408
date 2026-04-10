using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Host.Services.Auditing;
using RemoteDesktop.Host.Services.Users;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Host.Services;

public sealed class RemoteAuthenticationService : IAuthenticationService, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public RemoteAuthenticationService(IOptions<ControlServerOptions> options)
    {
        var baseAddress = NormalizeBaseAddress(options.Value.CentralServerUrl!);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<AuthenticatedUserSession?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            UserName = userName,
            Password = password
        }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<UserSessionDto>(cancellationToken: cancellationToken);
        return dto is null ? null : MapSession(dto);
    }

    public async Task<IReadOnlyList<UserAccount>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/api/users", cancellationToken);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<UserAccountDto>>(cancellationToken: cancellationToken);
        return items?.Select(MapUserAccount).ToList() ?? [];
    }

    public async Task SaveAccountAsync(UserAccountEditorModel model, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/users", new UserAccountUpsertRequest
        {
            UserName = model.UserName,
            DisplayName = model.DisplayName,
            Role = model.Role.ToString(),
            IsEnabled = model.IsEnabled,
            Password = model.Password
        }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ValidationException(message);
        }

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAccountAsync(string userName, string currentUserName, CancellationToken cancellationToken)
    {
        var encodedUserName = Uri.EscapeDataString(userName);
        var encodedCurrentUser = Uri.EscapeDataString(currentUserName);
        var response = await _httpClient.DeleteAsync($"/api/users/{encodedUserName}?currentUserName={encodedCurrentUser}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new ValidationException(message);
        }

        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private static string NormalizeBaseAddress(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    private static AuthenticatedUserSession MapSession(UserSessionDto dto)
    {
        return new AuthenticatedUserSession
        {
            UserName = dto.UserName,
            DisplayName = dto.DisplayName,
            Role = Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role) ? role : UserRole.Viewer
        };
    }

    private static UserAccount MapUserAccount(UserAccountDto dto)
    {
        return new UserAccount
        {
            Id = dto.Id,
            UserName = dto.UserName,
            DisplayName = dto.DisplayName,
            Role = Enum.TryParse<UserRole>(dto.Role, ignoreCase: true, out var role) ? role : UserRole.Viewer,
            IsEnabled = dto.IsEnabled,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            LastLoginAt = dto.LastLoginAt
        };
    }
}

public sealed class RemoteAuditService : IAuditService, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public RemoteAuditService(IOptions<ControlServerOptions> options)
    {
        var baseAddress = NormalizeBaseAddress(options.Value.CentralServerUrl!);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/api/audit-logs?take={Math.Clamp(take, 1, 1000)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<AuditLogEntryDto>>(cancellationToken: cancellationToken);
        return items?.Select(MapAuditLogEntry).ToList() ?? [];
    }

    public async Task WriteAsync(string action, string actorUserName, string actorDisplayName, string targetType, string? targetId, bool succeeded, string details, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/audit-logs", new AuditLogEntryDto
        {
            OccurredAt = DateTimeOffset.UtcNow,
            ActorUserName = actorUserName,
            ActorDisplayName = actorDisplayName,
            Action = action,
            TargetType = targetType,
            TargetId = targetId ?? string.Empty,
            Succeeded = succeeded,
            Details = details
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public Task WriteAsync(string action, AuthenticatedUserSession actor, string targetType, string? targetId, bool succeeded, string details, CancellationToken cancellationToken)
    {
        return WriteAsync(action, actor.UserName, actor.DisplayName, targetType, targetId, succeeded, details, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private static string NormalizeBaseAddress(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    private static AuditLogEntry MapAuditLogEntry(AuditLogEntryDto dto)
    {
        return new AuditLogEntry
        {
            Id = dto.Id,
            OccurredAt = dto.OccurredAt,
            ActorUserName = dto.ActorUserName,
            ActorDisplayName = dto.ActorDisplayName,
            Action = dto.Action,
            TargetType = dto.TargetType,
            TargetId = dto.TargetId,
            Succeeded = dto.Succeeded,
            Details = dto.Details
        };
    }
}
