using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;

namespace RemoteDesktop.Host.Services.Settings;

public interface IHostSettingsStore
{
    Task<HostSettingsDocument> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(HostSettingsDocument document, CancellationToken cancellationToken);
}

public sealed class HostSettingsStore : IHostSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ControlServerOptions> _options;

    public HostSettingsStore(IHostEnvironment environment, IConfiguration configuration, IOptions<ControlServerOptions> options)
    {
        _settingsFilePath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        _configuration = configuration;
        _options = options;
    }

    public async Task<HostSettingsDocument> LoadAsync(CancellationToken cancellationToken)
    {
        var document = new HostSettingsDocument
        {
            EnableDatabase = string.Equals(_options.Value.PersistenceMode, ControlServerOptions.PersistenceModeSqlServer, StringComparison.OrdinalIgnoreCase),
            RemoteDesktopDbConnectionString = _configuration.GetConnectionString("RemoteDesktopDb") ?? string.Empty,
            ServerUrl = _options.Value.ServerUrl,
            CentralServerUrl = _options.Value.CentralServerUrl,
            ConsoleName = _options.Value.ConsoleName,
            AdminUserName = _options.Value.AdminUserName,
            AdminPassword = _options.Value.AdminPassword,
            SharedAccessKey = _options.Value.SharedAccessKey,
            RequireHttpsRedirect = _options.Value.RequireHttpsRedirect,
            AgentHeartbeatTimeoutSeconds = _options.Value.AgentHeartbeatTimeoutSeconds
        };

        if (!File.Exists(_settingsFilePath))
        {
            return document;
        }

        var root = await ReadRootAsync(cancellationToken);
        var connectionString = root?["ConnectionStrings"]?["RemoteDesktopDb"]?.GetValue<string>();
        var controlServer = root?[ControlServerOptions.SectionName] as JsonObject;
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            document.RemoteDesktopDbConnectionString = connectionString;
        }

        if (controlServer is not null)
        {
            document.EnableDatabase = string.Equals(
                controlServer["PersistenceMode"]?.GetValue<string>(),
                ControlServerOptions.PersistenceModeSqlServer,
                StringComparison.OrdinalIgnoreCase);
            document.ServerUrl = controlServer["ServerUrl"]?.GetValue<string>() ?? document.ServerUrl;
            document.CentralServerUrl = controlServer["CentralServerUrl"]?.GetValue<string>() ?? document.CentralServerUrl;
            document.ConsoleName = controlServer["ConsoleName"]?.GetValue<string>() ?? document.ConsoleName;
            document.AdminUserName = controlServer["AdminUserName"]?.GetValue<string>() ?? document.AdminUserName;
            document.AdminPassword = controlServer["AdminPassword"]?.GetValue<string>() ?? document.AdminPassword;
            document.SharedAccessKey = controlServer["SharedAccessKey"]?.GetValue<string>() ?? document.SharedAccessKey;
            document.RequireHttpsRedirect = controlServer["RequireHttpsRedirect"]?.GetValue<bool?>() ?? document.RequireHttpsRedirect;
            document.AgentHeartbeatTimeoutSeconds = controlServer["AgentHeartbeatTimeoutSeconds"]?.GetValue<int?>() ?? document.AgentHeartbeatTimeoutSeconds;
        }

        return document;
    }

    public async Task SaveAsync(HostSettingsDocument document, CancellationToken cancellationToken)
    {
        Validate(document);
        var root = await ReadRootAsync(cancellationToken) ?? new JsonObject();
        var connectionStrings = root["ConnectionStrings"] as JsonObject ?? new JsonObject();
        connectionStrings["RemoteDesktopDb"] = document.RemoteDesktopDbConnectionString.Trim();
        root["ConnectionStrings"] = connectionStrings;

        var controlServer = root[ControlServerOptions.SectionName] as JsonObject ?? new JsonObject();
        controlServer["PersistenceMode"] = document.EnableDatabase
            ? ControlServerOptions.PersistenceModeSqlServer
            : ControlServerOptions.PersistenceModeMemory;
        controlServer["ServerUrl"] = document.ServerUrl.Trim();
        controlServer["CentralServerUrl"] = string.IsNullOrWhiteSpace(document.CentralServerUrl)
            ? null
            : document.CentralServerUrl.Trim();
        controlServer["ConsoleName"] = document.ConsoleName.Trim();
        controlServer["AdminUserName"] = document.AdminUserName.Trim();
        controlServer["AdminPassword"] = document.AdminPassword;
        controlServer["SharedAccessKey"] = document.SharedAccessKey;
        controlServer["RequireHttpsRedirect"] = document.RequireHttpsRedirect;
        controlServer["AgentHeartbeatTimeoutSeconds"] = document.AgentHeartbeatTimeoutSeconds;
        root[ControlServerOptions.SectionName] = controlServer;

        await WriteJsonAtomicallyAsync(root.ToJsonString(JsonOptions), cancellationToken);
    }

    private async Task<JsonObject?> ReadRootAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The settings file is invalid JSON: {_settingsFilePath}", exception);
        }
    }

    private async Task WriteJsonAtomicallyAsync(string json, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        var tempFilePath = $"{_settingsFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempFilePath, json, Encoding.UTF8, cancellationToken);
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static void Validate(HostSettingsDocument document)
    {
        if (document.EnableDatabase && string.IsNullOrWhiteSpace(document.RemoteDesktopDbConnectionString))
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
