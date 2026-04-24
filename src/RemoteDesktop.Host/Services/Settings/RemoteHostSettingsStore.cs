using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Options;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Host.Services.Settings;

public sealed class RemoteHostSettingsStore : IHostSettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly HttpClient _httpClient;
    private readonly HostSettingsStore _localSettingsStore;
    private readonly CentralConsoleSessionState _sessionState;
    private readonly string _settingsFilePath;
    private readonly ControlServerOptions _options;
    private bool _disposed;

    public RemoteHostSettingsStore(
        IHostEnvironment environment,
        HostSettingsStore localSettingsStore,
        IOptions<ControlServerOptions> options,
        CentralConsoleSessionState sessionState)
    {
        _localSettingsStore = localSettingsStore;
        _options = options.Value;
        _sessionState = sessionState;
        _settingsFilePath = Path.Combine(environment.ContentRootPath, "appsettings.json");
        var baseAddress = NormalizeBaseAddress(_options.CentralServerUrl!);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<HostSettingsDocument> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/settings/host");
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<HostSettingsDto>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Central server did not return host settings.");

            return new HostSettingsDocument
            {
                IsCentralSettingsAvailable = true,
                IsLocalFallbackMode = false,
                EnableDatabase = dto.EnableDatabase,
                RemoteDesktopDbConnectionString = dto.RemoteDesktopDbConnectionString,
                ServerUrl = dto.ServerUrl,
                CentralServerUrl = _options.CentralServerUrl,
                ConsoleName = dto.ConsoleName,
                AdminUserName = dto.AdminUserName,
                AdminPassword = dto.AdminPassword,
                SharedAccessKey = dto.SharedAccessKey,
                RequireHttpsRedirect = dto.RequireHttpsRedirect,
                AgentHeartbeatTimeoutSeconds = dto.AgentHeartbeatTimeoutSeconds
            };
        }
        catch (Exception exception) when (IsCentralServerUnavailable(exception, cancellationToken))
        {
            var localDocument = await _localSettingsStore.LoadAsync(cancellationToken);
            localDocument.IsCentralSettingsAvailable = false;
            localDocument.IsLocalFallbackMode = true;
            localDocument.SettingsStatusMessage =
                "目前無法連線到中央 Server，設定畫面已改用本機 appsettings.json。此時儲存只會更新本機設定，不會同步到中央 Server。";
            return localDocument;
        }
    }

    public async Task SaveAsync(HostSettingsDocument document, CancellationToken cancellationToken)
    {
        Validate(document);
        if (document.IsLocalFallbackMode || !document.IsCentralSettingsAvailable)
        {
            await _localSettingsStore.SaveAsync(document, cancellationToken);
            return;
        }

        try
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/settings/host");
            request.Content = JsonContent.Create(new HostSettingsDto
            {
                EnableDatabase = document.EnableDatabase,
                RemoteDesktopDbConnectionString = document.RemoteDesktopDbConnectionString,
                ServerUrl = document.ServerUrl,
                ConsoleName = document.ConsoleName,
                AdminUserName = document.AdminUserName,
                AdminPassword = document.AdminPassword,
                SharedAccessKey = document.SharedAccessKey,
                RequireHttpsRedirect = document.RequireHttpsRedirect,
                AgentHeartbeatTimeoutSeconds = document.AgentHeartbeatTimeoutSeconds
            });

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var message = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new ValidationException(message);
            }

            response.EnsureSuccessStatusCode();
            await SaveLocalCentralServerUrlAsync(document.CentralServerUrl, cancellationToken);
        }
        catch (Exception exception) when (IsCentralServerUnavailable(exception, cancellationToken))
        {
            await _localSettingsStore.SaveAsync(document, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        _sessionState.ApplyAuthorizationHeader(request);
        return request;
    }

    private async Task SaveLocalCentralServerUrlAsync(string? centralServerUrl, CancellationToken cancellationToken)
    {
        var root = await ReadRootAsync(cancellationToken) ?? new JsonObject();
        var controlServer = root[ControlServerOptions.SectionName] as JsonObject ?? new JsonObject();
        controlServer["CentralServerUrl"] = string.IsNullOrWhiteSpace(centralServerUrl)
            ? null
            : centralServerUrl.Trim();
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

    private static string NormalizeBaseAddress(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        return trimmed;
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

    private static bool IsCentralServerUnavailable(Exception exception, CancellationToken cancellationToken)
    {
        if (exception is ValidationException)
        {
            return false;
        }

        if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return true;
        }

        return exception is HttpRequestException
            or TaskCanceledException
            or IOException
            or InvalidOperationException;
    }
}
