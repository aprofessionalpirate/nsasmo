using System.Buffers.Binary;
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
        var actor = memory.Read(address, GameLayout.RecordSize);
        var dx = destination.X - BinaryPrimitives.ReadInt16LittleEndian(actor.AsSpan(GameLayout.XOffset));
        var dy = destination.Y - actor[GameLayout.YOffset];

        // Translate the complete geometry together; retain native animation and pointers.
        foreach (var offset in new[] { GameLayout.XOffset, GameLayout.BoundsLeftOffset,
            GameLayout.BoundsRightOffset, GameLayout.PreviousXOffset })
        {
            BinaryPrimitives.WriteInt16LittleEndian(actor.AsSpan(offset),
                checked((short)(BinaryPrimitives.ReadInt16LittleEndian(actor.AsSpan(offset)) + dx)));
        }

        foreach (var offset in new[] { GameLayout.YOffset, GameLayout.BoundsTopOffset,
            GameLayout.BoundsBottomOffset, GameLayout.PreviousYOffset })
        {
            actor[offset] = unchecked((byte)(actor[offset] + dy));
        }

        memory.Write(address, actor);
    }
}
