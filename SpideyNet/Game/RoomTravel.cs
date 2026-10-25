using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class RoomTravel(ProcessMemory memory, long dataAddress)
{
    private const int ResetFlagInstruction = 0x2043;
    private static ReadOnlySpan<byte> ResetFlag => [0xA2, 0x0E, 0x1D];
    private static ReadOnlySpan<byte> KeepFlag => [0x90, 0x90, 0x90];
    private bool ownsPatch;

    internal bool Ready => !ownsPatch || memory.Read(dataAddress + 0x1D0E, 1)[0] == 0;

    internal void Restore()
    {
        if (!ownsPatch)
        {
            return;
        }

        var address = dataAddress + GameProfile.CodeFromData + ResetFlagInstruction;
        if (!memory.Read(address, 3).AsSpan().SequenceEqual(KeepFlag))
        {
            throw new IOException("The room-loading instruction changed; restart DOSBox.");
        }

        memory.Write(address, ResetFlag.ToArray());
        ownsPatch = false;
    }

    internal void Request(byte room)
    {
        var address = dataAddress + GameProfile.CodeFromData + ResetFlagInstruction;
        if (!memory.Read(address, 3).AsSpan().SequenceEqual(ResetFlag))
        {
            throw new IOException("Unsupported room-loading instruction.");
        }

        // CS:2043 clears the request before physics each frame. Preserve it until
        // CS:045B consumes it; the loader clears it itself at CS:032D.
        memory.Write(address, KeepFlag.ToArray());
        ownsPatch = true;
        memory.Write(dataAddress + GameProfile.CurrentRoomOffset, [room]);
        memory.Write(dataAddress + 0x1D0E, [0xFF]);
    }

    internal void Place(GameFrame destination)
    {
        var address = dataAddress + GameProfile.PlayerDataOffset;
        memory.Write(address, PlayerPlacement.Build(memory.Read(address, GameLayout.RecordSize), destination,
            memory.Read(dataAddress + 0xA9AD, 16)));
    }
}
