using SpideyNet.Client;
using SpideyNet.Core;
using SpideyNet.Game;
using SpideyNet.Transport;

namespace SpideyNet.Probe;

internal static class PlayerToolsProbe
{
    internal static async Task PreviewAsync(Uri endpoint, string room, int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.Read() ?? throw new IOException("Start a VGA game first.");
        var destination = before with { Level = 3, X = 90, Y = 147 };
        var session = new Session();
        try
        {
            while (!game.TryTeleport(destination))
            {
                await Task.Delay(NetRules.LocalTickInterval, stop.Token);
            }

            session.Connect(processId, endpoint, room, "Lift explorer", InjectionMode.Experimental,
                InteractionMode.Experimental, SharedMode.Experimental);
            Console.WriteLine("Desktop peer ready in Lift for three minutes.");
            var tick = 0;
            while (!stop.IsCancellationRequested)
            {
                game.Constrain(new PlayerState(Guid.Empty, "Preview movement",
                    destination with { X = (short)(90 + tick++ % 30) }, tick, ConstraintKind.Position));
                await Task.Delay(100, stop.Token);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        finally
        {
            await session.DisconnectAsync();
            using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!game.TryTeleport(before))
            {
                await Task.Delay(NetRules.LocalTickInterval, restore.Token);
            }
        }
    }

    internal static async Task RunAsync(Uri endpoint, int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.Read() ?? throw new IOException("Start a VGA game first.");
        var room = Guid.NewGuid().ToString("N");
        var session = new Session();
        var target = new RelayClient();
        target.Publish(before with { Level = 17, X = 90, Y = 147 });
        var connection = target.Run(endpoint, room, "Travel target", InteractionMode.Disabled, stop.Token, SharedMode.Experimental);
        session.Connect(processId, endpoint, room, "Native player", InjectionMode.Experimental,
            InteractionMode.Disabled, SharedMode.Experimental);
        try
        {
            await UntilAsync(() => session.View.Players.Length == 1 && target.World?.Players.Length == 2, stop.Token);
            var self = target.World!.Players.Single(player => player.Name == "Native player").Id;
            target.Watch(self);
            await UntilAsync(() => target.Screen is not null, stop.Token);
            var serial = target.Screen!.Frame.Serial;
            await UntilAsync(() => target.Screen?.Frame.Serial > serial, stop.Token);
            Console.WriteLine("Native spectator: successive real DOSBox images reached a watcher in another room.");

            foreach (var level in new byte[] { 17, 3, 25, 1 })
            {
                target.Publish(before with { Level = level, X = 90, Y = 147 });
                await UntilAsync(() => session.View.Players.Any(player => player.Frame.Level == level), stop.Token);
                await session.TeleportAsync(target.World!.Self).WaitAsync(stop.Token);
                await UntilAsync(() => session.View.Local?.Level == level, stop.Token);
                if (session.View.Status.StartsWith("Stopped", StringComparison.Ordinal)
                    || Math.Abs(session.View.Local!.X - 90) > 8)
                {
                    throw new InvalidOperationException($"The session did not reach its peer: {session.View}.");
                }

                Console.WriteLine($"Session teleport: {RoomNames.Get(level)} at X={session.View.Local.X}, Y={session.View.Local.Y}.");
            }

            // A same-room move uses the same selected-player action without a loader request.
            target.Publish(before with { X = 150, Y = 147 });
            await UntilAsync(() => session.View.Players.Any(player => player.Frame.X == 150), stop.Token);
            await session.TeleportAsync(target.World!.Self).WaitAsync(stop.Token);
            await UntilAsync(() => Math.Abs((session.View.Local?.X ?? 0) - 150) <= 8, stop.Token);
            target.Watch(null);
            await Task.Delay(350, stop.Token);
            if (target.Screen is not null)
            {
                throw new InvalidOperationException("Stopping the watch retained an image.");
            }

            target.Publish(before with { Level = 17, X = 90, Y = 147 });
            await UntilAsync(() => session.View.Players.Any(player => player.Frame.Level == 17), stop.Token);
            var pendingTravel = session.TeleportAsync(target.World!.Self);
            await session.DisconnectAsync();
            await pendingTravel;
            await UntilAsync(() => game.Read()?.Level == 17, stop.Token);

            Console.WriteLine("Player tools: native streamed previews, session room travel, same-room placement, stop watching and disconnect during travel passed.");
        }
        finally
        {
            await session.DisconnectAsync();
            stop.Cancel();
            try
            {
                await connection;
            }
            catch (OperationCanceledException)
            {
            }

            game.CancelTeleport();
            using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!game.TryTeleport(before))
            {
                await Task.Delay(NetRules.LocalTickInterval, restore.Token);
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
