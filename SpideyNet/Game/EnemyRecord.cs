using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class EnemyRecord
{
    internal static byte[] Hide(byte[] actor)
    {
        var result = actor.ToArray();
        result[1] = 0;
        result[25] = 0;
        result[26] = 0;
        result[GameLayout.HealthOffset] = 0;
        result[GameLayout.CharacterOffset] = GameLayout.HiddenCharacter;

        // Keep bounds inside the room: native wrapping still runs for inactive actors.
        // Off-screen bounds repeatedly wrap X/Y without updating those same bounds.
        foreach (var offset in new[] { GameLayout.XOffset, GameLayout.BoundsLeftOffset,
            GameLayout.BoundsRightOffset, GameLayout.PreviousXOffset })
        {
            BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(offset), 0);
        }

        // Only the drawn position is off-screen; Y=200 is rejected by the VGA renderer.
        result[GameLayout.YOffset] = 200;
        result[GameLayout.BoundsTopOffset] = 0;
        result[GameLayout.BoundsBottomOffset] = 0;
        result[GameLayout.PreviousYOffset] = 200;
        result[GameLayout.PreviousFaceOffset] = 0;
        return result;
    }

    internal static byte[] Place(byte[] actor, EnemyState enemy, ReadOnlySpan<byte> dimensions, ReadOnlySpan<byte> origins)
    {
        var result = actor.ToArray();
        var width = dimensions[0];
        var height = dimensions[1];
        if ((enemy.Facing & 1) != 0)
        {
            (width, height) = (height, width);
        }

        // Hidden startup frames retain the last native shape; they have no sprite dimensions.
        if (enemy.Pose == GameLayout.HiddenCharacter)
        {
            width = actor[29];
            height = actor[30];
        }

        // Match CS:08DC and CS:0EED instead of translating stale bounds across a turn.
        // A skeleton at X=-5 faces right with its collision box still inside the room at X=8.
        var left = enemy.X + ((origins[enemy.Facing * 2] & 1) != 0 ? 21 - width : 0);
        var top = enemy.Y + ((origins[enemy.Facing * 2 + 1] & 1) != 0 ? 21 - height : 0);
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(GameLayout.XOffset), enemy.X);
        result[GameLayout.YOffset] = enemy.Y;
        result[GameLayout.FacingOffset] = enemy.Facing;
        result[GameLayout.CharacterOffset] = enemy.Pose;
        result[29] = width;
        result[30] = height;
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(GameLayout.BoundsLeftOffset), checked((short)left));
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(GameLayout.BoundsRightOffset), checked((short)(left + width - 1)));
        result[GameLayout.BoundsTopOffset] = unchecked((byte)top);
        result[GameLayout.BoundsBottomOffset] = unchecked((byte)(top + height - 1));
        return result;
    }

    internal static EnemyState Decode(byte[] actor, int mover)
    {
        // Dead native actors can retain or accumulate unbounded off-screen positions.
        // Their identity and death matter; invisible coordinates must not stop a session.
        if (actor[GameLayout.HealthOffset] == 0)
        {
            return new EnemyState(mover, 0, 0, 0, 0, GameLayout.HiddenCharacter);
        }

        // A live rat/mummy can briefly use the hidden pose while its native animation starts.
        // Preserve its health so a temporary hide cannot become a permanent shared death.
        return new EnemyState(mover, BinaryPrimitives.ReadInt16LittleEndian(actor.AsSpan(GameLayout.XOffset)),
            actor[GameLayout.YOffset], actor[GameLayout.FacingOffset], actor[GameLayout.HealthOffset], actor[GameLayout.CharacterOffset]);
    }
}
