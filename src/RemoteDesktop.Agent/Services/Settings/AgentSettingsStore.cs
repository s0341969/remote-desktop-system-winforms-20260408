using System.ComponentModel.DataAnnotations;
using System.Text;
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
        var machineIdentity = AgentIdentity.GetMachineIdentity();
        var document = new AgentSettingsDocument
        {
            ServerUrl = _options.Value.ServerUrl,
            DeviceId = machineIdentity,
            DeviceName = machineIdentity,
            SharedAccessKey = _options.Value.SharedAccessKey,
            FileTransferDirectory = _options.Value.FileTransferDirectory,
            CaptureFramesPerSecond = _options.Value.CaptureFramesPerSecond,
            JpegQuality = _options.Value.JpegQuality,
            MaxFrameWidth = _options.Value.MaxFrameWidth,
            ReconnectDelaySeconds = _options.Value.ReconnectDelaySeconds,
            HeartbeatIntervalSeconds = _options.Value.HeartbeatIntervalSeconds
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
            document.DeviceId = machineIdentity;
            document.DeviceName = machineIdentity;
            document.SharedAccessKey = agent["SharedAccessKey"]?.GetValue<string>() ?? document.SharedAccessKey;
            document.FileTransferDirectory = agent["FileTransferDirectory"]?.GetValue<string>() ?? document.FileTransferDirectory;
            document.CaptureFramesPerSecond = agent["CaptureFramesPerSecond"]?.GetValue<int?>() ?? document.CaptureFramesPerSecond;
            document.JpegQuality = agent["JpegQuality"]?.GetValue<long?>() ?? document.JpegQuality;
            document.MaxFrameWidth = agent["MaxFrameWidth"]?.GetValue<int?>() ?? document.MaxFrameWidth;
            document.ReconnectDelaySeconds = agent["ReconnectDelaySeconds"]?.GetValue<int?>() ?? document.ReconnectDelaySeconds;
            document.HeartbeatIntervalSeconds = agent["HeartbeatIntervalSeconds"]?.GetValue<int?>() ?? document.HeartbeatIntervalSeconds;
        }

        return document;
    }

    public async Task SaveAsync(AgentSettingsDocument document, CancellationToken cancellationToken)
    {
        var machineIdentity = AgentIdentity.GetMachineIdentity();
        document.DeviceId = machineIdentity;
        document.DeviceName = machineIdentity;
        Validate(document);
        var root = await ReadRootAsync(cancellationToken) ?? new JsonObject();
        var agent = root[AgentOptions.SectionName] as JsonObject ?? new JsonObject();
        agent["ServerUrl"] = document.ServerUrl.Trim();
        agent["DeviceId"] = machineIdentity;
        agent["DeviceName"] = machineIdentity;
        agent["SharedAccessKey"] = document.SharedAccessKey;
        agent["FileTransferDirectory"] = document.FileTransferDirectory.Trim();
        agent["CaptureFramesPerSecond"] = document.CaptureFramesPerSecond;
        agent["JpegQuality"] = document.JpegQuality;
        agent["MaxFrameWidth"] = document.MaxFrameWidth;
        agent["ReconnectDelaySeconds"] = document.ReconnectDelaySeconds;
        agent["HeartbeatIntervalSeconds"] = document.HeartbeatIntervalSeconds;
        root[AgentOptions.SectionName] = agent;

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
            var json = await ReadAllTextAsync(_settingsFilePath, cancellationToken);
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
            await WriteAllTextAsync(tempFilePath, json, cancellationToken);
            ReplaceFile(tempFilePath, _settingsFilePath);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
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

    private static async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using (cancellationToken.Register(() => stream.Dispose()))
        {
            return await reader.ReadToEndAsync();
        }
    }

    private static async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        using (cancellationToken.Register(() => stream.Dispose()))
        {
            await writer.WriteAsync(content);
            await writer.FlushAsync();
            stream.Flush(true);
        }
    }

    private static void ReplaceFile(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(sourcePath, destinationPath);
    }
}
