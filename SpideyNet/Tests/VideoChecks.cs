using SpideyNet.Core;
using SpideyNet.Game;

internal static class VideoChecks
{
    internal static void Run()
    {
        CheckMode(VideoMode.Vga, 4, 1);
        CheckMode(VideoMode.Cga, 1, 2);
        if (!GameProfile.Supports((byte)VideoMode.Vga) || !GameProfile.Supports((byte)VideoMode.Cga))
        {
            throw new InvalidOperationException("Accept verified video modes");
        }
        foreach (var mode in new[] { VideoMode.Ega, VideoMode.Tandy, (VideoMode)byte.MaxValue })
        {
            if (GameProfile.Supports((byte)mode)) { throw new InvalidOperationException("Reject unverified renderer modes"); }
            try { TileRaster.For(mode); }
            catch (IOException) { continue; }
            throw new InvalidOperationException("Reject unverified video modes");
        }
        Console.WriteLine("Video checks passed.");
    }

    private static void CheckMode(VideoMode mode, int planes, int cellBytes)
    {
        const byte Untouched = 0xA5;
        var raster = TileRaster.For(mode);
        var rowBytes = MoverRules.Columns * cellBytes;
        var planeBytes = MoverPlan.TileBytes * MoverRules.TileSize * cellBytes;
        var buffer = Enumerable.Repeat(Untouched, planeBytes * planes).ToArray();
        var expected = buffer.ToArray();
        var tile = Enumerable.Range(1, MoverRules.TileSize * cellBytes * planes).Select(value => (byte)value).ToArray();

        // Check an interior tile and the final tile against the native plane layout.
        foreach (var index in new[] { MoverRules.Columns + 3, MoverPlan.TileBytes - 1 })
        {
            raster.Paint(index, tile, buffer);
            for (var plane = 0; plane < planes; plane++)
            {
                for (var row = 0; row < MoverRules.TileSize; row++)
                {
                    for (var pixel = 0; pixel < cellBytes; pixel++)
                    {
                        var target = plane * planeBytes + (index / MoverRules.Columns * MoverRules.TileSize + row) * rowBytes
                            + index % MoverRules.Columns * cellBytes + pixel;
                        expected[target] = tile[(plane * MoverRules.TileSize + row) * cellBytes + pixel];
                    }
                }
            }
            if (!buffer.SequenceEqual(expected)) { throw new InvalidOperationException($"{mode} tile placement"); }
        }
        var before = buffer.ToArray();
        try { raster.Paint(MoverPlan.TileBytes, tile, buffer); }
        catch (InvalidDataException)
        {
            if (buffer.SequenceEqual(before)) { return; }
        }
        throw new InvalidOperationException("Reject out-of-range tiles without writes");
    }
}
