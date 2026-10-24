using System.IO.Compression;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SpideyNet.Core;

namespace SpideyNet.Client.Services;

internal static class PreviewImageFactory
{
    internal static Bitmap Create(ScreenFrame frame)
    {
        if (!frame.IsValid)
        {
            throw new InvalidDataException("Invalid player preview dimensions.");
        }

        // Bound decompression to one RGB565 frame, including malformed compressed data.
        var raw = new byte[ScreenFrame.RawBytes];
        using var input = new MemoryStream(frame.Data);
        using var decompressor = new DeflateStream(input, CompressionMode.Decompress);
        decompressor.ReadExactly(raw);
        if (decompressor.ReadByte() != -1)
        {
            throw new InvalidDataException("Player preview exceeds its dimensions.");
        }

        var bgra = new byte[ScreenFrame.Width * ScreenFrame.Height * 4];
        for (var index = 0; index < raw.Length / 2; index++)
        {
            var colour = raw[index * 2] | (raw[index * 2 + 1] << 8);
            bgra[index * 4] = (byte)((colour & 31) * 255 / 31);
            bgra[index * 4 + 1] = (byte)(((colour >> 5) & 63) * 255 / 63);
            bgra[index * 4 + 2] = (byte)((colour >> 11) * 255 / 31);
            bgra[index * 4 + 3] = 255;
        }

        var bitmap = new WriteableBitmap(new PixelSize(ScreenFrame.Width, ScreenFrame.Height),
            new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var pixels = bitmap.Lock();
        for (var row = 0; row < ScreenFrame.Height; row++)
        {
            Marshal.Copy(bgra, row * ScreenFrame.Width * 4,
                pixels.Address + row * pixels.RowBytes, ScreenFrame.Width * 4);
        }

        return bitmap;
    }
}
