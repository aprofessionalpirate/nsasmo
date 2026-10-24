using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class SuitProbe
{
    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var game = new GameBridge(processId, stop.Token);
        _ = game.Read() ?? throw new IOException("Start a VGA game first.");

        // Hold native simulation still to compare all actor bytes, scenery and code exactly.
        using (var pause = game.BeginFrameTransaction())
        {
            var before = game.Inspect();
            var background = game.InspectScene()!.BackgroundHash;
            foreach (var colour in Enum.GetValues<SuitColour>())
            {
                game.Render([], InjectionMode.Experimental, true, localColour: colour);
                var after = game.Inspect();
                var expectedCode = colour == SuitColour.Red ? before.RenderHex
                    : Convert.ToHexString(RenderPatch.Build(EnemyPlan.RenderCount - 1));
                if (after.ActorHex != before.ActorHex || after.EnemyCount != before.EnemyCount
                    || after.RenderHex != expectedCode)
                {
                    throw new IOException($"Local {colour} changed simulation state or used the wrong draw range.");
                }
                if (colour != SuitColour.Red && game.InspectScene()!.BackgroundHash == background)
                {
                    throw new IOException($"Local {colour} did not paint a suit.");
                }

                // Turning colours off and Observe must both remove local colour without guests.
                game.Render([], InjectionMode.Experimental, false);
                game.Render([], InjectionMode.Observe);
                after = game.Inspect();
                if (after.ActorHex != before.ActorHex || after.SlotHash != before.SlotHash
                    || after.RenderHex != before.RenderHex || game.InspectScene()!.BackgroundHash != background)
                {
                    throw new IOException($"Local {colour} did not restore native state exactly.");
                }
            }
        }
        Console.WriteLine("Eight local suits: no actor/health/physics changes; exact pixels, slots and draw-code restoration passed.");

        try
        {
            // Leave enough time per suit to inspect native movement and animation visually.
            foreach (var colour in Enum.GetValues<SuitColour>())
            {
                Console.WriteLine($"Displaying local {colour}.");
                var started = Environment.TickCount64;
                using var timer = new PeriodicTimer(NetRules.LocalTickInterval);
                while (Environment.TickCount64 - started < 4000 && await timer.WaitForNextTickAsync(stop.Token))
                {
                    game.Render([], InjectionMode.Experimental, true, localColour: colour);
                }
            }
        }
        finally
        {
            game.Render([], InjectionMode.Observe);
        }
    }
}
