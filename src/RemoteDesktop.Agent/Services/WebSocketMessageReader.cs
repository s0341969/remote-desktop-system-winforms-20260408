using System.Net.WebSockets;

namespace RemoteDesktop.Agent.Services;

internal static class WebSocketMessageReader
{
    public static async Task<WebSocketMessage> ReadAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024 * 2];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return new WebSocketMessage(WebSocketMessageType.Close, []);
            }

            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
            if (result.EndOfMessage)
            {
                return new WebSocketMessage(result.MessageType, stream.ToArray());
            }
        }
    }

    internal sealed record WebSocketMessage(WebSocketMessageType MessageType, byte[] Payload);
}
