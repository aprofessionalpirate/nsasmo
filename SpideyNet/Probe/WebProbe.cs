using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class WebProbe
{
    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.Read() ?? throw new IOException("Start the game in Midnight.");
        if (before.Level != 1)
        {
            throw new InvalidOperationException("Run the web driver check in Midnight.");
        }

        try
        {
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
