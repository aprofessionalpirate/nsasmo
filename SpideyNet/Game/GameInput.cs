using System.Runtime.InteropServices;
using SpideyNet.Core;

namespace SpideyNet.Game;

internal sealed class GameInput(int processId)
{
    private const int PushKey = 0x56;
    private const short DownMask = unchecked((short)0x8000);
    private bool wasDown;

    internal PlayerAction Poll()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out var foreground);
        var down = foreground == processId && (GetAsyncKeyState(PushKey) & DownMask) != 0;
        var push = down && !wasDown;
        wasDown = down;
        return push ? PlayerAction.PushPlayer : PlayerAction.Move;
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
