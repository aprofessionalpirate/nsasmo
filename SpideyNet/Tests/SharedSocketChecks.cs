using SpideyNet.Core;
using SpideyNet.Transport;

internal static class SharedSocketChecks
{
    private const byte StandingPose = 0x18;
    internal static async Task Run(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var firstStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        var key = Guid.NewGuid().ToString("N");
        var first = new RelayClient();
        var second = new RelayClient();
        var frame = new GameFrame(1, 10, 100, 0, StandingPose);
        var seed = new byte[SharedRules.PuzzleBytes];
        var enemy = new EnemyState(3, 100, 100, 0, 192);
        var helicopter = new MoverState(0, 0, 0, 0, 8, 15, 6, 2);
        first.Publish(frame, new SharedReport(seed, [], 0, [enemy], Movers: [helicopter]));
        var firstTask = first.Run(endpoint, key, "Owner", InteractionMode.Disabled, firstStop.Token, SharedMode.Experimental);
        Task? secondTask = null;
        try
        {
            await Until(() => first.World?.Shared?.Room is { } room && room.Owner == first.World.Self, stop.Token);
            var original = first.World!.Shared!.Room!;
            first.Publish(frame, new SharedReport(seed, [], original.Epoch, [enemy with { X = 120 }], Movers: [helicopter]));
            await Until(() => first.World?.Shared?.Room?.Enemies.Single().X == 120, stop.Token);
            second.Publish(frame with { X = 200 }, new SharedReport(seed, [], 0, [enemy with { X = 200, Health = 1 }], Movers: [helicopter]));
            secondTask = second.Run(endpoint, key, "Follower", InteractionMode.Disabled, stop.Token, SharedMode.Experimental);
            await Until(() => second.World?.Shared?.Room is not null, stop.Token);
            Check(() => second.World!.Shared!.Room!.Enemies.Single().Health == 192, "Joining peer cannot overwrite cached health");
            second.Publish(frame, new SharedReport(seed, [new PuzzleChange(2, SwitchState.On)], original.Epoch,
                [enemy with { X = 200, Health = 96 }], 1, [helicopter]));
            await Until(() => first.World?.Shared?.Room?.Enemies.Single().Health == 96
                && SharedRules.Get(first.World.Shared.Puzzles, 2) == SwitchState.On, stop.Token);
            Check(() => first.World!.Shared!.Room!.Enemies.Single().X == 120, "Follower damage does not move authoritative enemy");
            await Until(() => second.World?.Shared?.Acknowledged == 1, stop.Token);
            second.Publish(frame, new SharedReport(seed, [new PuzzleChange(2, SwitchState.Off)], original.Epoch, [enemy], 2, [helicopter]));
            await Until(() => SharedRules.Get(first.World!.Shared!.Puzzles, 2) == SwitchState.Off, stop.Token);
            Check(() => first.World!.Shared!.Room!.Enemies.Single().Health == 96, "Stale owner cannot heal shared enemy");

            second.Publish(frame with { X = 64 }, new SharedReport(seed, [], original.Epoch, [enemy], Movers: [helicopter]));
            await Until(() => second.World?.Shared?.Room?.Movers.Single().Owner == second.World?.Self, stop.Token);
            Check(() => first.World!.Shared!.Room!.Owner == first.World.Self, "A rider can drive scenery independently of enemy authority");
            second.Publish(frame with { X = 64 }, new SharedReport(seed, [], original.Epoch, [enemy],
                Movers: [helicopter with { Y = -1 }]));
            await Until(() => first.World?.Shared?.Room?.Movers.Single().Y == -1, stop.Token);

            firstStop.Cancel();
            await Until(() => second.World?.Shared?.Room?.Owner == second.World?.Self, stop.Token);
            var handoff = second.World!.Shared!.Room!;
            Check(() => handoff.Epoch != original.Epoch && handoff.Enemies.Single().Health == 96, "Handoff retains cached health");
            second.Publish(frame with { Level = 2 }, new SharedReport(seed, [], 0, []));
            await Until(() => second.World?.Shared?.Room?.Level == 2, stop.Token);
            second.Publish(frame, new SharedReport(seed, [], 0, [enemy], Movers: [helicopter]));
            await Until(() => second.World?.Shared?.Room?.Level == 1, stop.Token);
            Check(() => second.World!.Shared!.Room!.Enemies.Single().Health == 96, "Room reentry preserves enemy damage");
            second.Publish(frame, new SharedReport(seed, [], second.World!.Shared!.Room!.Epoch, [enemy with { Health = 0 }], Movers: [helicopter]));
            await Until(() => second.World?.Shared?.Room?.Enemies.Single().Health == 0, stop.Token);
            Console.WriteLine("Shared relay checks: owner movement, follower damage, joining, switches, acknowledgement, rider authority, helicopter movement, handoff, room reentry, death.");
        }
        finally
        {
            stop.Cancel();
            try { await Task.WhenAll(secondTask is null ? [firstTask] : [firstTask, secondTask]); }
            catch (OperationCanceledException) { }
        }
    }

    private static async Task Until(Func<bool> condition, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition()) { await Task.Delay(20, timeout.Token); }
    }

    private static void Check(Func<bool> condition, string name)
    {
        if (!condition()) { throw new InvalidOperationException(name); }
    }
}
