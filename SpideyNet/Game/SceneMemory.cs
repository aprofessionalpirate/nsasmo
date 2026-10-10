using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class SceneMemory(ProcessMemory memory, long dataAddress, long playerAddress)
{
    private const int PuzzleOffset = 0x88;
    private const int MoverOffset = 0x148;
    private const int MoverStride = 3;
    private const int DescriptorFlags = 2;
    private const byte ActiveFlag = 0x80;
    private const int SwitchCode = 0x1A9A;
    private const int TriggerOffset = 0x1C8A;
    private const int TriggerCount = 0x1D0A;
    private const int TriggerStride = 8;
    private const int MaxTriggers = 16;
    private const int TileOffset = 0x1F9D;
    private const int TileBytes = 32 * 22;
    private static ReadOnlySpan<byte> SwitchSignature => [0x50, 0x52, 0x57, 0x8A, 0x44, 0x04, 0xBB, 0xD7, 0xAA, 0xD7];
    private readonly MoverMemory movers = new(memory, dataAddress, playerAddress);
    private readonly Dictionary<(byte Level, int Index), byte> contacts = [];
    private const byte ContactFlag = 0x40;

    internal GameScene Read(GameFrame frame, ushort count)
    {
        Check(count);
        var actors = Actors(count);
        var enemies = actors.Select(actor => new EnemyState(Mover(actor.Data),
            BinaryPrimitives.ReadInt16LittleEndian(actor.Data.AsSpan(GameLayout.XOffset)),
            actor.Data[GameLayout.YOffset], actor.Data[GameLayout.FacingOffset], actor.Data[GameLayout.HealthOffset],
            actor.Data[GameLayout.CharacterOffset])).ToArray();
        if (!SharedRules.Valid(enemies)) { throw new IOException("Unsupported native enemy state."); }
        var puzzles = memory.Read(dataAddress + PuzzleOffset, SharedRules.PuzzleBytes);
        return new GameScene(frame, puzzles, enemies, movers.Read(count).Select(shape => shape.State).ToArray(),
            Events(frame.Level, puzzles));
    }

    internal SceneStatus Inspect(GameScene scene)
    {
        var count = memory.Read(dataAddress + TriggerCount, 1)[0];
        if (count > MaxTriggers) { throw new IOException("Unsupported switch table."); }
        var triggers = new List<PuzzleTrigger>();
        for (var index = 0; index < count; index++)
        {
            var record = memory.Read(dataAddress + TriggerOffset + index * TriggerStride, TriggerStride);
            var identity = record[5] * SharedRules.SwitchesPerByte + record[4];
            if (identity >= SharedRules.MaxSwitches || record[4] >= SharedRules.SwitchesPerByte)
            {
                throw new IOException("Unsupported puzzle identity.");
            }
            triggers.Add(new PuzzleTrigger(identity, record[0], record[2],
                (record[3] & ActiveFlag) == 0 ? TriggerKind.Contact : TriggerKind.Web,
                Script(BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(6)))));
        }
        return new SceneStatus(scene, triggers.ToArray(), Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(memory.Read(dataAddress + TileOffset, TileBytes))),
            ((VideoMode)memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0]).ToString(), movers.PixelHash());
    }

    internal void Apply(SharedState state, ushort count, GameFrame frame)
    {
        Check(count);
        if (!SharedRules.Valid(state)) { throw new InvalidDataException("Invalid shared scene."); }
        var actors = Actors(count);
        var patches = new List<(long Address, byte[] Data)>();
        if (state.Room is not null) { patches.AddRange(movers.Plan(movers.Read(count), state.Room.Movers, frame)); }
        foreach (var enemy in state.Room?.Enemies ?? [])
        {
            var candidates = actors.Where(actor => Mover(actor.Data) == enemy.Mover).ToArray();
            if (candidates.Length != 1) { throw new IOException("Native enemy identity mismatch; start the same game build."); }
            var actor = candidates[0];
            var local = actor.Data;
            var position = new byte[4];
            BinaryPrimitives.WriteInt16LittleEndian(position, enemy.X);
            position[2] = enemy.Y;
            position[3] = enemy.Facing;
            patches.Add((actor.Address + GameLayout.XOffset, position));
            patches.Add((actor.Address + GameLayout.HealthOffset, [enemy.Health]));
            patches.Add((actor.Address + GameLayout.CharacterOffset, [enemy.Pose]));

            // Move collision bounds with the actor; keep prior draw coordinates local.
            var dx = enemy.X - BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(GameLayout.XOffset));
            var dy = enemy.Y - local[GameLayout.YOffset];
            var bounds = local.AsSpan(GameLayout.BoundsLeftOffset, 6).ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(bounds, checked((short)(BinaryPrimitives.ReadInt16LittleEndian(bounds) + dx)));
            BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(2), checked((short)(BinaryPrimitives.ReadInt16LittleEndian(bounds.AsSpan(2)) + dx)));
            bounds[4] = unchecked((byte)(bounds[4] + dy));
            bounds[5] = unchecked((byte)(bounds[5] + dy));
            patches.Add((actor.Address + GameLayout.BoundsLeftOffset, bounds));
            if (enemy.Health != 0) { continue; }
            var flagsAddress = dataAddress + MoverOffset + enemy.Mover * MoverStride + DescriptorFlags;
            patches.Add((flagsAddress, [(byte)(memory.Read(flagsAddress, 1)[0] & ~ActiveFlag)]));
            patches.Add((actor.Address + GameLayout.CharacterOffset, [GameLayout.HiddenCharacter]));
        }

        var puzzles = memory.Read(dataAddress + PuzzleOffset, SharedRules.PuzzleBytes);
        patches.Add((dataAddress + PuzzleOffset, SharedRules.Apply(puzzles, state.Puzzles)));
        foreach (var patch in patches) { memory.Write(patch.Address, patch.Data); }
    }

    private (long Address, byte[] Data)[] Actors(ushort count)
    {
        var actors = new List<(long Address, byte[] Data)>();
        for (var slot = 1; slot < count; slot++)
        {
            var address = playerAddress + slot * GameLayout.RecordSize;
            var data = memory.Read(address, GameLayout.RecordSize);
            Mover(data);
            actors.Add((address, data));
        }
        return actors.ToArray();
    }

    private static int Mover(byte[] data)
    {
        var descriptor = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(GameLayout.DescriptorOffset));
        var offset = descriptor - MoverOffset;
        if (offset < 0 || offset >= SharedRules.MaxMovers * MoverStride || offset % MoverStride != 0)
        {
            throw new IOException("Enemy descriptor lies outside the verified mover table.");
        }
        return offset / MoverStride;
    }

    private void Check(ushort count)
    {
        if (count is 0 or > EnemyPlan.NativeCount
            || !GameProfile.Supports(memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0])
            || !memory.Read(dataAddress + GameProfile.CodeFromData + SwitchCode, SwitchSignature.Length)
                .AsSpan().SequenceEqual(SwitchSignature)
            || !memory.Read(dataAddress + GameProfile.CodeFromData + GameProfile.RenderOffset, GameProfile.RenderPrefix.Length)
                .AsSpan().SequenceEqual(GameProfile.RenderPrefix)
            || !memory.Read(dataAddress + GameProfile.CodeFromData + GameProfile.RenderOffset
                + GameProfile.CountInstruction + RenderPatch.Size, GameProfile.RenderSuffix.Length)
                .AsSpan().SequenceEqual(GameProfile.RenderSuffix))
        {
            throw new IOException("Shared scenes require the verified game build in VGA or CGA mode.");
        }
    }

    private string Script(ushort offset)
    {
        const int ScriptLimit = 128;
        const int RoomBufferEnd = 0x321D;
        var bytes = new List<byte>();
        for (var command = 0; command < ScriptLimit; command++)
        {
            if (offset < TileOffset || offset >= RoomBufferEnd) { throw new IOException("Invalid room script pointer."); }
            var kind = memory.Read(dataAddress + offset, 1)[0] & 0xC0;
            var size = kind == 0 ? 1 : kind == ActiveFlag ? 4 : 2;
            if (offset + size > RoomBufferEnd) { throw new IOException("Incomplete room script."); }
            bytes.AddRange(memory.Read(dataAddress + offset, size));
            if (kind == 0) { return Convert.ToHexString(bytes.ToArray()); }
            offset += (ushort)size;
        }
        throw new IOException("Unterminated room script.");
    }

    private PuzzleChange[] Events(byte level, byte[] puzzles)
    {
        var count = memory.Read(dataAddress + TriggerCount, 1)[0];
        if (count > MaxTriggers) { throw new IOException("Unsupported switch table."); }
        var result = new List<PuzzleChange>();
        for (var index = 0; index < count; index++)
        {
            var record = memory.Read(dataAddress + TriggerOffset + index * TriggerStride, TriggerStride);
            var identity = record[5] * SharedRules.SwitchesPerByte + record[4];
            if (identity >= SharedRules.MaxSwitches || record[4] >= SharedRules.SwitchesPerByte)
            {
                throw new IOException("Unsupported puzzle identity.");
            }
            var key = (level, identity);
            var previous = contacts.GetValueOrDefault(key);
            contacts[key] = record[3];
            if ((record[3] & ContactFlag) != 0 && (previous & ContactFlag) == 0)
            {
                // Native contact/web latches distinguish input from replicated script effects.
                result.Add(new PuzzleChange(identity, SharedRules.Get(puzzles, identity)));
            }
        }
        return result.ToArray();
    }
}
