namespace SpideyNet.Core;

public sealed record ClientUpdate(int Version, long Sequence, GameFrame Frame, PlayerAction Action = PlayerAction.Move,
    SharedReport? Shared = null, Guid? Watching = null, ScreenFrame? Screen = null);
