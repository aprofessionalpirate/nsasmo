using SpideyNet.Client;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class TrioProbe
{
    internal static async Task RunAsync(Uri endpoint, int[] ids, bool transitions, bool preview = false)
    {
        if (ids.Length != 3 || ids.Distinct().Count() != 3)
        {
            throw new ArgumentException("Choose three distinct disposable DOSBox processes.");
        }

        using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var games = new List<GameBridge>();
        var sessions = Enumerable.Range(0, 3).Select(_ => new Session()).ToArray();
        var key = Guid.NewGuid().ToString("N");
        try
        {
            foreach (var id in ids)
            {
                games.Add(new GameBridge(id, stop.Token));
            }

            if (transitions)
            {
                foreach (var game in games.Where(game => game.Read()?.Level == 2))
                {
                    Position(game, -20);
                    await UntilAsync(() => game.Read()?.Level == 1, sessions, stop.Token);
                }
            }

            var initial = games.Select(game => game.Inspect()).ToArray();
            var level = initial[0].Frame?.Level ?? throw new IOException("Start the first game.");
            if (initial.Any(status => status.Frame?.Level != level))
            {
                throw new InvalidOperationException("All three test games must start in the same room.");
            }

            Console.WriteLine($"Three-player room {level}: {games[0].Title}.");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(games[0].InspectScene()));
            Connect(sessions, ids, endpoint, key, InjectionMode.Observe);
            await UntilAsync(() => sessions.All(session => session.View.Players.Length == 2), sessions, stop.Token);
            for (var index = 0; index < 3; index++)
            {
                CheckStorage(initial[index], games[index].Inspect());
            }

            await DisconnectAsync(sessions);
            Console.WriteLine("Observe: two remote players per roster; native storage unchanged.");
            key = Guid.NewGuid().ToString("N");
            Connect(sessions, ids, endpoint, key, InjectionMode.Experimental);
            await UntilAsync(() => sessions.All(session => session.View.Players.Length == 2)
                && games.All(game => game.Inspect().RenderHex == Convert.ToHexString(RenderPatch.Build(EnemyPlan.RenderCount))),
                sessions, stop.Token);
            await UntilAsync(() => games.All(game => VisibleGuests(game) == 2), sessions, stop.Token);
            await Task.Delay(600, stop.Token);
            await SharedAsync(games, sessions, stop.Token);
            if (preview)
            {
                await PreviewAsync(games, sessions, stop.Token);
            }

            if (level == 1)
            {
                await NativeContactAsync(games, sessions, stop.Token);
                await InteractionsAsync(games, sessions, stop.Token);
                await DeathAsync(games, sessions, stop.Token);
                if (transitions)
                {
                    await TransitionsAsync(games, sessions, stop.Token);
                }
            }

            // Connection completion order can differ from launch order; remove the actual owner.
            var departing = Array.FindIndex(sessions, session => session.View.Shared == "Enemy owner: you");
            if (departing < 0)
            {
                throw new IOException("The test room has no enemy owner.");
            }

            var survivors = Enumerable.Range(0, 3).Where(index => index != departing).ToArray();
            await sessions[departing].DisconnectAsync();
            await UntilAsync(() => survivors.All(index => sessions[index].View.Players.Length == 1)
                && survivors.Count(index => sessions[index].View.Shared == "Enemy owner: you") == 1, sessions, stop.Token);
            await Task.Delay(600, stop.Token);
            CheckActive(sessions);
            Console.WriteLine("Owner departure: two surviving peers remain active.");
            sessions[departing].Connect(ids[departing], endpoint, key, $"Trio {departing + 1} rejoined", InjectionMode.Experimental,
                InteractionMode.Experimental, SharedMode.Experimental);
            await UntilAsync(() => sessions.All(session => session.View.Players.Length == 2), sessions, stop.Token);
            await UntilAsync(() => games.All(game => VisibleGuests(game) == 2), sessions, stop.Token);
            await Task.Delay(600, stop.Token);
            CheckActive(sessions);
            Console.WriteLine("Late rejoin: all three rosters restored.");
            await DisconnectAsync(sessions);
            if (!transitions)
            {
                for (var index = 0; index < 3; index++)
                {
                    CheckStorage(initial[index], games[index].Inspect());
                }
            }
            else if (games.Any(game => game.Inspect().RenderHex != initial[0].RenderHex))
            {
                throw new IOException("A transition left a renderer patched after disconnect.");
            }

            Console.WriteLine(transitions ? "Three-player transition cleanup: all native renderers restored."
                : "Three-player disconnect: native renderer and owned actor storage restored.");
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Trio test failed: {string.Join("; ", sessions.Select(session => session.View.Status))}", error);
        }
        finally
        {
            await DisconnectAsync(sessions);
            foreach (var game in games)
            {
                game.Dispose();
            }
        }
    }

    private static async Task SharedAsync(List<GameBridge> games, Session[] sessions, CancellationToken token)
    {
        await UntilAsync(() => sessions.Count(session => session.View.Shared == "Enemy owner: you") == 1, sessions, token);
        var owner = Array.FindIndex(sessions, session => session.View.Shared == "Enemy owner: you");
        var scene = games[owner].ReadScene() ?? throw new IOException("The owner room is unavailable.");
        var enemy = scene.Enemies.FirstOrDefault(value => value.Health > 32);
        if (enemy is not null)
        {
            // Damage from a follower must reach both the owner and the other follower.
            var health = (byte)(enemy.Health - 16);
            var follower = (owner + 2) % 3;
            var third = games[follower].ReadScene()!;
            games[follower].SyncScene(new SharedState(SharedRules.Normalize(third.Puzzles),
                new RoomState(third.Frame.Level, Guid.Empty, 1,
                    third.Enemies.Select(value => value.Mover == enemy.Mover ? value with { Health = health } : value).ToArray(), third.Movers)));
            await UntilAsync(() => games.All(game => game.ReadScene()?.Enemies.Any(value => value.Mover == enemy.Mover
                && value.Health <= health) == true), sessions, token);
            Console.WriteLine($"Enemy {enemy.Mover}: follower damage reached all games.");
        }

        var contact = games[0].InspectScene()!.Triggers.FirstOrDefault();
        if (contact is not null)
        {
            // Change the owner's first switch; contact-driven one-shot scripts are checked separately.
            var original = SharedRules.Get(scene.Puzzles, contact.Index);
            var changed = original == SwitchState.On ? SwitchState.Off : SwitchState.On;
            SetPuzzle(games[owner], contact.Index, changed);
            await UntilAsync(() => games.All(game => game.ReadScene() is { } current
                && SharedRules.Get(current.Puzzles, contact.Index) == changed), sessions, token);
            await Task.Delay(200, token);
            SetPuzzle(games[owner], contact.Index, original);
            await UntilAsync(() => games.All(game => game.ReadScene() is { } current
                && SharedRules.Get(current.Puzzles, contact.Index) == original), sessions, token);
            Console.WriteLine($"Puzzle {contact.Index}: reversible changes reached all three games.");
        }

        var mover = games[owner].ReadScene()!.Movers.FirstOrDefault();
        if (mover is not null)
        {
            var delta = mover.Top + mover.Y > 0 ? -1 : 1;
            var target = mover with { Y = checked((sbyte)(mover.Y + delta)), Motion = 0 };
            var current = games[owner].ReadScene()!;
            games[owner].SyncScene(new SharedState(SharedRules.Normalize(current.Puzzles),
                new RoomState(current.Frame.Level, Guid.Empty, 1, current.Enemies,
                    current.Movers.Select(value => value.Mover == mover.Mover ? target : value).ToArray())));
            await UntilAsync(() => games.All(game => game.ReadScene()?.Movers.Any(value => value.Mover == mover.Mover
                && value.Y == target.Y) == true), sessions, token);
            await UntilAsync(() => games.Select(game => game.InspectScene()?.CollisionHash).Distinct().Count() == 1,
                sessions, token);
            Console.WriteLine($"Scenery {mover.Mover}: displacement and collision flags agree in all games.");

            current = games[owner].ReadScene()!;
            games[owner].SyncScene(new SharedState(SharedRules.Normalize(current.Puzzles),
                new RoomState(current.Frame.Level, Guid.Empty, 1, current.Enemies,
                    current.Movers.Select(value => value.Mover == mover.Mover ? mover with { Motion = 0 } : value).ToArray())));
            await UntilAsync(() => games.All(game => game.ReadScene()?.Movers.Any(value => value.Mover == mover.Mover
                && value.Y == mover.Y) == true), sessions, token);
            if (scene.Frame.Level == 3)
            {
                await LiftRiderAsync(games, sessions, owner, mover, token);
            }
        }
    }

    private static async Task LiftRiderAsync(List<GameBridge> games, Session[] sessions, int owner, MoverState mover,
        CancellationToken token)
    {
        // Put the lift midway down its native shaft, then let a follower become its rider.
        var parked = mover with { Y = 10, Motion = 0 };
        MoveScenery(games[owner], parked);
        await UntilAsync(() => games.All(game => game.ReadScene()?.Movers.Any(value => value.Mover == mover.Mover
            && value.Y == parked.Y) == true), sessions, token);
        var rider = (owner + 1) % 3;
        var x = (short)((parked.Left + parked.X) * 8 + 4);
        var y = (byte)((parked.Top + parked.Y) * 8 - 20);
        Position(games[rider], x, y);
        await UntilAsync(() => games[rider].Read() is { } frame && MoverRules.Riding(frame, parked), sessions, token);
        await Task.Delay(300, token);
        var before = games[rider].Read()!;
        MoveScenery(games[rider], parked with { Y = 11 });
        await UntilAsync(() => games.All(game => game.ReadScene()?.Movers.Any(value => value.Mover == mover.Mover
            && value.Y == 11) == true), sessions, token);
        await UntilAsync(() => games[rider].Read() is { } frame && frame.Y >= before.Y + 6
            && MoverRules.Riding(frame, parked with { Y = 11 }), sessions, token);
        await UntilAsync(() => games.Select(game => game.InspectScene()?.CollisionHash).Distinct().Count() == 1, sessions, token);
        Position(games[rider], 80);
        Console.WriteLine("Lift: follower rider drove platform movement, was carried, and all three collision grids agree.");
    }

    private static void MoveScenery(GameBridge game, MoverState target)
    {
        var scene = game.ReadScene() ?? throw new IOException("The scenery room is unavailable.");
        game.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
            new RoomState(scene.Frame.Level, Guid.Empty, 1, scene.Enemies,
                scene.Movers.Select(value => value.Mover == target.Mover ? target : value).ToArray())));
    }

    private static async Task PreviewAsync(List<GameBridge> games, Session[] sessions, CancellationToken token)
    {
        for (var index = 0; index < 3; index++)
        {
            Position(games[index], (short)(11 + index * 40));
        }

        Console.WriteLine("Live keyboard preview ready for 75 seconds: V and Space in the first game.");
        var end = Environment.TickCount64 + 75000;
        var web = false;
        var pushed = false;
        while (Environment.TickCount64 < end)
        {
            CheckActive(sessions);
            if (!web && sessions.Any(session => session.View.Players.Any(player => player.Frame.Web is not null)))
            {
                web = true;
                Console.WriteLine("Live input: native Space web reached a remote production session.");
            }

            if (!pushed && sessions.Any(session => session.View.Players.Any(player => player.Constraint == ConstraintKind.Pushed)))
            {
                pushed = true;
                Console.WriteLine("Live input: V froze a remote player.");
            }

            await Task.Delay(10, token);
        }

        Console.WriteLine($"Live keyboard result: native web={web}, push={pushed}.");
    }

    private static async Task InteractionsAsync(List<GameBridge> games, Session[] sessions, CancellationToken token)
    {
        Position(games[0], 11);
        Position(games[1], 51);
        Position(games[2], 91);
        await UntilAsync(() => sessions[0].View.Players.Any(player => player.Name == "Trio 2" && Math.Abs(player.Frame.X - 51) < 3)
            && sessions[0].View.Players.Any(player => player.Name == "Trio 3" && Math.Abs(player.Frame.X - 91) < 3), sessions, token);
        sessions[0].PushPlayer();
        await UntilAsync(() => sessions[0].View.Players.Any(player => player.Name == "Trio 2" && player.Constraint == ConstraintKind.Pushed), sessions, token);
        if (sessions[0].View.Players.Single(player => player.Name == "Trio 3").Constraint != ConstraintKind.None)
        {
            throw new IOException("The farther player was pushed instead of the nearest one.");
        }

        Position(games[1], 65);
        await UntilAsync(() => games[1].Read() is { } frame && frame.X > 75, sessions, token);
        await UntilAsync(() => sessions[0].View.Players.All(player => player.Constraint == ConstraintKind.None), sessions, token);
        Position(games[1], 100);
        await UntilAsync(() => sessions[0].View.Players.Any(player => player.Name == "Trio 2" && Math.Abs(player.Frame.X - 100) < 3), sessions, token);
        Position(games[2], 67);
        await UntilAsync(() => sessions[0].View.Players.Any(player => player.Name == "Trio 3" && Math.Abs(player.Frame.X - 67) < 3), sessions, token);
        Console.WriteLine("Interactions: nearest target pushed, distant peer unaffected, expiry and player crossing passed.");
    }

    private static async Task TransitionsAsync(List<GameBridge> games, Session[] sessions, CancellationToken token)
    {
        for (var repeat = 0; repeat < 3; repeat++)
        {
            // Alternate two peers through the native exit while the third preserves Midnight's state.
            foreach (var index in new[] { 0, 2 })
            {
                Position(games[index], 260);
                await UntilAsync(() => games[index].Read()?.Level == 2, sessions, token);
                await UntilAsync(() => games.Select((game, other) => VisibleGuests(game) == (index == other ? 0 : 1)).All(value => value),
                    sessions, token);
                Position(games[index], -20);
                await UntilAsync(() => games[index].Read()?.Level == 1, sessions, token);
                await UntilAsync(() => sessions.All(session => session.View.Local?.Level == 1)
                    && sessions.All(session => session.View.Players.Length == 2), sessions, token);
                await Task.Delay(250, token);
                await UntilAsync(() => games.All(game => VisibleGuests(game) == 2), sessions, token);
                CheckActive(sessions);
            }

            Console.WriteLine($"Transition round {repeat + 1}: two peers left and reentered; third remained connected.");
        }
    }

    private static async Task NativeContactAsync(List<GameBridge> games, Session[] sessions, CancellationToken token)
    {
        var owner = Array.FindIndex(sessions, session => session.View.Shared == "Enemy owner: you");
        SetPuzzle(games[owner], 4, SwitchState.Off);
        SetPuzzle(games[owner], 5, SwitchState.Off);
        await UntilAsync(() => games.All(game => SharedRules.Get(game.ReadScene()!.Puzzles, 4) == SwitchState.Off), sessions, token);

        // A follower lands on Midnight's helicopter, using the game's real contact switch.
        var rider = (owner + 2) % 3;
        MoveScenery(games[owner], games[owner].ReadScene()!.Movers[0] with { Y = 0, Motion = 0 });
        await Task.Delay(300, token);
        Position(games[rider], 67, 100);
        await UntilAsync(() => games.All(game => SharedRules.Get(game.ReadScene()!.Puzzles, 4) == SwitchState.On), sessions, token);
        try
        {
            await UntilAsync(() => games.All(game => game.ReadScene()!.Movers[0].Y < 0), sessions, token);
        }
        catch
        {
            foreach (var game in games)
            {
                var current = game.ReadScene()!;
                Console.WriteLine($"Helicopter launch stalled: {System.Text.Json.JsonSerializer.Serialize(new
                {
                    current.Frame, current.Movers,
                    Launch = SharedRules.Get(current.Puzzles, 4), Stop = SharedRules.Get(current.Puzzles, 5)
                })}");
            }
            throw;
        }
        Position(games[rider], 67);
        await Task.Delay(300, token);
        SetPuzzle(games[owner], 5, SwitchState.On);
        await UntilAsync(() => games.All(game => game.ReadScene()!.Movers[0].Motion == 0), sessions, token);
        Console.WriteLine("Native follower contact: helicopter launch and stop reached all three games.");
    }

    private static async Task DeathAsync(List<GameBridge> games, Session[] sessions, CancellationToken token)
    {
        var scene = games[2].ReadScene()!;
        var victim = scene.Enemies.FirstOrDefault(value => value.Health > 0);
        if (victim is null)
        {
            return;
        }

        games[2].SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
            new RoomState(scene.Frame.Level, Guid.Empty, 1,
                scene.Enemies.Select(value => value.Mover == victim.Mover ? value with { Health = 0 } : value).ToArray(), scene.Movers)));
        await UntilAsync(() => games.All(game => game.ReadScene()!.Enemies.Any(value => value.Mover == victim.Mover
            && value.Health == 0)), sessions, token);
        Console.WriteLine("Enemy death: all three native actors became inactive before room transitions.");
    }

    private static void Position(GameBridge game, short x, byte y = 147)
    {
        var frame = game.Read() ?? throw new IOException("The test game is in a transition.");
        game.Constrain(new PlayerState(Guid.Empty, "Trio position", frame with { X = x, Y = y }, 1, ConstraintKind.Position));
    }

    private static int VisibleGuests(GameBridge game)
    {
        var status = game.Inspect();
        if (status.RenderHex != Convert.ToHexString(RenderPatch.Build(EnemyPlan.RenderCount)))
        {
            // With the original draw count, unused bytes are not visible actors.
            return 0;
        }

        var table = Convert.FromHexString(status.TableHex);
        return Enumerable.Range(EnemyPlan.FirstSlot - 1, table.Length / GameLayout.RecordSize - EnemyPlan.FirstSlot + 1)
            .Count(slot => table[slot * GameLayout.RecordSize + GameLayout.CharacterOffset] < GameLayout.PlayerCharacterLimit);
    }

    private static void SetPuzzle(GameBridge game, int index, SwitchState state)
    {
        var scene = game.ReadScene() ?? throw new IOException("The test room is unavailable.");
        var puzzles = SharedRules.Normalize(scene.Puzzles);
        SharedRules.Set(puzzles, new PuzzleChange(index, state));
        game.SyncScene(new SharedState(puzzles, null));
    }

    private static void Connect(Session[] sessions, int[] ids, Uri endpoint, string key, InjectionMode mode)
    {
        for (var index = 0; index < 3; index++)
        {
            sessions[index].Connect(ids[index], endpoint, key, $"Trio {index + 1}", mode,
                mode == InjectionMode.Observe ? InteractionMode.Disabled : InteractionMode.Experimental,
                mode == InjectionMode.Observe ? SharedMode.Independent : SharedMode.Experimental);
        }
    }

    private static Task DisconnectAsync(Session[] sessions) => Task.WhenAll(sessions.Select(session => session.DisconnectAsync()));

    private static void CheckActive(Session[] sessions)
    {
        var stopped = sessions.FirstOrDefault(session => session.View.Status.StartsWith("Stopped:", StringComparison.Ordinal));
        if (stopped is not null)
        {
            throw new IOException(stopped.View.Status);
        }
    }

    private static void CheckStorage(GameStatus before, GameStatus after)
    {
        if (before.RenderHex != after.RenderHex || before.SlotHash != after.SlotHash || before.EnemyCount != after.EnemyCount)
        {
            throw new IOException("Native renderer, guest storage or actor count was not restored.");
        }
    }

    private static async Task UntilAsync(Func<bool> condition, Session[] sessions, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        while (!condition())
        {
            CheckActive(sessions);
            await Task.Delay(20, timeout.Token);
        }
    }
}
