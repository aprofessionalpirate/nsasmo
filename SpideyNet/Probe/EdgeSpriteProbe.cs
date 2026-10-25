using System.Buffers.Binary;
using System.IO.Compression;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class EdgeSpriteProbe
{
    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var game = new GameBridge(processId, stop.Token);
        if (game.Read()?.Level != 1)
        {
            throw new IOException("Start a disposable VGA Midnight game first.");
        }
        var positions = new (short X, byte Y)[] { (80, 250), (80, 235), (-6, 90), (250, 90), (200, 170) };
        try
        {
            foreach (var position in positions)
            {
                for (byte facing = 0; facing < 8; facing++)
                {
                    foreach (var pose in new byte[] { 18, 27 })
                    {
                        var frame = new GameFrame(1, position.X, position.Y, facing, pose);
                        game.Render([frame], InjectionMode.Experimental);
                        await Task.Delay(150, stop.Token);
                        var expected = await CaptureAsync(game, frame);
                        for (var offset = 0; offset < expected.Length; offset += 2)
                        {
                            // Recolour only the sprite's clipped bounds, not red scenery elsewhere in the crop.
                            var x = Math.Clamp(frame.X - 3, 0, GameLayout.Width - 27) + offset / 2 % 27;
                            var y = Math.Clamp(frame.ScreenY, 0, GameLayout.Height - 21) + offset / 2 / 27;
                            var originX = frame.X - ((frame.Facing & 2) != 0 ? 3 : 0);
                            if (x < originX || x >= originX + 24 || y < frame.ScreenY || y >= frame.ScreenY + 21)
                            {
                                continue;
                            }
                            var colour = BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(offset));
                            colour = colour == 0xE200 ? (ushort)0x671C : colour == 0xA100 ? (ushort)0x051C : colour;
                            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset), colour);
                        }

                        game.Render([frame], InjectionMode.Experimental, true,
                            guestColours: [SuitColour.Cyan], localColour: SuitColour.Blue);
                        await Task.Delay(150, stop.Token);
                        if (!(await CaptureAsync(game, frame)).SequenceEqual(expected))
                        {
                            throw new IOException($"Clipped pose {pose}, facing {facing} at {frame.X},{frame.ScreenY} differs from native artwork.");
                        }
                    }
                }
                Console.WriteLine($"Native clipping matched for crouch/turn poses in eight facings at {position.X},{(position.Y >= 224 ? position.Y - 256 : position.Y)}.");
            }
        }
        finally
        {
            game.Render([], InjectionMode.Observe);
        }
        Console.WriteLine("80 clipped colour images match native draw_actor; local Blue and remote Cyan remain active.");
    }

    private static async Task<byte[]> CaptureAsync(GameBridge game, GameFrame frame)
    {
        const int width = 27;
        const int height = 21;
        var left = Math.Clamp(frame.X - 3, 0, GameLayout.Width - width);
        var top = 24 + Math.Clamp(frame.ScreenY, 0, GameLayout.Height - height);
        var image = await game.CaptureScreenAsync() ?? throw new IOException("No DOSBox image.");
        using var input = new MemoryStream(image.Data);
        using var decoder = new DeflateStream(input, CompressionMode.Decompress);
        var pixels = new byte[ScreenFrame.RawBytes];
        decoder.ReadExactly(pixels);
        var crop = new byte[width * height * 2];
        for (var row = 0; row < height; row++)
        {
            pixels.AsSpan(((top + row) * ScreenFrame.Width + left) * 2, width * 2).CopyTo(crop.AsSpan(row * width * 2));
        }
        return crop;
    }
}
