using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteDesktop.Agent.Models;
using RemoteDesktop.Agent.Options;

namespace RemoteDesktop.Agent.Services;

public sealed class RemoteAgentService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AgentOptions _options;
    private readonly DesktopCaptureService _captureService;
    private readonly InputInjectionService _inputInjectionService;
    private readonly ILogger<RemoteAgentService> _logger;

    public RemoteAgentService(
        IOptions<AgentOptions> options,
        DesktopCaptureService captureService,
        InputInjectionService inputInjectionService,
        ILogger<RemoteAgentService> logger)
    {
        _options = options.Value;
        _captureService = captureService;
        _inputInjectionService = inputInjectionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAgentLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Agent 連線循環失敗，將於稍後重試。");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
        }
    }

    private async Task RunAgentLoopAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        using var sendLock = new SemaphoreSlim(1, 1);
        var serverUri = BuildAgentWebSocketUri(_options.ServerUrl);
        await socket.ConnectAsync(serverUri, cancellationToken);
        _logger.LogInformation("已連線到 Control Server: {ServerUri}", serverUri);

        var initialFrame = _captureService.Capture();
        await SendTextAsync(socket, sendLock, CreateHelloPayload(initialFrame), cancellationToken);

        var receiveTask = ReceiveLoopAsync(socket, cancellationToken);
        var heartbeatTask = HeartbeatLoopAsync(socket, sendLock, cancellationToken);
        var captureTask = CaptureLoopAsync(socket, sendLock, cancellationToken);

        await Task.WhenAny(receiveTask, heartbeatTask, captureTask);

        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "agent-loop-finished", cancellationToken);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var message = await WebSocketMessageReader.ReadAsync(socket, cancellationToken);
            if (message.MessageType == WebSocketMessageType.Close)
            {
                return;
            }

            if (message.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(message.Payload);
            if (json.Contains("\"hello-ack\"", StringComparison.Ordinal))
            {
                continue;
            }

            var command = JsonSerializer.Deserialize<ViewerCommandMessage>(json, JsonOptions);
            if (command is null)
            {
                continue;
            }

            _inputInjectionService.Apply(command);
        }
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var frame = _captureService.Capture();
            var heartbeat = JsonSerializer.Serialize(new AgentHeartbeatMessage
            {
                ScreenWidth = frame.Width,
                ScreenHeight = frame.Height
            }, JsonOptions);

            await SendTextAsync(socket, sendLock, heartbeat, cancellationToken);
        }
    }

    private async Task CaptureLoopAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        var delayMs = Math.Clamp(1000 / Math.Max(_options.CaptureFramesPerSecond, 1), 40, 1000);
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var frame = _captureService.Capture();
            await sendLock.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(frame.Payload, WebSocketMessageType.Binary, true, cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }

            await Task.Delay(delayMs, cancellationToken);
        }
    }

    private string CreateHelloPayload(DesktopFrame frame)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        return JsonSerializer.Serialize(new AgentHelloMessage
        {
            DeviceId = _options.DeviceId,
            DeviceName = _options.DeviceName,
            HostName = Environment.MachineName,
            AgentVersion = version,
            AccessKey = _options.SharedAccessKey,
            ScreenWidth = frame.Width,
            ScreenHeight = frame.Height
        }, JsonOptions);
    }

    private static async Task SendTextAsync(ClientWebSocket socket, SemaphoreSlim sendLock, string payload, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await sendLock.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private static Uri BuildAgentWebSocketUri(string serverUrl)
    {
        var builder = new UriBuilder(serverUrl);
        builder.Scheme = builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        builder.Path = "/ws/agent";
        return builder.Uri;
    }
}
