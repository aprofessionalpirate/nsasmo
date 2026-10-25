using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class WebMemory(ProcessMemory memory, long dataAddress, long levelAddress)
{
    private const int CoordinatesOffset = 0x9658;
    private readonly TileMemory pixels = new(memory, dataAddress);
    private IReadOnlyList<PixelPatch> patches = [];
    private byte ownedLevel;

    internal WebState? Read(byte[] actor)
    {
        if ((actor[17] & 2) == 0)
        {
            return null;
        }

        var bytes = memory.Read(dataAddress + CoordinatesOffset, 8);
        var web = new WebState(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)),
            BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
        return WebRules.Valid(web) ? web : null;
    }

    internal void Constrain(WebState web)
    {
        if (!WebRules.Valid(web))
        {
            throw new InvalidDataException("Invalid web attachment geometry.");
        }

        // Bit 1 also changes the native animation script's record length.
        // Only redirect an existing shot; forcing that bit corrupts later animations.
        var actor = memory.Read(dataAddress + GameProfile.PlayerDataOffset, 18);
        if ((actor[17] & 2) == 0 || (actor[1] & 0x80) == 0)
        {
            return;
        }

        var bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, web.EndX);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), web.EndY);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), web.OriginX);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), web.OriginY);
        memory.Write(dataAddress + CoordinatesOffset, bytes);
    }

    internal void Update(IReadOnlyList<WebState> webs, byte level, IReadOnlyList<ColouredSprite>? colouredGuests = null)
    {
        TryRestore();
        ownedLevel = level;
        patches = pixels.PlanOverlay(webs, colouredGuests, level);
        foreach (var patch in patches)
        {
            memory.Write(patch.Address, patch.After);
        }
    }

    internal bool TryRestore()
    {
        // Room loading replaces the graphics; an old room's pixels no longer belong here.
        if (patches.Count > 0 && memory.Read(levelAddress, 1)[0] == ownedLevel)
        {
            foreach (var patch in pixels.RestoreOverlay(patches))
            {
                memory.Write(patch.Address, patch.Data);
            }
        }

        patches = [];
        return true;
    }

    internal void Restore()
    {
        if (patches.Count == 0)
        {
            return;
        }

        using var pause = memory.Pause();
        TryRestore();
    }
}
