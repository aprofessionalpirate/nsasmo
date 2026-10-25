namespace SpideyNet.Core;

public sealed class SharedTrack
{
    private readonly Dictionary<int, PuzzleChange> pending = [];
    private readonly Dictionary<int, long> sent = [];
    private long serial;
    private byte[]? baseline;
    private long tick = -1;
    private Guid self;
    private byte level;
    private long epoch;
    private EnemyState[]? previous;
    private readonly Dictionary<int, EnemyState> damage = [];
    private Guid puzzleOwner;
    private SharedState? accepted;
    private long motionEpoch;
    private byte motionLevel;
    private Guid motionOwner;
    private readonly Dictionary<int, EnemyTrack> enemyTracks = [];

    public SharedReport Capture(GameScene scene, WorldState? world)
    {
        if (world is null || self != world.Self)
        {
            Reset(world?.Self ?? Guid.Empty);
        }
        serial++;
        if (baseline is not null)
        {
            var drivesScripts = world?.Shared?.PuzzleOwner == self && puzzleOwner == self
                && world.Shared.Room?.Level == scene.Frame.Level;
            var roots = scene.Events?.Select(change => change.Index).ToHashSet() ?? [];
            for (var index = 0; index < SharedRules.MaxSwitches; index++)
            {
                if (!drivesScripts && !roots.Contains(index)) { continue; }
                var value = SharedRules.Get(scene.Puzzles, index);
                if (value == SharedRules.Get(baseline, index)) { continue; }
                pending[index] = new PuzzleChange(index, value);
                sent[index] = serial;
            }
        }

        var room = world?.Shared?.Room;
        if (level != scene.Frame.Level || epoch != (room?.Epoch ?? 0))
        {
            level = scene.Frame.Level;
            epoch = 0;
            previous = null;
            damage.Clear();
        }
        if (previous is not null)
        {
            foreach (var enemy in scene.Enemies)
            {
                var before = previous.FirstOrDefault(value => value.Mover == enemy.Mover);
                if (before is null || enemy.Health >= before.Health) { continue; }
                damage[enemy.Mover] = enemy;
            }
        }

        var enemies = scene.Enemies.Select(enemy => damage.TryGetValue(enemy.Mover, out var hit)
            ? enemy with { Health = Math.Min(enemy.Health, hit.Health) } : enemy).ToArray();
        return new SharedReport(SharedRules.Normalize(scene.Puzzles), pending.Values.Take(SharedRules.MaxChanges).ToArray(), epoch,
            enemies, serial, scene.Movers);
    }

    public SharedState? Accept(GameScene scene, WorldState? world, long? nowMs = null)
    {
        if (world?.Shared is null)
        {
            return null;
        }

        var now = nowMs ?? Environment.TickCount64;
        if (world.Tick == tick)
        {
            return accepted?.Room is { } cached && cached.Owner != world.Self && cached.Level == scene.Frame.Level
                ? Sample(accepted, now) : null;
        }

        tick = world.Tick;
        var shared = world.Shared;
        puzzleOwner = shared.PuzzleOwner;
        foreach (var index in pending.Keys.ToArray())
        {
            if (shared.Acknowledged < sent[index] || SharedRules.Get(shared.Puzzles, index) != pending[index].State) { continue; }
            pending.Remove(index);
            sent.Remove(index);
        }
        var puzzles = shared.Puzzles.ToArray();
        foreach (var change in pending.Values) { SharedRules.Set(puzzles, change); }
        baseline = puzzles;

        var room = shared.Room;
        if (room is null || room.Level != scene.Frame.Level)
        {
            accepted = new SharedState(puzzles, null);
            return accepted;
        }

        epoch = room.Epoch;
        foreach (var mover in damage.Keys.ToArray())
        {
            var enemy = room.Enemies.FirstOrDefault(value => value.Mover == mover);
            if (enemy is null || enemy.Health <= damage[mover].Health) { damage.Remove(mover); }
        }
        var enemies = room.Enemies.Select(enemy => damage.TryGetValue(enemy.Mover, out var hit)
            ? enemy with { Health = Math.Min(enemy.Health, hit.Health) } : enemy).ToArray();
        previous = enemies;

        if (motionEpoch != room.Epoch || motionLevel != room.Level || motionOwner != room.Owner)
        {
            enemyTracks.Clear();
            motionEpoch = room.Epoch;
            motionLevel = room.Level;
            motionOwner = room.Owner;
        }

        accepted = new SharedState(puzzles, room with { Enemies = enemies });
        if (room.Owner == world.Self)
        {
            return accepted;
        }

        foreach (var enemy in enemies)
        {
            if (!enemyTracks.TryGetValue(enemy.Mover, out var track))
            {
                track = new EnemyTrack();
                enemyTracks.Add(enemy.Mover, track);
            }

            track.Add(enemy, now);
        }

        return Sample(accepted, now);
    }

    private SharedState Sample(SharedState state, long now)
    {
        var room = state.Room!;
        var enemies = room.Enemies.Select(enemy =>
        {
            var sampled = enemyTracks.TryGetValue(enemy.Mover, out var track) ? track.Sample(now) : enemy;
            return damage.TryGetValue(enemy.Mover, out var hit)
                ? sampled with { Health = Math.Min(sampled.Health, hit.Health) } : sampled;
        }).ToArray();
        return state with { Room = room with { Enemies = enemies } };
    }

    private void Reset(Guid identity)
    {
        self = identity;
        baseline = null;
        tick = -1;
        epoch = 0;
        previous = null;
        accepted = null;
        enemyTracks.Clear();
        motionEpoch = 0;
        motionLevel = 0;
        motionOwner = Guid.Empty;
        pending.Clear();
        sent.Clear();
        serial = 0;
        puzzleOwner = Guid.Empty;
        damage.Clear();
    }
}
