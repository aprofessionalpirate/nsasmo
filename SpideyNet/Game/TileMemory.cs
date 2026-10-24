using System.Buffers.Binary;
using System.Security.Cryptography;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class TileMemory(ProcessMemory memory, long dataAddress)
{
    private const int DataInstruction = 0x108;
    private const int BufferPointer = 0x98CA;
    private const int SolidPointer = 0x98D6;
    private const int BackPointer = 0x98DA;
    private const int BackTiles = 0x235D;
    private const int SpritePointer = 0x98DE;
    private const int SpriteBankFrames = 0x60;
    private const int RealModeLimit = 0x100000;
    private const byte CharacterMask = 0x3F;

    internal IReadOnlyList<(long Address, byte[] Data)> Plan(byte[] before, byte[] after)
    {
        if (before.AsSpan().SequenceEqual(after)) { return []; }
        var raster = TileRaster.For((VideoMode)memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0]);
        var ram = Ram();
        var buffer = Far(BufferPointer, ram, raster.BufferBytes);
        var solid = Far(SolidPointer, ram, (CharacterMask + 1) * raster.TileBytes);
        var background = Far(BackPointer, ram, (byte.MaxValue + 1) * raster.TileBytes);
        var backs = memory.Read(dataAddress + BackTiles, MoverPlan.TileBytes);
        var saved = memory.Read(buffer, raster.BufferBytes);
        var painted = saved.ToArray();
        var sprites = new Dictionary<long, byte[]>();
        for (var index = 0; index < after.Length; index++)
        {
            if (before[index] == after[index]) { continue; }
            var character = after[index] & CharacterMask;
            var source = character == 0 ? background + backs[index] * raster.TileBytes : solid + character * raster.TileBytes;
            if (!sprites.TryGetValue(source, out var pixels))
            {
                pixels = memory.Read(source, raster.TileBytes);
                sprites.Add(source, pixels);
            }
            raster.Paint(index, pixels, painted);
        }

        // Write changed rows only; the game copies each plane to its own VGA bank.
        var patches = new List<(long Address, byte[] Data)>();
        for (var offset = 0; offset < painted.Length; offset += raster.RowBytes)
        {
            var row = painted.AsSpan(offset, raster.RowBytes);
            if (row.SequenceEqual(saved.AsSpan(offset, raster.RowBytes))) { continue; }
            patches.Add((buffer + offset, row.ToArray()));
        }
        return patches;
    }

    internal string Hash()
    {
        var raster = TileRaster.For((VideoMode)memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0]);
        var buffer = Far(BufferPointer, Ram(), raster.BufferBytes);
        return Convert.ToHexString(SHA256.HashData(memory.Read(buffer, raster.BufferBytes)));
    }

    internal IReadOnlyList<PixelPatch> PlanOverlay(IReadOnlyList<WebState> webs, IReadOnlyList<ColouredSprite>? guests, byte level)
    {
        if (webs.Count == 0 && (guests is null || guests.Count == 0))
        {
            return [];
        }

        var raster = TileRaster.For((VideoMode)memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0]);
        var ram = Ram();
        var address = Far(BufferPointer, ram, raster.BufferBytes);
        var before = memory.Read(address, raster.BufferBytes);
        var after = before.ToArray();
        if (guests is not null)
        {
            // Read native assets only. Alternate facing uses the game's pre-transposed bank.
            var sprites = Far(SpritePointer, ram, (SpriteBankFrames + GameLayout.PlayerCharacterLimit) * SpriteRaster.FrameBytes);
            for (var index = 0; index < guests.Count; index++)
            {
                var guest = guests[index].Frame;
                if (guest.Level != level)
                {
                    continue;
                }

                var frame = guest.Pose + ((guest.Facing & 1) != 0 ? SpriteBankFrames : 0);
                var pixels = memory.Read(sprites + frame * SpriteRaster.FrameBytes, SpriteRaster.FrameBytes);
                SpriteRaster.Paint(guest, guests[index].Colour, pixels, after);
            }
        }

        foreach (var web in webs)
        {
            raster.PaintWeb(web, after);
        }

        // Batch one changed span per plane, rather than issuing a cross-process write per pixel.
        var patches = new List<PixelPatch>();
        for (var plane = 0; plane < raster.Planes; plane++)
        {
            var start = plane * raster.PlaneBytes;
            var end = start + raster.PlaneBytes;
            while (start < end && before[start] == after[start])
            {
                start++;
            }
            while (end > start && before[end - 1] == after[end - 1])
            {
                end--;
            }

            if (start < end)
            {
                patches.Add(new PixelPatch(address + start, before[start..end], after[start..end]));
            }
        }

        return patches;
    }

    private long Ram()
    {
        var instruction = memory.Read(dataAddress + GameProfile.CodeFromData + DataInstruction, 5);
        if (instruction[0] != 0xB8 || instruction[3] != 0x8E || instruction[4] != 0xD8)
        {
            throw new IOException("Unsupported DOS segment instruction.");
        }
        var segment = BinaryPrimitives.ReadUInt16LittleEndian(instruction.AsSpan(1));
        return dataAddress - segment * 16L;
    }

    private long Far(int offset, long ram, int length)
    {
        var pointer = memory.Read(dataAddress + offset, 4);
        var relative = BinaryPrimitives.ReadUInt16LittleEndian(pointer)
            + BinaryPrimitives.ReadUInt16LittleEndian(pointer.AsSpan(2)) * 16;
        if (relative <= 0 || relative + length > RealModeLimit)
        {
            throw new IOException("Graphics buffer lies outside DOS memory.");
        }
        return ram + relative;
    }
}
