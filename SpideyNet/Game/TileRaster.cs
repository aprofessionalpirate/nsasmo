using SpideyNet.Core;

namespace SpideyNet.Game;

internal readonly record struct TileRaster
{
    private const int Rows = MoverRules.TileSize;
    private const int WebColour = 3;
    internal int Planes => 4;
    internal int RowBytes => MoverRules.Columns;
    internal int PlaneBytes => MoverPlan.TileBytes * Rows;
    internal int TileBytes => Planes * Rows;
    internal int BufferBytes => Planes * PlaneBytes;

    internal static TileRaster For(VideoMode mode) => mode switch
    {
        VideoMode.Vga => new(),
        _ => throw new IOException("Select VGA in the game's video menu.")
    };

    internal void Paint(int index, ReadOnlySpan<byte> tile, Span<byte> buffer)
    {
        if (index < 0 || index >= MoverPlan.TileBytes || tile.Length != TileBytes || buffer.Length != BufferBytes)
        {
            throw new InvalidDataException("Invalid tile buffer dimensions.");
        }

        // VGA tiles store eight bytes per plane, one byte for each row.
        for (var plane = 0; plane < Planes; plane++)
        {
            for (var row = 0; row < Rows; row++)
            {
                var source = plane * Rows + row;
                var target = plane * PlaneBytes + (index / MoverRules.Columns * Rows + row) * RowBytes
                    + index % MoverRules.Columns;
                buffer[target] = tile[source];
            }
        }
    }

    internal void PaintWeb(WebState web, Span<byte> buffer)
    {
        if (!WebRules.Valid(web) || buffer.Length != BufferBytes)
        {
            throw new InvalidDataException("Invalid web geometry or graphics buffer.");
        }

        // Clip each line pixel into the native backing buffer, including off-screen shots.
        var x = (int)web.OriginX;
        var y = (int)web.OriginY;
        var dx = Math.Abs(web.EndX - x);
        var dy = -Math.Abs(web.EndY - y);
        var stepX = x < web.EndX ? 1 : -1;
        var stepY = y < web.EndY ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            if (x is >= 0 and < GameLayout.Width && y is >= 0 and < GameLayout.Height)
            {
                for (var plane = 0; plane < Planes; plane++)
                {
                    var index = plane * PlaneBytes + y * RowBytes + x / 8;
                    var mask = 0x80 >> (x & 7);
                    // Backing planes run high bit first. Palette 3 is white; 15 is yellow.
                    // Replace the pixel colour, including clearing bits on coloured scenery.
                    buffer[index] = (byte)((buffer[index] & ~mask)
                        | ((WebColour & (1 << (Planes - 1 - plane))) != 0 ? mask : 0));
                }
            }

            if (x == web.EndX && y == web.EndY)
            {
                break;
            }

            var twice = error * 2;
            if (twice >= dy)
            {
                error += dy;
                x += stepX;
            }
            if (twice <= dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }
}
