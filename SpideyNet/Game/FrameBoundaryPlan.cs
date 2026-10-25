using System.Buffers.Binary;

namespace SpideyNet.Game;

internal static class FrameBoundaryPlan
{
    internal const int Enter = 0x03AF;
    internal const int Exit = 0x03C8;
    internal const int Storage = 0x36A9;
    internal const int StubSize = 11;
    internal const int Flag = Storage + StubSize * 2;
    internal static ReadOnlySpan<byte> OriginalEnter => [0xE8, 0x86, 0x2E];
    internal static ReadOnlySpan<byte> OriginalExit => [0xE8, 0x25, 0x35];
    internal static ReadOnlySpan<byte> OriginalStorage => [0x8B, 0x8F, 0x01, 0x00, 0x8B, 0xC1, 0x80, 0xE1,
        0x03, 0xF6, 0x87, 0x04, 0x00, 0x02, 0x74, 0x0A, 0xB1, 0x03, 0x2A, 0xC8, 0x80, 0xE1, 0x03];

    internal static byte[] Call(int address, int target)
    {
        var bytes = new byte[] { 0xE8, 0, 0 };
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(1), checked((short)(target - address - bytes.Length)));
        return bytes;
    }

    internal static byte[] Build()
    {
        var bytes = new byte[StubSize * 2 + 1];
        Stub(Storage, 0x3238, 1).CopyTo(bytes, 0);
        Stub(Storage + StubSize, 0x38F0, 0).CopyTo(bytes, StubSize);

        // Installation can interrupt a frame already being copied. Wait for its exit first.
        bytes[^1] = 1;
        return bytes;
    }

    private static byte[] Stub(int address, int target, byte busy)
    {
        // PUSHF; MOV BYTE CS:[flag],busy; POPF; JMP original. A tail jump preserves
        // the native call's return address and every register/flag, including DF.
        var bytes = new byte[] { 0x9C, 0x2E, 0xC6, 0x06, 0, 0, busy, 0x9D, 0xE9, 0, 0 };
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), Flag);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(9), checked((short)(target - address - bytes.Length)));
        return bytes;
    }
}
