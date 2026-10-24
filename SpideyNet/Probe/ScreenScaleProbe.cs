using System.Runtime.InteropServices;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class ScreenScaleProbe
{
    internal static void Run()
    {
        foreach (var scale in new[] { 2, 3, 4 })
        {
            Check(scale);
        }
        Console.WriteLine("Preview scaling: 2x, 3x and 4x high-contrast source colours retained without darkening.");
    }

    private static void Check(int scale)
    {
        var source = CreateCompatibleDC(0);
        var target = CreateCompatibleDC(0);
        var info = new ScreenBitmapInfo { Size = 40, Width = ScreenFrame.Width * scale,
            Height = -ScreenFrame.Height * scale, Planes = 1, BitCount = 32 };
        var input = CreateDIBSection(source, ref info, 0, out var sourcePixels, 0, 0);
        info.Width = ScreenFrame.Width;
        info.Height = -ScreenFrame.Height;
        var output = CreateDIBSection(target, ref info, 0, out var targetPixels, 0, 0);
        nint oldSource = 0;
        nint oldTarget = 0;
        try
        {
            if (source == 0 || target == 0 || input == 0 || output == 0)
            {
                throw new IOException("Cannot create synthetic preview scale buffers.");
            }
            oldSource = SelectObject(source, input);
            oldTarget = SelectObject(target, output);
            var pixels = new int[ScreenFrame.Width * ScreenFrame.Height * scale * scale];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = index % 2 == 0 ? 0x00FF0000 : 0x0000FFFF;
            }
            Marshal.Copy(pixels, 0, sourcePixels, pixels.Length);

            // Start with GDI's default shrinking mode; the production driver must override it.
            SetStretchBltMode(target, 1);
            if (!StretchBlt(target, 0, 0, ScreenFrame.Width, ScreenFrame.Height,
                source, 0, 0, ScreenFrame.Width * scale, ScreenFrame.Height * scale, 0x00CC0020))
            {
                throw new IOException("Cannot check the default preview scaling mode.");
            }
            GdiFlush();
            var actual = new int[ScreenFrame.Width * ScreenFrame.Height];
            Marshal.Copy(targetPixels, actual, 0, actual.Length);
            Console.WriteLine($"Default GDI shrinking at {scale}x: {actual.Count(value => (value & 0xFFFFFF) == 0)} black pixels.");

            if (!GameScreenScale.Copy(source, target, ScreenFrame.Width * scale, ScreenFrame.Height * scale))
            {
                throw new IOException("The preview scaling driver failed.");
            }
            Marshal.Copy(targetPixels, actual, 0, actual.Length);
            if (actual.Any(value => (value & 0xFFFFFF) is not (0x00FF0000 or 0x0000FFFF)))
            {
                throw new IOException("Preview shrinking introduced darkened or blended colours.");
            }
        }
        finally
        {
            if (oldSource != 0)
            {
                SelectObject(source, oldSource);
            }
            if (oldTarget != 0)
            {
                SelectObject(target, oldTarget);
            }
            if (input != 0)
            {
                DeleteObject(input);
            }
            if (output != 0)
            {
                DeleteObject(output);
            }
            DeleteDC(source);
            DeleteDC(target);
        }
    }

    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref ScreenBitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint item);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(nint dc, int mode);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(nint target, int x, int y, int width, int height,
        nint source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint operation);
}
