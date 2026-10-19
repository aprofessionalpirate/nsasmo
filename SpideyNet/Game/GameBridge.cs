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
    private long roomHeaderAddress;
    private byte[]? savedSlots;
    private byte ownedLevel;
    private ushort ownedCount;
    private SceneMemory? sceneMemory;
    private EnemyLoop? enemyLoop;
    private WebMemory? webMemory;
    private const int RoomSettleMs = 100;
    private byte observedLevel;
    private long roomChangedAt;
    private bool webPull;

    public GameBridge(int processId, CancellationToken token)
    {
        memory = new ProcessMemory(processId);
        input = new GameInput(processId);
        try
        {
            Locate(token);
            observedLevel = memory.Read(CurrentRoomAddress(), 1)[0];
            roomChangedAt = Environment.TickCount64 - RoomSettleMs;
        }
        catch { memory.Dispose(); throw; }
    }

    public GameFrame? Read()
    {
        ValidateMarkers();
        if (!GameProfile.Supports(memory.Read(playerAddress - GameProfile.PlayerDataOffset + GameProfile.ModeOffset, 1)[0]))
        {
            throw new IOException("SpideyNet requires VGA. Select VGA in the game's video menu.");
        }

        var level = memory.Read(CurrentRoomAddress(), 1)[0];
        var data = memory.Read(playerAddress, GameLayout.RecordSize);
        if (level != memory.Read(CurrentRoomAddress(), 1)[0])
        {
            return null;
        }
        var frame = GameLayout.Decode(data, level);
        if (frame is null || !LevelNames.Matches(level, Title))
        {
            return null;
        }

        // The DOS loader changes the room index before finishing its object tables.
        // Wait briefly after transitions before publishing or patching that scene.
        if (observedLevel != level)
        {
            observedLevel = level;
            roomChangedAt = Environment.TickCount64;
            sceneMemory?.Reset();
        }

        if (Environment.TickCount64 - roomChangedAt < RoomSettleMs)
        {
            return null;
        }

        webMemory ??= new WebMemory(memory, playerAddress - GameProfile.PlayerDataOffset, CurrentRoomAddress());
        return frame with { Web = webMemory.Read(data), WebHeld = (data[1] & 0x10) != 0 };
    }

    public string Title => Encoding.ASCII.GetString(memory.Read(
        roomHeaderAddress + GameLayout.TitleOffset, GameLayout.TitleSize)).Trim();

    public PlayerAction ReadAction() => input.Poll();

    public GameScene? ReadScene()
    {
        using var pause = memory.Pause();
        var local = Read();
        if (local is null) { return null; }
        sceneMemory ??= new SceneMemory(memory, playerAddress - GameProfile.PlayerDataOffset, playerAddress);
        return sceneMemory.Read(local, ReadCount());
    }

    public void SyncScene(SharedState state, EnemyMode mode = EnemyMode.Native, Guid? localPlayer = null)
    {
        using var pause = memory.Pause();
        var local = Read();
        if (local is null || state.Room is not null && state.Room.Level != local.Level)
        {
            ResumeEnemies();
            return;
        }
        sceneMemory ??= new SceneMemory(memory, playerAddress - GameProfile.PlayerDataOffset, playerAddress);
        if (!sceneMemory.TryApply(state, ReadCount(), local, localPlayer))
        {
            // A room reload can still be between tables; the next snapshot retries.
            ResumeEnemies();
            return;
        }
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
        if (!webPull && (authority is null || authority.Constraint == ConstraintKind.None))
        {
            return;
        }

        using var pause = memory.Pause();
        if (webPull && authority?.Constraint != ConstraintKind.WebPull)
        {
            webMemory?.Release();
            webPull = false;
        }

        if (authority is null || authority.Constraint == ConstraintKind.None) { return; }
        var local = Read();
        if (local is null || local.Level != authority.Frame.Level || !NetRules.Valid(authority.Frame)) { return; }

        // Apply relay-authorized freezing or pulling without making players solid.
        var position = new byte[3];
        BinaryPrimitives.WriteInt16LittleEndian(position, authority.Frame.X);
        position[2] = authority.Frame.Y;
        if (memory.Read(CurrentRoomAddress(), 1)[0] != local.Level)
        {
            return;
        }
        memory.Write(playerAddress + GameLayout.XOffset, position);
        if (authority.Constraint == ConstraintKind.WebPull && authority.Frame.Web is { } web)
        {
            webMemory!.Constrain(web);
            webPull = true;
        }
    }

    public void Render(IReadOnlyList<GameFrame> guests, InjectionMode mode)
    {
        if (mode == InjectionMode.Observe)
        {
            Restore();
            webMemory?.Restore();
            ReleaseWebPull();
            return;
        }
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
        if (count is 0 or > EnemyPlan.NativeCount)
        {
            throw new IOException("Enemy count does not match the known layout.");
        }

        sceneMemory ??= new SceneMemory(memory, playerAddress - GameProfile.PlayerDataOffset, playerAddress);
        sceneMemory.HideDead(count);
        if (savedSlots is null)
        {
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
        if (memory.Read(CurrentRoomAddress(), 1)[0] != ownedLevel)
        {
            Restore();
            return;
        }
        var instruction = memory.Read(RenderAddress(), RenderPatch.Size);
        var patchCode = RenderPatch.Build(EnemyPlan.RenderCount);
        if (!instruction.AsSpan().SequenceEqual(savedCode) && !instruction.AsSpan().SequenceEqual(patchCode))
        {
            throw new IOException("Renderer changed; restart DOSBox.");
        }
        memory.Write(SlotAddress(), batch);
        foreach (var patch in gapPatches) { memory.Write(patch.Address, patch.After); }
        if (!instruction.AsSpan().SequenceEqual(patchCode)) { memory.Write(RenderAddress(), patchCode); }
        webMemory!.Update(guests.Where(guest => guest.Level == local.Level).Select(guest => guest.Web).OfType<WebState>().ToArray(), local.Level);
    }

    public void Dispose()
    {
        try
        {
            Restore();
            webMemory?.Restore();
        }
        finally
        {
            try { ReleaseWebPull(); }
            finally
            {
                try { ResumeEnemies(); }
                finally { memory.Dispose(); }
            }
        }
    }

    private void ReleaseWebPull()
    {
        if (!webPull)
        {
            return;
        }

        using var pause = memory.Pause();
        webMemory?.Release();
        webPull = false;
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
        using var pause = memory.Pause();
        webMemory?.TryRestore();
        if (savedSlots is null) { return; }
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
        if (memory.Read(CurrentRoomAddress(), 1)[0] != ownedLevel
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

    // The room header starts with a room-dependent value, not its identity (for example, Mummy has 8).
    private long CurrentRoomAddress() => playerAddress - GameProfile.PlayerDataOffset + GameProfile.CurrentRoomOffset;

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
            throw new IOException("Unsupported renderer. Select VGA and use DOSBox core=normal.");
        }
    }

    private void ValidateMarkers()
    {
        if (!memory.Read(playerAddress - PlayerMarker.Length, PlayerMarker.Length).AsSpan().SequenceEqual(PlayerMarker)
            || !memory.Read(roomHeaderAddress - GameLayout.LevelOffset - LevelMarker.Length,
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
                var currentRoom = memory.Read(player - GameProfile.PlayerDataOffset + GameProfile.CurrentRoomOffset, 1)[0];
                var frame = GameLayout.Decode(memory.Read(player, GameLayout.RecordSize), currentRoom);
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
        (playerAddress, roomHeaderAddress) = candidates.Single();
    }
}
