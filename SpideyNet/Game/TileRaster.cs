using SpideyNet.Core;

namespace SpideyNet.Game;

internal readonly record struct TileRaster(int Planes, int CellBytes)
{
    private const int Rows = MoverRules.TileSize;
    private const int VgaPlanes = 4;
    private const int CgaCellBytes = 2;

    internal int RowBytes => MoverRules.Columns * CellBytes;
    internal int PlaneBytes => MoverPlan.TileBytes * Rows * CellBytes;
    internal int TileBytes => Planes * Rows * CellBytes;
    internal int BufferBytes => Planes * PlaneBytes;

    internal static TileRaster For(VideoMode mode) => mode switch
    {
        VideoMode.Vga => new(VgaPlanes, 1),
        VideoMode.Cga => new(1, CgaCellBytes),
        _ => throw new IOException("Select VGA or CGA in the game's video menu.")
    };

    internal void Paint(int index, ReadOnlySpan<byte> tile, Span<byte> buffer)
    {
        if (index < 0 || index >= MoverPlan.TileBytes || tile.Length != TileBytes || buffer.Length != BufferBytes)
        {
            throw new InvalidDataException("Invalid tile buffer dimensions.");
        }

        // VGA tiles store eight rows per plane; CGA stores two bytes per row.
        for (var plane = 0; plane < Planes; plane++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var source = (plane * Rows + row) * CellBytes;
                var target = plane * PlaneBytes + (index / MoverRules.Columns * Rows + row) * RowBytes
                    + index % MoverRules.Columns * CellBytes;
                tile.Slice(source, CellBytes).CopyTo(buffer.Slice(target, CellBytes));
            }
        }
    }
}
