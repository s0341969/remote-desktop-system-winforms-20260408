using System.Buffers;
using System.Net.WebSockets;

namespace RemoteDesktop.Server.Services;

internal static class WebSocketMessageReader
{
    public static async Task<WebSocketMessage> ReadAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        using var stream = new MemoryStream();
        try
        {
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
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal sealed record WebSocketMessage(WebSocketMessageType MessageType, byte[] Payload);
}
