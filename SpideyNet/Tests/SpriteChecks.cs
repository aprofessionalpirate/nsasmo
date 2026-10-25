using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Tests;

internal static class SpriteChecks
{
    internal static void Run()
    {
        var raster = TileRaster.For(VideoMode.Vga);
        var sprite = Enumerable.Repeat((byte)255, SpriteRaster.FrameBytes).ToArray();
        var colours = new[] { 2, 6, 1, 5, 0, 3, 7, 15 };
        for (var x = 0; x < colours.Length; x++)
        {
            for (var plane = 0; plane < 4; plane++)
            {
                if ((colours[x] & (8 >> plane)) == 0)
                {
                    sprite[plane * 63 + 3] &= (byte)~(128 >> x);
                }
            }
        }

        // Check every suit style and both mirror axes against explicit source pixels.
        var bright = new[] { 2, 8, 9, 10, 11, 12, 3, 7 };
        var shade = new[] { 6, 12, 13, 14, 6, 1, 7, 4 };
        for (var style = 0; style < Enum.GetValues<SuitColour>().Length; style++)
        {
            for (byte facing = 0; facing < 8; facing++)
            {
                var buffer = new byte[raster.BufferBytes];
                var expected = buffer.ToArray();
                SpriteRaster.Paint(new GameFrame(1, 32, 40, facing, 24), (SuitColour)style, sprite, buffer);
                for (var x = 0; x < colours.Length; x++)
                {
                    if (colours[x] == 15)
                    {
                        continue;
                    }

                    var colour = colours[x] == 2 ? bright[style] : colours[x] == 6 ? shade[style] : colours[x];
                    var targetX = (facing & 2) != 0 ? 29 + 23 - x : 32 + x;
                    var targetY = (facing & 4) != 0 ? 59 : 41;
                    SetPixel(expected, targetX, targetY, colour, raster);
                }
                Check(buffer.SequenceEqual(expected), "Sprite colour, shading, eye, outline and orientation");
            }
        }

        // Transparent pixels leave even coloured scenery untouched, including clipped sprites.
        var transparent = Enumerable.Repeat((byte)255, SpriteRaster.FrameBytes).ToArray();
        foreach (var x in new short[] { -32, -1, 0, 235, 255, 280 })
        {
            foreach (var y in new byte[] { 0, 155, 175, 200, 208 })
            {
                var buffer = Enumerable.Repeat((byte)0xA5, raster.BufferBytes).ToArray();
                SpriteRaster.Paint(new GameFrame(1, x, y, 0, 0), 0, transparent, buffer);
                Check(buffer.All(value => value == 0xA5), "Transparency and clipping preserve scenery");
            }
        }

        var edge = new byte[raster.BufferBytes];
        SpriteRaster.Paint(new GameFrame(1, -1, 0, 0, 0), SuitColour.Cyan, sprite, edge);
        Check(ReadPixel(edge, 0, 1, raster) == 12, "Left-edge clipping retain the correct source pixel");
        Check(edge.Skip(1).Take(raster.RowBytes - 1).All(value => value == 0), "No wrapped row writes");
        // Verify actor zero is skipped without extending the final actor or accepting mixed patches.
        Check(ActorDrawPlan.Build(false).SequenceEqual(new byte[] { 0xBB, 0xB2, 0x92, 0xB9, 12, 0, 0x90 }), "Native player draw range");
        Check(ActorDrawPlan.Build(true).SequenceEqual(new byte[] { 0xBB, 0xE4, 0x92, 0xB9, 11, 0, 0x90 }), "Composited player draw range");
        Check(ActorDrawPlan.IsSupported(new byte[] { 0xBB, 0xB2, 0x92, 0x8B, 0x0E, 0x8B, 0x92 }), "Original renderer signature");
        var mixed = ActorDrawPlan.Build(true);
        mixed[4] = 12;
        Check(!ActorDrawPlan.IsSupported(mixed), "Reject mismatched start/count patches");
        Console.WriteLine("Eight suit palettes, sprite geometry and local/native draw-range checks passed.");
    }

    private static void SetPixel(byte[] buffer, int x, int y, int colour, TileRaster raster)
    {
        for (var plane = 0; plane < 4; plane++)
        {
            if ((colour & (8 >> plane)) != 0)
            {
                buffer[plane * raster.PlaneBytes + y * raster.RowBytes + x / 8] |= (byte)(128 >> (x & 7));
            }
        }
    }

    private static int ReadPixel(byte[] buffer, int x, int y, TileRaster raster)
    {
        var colour = 0;
        for (var plane = 0; plane < 4; plane++)
        {
            colour = (colour << 1) | ((buffer[plane * raster.PlaneBytes + y * raster.RowBytes + x / 8] & (128 >> (x & 7))) != 0 ? 1 : 0);
        }
        return colour;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
