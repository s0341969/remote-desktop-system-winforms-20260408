using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services.Users;

public interface IUserAccountStore
{
    Task<IReadOnlyList<ServerUserAccount>> GetAllAsync(CancellationToken cancellationToken);

    Task<ServerUserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);

    Task UpsertAsync(ServerUserAccount account, CancellationToken cancellationToken);

    Task DeleteAsync(string userName, CancellationToken cancellationToken);

    Task UpdateLastLoginAsync(string userName, DateTimeOffset lastLoginAt, CancellationToken cancellationToken);
}

public sealed class JsonUserAccountStore : IUserAccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _usersFilePath;
    private readonly ControlServerOptions _options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public JsonUserAccountStore(IHostEnvironment environment, IOptions<ControlServerOptions> options)
    {
        _usersFilePath = Path.Combine(environment.ContentRootPath, "users.json");
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ServerUserAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var accounts = await ReadOrBootstrapAsync(cancellationToken);
            return accounts.OrderBy(static account => account.UserName, StringComparer.OrdinalIgnoreCase).ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<ServerUserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var accounts = await ReadOrBootstrapAsync(cancellationToken);
            return accounts.FirstOrDefault(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpsertAsync(ServerUserAccount account, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var accounts = await ReadOrBootstrapAsync(cancellationToken);
            var index = accounts.FindIndex(existing => string.Equals(existing.UserName, account.UserName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                accounts[index] = account;
            }
            else
            {
                accounts.Add(account);
            }

            await WriteAsync(accounts, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task DeleteAsync(string userName, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var accounts = await ReadOrBootstrapAsync(cancellationToken);
            accounts.RemoveAll(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase));
            await WriteAsync(accounts, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpdateLastLoginAsync(string userName, DateTimeOffset lastLoginAt, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var accounts = await ReadOrBootstrapAsync(cancellationToken);
            var index = accounts.FindIndex(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return;
            }

            var account = accounts[index];
            accounts[index] = account with
            {
                UpdatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = lastLoginAt
            };

            await WriteAsync(accounts, cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<ServerUserAccount>> ReadOrBootstrapAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_usersFilePath))
        {
            var bootstrap = new List<ServerUserAccount> { CreateBootstrapAdministrator() };
            await WriteAsync(bootstrap, cancellationToken);
            return bootstrap;
        }

        var json = await File.ReadAllTextAsync(_usersFilePath, cancellationToken);
        var accounts = JsonSerializer.Deserialize<List<ServerUserAccount>>(json, JsonOptions) ?? new List<ServerUserAccount>();
        if (accounts.Count > 0)
        {
            return accounts;
        }

        accounts.Add(CreateBootstrapAdministrator());
        await WriteAsync(accounts, cancellationToken);
        return accounts;
    }

    private async Task WriteAsync(List<ServerUserAccount> accounts, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_usersFilePath)!);
        var tempFilePath = $"{_usersFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var json = JsonSerializer.Serialize(accounts, JsonOptions);
            await File.WriteAllTextAsync(tempFilePath, json, Encoding.UTF8, cancellationToken);
            File.Move(tempFilePath, _usersFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private ServerUserAccount CreateBootstrapAdministrator()
    {
        var passwordResult = PasswordHasher.HashPassword(_options.AdminPassword);
        var userName = string.IsNullOrWhiteSpace(_options.AdminUserName) ? "admin" : _options.AdminUserName.Trim();
        var now = DateTimeOffset.UtcNow;

        return new ServerUserAccount(
            Guid.NewGuid(),
            userName,
            "Administrator",
            "Administrator",
            true,
            passwordResult.Hash,
            passwordResult.Salt,
            passwordResult.Iterations,
            now,
            now,
            null);
    }
}

public sealed class UserAccountService
{
    private readonly IUserAccountStore _userAccountStore;

    public UserAccountService(IUserAccountStore userAccountStore)
    {
        _userAccountStore = userAccountStore;
    }

    public async Task<UserSessionDto?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
    {
        var normalizedUserName = userName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var account = await _userAccountStore.FindByUserNameAsync(normalizedUserName, cancellationToken);
        if (account is null || !account.IsEnabled)
        {
            return null;
        }

        if (!PasswordHasher.Verify(password, account.PasswordHash, account.PasswordSalt, account.PasswordIterations))
        {
            return null;
        }

        await _userAccountStore.UpdateLastLoginAsync(account.UserName, DateTimeOffset.UtcNow, cancellationToken);
        return ToSession(account);
    }

    public async Task<IReadOnlyList<UserAccountDto>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = await _userAccountStore.GetAllAsync(cancellationToken);
        return accounts.Select(ToDto).ToList();
    }

    public async Task SaveAccountAsync(UserAccountUpsertRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var accounts = await _userAccountStore.GetAllAsync(cancellationToken);
        var existing = await _userAccountStore.FindByUserNameAsync(request.UserName.Trim(), cancellationToken);
        EnsureEnabledAdministratorRemains(accounts, existing, request);
        var passwordHash = existing?.PasswordHash ?? string.Empty;
        var passwordSalt = existing?.PasswordSalt ?? string.Empty;
        var passwordIterations = existing?.PasswordIterations ?? 0;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var passwordResult = PasswordHasher.HashPassword(request.Password);
            passwordHash = passwordResult.Hash;
            passwordSalt = passwordResult.Salt;
            passwordIterations = passwordResult.Iterations;
        }
        else if (existing is null)
        {
            throw new ValidationException("Password is required for a new account.");
        }

        var now = DateTimeOffset.UtcNow;
        var role = NormalizeRole(request.Role);
        var account = new ServerUserAccount(
            existing?.Id ?? Guid.NewGuid(),
            request.UserName.Trim(),
            request.DisplayName.Trim(),
            role,
            request.IsEnabled,
            passwordHash,
            passwordSalt,
            passwordIterations,
            existing?.CreatedAt ?? now,
            now,
            existing?.LastLoginAt);

        await _userAccountStore.UpsertAsync(account, cancellationToken);
    }

    public async Task DeleteAccountAsync(string userName, string currentUserName, CancellationToken cancellationToken)
    {
        if (string.Equals(userName, currentUserName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("You cannot delete the currently signed-in account.");
        }

        var accounts = await _userAccountStore.GetAllAsync(cancellationToken);
        var target = accounts.FirstOrDefault(account => string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        if (string.Equals(target.Role, "Administrator", StringComparison.OrdinalIgnoreCase) && target.IsEnabled)
        {
            var otherEnabledAdministratorExists = accounts.Any(account =>
                !string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(account.Role, "Administrator", StringComparison.OrdinalIgnoreCase)
                && account.IsEnabled);

            if (!otherEnabledAdministratorExists)
            {
                throw new ValidationException("At least one enabled administrator account must remain.");
            }
        }

        await _userAccountStore.DeleteAsync(userName, cancellationToken);
    }

    private static UserSessionDto ToSession(ServerUserAccount account)
    {
        return new UserSessionDto
        {
            UserName = account.UserName,
            DisplayName = string.IsNullOrWhiteSpace(account.DisplayName) ? account.UserName : account.DisplayName,
            Role = account.Role
        };
    }

    private static UserAccountDto ToDto(ServerUserAccount account)
    {
        return new UserAccountDto
        {
            Id = account.Id,
            UserName = account.UserName,
            DisplayName = account.DisplayName,
            Role = account.Role,
            IsEnabled = account.IsEnabled,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
            LastLoginAt = account.LastLoginAt
        };
    }

    private static void Validate(UserAccountUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            throw new ValidationException("User name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ValidationException("Display name is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 10)
        {
            throw new ValidationException("Password must be at least 10 characters.");
        }
    }

    private static void EnsureEnabledAdministratorRemains(
        IReadOnlyList<ServerUserAccount> accounts,
        ServerUserAccount? existing,
        UserAccountUpsertRequest request)
    {
        if (existing is null || !string.Equals(existing.Role, "Administrator", StringComparison.OrdinalIgnoreCase) || !existing.IsEnabled)
        {
            return;
        }

        if (string.Equals(NormalizeRole(request.Role), "Administrator", StringComparison.OrdinalIgnoreCase) && request.IsEnabled)
        {
            return;
        }

        var otherEnabledAdministratorExists = accounts.Any(account =>
            !string.Equals(account.UserName, existing.UserName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(account.Role, "Administrator", StringComparison.OrdinalIgnoreCase)
            && account.IsEnabled);

        if (!otherEnabledAdministratorExists)
        {
            throw new ValidationException("At least one enabled administrator account must remain.");
        }
    }

    private static string NormalizeRole(string value)
    {
        return value.Trim() switch
        {
            "Administrator" => "Administrator",
            "Operator" => "Operator",
            "Viewer" => "Viewer",
            _ => throw new ValidationException("Role must be Administrator, Operator, or Viewer.")
        };
    }
}

public static class PasswordHasher
{
    public const int SaltSize = 16;
    public const int KeySize = 32;
    public const int DefaultIterations = 100_000;

    public static PasswordHashResult HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            DefaultIterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return new PasswordHashResult(
            Convert.ToBase64String(hash),
            Convert.ToBase64String(salt),
            DefaultIterations);
    }

    public static bool Verify(string password, string passwordHash, string passwordSalt, int iterations)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(passwordSalt) || iterations <= 0)
        {
            return false;
        }

        var salt = Convert.FromBase64String(passwordSalt);
        var expectedHash = Convert.FromBase64String(passwordHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

public sealed record PasswordHashResult(string Hash, string Salt, int Iterations);

public sealed record ServerUserAccount(
    Guid Id,
    string UserName,
    string DisplayName,
    string Role,
    bool IsEnabled,
    string PasswordHash,
    string PasswordSalt,
    int PasswordIterations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLoginAt);
