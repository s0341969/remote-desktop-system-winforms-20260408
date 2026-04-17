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
    private readonly AgentRuntimeState _runtimeState;
    private readonly AgentInventoryService _inventoryService;
    private readonly DesktopCaptureService _captureService;
    private readonly ClipboardSyncService _clipboardSyncService;
    private readonly InputInjectionService _inputInjectionService;
    private readonly FileTransferService _fileTransferService;
    private readonly ILogger<RemoteAgentService> _logger;
    private volatile int _lastScreenWidth;
    private volatile int _lastScreenHeight;

    public RemoteAgentService(
        IOptions<AgentOptions> options,
        AgentRuntimeState runtimeState,
        AgentInventoryService inventoryService,
        DesktopCaptureService captureService,
        ClipboardSyncService clipboardSyncService,
        InputInjectionService inputInjectionService,
        FileTransferService fileTransferService,
        ILogger<RemoteAgentService> logger)
    {
        _options = options.Value;
        _runtimeState = runtimeState;
        _inventoryService = inventoryService;
        _captureService = captureService;
        _clipboardSyncService = clipboardSyncService;
        _inputInjectionService = inputInjectionService;
        _fileTransferService = fileTransferService;
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
                _runtimeState.MarkError(exception);
                _logger.LogError(exception, "Agent loop failed unexpectedly.");
            }

            _runtimeState.MarkDisconnected(AgentUiText.Bi($"將於 {_options.ReconnectDelaySeconds} 秒後重新連線。", $"Reconnecting in {_options.ReconnectDelaySeconds} seconds."));
            await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), stoppingToken);
        }
    }

    private async Task RunAgentLoopAsync(CancellationToken cancellationToken)
    {
        using var socket = new ClientWebSocket();
        using var sendLock = new SemaphoreSlim(1, 1);
        var serverUri = BuildAgentWebSocketUri(_options.ServerUrl);
        _runtimeState.MarkConnecting(serverUri);
        await socket.ConnectAsync(serverUri, cancellationToken);
        _runtimeState.MarkConnected();
        if (!_inputInjectionService.IsProcessElevated())
        {
            _runtimeState.MarkWarning(AgentUiText.Bi(
                "Agent 目前未以系統管理員權限執行。一般視窗可正常控制，但較高權限程式、UAC 或安全桌面可能拒絕接收輸入。",
                "The Agent is not running elevated. Standard windows can still be controlled, but higher-privilege apps, UAC prompts, or the secure desktop may reject input."));
        }

        _logger.LogInformation("Connected to Control Server: {ServerUri}", serverUri);

        var screenSize = _captureService.GetVirtualScreenSize();
        var inventory = _inventoryService.Collect();
        _lastScreenWidth = screenSize.Width;
        _lastScreenHeight = screenSize.Height;
        await SendTextAsync(socket, sendLock, CreateHelloPayload(screenSize.Width, screenSize.Height, inventory), cancellationToken);

        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = ReceiveLoopAsync(socket, sendLock, loopCts.Token);
        var heartbeatTask = HeartbeatLoopAsync(socket, sendLock, loopCts.Token);
        var captureTask = CaptureLoopAsync(socket, sendLock, loopCts.Token);
        var inventoryTask = InventoryLoopAsync(socket, sendLock, loopCts.Token);
        var completedTask = await Task.WhenAny(receiveTask, heartbeatTask, captureTask, inventoryTask);
        loopCts.Cancel();

        try
        {
            await completedTask;
            await Task.WhenAll(receiveTask, heartbeatTask, captureTask, inventoryTask);
        }
        catch (OperationCanceledException) when (loopCts.IsCancellationRequested)
        {
        }

        if (socket.State == WebSocketState.Open)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "agent-loop-finished", cancellationToken);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
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

            var handled = await _fileTransferService.TryHandleAsync(
                command,
                async (status, token) =>
                {
                    var payload = JsonSerializer.Serialize(status, JsonOptions);
                    await SendTextAsync(socket, sendLock, payload, token);
                },
                cancellationToken);

            if (handled)
            {
                continue;
            }

            if (await TryHandleClipboardAsync(command, socket, sendLock, cancellationToken))
            {
                continue;
            }

            _inputInjectionService.Apply(command);
        }
    }

    private async Task<bool> TryHandleClipboardAsync(
        ViewerCommandMessage command,
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        switch (command.Type)
        {
            case "clipboard-set":
                await HandleClipboardSetAsync(command, socket, sendLock, cancellationToken);
                return true;
            case "clipboard-get":
                await HandleClipboardGetAsync(socket, sendLock, cancellationToken);
                return true;
            default:
                return false;
        }
    }

    private async Task HandleClipboardSetAsync(
        ViewerCommandMessage command,
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = command.ClipboardText ?? string.Empty;
            await _clipboardSyncService.SetTextAsync(text, cancellationToken);
            await PublishClipboardMessageAsync(socket, sendLock, new AgentClipboardMessage
            {
                Operation = "set",
                Status = "completed",
                Text = string.Empty,
                Truncated = false,
                Message = AgentUiText.Bi("遠端剪貼簿已更新。", "Remote clipboard updated successfully.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            await PublishClipboardMessageAsync(socket, sendLock, new AgentClipboardMessage
            {
                Operation = "set",
                Status = "failed",
                Text = string.Empty,
                Truncated = false,
                Message = AgentUiText.Bi($"遠端剪貼簿更新失敗：{exception.Message}", $"Remote clipboard update failed: {exception.Message}")
            }, cancellationToken);
        }
    }

    private async Task HandleClipboardGetAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        try
        {
            const int maxClipboardCharacters = 32_768;
            var text = await _clipboardSyncService.GetTextAsync(cancellationToken);
            var truncated = false;
            if (text.Length > maxClipboardCharacters)
            {
                text = text[..maxClipboardCharacters];
                truncated = true;
            }

            await PublishClipboardMessageAsync(socket, sendLock, new AgentClipboardMessage
            {
                Operation = "get",
                Status = "completed",
                Text = text,
                Truncated = truncated,
                Message = truncated
                    ? AgentUiText.Bi("遠端剪貼簿已截斷到系統支援的最大長度。", "Remote clipboard was truncated to the maximum supported length.")
                    : AgentUiText.Bi("遠端剪貼簿已成功讀取。", "Remote clipboard retrieved successfully.")
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            await PublishClipboardMessageAsync(socket, sendLock, new AgentClipboardMessage
            {
                Operation = "get",
                Status = "failed",
                Text = string.Empty,
                Truncated = false,
                Message = AgentUiText.Bi($"讀取遠端剪貼簿失敗：{exception.Message}", $"Remote clipboard read failed: {exception.Message}")
            }, cancellationToken);
        }
    }

    private static Task PublishClipboardMessageAsync(
        ClientWebSocket socket,
        SemaphoreSlim sendLock,
        AgentClipboardMessage message,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(message, JsonOptions);
        return SendTextAsync(socket, sendLock, payload, cancellationToken);
    }

    private async Task HeartbeatLoopAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var heartbeat = JsonSerializer.Serialize(new AgentHeartbeatMessage
            {
                ScreenWidth = _lastScreenWidth,
                ScreenHeight = _lastScreenHeight
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
            _lastScreenWidth = frame.Width;
            _lastScreenHeight = frame.Height;
            await sendLock.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(frame.Payload, WebSocketMessageType.Binary, true, cancellationToken);
                _runtimeState.MarkFrameSent();
            }
            finally
            {
                sendLock.Release();
            }

            await Task.Delay(delayMs, cancellationToken);
        }
    }

    private async Task InventoryLoopAsync(ClientWebSocket socket, SemaphoreSlim sendLock, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.InventoryRefreshMinutes));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var inventory = _inventoryService.Collect();
            var payload = JsonSerializer.Serialize(new AgentInventoryUpdateMessage
            {
                Inventory = inventory
            }, JsonOptions);

            await SendTextAsync(socket, sendLock, payload, cancellationToken);
        }
    }

    private string CreateHelloPayload(int screenWidth, int screenHeight, AgentInventoryProfile inventory)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        var machineIdentity = AgentIdentity.GetMachineIdentity();
        return JsonSerializer.Serialize(new AgentHelloMessage
        {
            DeviceId = machineIdentity,
            DeviceName = machineIdentity,
            HostName = machineIdentity,
            AgentVersion = version,
            AccessKey = _options.SharedAccessKey,
            ScreenWidth = screenWidth,
            ScreenHeight = screenHeight,
            Inventory = inventory
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
        var builder = new UriBuilder(serverUrl)
        {
            Scheme = serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = "/ws/agent"
        };

        return builder.Uri;
    }
}
