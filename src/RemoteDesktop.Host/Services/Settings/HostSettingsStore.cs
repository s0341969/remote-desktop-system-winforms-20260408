using System.ComponentModel.DataAnnotations;
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
            RemoteDesktopDbConnectionString = _configuration.GetConnectionString("RemoteDesktopDb") ?? string.Empty,
            ServerUrl = _options.Value.ServerUrl,
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
            document.ServerUrl = controlServer["ServerUrl"]?.GetValue<string>() ?? document.ServerUrl;
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
        controlServer["ServerUrl"] = document.ServerUrl.Trim();
        controlServer["ConsoleName"] = document.ConsoleName.Trim();
        controlServer["AdminUserName"] = document.AdminUserName.Trim();
        controlServer["AdminPassword"] = document.AdminPassword;
        controlServer["SharedAccessKey"] = document.SharedAccessKey;
        controlServer["RequireHttpsRedirect"] = document.RequireHttpsRedirect;
        controlServer["AgentHeartbeatTimeoutSeconds"] = document.AgentHeartbeatTimeoutSeconds;
        root[ControlServerOptions.SectionName] = controlServer;

        await File.WriteAllTextAsync(_settingsFilePath, root.ToJsonString(JsonOptions), cancellationToken);
    }

    private async Task<JsonObject?> ReadRootAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
        return JsonNode.Parse(json) as JsonObject;
    }

    private static void Validate(HostSettingsDocument document)
    {
        var validationContext = new ValidationContext(document);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(document, validationContext, validationResults, validateAllProperties: true))
        {
            throw new ValidationException(validationResults[0].ErrorMessage);
        }
    }
}
