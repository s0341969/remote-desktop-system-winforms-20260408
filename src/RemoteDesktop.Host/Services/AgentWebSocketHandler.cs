using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteDesktop.Host.Models;

namespace RemoteDesktop.Host.Services;

public sealed class AgentWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DeviceBroker _broker;
    private readonly ILogger<AgentWebSocketHandler> _logger;

    public AgentWebSocketHandler(DeviceBroker broker, ILogger<AgentWebSocketHandler> logger)
    {
        _broker = broker;
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket request required.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        DeviceBroker.AgentSession? session = null;

        try
        {
            var helloEnvelope = await WebSocketMessageReader.ReadAsync(socket, context.RequestAborted);
            if (helloEnvelope.MessageType != WebSocketMessageType.Text)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "First message must be hello.", context.RequestAborted);
                return;
            }

            var helloJson = Encoding.UTF8.GetString(helloEnvelope.Payload);
            var hello = JsonSerializer.Deserialize<AgentHelloMessage>(helloJson, JsonOptions);
            if (hello is null || !string.Equals(hello.Type, "hello", StringComparison.OrdinalIgnoreCase))
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Invalid hello payload.", context.RequestAborted);
                return;
            }

            var registration = await _broker.RegisterAgentAsync(socket, hello, context.RequestAborted);
            if (registration.Unauthorized)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Unauthorized agent.", context.RequestAborted);
                return;
            }

            if (registration.Invalid || registration.Session is null)
            {
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid agent metadata.", context.RequestAborted);
                return;
            }

            session = registration.Session;

            var ack = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                type = "hello-ack",
                deviceId = session.DeviceId,
                acceptedAt = DateTimeOffset.UtcNow
            }, JsonOptions));

            await socket.SendAsync(ack, WebSocketMessageType.Text, true, context.RequestAborted);

            while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var message = await WebSocketMessageReader.ReadAsync(socket, context.RequestAborted);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (message.MessageType == WebSocketMessageType.Binary)
                {
                    await _broker.PublishFrameAsync(session.DeviceId, message.Payload, context.RequestAborted);
                    session.LastHeartbeatAt = DateTimeOffset.UtcNow;
                    continue;
                }

                var payload = Encoding.UTF8.GetString(message.Payload);
                var heartbeat = JsonSerializer.Deserialize<AgentHeartbeatMessage>(payload, JsonOptions);
                if (heartbeat is not null && string.Equals(heartbeat.Type, "heartbeat", StringComparison.OrdinalIgnoreCase))
                {
                    await _broker.TouchAgentAsync(session.DeviceId, heartbeat.ScreenWidth, heartbeat.ScreenHeight, context.RequestAborted);
                }
            }
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "處理 Agent WebSocket 連線時發生錯誤。");
        }
        finally
        {
            if (session is not null)
            {
                await _broker.DisconnectAgentAsync(session.DeviceId, "socket-closed", CancellationToken.None);
            }
        }
    }
}
