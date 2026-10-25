using SpideyNet.Core;
using SpideyNet.Relay;

namespace SpideyNet.Tests;

internal static class MoverAuthorityChecks
{
    internal static void Run()
    {
        var rooms = new SharedRooms();
        var owner = new Peer("Enemy owner");
        var rider = new Peer("Helicopter rider");
        var frame = new GameFrame(1, 10, 100, 0, 24);
        var helicopter = new MoverState(0, 0, 0, 0, 8, 15, 6, 2);
        owner.State = new PlayerState(owner.Id, owner.Name, frame, 1);
        rooms.Apply(owner, new SharedReport(new byte[SharedRules.PuzzleBytes], [], 0, [], Movers: [helicopter]));
        var epoch = rooms.Snapshot(1, 0)!.Room!.Epoch;
        owner.SharedEpoch = epoch;
        owner.Movers = new MoverObservation(1, epoch, 1, [helicopter]);
        rider.SharedEpoch = epoch;
        rider.State = new PlayerState(rider.Id, rider.Name, frame with { X = 64, Y = 91 }, 2);
        rider.Movers = new MoverObservation(1, epoch, 2, [helicopter with { Y = -1, Motion = 0x60 }]);

        // A non-owner's movement is not accepted yet. Election must still see contact
        // against its own helicopter and retain the launch command during the handoff.
        rooms.Apply(rider, new SharedReport(new byte[SharedRules.PuzzleBytes], [], epoch, [],
            Movers: rider.Movers.Movers));
        rooms.Elect([owner, rider]);
        var chosen = rooms.Snapshot(1, 0)!.Room!.Movers.Single();
        Check(chosen.Owner == rider.Id && chosen.Y == 0 && chosen.Motion == 0x60,
            "Rider handoff preserves cached position and a newly triggered launch");

        rider.Movers = rider.Movers with { Epoch = 0 };
        rooms.Elect([rider]);
        Check(rooms.Snapshot(1, 0)!.Room!.Movers.Single().Owner == rider.Id,
            "Current rider survives an enemy-owner epoch acknowledgement");
        rider.State = rider.State with { Sequence = 3, Frame = rider.State.Frame with { X = 230 } };
        owner.State = owner.State with { Sequence = 4, Frame = frame with { X = 200 } };
        rooms.Elect([owner, rider]);
        Check(rooms.Snapshot(1, 0)!.Room!.Movers.Single().Owner == owner.Id,
            "An outdated contact observation cannot retain rider control");
        Console.WriteLine("Mover authority: lagging contact, one-shot launch, cached position, epoch handoff and stale-report rejection passed.");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }
}
