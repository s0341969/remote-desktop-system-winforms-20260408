using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;

namespace RemoteDesktop.Server.Services.Users;

public sealed class SqlUserAccountStore : IUserAccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _connectionString;
    private readonly string _usersFilePath;
    private readonly ControlServerOptions _options;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public SqlUserAccountStore(IConfiguration configuration, IHostEnvironment environment, IOptions<ControlServerOptions> options)
    {
        _connectionString = configuration.GetConnectionString("RemoteDesktopDb")
            ?? throw new InvalidOperationException("Missing required connection string: ConnectionStrings:RemoteDesktopDb.");
        _usersFilePath = Path.Combine(environment.ContentRootPath, "users.json");
        _options = options.Value;
    }

    public async Task<IReadOnlyList<ServerUserAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);
        var items = new List<ServerUserAccount>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT
                Id,
                UserName,
                DisplayName,
                Role,
                IsEnabled,
                PasswordHash,
                PasswordSalt,
                PasswordIterations,
                CreatedAt,
                UpdatedAt,
                LastLoginAt
            FROM dbo.RemoteDesktopUserAccounts
            ORDER BY UserName;
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadAccount(reader));
        }

        return items;
    }

    public async Task<ServerUserAccount?> FindByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT
                Id,
                UserName,
                DisplayName,
                Role,
                IsEnabled,
                PasswordHash,
                PasswordSalt,
                PasswordIterations,
                CreatedAt,
                UpdatedAt,
                LastLoginAt
            FROM dbo.RemoteDesktopUserAccounts
            WHERE UserName = @userName;
            """,
            connection);
        command.Parameters.AddWithValue("@userName", userName.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAccount(reader) : null;
    }

    public async Task UpsertAsync(ServerUserAccount account, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            """
            MERGE dbo.RemoteDesktopUserAccounts AS target
            USING
            (
                SELECT
                    @id AS Id,
                    @userName AS UserName,
                    @displayName AS DisplayName,
                    @role AS Role,
                    @isEnabled AS IsEnabled,
                    @passwordHash AS PasswordHash,
                    @passwordSalt AS PasswordSalt,
                    @passwordIterations AS PasswordIterations,
                    @createdAt AS CreatedAt,
                    @updatedAt AS UpdatedAt,
                    @lastLoginAt AS LastLoginAt
            ) AS source
            ON target.UserName = source.UserName
            WHEN MATCHED THEN
                UPDATE SET
                    Id = source.Id,
                    DisplayName = source.DisplayName,
                    Role = source.Role,
                    IsEnabled = source.IsEnabled,
                    PasswordHash = source.PasswordHash,
                    PasswordSalt = source.PasswordSalt,
                    PasswordIterations = source.PasswordIterations,
                    CreatedAt = source.CreatedAt,
                    UpdatedAt = source.UpdatedAt,
                    LastLoginAt = source.LastLoginAt
            WHEN NOT MATCHED THEN
                INSERT
                (
                    Id,
                    UserName,
                    DisplayName,
                    Role,
                    IsEnabled,
                    PasswordHash,
                    PasswordSalt,
                    PasswordIterations,
                    CreatedAt,
                    UpdatedAt,
                    LastLoginAt
                )
                VALUES
                (
                    source.Id,
                    source.UserName,
                    source.DisplayName,
                    source.Role,
                    source.IsEnabled,
                    source.PasswordHash,
                    source.PasswordSalt,
                    source.PasswordIterations,
                    source.CreatedAt,
                    source.UpdatedAt,
                    source.LastLoginAt
                );
            """,
            connection);
        FillParameters(command, account);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string userName, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("DELETE FROM dbo.RemoteDesktopUserAccounts WHERE UserName = @userName;", connection);
        command.Parameters.AddWithValue("@userName", userName.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateLastLoginAsync(string userName, DateTimeOffset lastLoginAt, CancellationToken cancellationToken)
    {
        await EnsureSeededAsync(cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            """
            UPDATE dbo.RemoteDesktopUserAccounts
            SET
                LastLoginAt = @lastLoginAt,
                UpdatedAt = @updatedAt
            WHERE UserName = @userName;
            """,
            connection);
        command.Parameters.AddWithValue("@userName", userName.Trim());
        command.Parameters.Add("@lastLoginAt", System.Data.SqlDbType.DateTimeOffset).Value = lastLoginAt;
        command.Parameters.Add("@updatedAt", System.Data.SqlDbType.DateTimeOffset).Value = DateTimeOffset.Now;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var countCommand = new SqlCommand("SELECT COUNT(1) FROM dbo.RemoteDesktopUserAccounts;", connection);
            var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
            if (count > 0)
            {
                return;
            }

            var seedAccounts = await ReadSeedAccountsAsync(cancellationToken);
            foreach (var account in seedAccounts)
            {
                await using var insertCommand = new SqlCommand(
                    """
                    INSERT INTO dbo.RemoteDesktopUserAccounts
                    (
                        Id,
                        UserName,
                        DisplayName,
                        Role,
                        IsEnabled,
                        PasswordHash,
                        PasswordSalt,
                        PasswordIterations,
                        CreatedAt,
                        UpdatedAt,
                        LastLoginAt
                    )
                    VALUES
                    (
                        @id,
                        @userName,
                        @displayName,
                        @role,
                        @isEnabled,
                        @passwordHash,
                        @passwordSalt,
                        @passwordIterations,
                        @createdAt,
                        @updatedAt,
                        @lastLoginAt
                    );
                    """,
                    connection);
                FillParameters(insertCommand, account);
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<IReadOnlyList<ServerUserAccount>> ReadSeedAccountsAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(_usersFilePath))
        {
            var json = await File.ReadAllTextAsync(_usersFilePath, cancellationToken);
            var accounts = JsonSerializer.Deserialize<List<ServerUserAccount>>(json, JsonOptions);
            if (accounts is { Count: > 0 })
            {
                return accounts;
            }
        }

        return [CreateBootstrapAdministrator()];
    }

    private ServerUserAccount CreateBootstrapAdministrator()
    {
        var passwordResult = PasswordHasher.HashPassword(_options.AdminPassword);
        var userName = string.IsNullOrWhiteSpace(_options.AdminUserName) ? "admin" : _options.AdminUserName.Trim();
        var now = DateTimeOffset.Now;

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

    private static void FillParameters(SqlCommand command, ServerUserAccount account)
    {
        command.Parameters.AddWithValue("@id", account.Id);
        command.Parameters.AddWithValue("@userName", account.UserName);
        command.Parameters.AddWithValue("@displayName", account.DisplayName);
        command.Parameters.AddWithValue("@role", account.Role);
        command.Parameters.AddWithValue("@isEnabled", account.IsEnabled);
        command.Parameters.AddWithValue("@passwordHash", account.PasswordHash);
        command.Parameters.AddWithValue("@passwordSalt", account.PasswordSalt);
        command.Parameters.AddWithValue("@passwordIterations", account.PasswordIterations);
        command.Parameters.Add("@createdAt", System.Data.SqlDbType.DateTimeOffset).Value = account.CreatedAt;
        command.Parameters.Add("@updatedAt", System.Data.SqlDbType.DateTimeOffset).Value = account.UpdatedAt;
        command.Parameters.Add("@lastLoginAt", System.Data.SqlDbType.DateTimeOffset).Value = (object?)account.LastLoginAt ?? DBNull.Value;
    }

    private static ServerUserAccount ReadAccount(SqlDataReader reader)
    {
        return new ServerUserAccount(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetInt32(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10));
    }
}
