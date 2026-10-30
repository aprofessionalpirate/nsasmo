using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Tests;

internal static class PlayerSpriteChecks
{
    internal static void Run()
    {
        Check(Enumerable.Range(0, 256).Count(value => PlayerSpriteRules.Valid((byte)value)) == 44,
            "Spider-Man plus 43 nonblank enemy frames");
        var frame = new GameFrame(1, 40, 60, 0, 24);
        for (byte facing = 0; facing < 8; facing++)
        {
            Check(PlayerSpriteRules.SourceFrame(frame with { Facing = facing }, 0) == 24 + ((facing & 1) != 0 ? 96 : 0),
                "Spider-Man retains native animation and sideways bank");
            for (byte enemy = PlayerSpriteRules.FirstEnemy; enemy <= PlayerSpriteRules.LastEnemy; enemy++)
            {
                Check(PlayerSpriteRules.SourceFrame(frame with { Facing = facing }, enemy) == enemy,
                    "Enemy appearances never access an unverified sideways bank");
            }
        }

        // Independently map source pixels into all orientations, including three clipped screen edges.
        var raster = TileRaster.For(VideoMode.Vga);
        var source = new byte[SpriteRaster.FrameBytes];
        for (var y = 0; y < 21; y++)
        {
            for (var x = 0; x < 24; x++)
            {
                SetPixel(source, 63, 3, x, y, (x + y * 5) % 16);
            }
        }

        foreach (var position in new (short X, int Y)[] { (40, 60), (-6, -6), (250, 170) })
        {
            for (byte facing = 0; facing < 8; facing++)
            {
                var player = frame with { X = position.X, Y = unchecked((byte)position.Y), Facing = facing };
                var expected = Enumerable.Repeat((byte)0xA5, raster.BufferBytes).ToArray();
                var sideways = (facing & 1) != 0;
                var width = sideways ? 21 : 24;
                var height = sideways ? 24 : 21;
                for (var y = 0; y < 21; y++)
                {
                    for (var x = 0; x < 24; x++)
                    {
                        var colour = (x + y * 5) % 16;
                        var targetX = sideways ? y : x;
                        var targetY = sideways ? x : y;
                        if ((facing & 2) != 0)
                        {
                            targetX = width - 1 - targetX;
                        }
                        if ((facing & 4) != 0)
                        {
                            targetY = height - 1 - targetY;
                        }

                        targetX += player.X - ((facing & 2) != 0 ? 3 : 0);
                        targetY += player.ScreenY;
                        if (colour != 15 && targetX is >= 0 and < GameLayout.Width && targetY is >= 0 and < GameLayout.Height)
                        {
                            SetPixel(expected, raster.PlaneBytes, raster.RowBytes, targetX, targetY, colour);
                        }
                    }
                }

                foreach (var colour in Enum.GetValues<SuitColour>())
                {
                    var buffer = Enumerable.Repeat((byte)0xA5, raster.BufferBytes).ToArray();
                    SpriteRaster.Paint(player, colour, source, buffer, enemy: true);
                    Check(buffer.SequenceEqual(expected), "Enemy pixels retain original colours, transparency, orientation and clipping");
                }
            }
        }
        Console.WriteLine("Player sprites: 43 visible enemy frames, bounded bank access, eight facings, clipping and original enemy colours passed.");
    }

    private static void SetPixel(byte[] buffer, int planeBytes, int rowBytes, int x, int y, int colour)
    {
        for (var plane = 0; plane < 4; plane++)
        {
            var offset = plane * planeBytes + y * rowBytes + x / 8;
            var mask = 128 >> (x & 7);
            buffer[offset] = (byte)((buffer[offset] & ~mask) | ((colour & (8 >> plane)) != 0 ? mask : 0));
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
