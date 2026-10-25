using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class EnemyLoop(ProcessMemory memory, long dataAddress)
{
    private const int LoopOffset = 0x370;
    private const int CountOffset = 3;
    private const int PlayerCount = 1;
    private static ReadOnlySpan<byte> Prefix => [0xBD, 0xB2, 0x92];
    private static ReadOnlySpan<byte> Suffix => [0x80, 0x0E, 0x8F, 0x92, 0xFF, 0x89, 0x0E, 0x8D, 0x92];
    private byte[]? original;

    internal void Set(EnemyMode mode)
    {
        if (mode == EnemyMode.Native && original is null) { return; }
        var address = dataAddress + GameProfile.CodeFromData + LoopOffset;
        var countAddress = address + CountOffset;
        var current = memory.Read(countAddress, RenderPatch.Size);
        var replica = RenderPatch.Build(PlayerCount);
        if (!GameProfile.Supports(memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0])
            || !memory.Read(address, Prefix.Length).AsSpan().SequenceEqual(Prefix)
            || !memory.Read(countAddress + RenderPatch.Size, Suffix.Length).AsSpan().SequenceEqual(Suffix)
            || !current.AsSpan().SequenceEqual(GameProfile.RenderCount)
                && !(original is not null && current.AsSpan().SequenceEqual(replica)))
        {
            throw new IOException("Enemy loop changed; restart DOSBox.");
        }

        // Followers run player input only; collision and puzzle loops keep the native count.
        if (mode == EnemyMode.Replica)
        {
            original ??= current;
            if (!current.AsSpan().SequenceEqual(replica)) { memory.Write(countAddress, replica); }
            return;
        }

        memory.Write(countAddress, original!);
        original = null;
    }
}
