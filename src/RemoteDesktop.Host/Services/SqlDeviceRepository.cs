using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services;

public sealed class SqlDeviceRepository : IDeviceRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SqlDeviceRepository> _logger;

    public SqlDeviceRepository(IConfiguration configuration, IHostEnvironment environment, ILogger<SqlDeviceRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("RemoteDesktopDb")
            ?? throw new InvalidOperationException("Missing required connection string: ConnectionStrings:RemoteDesktopDb.");
        _environment = environment;
        _logger = logger;
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(_environment.ContentRootPath, "DatabaseScripts", "001_create_remote_desktop_schema.sql");
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Schema script was not found: {scriptPath}", scriptPath);
        }

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(script, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("MSSQL schema initialized.");
    }

    public async Task UpsertDeviceOnlineAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.RemoteDesktopDevices AS target
            USING
            (
                SELECT
                    @deviceId AS DeviceId,
                    @deviceName AS DeviceName,
                    @hostName AS HostName,
                    @remoteIpAddress AS RemoteIpAddress,
                    @agentVersion AS AgentVersion,
                    @screenWidth AS ScreenWidth,
                    @screenHeight AS ScreenHeight,
                    @inventoryJson AS InventoryJson,
                    @inventoryCollectedAt AS InventoryCollectedAt,
                    @now AS CurrentTime
            ) AS source
            ON target.DeviceId = source.DeviceId
            WHEN MATCHED THEN
                UPDATE SET
                    DeviceName = source.DeviceName,
                    HostName = source.HostName,
                    RemoteIpAddress = source.RemoteIpAddress,
                    AgentVersion = source.AgentVersion,
                    ScreenWidth = source.ScreenWidth,
                    ScreenHeight = source.ScreenHeight,
                    InventoryJson = source.InventoryJson,
                    InventoryCollectedAt = source.InventoryCollectedAt,
                    IsOnline = 1,
                    LastSeenAt = source.CurrentTime,
                    LastConnectedAt = source.CurrentTime,
                    UpdatedAt = source.CurrentTime
            WHEN NOT MATCHED THEN
                INSERT
                (
                    DeviceId,
                    DeviceName,
                    HostName,
                    RemoteIpAddress,
                    AgentVersion,
                    ScreenWidth,
                    ScreenHeight,
                    InventoryJson,
                    InventoryCollectedAt,
                    IsOnline,
                    IsAuthorized,
                    AuthorizedAt,
                    AuthorizedBy,
                    CreatedAt,
                    UpdatedAt,
                    LastSeenAt,
                    LastConnectedAt
                )
                VALUES
                (
                    source.DeviceId,
                    source.DeviceName,
                    source.HostName,
                    source.RemoteIpAddress,
                    source.AgentVersion,
                    source.ScreenWidth,
                    source.ScreenHeight,
                    source.InventoryJson,
                    source.InventoryCollectedAt,
                    1,
                    0,
                    NULL,
                    NULL,
                    source.CurrentTime,
                    source.CurrentTime,
                    source.CurrentTime,
                    source.CurrentTime
                );
            """;

        await ExecuteAsync(sql, command =>
        {
            var now = DateTimeOffset.Now;
            AddStringParameter(command, "@deviceId", descriptor.DeviceId, 128);
            AddStringParameter(command, "@deviceName", descriptor.DeviceName, 256);
            AddStringParameter(command, "@hostName", descriptor.HostName, 256);
            AddNullableStringParameter(command, "@remoteIpAddress", descriptor.RemoteIpAddress, 64);
            AddStringParameter(command, "@agentVersion", descriptor.AgentVersion, 64);
            command.Parameters.Add("@screenWidth", SqlDbType.Int).Value = descriptor.ScreenWidth;
            command.Parameters.Add("@screenHeight", SqlDbType.Int).Value = descriptor.ScreenHeight;
            AddNullableStringParameter(command, "@inventoryJson", SerializeInventory(descriptor.Inventory), -1);
            command.Parameters.Add("@inventoryCollectedAt", SqlDbType.DateTimeOffset).Value = (object?)descriptor.Inventory?.CollectedAt ?? DBNull.Value;
            command.Parameters.Add("@now", SqlDbType.DateTimeOffset).Value = now;
        }, cancellationToken);
    }

    public async Task<Guid> StartPresenceAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string findOpenSql = """
            SELECT TOP (1) PresenceId
            FROM dbo.RemoteDesktopAgentPresenceLogs
            WHERE DeviceId = @deviceId AND DisconnectedAt IS NULL
            ORDER BY ConnectedAt DESC;
            """;

        await using var findCommand = new SqlCommand(findOpenSql, connection);
        AddStringParameter(findCommand, "@deviceId", descriptor.DeviceId, 128);
        var existingPresenceId = await findCommand.ExecuteScalarAsync(cancellationToken);
        if (existingPresenceId is Guid currentPresenceId)
        {
            const string updateOpenSql = """
                UPDATE dbo.RemoteDesktopAgentPresenceLogs
                SET
                    DeviceName = @deviceName,
                    HostName = @hostName,
                    RemoteIpAddress = @remoteIpAddress,
                    AgentVersion = @agentVersion,
                    LastSeenAt = @lastSeenAt,
                    DisconnectReason = NULL
                WHERE PresenceId = @presenceId;
                """;

            await using var updateCommand = new SqlCommand(updateOpenSql, connection);
            updateCommand.Parameters.Add("@presenceId", SqlDbType.UniqueIdentifier).Value = currentPresenceId;
            AddStringParameter(updateCommand, "@deviceName", descriptor.DeviceName, 256);
            AddStringParameter(updateCommand, "@hostName", descriptor.HostName, 256);
            AddNullableStringParameter(updateCommand, "@remoteIpAddress", descriptor.RemoteIpAddress, 64);
            AddStringParameter(updateCommand, "@agentVersion", descriptor.AgentVersion, 64);
            updateCommand.Parameters.Add("@lastSeenAt", SqlDbType.DateTimeOffset).Value = now;
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            return currentPresenceId;
        }

        var presenceId = Guid.NewGuid();
        const string insertSql = """
            INSERT INTO dbo.RemoteDesktopAgentPresenceLogs
            (
                PresenceId,
                DeviceId,
                DeviceName,
                HostName,
                RemoteIpAddress,
                AgentVersion,
                ConnectedAt,
                LastSeenAt,
                DisconnectedAt,
                DisconnectReason
            )
            VALUES
            (
                @presenceId,
                @deviceId,
                @deviceName,
                @hostName,
                @remoteIpAddress,
                @agentVersion,
                @connectedAt,
                @lastSeenAt,
                NULL,
                NULL
            );
            """;

        await using var insertCommand = new SqlCommand(insertSql, connection);
        insertCommand.Parameters.Add("@presenceId", SqlDbType.UniqueIdentifier).Value = presenceId;
        AddStringParameter(insertCommand, "@deviceId", descriptor.DeviceId, 128);
        AddStringParameter(insertCommand, "@deviceName", descriptor.DeviceName, 256);
        AddStringParameter(insertCommand, "@hostName", descriptor.HostName, 256);
        AddNullableStringParameter(insertCommand, "@remoteIpAddress", descriptor.RemoteIpAddress, 64);
        AddStringParameter(insertCommand, "@agentVersion", descriptor.AgentVersion, 64);
        insertCommand.Parameters.Add("@connectedAt", SqlDbType.DateTimeOffset).Value = now;
        insertCommand.Parameters.Add("@lastSeenAt", SqlDbType.DateTimeOffset).Value = now;
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        return presenceId;
    }

    public Task TouchPresenceAsync(Guid presenceId, string deviceId, int screenWidth, int screenHeight, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.RemoteDesktopDevices
            SET
                ScreenWidth = @screenWidth,
                ScreenHeight = @screenHeight,
                IsOnline = 1,
                LastSeenAt = @now,
                UpdatedAt = @now
            WHERE DeviceId = @deviceId;

            UPDATE dbo.RemoteDesktopAgentPresenceLogs
            SET LastSeenAt = @now
            WHERE PresenceId = @presenceId AND DisconnectedAt IS NULL;
            """;

        return ExecuteAsync(sql, command =>
        {
            var now = DateTimeOffset.Now;
            command.Parameters.Add("@presenceId", SqlDbType.UniqueIdentifier).Value = presenceId;
            AddStringParameter(command, "@deviceId", deviceId, 128);
            command.Parameters.Add("@screenWidth", SqlDbType.Int).Value = screenWidth;
            command.Parameters.Add("@screenHeight", SqlDbType.Int).Value = screenHeight;
            command.Parameters.Add("@now", SqlDbType.DateTimeOffset).Value = now;
        }, cancellationToken);
    }

    public async Task ClosePresenceAsync(Guid presenceId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.Now;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await UpdateDeviceOfflineAsync(connection, transaction, deviceId, now, cancellationToken);
        var currentLog = await ClosePresenceLogAsync(connection, transaction, presenceId, deviceId, reason, now, cancellationToken);
        if (currentLog is not null)
        {
            await MergePresenceLogIfReasonUnchangedAsync(connection, transaction, currentLog, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedByUserName, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.RemoteDesktopDevices
            SET
                IsAuthorized = @isAuthorized,
                AuthorizedAt = CASE WHEN @isAuthorized = 1 THEN @changedAt ELSE NULL END,
                AuthorizedBy = CASE WHEN @isAuthorized = 1 THEN @changedBy ELSE NULL END,
                UpdatedAt = @changedAt
            WHERE DeviceId = @deviceId;
            """;

        return ExecuteAsync(sql, command =>
        {
            var changedAt = DateTimeOffset.Now;
            AddStringParameter(command, "@deviceId", deviceId, 128);
            command.Parameters.Add("@isAuthorized", SqlDbType.Bit).Value = isAuthorized;
            command.Parameters.Add("@changedAt", SqlDbType.DateTimeOffset).Value = changedAt;
            AddStringParameter(command, "@changedBy", changedByUserName, 128);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                DeviceId,
                DeviceName,
                HostName,
                RemoteIpAddress,
                AgentVersion,
                ScreenWidth,
                ScreenHeight,
                InventoryJson,
                InventoryCollectedAt,
                IsOnline,
                IsAuthorized,
                AuthorizedAt,
                AuthorizedBy,
                CreatedAt,
                LastSeenAt,
                LastConnectedAt,
                LastDisconnectedAt
            FROM dbo.RemoteDesktopDevices
            ORDER BY IsOnline DESC, LastSeenAt DESC;
            """;

        var result = new List<DeviceRecord>(take);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(ReadDevice(reader));
        }

        return result;
    }

    public async Task<DeviceRecord?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                DeviceId,
                DeviceName,
                HostName,
                RemoteIpAddress,
                AgentVersion,
                ScreenWidth,
                ScreenHeight,
                InventoryJson,
                InventoryCollectedAt,
                IsOnline,
                IsAuthorized,
                AuthorizedAt,
                AuthorizedBy,
                CreatedAt,
                LastSeenAt,
                LastConnectedAt,
                LastDisconnectedAt
            FROM dbo.RemoteDesktopDevices
            WHERE DeviceId = @deviceId;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddStringParameter(command, "@deviceId", deviceId, 128);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDevice(reader) : null;
    }

    public async Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                PresenceId,
                DeviceId,
                DeviceName,
                HostName,
                RemoteIpAddress,
                AgentVersion,
                ConnectedAt,
                LastSeenAt,
                DisconnectedAt,
                DisconnectReason,
                DATEDIFF(SECOND, ConnectedAt, COALESCE(DisconnectedAt, SYSDATETIMEOFFSET())) AS OnlineSeconds
            FROM dbo.RemoteDesktopAgentPresenceLogs
            ORDER BY ConnectedAt DESC;
            """;

        var result = new List<AgentPresenceLogRecord>(take);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AgentPresenceLogRecord
            {
                PresenceId = reader.GetGuid(0),
                DeviceId = reader.GetString(1),
                DeviceName = reader.GetString(2),
                HostName = reader.GetString(3),
                RemoteIpAddress = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                AgentVersion = reader.GetString(5),
                ConnectedAt = reader.GetFieldValue<DateTimeOffset>(6),
                LastSeenAt = reader.GetFieldValue<DateTimeOffset>(7),
                DisconnectedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                DisconnectReason = reader.IsDBNull(9) ? null : reader.GetString(9),
                OnlineSeconds = reader.GetInt32(10)
            });
        }

        return result;
    }

    public async Task UpdateInventoryAsync(string deviceId, AgentInventoryProfile inventory, CancellationToken cancellationToken)
    {
        var serializedInventory = SerializeInventory(inventory) ?? throw new InvalidOperationException("Inventory payload could not be serialized.");
        var fingerprint = CalculateFingerprint(inventory);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var currentSnapshot = await GetCurrentInventorySnapshotAsync(connection, deviceId, cancellationToken);
        var changeSummary = BuildChangeSummary(currentSnapshot?.Inventory, inventory);

        const string updateSql = """
            UPDATE dbo.RemoteDesktopDevices
            SET
                InventoryJson = @inventoryJson,
                InventoryCollectedAt = @inventoryCollectedAt,
                UpdatedAt = @updatedAt
            WHERE DeviceId = @deviceId;
            """;

        await using (var updateCommand = new SqlCommand(updateSql, connection))
        {
            AddStringParameter(updateCommand, "@deviceId", deviceId, 128);
            AddNullableStringParameter(updateCommand, "@inventoryJson", serializedInventory, -1);
            updateCommand.Parameters.Add("@inventoryCollectedAt", SqlDbType.DateTimeOffset).Value = inventory.CollectedAt;
            updateCommand.Parameters.Add("@updatedAt", SqlDbType.DateTimeOffset).Value = DateTimeOffset.Now;
            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (currentSnapshot is { } existingSnapshot && string.Equals(existingSnapshot.Fingerprint, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        const string insertHistorySql = """
            INSERT INTO dbo.RemoteDesktopInventoryHistory
            (
                HistoryId,
                DeviceId,
                InventoryFingerprint,
                InventoryJson,
                CollectedAt,
                RecordedAt,
                ChangeSummary
            )
            VALUES
            (
                @historyId,
                @deviceId,
                @inventoryFingerprint,
                @inventoryJson,
                @collectedAt,
                @recordedAt,
                @changeSummary
            );
            """;

        await using var insertCommand = new SqlCommand(insertHistorySql, connection);
        insertCommand.Parameters.Add("@historyId", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        AddStringParameter(insertCommand, "@deviceId", deviceId, 128);
        AddStringParameter(insertCommand, "@inventoryFingerprint", fingerprint, 64);
        AddNullableStringParameter(insertCommand, "@inventoryJson", serializedInventory, -1);
        insertCommand.Parameters.Add("@collectedAt", SqlDbType.DateTimeOffset).Value = inventory.CollectedAt;
        insertCommand.Parameters.Add("@recordedAt", SqlDbType.DateTimeOffset).Value = DateTimeOffset.Now;
        AddStringParameter(insertCommand, "@changeSummary", changeSummary, 512);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryHistoryRecord>> GetInventoryHistoryAsync(string deviceId, int take, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                HistoryId,
                DeviceId,
                InventoryFingerprint,
                InventoryJson,
                CollectedAt,
                RecordedAt,
                ChangeSummary
            FROM dbo.RemoteDesktopInventoryHistory
            WHERE DeviceId = @deviceId
            ORDER BY RecordedAt DESC;
            """;

        var result = new List<InventoryHistoryRecord>(take);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@take", SqlDbType.Int).Value = take;
        AddStringParameter(command, "@deviceId", deviceId, 128);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var inventory = DeserializeInventory(reader, 3);
            if (inventory is null)
            {
                continue;
            }

            result.Add(new InventoryHistoryRecord
            {
                HistoryId = reader.GetGuid(0),
                DeviceId = reader.GetString(1),
                InventoryFingerprint = reader.GetString(2),
                Inventory = inventory,
                CollectedAt = reader.GetFieldValue<DateTimeOffset>(4),
                RecordedAt = reader.GetFieldValue<DateTimeOffset>(5),
                ChangeSummary = reader.IsDBNull(6) ? string.Empty : reader.GetString(6)
            });
        }

        return result;
    }

    private async Task ExecuteAsync(string sql, Action<SqlCommand> parameterize, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        parameterize(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateDeviceOfflineAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string deviceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.RemoteDesktopDevices
            SET
                IsOnline = 0,
                LastDisconnectedAt = @now,
                UpdatedAt = @now
            WHERE DeviceId = @deviceId;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        AddStringParameter(command, "@deviceId", deviceId, 128);
        command.Parameters.Add("@now", SqlDbType.DateTimeOffset).Value = now;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AgentPresenceLogRecord?> ClosePresenceLogAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid presenceId,
        string deviceId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const string closeSql = """
            UPDATE dbo.RemoteDesktopAgentPresenceLogs
            SET
                DisconnectedAt = COALESCE(DisconnectedAt, @now),
                DisconnectReason = COALESCE(DisconnectReason, @reason),
                LastSeenAt = @now
            WHERE PresenceId = @presenceId;
            """;

        await using (var closeCommand = new SqlCommand(closeSql, connection, transaction))
        {
            closeCommand.Parameters.Add("@presenceId", SqlDbType.UniqueIdentifier).Value = presenceId;
            AddStringParameter(closeCommand, "@reason", reason, 128);
            closeCommand.Parameters.Add("@now", SqlDbType.DateTimeOffset).Value = now;
            await closeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string readSql = """
            SELECT
                PresenceId,
                DeviceId,
                DeviceName,
                HostName,
                RemoteIpAddress,
                AgentVersion,
                ConnectedAt,
                LastSeenAt,
                DisconnectedAt,
                DisconnectReason
            FROM dbo.RemoteDesktopAgentPresenceLogs
            WHERE PresenceId = @presenceId AND DeviceId = @deviceId;
            """;

        await using var readCommand = new SqlCommand(readSql, connection, transaction);
        readCommand.Parameters.Add("@presenceId", SqlDbType.UniqueIdentifier).Value = presenceId;
        AddStringParameter(readCommand, "@deviceId", deviceId, 128);
        await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var connectedAt = reader.GetFieldValue<DateTimeOffset>(6);
        var lastSeenAt = reader.GetFieldValue<DateTimeOffset>(7);
        DateTimeOffset? disconnectedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8);
        return new AgentPresenceLogRecord
        {
            PresenceId = reader.GetGuid(0),
            DeviceId = reader.GetString(1),
            DeviceName = reader.GetString(2),
            HostName = reader.GetString(3),
            RemoteIpAddress = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            AgentVersion = reader.GetString(5),
            ConnectedAt = connectedAt,
            LastSeenAt = lastSeenAt,
            DisconnectedAt = disconnectedAt,
            DisconnectReason = reader.IsDBNull(9) ? null : reader.GetString(9),
            OnlineSeconds = (long)Math.Max(0, ((disconnectedAt ?? now) - connectedAt).TotalSeconds)
        };
    }

    private static async Task MergePresenceLogIfReasonUnchangedAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AgentPresenceLogRecord currentLog,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentLog.DisconnectReason))
        {
            return;
        }

        const string findPreviousSql = """
            SELECT TOP (1) PresenceId
            FROM dbo.RemoteDesktopAgentPresenceLogs
            WHERE DeviceId = @deviceId
              AND PresenceId <> @presenceId
              AND DisconnectReason = @reason
              AND DisconnectedAt IS NOT NULL
            ORDER BY DisconnectedAt DESC, ConnectedAt DESC;
            """;

        Guid? previousPresenceId;
        await using (var findCommand = new SqlCommand(findPreviousSql, connection, transaction))
        {
            AddStringParameter(findCommand, "@deviceId", currentLog.DeviceId, 128);
            findCommand.Parameters.Add("@presenceId", SqlDbType.UniqueIdentifier).Value = currentLog.PresenceId;
            AddStringParameter(findCommand, "@reason", currentLog.DisconnectReason, 128);
            var previousResult = await findCommand.ExecuteScalarAsync(cancellationToken);
            previousPresenceId = previousResult is Guid guid ? guid : null;
        }

        if (previousPresenceId is null)
        {
            return;
        }

        const string mergeSql = """
            UPDATE dbo.RemoteDesktopAgentPresenceLogs
            SET
                DeviceName = @deviceName,
                HostName = @hostName,
                RemoteIpAddress = @remoteIpAddress,
                AgentVersion = @agentVersion,
                ConnectedAt = @connectedAt,
                LastSeenAt = @lastSeenAt,
                DisconnectedAt = @disconnectedAt,
                DisconnectReason = @disconnectReason
            WHERE PresenceId = @targetPresenceId;

            DELETE FROM dbo.RemoteDesktopAgentPresenceLogs
            WHERE PresenceId = @sourcePresenceId;
            """;

        await using var mergeCommand = new SqlCommand(mergeSql, connection, transaction);
        mergeCommand.Parameters.Add("@targetPresenceId", SqlDbType.UniqueIdentifier).Value = previousPresenceId.Value;
        mergeCommand.Parameters.Add("@sourcePresenceId", SqlDbType.UniqueIdentifier).Value = currentLog.PresenceId;
        AddStringParameter(mergeCommand, "@deviceName", currentLog.DeviceName, 256);
        AddStringParameter(mergeCommand, "@hostName", currentLog.HostName, 256);
        AddNullableStringParameter(mergeCommand, "@remoteIpAddress", currentLog.RemoteIpAddress, 64);
        AddStringParameter(mergeCommand, "@agentVersion", currentLog.AgentVersion, 64);
        mergeCommand.Parameters.Add("@connectedAt", SqlDbType.DateTimeOffset).Value = currentLog.ConnectedAt;
        mergeCommand.Parameters.Add("@lastSeenAt", SqlDbType.DateTimeOffset).Value = currentLog.LastSeenAt;
        mergeCommand.Parameters.Add("@disconnectedAt", SqlDbType.DateTimeOffset).Value = (object?)currentLog.DisconnectedAt ?? DBNull.Value;
        AddStringParameter(mergeCommand, "@disconnectReason", currentLog.DisconnectReason, 128);
        await mergeCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddStringParameter(SqlCommand command, string name, string? value, int length)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = value ?? string.Empty;
    }

    private static void AddNullableStringParameter(SqlCommand command, string name, string? value, int length)
    {
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static async Task<(string Fingerprint, AgentInventoryProfile? Inventory)?> GetCurrentInventorySnapshotAsync(SqlConnection connection, string deviceId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT InventoryJson
            FROM dbo.RemoteDesktopDevices
            WHERE DeviceId = @deviceId;
            """;

        await using var command = new SqlCommand(sql, connection);
        AddStringParameter(command, "@deviceId", deviceId, 128);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string inventoryJson || string.IsNullOrWhiteSpace(inventoryJson))
        {
            return null;
        }

        var inventory = JsonSerializer.Deserialize<AgentInventoryProfile>(inventoryJson, JsonOptions);
        return inventory is null ? null : (CalculateFingerprint(inventory), inventory);
    }

    private static string? SerializeInventory(AgentInventoryProfile? inventory)
    {
        return inventory is null ? null : JsonSerializer.Serialize(inventory, JsonOptions);
    }

    private static string CalculateFingerprint(AgentInventoryProfile inventory)
    {
        var payload = string.Join("|", new[]
        {
            inventory.CpuName,
            inventory.InstalledMemoryBytes.ToString(),
            inventory.StorageSummary,
            inventory.OsName,
            inventory.OsVersion,
            inventory.OsBuildNumber,
            inventory.OfficeVersion,
            inventory.LastWindowsUpdateTitle,
            inventory.LastWindowsUpdateInstalledAt?.UtcDateTime.ToString("O") ?? string.Empty
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static string BuildChangeSummary(AgentInventoryProfile? previousInventory, AgentInventoryProfile currentInventory)
    {
        if (previousInventory is null)
        {
            return "首次盤點。 / Initial inventory snapshot.";
        }

        var changes = new List<string>();
        AppendChange(changes, "CPU", previousInventory.CpuName, currentInventory.CpuName);
        AppendChange(changes, "記憶體", FormatBytes(previousInventory.InstalledMemoryBytes), FormatBytes(currentInventory.InstalledMemoryBytes));
        AppendChange(changes, "磁碟", previousInventory.StorageSummary, currentInventory.StorageSummary);
        AppendChange(changes, "作業系統", $"{previousInventory.OsName} {previousInventory.OsVersion} ({previousInventory.OsBuildNumber})", $"{currentInventory.OsName} {currentInventory.OsVersion} ({currentInventory.OsBuildNumber})");
        AppendChange(changes, "Office", previousInventory.OfficeVersion, currentInventory.OfficeVersion);
        AppendChange(changes, "最後更新", $"{previousInventory.LastWindowsUpdateTitle} {previousInventory.LastWindowsUpdateInstalledAt:yyyy-MM-dd}", $"{currentInventory.LastWindowsUpdateTitle} {currentInventory.LastWindowsUpdateInstalledAt:yyyy-MM-dd}");
        return changes.Count == 0
            ? "盤點時間更新，內容無變更。 / Inventory refreshed without content changes."
            : string.Join("；", changes);
    }

    private static void AppendChange(List<string> changes, string label, string? before, string? after)
    {
        var normalizedBefore = string.IsNullOrWhiteSpace(before) ? "-" : before.Trim();
        var normalizedAfter = string.IsNullOrWhiteSpace(after) ? "-" : after.Trim();
        if (string.Equals(normalizedBefore, normalizedAfter, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add($"{label}: {normalizedBefore} -> {normalizedAfter}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "未知";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static AgentInventoryProfile? DeserializeInventory(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AgentInventoryProfile>(reader.GetString(ordinal), JsonOptions);
    }

    private static DeviceRecord ReadDevice(SqlDataReader reader)
    {
        return new DeviceRecord
        {
            DeviceId = reader.GetString(0),
            DeviceName = reader.GetString(1),
            HostName = reader.GetString(2),
            RemoteIpAddress = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            AgentVersion = reader.GetString(4),
            ScreenWidth = reader.GetInt32(5),
            ScreenHeight = reader.GetInt32(6),
            Inventory = DeserializeInventory(reader, 7),
            IsOnline = reader.GetBoolean(9),
            IsAuthorized = reader.GetBoolean(10),
            AuthorizedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            AuthorizedBy = reader.IsDBNull(12) ? null : reader.GetString(12),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(13),
            LastSeenAt = reader.GetFieldValue<DateTimeOffset>(14),
            LastConnectedAt = reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
            LastDisconnectedAt = reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTimeOffset>(16)
        };
    }
}
