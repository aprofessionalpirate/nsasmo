using System;

namespace MultiSpideyWinForms
{
    public class SpideyWindow
    {
        public IntPtr Handle { get; set; }
        public IntPtr OriginalParentHandle { get; set; }
        public long OriginalWindowInformation { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int BorderlessWidth { get; set; }
        public int BorderlessHeight { get; set; }
    }
}
