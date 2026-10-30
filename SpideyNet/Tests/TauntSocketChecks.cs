using System.Net.WebSockets;
using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Tests;

internal static class TauntSocketChecks
{
    internal static async Task RunAsync(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var receiverStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        var sender = new RelayClient();
        var receiver = new RelayClient();
        var isolated = new RelayClient();
        var late = new RelayClient();
        var room = Guid.NewGuid().ToString("N");
        var frame = new GameFrame(1, 40, 147, 0, 24);
        foreach (var client in new[] { sender, receiver, isolated, late })
        {
            client.Publish(frame);
        }

        // Taunts belong to the private relay room, even when players occupy different game rooms.
        receiver.Publish(frame with { Level = 3 });
        var receiving = receiver.Run(endpoint, room, "Listener", InteractionMode.Disabled, receiverStop.Token);
        var connections = new List<Task>
        {
            sender.Run(endpoint, room, "Sender", InteractionMode.Disabled, stop.Token),
            receiving,
            isolated.Run(endpoint, Guid.NewGuid().ToString("N"), "Isolated", InteractionMode.Disabled, stop.Token)
        };
        try
        {
            await UntilAsync(() => sender.World?.Players.Length == 2 && receiver.World?.Players.Length == 2
                && isolated.World is not null, stop.Token);
            Check(sender.SendTaunt(TauntId.SpiderMan), "Queue a supported clip");
            var heard = await ReceiveAsync(receiver, 1, stop.Token);
            var echo = await ReceiveAsync(sender, 1, stop.Token);
            Check(heard[0] == echo[0] && heard[0].Player == sender.World!.Self
                && heard[0].Name == "Sender" && heard[0].Clip == TauntId.SpiderMan,
                "Relay stamps the sender identity and echoes the same event once");
            await Task.Delay(250, stop.Token);
            Check(receiver.TakeTaunts().Length == 0 && sender.TakeTaunts().Length == 0,
                "Repeated snapshots do not replay a clip");
            Check(isolated.TakeTaunts().Length == 0, "Private room isolation");
            Check(!sender.SendTaunt((TauntId)999), "Reject a local unknown clip");

            // Every catalogue entry can be sent immediately; there is no time-based cooldown.
            var clips = Enum.GetValues<TauntId>();
            foreach (var clip in clips)
            {
                Check(sender.SendTaunt(clip), "Queue an immediate repeated taunt");
            }

            var burst = await ReceiveAsync(receiver, clips.Length, stop.Token);
            Check(burst.Select(item => item.Clip).SequenceEqual(clips), "Deliver a burst in send order");
            Check(burst.Select(item => item.Sequence).Distinct().Count() == clips.Length, "Burst events are distinct");
            await ReceiveAsync(sender, clips.Length, stop.Token);

            connections.Add(late.Run(endpoint, room, "Late join", InteractionMode.Disabled, stop.Token));
            await UntilAsync(() => late.World?.Players.Length == 3, stop.Token);
            await Task.Delay(150, stop.Token);
            Check(late.TakeTaunts().Length == 0, "Late joins do not hear stored history");

            receiverStop.Cancel();
            try
            {
                await receiving;
            }
            catch (OperationCanceledException)
            {
            }

            connections.Add(receiver.Run(endpoint, room, "Rejoined", InteractionMode.Disabled, stop.Token));
            await UntilAsync(() => receiver.World?.Players.Length == 3, stop.Token);
            await Task.Delay(150, stop.Token);
            Check(receiver.TakeTaunts().Length == 0, "Reconnects do not replay history");
            Check(sender.SendTaunt(TauntId.OhPeter), "Send after reconnect");
            Check((await ReceiveAsync(receiver, 1, stop.Token))[0].Clip == TauntId.OhPeter,
                "Reconnected clients receive new taunts");
            Check((await ReceiveAsync(late, 1, stop.Token))[0].Clip == TauntId.OhPeter,
                "Late clients receive new taunts");
            Check(isolated.TakeTaunts().Length == 0, "Burst stays inside the private room");
            await RejectUnknownAsync(endpoint, frame, stop.Token);
            Console.WriteLine("Taunt sockets: cross-game-room delivery, identity, echo, no cooldown, ordering, deduplication, isolation, late joins, reconnect and invalid-ID rejection passed.");
        }
        finally
        {
            stop.Cancel();
            try
            {
                await Task.WhenAll(connections);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task RejectUnknownAsync(Uri endpoint, GameFrame frame, CancellationToken token)
    {
        var address = new UriBuilder(endpoint)
        {
            Path = NetRules.SocketPath,
            Query = $"room={Guid.NewGuid():N}&name=Invalid&pvp=0"
        }.Uri;
        using var socket = new ClientWebSocket();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        await socket.ConnectAsync(address, deadline.Token);
        await Wire.Send(socket, new ClientUpdate(NetRules.Version, 1, frame, Taunt: (TauntId)999), deadline.Token);
        try
        {
            var buffer = new byte[8192];
            while ((await socket.ReceiveAsync(buffer.AsMemory(), deadline.Token)).MessageType != WebSocketMessageType.Close)
            {
            }
        }
        catch (WebSocketException)
        {
            // The relay aborts invalid peers instead of accepting a supplied audio identifier.
        }
    }

    private static async Task<TauntEvent[]> ReceiveAsync(RelayClient client, int count, CancellationToken token)
    {
        var received = new List<TauntEvent>();
        await UntilAsync(() =>
        {
            received.AddRange(client.TakeTaunts());
            return received.Count >= count;
        }, token);
        Check(received.Count == count, "Receive exactly the requested events");
        return received.ToArray();
    }

    private static async Task UntilAsync(Func<bool> ready, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
