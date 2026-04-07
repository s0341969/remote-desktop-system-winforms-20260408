using System.Net.WebSockets;
using System.Text;

namespace RemoteDesktop.Host.Services;

public sealed class ViewerWebSocketHandler
{
    private readonly DeviceBroker _broker;

    public ViewerWebSocketHandler(DeviceBroker broker)
    {
        _broker = broker;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
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
        var attached = await _broker.AttachViewerAsync(deviceId, context.User.Identity.Name ?? "viewer", socket, context.RequestAborted);
        if (!attached)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Device unavailable or busy.", context.RequestAborted);
            return;
        }

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
