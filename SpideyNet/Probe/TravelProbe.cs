using System.IO.Compression;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class TravelProbe
{
    internal static async Task RunAsync(int processId, string imagePath)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var game = new GameBridge(processId, stop.Token);
        var before = game.Read() ?? throw new IOException("Start a VGA game before testing travel.");
        try
        {
            foreach (var room in new byte[] { 3, 4, 17, 25, 6, 1, 2, 1 })
            {
                var destination = before with { Level = room, X = 90, Y = 147, Web = null, WebHeld = false };
                await TravelAsync(game, destination, stop.Token);
                var arrived = game.Read() ?? throw new IOException("The destination room was not readable.");
                if (arrived.Level != room || Math.Abs(arrived.X - destination.X) > 8
                    || Math.Abs(arrived.Y - destination.Y) > 8 || game.Title != RoomNames.Get(room))
                {
                    throw new InvalidOperationException($"Teleport did not reach {RoomNames.Get(room)} at the requested position: {arrived}.");
                }

                var screen = await game.CaptureScreenAsync() ?? throw new IOException("DOSBox did not provide a game screen.");
                var pixels = new byte[ScreenFrame.RawBytes];
                using var source = new MemoryStream(screen.Data);
                using var decoder = new DeflateStream(source, CompressionMode.Decompress);
                decoder.ReadExactly(pixels);
                if (pixels.All(value => value == 0))
                {
                    throw new IOException("The captured game screen was blank.");
                }

                File.WriteAllBytes(imagePath, pixels);
                Console.WriteLine($"Travel: {game.Title}, X={arrived.X}, Y={arrived.Y}; preview {screen.Data.Length} bytes.");
            }
        }
        finally
        {
            game.CancelTeleport();
            using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await TravelAsync(game, before, restore.Token);
        }
    }

    private static async Task TravelAsync(GameBridge game, GameFrame destination, CancellationToken token)
    {
        while (!game.TryTeleport(destination))
        {
            await Task.Delay(NetRules.LocalTickInterval, token);
        }
    }
}
