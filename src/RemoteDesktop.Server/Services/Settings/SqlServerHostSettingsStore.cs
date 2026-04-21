using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using RemoteDesktop.Server.Options;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services.Settings;

public sealed class SqlServerHostSettingsStore : IServerHostSettingsStore
{
    private const string HostSettingsKey = "host-settings";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;
    private readonly ServerHostSettingsStore _fileMirrorStore;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ControlServerOptions> _options;

    public SqlServerHostSettingsStore(
        IConfiguration configuration,
        IOptions<ControlServerOptions> options,
        ServerHostSettingsStore fileMirrorStore)
    {
        _connectionString = configuration.GetConnectionString("RemoteDesktopDb")
            ?? throw new InvalidOperationException("Missing required connection string: ConnectionStrings:RemoteDesktopDb.");
        _configuration = configuration;
        _options = options;
        _fileMirrorStore = fileMirrorStore;
    }

    public async Task<HostSettingsDto> LoadAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SettingJson
            FROM dbo.RemoteDesktopServerSettings
            WHERE SettingKey = @settingKey;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@settingKey", HostSettingsKey);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is string json && !string.IsNullOrWhiteSpace(json))
        {
            var fromDatabase = JsonSerializer.Deserialize<HostSettingsDto>(json, JsonOptions);
            if (fromDatabase is not null)
            {
                return Normalize(fromDatabase);
            }
        }

        var fromFile = await _fileMirrorStore.LoadAsync(cancellationToken);
        var normalized = Normalize(fromFile);
        await SaveCoreAsync(normalized, mirrorToFile: false, cancellationToken);
        return normalized;
    }

    public Task SaveAsync(HostSettingsDto document, CancellationToken cancellationToken)
    {
        Validate(document);
        return SaveCoreAsync(Normalize(document), mirrorToFile: true, cancellationToken);
    }

    private async Task SaveCoreAsync(HostSettingsDto document, bool mirrorToFile, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.RemoteDesktopServerSettings AS target
            USING
            (
                SELECT
                    @settingKey AS SettingKey,
                    @settingJson AS SettingJson,
                    @updatedAt AS UpdatedAt
            ) AS source
            ON target.SettingKey = source.SettingKey
            WHEN MATCHED THEN
                UPDATE SET
                    SettingJson = source.SettingJson,
                    UpdatedAt = source.UpdatedAt
            WHEN NOT MATCHED THEN
                INSERT (SettingKey, SettingJson, UpdatedAt)
                VALUES (source.SettingKey, source.SettingJson, source.UpdatedAt);
            """;

        var payload = JsonSerializer.Serialize(document, JsonOptions);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@settingKey", HostSettingsKey);
        command.Parameters.AddWithValue("@settingJson", payload);
        command.Parameters.Add("@updatedAt", System.Data.SqlDbType.DateTimeOffset).Value = DateTimeOffset.Now;
        await command.ExecuteNonQueryAsync(cancellationToken);

        if (mirrorToFile)
        {
            await _fileMirrorStore.SaveAsync(document, cancellationToken);
        }
    }

    private HostSettingsDto Normalize(HostSettingsDto document)
    {
        return new HostSettingsDto
        {
            EnableDatabase = true,
            RemoteDesktopDbConnectionString = string.IsNullOrWhiteSpace(document.RemoteDesktopDbConnectionString)
                ? _configuration.GetConnectionString("RemoteDesktopDb") ?? string.Empty
                : document.RemoteDesktopDbConnectionString,
            ServerUrl = string.IsNullOrWhiteSpace(document.ServerUrl) ? _options.Value.ServerUrl : document.ServerUrl,
            ConsoleName = string.IsNullOrWhiteSpace(document.ConsoleName) ? _options.Value.ConsoleName : document.ConsoleName,
            AdminUserName = string.IsNullOrWhiteSpace(document.AdminUserName) ? _options.Value.AdminUserName : document.AdminUserName,
            AdminPassword = string.IsNullOrWhiteSpace(document.AdminPassword) ? _options.Value.AdminPassword : document.AdminPassword,
            SharedAccessKey = string.IsNullOrWhiteSpace(document.SharedAccessKey) ? _options.Value.SharedAccessKey : document.SharedAccessKey,
            RequireHttpsRedirect = document.RequireHttpsRedirect,
            AgentHeartbeatTimeoutSeconds = document.AgentHeartbeatTimeoutSeconds <= 0
                ? _options.Value.AgentHeartbeatTimeoutSeconds
                : document.AgentHeartbeatTimeoutSeconds
        };
    }

    private static void Validate(HostSettingsDto document)
    {
        if (string.IsNullOrWhiteSpace(document.RemoteDesktopDbConnectionString))
        {
            throw new ValidationException("MSSQL connection string is required when database persistence is enabled.");
        }

        var validationContext = new ValidationContext(document);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(document, validationContext, validationResults, validateAllProperties: true))
        {
            throw new ValidationException(validationResults[0].ErrorMessage);
        }
    }
}
