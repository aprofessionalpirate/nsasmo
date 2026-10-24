using System.Buffers.Binary;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Tests;

internal static class PlacementChecks
{
    internal static void Run()
    {
        var current = Enumerable.Repeat((byte)0xA5, GameLayout.RecordSize).ToArray();
        Write(current, GameLayout.XOffset, 40);
        Write(current, GameLayout.BoundsLeftOffset, 41);
        Write(current, GameLayout.BoundsRightOffset, 51);
        Write(current, GameLayout.PreviousXOffset, 37);
        current[GameLayout.YOffset] = 140;
        current[GameLayout.BoundsTopOffset] = 141;
        current[GameLayout.BoundsBottomOffset] = 159;
        current[GameLayout.PreviousYOffset] = 136;
        current[29] = 8;
        current[30] = 12;
        var origins = new byte[] { 7, 7, 7, 0, 0, 7, 0, 0, 7, 0, 7, 7, 0, 0, 0, 7 };
        var original = current.ToArray();
        var target = new GameFrame(1, 90, 110, 2, 24);
        var actual = PlayerPlacement.Build(current, target, origins);
        var expected = original.ToArray();
        Write(expected, GameLayout.XOffset, 90);
        Write(expected, GameLayout.BoundsLeftOffset, 90);
        Write(expected, GameLayout.BoundsRightOffset, 97);
        Write(expected, GameLayout.PreviousXOffset, 90);
        expected[GameLayout.YOffset] = 110;
        expected[GameLayout.BoundsTopOffset] = 119;
        expected[GameLayout.BoundsBottomOffset] = 130;
        expected[GameLayout.PreviousYOffset] = 110;
        expected[GameLayout.FacingOffset] = 2;
        expected[GameLayout.PreviousFaceOffset] = 2;
        if (!actual.SequenceEqual(expected) || !current.SequenceEqual(original))
        {
            throw new InvalidOperationException("Placement must match exact coordinates/facing, reset history and preserve native animation state.");
        }
        // These expected anchors come from the native eight-facing origin table.
        var expectedLeft = new[] { 103, 103, 90, 90, 103, 103, 90, 90 };
        var expectedTop = new byte[] { 119, 110, 119, 110, 110, 119, 110, 119 };
        for (byte facing = 0; facing < 8; facing++)
        {
            var placed = PlayerPlacement.Build(current, target with { Facing = facing }, origins);
            if (BinaryPrimitives.ReadInt16LittleEndian(placed.AsSpan(GameLayout.BoundsLeftOffset)) != expectedLeft[facing]
                || placed[GameLayout.BoundsTopOffset] != expectedTop[facing])
            {
                throw new InvalidOperationException("Placement collision bounds must follow the target orientation.");
            }
        }
        Console.WriteLine("Teleport placement: exact coordinates, facing, bounds, history and preserved native state passed.");
    }

    private static void Write(byte[] bytes, int offset, short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
    }
}
