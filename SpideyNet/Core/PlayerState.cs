namespace SpideyNet.Core;

public sealed record PlayerState(Guid Id, string Name, GameFrame Frame, long Sequence,
    ConstraintKind Constraint = ConstraintKind.None, SuitColour Colour = SuitColour.Red, byte Sprite = PlayerSpriteRules.SpiderMan);
