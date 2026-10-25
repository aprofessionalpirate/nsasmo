using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class WebProbe
{
    internal static async Task RunAsync(int processId, bool stress = false)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.Read() ?? throw new IOException("Start the game in Midnight.");
        if (before.Level != 1)
        {
            throw new InvalidOperationException("Run the web driver check in Midnight.");
        }

        if (before.Web is not null || before.WebHeld)
        {
            throw new InvalidOperationException("Release Space and let the native shot finish before running the web driver check.");
        }

        try
        {
            if (stress)
            {
                var storage = game.Inspect();
                var guest = before with { X = (short)Math.Clamp(before.X + 40, 0, 235) };
                for (var cycle = 0; cycle < 30; cycle++)
                {
                    // Repeatedly exercise the real constraint/render/release path at local cadence.
                    var tether = new WebState((short)(guest.X + 7), (short)(guest.Y + 7),
                        (short)(before.X + 7), (short)(before.Y + 7));
                    for (var tick = 0; tick < 6; tick++)
                    {
                        game.Constrain(new PlayerState(Guid.Empty, "Web stress", before with { Web = tether },
                            tick + 1, ConstraintKind.WebPull));
                        var local = game.Read();
                        if (local is null || local.Web is not null)
                        {
                            throw new InvalidOperationException("A tether correction changed the idle native animation.");
                        }

                        game.Render([guest with { Web = tether }], InjectionMode.Experimental);
                        await Task.Delay(NetRules.LocalTickInterval, stop.Token);
                    }

                    game.Constrain(null);
                    game.Render([], InjectionMode.Observe);
                }

                var after = game.Inspect();
                if (storage.RenderHex != after.RenderHex || storage.SlotHash != after.SlotHash
                    || storage.EnemyCount != after.EnemyCount || game.Read()?.Web is not null)
                {
                    throw new InvalidOperationException("Web stress did not restore the renderer, guest storage or native web state.");
                }

                Console.WriteLine("Web stress: 30 pull/render/release cycles passed; renderer, guest storage and web state restored.");
                return;
            }

            // An idle animation must stay native even when the relay requests a tether.
            // Forcing its web bit changes script parsing and can corrupt the next pose.
            var web = new WebState(140, 100, 80, 140);
            game.Constrain(new PlayerState(Guid.Empty, "Web driver", before with { X = 80, Y = 140, Web = web },
                1, ConstraintKind.WebPull));
            var active = game.Read();
            if (active is null || active.Web is not null)
            {
                throw new InvalidOperationException("A relay correction forced an idle animation into a native web shot.");
            }

            game.Constrain(null);
            var released = game.Read();
            if (released is null || released.Web is not null)
            {
                throw new InvalidOperationException("Releasing the relay tether changed the native animation's web state.");
            }

            await Task.Delay(50, stop.Token);
            Console.WriteLine("Native web driver: idle animation ownership and tether release passed.");
        }
        finally
        {
            game.Constrain(new PlayerState(Guid.Empty, "Web driver restore", before, 2, ConstraintKind.Position));
        }
    }
}
