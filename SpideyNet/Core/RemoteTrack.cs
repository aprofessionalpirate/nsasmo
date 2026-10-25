namespace SpideyNet.Core;

public sealed class RemoteTrack
{
    private const double DelayMs = NetRules.TickMs * 2;
    private const int MaxSamples = 6;
    private const int SnapDistance = 48;
    private readonly Queue<(long Time, GameFrame Frame)> samples = new();

    public void Add(GameFrame frame, long now)
    {
        // Reappearing players start a new timeline, even in the same room.
        // Otherwise the first fresh update briefly renders their expired position.
        if (samples.Count > 0 && (samples.Last().Frame.Level != frame.Level
            || now - samples.Last().Time >= NetRules.StaleMs))
        {
            samples.Clear();
        }

        samples.Enqueue((now, frame));
        while (samples.Count > MaxSamples) { samples.Dequeue(); }
    }

    public GameFrame? Sample(long now)
    {
        if (samples.Count == 0 || now - samples.Last().Time >= NetRules.StaleMs) { return null; }
        var target = now - DelayMs;
        while (samples.Count > 2 && samples.ElementAt(1).Time <= target) { samples.Dequeue(); }
        var first = samples.Peek();
        var last = samples.Count > 1 ? samples.ElementAt(1) : first;
        if (last.Time <= first.Time || Math.Abs(last.Frame.X - first.Frame.X) > SnapDistance
            || Math.Abs(last.Frame.ScreenY - first.Frame.ScreenY) > SnapDistance)
        {
            return last.Frame;
        }

        var amount = Math.Clamp((double)(target - first.Time) / (last.Time - first.Time), 0, 1);
        return last.Frame with
        {
            X = (short)Math.Round(first.Frame.X + (last.Frame.X - first.Frame.X) * amount),
            Y = unchecked((byte)(int)Math.Round(first.Frame.ScreenY + (last.Frame.ScreenY - first.Frame.ScreenY) * amount)),
            Web = WebRules.Interpolate(first.Frame.Web, last.Frame.Web, amount)
        };
    }
}
