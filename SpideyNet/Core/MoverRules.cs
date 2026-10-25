namespace SpideyNet.Core;

public static class MoverRules
{
    public const int MaxObjects = 8;
    public const int TileSize = 8;
    public const int Columns = 32;
    public const int Rows = 22;
    private const int PlayerHeight = 20;
    private const int PlayerWidth = 8;
    private const int ContactTolerance = 8;

    public static bool Riding(GameFrame player, MoverState mover)
    {
        var left = (mover.Left + mover.X) * TileSize;
        var top = (mover.Top + mover.Y) * TileSize;
        return player.X + PlayerWidth > left && player.X < left + mover.Width * TileSize
            && Math.Abs(player.ScreenY + PlayerHeight - top) <= ContactTolerance;
    }

    public static bool SameShape(MoverState first, MoverState second) => first.Mover == second.Mover
        && first.Left == second.Left && first.Top == second.Top && first.Width == second.Width && first.Height == second.Height;

    public static bool Valid(MoverState[]? movers) => movers is not null && movers.Length <= MaxObjects
        && movers.All(mover => mover is not null && mover.Mover is >= 0 and < SharedRules.MaxMovers
            && mover.Motion < 0x80 && mover.Left < Columns && mover.Top < Rows
            && mover.Width is > 0 and <= Columns && mover.Height is > 0 and <= Rows
            && mover.Left + mover.X >= 0 && mover.Left + mover.X + mover.Width <= Columns
            && mover.Top + mover.Y >= 0 && mover.Top + mover.Y + mover.Height <= Rows)
        && movers.Select(mover => mover.Mover).Distinct().Count() == movers.Length;
}
