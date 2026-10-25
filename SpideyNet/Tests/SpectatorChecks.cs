using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Tests;

internal static class SpectatorChecks
{
    internal static async Task RunAsync(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var room = Guid.NewGuid().ToString("N");
        var source = new RelayClient();
        var watcher = new RelayClient();
        var bystander = new RelayClient();
        var isolated = new RelayClient();
        var frame = new GameFrame(1, 80, 147, 0, 24);
        source.Publish(frame);
        watcher.Publish(frame with { Level = 17 });
        bystander.Publish(frame with { Level = 25 });
        isolated.Publish(frame);
        var connections = new[]
        {
            source.Run(endpoint, room, "Source", InteractionMode.Disabled, stop.Token),
            watcher.Run(endpoint, room, "Watcher", InteractionMode.Disabled, stop.Token),
            bystander.Run(endpoint, room, "Bystander", InteractionMode.Disabled, stop.Token),
            isolated.Run(endpoint, Guid.NewGuid().ToString("N"), "Isolated", InteractionMode.Disabled, stop.Token)
        };
        try
        {
            await UntilAsync(() => source.World?.Players.Length == 3 && watcher.World?.Players.Length == 3, stop.Token);
            var id = source.World!.Self;
            watcher.Watch(id);
            await UntilAsync(() => source.World?.ScreenRequested == true, stop.Token);

            // A large frame exercises pooled, fragmented transport while movement continues.
            var payload = new byte[80000];
            Random.Shared.NextBytes(payload);
            source.PublishScreen(new ScreenFrame(1, payload));
            await UntilAsync(() => watcher.Screen?.Frame.Serial == 1, stop.Token);
            if (!watcher.Screen!.Frame.Data.SequenceEqual(payload) || bystander.Screen is not null
                || watcher.World!.Players.Single(player => player.Id == watcher.World.Self).Frame.Level != 17)
            {
                throw new InvalidOperationException("Spying changed the viewer's room or sent images to an unrelated player.");
            }

            var tick = watcher.World.Tick;
            source.Publish(frame with { Level = 3, X = 120 });
            await UntilAsync(() => watcher.World?.Players.Any(player => player.Id == id
                && player.Frame.Level == 3 && player.Frame.X == 120) == true, stop.Token);
            if (watcher.World!.Tick <= tick)
            {
                throw new InvalidOperationException("Screen transfer stalled movement updates.");
            }

            await UntilAsync(() => watcher.Screen is null, stop.Token);
            watcher.Watch(bystander.World!.Self);
            await UntilAsync(() => source.World?.ScreenRequested == false && bystander.World?.ScreenRequested == true, stop.Token);
            bystander.PublishScreen(new ScreenFrame(2, [1, 2, 3]));
            await UntilAsync(() => watcher.Screen?.Player == bystander.World!.Self, stop.Token);
            watcher.Watch(null);
            await UntilAsync(() => bystander.World?.ScreenRequested == false, stop.Token);
            isolated.Watch(id);
            await Task.Delay(150, stop.Token);
            if (source.World?.ScreenRequested != false || isolated.Screen is not null || watcher.Screen is not null)
            {
                throw new InvalidOperationException("Screen subscription crossed rooms or survived Stop spying.");
            }

            Console.WriteLine("Spectator sockets: on-demand capture, cross-level preview, large frames, movement, stale image expiry, retarget, stop and room isolation passed.");
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
