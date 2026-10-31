using SpideyNet.Core;
using SpideyNet.Tests;

try
{
    if (args.Length == 3 && args[0] == "--demo")
    {
        await DemoPeer.Run(new Uri(args[1]), args[2]);
        return 0;
    }

    var data = new byte[GameLayout.RecordSize];
    data[2] = 248;
    data[3] = 255;
    data[4] = 147;
    data[6] = 0x12;

    Check(() => GameLayout.Decode(data, 2)?.X == -8, "Signed offscreen coordinate");
    Check(() => GameLayout.Decode(data, 2)?.Pose == data[GameLayout.CharacterOffset], "Publish the rendered animation frame");
    Check(() => GameLayout.Decode(data, 0) is null, "Reject menu/dead zone");
    Check(() => GameLayout.Decode(data, 64) is null, "Reject unknown room");
    Check(() => GameLayout.Decode(data.AsSpan(0, 12), 2) is null, "Reject partial read");
    data[6] = 0x80;
    Check(() => GameLayout.Decode(data, 2) is null, "Reject non-player data");
    data[6] = 0x12;
    data[19] = 0x94;
    data[20] = 0x9B;
    data[39] = 0xC0;
    data[31] = 0x98;
    data[32] = 0x92;
    // Health is read from the actor without changing its native 0..192 scale.
    foreach (var (native, percent) in new (byte, int)[] { (0, 0), (1, 1), (96, 50), (192, 100), (255, 100) })
    {
        data[GameLayout.HealthOffset] = native;
        Check(() => GameLayout.Decode(data, 2)?.HealthPercent == percent, "Scale native player health to 0..100");
    }

    data[GameLayout.HealthOffset] = GameLayout.MaxHealth;
    var source = data.ToArray();
    const byte StandingPose = 0x18;
    var guest = new GameFrame(2, 96, 147, 2, StandingPose);
    Check(() => NetRules.Valid(guest with { Facing = 4 }), "Publish upside-down player poses");
    Check(() => !NetRules.Valid(guest with { Facing = GameLayout.FacingMask + 1 }), "Reject unknown facing bits");
    const ushort GuestDescriptor = 0x9509;
    var actor = EnemyPlan.Build(data, guest, GuestDescriptor);
    Check(() => actor.Length == EnemyPlan.Stride, "Bounded enemy record");
    Check(() => actor[1] == 0, "Do not copy local input");
    Check(() => actor[GameLayout.CharacterOffset] == StandingPose, "Guest animation follows the remote player");
    Check(() => !NetRules.Valid(guest with { Pose = GameLayout.HiddenCharacter }), "Reject non-player sprite frames");
    Check(() => actor[19] == data[19], "Use local sprite references");
    Check(() => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(actor.AsSpan(31)) == GuestDescriptor,
        "Use a separate valid actor descriptor");
    Check(() => actor[39] == 0 && actor[48] == 0 && actor[49] == 0, "Do not copy health or attack power");
    Check(() => data.SequenceEqual(source), "Do not mutate source record");
    Check(() => EnemyPlan.NativeCount + EnemyPlan.MaxGuests <= EnemyPlan.Capacity, "Stay within documented count");
    Check(() => EnemyPlan.Build(data, null, GuestDescriptor)[6] == 0x5F, "Hide absent players");
    Check(() => EnemyPlan.MaxGuests * EnemyPlan.DescriptorSize <= (EnemyPlan.Capacity - EnemyPlan.DescriptorSlot) * EnemyPlan.Stride,
        "Descriptors fit outside the active actor range");
    Check(() => RenderPatch.Build(12).SequenceEqual(new byte[] { 0xB9, 12, 0, 0x90 }), "Patch only the renderer count instruction");
    Check(() => Math.Abs(NetRules.TickMs - 1000d / 30) < 0.001, "Target thirty network updates per second");
    Check(() => NetRules.LocalTickRate == 60, "Keep local input and rendering at sixty hertz");
    Check(() => NetRules.MaxRate >= 30 * 2, "Allow thirty updates with burst headroom");

    var track = new RemoteTrack();
    track.Add(guest with { X = 0 }, 1000);
    track.Add(guest with { X = 20 }, 1100);
    var sampleTime = (long)Math.Round(1050 + NetRules.TickMs * 2);
    Check(() => track.Sample(sampleTime)?.X == 10, "Interpolate between buffered samples");
    Check(() => track.Sample(4000) is null, "Expire disconnected players");
    track.Add(guest with { X = 68 }, 4000);
    Check(() => track.Sample(4000)?.X == 68, "Reappearing players do not reuse expired interpolation samples");
    track.Add(guest with { Level = 3, X = 100 }, 4100);
    Check(() => track.Sample(4100)?.X == 100, "Snap across rooms");
    Check(() => InteractionRules.CanPush(guest with { Facing = 0, X = 0 }, guest with { X = 40 }), "Push reaches a nearby player");
    Check(() => InteractionRules.CanPush(guest with { Facing = 2, X = 0 }, guest with { X = 40 }), "Push direction depends on separation, not facing");
    Check(() => !InteractionRules.CanPush(guest, guest with { Level = 3 }), "Push cannot cross rooms");
    PushChecks.Run();
    SharedChecks.Run();
    PuzzlePulseChecks.Run();
    MoverAuthorityChecks.Run();
    VideoChecks.Run();
    SpriteChecks.Run();
    PlayerSpriteChecks.Run();
    PlayerEdgeChecks.Run();
    PlacementChecks.Run();
    FeatureChecks.Run();
    RelayEndpointChecks.Run();
    TauntChecks.Run();
    if (args.Length == 2 && args[0] == "--relay")
    {
        await IntegrationChecks.RunAsync(new Uri(args[1]));
        await SpectatorChecks.RunAsync(new Uri(args[1]));
        await TauntSocketChecks.RunAsync(new Uri(args[1]));
        await EnemyOwnerSocketChecks.RunAsync(new Uri(args[1]));
        await PuzzlePulseSocketChecks.RunAsync(new Uri(args[1]));
        await TrioInteractionChecks.RunAsync(new Uri(args[1]));
        await SharedSocketChecks.Run(new Uri(args[1]));
    }
    Console.WriteLine("All checks passed.");
    return 0;
}
catch (Exception error)
{
    // Report failed checks in the terminal without opening a Windows application-error dialog.
    Console.Error.WriteLine($"Tests failed: {error}");
    return 1;
}

static void Check(Func<bool> condition, string name)
{
    if (!condition())
    {
        throw new InvalidOperationException(name);
    }
}
