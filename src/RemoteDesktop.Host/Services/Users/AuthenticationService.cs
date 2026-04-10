using System.ComponentModel.DataAnnotations;
using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services.Users;

public interface IAuthenticationService
{
    Task<AuthenticatedUserSession?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserAccount>> GetAccountsAsync(CancellationToken cancellationToken);

    Task SaveAccountAsync(UserAccountEditorModel model, CancellationToken cancellationToken);

    Task DeleteAccountAsync(string userName, string currentUserName, CancellationToken cancellationToken);
}

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserAccountStore _userAccountStore;

    public AuthenticationService(IUserAccountStore userAccountStore)
    {
        _userAccountStore = userAccountStore;
    }

    public async Task<AuthenticatedUserSession?> AuthenticateAsync(string userName, string password, CancellationToken cancellationToken)
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

    public Task<IReadOnlyList<UserAccount>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        return _userAccountStore.GetAllAsync(cancellationToken);
    }

    public async Task SaveAccountAsync(UserAccountEditorModel model, CancellationToken cancellationToken)
    {
        Validate(model);

        var accounts = await _userAccountStore.GetAllAsync(cancellationToken);
        var existing = await _userAccountStore.FindByUserNameAsync(model.UserName.Trim(), cancellationToken);
        EnsureEnabledAdministratorRemains(accounts, existing, model);
        var passwordHash = existing?.PasswordHash ?? string.Empty;
        var passwordSalt = existing?.PasswordSalt ?? string.Empty;
        var passwordIterations = existing?.PasswordIterations ?? 0;

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var passwordResult = PasswordHasher.HashPassword(model.Password);
            passwordHash = passwordResult.Hash;
            passwordSalt = passwordResult.Salt;
            passwordIterations = passwordResult.Iterations;
        }
        else if (existing is null)
        {
            throw new ValidationException("Password is required for a new account.");
        }

        var now = DateTimeOffset.UtcNow;
        var account = new UserAccount
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            UserName = model.UserName.Trim(),
            DisplayName = model.DisplayName.Trim(),
            Role = model.Role,
            IsEnabled = model.IsEnabled,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordIterations = passwordIterations,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now,
            LastLoginAt = existing?.LastLoginAt
        };

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

        if (target.Role == UserRole.Administrator && target.IsEnabled)
        {
            var otherEnabledAdministratorExists = accounts.Any(account =>
                !string.Equals(account.UserName, userName, StringComparison.OrdinalIgnoreCase)
                && account.Role == UserRole.Administrator
                && account.IsEnabled);

            if (!otherEnabledAdministratorExists)
            {
                throw new ValidationException("At least one enabled administrator account must remain.");
            }
        }

        await _userAccountStore.DeleteAsync(userName, cancellationToken);
    }

    public static AuthenticatedUserSession ToSession(UserAccount account)
    {
        return new AuthenticatedUserSession
        {
            UserName = account.UserName,
            DisplayName = string.IsNullOrWhiteSpace(account.DisplayName) ? account.UserName : account.DisplayName,
            Role = account.Role
        };
    }

    private static void Validate(UserAccountEditorModel model)
    {
        if (string.IsNullOrWhiteSpace(model.UserName))
        {
            throw new ValidationException("User name is required.");
        }

        if (string.IsNullOrWhiteSpace(model.DisplayName))
        {
            throw new ValidationException("Display name is required.");
        }

        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password.Length < 10)
        {
            throw new ValidationException("Password must be at least 10 characters.");
        }
    }

    private static void EnsureEnabledAdministratorRemains(
        IReadOnlyList<UserAccount> accounts,
        UserAccount? existing,
        UserAccountEditorModel model)
    {
        if (existing is not { Role: UserRole.Administrator, IsEnabled: true })
        {
            return;
        }

        if (model.Role == UserRole.Administrator && model.IsEnabled)
        {
            return;
        }

        var otherEnabledAdministratorExists = accounts.Any(account =>
            !string.Equals(account.UserName, existing.UserName, StringComparison.OrdinalIgnoreCase)
            && account.Role == UserRole.Administrator
            && account.IsEnabled);

        if (!otherEnabledAdministratorExists)
        {
            throw new ValidationException("At least one enabled administrator account must remain.");
        }
    }
}

public sealed class UserAccountEditorModel
{
    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Operator;

    public bool IsEnabled { get; set; } = true;

    public string Password { get; set; } = string.Empty;
}
