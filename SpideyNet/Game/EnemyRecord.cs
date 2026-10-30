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
