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

            // A relay tether must use the same native geometry that ordinary Space shots publish.
            var web = new WebState(140, 100, 80, 140);
            game.Constrain(new PlayerState(Guid.Empty, "Web driver", before with { X = 80, Y = 140, Web = web },
                1, ConstraintKind.WebPull));
            var active = game.Read();
            if (active?.Web != web)
            {
                throw new InvalidOperationException("The native web coordinates did not round-trip through the driver.");
            }

            game.Constrain(null);
            if (game.Read()?.Web is not null)
            {
                throw new InvalidOperationException("Releasing the relay tether left the native web active.");
            }

            await Task.Delay(50, stop.Token);
            Console.WriteLine("Native web driver: coordinates round-trip and tether release passed.");
        }
        finally
        {
            game.Constrain(new PlayerState(Guid.Empty, "Web driver restore", before, 2, ConstraintKind.Position));
        }
    }
}
