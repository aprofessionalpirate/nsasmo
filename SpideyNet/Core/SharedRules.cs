namespace SpideyNet.Core;

public static class SharedRules
{
    public const int PuzzleBytes = 192;
    public const int SwitchesPerByte = 4;
    public const int MaxSwitches = PuzzleBytes * SwitchesPerByte;
    public const byte StateMask = 0x55;
    public const int MaxMovers = 256;
    public const int MaxEnemies = EnemyPlan.NativeCount - 1;
    public const int MaxChanges = 64;
    public const int EnemyFrameCount = 0x60;

    public static byte[] Normalize(ReadOnlySpan<byte> puzzles)
    {
        if (puzzles.Length != PuzzleBytes) { throw new ArgumentException("Incomplete puzzle state."); }
        return puzzles.ToArray().Select(value => (byte)(value & StateMask)).ToArray();
    }

    public static SwitchState Get(ReadOnlySpan<byte> puzzles, int index)
    {
        return (puzzles[index / SwitchesPerByte] & Mask(index)) == 0 ? SwitchState.Off : SwitchState.On;
    }

    public static void Set(Span<byte> puzzles, PuzzleChange change)
    {
        var index = change.Index / SwitchesPerByte;
        var mask = Mask(change.Index);
        puzzles[index] = (byte)((puzzles[index] & ~mask) | (change.State == SwitchState.On ? mask : 0));
    }

    public static byte[] Apply(ReadOnlySpan<byte> local, ReadOnlySpan<byte> shared)
    {
        if (local.Length != PuzzleBytes || shared.Length != PuzzleBytes)
        {
            throw new ArgumentException("Incomplete puzzle state.");
        }

        // Dirty bits ask the native scripts to update doors and redraw tiles.
        var result = local.ToArray();
        for (var index = 0; index < PuzzleBytes; index++)
        {
            var changed = (local[index] ^ shared[index]) & StateMask;
            result[index] = (byte)((local[index] & ~StateMask) | (shared[index] & StateMask) | (changed << 1));
        }
        return result;
    }

    public static byte[] Apply(ReadOnlySpan<byte> local, ReadOnlySpan<byte> shared, ReadOnlySpan<byte> sampled)
    {
        if (local.Length != PuzzleBytes || shared.Length != PuzzleBytes || sampled.Length != PuzzleBytes)
        {
            throw new ArgumentException("Incomplete puzzle state.");
        }

        // A native switch can change after sampling and before applying the network snapshot.
        // Keep those newer bits (including On -> Off) so the next capture can publish them.
        var merged = shared.ToArray();
        for (var index = 0; index < PuzzleBytes; index++)
        {
            var changed = (local[index] ^ sampled[index]) & StateMask;
            merged[index] = (byte)((merged[index] & ~changed) | (local[index] & changed));
        }

        return Apply(local, merged);
    }

    public static bool Valid(byte[]? puzzles) => puzzles?.Length == PuzzleBytes
        && puzzles.All(value => (value & ~StateMask) == 0);

    public static bool Valid(EnemyState[]? enemies) => enemies is not null && enemies.Length <= MaxEnemies
        && enemies.All(enemy => enemy is not null && enemy.Mover is >= 0 and < MaxMovers
            && enemy.X is >= -256 and <= 512 && enemy.Facing <= GameLayout.FacingMask && enemy.Pose < EnemyFrameCount)
        && enemies.Select(enemy => enemy.Mover).Distinct().Count() == enemies.Length;

    public static bool Valid(SharedReport report) => Valid(report.Seed) && Valid(report.Enemies)
        && MoverRules.Valid(report.Movers ?? [])
        && report.Epoch >= 0 && report.Serial >= 0 && report.Changes is not null && report.Changes.Length <= MaxChanges
        && report.Changes.All(change => change is not null && change.Index is >= 0 and < MaxSwitches
            && Enum.IsDefined(change.State))
        && report.Changes.Select(change => change.Index).Distinct().Count() == report.Changes.Length;

    public static bool Valid(SharedState state) => Valid(state.Puzzles) && state.Acknowledged >= 0
        && (state.Room is null || state.Room.Level is > 0 and <= GameLayout.MaxLevel
            && state.Room.Epoch > 0 && Valid(state.Room.Enemies) && MoverRules.Valid(state.Room.Movers));

    private static byte Mask(int index) => checked((byte)(1 << (index % SwitchesPerByte * 2)));
}
