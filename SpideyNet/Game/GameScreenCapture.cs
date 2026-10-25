using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class GameScreenCapture
{
    internal static ScreenFrame? Capture(int processId)
    {
        // Match PrintWindow's physical pixels even when a host process uses DPI virtualization.
        var previous = SetThreadDpiAwarenessContext(-4);
        try
        {
            return CapturePixels(processId);
        }
        finally
        {
            if (previous != 0)
            {
                SetThreadDpiAwarenessContext(previous);
            }
        }
    }

    private static ScreenFrame? CapturePixels(int processId)
    {
        using var process = Process.GetProcessById(processId);
        if (!process.ProcessName.StartsWith("DOSBox", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var window = process.MainWindowHandle;
        if (window == 0 || IsIconic(window) || !GetClientRect(window, out var rect)
            || rect.Right is < 256 or > 4096 || rect.Bottom is < 176 or > 2160)
        {
            return null;
        }

        var source = GetDC(window);
        var target = CreateCompatibleDC(source);
        var info = new ScreenBitmapInfo { Size = 40, Width = ScreenFrame.Width,
            Height = -ScreenFrame.Height, Planes = 1, BitCount = 32 };
        var bitmap = CreateDIBSection(target, ref info, 0, out var pixels, 0, 0);
        var full = CreateCompatibleDC(source);
        var fullBitmap = CreateCompatibleBitmap(source, rect.Right, rect.Bottom);
        nint previous = 0;
        nint fullPrevious = 0;
        try
        {
            if (source == 0 || target == 0 || bitmap == 0 || full == 0 || fullBitmap == 0)
            {
                return null;
            }

            previous = SelectObject(target, bitmap);
            fullPrevious = SelectObject(full, fullBitmap);
            // Capture only this DOSBox client area, without foreground changes or desktop pixels.
            if (!PrintWindow(window, full, 3)
                || !GameScreenScale.Copy(full, target, rect.Right, rect.Bottom))
            {
                return null;
            }

            var bgra = new byte[ScreenFrame.Width * ScreenFrame.Height * 4];
            Marshal.Copy(pixels, bgra, 0, bgra.Length);
            var raw = new byte[ScreenFrame.RawBytes];
            for (var index = 0; index < raw.Length / 2; index++)
            {
                var offset = index * 4;
                var colour = (ushort)(((bgra[offset + 2] >> 3) << 11)
                    | ((bgra[offset + 1] >> 2) << 5) | (bgra[offset] >> 3));
                raw[index * 2] = (byte)colour;
                raw[index * 2 + 1] = (byte)(colour >> 8);
            }

            using var output = new MemoryStream();
            using (var compressor = new DeflateStream(output, CompressionLevel.Fastest, true))
            {
                compressor.Write(raw);
            }

            var image = new ScreenFrame(Environment.TickCount64, output.ToArray());
            return image.IsValid ? image : null;
        }
        finally
        {
            if (previous != 0)
            {
                SelectObject(target, previous);
            }
            if (fullPrevious != 0)
            {
                SelectObject(full, fullPrevious);
            }
            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }
            if (fullBitmap != 0)
            {
                DeleteObject(fullBitmap);
            }
            if (target != 0)
            {
                DeleteDC(target);
            }
            if (full != 0)
            {
                DeleteDC(full);
            }
            if (source != 0)
            {
                ReleaseDC(window, source);
            }
        }
    }

    [DllImport("user32.dll")] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out ScreenRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PrintWindow(nint window, nint dc, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref ScreenBitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
}
