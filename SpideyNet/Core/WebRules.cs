namespace SpideyNet.Core;

public static class WebRules
{
    private const double PullSpeed = 96;
    private const double StopDistance = 12;

    public static bool Hits(GameFrame source, GameFrame target)
    {
        if (!source.WebHeld || source.Web is not { } web || source.Level != target.Level)
        {
            return false;
        }

        // Check the visible shot segment against the target body, without making players solid.
        var dx = web.EndX - web.OriginX;
        var dy = web.EndY - web.OriginY;
        var length = dx * dx + dy * dy;
        var x = target.X + 7 - web.OriginX;
        var y = target.Y + 7 - web.OriginY;
        var along = length == 0 ? 0 : Math.Clamp((double)(x * dx + y * dy) / length, 0, 1);
        return Math.Pow(x - along * dx, 2) + Math.Pow(y - along * dy, 2) <= 100;
    }

    public static GameFrame Pull(GameFrame source, GameFrame target, double elapsedSeconds)
    {
        var dx = target.X - source.X;
        var dy = target.Y - source.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var step = Math.Min(PullSpeed * Math.Clamp(elapsedSeconds, 0, 0.1), Math.Max(0, distance - StopDistance));
        var amount = distance == 0 ? 0 : step / distance;
        var x = (short)Math.Round(source.X + dx * amount);
        var y = (byte)Math.Clamp(Math.Round(source.Y + dy * amount), 0, GameLayout.Height + 32);

        // The shooter is pulled toward the other player while Space remains held.
        return source with
        {
            X = x,
            Y = y,
            Web = new WebState((short)(target.X + 7), (short)(target.Y + 7), (short)(x + 7), (short)(y + 7))
        };
    }

    public static bool Valid(WebState? web)
    {
        return web is null || web.EndX is >= -256 and <= 512 && web.EndY is >= -256 and <= 432
            && web.OriginX is >= -64 and <= 320 && web.OriginY is >= -64 and <= 240;
    }

    public static WebState? Interpolate(WebState? first, WebState? last, double amount)
    {
        if (first is null || last is null)
        {
            return last;
        }

        return new WebState(Blend(first.EndX, last.EndX, amount), Blend(first.EndY, last.EndY, amount),
            Blend(first.OriginX, last.OriginX, amount), Blend(first.OriginY, last.OriginY, amount));
    }

    private static short Blend(short first, short last, double amount) => (short)Math.Round(first + (last - first) * amount);
}
