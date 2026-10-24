using System.Runtime.InteropServices;

namespace SpideyNet.Game;

[StructLayout(LayoutKind.Sequential)]
internal struct ScreenRect
{
    internal int Left;
    internal int Top;
    internal int Right;
    internal int Bottom;
}
