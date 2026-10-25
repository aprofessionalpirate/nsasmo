using SpideyNet.Core;
using SpideyNet.Game;

internal static class VideoProbe
{
    private const int DisplaySeconds = 20;
    private const short GuestX = 96;

    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(DisplaySeconds * 3));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.InspectScene() ?? throw new InvalidOperationException("Start the game.");
        var scene = before.Scene!;
        var original = scene.Movers.FirstOrDefault() ?? throw new InvalidOperationException("Choose a room with moving scenery.");
        if (original.Motion != 0) { throw new InvalidOperationException("Choose stationary scenery."); }
        var step = original.Top + original.Y == 0 ? 1 : -1;
        var moved = original with { Y = checked((sbyte)(original.Y + step)) };
        var renderer = game.Inspect();

        try
        {
            game.Render([scene.Frame with { X = GuestX, Web = new WebState(160, 80, GuestX, 147) }], InjectionMode.Experimental);
            Apply(game, scene, moved);
            var changed = game.InspectScene()!;
            if (changed.TileHash == before.TileHash || changed.BackgroundHash == before.BackgroundHash)
            {
                throw new InvalidOperationException("Moving scenery did not repaint graphics and collision tiles.");
            }
            Console.WriteLine($"{changed.Video}: guest and remote web rendering active; scenery and background changed.");
            await Task.Delay(TimeSpan.FromSeconds(DisplaySeconds), stop.Token);
        }
        finally
        {
            Apply(game, scene, original);
            game.Render([], InjectionMode.Observe);
        }

        var restored = game.InspectScene()!;
        var after = game.Inspect();
        if (before.TileHash != restored.TileHash || before.BackgroundHash != restored.BackgroundHash
            || renderer.RenderHex != after.RenderHex || renderer.SlotHash != after.SlotHash
            || renderer.EnemyCount != after.EnemyCount)
        {
            throw new InvalidOperationException("Graphics or renderer restoration failed.");
        }
        Console.WriteLine($"{restored.Video}: collision tiles, background, actor storage and renderer restored.");
    }

    private static void Apply(GameBridge game, GameScene scene, MoverState mover)
    {
        var local = game.ReadScene() ?? throw new IOException("Game left the test room.");
        if (local.Frame.Level != scene.Frame.Level) { throw new IOException("Game changed rooms during graphics test."); }
        var targets = local.Movers.Select(value => value.Mover == mover.Mover ? mover : value).ToArray();
        game.SyncScene(new SharedState(SharedRules.Normalize(local.Puzzles),
            new RoomState(local.Frame.Level, Guid.Empty, 1, local.Enemies, targets)));
    }
}
