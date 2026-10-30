namespace SpideyNet.Core;

public sealed record TauntEvent(long Sequence, Guid Player, string Name, TauntId Clip);
