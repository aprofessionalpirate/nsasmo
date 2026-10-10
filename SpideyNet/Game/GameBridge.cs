using System.Text;
using System.Security.Cryptography;
using System.Buffers.Binary;
using SpideyNet.Core;

namespace SpideyNet.Game;

public sealed class GameBridge : IDisposable
{
    private static readonly byte[] PlayerMarker = Encoding.ASCII.GetBytes("Mary Jane is DEAD!");
    private static readonly byte[] LevelMarker = Encoding.ASCII.GetBytes("Error reading files");
    private const int CountOffset = -38;
    private readonly List<(long Address, byte[] Before, byte[] After)> gapPatches = [];
    private byte[]? savedCode;
    private readonly ProcessMemory memory;
    private readonly GameInput input;
    private long playerAddress;
    private long levelAddress;
    private byte[]? savedSlots;
    private byte ownedLevel;
    private ushort ownedCount;
    private SceneMemory? sceneMemory;
    private EnemyLoop? enemyLoop;

    public GameBridge(int processId, CancellationToken token)
    {
        memory = new ProcessMemory(processId);
        input = new GameInput(processId);
        try { Locate(token); }
        catch { memory.Dispose(); throw; }
    }

    public GameFrame? Read()
    {
        ValidateMarkers();
        var level = memory.Read(levelAddress, 1)[0];
        var data = memory.Read(playerAddress, GameLayout.RecordSize);
        if (level != memory.Read(levelAddress, 1)[0]) { return null; }
        return GameLayout.Decode(data, level);
    }

    public string Title => Encoding.ASCII.GetString(memory.Read(
        levelAddress + GameLayout.TitleOffset, GameLayout.TitleSize)).Trim();

    public PlayerAction ReadAction() => input.Poll();

    public GameScene? ReadScene()
    {
        using var pause = memory.Pause();
        var local = Read();
        if (local is null) { return null; }
        sceneMemory ??= new SceneMemory(memory, playerAddress - GameProfile.PlayerDataOffset, playerAddress);
        return sceneMemory.Read(local, ReadCount());
    }

    public void SyncScene(SharedState state, EnemyMode mode = EnemyMode.Native)
    {
        using var pause = memory.Pause();
        var local = Read();
        if (local is null || state.Room is not null && state.Room.Level != local.Level)
        {
            ResumeEnemies();
            return;
        }
        sceneMemory ??= new SceneMemory(memory, playerAddress - GameProfile.PlayerDataOffset, playerAddress);
        sceneMemory.Apply(state, ReadCount(), local);
        enemyLoop ??= new EnemyLoop(memory, playerAddress - GameProfile.PlayerDataOffset);
        enemyLoop.Set(state.Room is null ? EnemyMode.Native : mode);
    }

    public void ResumeEnemies()
    {
        if (enemyLoop is null) { return; }
        using var pause = memory.Pause();
        enemyLoop?.Set(EnemyMode.Native);
    }

    public SceneStatus? InspectScene()
    {
        using var pause = memory.Pause();
        var scene = ReadScene();
        return scene is null ? null : sceneMemory!.Inspect(scene);
    }

    public GameStatus Inspect()
    {
        return new GameStatus(Read(), Title, ReadCount(),
            Convert.ToHexString(SHA256.HashData(memory.Read(SlotAddress(), EnemyPlan.ReservedSize))),
            Convert.ToHexString(memory.Read(playerAddress, GameLayout.RecordSize)),
            Convert.ToHexString(memory.Read(playerAddress + GameLayout.RecordSize, EnemyPlan.MaxGuests * EnemyPlan.Stride)),
            Convert.ToHexString(memory.Read(RenderAddress(), RenderPatch.Size)));
    }

    public void Constrain(PlayerState? authority)
    {
        if (authority is null || authority.Constraint == ConstraintKind.None) { return; }
        using var pause = memory.Pause();
        var local = Read();
        if (local is null || local.Level != authority.Frame.Level || !NetRules.Valid(authority.Frame)) { return; }

        // Experimental PvP changes only local X/Y, as described in the wiki.
        var position = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(position, authority.Frame.X);
        position[2] = authority.Frame.Y;
        if (memory.Read(levelAddress, 1)[0] != local.Level) { return; }
        memory.Write(playerAddress + GameLayout.XOffset, position);
    }

    public void Render(IReadOnlyList<GameFrame> guests, InjectionMode mode)
    {
        if (mode == InjectionMode.Observe) { Restore(); return; }
        using var pause = memory.Pause();
        if (guests.Count > EnemyPlan.MaxGuests)
        {
            throw new InvalidDataException("Too many players for the reserved slots.");
        }

        var local = Read();
        if (local is null) { Restore(); return; }
        if (savedSlots is not null && local.Level != ownedLevel)
        {
            // A room reload replaces the table; old backups no longer belong here.
            Restore();
            return;
        }
        var count = ReadCount();
        if (savedSlots is not null && count != ownedCount)
        {
            Restore();
            throw new IOException("Enemy table changed; injection stopped.");
        }
        if (!guests.Any(guest => guest.Level == local.Level)) { Restore(); return; }
        if (savedSlots is null)
        {
            if (count is 0 or > EnemyPlan.NativeCount)
            {
                throw new IOException("Enemy count does not match the known layout.");
            }
            CheckRenderer();
            savedSlots = memory.Read(SlotAddress(), EnemyPlan.ReservedSize);
            savedCode = memory.Read(RenderAddress(), RenderPatch.Size);
            ownedCount = count;
            ownedLevel = local.Level;
            SaveGaps(count);
        }

        var player = memory.Read(playerAddress, GameLayout.RecordSize);
        var batch = savedSlots.ToArray();
        var descriptorStart = (EnemyPlan.DescriptorSlot - EnemyPlan.FirstSlot) * EnemyPlan.Stride;
        batch.AsSpan(descriptorStart, EnemyPlan.MaxGuests * EnemyPlan.DescriptorSize).Clear();
        for (var index = 0; index < EnemyPlan.MaxGuests; index++)
        {
            var remote = index < guests.Count && guests[index].Level == local.Level ? guests[index] : null;
            EnemyPlan.Build(player, remote, Descriptor(index)).CopyTo(batch, index * EnemyPlan.Stride);
        }

        // Extend drawing only; physics and scripts keep their original actor count.
        ValidateMarkers();
        if (memory.Read(levelAddress, 1)[0] != ownedLevel) { Restore(); return; }
        var instruction = memory.Read(RenderAddress(), RenderPatch.Size);
        var patchCode = RenderPatch.Build(EnemyPlan.RenderCount);
        if (!instruction.AsSpan().SequenceEqual(savedCode) && !instruction.AsSpan().SequenceEqual(patchCode))
        {
            throw new IOException("Renderer changed; restart DOSBox.");
        }
        memory.Write(SlotAddress(), batch);
        foreach (var patch in gapPatches) { memory.Write(patch.Address, patch.After); }
        if (!instruction.AsSpan().SequenceEqual(patchCode)) { memory.Write(RenderAddress(), patchCode); }
    }

    public void Dispose()
    {
        try { Restore(); }
        finally
        {
            try { ResumeEnemies(); }
            finally { memory.Dispose(); }
        }
    }

    private long SlotAddress() => playerAddress + EnemyPlan.FirstSlot * EnemyPlan.Stride;

    private ushort ReadCount() => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(playerAddress + CountOffset, sizeof(ushort)));

    private static ushort Descriptor(int index) => checked((ushort)(GameProfile.PlayerDataOffset
        + EnemyPlan.DescriptorSlot * EnemyPlan.Stride + index * EnemyPlan.DescriptorSize));

    private void SaveGaps(ushort count)
    {
        for (var index = count; index < EnemyPlan.FirstSlot; index++)
        {
            // Hide gaps without overwriting the web state embedded in these actors.
            var start = playerAddress + index * EnemyPlan.Stride;
            SavePatch(start + GameLayout.CharacterOffset, [GameLayout.HiddenCharacter]);
        }
    }

    private void SavePatch(long address, byte[] value)
    {
        gapPatches.Add((address, memory.Read(address, value.Length), value));
    }

    private void Restore()
    {
        if (savedSlots is null) { return; }
        using var pause = memory.Pause();
        ValidateMarkers();
        if (savedCode is not null)
        {
            var current = memory.Read(RenderAddress(), RenderPatch.Size);
            if (!current.AsSpan().SequenceEqual(RenderPatch.Build(EnemyPlan.RenderCount))
                && !current.AsSpan().SequenceEqual(savedCode))
            {
                throw new IOException("Renderer patch changed; restart DOSBox.");
            }
            // Restore code even after a room change; old actor backups are discarded.
            memory.Write(RenderAddress(), savedCode);
            savedCode = null;
        }
        if (memory.Read(levelAddress, 1)[0] != ownedLevel
            || ReadCount() != ownedCount)
        {
            Abandon();
            return;
        }

        // The original draw loop no longer visits reserved storage.
        foreach (var patch in gapPatches)
        {
            if (memory.Read(patch.Address, patch.After.Length).AsSpan().SequenceEqual(patch.After))
            {
                memory.Write(patch.Address, patch.Before);
            }
        }
        memory.Write(SlotAddress(), savedSlots);
        Abandon();
    }

    private void Abandon() { savedSlots = null; savedCode = null; ownedCount = 0; gapPatches.Clear(); }

    private long RenderAddress() => playerAddress - GameProfile.PlayerDataOffset + GameProfile.CodeFromData
        + GameProfile.RenderOffset + GameProfile.CountInstruction;

    private void CheckRenderer()
    {
        var address = RenderAddress() - GameProfile.CountInstruction;
        if (!GameProfile.Supports(memory.Read(playerAddress - GameProfile.PlayerDataOffset + GameProfile.ModeOffset, 1)[0])
            || !memory.Read(address, GameProfile.RenderPrefix.Length).AsSpan().SequenceEqual(GameProfile.RenderPrefix)
            || !memory.Read(RenderAddress(), RenderPatch.Size).AsSpan().SequenceEqual(GameProfile.RenderCount)
            || !memory.Read(RenderAddress() + RenderPatch.Size, GameProfile.RenderSuffix.Length)
                .AsSpan().SequenceEqual(GameProfile.RenderSuffix))
        {
            throw new IOException("Unsupported renderer. Select VGA or CGA and use DOSBox core=normal.");
        }
    }

    private void ValidateMarkers()
    {
        if (!memory.Read(playerAddress - PlayerMarker.Length, PlayerMarker.Length).AsSpan().SequenceEqual(PlayerMarker)
            || !memory.Read(levelAddress - GameLayout.LevelOffset - LevelMarker.Length,
                LevelMarker.Length).AsSpan().SequenceEqual(LevelMarker))
        {
            throw new IOException("Game memory moved; detach and reattach.");
        }
    }

    private void Locate(CancellationToken token)
    {
        var candidates = new HashSet<(long Player, long Level)>();
        foreach (var chunk in memory.Scan(token))
        {
            var playerIndex = chunk.Data.AsSpan().IndexOf(PlayerMarker);
            var levelIndex = chunk.Data.AsSpan().IndexOf(LevelMarker);
            if (playerIndex < 0 || levelIndex < 0) { continue; }
            var player = chunk.Address + playerIndex + PlayerMarker.Length;
            var level = chunk.Address + levelIndex + LevelMarker.Length + GameLayout.LevelOffset;
            try
            {
                var frame = GameLayout.Decode(memory.Read(player, GameLayout.RecordSize), memory.Read(level, 1)[0]);
                var title = Encoding.ASCII.GetString(memory.Read(level + GameLayout.TitleOffset, GameLayout.TitleSize)).Trim();
                if (frame is null || !NetRules.Valid(frame) || !LevelNames.Matches(frame.Level, title)) { continue; }
                candidates.Add((player, level));
            }
            catch (IOException) { }
        }
        if (candidates.Count != 1)
        {
            throw new IOException($"Found {candidates.Count} valid layouts. Start a game in DOSBox, then attach.");
        }
        (playerAddress, levelAddress) = candidates.Single();
    }
}
