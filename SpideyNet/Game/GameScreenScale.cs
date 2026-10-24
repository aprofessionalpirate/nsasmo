using System.Runtime.InteropServices;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class GameScreenScale
{
    internal static bool Copy(nint source, nint target, int width, int height)
    {
        // BLACKONWHITE shrinking ANDs neighbouring pixels, darkening scaled pixel art.
        // COLORONCOLOR preserves a source colour instead of combining its palette bits.
        return SetStretchBltMode(target, 3) != 0
            && StretchBlt(target, 0, 0, ScreenFrame.Width, ScreenFrame.Height,
                source, 0, 0, width, height, 0x00CC0020)
            && GdiFlush();
    }

    // Finish batched GDI drawing before the caller reads the DIB memory directly.
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GdiFlush();

    [DllImport("gdi32.dll")]
    private static extern int SetStretchBltMode(nint dc, int mode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StretchBlt(nint target, int x, int y, int width, int height,
        nint source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint operation);
}
