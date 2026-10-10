namespace SpideyNet.Core;

public sealed record SharedState(byte[] Puzzles, RoomState? Room, long Acknowledged = 0, Guid PuzzleOwner = default);
