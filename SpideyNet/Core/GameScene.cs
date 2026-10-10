namespace SpideyNet.Core;

public sealed record GameScene(GameFrame Frame, byte[] Puzzles, EnemyState[] Enemies, MoverState[] Movers,
    PuzzleChange[]? Events = null);
