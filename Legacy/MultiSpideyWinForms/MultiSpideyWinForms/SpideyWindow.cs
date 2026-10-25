using System;

namespace MultiSpideyWinForms
{
    public class SpideyWindow
    {
        public IntPtr Handle { get; }
        public IntPtr OriginalParentHandle { get; }
        public long OriginalWindowInformation { get; }
        public int Width { get; }
        public int Height { get; }
        public int BorderlessWidth { get; }
        public int BorderlessHeight { get; }

        public SpideyWindow(IntPtr handle, IntPtr originalParentHandle, long originalWindowInformation, int width, int height, int borderlessWidth, int borderlessHeight)
        {
            Handle = handle;
            OriginalParentHandle = originalParentHandle;
            OriginalWindowInformation = originalWindowInformation;
            Width = width;
            Height = height;
            BorderlessWidth = borderlessWidth;
            BorderlessHeight = borderlessHeight;
        }
    }
}
