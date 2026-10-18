namespace SpideyNet.Core;

internal sealed class EnemyTrack
{
    private readonly Queue<(long Time, EnemyState State)> samples = new();

    internal void Add(EnemyState state, long now)
    {
        if (samples.Count > 0 && now - samples.Last().Time >= NetRules.StaleMs)
        {
            samples.Clear();
        }

        samples.Enqueue((now, state));
        while (samples.Count > 6)
        {
            samples.Dequeue();
        }
    }

    internal EnemyState Sample(long now)
    {
        var target = now - NetRules.TickMs * 2;
        while (samples.Count > 2 && samples.ElementAt(1).Time <= target)
        {
            samples.Dequeue();
        }

        var first = samples.Peek();
        var last = samples.Count > 1 ? samples.ElementAt(1) : first;
        var latest = samples.Last().State;
        if (last.Time <= first.Time || Math.Abs(last.State.X - first.State.X) > 48
            || Math.Abs(last.State.Y - first.State.Y) > 48)
        {
            return latest;
        }

        // Smooth motion between network updates, while health/death remain immediate.
        var amount = Math.Clamp((target - first.Time) / (last.Time - first.Time), 0, 1);
        return last.State with
        {
            X = (short)Math.Round(first.State.X + (last.State.X - first.State.X) * amount),
            Y = (byte)Math.Round(first.State.Y + (last.State.Y - first.State.Y) * amount),
            Health = latest.Health,
            Pose = latest.Health == 0 ? latest.Pose : last.State.Pose
        };
    }
}
