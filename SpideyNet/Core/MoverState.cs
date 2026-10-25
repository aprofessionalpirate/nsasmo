namespace SpideyNet.Core;

public sealed record MoverState(int Mover, sbyte X, sbyte Y, byte Motion, byte Left, byte Top, byte Width, byte Height,
    Guid Owner = default);
