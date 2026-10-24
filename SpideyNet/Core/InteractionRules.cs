namespace SpideyNet.Core;

public static class InteractionRules
{
    public const int FreezeMs = 1200;
    public const int CooldownMs = 600;
    private const int WebRange = 64;
    private const int WebHeight = 12;

    public static bool WebHits(GameFrame source, GameFrame target)
    {
        if (source.Level != target.Level || Math.Abs(source.ScreenY - target.ScreenY) > WebHeight)
        {
            return false;
        }
        var delta = target.X - source.X;
        return source.Facing switch
        {
            0 => delta is > 0 and <= WebRange,
            2 => delta is < 0 and >= -WebRange,
            _ => false
        };
    }
}
