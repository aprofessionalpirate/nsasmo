using MultiSpideyWinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SpideyCheatParser
{
    public class Program
    {
        public static ASCIIEncoding AsciiEncoding = new ASCIIEncoding();

        // Need to make sure Spidey is frozen via Cheat Engine so levels don't change
        // Dump number + level title + enemy count
        // Also, send number rather than level title and just read from static list
        public static void Main(string[] args)
        {
            IEnumerable<IntPtr> handles = null;

            while (handles == null || handles.Count() == 0 || handles.All(h => h == IntPtr.Zero))
            {
                handles = WindowManager.FindSpideyWindows();
                Thread.Sleep(100);
            }

            var spideyWindow = WindowManager.GetSpideyWindow(handles.First());
            while (!MemoryScanner.GetMemoryAddresses(out string error, spideyWindow.Handle, true))
            {
                Thread.Sleep(100);
            }

            MemoryScanner.WriteSpideyXAndYPosition(0x77, 0x77);

            using (var output = new StreamWriter("Levels.txt", false))
            {
                for (byte levelNumber = 0x00; levelNumber <= 0x3F; ++levelNumber)
                {
                    MemoryScanner.WriteSpideyLevelCheatData(levelNumber);
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    while (stopwatch.ElapsedMilliseconds < 500)
                    {
                        MemoryScanner.WriteSpideyXAndYPosition(0x77, 0x77);
                    }
                    var name = AsciiEncoding.GetString(MemoryScanner.ReadLocationData());
                    var enemyCount = MemoryScanner.ReadEnemyCountData();
                    output.WriteLine(levelNumber.ToString("X2") + "|" + name + "|" + enemyCount.ToString("X2"));
                }
            }
        }
    }
}
