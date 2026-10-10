namespace SpideyNet.Core;

public static class InteractionRules
{
    public const int FreezeMs = 1200;
    public const int CooldownMs = 600;
    private const int BodyWidth = 12;
    private const int BodyHeight = 16;
    private const int WebRange = 64;
    private const int WebHeight = 12;

    public static bool Collides(GameFrame start, GameFrame next, GameFrame other)
    {
        if (next.Level != other.Level || start.Level != next.Level) { return false; }
        if (Math.Abs(start.X - other.X) < BodyWidth && Math.Abs(start.Y - other.Y) < BodyHeight) { return false; }

        // Sweep horizontal movement so fast updates cannot pass through a player.
        return other.X > Math.Min(start.X, next.X) - BodyWidth
            && other.X < Math.Max(start.X, next.X) + BodyWidth
            && Math.Abs(next.Y - other.Y) < BodyHeight;
    }

    public static bool WebHits(GameFrame source, GameFrame target)
    {
        if (source.Level != target.Level || Math.Abs(source.Y - target.Y) > WebHeight) { return false; }
        var delta = target.X - source.X;
        return source.Facing switch
        {
            0 => delta is > 0 and <= WebRange,
            2 => delta is < 0 and >= -WebRange,
            _ => false
        };
    }
}
