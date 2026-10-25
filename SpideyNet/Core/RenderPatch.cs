using System.Buffers.Binary;

namespace SpideyNet.Core;

public static class RenderPatch
{
    private const byte MoveCxImmediate = 0xB9;
    private const byte Nop = 0x90;
    public const int Size = 4;

    public static byte[] Build(ushort count)
    {
        if (count is 0 or > EnemyPlan.Capacity) { throw new ArgumentOutOfRangeException(nameof(count)); }
        var bytes = new byte[Size];
        bytes[0] = MoveCxImmediate;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1), count);
        bytes[^1] = Nop;
        return bytes;
    }
}
