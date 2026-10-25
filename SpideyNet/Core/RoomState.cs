namespace SpideyNet.Core;

public sealed record RoomState(byte Level, Guid Owner, long Epoch, EnemyState[] Enemies, MoverState[] Movers);
