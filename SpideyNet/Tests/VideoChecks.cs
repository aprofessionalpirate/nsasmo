using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Tests;

internal static class VideoChecks
{
    internal static void Run()
    {
        CheckVga();
        if (!GameProfile.Supports((byte)VideoMode.Vga))
        {
            throw new InvalidOperationException("Accept VGA");
        }

        // Every other native menu value must fail before graphics writes.
        for (var value = 1; value <= byte.MaxValue; value++)
        {
            if (GameProfile.Supports((byte)value))
            {
                throw new InvalidOperationException("Reject non-VGA renderer modes");
            }

            try
            {
                TileRaster.For((VideoMode)value);
            }
            catch (IOException)
            {
                continue;
            }

            throw new InvalidOperationException("Reject non-VGA video modes");
        }

        Console.WriteLine("Video checks passed.");
    }

    private static void CheckVga()
    {
        const byte Untouched = 0xA5;
        const int Planes = 4;
        var raster = TileRaster.For(VideoMode.Vga);
        var rowBytes = MoverRules.Columns;
        var planeBytes = MoverPlan.TileBytes * MoverRules.TileSize;
        var buffer = Enumerable.Repeat(Untouched, planeBytes * Planes).ToArray();
        var expected = buffer.ToArray();
        var tile = Enumerable.Range(1, MoverRules.TileSize * Planes).Select(value => (byte)value).ToArray();

        // Check an interior tile and the final tile against the native plane layout.
        foreach (var index in new[] { MoverRules.Columns + 3, MoverPlan.TileBytes - 1 })
        {
            raster.Paint(index, tile, buffer);
            for (var plane = 0; plane < Planes; plane++)
            {
                for (var row = 0; row < MoverRules.TileSize; row++)
                {
                    var target = plane * planeBytes + (index / MoverRules.Columns * MoverRules.TileSize + row) * rowBytes
                        + index % MoverRules.Columns;
                    expected[target] = tile[plane * MoverRules.TileSize + row];
                }
            }
            if (!buffer.SequenceEqual(expected))
            {
                throw new InvalidOperationException("VGA tile placement");
            }
        }

        var before = buffer.ToArray();
        try
        {
            raster.Paint(MoverPlan.TileBytes, tile, buffer);
        }
        catch (InvalidDataException)
        {
            if (buffer.SequenceEqual(before))
            {
                return;
            }
        }

        throw new InvalidOperationException("Reject out-of-range tiles without writes");
    }
}
