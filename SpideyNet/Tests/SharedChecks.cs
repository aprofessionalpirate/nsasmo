using SpideyNet.Core;

internal static class SharedChecks
{
    internal static void Run()
    {
        var initial = new byte[SharedRules.PuzzleBytes];
        var desired = initial.ToArray();
        SharedRules.Set(desired, new PuzzleChange(2, SwitchState.On));
        var patch = SharedRules.Apply(initial, desired);
        Check(() => patch[0] == 0x30, "Set switch state and its script dirty bit");
        patch[0] |= 2;
        Check(() => SharedRules.Apply(patch, desired)[0] == patch[0], "Preserve native dirty bits");
        Check(() => SharedRules.Normalize(patch)[0] == 0x10, "Never transmit transient script dirty bits");
        Check(() => SharedRules.Apply(patch, initial)[0] == 0x22, "Reversible switch marks native scripts dirty");
        Check(() => !SharedRules.Valid([new EnemyState(SharedRules.MaxMovers, 0, 0, 0, 1)]), "Reject remote mover addresses");
        var enemy = new EnemyState(3, 100, 100, 0, 192);
        Check(() => !SharedRules.Valid([enemy, enemy]), "Reject duplicate native identities");
        var frame = new GameFrame(1, 10, 100, 0, 94);
        var scene = new GameScene(frame, initial, [enemy], []);
        var self = Guid.NewGuid();
        var room = new RoomState(1, Guid.NewGuid(), 1, [enemy], []);
        var world = new WorldState(NetRules.Version, self, 1, [], new SharedState(initial, room, PuzzleOwner: room.Owner));
        var track = new SharedTrack();
        Check(() => track.Capture(scene, world).Epoch == 0, "New peers cannot overwrite cached enemies");
        track.Accept(scene, world);
        var derived = scene with { Puzzles = desired };
        Check(() => track.Capture(derived, world).Changes.Length == 0, "Follower script effects must not echo into shared puzzles");
        var changed = derived with { Events = [new PuzzleChange(2, SwitchState.On)] };
        var proposal = track.Capture(changed, world);
        Check(() => proposal.Changes.Single().Index == 2, "Publish native switch changes");
        var pending = track.Accept(changed, world with { Tick = 2 });
        Check(() => SharedRules.Get(pending!.Puzzles, 2) == SwitchState.On, "Retain local switch while awaiting acknowledgement");
        Check(() => track.Capture(changed, world).Changes.Length == 1, "Retry switch until acknowledged");
        track.Accept(changed, world with { Tick = 3, Shared = new SharedState(desired, room, proposal.Serial) });
        Check(() => track.Capture(changed, world).Changes.Length == 0, "Acknowledged switches stop retrying");

        var hurt = changed with { Enemies = [enemy with { Health = 96 }] };
        Check(() => track.Capture(hurt, world).Enemies[0].Health == 96, "Report follower damage");
        var correction = track.Accept(hurt, world with { Tick = 4 });
        Check(() => correction!.Room!.Enemies[0].Health == 96, "Pending damage survives old snapshots");
        var handed = world with { Tick = 5, Shared = new SharedState(desired, room with { Owner = self, Epoch = 2 }) };
        Check(() => track.Capture(hurt, handed).Epoch == 0, "Handoff first requires cached state");
        Check(() => track.Accept(hurt, handed)!.Room!.Enemies[0].X == enemy.X, "Handoff adopts cached position");
        Check(() => track.Capture(hurt, handed).Epoch == 2, "Handoff acknowledges new authority epoch");
        var helicopter = new MoverState(0, 0, 0, 0, 3, 15, 2, 1);
        var grid = new byte[MoverPlan.TileBytes];
        var start = helicopter.Top * MoverRules.Columns + helicopter.Left;
        grid[start] = 0xC1;
        grid[start + 1] = 0xC2;
        grid[0] = 0xC3;
        var shape = new MoverShape(helicopter, [0xC1, 0xC2]);
        var raised = helicopter with { Y = -1 };
        var moved = MoverPlan.Build(grid, [shape], [shape with { State = raised }]);
        Check(() => moved[start] == 0 && moved[start + 1] == 0, "Remove old platform collision tiles");
        Check(() => moved[start - MoverRules.Columns] == 0xC1 && moved[0] == 0xC3,
            "Move platform tiles without touching distant walls");
        Check(() => grid[start] == 0xC1, "Do not mutate source tile grid");
        Check(() => MoverRules.Riding(frame with { X = 24, Y = 100 }, helicopter), "Elect a rider as platform driver");
        Check(() => !MoverRules.Riding(frame with { X = 200, Y = 100 }, helicopter), "Reject distant riders");
        var edge = MoverPlan.Build(grid, [shape], [shape with { State = helicopter with { X = -3 } }]);
        Check(() => edge[helicopter.Top * MoverRules.Columns] == 0xC1, "Permit movers at the room boundary");
        Check(() => !MoverRules.Valid([helicopter with { X = -4 }]), "Reject partially offscreen mover rectangles");
        Check(() => !MoverRules.Valid([helicopter with { Y = -16 }]), "Reject mover positions that make native drawing leave the tile grid");
        Console.WriteLine("Shared-state checks passed.");
    }

    private static void Check(Func<bool> condition, string name)
    {
        if (!condition()) { throw new InvalidOperationException(name); }
    }
}
