using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using SpideyNet.Core;

namespace SpideyNet.Transport;

public static class Wire
{
    public static async Task<T?> Receive<T>(WebSocket socket, CancellationToken token)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(NetRules.MaxMessageBytes);
        try
        {
            var used = 0;

            // Assemble fragmented frames within a fixed allocation.
            while (used < NetRules.MaxMessageBytes)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(used, NetRules.MaxMessageBytes - used), token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return default;
                }
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidDataException("Expected a text message.");
                }

                used += result.Count;
                if (result.EndOfMessage)
                {
                    return JsonSerializer.Deserialize<T>(buffer.AsSpan(0, used))
                        ?? throw new InvalidDataException("Empty message.");
                }
            }

            throw new InvalidDataException("Message exceeds the size limit.");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async Task Send<T>(WebSocket socket, T message, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        if (bytes.Length > NetRules.MaxMessageBytes)
        {
            throw new InvalidDataException("Message exceeds the size limit.");
        }

        await socket.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text,
            WebSocketMessageFlags.EndOfMessage, token);
    }
}
