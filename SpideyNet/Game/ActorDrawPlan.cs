using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class ActorDrawPlan
{
    internal const int Size = 7;

    internal static bool IsSupported(ReadOnlySpan<byte> code)
    {
        return code.Length == Size && (code.SequenceEqual(Build(false)) || code.SequenceEqual(Build(true))
            || code[..3].SequenceEqual(GameProfile.RenderPrefix) && code[3..].SequenceEqual(GameProfile.RenderCount));
    }

    internal static byte[] Build(bool skipLocal)
    {
        var bytes = new byte[Size];
        GameProfile.RenderPrefix.CopyTo(bytes);

        // Start at actor one when the compositor draws actor zero. Keep the final slot
        // unchanged: advancing the start also reduces the draw count by one.
        if (skipLocal)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1),
                GameProfile.PlayerDataOffset + 1 + EnemyPlan.Stride);
        }
        RenderPatch.Build((ushort)(EnemyPlan.RenderCount - (skipLocal ? 1 : 0))).CopyTo(bytes, 3);
        return bytes;
    }
}
