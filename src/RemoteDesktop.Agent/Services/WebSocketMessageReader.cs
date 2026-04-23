using System.Buffers;
using System.Net.WebSockets;

namespace RemoteDesktop.Agent.Services;

internal static class WebSocketMessageReader
{
    public static async Task<WebSocketMessage> ReadAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        using var stream = new MemoryStream();
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return new WebSocketMessage(WebSocketMessageType.Close, Array.Empty<byte>());
                }

                await stream.WriteAsync(buffer, 0, result.Count, cancellationToken);
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
