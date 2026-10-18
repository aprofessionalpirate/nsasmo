using System.Buffers.Binary;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class InactiveProbe
{
    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using (var game = new GameBridge(processId, stop.Token))
        {
            var scene = game.ReadScene() ?? throw new IOException("Start the game.");
            if (!scene.Enemies.Any(enemy => enemy.Health == 0))
            {
                throw new InvalidOperationException("Choose a room with an already defeated enemy.");
            }

            // Apply existing progress once, then release every patch and let DOS run alone.
            game.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
                new RoomState(scene.Frame.Level, Guid.Empty, 1, scene.Enemies, scene.Movers)));
        }

        using var observer = new GameBridge(processId, stop.Token);
        for (var tick = 0; tick < 10; tick++)
        {
            await Task.Delay(500, stop.Token);
            var status = observer.Inspect();
            var actors = Convert.FromHexString(status.TableHex);
            for (var slot = 0; slot < status.EnemyCount - 1; slot++)
            {
                var actor = actors.AsSpan(slot * GameLayout.RecordSize, GameLayout.RecordSize).ToArray();
                if (actor[GameLayout.HealthOffset] != 0)
                {
                    continue;
                }

                if (BinaryPrimitives.ReadInt16LittleEndian(actor.AsSpan(GameLayout.XOffset)) != 0
                    || BinaryPrimitives.ReadInt16LittleEndian(actor.AsSpan(GameLayout.BoundsLeftOffset)) != 0
                    || actor[GameLayout.YOffset] != 200)
                {
                    throw new InvalidOperationException($"Inactive actor moved after disconnect: {Convert.ToHexString(actor)}");
                }
            }
        }

        Console.WriteLine("Inactive native actors stayed at safe coordinates for five seconds after all patches were released.");
    }
}
