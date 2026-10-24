using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class SpriteRaster
{
    internal const int FrameBytes = 256;
    private const int Width = 24;
    private const int Height = 21;
    private const int PlaneBytes = 63;
    private static ReadOnlySpan<byte> Bright => [2, 8, 9, 10, 11, 12, 3, 7];
    private static ReadOnlySpan<byte> Shade => [6, 12, 13, 14, 6, 1, 7, 4];

    internal static void Paint(GameFrame frame, SuitColour colourStyle, ReadOnlySpan<byte> sprite, Span<byte> buffer)
    {
        var raster = TileRaster.For(VideoMode.Vga);
        if (!NetRules.Valid(frame) || !Enum.IsDefined(colourStyle)
            || sprite.Length != FrameBytes || buffer.Length != raster.BufferBytes)
        {
            throw new InvalidDataException("Invalid guest sprite or graphics buffer.");
        }

        // Match draw_actor's 24x21 source, mirrored origin and screen-edge clipping.
        var mirrored = (frame.Facing & 2) != 0;
        var originX = frame.X - (mirrored ? 3 : 0);
        var originY = (int)frame.Y;
        for (var row = 0; row < Height; row++)
        {
            var y = originY + row;
            if (y is < 0 or >= GameLayout.Height)
            {
                continue;
            }

            var sourceY = (frame.Facing & 4) != 0 ? Height - 1 - row : row;
            for (var column = 0; column < Width; column++)
            {
                var x = originX + column;
                if (x is < 0 or >= GameLayout.Width)
                {
                    continue;
                }

                var sourceX = mirrored ? Width - 1 - column : column;
                var source = sourceY * 3 + sourceX / 8;
                var bit = 0x80 >> (sourceX & 7);
                var colour = 0;
                for (var plane = 0; plane < raster.Planes; plane++)
                {
                    colour = (colour << 1) | ((sprite[plane * PlaneBytes + source] & bit) != 0 ? 1 : 0);
                }

                // All-one pixels are transparent. Recolour the red suit and its shading only;
                // blue clothing, outlines and white eyes retain their native colours.
                if (colour == 15)
                {
                    continue;
                }
                colour = colour == 2 ? Bright[(int)colourStyle] : colour == 6 ? Shade[(int)colourStyle] : colour;
                var target = y * raster.RowBytes + x / 8;
                var mask = 0x80 >> (x & 7);
                for (var plane = 0; plane < raster.Planes; plane++)
                {
                    var offset = plane * raster.PlaneBytes + target;
                    buffer[offset] = (byte)((buffer[offset] & ~mask)
                        | ((colour & (8 >> plane)) != 0 ? mask : 0));
                }
            }
        }
    }
}
