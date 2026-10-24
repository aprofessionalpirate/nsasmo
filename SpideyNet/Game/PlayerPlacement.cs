using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class PlayerPlacement
{
    private static ReadOnlySpan<byte> Origins => [7, 7, 7, 0, 0, 7, 0, 0, 7, 0, 7, 7, 0, 0, 0, 7];

    internal static byte[] Build(ReadOnlySpan<byte> current, GameFrame destination, ReadOnlySpan<byte> origins)
    {
        if (current.Length != GameLayout.RecordSize || !NetRules.Valid(destination) || !origins.SequenceEqual(Origins))
        {
            throw new ArgumentException("Invalid player record or teleport destination.");
        }
        var actor = current.ToArray();
        var width = actor[29];
        var height = actor[30];
        if (width is 0 or > 21 || height is 0 or > 21)
        {
            throw new InvalidDataException("The native player collision shape is not ready for teleporting.");
        }

        // Match CS:0EDD: mirrored and transposed poses anchor their collision box differently.
        // Translating the old bounds would leave a right-facing box on a left-facing sprite.
        var left = destination.X + ((origins[destination.Facing * 2] & 1) != 0 ? 21 - width : 0);
        var top = destination.Y + ((origins[destination.Facing * 2 + 1] & 1) != 0 ? 21 - height : 0);
        BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(GameLayout.XOffset), destination.X);
        actor[GameLayout.YOffset] = destination.Y;
        BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(GameLayout.BoundsLeftOffset), checked((short)left));
        BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(GameLayout.BoundsRightOffset), checked((short)(left + width - 1)));
        actor[GameLayout.BoundsTopOffset] = unchecked((byte)top);
        actor[GameLayout.BoundsBottomOffset] = unchecked((byte)(top + height - 1));

        // Clear stale displacement history and match the mirrored sprite's origin at the target.
        BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(GameLayout.PreviousXOffset), destination.X);
        actor[GameLayout.PreviousYOffset] = destination.Y;
        actor[GameLayout.FacingOffset] = destination.Facing;
        actor[GameLayout.PreviousFaceOffset] = destination.Facing;
        return actor;
    }
}
