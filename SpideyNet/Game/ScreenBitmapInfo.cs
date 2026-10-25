using System.Runtime.InteropServices;

namespace SpideyNet.Game;

[StructLayout(LayoutKind.Sequential)]
internal struct ScreenBitmapInfo
{
    internal uint Size;
    internal int Width;
    internal int Height;
    internal ushort Planes;
    internal ushort BitCount;
    internal uint Compression;
    internal uint SizeImage;
    internal int XPelsPerMeter;
    internal int YPelsPerMeter;
    internal uint ColoursUsed;
    internal uint ColoursImportant;
}
