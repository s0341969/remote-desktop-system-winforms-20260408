using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using RemoteDesktop.Server.Services.Security;
using RemoteDesktop.Shared.Models;

namespace RemoteDesktop.Server.Services;

public sealed class ViewerWebSocketHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DeviceBroker _broker;
    private readonly ConsoleSessionTokenService _sessionTokenService;

    public ViewerWebSocketHandler(DeviceBroker broker, ConsoleSessionTokenService sessionTokenService)
    {
        _broker = broker;
        _sessionTokenService = sessionTokenService;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!_sessionTokenService.TryAuthenticate(context.Request, out var session))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var deviceId = context.Request.Query["deviceId"].ToString();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Missing deviceId.");
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var attached = await _broker.AttachViewerAsync(
            deviceId,
            session.DisplayName,
            _sessionTokenService.CanControlRemote(session),
            async (payload, cancellationToken) =>
            {
                await socket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
            },
            async (status, cancellationToken) =>
            {
                var envelope = new ViewerTransportEnvelope
                {
                    Type = "transfer-status",
                    DeviceId = deviceId,
                    TransferStatus = status
                };

                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            },
            async (clipboard, cancellationToken) =>
            {
                var envelope = new ViewerTransportEnvelope
                {
                    Type = "clipboard",
                    DeviceId = deviceId,
                    Clipboard = clipboard
                };

                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, JsonOptions));
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            },
            context.RequestAborted);

        if (!attached)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Device unavailable or busy.", context.RequestAborted);
            return;
        }

        var readyEnvelope = new ViewerTransportEnvelope
        {
            Type = "viewer-ready",
            DeviceId = deviceId,
            Message = "viewer-ready"
        };

        var readyPayload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(readyEnvelope, JsonOptions));
        await socket.SendAsync(readyPayload, WebSocketMessageType.Text, true, context.RequestAborted);

        try
        {
            while (!context.RequestAborted.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var message = await WebSocketMessageReader.ReadAsync(socket, context.RequestAborted);
                if (message.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (message.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var json = Encoding.UTF8.GetString(message.Payload);
                await _broker.ForwardViewerCommandAsync(deviceId, json, context.RequestAborted);
            }
        }
        finally
        {
            await _broker.DetachViewerAsync(deviceId);
        }
    }
}
