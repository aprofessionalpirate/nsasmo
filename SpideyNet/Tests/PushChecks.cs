using SpideyNet.Core;

namespace SpideyNet.Tests;

internal static class PushChecks
{
    internal static void Run()
    {
        var source = new GameFrame(1, 100, 80, 0, 24);
        foreach (var (x, y) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1) })
        {
            var target = source with { X = (short)(x < 0 ? 180 : 50), Y = (byte)(y < 0 ? 130 : 25) };
            var pusher = target with { X = (short)(target.X - x * 20), Y = (byte)(target.Y - y * 20) };
            var push = new PushMotion(pusher, target, 1000);
            var middle = Advance(push, target, 1000, 200);
            var end = Advance(push, middle, 1200, 200);
            Check((end.X - target.X) * x >= 0 && (end.ScreenY - target.ScreenY) * y >= 0,
                "Pushes move away from their source in every direction");
            Check(Math.Abs(Math.Sqrt(Math.Pow(end.X - target.X, 2) + Math.Pow(end.ScreenY - target.ScreenY, 2)) - 128) < 1,
                "Cardinal and diagonal pushes cover the same distance");
            Check(Math.Abs(middle.X - target.X) <= Math.Abs(end.X - target.X)
                && Math.Abs(middle.ScreenY - target.ScreenY) <= Math.Abs(end.ScreenY - target.ScreenY),
                "The trajectory progresses without overshooting");
            Check(!push.Finished(target, 1400) && push.Finished(end, 1400) && push.Finished(target, 6000),
                "Retain the endpoint until applied, with a bounded acknowledgement timeout");
        }

        Check(InteractionRules.CanPush(source, source with { X = 228 })
            && !InteractionRules.CanPush(source, source with { X = 229 })
            && InteractionRules.CanPush(source, source with { Y = 150 })
            && !InteractionRules.CanPush(source, source with { Level = 2 }),
            "Larger radial targeting includes vertical targets but never crosses rooms");
        var wrapped = source with { Y = 250 };
        Check(InteractionRules.CanPush(wrapped, source with { Y = 6 }), "Use signed native top-edge coordinates");
        var edgeStart = source with { X = 230, Y = 150 };
        var edge = Advance(new PushMotion(source, edgeStart, 0), edgeStart, 0, 400);
        Check(edge.X == 235 && edge.Y <= 155 && NetRules.Valid(edge), "Push endpoints remain bounded inside their native room");
        var overlap = Advance(new PushMotion(source, source, 0), source, 0, 400);
        Check(overlap.X == 228 && overlap.Y == 80, "Overlapping players use a deterministic facing fallback");
        // The first 15 pixels reach the right edge; the remaining 113 continue in room 2.
        var doorway = source with { X = 220, Exits = new RoomLinks(0, 2, 0, 0) };
        var crossingPush = new PushMotion(source, doorway, 0);
        var crossing = Advance(crossingPush, doorway, 0, 1000);
        Check(crossing.Level == 2 && crossing.X == 113, "Carry the full unspent impulse into a connected room");
        var waitingPush = new PushMotion(source, doorway, 0);
        var entrance = waitingPush.Sample(doorway, 50);
        Check(entrance.Level == 2 && entrance.X == 0, "Crossing requests the connected room's entrance");
        Check(waitingPush.Sample(doorway, 1000) == entrance && waitingPush.Accepts(1) && waitingPush.Accepts(2),
            "Pause displacement while native loading is unacknowledged");
        Check(!NetRules.Valid(source with { Exits = new RoomLinks(0, 64, 0, 0) }), "Reject invalid room links on the wire");
        Console.WriteLine("Pushes: radial range, all directions, smooth displacement, endpoint acknowledgement and room bounds passed.");
    }

    private static GameFrame Advance(PushMotion push, GameFrame current, long start, int duration)
    {
        for (var now = start + 25; now <= start + duration; now += 25)
        {
            current = push.Sample(current, now);
        }
        return current;
    }

    private static void Check(bool success, string message)
    {
        if (!success)
        {
            throw new InvalidOperationException(message);
        }
    }
}
