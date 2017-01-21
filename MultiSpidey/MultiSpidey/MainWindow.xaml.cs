using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MultiSpidey
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string MaryJaneIsDead = "4D617279204A616E6520697320444541442100";
        const string Credits = "4D7973746572696F202020202020202020202020202020204A2E4A6F6E6168204A616D65736F6E20202020202020202043686565736563616B652020202020202020202020202020456C76696520202020202020202020202020202020202020436F6C696E20202020202020202020202020202020202020466C6173682054686F6D70736F6E202020202020202020204A6F6520526F62657274736F6E2020202020202020202020444D502020202020202020202020202020202020202020204B69636B6168612074686520547269636B737465722020205357472020202020202020202020202020202020202020204B52412020202020202020202020202020202020202020204368616D706965202020202020202020202020202020202052696B2020202020202020202020202020202020202020204D617279204A616E652020202020202020202020202020205065746572205061726B657220202020202020202020202041756E74204D6179";

        // REQUIRED CONSTS
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int MEM_COMMIT = 0x00001000;
        const int PAGE_READWRITE = 0x04;
        const int PROCESS_WM_READ = 0x0010;

        // REQUIRED METHODS
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        // REQUIRED STRUCTS
        public struct MEMORY_BASIC_INFORMATION
        {
            public int BaseAddress;
            public int AllocationBase;
            public int AllocationProtect;
            public int RegionSize;
            public int State;
            public int Protect;
            public int lType;
        }

        public struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public MainWindow()
        {
            InitializeComponent();
            SetWindowToSpidey();

            // getting minimum & maximum address
            SYSTEM_INFO sys_info = new SYSTEM_INFO();
            GetSystemInfo(out sys_info);

            IntPtr proc_min_address = sys_info.minimumApplicationAddress;
            IntPtr proc_max_address = sys_info.maximumApplicationAddress;

            // saving the values as long ints so I won't have to do a lot of casts later
            long proc_min_address_l = (long)proc_min_address;
            long proc_max_address_l = (long)proc_max_address;
            
            Process process = Process.GetProcessesByName("DOSBox")[0];

            // opening the process with desired access level
            IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);
            
            // this will store any information we get from VirtualQueryEx()
            MEMORY_BASIC_INFORMATION mem_basic_info = new MEMORY_BASIC_INFORMATION();

            int bytesRead = 0;  // number of bytes read with ReadProcessMemory
            
            long spideyAddress = 0;
            long levelAddress = 0;
            while (proc_min_address_l < proc_max_address_l)
            {
                // 28 = sizeof(MEMORY_BASIC_INFORMATION)
                VirtualQueryEx(processHandle, proc_min_address, out mem_basic_info, 28);

                // if this memory chunk is accessible
                if (mem_basic_info.Protect == PAGE_READWRITE && mem_basic_info.State == MEM_COMMIT)
                {
                    byte[] buffer = new byte[mem_basic_info.RegionSize];

                    // read everything in the buffer above
                    ReadProcessMemory((int)processHandle, mem_basic_info.BaseAddress, buffer, mem_basic_info.RegionSize, ref bytesRead);

                    var line = new StringBuilder();
                    // then output this in the file
                    for (int i = 0; i < mem_basic_info.RegionSize; i++)
                    {
                        //sw.WriteLine("0x{0} : {1}", (mem_basic_info.BaseAddress + i).ToString("X"), (char)buffer[i]);
                        line.Append(buffer[i].ToString("X"));
                    }

                    int maryJaneIsDeadIndex = 0;
                    int creditsIndex = 0;

                    if ((maryJaneIsDeadIndex = line.ToString().IndexOf(MaryJaneIsDead)) > 0 &&
                        (creditsIndex = line.ToString().IndexOf(Credits)) > 0)
                    {
                        spideyAddress = mem_basic_info.BaseAddress + maryJaneIsDeadIndex / 2 + 32;
                        levelAddress = mem_basic_info.BaseAddress + creditsIndex / 2 + 734;
                        break;
                    }
                }

                // move to the next memory chunk
                proc_min_address_l += mem_basic_info.RegionSize;
                proc_min_address = new IntPtr(proc_min_address_l);
            }
            
            if (spideyAddress > 0 && levelAddress > 0)
            {
                MessageBox.Show(string.Format("Found {0} and {1}", spideyAddress.ToString(), levelAddress.ToString()));
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        private void SetWindowToSpidey()
        {
            var spideyWindow = WindowFinder.FindWindowsWithText("SPIDEY").FirstOrDefault();
            if (spideyWindow == null || spideyWindow == IntPtr.Zero) return;

            ShowActivated = true;
            HwndSourceParameters parameters = new HwndSourceParameters();

            parameters.WindowStyle = 0x10000000 | 0x40000000;
            parameters.SetPosition(0, 0);
            parameters.SetSize((int)Width, (int)Height);
            parameters.ParentWindow = spideyWindow;
            parameters.UsesPerPixelOpacity = true;
            HwndSource src = new HwndSource(parameters);

            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            src.RootVisual = (Visual)Content;

            double dpiX = 96.0;
            double dpiY = 96.0;

            using (var source = new HwndSource(new HwndSourceParameters()))
            {
                dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
            }

            RECT spideyRect;
            GetWindowRect(spideyWindow, out spideyRect);
            
            Width = (spideyRect.Right - spideyRect.Left) * (96.0 / dpiX);
            Height = (spideyRect.Bottom - spideyRect.Top) * (96.0 / dpiY);
        }
    }
}
