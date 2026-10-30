using SpideyNet.Core;
using SpideyNet.Relay;

namespace SpideyNet.Tests;

internal static class PuzzlePulseChecks
{
    internal static void Run()
    {
        const int button = 176;
        var puzzles = new byte[SharedRules.PuzzleBytes];
        var frame = new GameFrame(46, 27, 99, 0, 24);
        var owner = new Peer("Owner");
        owner.State = new PlayerState(owner.Id, owner.Name, frame, 1);
        var follower = new Peer("Follower");
        follower.State = new PlayerState(follower.Id, follower.Name, frame, 1);
        var rooms = new SharedRooms();
        rooms.Apply(owner, new SharedReport(puzzles, [], 0, []));
        var cached = rooms.Snapshot(46, 0)!;
        var world = new WorldState(NetRules.Version, follower.Id, 1, [], cached);
        var scene = new GameScene(frame, puzzles, [], []);
        var track = new SharedTrack();
        track.Capture(scene, world);
        track.Accept(scene, world);
        var pressed = scene with { Events = [new PuzzleChange(button, SwitchState.Off)], PulseInputs = [button] };
        var report = track.Capture(pressed, world);
        Check(report.Pulses is { Length: 1 } && report.Pulses[0].State == SwitchState.On,
            "Publish a press that reset before scene sampling");
        Check(SharedRules.Valid(report), "Validate a pulse report");
        var origin = track.Accept(pressed, world with { Tick = 2 });
        Check(SharedRules.Get(origin!.Puzzles, button) == SwitchState.Off, "Never replay input on its originating game");
        rooms.Apply(follower, report);
        Check(SharedRules.Get(rooms.Snapshot(46, 0)!.Puzzles, button) == SwitchState.On, "Owner receives the input state");
        rooms.Apply(owner, new SharedReport(puzzles, [new PuzzleChange(button, SwitchState.Off)], cached.Room!.Epoch, []));
        rooms.Apply(follower, report);
        Check(SharedRules.Get(rooms.Snapshot(46, 0)!.Puzzles, button) == SwitchState.Off, "Retried input cannot fire the script twice");
        track.Accept(pressed, world with { Tick = 3, Shared = cached with { Acknowledged = report.Serial } });
        Check(track.Capture(scene, world).Pulses is { Length: 0 }, "Acknowledge input even if the script has already reset it");

        var alreadyOn = puzzles.ToArray();
        SharedRules.Set(alreadyOn, new PuzzleChange(button, SwitchState.On));
        var once = new SharedTrack();
        var onScene = scene with { Puzzles = alreadyOn };
        var onWorld = world with { Shared = cached with { Puzzles = alreadyOn } };
        once.Capture(onScene, onWorld);
        once.Accept(onScene, onWorld);
        Check(once.Capture(onScene with { Events = [new PuzzleChange(button, SwitchState.On)] }, onWorld).Pulses is { Length: 0 },
            "An already-on one-shot latch must not be invented as another press");

        var ownerTrack = new SharedTrack();
        var ownerWorld = world with { Self = owner.Id };
        ownerTrack.Capture(scene, ownerWorld);
        ownerTrack.Accept(scene, ownerWorld);
        var ownerReport = ownerTrack.Capture(pressed, ownerWorld);
        var ackPuzzle = puzzles.ToArray();
        SharedRules.Set(ackPuzzle, new PuzzleChange(button, SwitchState.On));
        ownerTrack.Accept(pressed, ownerWorld with { Tick = 2,
            Shared = cached with { Acknowledged = ownerReport.Serial, Puzzles = ackPuzzle } });
        Check(ownerTrack.Capture(scene, ownerWorld).Changes.Any(change => change.Index == button && change.State == SwitchState.Off),
            "An originating owner acknowledges delivery then publishes the consumed state");
        Check(!SharedRules.Valid(report with { Pulses = [new PuzzlePulse(button, SwitchState.On, report.Serial + 1)] }),
            "Reject future input serials");
        Console.WriteLine("Puzzle pulses: self-reset input, source suppression, retries, deduplication, acknowledgement and one-shot exclusion passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
