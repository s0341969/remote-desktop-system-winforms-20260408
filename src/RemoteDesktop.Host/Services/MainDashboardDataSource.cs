using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using SharedPresenceLogRecord = RemoteDesktop.Shared.Models.AgentPresenceLogRecord;
using SharedDeviceRecord = RemoteDesktop.Shared.Models.DeviceRecord;

namespace RemoteDesktop.Host.Services;

public interface IMainDashboardDataSource
{
    string DashboardServerUrl { get; }

    string HealthUrl { get; }

    bool SupportsViewerSessions { get; }

    Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken);

    Task<bool> SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedBy, CancellationToken cancellationToken);
}

public sealed class MainDashboardDataSourceFactory
{
    private readonly IDeviceRepository _repository;
    private readonly DeviceBroker _deviceBroker;
    private readonly IOptions<ControlServerOptions> _options;

    public MainDashboardDataSourceFactory(
        IDeviceRepository repository,
        DeviceBroker deviceBroker,
        IOptions<ControlServerOptions> options)
    {
        _repository = repository;
        _deviceBroker = deviceBroker;
        _options = options;
    }

    public IMainDashboardDataSource Create()
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.CentralServerUrl))
        {
            return new LocalMainDashboardDataSource(_repository, _deviceBroker, options);
        }

        return new RemoteMainDashboardDataSource(options);
    }
}

internal sealed class LocalMainDashboardDataSource : IMainDashboardDataSource
{
    private readonly IDeviceRepository _repository;
    private readonly DeviceBroker _deviceBroker;
    private readonly ControlServerOptions _options;

    public LocalMainDashboardDataSource(
        IDeviceRepository repository,
        DeviceBroker deviceBroker,
        ControlServerOptions options)
    {
        _repository = repository;
        _deviceBroker = deviceBroker;
        _options = options;
    }

    public string DashboardServerUrl => _options.ServerUrl;

    public string HealthUrl => $"{_options.ServerUrl.TrimEnd('/')}/healthz";

    public bool SupportsViewerSessions => true;

    public Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        return _repository.GetDevicesAsync(take, cancellationToken);
    }

    public Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken)
    {
        return _repository.GetPresenceLogsAsync(take, cancellationToken);
    }

    public Task<bool> SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedBy, CancellationToken cancellationToken)
    {
        return _deviceBroker.SetDeviceAuthorizationAsync(deviceId, isAuthorized, changedBy, cancellationToken);
    }
}

internal sealed class RemoteMainDashboardDataSource : IMainDashboardDataSource, IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public RemoteMainDashboardDataSource(ControlServerOptions options)
    {
        var baseAddress = NormalizeBaseAddress(options.CentralServerUrl!);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public string DashboardServerUrl => _httpClient.BaseAddress!.ToString().TrimEnd('/');

    public string HealthUrl => $"{DashboardServerUrl}/healthz";

    public bool SupportsViewerSessions => false;

    public async Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/api/devices?take={Math.Clamp(take, 1, 500)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var devices = await response.Content.ReadFromJsonAsync<List<SharedDeviceRecord>>(cancellationToken: cancellationToken);
        return devices?.Select(MapDeviceRecord).ToList() ?? [];
    }

    public async Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/api/presence-logs?take={Math.Clamp(take, 1, 500)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var logs = await response.Content.ReadFromJsonAsync<List<SharedPresenceLogRecord>>(cancellationToken: cancellationToken);
        return logs?.Select(MapPresenceLogRecord).ToList() ?? [];
    }

    public async Task<bool> SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var encodedDeviceId = Uri.EscapeDataString(deviceId);
        var encodedChangedBy = Uri.EscapeDataString(changedBy);
        var response = await _httpClient.PostAsync(
            $"/api/devices/{encodedDeviceId}/authorization?isAuthorized={isAuthorized}&changedBy={encodedChangedBy}",
            content: null,
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
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

    private static string NormalizeBaseAddress(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.EndsWith("/", StringComparison.Ordinal))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    private static DeviceRecord MapDeviceRecord(SharedDeviceRecord source)
    {
        return new DeviceRecord
        {
            DeviceId = source.DeviceId,
            DeviceName = source.DeviceName,
            HostName = source.HostName,
            AgentVersion = source.AgentVersion,
            ScreenWidth = source.ScreenWidth,
            ScreenHeight = source.ScreenHeight,
            IsOnline = source.IsOnline,
            IsAuthorized = source.IsAuthorized,
            AuthorizedAt = source.AuthorizedAt,
            AuthorizedBy = source.AuthorizedBy,
            CreatedAt = source.CreatedAt,
            LastSeenAt = source.LastSeenAt,
            LastConnectedAt = source.LastConnectedAt,
            LastDisconnectedAt = source.LastDisconnectedAt
        };
    }

    private static AgentPresenceLogRecord MapPresenceLogRecord(SharedPresenceLogRecord source)
    {
        return new AgentPresenceLogRecord
        {
            PresenceId = source.PresenceId,
            DeviceId = source.DeviceId,
            DeviceName = source.DeviceName,
            HostName = source.HostName,
            AgentVersion = source.AgentVersion,
            ConnectedAt = source.ConnectedAt,
            LastSeenAt = source.LastSeenAt,
            DisconnectedAt = source.DisconnectedAt,
            DisconnectReason = source.DisconnectReason,
            OnlineSeconds = source.OnlineSeconds
        };
    }
}
