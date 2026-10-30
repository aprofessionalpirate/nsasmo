using System.Net.WebSockets;
using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Tests;

internal static class PuzzlePulseSocketChecks
{
    internal static async Task RunAsync(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var owner = new RelayClient();
        var source = new RelayClient();
        var isolated = new RelayClient();
        var key = Guid.NewGuid().ToString("N");
        var frame = new GameFrame(46, 220, 35, 0, 24);
        var seed = new byte[SharedRules.PuzzleBytes];
        owner.Publish(frame, new SharedReport(seed, [], 0, []));
        source.Publish(frame, new SharedReport(seed, [], 0, []));
        isolated.Publish(frame, new SharedReport(seed, [], 0, []));
        var connections = new List<Task>
        {
            owner.Run(endpoint, key, "Owner", InteractionMode.Disabled, stop.Token, SharedMode.Experimental),
            isolated.Run(endpoint, Guid.NewGuid().ToString("N"), "Isolated", InteractionMode.Disabled, stop.Token, SharedMode.Experimental)
        };
        try
        {
            await UntilAsync(() => owner.World?.Shared?.Room is not null, stop.Token);
            connections.Add(source.Run(endpoint, key, "Button presser", InteractionMode.Disabled, stop.Token, SharedMode.Experimental));
            await UntilAsync(() => source.World?.Shared?.Room is not null && isolated.World?.Shared?.Room is not null, stop.Token);
            var epoch = source.World!.Shared!.Room!.Epoch;
            var press = new SharedReport(seed, [], epoch, [], 2,
                Pulses: [new PuzzlePulse(176, SwitchState.On, 2), new PuzzlePulse(177, SwitchState.On, 2)]);
            source.Publish(frame, press);
            await UntilAsync(() => SharedRules.Get(owner.World!.Shared!.Puzzles, 176) == SwitchState.On
                && SharedRules.Get(owner.World.Shared.Puzzles, 177) == SwitchState.On
                && source.World!.Shared!.Acknowledged >= 2, stop.Token);
            if (SharedRules.Get(isolated.World!.Shared!.Puzzles, 176) != SwitchState.Off)
            {
                throw new IOException("Puzzle input crossed private relay rooms.");
            }

            owner.Publish(frame, new SharedReport(seed, [new PuzzleChange(176, SwitchState.Off), new PuzzleChange(177, SwitchState.Off)], epoch, [], 3));
            await UntilAsync(() => owner.World!.Shared!.Acknowledged >= 3
                && SharedRules.Get(source.World!.Shared!.Puzzles, 176) == SwitchState.Off, stop.Token);
            owner.Publish(frame, new SharedReport(seed, [], epoch, [], 4));
            await Task.Delay(200, stop.Token);
            if (SharedRules.Get(owner.World!.Shared!.Puzzles, 176) != SwitchState.Off
                || SharedRules.Get(owner.World.Shared.Puzzles, 177) != SwitchState.Off)
            {
                throw new IOException("Retried self-reset input replayed after consumption.");
            }

            using var invalid = new ClientWebSocket();
            await invalid.ConnectAsync(new UriBuilder(endpoint)
            {
                Path = NetRules.SocketPath,
                Query = $"room={Guid.NewGuid():N}&name=Invalid&shared=1&pvp=0"
            }.Uri, stop.Token);
            await Wire.Send(invalid, new ClientUpdate(NetRules.Version, 1, frame,
                Shared: press with { Pulses = [new PuzzlePulse(SharedRules.MaxSwitches, SwitchState.On, 2)] }), stop.Token);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(3));
                while (await Wire.Receive<WorldState>(invalid, timeout.Token) is not null)
                {
                }
            }
            catch (WebSocketException)
            {
            }
            Console.WriteLine("Puzzle pulse sockets: two simultaneous inputs, acknowledgement, retry deduplication, private-room isolation and invalid-input rejection passed.");
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

    private static async Task UntilAsync(Func<bool> ready, CancellationToken token)
    {
        while (!ready())
        {
            await Task.Delay(10, token);
        }
    }
}
