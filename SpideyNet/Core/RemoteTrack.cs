namespace SpideyNet.Core;

public sealed class RemoteTrack
{
    private const int DelayMs = NetRules.TickMs * 2;
    private const int MaxSamples = 6;
    private const int SnapDistance = 48;
    private readonly Queue<(long Time, GameFrame Frame)> samples = new();

    public void Add(GameFrame frame, long now)
    {
        if (samples.Count > 0 && samples.Last().Frame.Level != frame.Level) { samples.Clear(); }
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
            || Math.Abs(last.Frame.Y - first.Frame.Y) > SnapDistance)
        {
            return last.Frame;
        }

        var amount = Math.Clamp((double)(target - first.Time) / (last.Time - first.Time), 0, 1);
        return last.Frame with
        {
            X = (short)Math.Round(first.Frame.X + (last.Frame.X - first.Frame.X) * amount),
            Y = (byte)Math.Round(first.Frame.Y + (last.Frame.Y - first.Frame.Y) * amount)
        };
    }
}
