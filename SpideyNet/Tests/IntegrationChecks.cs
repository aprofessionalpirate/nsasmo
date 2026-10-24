using System.Net.WebSockets;
using System.Text.Json;
using SpideyNet.Core;
using SpideyNet.Transport;

internal static class IntegrationChecks
{
    private const int MinSnapshotRate = 24;
    private const int MaxSnapshotRate = 36;
    private const byte StandingPose = 0x18;
    private const byte CrouchingPose = 0x12;
    internal static async Task RunAsync(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var room = Guid.NewGuid().ToString("N");
        var first = new RelayClient();
        var second = new RelayClient();
        var isolated = new RelayClient();
        var source = new GameFrame(2, 40, 100, 0, StandingPose);
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
            await UntilAsync(() => first.World?.Players.Length == 2 && second.World?.Players.Length == 2, stop.Token);
            await UntilAsync(() => isolated.World?.Players.Length == 1, stop.Token);
            Check(() => first.World!.Self != second.World!.Self, "Relay assigns distinct identities");
            const byte CeilingFacing = 4;
            first.Publish(source with { Pose = CrouchingPose, Facing = CeilingFacing });
            await UntilAsync(() => second.World?.Players.Any(p => p.Id == first.World!.Self
                && p.Frame.Pose == CrouchingPose && p.Frame.Facing == CeilingFacing) == true, stop.Token);
            Check(() => second.World!.Players.Single(p => p.Id == second.World.Self).Frame.Pose == StandingPose,
                "Player animations remain independent");
            first.Publish(source);
            first.FireWeb();
            await UntilAsync(() => second.World?.Players.Any(p => p.Id == second.World.Self
                && p.Constraint == ConstraintKind.Frozen) == true, stop.Token);
            second.Publish(source with { X = 140 });
            await Task.Delay(150, stop.Token);
            Check(() => second.World!.Players.Single(p => p.Id == second.World.Self).Frame.X == 80, "Freeze holds authoritative position");
            await UntilAsync(() => second.World?.Players.Single(p => p.Id == second.World.Self).Frame.X == 140, stop.Token);
            second.Publish(source with { X = 80 });
            await UntilAsync(() => second.World?.Players.Single(p => p.Id == second.World.Self).Frame.X == 80, stop.Token);
            first.Publish(source with { X = 100 });
            await UntilAsync(() => first.World?.Players.Single(p => p.Id == first.World.Self).Frame.X == 100, stop.Token);
            Check(() => first.World!.Players.Single(p => p.Id == first.World.Self).Constraint == ConstraintKind.None,
                "Players pass through each other without position corrections");
            await NativeWebCheckAsync(first, second, source, stop.Token);
            await TeleportCheckAsync(first, second, source, stop.Token);
            await FragmentCheckAsync(endpoint, stop.Token);
            await CapacityCheckAsync(endpoint, stop.Token);
            await RejectCheckAsync(endpoint, "{broken"u8.ToArray(), WebSocketMessageType.Text, stop.Token);
            await RejectCheckAsync(endpoint, new byte[NetRules.MaxMessageBytes + 1], WebSocketMessageType.Text, stop.Token);
            await RejectCheckAsync(endpoint, [0], WebSocketMessageType.Binary, stop.Token);
            var invalid = JsonSerializer.SerializeToUtf8Bytes(new ClientUpdate(NetRules.Version, 1, source with { Level = 0 }));
            await RejectCheckAsync(endpoint, invalid, WebSocketMessageType.Text, stop.Token);
            var invalidPose = JsonSerializer.SerializeToUtf8Bytes(new ClientUpdate(NetRules.Version, 1,
                source with { Pose = GameLayout.HiddenCharacter }));
            await RejectCheckAsync(endpoint, invalidPose, WebSocketMessageType.Text, stop.Token);
            var invalidWeb = JsonSerializer.SerializeToUtf8Bytes(new ClientUpdate(NetRules.Version, 1,
                source with { Web = new WebState(short.MaxValue, 0, 0, 0) }));
            await RejectCheckAsync(endpoint, invalidWeb, WebSocketMessageType.Text, stop.Token);
            await CadenceCheckAsync(endpoint, stop.Token);
            Console.WriteLine("Relay checks: identities, room isolation, freeze/expiry, free crossing, native web/pull/release, fragmentation, capacity and invalid-message rejection.");
        }
        finally
        {
            stop.Cancel();
            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task TeleportCheckAsync(RelayClient first, RelayClient second, GameFrame frame, CancellationToken token)
    {
        first.Publish(frame);
        second.Publish(frame with { X = 80 });
        await UntilAsync(() => second.World?.Players.Single(player => player.Id == second.World.Self).Frame.X == 80, token);
        first.FireWeb();
        await UntilAsync(() => second.World?.Players.Single(player => player.Id == second.World.Self).Constraint == ConstraintKind.Frozen, token);
        second.Publish(frame with { X = 180 });
        second.Teleported();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(500);
        await UntilAsync(() => second.World?.Players.Single(player => player.Id == second.World.Self)
            is { Constraint: ConstraintKind.None, Frame.X: 180 }, deadline.Token);
        Console.WriteLine("Teleport socket: relocation clears an existing freeze without waiting for expiry.");
    }

    private static async Task NativeWebCheckAsync(RelayClient first, RelayClient second, GameFrame source, CancellationToken token)
    {
        // Native Space webs carry their geometry; a hit pulls the shooter toward its target.
        second.Publish(source with { X = 140 });
        await UntilAsync(() => second.World?.Players.Single(p => p.Id == second.World.Self).Frame.X == 140, token);
        var web = new WebState(150, 107, 47, 107);
        first.Publish(source with { Web = web, WebHeld = true });
        await UntilAsync(() => second.World?.Players.Any(p => p.Id == first.World!.Self
            && p.Constraint == ConstraintKind.WebPull && p.Frame.Web?.EndX == 147) == true, token);
        await UntilAsync(() => first.World?.Players.Single(p => p.Id == first.World.Self).Frame.X > 50, token);

        second.Publish(source with { Level = 3, X = 140 });
        await UntilAsync(() => first.World?.Players.Single(p => p.Id == first.World.Self).Constraint == ConstraintKind.None, token);
        first.Publish(source with { X = 60 });
        await UntilAsync(() => second.World?.Players.Any(p => p.Id == first.World!.Self
            && p.Frame.X == 60 && p.Frame.Web is null && p.Constraint == ConstraintKind.None) == true, token);

        // Visible webs remain replicated when no player is hit, including another room.
        second.Publish(source with { Level = 3, X = 140 });
        first.Publish(source with { Web = web, WebHeld = true });
        await UntilAsync(() => second.World?.Players.Any(p => p.Id == first.World!.Self
            && p.Frame.Web == web && p.Constraint == ConstraintKind.None) == true, token);
        first.Publish(source);
    }

    private static async Task RejectCheckAsync(Uri endpoint, byte[] payload, WebSocketMessageType type, CancellationToken token)
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

    private static async Task CadenceCheckAsync(Uri endpoint, CancellationToken token)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(Address(endpoint, Guid.NewGuid().ToString("N")), token);
        var first = await Wire.Receive<WorldState>(socket, token)
            ?? throw new InvalidOperationException("Relay closed during cadence test.");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var last = first;
        var received = 0;
        while (clock.ElapsedMilliseconds < NetRules.RateWindowMs)
        {
            last = await Wire.Receive<WorldState>(socket, token)
                ?? throw new InvalidOperationException("Relay closed during cadence test.");
            received++;
        }

        // Allow scheduler jitter; count delivered snapshots rather than timer settings.
        var rate = received / clock.Elapsed.TotalSeconds;
        Check(() => rate >= MinSnapshotRate && rate <= MaxSnapshotRate && last.Tick > first.Tick,
            "Relay delivers snapshots near thirty hertz");
        Console.WriteLine($"Relay snapshot rate: {rate:F1}/sec.");
        socket.Abort();
    }

    private static async Task FragmentCheckAsync(Uri endpoint, CancellationToken token)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(Address(endpoint, Guid.NewGuid().ToString("N")), token);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ClientUpdate(NetRules.Version, 1, new GameFrame(2, 80, 100, 0, StandingPose)));
        var middle = bytes.Length / 2;
        await socket.SendAsync(bytes.AsMemory(0, middle), WebSocketMessageType.Text, WebSocketMessageFlags.None, token);
        await socket.SendAsync(bytes.AsMemory(middle), WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, token);
        WorldState? state;
        do { state = await Wire.Receive<WorldState>(socket, token); }
        while (state is not null && state.Players.Length == 0);
        Check(() => state?.Players.Single().Frame.X == 80, "Fragmented message assembled");
        socket.Abort();
    }

    private static async Task CapacityCheckAsync(Uri endpoint, CancellationToken token)
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

    private static async Task UntilAsync(Func<bool> condition, CancellationToken token)
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
