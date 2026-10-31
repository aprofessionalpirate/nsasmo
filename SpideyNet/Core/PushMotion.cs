namespace SpideyNet.Core;

public sealed class PushMotion
{
    private const int Distance = 128;
    private const int RightEdge = GameLayout.Width - 21;
    private const int BottomEdge = GameLayout.Height - 21;
    private readonly long startedAt;
    private readonly double dx;
    private readonly double dy;
    private GameFrame position;
    private byte previousRoom;
    private double x;
    private double y;
    private double elapsed;
    private long sampledAt;

    public PushMotion(GameFrame source, GameFrame target, long now)
    {
        position = target with { Web = null, WebHeld = false };
        previousRoom = target.Level;
        x = target.X;
        y = target.ScreenY;
        sampledAt = startedAt = now;
        var separationX = target.X - source.X;
        var separationY = target.ScreenY - source.ScreenY;

        // Overlapping players have no separation vector; use the pusher's horizontal facing.
        if (separationX == 0 && separationY == 0)
        {
            separationX = (source.Facing & 2) != 0 ? -1 : 1;
        }

        var length = Math.Sqrt(separationX * separationX + separationY * separationY);
        dx = separationX / length * Distance;
        dy = separationY / length * Distance;
    }

    public bool Accepts(byte level) => level == position.Level || level == previousRoom;

    public bool Finished(GameFrame current, long now)
    {
        // Wait for native placement acknowledgement, but never retain a failed client forever.
        return now - startedAt >= 5000 || elapsed >= InteractionRules.PushMs
            && current.Level == position.Level
            && (Math.Abs(current.X - position.X) <= 2 && Math.Abs(current.ScreenY - position.ScreenY) <= 2
                || now - startedAt >= InteractionRules.PushMs + 1000);
    }

    public GameFrame Sample(GameFrame current, long now)
    {
        var delta = Math.Clamp(now - sampledAt, 0, 50);
        sampledAt = now;
        if (current.Level != position.Level)
        {
            // Pause the remaining impulse while DOS loads the connected room.
            return position;
        }

        previousRoom = position.Level;
        position = position with { Exits = current.Exits };
        var step = Math.Min(delta, InteractionRules.PushMs - elapsed);
        var moveX = dx * step / InteractionRules.PushMs;
        var moveY = dy * step / InteractionRules.PushMs;
        var fraction = 1d;
        var next = (byte)0;
        var horizontal = false;

        // Follow the actual room's exits, without querying collision tiles or locked doors.
        if (moveX != 0)
        {
            var crossing = (moveX > 0 ? RightEdge - x : -x) / moveX;
            if (crossing >= 0 && crossing < fraction)
            {
                fraction = crossing;
                next = moveX > 0 ? current.Exits?.Right ?? 0 : current.Exits?.Left ?? 0;
                horizontal = true;
            }
        }
        if (moveY != 0)
        {
            var crossing = (moveY > 0 ? BottomEdge - y : -y) / moveY;
            if (crossing >= 0 && crossing < fraction)
            {
                fraction = crossing;
                next = moveY > 0 ? current.Exits?.Down ?? 0 : current.Exits?.Up ?? 0;
                horizontal = false;
            }
        }

        x += moveX * fraction;
        y += moveY * fraction;
        elapsed += step * fraction;
        if (fraction < 1)
        {
            if (next == 0)
            {
                elapsed = InteractionRules.PushMs;
            }
            else
            {
                // Preserve the unspent distance; use the new room's links after it finishes loading.
                previousRoom = position.Level;
                position = position with { Level = next, Exits = null };
                if (horizontal)
                {
                    x = moveX > 0 ? 0 : RightEdge;
                }
                else
                {
                    y = moveY > 0 ? 0 : BottomEdge;
                }
            }
        }

        position = position with
        {
            X = (short)Math.Clamp(Math.Round(x), 0, RightEdge),
            Y = (byte)Math.Clamp(Math.Round(y), 0, BottomEdge)
        };
        return position;
    }
}
