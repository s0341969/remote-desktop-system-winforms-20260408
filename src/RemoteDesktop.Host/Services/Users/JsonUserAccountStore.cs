using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Services.Users;

public interface IUserAccountStore
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);

    Task UpsertAsync(UserAccount account, CancellationToken cancellationToken);

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

    public JsonUserAccountStore(IHostEnvironment environment, Microsoft.Extensions.Options.IOptions<ControlServerOptions> options)
    {
        _usersFilePath = Path.Combine(environment.ContentRootPath, "users.json");
        _options = options.Value;
    }

    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var accounts = await ReadOrBootstrapAsync(cancellationToken);
            return accounts
                .OrderBy(static account => account.UserName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<UserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken)
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

    public async Task UpsertAsync(UserAccount account, CancellationToken cancellationToken)
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
            accounts[index] = new UserAccount
            {
                Id = account.Id,
                UserName = account.UserName,
                DisplayName = account.DisplayName,
                Role = account.Role,
                IsEnabled = account.IsEnabled,
                PasswordHash = account.PasswordHash,
                PasswordSalt = account.PasswordSalt,
                PasswordIterations = account.PasswordIterations,
                CreatedAt = account.CreatedAt,
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

    private async Task<List<UserAccount>> ReadOrBootstrapAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_usersFilePath))
        {
            var bootstrap = new List<UserAccount> { CreateBootstrapAdministrator() };
            await WriteAsync(bootstrap, cancellationToken);
            return bootstrap;
        }

        var json = await File.ReadAllTextAsync(_usersFilePath, cancellationToken);
        var accounts = JsonSerializer.Deserialize<List<UserAccount>>(json, JsonOptions) ?? new List<UserAccount>();
        if (accounts.Count > 0)
        {
            return accounts;
        }

        accounts.Add(CreateBootstrapAdministrator());
        await WriteAsync(accounts, cancellationToken);
        return accounts;
    }

    private async Task WriteAsync(List<UserAccount> accounts, CancellationToken cancellationToken)
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

    private UserAccount CreateBootstrapAdministrator()
    {
        var passwordResult = PasswordHasher.HashPassword(_options.AdminPassword);
        var userName = string.IsNullOrWhiteSpace(_options.AdminUserName) ? "admin" : _options.AdminUserName.Trim();
        var now = DateTimeOffset.UtcNow;

        return new UserAccount
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            DisplayName = "Administrator",
            Role = UserRole.Administrator,
            IsEnabled = true,
            PasswordHash = passwordResult.Hash,
            PasswordSalt = passwordResult.Salt,
            PasswordIterations = passwordResult.Iterations,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
