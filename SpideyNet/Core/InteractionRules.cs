namespace SpideyNet.Core;

public static class InteractionRules
{
    public const int PushMs = 400;
    public const int CooldownMs = 600;
    private const int PushRange = 128;

    public static bool CanPush(GameFrame source, GameFrame target)
    {
        // Pushes work in every direction, including across the native wrapped top edge.
        var dx = target.X - source.X;
        var dy = target.ScreenY - source.ScreenY;
        return source.Level == target.Level && dx * dx + dy * dy <= PushRange * PushRange;
    }
}
