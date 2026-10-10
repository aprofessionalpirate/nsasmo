using SpideyNet.Core;
using SpideyNet.Game;
using SpideyNet.Transport;

internal static class PairProbe
{
    private const int LimitSeconds = 30;
    private const int SwitchOnTick = 60;
    private const int SwitchOffTick = 100;
    private const int DamageTick = 140;
    private const int FinishTick = 180;
    private const int RaiseTick = 40;
    private const int LowerTick = 160;

    internal static async Task Run(Uri endpoint, int firstId, int secondId, PairMode mode)
    {
        if (firstId == secondId) { throw new ArgumentException("Choose two distinct DOSBox processes."); }
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(LimitSeconds));
        using var firstGame = new GameBridge(firstId, stop.Token);
        using var secondGame = new GameBridge(secondId, stop.Token);
        var firstBefore = firstGame.InspectScene() ?? throw new InvalidOperationException("Start the first game.");
        var secondBefore = secondGame.InspectScene() ?? throw new InvalidOperationException("Start the second game.");
        if (firstBefore.Scene!.Frame.Level != secondBefore.Scene!.Frame.Level || firstBefore.Triggers.Length == 0
            || firstBefore.Scene.Enemies.Length == 0)
        {
            throw new InvalidOperationException("Choose the same room, containing an enemy and a switch.");
        }
        var trigger = firstBefore.Triggers[0].Index;
        var initial = SharedRules.Get(firstBefore.Scene.Puzzles, trigger);
        var opposite = initial == SwitchState.On ? SwitchState.Off : SwitchState.On;
        var mover = firstBefore.Scene.Enemies[0].Mover;
        var health = (byte)(firstBefore.Scene.Enemies[0].Health / 2);
        var helicopter = firstBefore.Scene.Movers.FirstOrDefault();
        var first = new RelayClient();
        var second = new RelayClient();
        var firstTrack = new SharedTrack();
        var secondTrack = new SharedTrack();
        var room = Guid.NewGuid().ToString("N");
        first.Publish(firstBefore.Scene.Frame, new SharedReport(SharedRules.Normalize(firstBefore.Scene.Puzzles), [], 0,
            firstBefore.Scene.Enemies, Movers: firstBefore.Scene.Movers));
        var firstTask = first.Run(endpoint, room, "Native first", InteractionMode.Disabled, stop.Token, SharedMode.Experimental);
        Task? secondTask = null;
        try
        {
            while (first.World?.Shared?.Room is null) { await Task.Delay(NetRules.TickMs, stop.Token); }
            secondTask = second.Run(endpoint, room, "Native second", InteractionMode.Disabled, stop.Token, SharedMode.Experimental);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(NetRules.TickMs));
            for (var tick = 0; tick <= FinishTick && await timer.WaitForNextTickAsync(stop.Token); tick++)
            {
                if (tick is SwitchOnTick or SwitchOffTick)
                {
                    var scene = firstGame.ReadScene()!;
                    var puzzles = SharedRules.Normalize(scene.Puzzles);
                    SharedRules.Set(puzzles, new PuzzleChange(trigger, tick == SwitchOnTick ? opposite : initial));
                    firstGame.SyncScene(new SharedState(puzzles, null));
                }
                if (tick == DamageTick)
                {
                    // Damage on the follower must reach the room owner.
                    var target = first.World!.Shared!.Room!.Owner == first.World.Self ? secondGame : firstGame;
                    var scene = target.ReadScene()!;
                    target.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
                        new RoomState(scene.Frame.Level, Guid.Empty, 1,
                            scene.Enemies.Select(enemy => enemy.Mover == mover ? enemy with { Health = health } : enemy).ToArray(), scene.Movers)));
                }
                if (helicopter is not null && tick is RaiseTick or LowerTick)
                {
                    var scene = firstGame.ReadScene()!;
                    var step = helicopter.Top + helicopter.Y == 0 ? 1 : -1;
                    var target = helicopter with { Y = tick == RaiseTick ? checked((sbyte)(helicopter.Y + step)) : helicopter.Y };
                    firstGame.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
                        new RoomState(scene.Frame.Level, Guid.Empty, 1, scene.Enemies,
                            scene.Movers.Select(mover => mover.Mover == target.Mover ? target : mover).ToArray())));
                }
                Update(firstGame, first, firstTrack);
                Update(secondGame, second, secondTrack);
                if (helicopter is not null && tick == 55)
                {
                    var firstScene = firstGame.InspectScene()!;
                    var secondScene = secondGame.InspectScene()!;
                    var firstMover = firstScene.Scene!.Movers.Single(mover => mover.Mover == helicopter.Mover);
                    var secondMover = secondScene.Scene!.Movers.Single(mover => mover.Mover == helicopter.Mover);
                    if (firstMover.Y != secondMover.Y || firstScene.TileHash != secondScene.TileHash)
                    {
                        throw new InvalidOperationException("Native moving scenery or its collision tiles did not synchronize.");
                    }
                    Console.WriteLine($"Native helicopter: Y {firstMover.Y}; matching collision grids.");
                }
                if (tick is 90 or 130)
                {
                    var expected = tick == 90 ? opposite : initial;
                    if (SharedRules.Get(secondGame.ReadScene()!.Puzzles, trigger) != expected)
                    {
                        throw new InvalidOperationException("Native puzzle state did not synchronize.");
                    }
                    Console.WriteLine($"Native switch {trigger}: {expected} on both games.");
                }
            }
            if (mode == PairMode.Contact)
            {
                await Contact(firstGame, secondGame, first, second, firstTrack, secondTrack, stop.Token);
            }
            if (mode == PairMode.Transition)
            {
                await Transition(firstGame, secondGame, first, second, firstTrack, secondTrack, stop.Token);
            }
            var after = new[] { firstGame.InspectScene()!, secondGame.InspectScene()! };
            foreach (var status in after)
            {
                if (mode != PairMode.Transition && status.Scene!.Enemies.Single(enemy => enemy.Mover == mover).Health > health)
                {
                    throw new InvalidOperationException("Native follower damage did not synchronize.");
                }
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(status));
            }
            firstGame.Render([], InjectionMode.Observe);
            secondGame.Render([], InjectionMode.Observe);
            Console.WriteLine("Two-game puzzle, enemy and moving-scenery synchronization passed.");
        }
        finally
        {
            stop.Cancel();
            try { await Task.WhenAll(secondTask is null ? [firstTask] : [firstTask, secondTask]); }
            catch (OperationCanceledException) { }
        }
    }

    private static PuzzleChange[] Update(GameBridge game, RelayClient relay, SharedTrack track)
    {
        var scene = game.ReadScene() ?? throw new IOException("Game left the test room.");
        var world = relay.World;
        relay.Publish(scene.Frame, track.Capture(scene, world));
        if (track.Accept(scene, world) is { } correction) { game.SyncScene(correction); }
        var guests = world?.Players.Where(player => player.Id != world.Self).Select(player => player.Frame).ToArray() ?? [];
        game.Render(guests, InjectionMode.Experimental);
        return scene.Events ?? [];
    }

    private static async Task Contact(GameBridge firstGame, GameBridge secondGame, RelayClient first, RelayClient second,
        SharedTrack firstTrack, SharedTrack secondTrack, CancellationToken token)
    {
        const int ContactIndex = 4;
        const int StopIndex = 5;
        const short ContactX = 67;
        const byte ContactY = 100;
        const short GroundX = 11;
        const byte GroundY = 147;
        foreach (var game in new[] { firstGame, secondGame })
        {
            game.Constrain(new PlayerState(Guid.Empty, "Contact probe", game.Read()! with { X = GroundX, Y = GroundY },
                1, ConstraintKind.Collision));
        }
        for (var tick = 0; tick < 10; tick++)
        {
            Update(firstGame, first, firstTrack);
            Update(secondGame, second, secondTrack);
            await Task.Delay(NetRules.TickMs, token);
        }
        var initial = firstGame.ReadScene()!;
        var reset = SharedRules.Normalize(initial.Puzzles);
        SharedRules.Set(reset, new PuzzleChange(ContactIndex, SwitchState.Off));
        SharedRules.Set(reset, new PuzzleChange(StopIndex, SwitchState.Off));
        firstGame.SyncScene(new SharedState(reset, null));
        for (var tick = 0; tick < 20; tick++)
        {
            Update(firstGame, first, firstTrack);
            Update(secondGame, second, secondTrack);
            await Task.Delay(NetRules.TickMs, token);
        }
        var before = secondGame.ReadScene()!;
        const SwitchState Expected = SwitchState.On;
        if (SharedRules.Get(before.Puzzles, ContactIndex) != SwitchState.Off)
        {
            throw new InvalidOperationException("One-shot contact fixture did not reset.");
        }
        secondGame.Constrain(new PlayerState(Guid.Empty, "Contact probe", before.Frame with { X = ContactX, Y = ContactY },
            1, ConstraintKind.Collision));
        var observed = new List<PuzzleChange>();
        for (var tick = 0; tick < 40; tick++)
        {
            Update(firstGame, first, firstTrack);
            observed.AddRange(Update(secondGame, second, secondTrack));
            await Task.Delay(NetRules.TickMs, token);
        }
        if (!observed.Any(change => change.Index == ContactIndex)
            || SharedRules.Get(firstGame.ReadScene()!.Puzzles, ContactIndex) != Expected)
        {
            throw new InvalidOperationException($"Native contact failed: events {string.Join(',', observed.Select(change => $"{change.Index}:{change.State}"))}; "
                + $"expected {Expected}, first {SharedRules.Get(firstGame.ReadScene()!.Puzzles, ContactIndex)}, "
                + $"second {SharedRules.Get(secondGame.ReadScene()!.Puzzles, ContactIndex)}, frame {secondGame.Read()}.");
        }
        secondGame.Constrain(new PlayerState(Guid.Empty, "Contact probe", before.Frame, 1, ConstraintKind.Collision));
        var driver = first.World!.Shared!.Room!.Movers[0].Owner == first.World.Self ? firstGame : secondGame;
        var stopped = driver.ReadScene()!;
        driver.SyncScene(new SharedState(SharedRules.Normalize(stopped.Puzzles),
            new RoomState(stopped.Frame.Level, Guid.Empty, 1, stopped.Enemies,
                stopped.Movers.Select(mover => mover with { Motion = 0 }).ToArray())));
        for (var tick = 0; tick < 20; tick++)
        {
            Update(firstGame, first, firstTrack);
            Update(secondGame, second, secondTrack);
            await Task.Delay(NetRules.TickMs, token);
        }
        Console.WriteLine("Native follower contact reached both games.");
    }

    private static async Task Transition(GameBridge firstGame, GameBridge secondGame, RelayClient first, RelayClient second,
        SharedTrack firstTrack, SharedTrack secondTrack, CancellationToken token)
    {
        const byte Midnight = 1;
        const byte Foyer = 2;
        const short ExitX = 260;
        var scene = firstGame.ReadScene()!;
        if (scene.Frame.Level != Midnight) { throw new InvalidOperationException("Start the transition test in Midnight."); }
        var victim = scene.Enemies[0].Mover;
        firstGame.SyncScene(new SharedState(SharedRules.Normalize(scene.Puzzles),
            new RoomState(scene.Frame.Level, Guid.Empty, 1,
                scene.Enemies.Select(enemy => enemy.Mover == victim ? enemy with { Health = 0 } : enemy).ToArray(), scene.Movers)));
        for (var tick = 0; tick < 20; tick++)
        {
            Update(firstGame, first, firstTrack);
            Update(secondGame, second, secondTrack);
            await Task.Delay(NetRules.TickMs, token);
        }
        if (secondGame.ReadScene()!.Enemies.Single(enemy => enemy.Mover == victim).Health != 0)
        {
            throw new InvalidOperationException("Native enemy death did not synchronize.");
        }
        foreach (var game in new[] { firstGame, secondGame })
        {
            game.Constrain(new PlayerState(Guid.Empty, "Transition probe", game.Read()! with { X = ExitX }, 1, ConstraintKind.Collision));
            for (var tick = 0; tick < 20; tick++)
            {
                Update(firstGame, first, firstTrack);
                Update(secondGame, second, secondTrack);
                await Task.Delay(NetRules.TickMs, token);
            }
        }
        if (firstGame.Read()!.Level != Foyer || secondGame.Read()!.Level != Foyer
            || first.World?.Shared?.Room?.Level != Foyer || second.World?.Shared?.Room?.Level != Foyer)
        {
            throw new InvalidOperationException("Native shared room transition did not complete.");
        }
        Console.WriteLine("Native enemy death and Midnight-to-Foyer handoff passed.");
    }
}
