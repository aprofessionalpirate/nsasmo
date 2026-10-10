using System.Net.WebSockets;
using System.Text.Json;
using SpideyNet.Core;
using SpideyNet.Transport;

internal static class IntegrationChecks
{
    internal static async Task Run(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var room = Guid.NewGuid().ToString("N");
        var first = new RelayClient();
        var second = new RelayClient();
        var isolated = new RelayClient();
        var source = new GameFrame(2, 40, 100, 0, 94);
        first.Publish(source);
        second.Publish(source with { X = 80 });
        isolated.Publish(source);
        var tasks = new[]
        {
            first.Run(endpoint, room, "First", InteractionMode.Experimental, stop.Token),
            second.Run(endpoint, room, "Second", InteractionMode.Experimental, stop.Token),
            isolated.Run(endpoint, Guid.NewGuid().ToString("N"), "Isolated", InteractionMode.Disabled, stop.Token)
        };
        try
        {
            await Until(() => first.World?.Players.Length == 2 && second.World?.Players.Length == 2, stop.Token);
            await Until(() => isolated.World?.Players.Length == 1, stop.Token);
            Check(() => first.World!.Self != second.World!.Self, "Relay assigns distinct identities");
            first.FireWeb();
            await Until(() => second.World?.Players.Any(p => p.Id == second.World.Self
                && p.Constraint == ConstraintKind.Frozen) == true, stop.Token);
            second.Publish(source with { X = 140 });
            await Task.Delay(150, stop.Token);
            Check(() => second.World!.Players.Single(p => p.Id == second.World.Self).Frame.X == 80, "Freeze holds authoritative position");
            await Until(() => second.World?.Players.Single(p => p.Id == second.World.Self).Frame.X == 140, stop.Token);
            second.Publish(source with { X = 80 });
            await Until(() => second.World?.Players.Single(p => p.Id == second.World.Self).Frame.X == 80, stop.Token);
            first.Publish(source with { X = 100 });
            await Until(() => first.World?.Players.Single(p => p.Id == first.World.Self).Constraint == ConstraintKind.Collision, stop.Token);
            Check(() => first.World!.Players.Single(p => p.Id == first.World.Self).Frame.X == 40, "Collision blocks crossing");
            await FragmentCheck(endpoint, stop.Token);
            await CapacityCheck(endpoint, stop.Token);
            await RejectCheck(endpoint, "{broken"u8.ToArray(), WebSocketMessageType.Text, stop.Token);
            await RejectCheck(endpoint, new byte[NetRules.MaxMessageBytes + 1], WebSocketMessageType.Text, stop.Token);
            await RejectCheck(endpoint, [0], WebSocketMessageType.Binary, stop.Token);
            var invalid = JsonSerializer.SerializeToUtf8Bytes(new ClientUpdate(NetRules.Version, 1, source with { Level = 0 }));
            await RejectCheck(endpoint, invalid, WebSocketMessageType.Text, stop.Token);
            Console.WriteLine("Relay checks: identities, room isolation, web freeze, expiry, collision, fragmentation, capacity, malformed/oversized/binary/invalid-room rejection.");
        }
        finally
        {
            stop.Cancel();
            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task RejectCheck(Uri endpoint, byte[] payload, WebSocketMessageType type, CancellationToken token)
    {
        using var socket = new ClientWebSocket();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(Address(endpoint, Guid.NewGuid().ToString("N")), timeout.Token);
        await socket.SendAsync(payload.AsMemory(), type, WebSocketMessageFlags.EndOfMessage, timeout.Token);
        try
        {
            while (await Wire.Receive<WorldState>(socket, timeout.Token) is not null) { }
        }
        catch (WebSocketException) { }
    }

    private static async Task FragmentCheck(Uri endpoint, CancellationToken token)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(Address(endpoint, Guid.NewGuid().ToString("N")), token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ClientUpdate(NetRules.Version, 1, new GameFrame(2, 80, 100, 0, 94)));
        var middle = bytes.Length / 2;
        await socket.SendAsync(bytes.AsMemory(0, middle), WebSocketMessageType.Text, WebSocketMessageFlags.None, token);
        await socket.SendAsync(bytes.AsMemory(middle), WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, token);
        WorldState? state;
        do { state = await Wire.Receive<WorldState>(socket, token); }
        while (state is not null && state.Players.Length == 0);
        Check(() => state?.Players.Single().Frame.X == 80, "Fragmented message assembled");
        socket.Abort();
    }

    private static async Task CapacityCheck(Uri endpoint, CancellationToken token)
    {
        var room = Guid.NewGuid().ToString("N");
        var sockets = new List<ClientWebSocket>();
        try
        {
            for (var index = 0; index < NetRules.MaxPlayers; index++)
            {
                var socket = new ClientWebSocket();
                sockets.Add(socket);
                await socket.ConnectAsync(Address(endpoint, room), token);
            }
            using var excess = new ClientWebSocket();
            try { await excess.ConnectAsync(Address(endpoint, room), token); }
            catch (WebSocketException) { return; }
            throw new InvalidOperationException("Room exceeded capacity.");
        }
        finally
        {
            foreach (var socket in sockets) { socket.Abort(); socket.Dispose(); }
        }
    }

    private static Uri Address(Uri endpoint, string room) => new UriBuilder(endpoint)
    {
        Path = NetRules.SocketPath,
        Query = $"room={room}&name=Test&pvp=0"
    }.Uri;

    private static async Task Until(Func<bool> condition, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition()) { await Task.Delay(20, timeout.Token); }
    }

    private static void Check(Func<bool> condition, string name)
    {
        if (!condition()) { throw new InvalidOperationException(name); }
    }
}
