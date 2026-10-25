using System.IO.Compression;
using Avalonia.Headless.XUnit;
using SpideyNet.Client.Services;
using SpideyNet.Core;
using Xunit;

namespace SpideyNet.Client.Tests;

public sealed class PreviewTests
{
    [AvaloniaFact]
    public void PreviewHasFixedDimensionsAndRejectsTruncatedOrExpandedData()
    {
        using var image = PreviewImageFactory.Create(Encode(new byte[ScreenFrame.RawBytes]));
        Assert.Equal(ScreenFrame.Width, image.PixelSize.Width);
        Assert.Equal(ScreenFrame.Height, image.PixelSize.Height);
        Assert.Throws<EndOfStreamException>(() => PreviewImageFactory.Create(Encode(new byte[10])));
        Assert.Throws<InvalidDataException>(() => PreviewImageFactory.Create(Encode(new byte[ScreenFrame.RawBytes + 1])));
        Assert.Throws<InvalidDataException>(() => PreviewImageFactory.Create(new ScreenFrame(1, [])));
    }

    private static ScreenFrame Encode(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var compressor = new DeflateStream(output, CompressionLevel.Fastest, true))
        {
            compressor.Write(bytes);
        }

        return new ScreenFrame(1, output.ToArray());
    }
}
