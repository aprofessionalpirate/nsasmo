using System.IO.Compression;
using SpideyNet.Client;
using SpideyNet.Core;
using SpideyNet.Game;
using SpideyNet.Transport;

namespace SpideyNet.Probe;

internal static class RenderRegressionProbe
{
    internal static async Task RunAsync(Uri endpoint, int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var game = new GameBridge(processId, stop.Token);
        var original = game.Read() ?? throw new IOException("Start a disposable VGA Midnight game first.");
        if (original.Level != 1)
        {
            throw new IOException("The colour visibility sampling region is specific to Midnight.");
        }
        var before = game.Inspect();
        var session = new Session();
        var peer = new RelayClient();
        Task? connection = null;
        var room = $"render-{Guid.NewGuid():N}";
        try
        {
            session.Connect(processId, endpoint, room, "Render viewer", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Experimental, true, SuitColour.Cyan);
            await WaitAsync(() => session.View.Status.StartsWith("Connected", StringComparison.Ordinal)
                && session.View.Local is not null, stop.Token);
            await Task.Delay(150, stop.Token);
            peer.Publish(new GameFrame(1, 80, 90, 0, 24), colour: SuitColour.Cyan);
            connection = peer.Run(endpoint, room, "Cyan guest", InteractionMode.Disabled, stop.Token, SharedMode.Experimental);
            await WaitAsync(() => session.View.Players.Length == 1 && peer.World?.Shared?.Room is not null, stop.Token);
            await Task.Delay(1000, stop.Token);

            var minimum = int.MaxValue;
            var maximum = 0;
            var localMinimum = int.MaxValue;
            for (var sample = 0; sample < 100; sample++)
            {
                var image = await game.CaptureScreenAsync() ?? throw new IOException("No DOSBox image.");
                var count = CountSuitPixels(image, 80, 114);
                localMinimum = Math.Min(localMinimum, CountSuitPixels(image, original.X, original.Y + 24));
                minimum = Math.Min(minimum, count);
                maximum = Math.Max(maximum, count);
                if (session.View.Status.StartsWith("Stopped", StringComparison.Ordinal))
                {
                    throw new IOException(session.View.Status);
                }
                await Task.Delay(70, stop.Token);
            }
            Console.WriteLine($"Shared-world colour visibility: 100 displayed-frame samples, suit pixels {minimum}..{maximum}.");
            Console.WriteLine($"Local cyan suit minimum: {localMinimum} pixels across those displayed frames.");
            if (minimum < 20 || localMinimum < 20)
            {
                throw new IOException("A native displayed frame lost the coloured guest.");
            }
        }
        finally
        {
            await session.DisconnectAsync();
            stop.Cancel();
            if (connection is not null)
            {
                try
                {
                    await connection;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        if (game.Inspect().RenderHex != before.RenderHex || game.Inspect().SlotHash != before.SlotHash)
        {
            throw new IOException("Shared colour renderer did not restore its reserved bytes.");
        }
        Console.WriteLine("Shared-world coloured rendering and native renderer/storage restoration passed.");
    }

    private static int CountSuitPixels(ScreenFrame image, int left, int top)
    {
        using var input = new MemoryStream(image.Data);
        using var decoder = new DeflateStream(input, CompressionMode.Decompress);
        var pixels = new byte[ScreenFrame.RawBytes];
        decoder.ReadExactly(pixels);
        var count = 0;
        for (var y = top; y < top + 21; y++)
        {
            for (var x = left; x < left + 24; x++)
            {
                var offset = (y * ScreenFrame.Width + x) * 2;
                var colour = pixels[offset] | (pixels[offset + 1] << 8);
                var red = (colour >> 11) & 31;
                var green = (colour >> 5) & 63;
                var blue = colour & 31;
                if (red < 16 && green > 30 && blue > 20)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static async Task WaitAsync(Func<bool> ready, CancellationToken token)
    {
        while (!ready())
        {
            await Task.Delay(30, token);
        }
    }
}
