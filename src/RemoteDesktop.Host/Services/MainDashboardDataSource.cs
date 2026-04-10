using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RemoteDesktop.Host.Models;
using RemoteDesktop.Host.Options;
using SharedDashboardUpdateEnvelope = RemoteDesktop.Shared.Models.DashboardUpdateEnvelope;
using SharedPresenceLogRecord = RemoteDesktop.Shared.Models.AgentPresenceLogRecord;
using SharedDeviceRecord = RemoteDesktop.Shared.Models.DeviceRecord;

namespace RemoteDesktop.Host.Services;

public interface IMainDashboardDataSource
{
    string DashboardServerUrl { get; }

    string HealthUrl { get; }

    bool SupportsViewerSessions { get; }

    bool SupportsRealtimeUpdates { get; }

    event EventHandler? DashboardUpdated;

    Task StartAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken);

    Task<bool> SetDeviceAuthorizationAsync(string deviceId, bool isAuthorized, string changedBy, CancellationToken cancellationToken);
}

public sealed class MainDashboardDataSourceFactory
{
    private readonly IDeviceRepository _repository;
    private readonly DeviceBroker _deviceBroker;
    private readonly IOptions<ControlServerOptions> _options;
    private readonly CentralConsoleSessionState _sessionState;

    public MainDashboardDataSourceFactory(
        IDeviceRepository repository,
        DeviceBroker deviceBroker,
        IOptions<ControlServerOptions> options,
        CentralConsoleSessionState sessionState)
    {
        _repository = repository;
        _deviceBroker = deviceBroker;
        _options = options;
        _sessionState = sessionState;
    }

    public IMainDashboardDataSource Create()
    {
        var options = _options.Value;
        if (string.IsNullOrWhiteSpace(options.CentralServerUrl))
        {
            return new LocalMainDashboardDataSource(_repository, _deviceBroker, options);
        }

        return new RemoteMainDashboardDataSource(options, _sessionState);
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

    public bool SupportsRealtimeUpdates => false;

    public event EventHandler? DashboardUpdated
    {
        add { }
        remove { }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly CentralConsoleSessionState _sessionState;
    private readonly Uri _dashboardWebSocketUri;
    private readonly CancellationTokenSource _disposeCancellationTokenSource = new();
    private Task? _backgroundTask;
    private int _started;
    private bool _disposed;

    public RemoteMainDashboardDataSource(ControlServerOptions options, CentralConsoleSessionState sessionState)
    {
        _sessionState = sessionState;
        var baseAddress = NormalizeBaseAddress(options.CentralServerUrl!);
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(15)
        };
        _dashboardWebSocketUri = BuildDashboardWebSocketUri(_httpClient.BaseAddress!);
    }

    public string DashboardServerUrl => _httpClient.BaseAddress!.ToString().TrimEnd('/');

    public string HealthUrl => $"{DashboardServerUrl}/healthz";

    public bool SupportsViewerSessions => true;

    public bool SupportsRealtimeUpdates => true;

    public event EventHandler? DashboardUpdated;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            return _backgroundTask ?? Task.CompletedTask;
        }

        var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCancellationTokenSource.Token);
        _backgroundTask = Task.Run(() => RunDashboardUpdatesLoopAsync(linkedCancellationTokenSource.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DeviceRecord>> GetDevicesAsync(int take, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/devices?take={Math.Clamp(take, 1, 500)}");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var devices = await response.Content.ReadFromJsonAsync<List<SharedDeviceRecord>>(cancellationToken: cancellationToken);
        return devices?.Select(MapDeviceRecord).ToList() ?? [];
    }

    public async Task<IReadOnlyList<AgentPresenceLogRecord>> GetPresenceLogsAsync(int take, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/presence-logs?take={Math.Clamp(take, 1, 500)}");
        var response = await _httpClient.SendAsync(request, cancellationToken);
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
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"/api/devices/{encodedDeviceId}/authorization?isAuthorized={isAuthorized}&changedBy={encodedChangedBy}");
        var response = await _httpClient.SendAsync(request, cancellationToken);

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

        _disposeCancellationTokenSource.Cancel();
        _httpClient.Dispose();
        _disposeCancellationTokenSource.Dispose();
        _disposed = true;
    }

    private async Task RunDashboardUpdatesLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                _sessionState.ApplyAuthorizationHeader(socket);
                await socket.ConnectAsync(_dashboardWebSocketUri, cancellationToken);
                await ListenForDashboardUpdatesAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task ListenForDashboardUpdatesAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var message = await WebSocketMessageReader.ReadAsync(socket, cancellationToken);
            if (message.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (message.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var envelope = JsonSerializer.Deserialize<SharedDashboardUpdateEnvelope>(Encoding.UTF8.GetString(message.Payload), JsonOptions);
            if (envelope is null)
            {
                continue;
            }

            if (string.Equals(envelope.Type, "dashboard-ready", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(envelope.Type, "dashboard-changed", StringComparison.OrdinalIgnoreCase))
            {
                DashboardUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private static Uri BuildDashboardWebSocketUri(Uri baseAddress)
    {
        var builder = new UriBuilder(baseAddress)
        {
            Path = "/ws/dashboard",
            Query = string.Empty,
            Scheme = string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? Uri.UriSchemeWss : Uri.UriSchemeWs
        };

        return builder.Uri;
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

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        _sessionState.ApplyAuthorizationHeader(request);
        return request;
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


