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
    private long appliedEpoch;
    private byte appliedLevel;
    private Guid appliedOwner;
    private readonly Dictionary<int, Guid> moverOwners = [];

    internal void Reset()
    {
        appliedEpoch = 0;
        appliedLevel = 0;
        appliedOwner = Guid.Empty;
        moverOwners.Clear();
        contacts.Clear();
    }

    internal void HideDead(ushort count)
    {
        foreach (var actor in Actors(count))
        {
            if (actor.Data[GameLayout.HealthOffset] == 0 || actor.Data[GameLayout.CharacterOffset] == GameLayout.HiddenCharacter)
            {
                // Stale movement can send hidden actors far outside the renderer's clip range.
                var hidden = EnemyRecord.Hide(actor.Data);
                if (!actor.Data.AsSpan().SequenceEqual(hidden))
                {
                    memory.Write(actor.Address, hidden);
                }
            }
        }
    }

    internal GameScene Read(GameFrame frame, ushort count)
    {
        Check(count);
        var actors = Actors(count);
        var enemies = actors.Select(actor => EnemyRecord.Decode(actor.Data, Mover(actor.Data))).ToArray();
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
        // Native animated tiles (for example, Mummy's fire) change their artwork independently.
        // Compare collision flags separately while preserving the exact tile hash for restoration checks.
        var tiles = memory.Read(dataAddress + TileOffset, TileBytes);
        return new SceneStatus(scene, triggers.ToArray(), Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(tiles)),
            ((VideoMode)memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0]).ToString(), movers.PixelHash(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                tiles.Select(tile => (byte)(tile & 0xC0)).ToArray())));
    }

    internal bool TryApply(SharedState state, ushort count, GameFrame frame, Guid? localPlayer, byte[]? sampledPuzzles)
    {
        Check(count);
        if (!SharedRules.Valid(state)) { throw new InvalidDataException("Invalid shared scene."); }
        var actors = Actors(count);
        var patches = new List<(long Address, byte[] Data)>();
        var preserveMotion = state.Room is { } authority && localPlayer == authority.Owner
            && appliedLevel == authority.Level && appliedEpoch == authority.Epoch && appliedOwner == authority.Owner;
        if (state.Room is { } room)
        {
            var shapes = movers.Read(count);
            if (shapes.Length != room.Movers.Length || room.Movers.Any(target =>
                shapes.Count(shape => MoverRules.SameShape(shape.State, target)) != 1)
                || room.Enemies.Any(target => actors.Count(actor => Mover(actor.Data) == target.Mover) != 1))
            {
                return false;
            }

            // An owner must keep the live paused state, not rewind to an earlier sample.
            var targets = room.Movers.Select(target => localPlayer == target.Owner
                && moverOwners.GetValueOrDefault(target.Mover) == target.Owner
                ? shapes.Single(shape => shape.State.Mover == target.Mover).State : target).ToArray();
            patches.AddRange(movers.Plan(shapes, targets, frame));
        }

        foreach (var enemy in state.Room?.Enemies ?? [])
        {
            var candidates = actors.Where(actor => Mover(actor.Data) == enemy.Mover).ToArray();
            if (candidates.Length != 1) { throw new IOException("Native enemy identity mismatch; start the same game build."); }
            var actor = candidates[0];
            var local = actor.Data;
            patches.Add((actor.Address + GameLayout.HealthOffset, [Math.Min(enemy.Health, local[GameLayout.HealthOffset])]));
            if (!preserveMotion && enemy.Health != 0)
            {
                var position = new byte[4];
                BinaryPrimitives.WriteInt16LittleEndian(position, enemy.X);
                position[2] = enemy.Y;
                position[3] = enemy.Facing;
                patches.Add((actor.Address + GameLayout.XOffset, position));
                patches.Add((actor.Address + GameLayout.CharacterOffset, [enemy.Pose]));

                // Move collision bounds with replicas; keep prior draw coordinates local.
                var dx = enemy.X - BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(GameLayout.XOffset));
                var dy = enemy.Y - local[GameLayout.YOffset];
                var bounds = local.AsSpan(GameLayout.BoundsLeftOffset, 6).ToArray();
                BinaryPrimitives.WriteInt16LittleEndian(bounds, checked((short)(BinaryPrimitives.ReadInt16LittleEndian(bounds) + dx)));
                BinaryPrimitives.WriteInt16LittleEndian(bounds.AsSpan(2), checked((short)(BinaryPrimitives.ReadInt16LittleEndian(bounds.AsSpan(2)) + dx)));
                bounds[4] = unchecked((byte)(bounds[4] + dy));
                bounds[5] = unchecked((byte)(bounds[5] + dy));
                patches.Add((actor.Address + GameLayout.BoundsLeftOffset, bounds));
            }
            if (enemy.Health != 0) { continue; }
            // Replicated death must leave inert bounds before the DOS wrap loop runs.
            patches.Add((actor.Address, EnemyRecord.Hide(local)));
            var flagsAddress = dataAddress + MoverOffset + enemy.Mover * MoverStride + DescriptorFlags;
            patches.Add((flagsAddress, [(byte)(memory.Read(flagsAddress, 1)[0] & ~ActiveFlag)]));
        }

        var puzzles = memory.Read(dataAddress + PuzzleOffset, SharedRules.PuzzleBytes);
        patches.Add((dataAddress + PuzzleOffset, sampledPuzzles is null
            ? SharedRules.Apply(puzzles, state.Puzzles)
            : SharedRules.Apply(puzzles, state.Puzzles, sampledPuzzles)));
        foreach (var patch in patches) { memory.Write(patch.Address, patch.Data); }
        if (state.Room is { } applied)
        {
            appliedLevel = applied.Level;
            appliedEpoch = applied.Epoch;
            appliedOwner = applied.Owner;
            moverOwners.Clear();
            foreach (var mover in applied.Movers)
            {
                moverOwners[mover.Mover] = mover.Owner;
            }
        }

        return true;
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
            || !ActorDrawPlan.IsSupported(memory.Read(dataAddress + GameProfile.CodeFromData
                + GameProfile.RenderOffset, ActorDrawPlan.Size))
            || !memory.Read(dataAddress + GameProfile.CodeFromData + GameProfile.RenderOffset
                + GameProfile.CountInstruction + RenderPatch.Size, GameProfile.RenderSuffix.Length)
                .AsSpan().SequenceEqual(GameProfile.RenderSuffix))
        {
            throw new IOException("Shared scenes require the verified game build in VGA mode.");
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
