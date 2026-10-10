using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class MoverMemory(ProcessMemory memory, long dataAddress, long playerAddress)
{
    private const int ObjectCount = 0x1C83;
    private const int ObjectPointers = 0x1B31;
    private const int ObjectStart = 0x1B41;
    private const int ObjectEnd = 0x1C81;
    private const int HeaderSize = 7;
    private const byte ActorFlag = 0x80;
    private const int MoverOffset = 0x148;
    private const int MoverStride = 3;
    private const int TileOffset = 0x1F9D;
    private readonly TileMemory pixels = new(memory, dataAddress);

    internal string PixelHash() => pixels.Hash();

    internal MoverShape[] Read(ushort actors)
    {
        var count = memory.Read(dataAddress + ObjectCount, 1)[0] + actors - 1;
        if (count > MoverRules.MaxObjects) { throw new IOException("Unsupported moving object count."); }
        var result = new List<MoverShape>();
        for (var index = 0; index < count; index++)
        {
            var offset = BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(dataAddress + ObjectPointers + index * sizeof(ushort), sizeof(ushort)));
            if (offset < ObjectStart || offset + HeaderSize > ObjectEnd) { throw new IOException("Invalid native object pointer."); }
            var header = memory.Read(dataAddress + offset, HeaderSize);
            if ((header[0] & ActorFlag) != 0) { continue; }
            var descriptor = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(3));
            var mover = descriptor - MoverOffset;
            if (mover < 0 || mover >= SharedRules.MaxMovers * MoverStride || mover % MoverStride != 0)
            {
                throw new IOException("Invalid moving object descriptor.");
            }
            var data = memory.Read(dataAddress + descriptor, MoverStride);
            var state = new MoverState(mover / MoverStride, unchecked((sbyte)data[1]), unchecked((sbyte)data[0]),
                (byte)(data[2] & ~ActorFlag), header[1], header[2], header[5], header[6]);
            var size = state.Width * state.Height;
            if (!MoverRules.Valid([state]) || offset + HeaderSize + size > ObjectEnd)
            {
                throw new IOException("Unsupported moving object shape.");
            }
            result.Add(new MoverShape(state, memory.Read(dataAddress + offset + HeaderSize, size)));
        }
        return result.ToArray();
    }

    internal IReadOnlyList<(long Address, byte[] Data)> Plan(MoverShape[] local, MoverState[] shared, GameFrame frame)
    {
        if (!MoverRules.Valid(shared) || local.Length != shared.Length) { throw new IOException("Moving object count changed."); }
        var patches = new List<(long Address, byte[] Data)>();
        var shapes = new List<MoverShape>();
        var carryX = 0;
        var carryY = 0;
        foreach (var target in shared)
        {
            var matches = local.Where(shape => MoverRules.SameShape(shape.State, target)).ToArray();
            if (matches.Length != 1) { throw new IOException("Moving object identity mismatch."); }
            var source = matches[0];
            shapes.Add(source with { State = target });
            if (source.State.X != target.X || source.State.Y != target.Y || source.State.Motion != target.Motion)
            {
                patches.Add((dataAddress + MoverOffset + target.Mover * MoverStride,
                    [unchecked((byte)target.Y), unchecked((byte)target.X), target.Motion]));
            }
            if (!MoverRules.Riding(frame, source.State)) { continue; }
            carryX = (target.X - source.State.X) * MoverRules.TileSize;
            carryY = (target.Y - source.State.Y) * MoverRules.TileSize;
        }
        var before = memory.Read(dataAddress + TileOffset, MoverPlan.TileBytes);
        var after = MoverPlan.Build(before, local, shapes);
        patches.AddRange(pixels.Plan(before, after));
        if (!before.AsSpan().SequenceEqual(after)) { patches.Add((dataAddress + TileOffset, after)); }
        if (carryX == 0 && carryY == 0) { return patches; }

        // Carry a local rider with the corrected platform, preserving local pointers.
        var player = memory.Read(playerAddress, GameLayout.RecordSize);
        var position = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(position, checked((short)(frame.X + carryX)));
        position[2] = unchecked((byte)(frame.Y + carryY));
        patches.Add((playerAddress + GameLayout.XOffset, position));
        var bounds = player.AsSpan(GameLayout.BoundsLeftOffset, 6).ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bounds, checked((short)(BinaryPrimitives.ReadInt16LittleEndian(bounds) + carryX)));
        BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(2), checked((short)(BinaryPrimitives.ReadInt16LittleEndian(bounds.AsSpan(2)) + carryX)));
        bounds[4] = unchecked((byte)(bounds[4] + carryY));
        bounds[5] = unchecked((byte)(bounds[5] + carryY));
        patches.Add((playerAddress + GameLayout.BoundsLeftOffset, bounds));
        return patches;
    }
}
