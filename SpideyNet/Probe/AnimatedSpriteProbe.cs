using System.Buffers.Binary;
using System.IO.Compression;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class AnimatedSpriteProbe
{
    private const int Left = 77;
    private const int Top = 114;
    private const int Width = 27;
    private const int Height = 21;

    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var game = new GameBridge(processId, stop.Token);
        var local = game.Read() ?? throw new IOException("Start a disposable VGA Midnight game first.");
        if (local.Level != 1)
        {
            throw new IOException("The native reference area must be empty Midnight sky.");
        }
        var guest = new GameFrame(1, 80, 90, 0, 24);
        var references = new List<byte[]>();
        try
        {
            // Use actual native draw_actor output as the oracle, including clipping/mirroring.
            // Recolour captured red pixels, independently of SpriteRaster's implementation.
            for (byte facing = 0; facing < 8; facing++)
            {
                for (byte pose = 24; pose < 28; pose++)
                {
                    var frame = guest with { Facing = facing, Pose = pose };
                    game.Render([frame], InjectionMode.Experimental);
                    await Task.Delay(150, stop.Token);
                    var expected = await CaptureAsync(game);
                    for (var offset = 0; offset < expected.Length; offset += 2)
                    {
                        var colour = BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(offset));
                        colour = colour == 0xE200 ? (ushort)0x671C : colour == 0xA100 ? (ushort)0x051C : colour;
                        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset), colour);
                    }
                    references.Add(expected);
                    game.Render([frame], InjectionMode.Experimental, true, guestColours: [SuitColour.Cyan]);
                    await Task.Delay(150, stop.Token);
                    if (!(await CaptureAsync(game)).SequenceEqual(expected))
                    {
                        throw new IOException($"Coloured pose {pose}, facing {facing} differs from native artwork.");
                    }
                }
            }
            Console.WriteLine("32 coloured pose/facing images match independently recoloured native draw_actor output exactly.");

            using var animation = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
            var worker = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(NetRules.LocalTickInterval);
                var tick = 0;
                while (await timer.WaitForNextTickAsync(animation.Token))
                {
                    var frame = guest with { Pose = (byte)(24 + tick % 4), Facing = (byte)(tick / 4 % 8) };
                    game.Render([frame], InjectionMode.Experimental, true, guestColours: [SuitColour.Cyan]);
                    tick++;
                }
            }, animation.Token);
            try
            {
                await Task.Delay(200, stop.Token);
                for (var sample = 0; sample < 600; sample++)
                {
                    var image = await CaptureAsync(game);
                    if (!references.Any(reference => reference.SequenceEqual(image)))
                    {
                        var differences = references.Min(reference => reference.Zip(image).Count(pair => pair.First != pair.Second));
                        throw new IOException($"Mixed animated frame at capture {sample}; {differences} bytes differ from the closest native pose.");
                    }
                    await Task.Delay(15, stop.Token);
                }
            }
            finally
            {
                animation.Cancel();
                try
                {
                    await worker;
                }
                catch (OperationCanceledException) when (animation.IsCancellationRequested)
                {
                }
            }
            Console.WriteLine("600 animated displayed-frame captures match whole native poses; no mixed colour planes.");
        }
        finally
        {
            game.Render([], InjectionMode.Observe);
        }
    }

    private static async Task<byte[]> CaptureAsync(GameBridge game)
    {
        var image = await game.CaptureScreenAsync() ?? throw new IOException("No DOSBox image.");
        using var input = new MemoryStream(image.Data);
        using var decoder = new DeflateStream(input, CompressionMode.Decompress);
        var pixels = new byte[ScreenFrame.RawBytes];
        decoder.ReadExactly(pixels);
        var crop = new byte[Width * Height * 2];
        for (var row = 0; row < Height; row++)
        {
            pixels.AsSpan(((Top + row) * ScreenFrame.Width + Left) * 2, Width * 2).CopyTo(crop.AsSpan(row * Width * 2));
        }
        return crop;
    }
}
