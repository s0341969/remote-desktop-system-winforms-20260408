using Microsoft.Data.SqlClient;
using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services;

public sealed class SqlDeviceRepository : IDeviceRepository
{
    private readonly string _connectionString;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SqlDeviceRepository> _logger;

    public SqlDeviceRepository(IConfiguration configuration, IHostEnvironment environment, ILogger<SqlDeviceRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("RemoteDesktopDb")
            ?? throw new InvalidOperationException("找不到 ConnectionStrings:RemoteDesktopDb。");
        _environment = environment;
        _logger = logger;
    }

    public async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(_environment.ContentRootPath, "DatabaseScripts", "001_create_remote_desktop_schema.sql");
        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(script, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("MSSQL schema 初始化完成。");
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
                    @agentVersion AS AgentVersion,
                    @screenWidth AS ScreenWidth,
                    @screenHeight AS ScreenHeight,
                    @now AS CurrentTime
            ) AS source
            ON target.DeviceId = source.DeviceId
            WHEN MATCHED THEN
                UPDATE SET
                    DeviceName = source.DeviceName,
                    HostName = source.HostName,
                    AgentVersion = source.AgentVersion,
                    ScreenWidth = source.ScreenWidth,
                    ScreenHeight = source.ScreenHeight,
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
                    AgentVersion,
                    ScreenWidth,
                    ScreenHeight,
                    IsOnline,
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
                    source.AgentVersion,
                    source.ScreenWidth,
                    source.ScreenHeight,
                    1,
                    source.CurrentTime,
                    source.CurrentTime,
                    source.CurrentTime,
                    source.CurrentTime
                );
            """;

        await ExecuteAsync(sql, command =>
        {
            var now = DateTimeOffset.UtcNow;
            command.Parameters.AddWithValue("@deviceId", descriptor.DeviceId);
            command.Parameters.AddWithValue("@deviceName", descriptor.DeviceName);
            command.Parameters.AddWithValue("@hostName", descriptor.HostName);
            command.Parameters.AddWithValue("@agentVersion", descriptor.AgentVersion);
            command.Parameters.AddWithValue("@screenWidth", descriptor.ScreenWidth);
            command.Parameters.AddWithValue("@screenHeight", descriptor.ScreenHeight);
            command.Parameters.AddWithValue("@now", now);
        }, cancellationToken);
    }

    public async Task<Guid> StartPresenceAsync(AgentDescriptor descriptor, CancellationToken cancellationToken)
    {
        var presenceId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        const string sql = """
            INSERT INTO dbo.RemoteDesktopAgentPresenceLogs
            (
                PresenceId,
                DeviceId,
                DeviceName,
                HostName,
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
                @agentVersion,
                @connectedAt,
                @lastSeenAt,
                NULL,
                NULL
            );
            """;

        await ExecuteAsync(sql, command =>
        {
            command.Parameters.AddWithValue("@presenceId", presenceId);
            command.Parameters.AddWithValue("@deviceId", descriptor.DeviceId);
            command.Parameters.AddWithValue("@deviceName", descriptor.DeviceName);
            command.Parameters.AddWithValue("@hostName", descriptor.HostName);
            command.Parameters.AddWithValue("@agentVersion", descriptor.AgentVersion);
            command.Parameters.AddWithValue("@connectedAt", now);
            command.Parameters.AddWithValue("@lastSeenAt", now);
        }, cancellationToken);

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
            var now = DateTimeOffset.UtcNow;
            command.Parameters.AddWithValue("@presenceId", presenceId);
            command.Parameters.AddWithValue("@deviceId", deviceId);
            command.Parameters.AddWithValue("@screenWidth", screenWidth);
            command.Parameters.AddWithValue("@screenHeight", screenHeight);
            command.Parameters.AddWithValue("@now", now);
        }, cancellationToken);
    }

    public Task ClosePresenceAsync(Guid presenceId, string deviceId, string reason, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.RemoteDesktopDevices
            SET
                IsOnline = 0,
                LastDisconnectedAt = @now,
                UpdatedAt = @now
            WHERE DeviceId = @deviceId;

            UPDATE dbo.RemoteDesktopAgentPresenceLogs
            SET
                DisconnectedAt = COALESCE(DisconnectedAt, @now),
                DisconnectReason = COALESCE(DisconnectReason, @reason),
                LastSeenAt = @now
            WHERE PresenceId = @presenceId;
            """;

        return ExecuteAsync(sql, command =>
        {
            var now = DateTimeOffset.UtcNow;
            command.Parameters.AddWithValue("@presenceId", presenceId);
            command.Parameters.AddWithValue("@deviceId", deviceId);
            command.Parameters.AddWithValue("@reason", reason);
            command.Parameters.AddWithValue("@now", now);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (@take)
                DeviceId,
                DeviceName,
                HostName,
                AgentVersion,
                ScreenWidth,
                ScreenHeight,
                IsOnline,
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
        command.Parameters.AddWithValue("@take", take);

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
                AgentVersion,
                ScreenWidth,
                ScreenHeight,
                IsOnline,
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
        command.Parameters.AddWithValue("@deviceId", deviceId);

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
        command.Parameters.AddWithValue("@take", take);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new AgentPresenceLogRecord
            {
                PresenceId = reader.GetGuid(0),
                DeviceId = reader.GetString(1),
                DeviceName = reader.GetString(2),
                HostName = reader.GetString(3),
                AgentVersion = reader.GetString(4),
                ConnectedAt = reader.GetFieldValue<DateTimeOffset>(5),
                LastSeenAt = reader.GetFieldValue<DateTimeOffset>(6),
                DisconnectedAt = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                DisconnectReason = reader.IsDBNull(8) ? null : reader.GetString(8),
                OnlineSeconds = reader.GetInt32(9)
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

    private static DeviceRecord ReadDevice(SqlDataReader reader)
    {
        return new DeviceRecord
        {
            DeviceId = reader.GetString(0),
            DeviceName = reader.GetString(1),
            HostName = reader.GetString(2),
            AgentVersion = reader.GetString(3),
            ScreenWidth = reader.GetInt32(4),
            ScreenHeight = reader.GetInt32(5),
            IsOnline = reader.GetBoolean(6),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(7),
            LastSeenAt = reader.GetFieldValue<DateTimeOffset>(8),
            LastConnectedAt = reader.IsDBNull(9) ? null : reader.GetFieldValue<DateTimeOffset>(9),
            LastDisconnectedAt = reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10)
        };
    }
}
