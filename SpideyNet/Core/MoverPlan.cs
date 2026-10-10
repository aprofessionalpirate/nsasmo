namespace SpideyNet.Core;

public static class MoverPlan
{
    public const int TileBytes = MoverRules.Columns * MoverRules.Rows;
    private const byte SolidMask = 0xC0;

    public static byte[] Build(ReadOnlySpan<byte> tiles, IReadOnlyList<MoverShape> before, IReadOnlyList<MoverShape> after)
    {
        if (tiles.Length != TileBytes || !MoverRules.Valid(before.Select(shape => shape.State).ToArray())
            || !MoverRules.Valid(after.Select(shape => shape.State).ToArray())
            || after.Any(shape => shape.Tiles.Length != shape.State.Width * shape.State.Height))
        {
            throw new ArgumentException("Invalid moving object tiles.");
        }
        var result = tiles.ToArray();
        var changed = before.Where(shape => after.Any(next => next.State.Mover == shape.State.Mover
            && (next.State.X != shape.State.X || next.State.Y != shape.State.Y))).ToArray();
        if (changed.Length == 0) { return result; }

        // Match native removal, then repaint objects at their shared positions.
        foreach (var shape in changed)
        {
            foreach (var cell in Cells(shape.State))
            {
                if ((result[cell.Index] & SolidMask) != 0) { result[cell.Index] = 0; }
            }
        }
        foreach (var shape in after)
        {
            foreach (var cell in Cells(shape.State)) { result[cell.Index] = shape.Tiles[cell.Source]; }
        }
        return result;
    }

    private static IEnumerable<(int Index, int Source)> Cells(MoverState state)
    {
        for (var row = 0; row < state.Height; row++)
        {
            var y = state.Top + state.Y + row;
            if (y is < 0 or >= MoverRules.Rows) { continue; }
            for (var column = 0; column < state.Width; column++)
            {
                var x = state.Left + state.X + column;
                if (x is < 0 or >= MoverRules.Columns) { continue; }
                yield return (y * MoverRules.Columns + x, row * state.Width + column);
            }
        }
    }
}
