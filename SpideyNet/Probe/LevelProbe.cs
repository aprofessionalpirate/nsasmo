using SpideyNet.Client;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class LevelProbe
{
    internal static async Task JoinAsync(Uri endpoint, string room, int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.Read() ?? throw new IOException("Start the preview game.");
        var session = new Session();
        try
        {
            // A short-lived real peer lets the desktop roster and in-game guest be inspected.
            game.Constrain(new PlayerState(Guid.Empty, "Preview position", before with { X = 96, Y = 80 },
                1, ConstraintKind.Position));
            session.Connect(processId, endpoint, room, "Level preview", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Experimental);
            await UntilAsync(() => session.View.Players.Length > 0, stop.Token);
            Console.WriteLine("Desktop preview peer joined for thirty seconds.");
            await Task.Delay(TimeSpan.FromSeconds(30), stop.Token);
            if (session.View.Status.StartsWith("Stopped:", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(session.View.Status);
            }
        }
        finally
        {
            await session.DisconnectAsync();
            game.Constrain(new PlayerState(Guid.Empty, "Preview restore", before, 2, ConstraintKind.Position));
        }
    }

    internal static async Task RunAsync(Uri endpoint, int firstId, int secondId)
    {
        if (firstId == secondId)
        {
            throw new ArgumentException("Choose two distinct test-game processes.");
        }

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var firstGame = new GameBridge(firstId, stop.Token);
        using var secondGame = new GameBridge(secondId, stop.Token);
        var initial = firstGame.ReadScene() ?? throw new IOException("Start the first test game.");
        var secondInitial = secondGame.ReadScene() ?? throw new IOException("Start the second test game.");
        if (initial.Frame.Level != secondInitial.Frame.Level)
        {
            throw new InvalidOperationException("Both test games must start in the same room.");
        }

        var firstStorage = firstGame.Inspect();
        var secondStorage = secondGame.Inspect();
        var first = new Session();
        var second = new Session();
        try
        {
            // Observe must populate the roster without touching actor or renderer storage.
            Connect(first, second, firstId, secondId, endpoint, InjectionMode.Observe);
            Console.WriteLine($"Observe: room {initial.Frame.Level} ({firstGame.Title}).");
            await UntilAsync(() => first.View.Players.Length == 1 && second.View.Players.Length == 1, stop.Token);
            CheckStorage(firstStorage, firstGame.Inspect());
            CheckStorage(secondStorage, secondGame.Inspect());
            await Task.WhenAll(first.DisconnectAsync(), second.DisconnectAsync());
            Console.WriteLine("Observe and restoration passed; starting shared rendering.");

            Connect(first, second, firstId, secondId, endpoint, InjectionMode.Experimental);
            await UntilAsync(() => first.View.Players.Length == 1 && second.View.Players.Length == 1, stop.Token);
            await UntilAsync(() => firstGame.Inspect().RenderHex != firstStorage.RenderHex
                && secondGame.Inspect().RenderHex != secondStorage.RenderHex, stop.Token);
            await Task.Delay(1000, stop.Token);
            CheckActive(first, second);
            Console.WriteLine("Both shared renderers are active.");

            // A hit reported by either game must reach the other peer's native actor.
            var scene = firstGame.ReadScene() ?? throw new IOException("The first game left its room.");
            var enemy = scene.Enemies.FirstOrDefault(value => value.Health > 32);
            if (enemy is not null)
            {
                var health = (byte)(enemy.Health - 16);
                Console.WriteLine($"Applying enemy {enemy.Mover} health {enemy.Health} -> {health}; {scene.Movers.Length} scenery objects.");
                firstGame.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
                    new RoomState(scene.Frame.Level, Guid.Empty, 1,
                        scene.Enemies.Select(value => value.Mover == enemy.Mover ? value with { Health = health } : value).ToArray(), scene.Movers)));
                try
                {
                    await UntilAsync(() => secondGame.ReadScene()?.Enemies.Any(value => value.Mover == enemy.Mover
                        && value.Health <= health) == true, stop.Token);
                }
                catch (OperationCanceledException)
                {
                    CheckActive(first, second);
                    throw new IOException($"Enemy {enemy.Mover} damage did not replicate: first health "
                        + $"{firstGame.ReadScene()?.Enemies.FirstOrDefault(value => value.Mover == enemy.Mover)?.Health}, second health "
                        + $"{secondGame.ReadScene()?.Enemies.FirstOrDefault(value => value.Mover == enemy.Mover)?.Health}.");
                }
                Console.WriteLine($"Native enemy {enemy.Mover}: damage synchronized.");
            }

            // Sample after scripts settle; both native collision grids must eventually agree.
            await UntilAsync(() => firstGame.InspectScene()?.CollisionHash == secondGame.InspectScene()?.CollisionHash, stop.Token);
            var after = firstGame.ReadScene() ?? throw new IOException("The first game left its room.");
            if (after.Frame.Level != initial.Frame.Level || secondGame.Read()?.Level != initial.Frame.Level)
            {
                throw new InvalidOperationException("A test game left the requested room.");
            }

            CheckActive(first, second);
            Console.WriteLine($"Room {initial.Frame.Level} ({firstGame.Title}): Observe, two guest renderers, shared scene and collision grids passed; "
                + $"{after.Enemies.Length} enemies, {after.Movers.Length} scenery objects.");
        }
        finally
        {
            await Task.WhenAll(first.DisconnectAsync(), second.DisconnectAsync());
        }

        CheckStorage(firstStorage, firstGame.Inspect());
        CheckStorage(secondStorage, secondGame.Inspect());
        Console.WriteLine("Both test games restored renderer instructions, reserved storage and native counts.");
    }

    private static void Connect(Session first, Session second, int firstId, int secondId, Uri endpoint, InjectionMode mode)
    {
        var key = Guid.NewGuid().ToString("N");
        var shared = mode == InjectionMode.Experimental ? SharedMode.Experimental : SharedMode.Independent;
        first.Connect(firstId, endpoint, key, "Level first", mode, InteractionMode.Disabled, shared);
        second.Connect(secondId, endpoint, key, "Level second", mode, InteractionMode.Disabled, shared);
    }

    private static void CheckActive(Session first, Session second)
    {
        if (first.View.Status.StartsWith("Stopped:", StringComparison.Ordinal)
            || second.View.Status.StartsWith("Stopped:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Session stopped: {first.View.Status}; {second.View.Status}");
        }
    }

    private static void CheckStorage(GameStatus before, GameStatus after)
    {
        if (before.RenderHex != after.RenderHex || before.SlotHash != after.SlotHash || before.EnemyCount != after.EnemyCount)
        {
            throw new InvalidOperationException("Native renderer, guest storage or actor count was not restored.");
        }
    }

    private static async Task UntilAsync(Func<bool> condition, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
