namespace SpideyNet.Core;

public sealed record SharedReport(byte[] Seed, PuzzleChange[] Changes, long Epoch, EnemyState[] Enemies, long Serial = 0,
    MoverState[]? Movers = null);
