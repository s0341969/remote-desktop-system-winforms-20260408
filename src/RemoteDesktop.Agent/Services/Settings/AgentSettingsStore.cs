using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services.Settings;

public interface IAgentSettingsStore
{
    Task<AgentSettingsDocument> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AgentSettingsDocument document, CancellationToken cancellationToken);
}

public sealed class AgentSettingsStore : IAgentSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;
    private readonly IOptions<AgentOptions> _options;

    public AgentSettingsStore(IHostEnvironment environment, IOptions<AgentOptions> options)
    {
        _settingsFilePath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        _options = options;
    }

    public async Task<AgentSettingsDocument> LoadAsync(CancellationToken cancellationToken)
    {
        var document = new AgentSettingsDocument
        {
            ServerUrl = _options.Value.ServerUrl,
            DeviceId = _options.Value.DeviceId,
            DeviceName = _options.Value.DeviceName,
            SharedAccessKey = _options.Value.SharedAccessKey,
            CaptureFramesPerSecond = _options.Value.CaptureFramesPerSecond,
            JpegQuality = _options.Value.JpegQuality,
            MaxFrameWidth = _options.Value.MaxFrameWidth,
            ReconnectDelaySeconds = _options.Value.ReconnectDelaySeconds
        };

        if (!File.Exists(_settingsFilePath))
        {
            return document;
        }

        var root = await ReadRootAsync(cancellationToken);
        var agent = root?[AgentOptions.SectionName] as JsonObject;
        if (agent is not null)
        {
            document.ServerUrl = agent["ServerUrl"]?.GetValue<string>() ?? document.ServerUrl;
            document.DeviceId = agent["DeviceId"]?.GetValue<string>() ?? document.DeviceId;
            document.DeviceName = agent["DeviceName"]?.GetValue<string>() ?? document.DeviceName;
            document.SharedAccessKey = agent["SharedAccessKey"]?.GetValue<string>() ?? document.SharedAccessKey;
            document.CaptureFramesPerSecond = agent["CaptureFramesPerSecond"]?.GetValue<int?>() ?? document.CaptureFramesPerSecond;
            document.JpegQuality = agent["JpegQuality"]?.GetValue<long?>() ?? document.JpegQuality;
            document.MaxFrameWidth = agent["MaxFrameWidth"]?.GetValue<int?>() ?? document.MaxFrameWidth;
            document.ReconnectDelaySeconds = agent["ReconnectDelaySeconds"]?.GetValue<int?>() ?? document.ReconnectDelaySeconds;
        }

        return document;
    }

    public async Task SaveAsync(AgentSettingsDocument document, CancellationToken cancellationToken)
    {
        Validate(document);
        var root = await ReadRootAsync(cancellationToken) ?? new JsonObject();
        var agent = root[AgentOptions.SectionName] as JsonObject ?? new JsonObject();
        agent["ServerUrl"] = document.ServerUrl.Trim();
        agent["DeviceId"] = document.DeviceId.Trim();
        agent["DeviceName"] = document.DeviceName.Trim();
        agent["SharedAccessKey"] = document.SharedAccessKey;
        agent["CaptureFramesPerSecond"] = document.CaptureFramesPerSecond;
        agent["JpegQuality"] = document.JpegQuality;
        agent["MaxFrameWidth"] = document.MaxFrameWidth;
        agent["ReconnectDelaySeconds"] = document.ReconnectDelaySeconds;
        root[AgentOptions.SectionName] = agent;

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

    private static void Validate(AgentSettingsDocument document)
    {
        var validationContext = new ValidationContext(document);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(document, validationContext, validationResults, validateAllProperties: true))
        {
            throw new ValidationException(validationResults[0].ErrorMessage);
        }
    }
}
