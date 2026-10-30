namespace SpideyNet.Core;

public sealed record WorldState(int Version, Guid Self, long Tick, PlayerState[] Players, SharedState? Shared = null, bool ScreenRequested = false, PlayerScreen? Screen = null,
    TauntEvent[]? Taunts = null, long TauntSerial = 0);
