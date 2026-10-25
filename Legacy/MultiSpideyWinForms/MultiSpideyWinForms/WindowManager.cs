using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MultiSpideyWinForms
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class WindowManager
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern long SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", CharSet = CharSet.Auto)]
        private static extern long GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", CharSet = CharSet.Auto)]
        private static extern long GetWindowLong64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLong64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int cx, int cy, bool repaint);

        private const int SWP_NOOWNERZORDER = 0x200;
        private const int SWP_NOREDRAW = 0x8;
        private const int SWP_NOZORDER = 0x4;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int WS_EX_MDICHILD = 0x40;
        private const int SWP_FRAMECHANGED = 0x20;
        private const int SWP_NOACTIVATE = 0x10;
        private const int SWP_ASYNCWINDOWPOS = 0x4000;
        private const int SWP_NOMOVE = 0x2;
        private const int SWP_NOSIZE = 0x1;
        private const int GWL_STYLE = (-16);
        private const int WS_VISIBLE = 0x10000000;
        private const int WM_CLOSE = 0x10;
        private const int WS_CHILD = 0x40000000;

        private const string SPIDEY_WINDOW_TITLE = "SPIDEY";

        public static long GetWindowLong(IntPtr hWnd, int nIndex)
        {
            if (!MemoryScanner.Is64BitProcess)
            {
                return GetWindowLong32(hWnd, nIndex);
            }
            else
            {
                return GetWindowLong64(hWnd, nIndex);
            }
        }

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong)
        {
            if (!MemoryScanner.Is64BitProcess)
            {
                return SetWindowLong32(hWnd, nIndex, (IntPtr)dwNewLong);
            }
            else
            {
                return SetWindowLong64(hWnd, nIndex, (IntPtr)dwNewLong);
            }
        }

        public static IEnumerable<IntPtr> FindSpideyWindows()
        {
            return WindowFinder.FindWindowsWithText(SPIDEY_WINDOW_TITLE);
        }

        public static SpideyWindow AttachSpideyWindow(IntPtr handle, IntPtr newParent)
        {
            var originalParentHandle = GetParent(handle);
            var originalWindowInformation = GetWindowLong(handle, GWL_STYLE);

            SetParent(handle, newParent);

            // Have to do this weird thing because height is incorrect on fresh DOSBox
            GetWindowRect(handle, out RECT windowRect);
            var initialWindowWidth = windowRect.Right - windowRect.Left;
            var initialWindowHeight = windowRect.Bottom - windowRect.Top;
            SetParent(handle, originalParentHandle);
            MoveWindow(handle, 0, 0, initialWindowWidth, initialWindowHeight, true);
            SetParent(handle, newParent);
            GetClientRect(handle, out RECT clientRect);
            GetWindowRect(handle, out windowRect);
            var borderlessWidth = clientRect.Right - clientRect.Left;
            var borderlessHeight = clientRect.Bottom - clientRect.Top;
            var width = windowRect.Right - windowRect.Left;
            var height = windowRect.Bottom - windowRect.Top;

            // Remove border and whatnot
            SetWindowLong(handle, GWL_STYLE, WS_VISIBLE);

            // Move the window to overlay it on this window
            MoveWindow(handle, 0, 0, width, height, true);

            return new SpideyWindow(handle, originalParentHandle, originalWindowInformation, width, height, borderlessWidth, borderlessHeight);
        }

        public static SpideyWindow GetSpideyWindow(IntPtr handle)
        {
            var originalParentHandle = GetParent(handle);
            var originalWindowInformation = GetWindowLong(handle, GWL_STYLE);

            GetWindowRect(handle, out RECT windowRect);
            GetClientRect(handle, out RECT clientRect);
            var borderlessWidth = clientRect.Right - clientRect.Left;
            var borderlessHeight = clientRect.Bottom - clientRect.Top;
            var width = windowRect.Right - windowRect.Left;
            var height = windowRect.Bottom - windowRect.Top;

            return new SpideyWindow(handle, originalParentHandle, originalWindowInformation, width, height, borderlessWidth, borderlessHeight);
        }

        public static void UpdateSpideyWindow(SpideyWindow spideyWindow)
        {
            if (spideyWindow != null && spideyWindow.Handle != null && spideyWindow.Handle != IntPtr.Zero)
            {
                // Have move it off 0, 0 first otherwise it won't update
                MoveWindow(spideyWindow.Handle, 1, 1, spideyWindow.Width, spideyWindow.Height, true);
                MoveWindow(spideyWindow.Handle, 0, 0, spideyWindow.Width, spideyWindow.Height, true);
            }
        }

        public static bool DetachSpideyWindow(SpideyWindow spideyWindow)
        {
            // Don't check if OriginalParentHandle is IntPtr.Zero as it will be
            if (spideyWindow != null && 
                spideyWindow.Handle != null && spideyWindow.Handle != IntPtr.Zero &&
                spideyWindow.OriginalParentHandle != null && spideyWindow.OriginalWindowInformation != 0)
            {
                SetParent(spideyWindow.Handle, spideyWindow.OriginalParentHandle);
                SetWindowLong(spideyWindow.Handle, GWL_STYLE, spideyWindow.OriginalWindowInformation);
                MoveWindow(spideyWindow.Handle, 0, 0, spideyWindow.Width, spideyWindow.Height, true);
                return true;
            }
            return false;
        }
    }
}
