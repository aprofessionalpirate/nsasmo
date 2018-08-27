using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MultiSpideyWinForms
{
    public static class MemoryScanner
    {
        public static readonly bool Is64BitProcess = (IntPtr.Size == 8);

        private static long _spideyAddress;
        private static long _levelAddress;
        private static long _dosBoxProcess;

        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_WM_READ = 0x0010;

        private const string MARY_JANE_IS_DEAD = "4D617279204A616E6520697320444541442100";
        private const string CREDITS = "4D7973746572696F202020202020202020202020202020204A2E4A6F6E6168204A616D65736F6E20202020202020202043686565736563616B652020202020202020202020202020456C76696520202020202020202020202020202020202020436F6C696E20202020202020202020202020202020202020466C6173682054686F6D70736F6E202020202020202020204A6F6520526F62657274736F6E2020202020202020202020444D502020202020202020202020202020202020202020204B69636B6168612074686520547269636B737465722020205357472020202020202020202020202020202020202020204B52412020202020202020202020202020202020202020204368616D706965202020202020202020202020202020202052696B2020202020202020202020202020202020202020204D617279204A616E652020202020202020202020202020205065746572205061726B657220202020202020202020202041756E74204D6179";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION64 lpBuffer, uint dwLength);
        
        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool wow64Process
        );

        private static bool IsProcessWow64(Process process)
        {
            if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
                Environment.OSVersion.Version.Major >= 6)
            {
                bool retVal;
                if (!IsWow64Process(process.Handle, out retVal))
                {
                    return false;
                }
                return retVal;
            }
            else
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public AllocationProtectEnum AllocationProtect;
            public uint RegionSize;
            public StateEnum State;
            public AllocationProtectEnum Protect;
            public TypeEnum Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION64
        {
            /// <summary>
            /// A pointer to the base address of the region of pages.
            /// </summary>
            public IntPtr BaseAddress;
            /// <summary>
            /// A pointer to the base address of a range of pages allocated by the VirtualAlloc function. The page pointed to by the BaseAddress member is contained within this allocation range.
            /// </summary>
            public IntPtr AllocationBase;
            /// <summary>
            /// The memory protection option when the region was initially allocated. This member can be one of the memory protection constants or 0 if the caller does not have access.
            /// </summary>
            public AllocationProtectEnum AllocationProtect;
            /// <summary>
            /// Required in the 64 bit struct. Blame Windows.
            /// </summary>
            public uint __alignment1;
            /// <summary>
            /// The size of the region beginning at the base address in which all pages have identical attributes, in bytes.
            /// </summary>
            public long RegionSize;
            /// <summary>
            /// The state of the pages in the region.
            /// </summary>
            public StateEnum State;
            /// <summary>
            /// The access protection of the pages in the region. This member is one of the values listed for the AllocationProtect member.
            /// </summary>
            public AllocationProtectEnum Protect;
            /// <summary>
            /// The type of pages in the region.
            /// </summary>
            public TypeEnum Type;
            /// <summary>
            /// Required in the 64 bit struct. Blame Windows.
            /// </summary>
            public uint __alignment2;
        };

        public enum AllocationProtectEnum : uint
        {
            PAGE_EXECUTE = 0x00000010,
            PAGE_EXECUTE_READ = 0x00000020,
            PAGE_EXECUTE_READWRITE = 0x00000040,
            PAGE_EXECUTE_WRITECOPY = 0x00000080,
            PAGE_NOACCESS = 0x00000001,
            PAGE_READONLY = 0x00000002,
            PAGE_READWRITE = 0x00000004,
            PAGE_WRITECOPY = 0x00000008,
            PAGE_GUARD = 0x00000100,
            PAGE_NOCACHE = 0x00000200,
            PAGE_WRITECOMBINE = 0x00000400
        }

        public enum StateEnum : uint
        {
            MEM_COMMIT = 0x1000,
            MEM_FREE = 0x10000,
            MEM_RESERVE = 0x2000
        }

        public enum TypeEnum : uint
        {
            MEM_IMAGE = 0x1000000,
            MEM_MAPPED = 0x40000,
            MEM_PRIVATE = 0x20000
        }

        public static bool GetMemoryAddresses(Form form, IntPtr window)
        {
            GetWindowThreadProcessId(window, out uint processId);
            var process = Process.GetProcessById((int)processId);

            if (process == null)
                return false;

            // Getting minimum & maximum address
            if ((Is64BitProcess || IsProcessWow64(Process.GetCurrentProcess())) && !IsProcessWow64(process))
            {
                form.Invoke(new Action(() => { MessageBox.Show("DOSBox process is 64-bit, please use 32-bit DOSBox"); }));
                return false;
            }

            IntPtr proc_min_address = IntPtr.Zero;

            // saving the values as long ints so I won't have to do a lot of casts later
            long proc_min_address_l = (long)proc_min_address;
            long proc_max_address_l = 0x7fffffff;

            // opening the process with desired access level
            var dosBoxProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);

            long spideyAddress = 0;
            long levelAddress = 0;
            if (!Is64BitProcess)
            {
                // this will store any information we get from VirtualQueryEx()
                var mem_basic_info = new MEMORY_BASIC_INFORMATION();
                int bytesRead = 0;  // number of bytes read with ReadProcessMemory
                var dwLength = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)); // 28 = sizeof(MEMORY_BASIC_INFORMATION)
                while (proc_min_address_l < proc_max_address_l)
                {
                    var result = VirtualQueryEx(dosBoxProcess, proc_min_address, out mem_basic_info, dwLength);
                    if (result == 0)
                    {
                        form.Invoke(new Action(() => { MessageBox.Show("VirtualQueryEx failed"); }));
                        break;
                    }

                    // if this memory chunk is accessible
                    // Could also just check if .Protect != PAGE_GUARD (possibly more to check) to access more memory areas
                    if (mem_basic_info.Protect == AllocationProtectEnum.PAGE_READWRITE && mem_basic_info.State == StateEnum.MEM_COMMIT)
                    {
                        var buffer = new byte[mem_basic_info.RegionSize];

                        // read everything in the buffer above
                        ReadProcessMemory((int)dosBoxProcess, (int)mem_basic_info.BaseAddress, buffer, (int)mem_basic_info.RegionSize, ref bytesRead);

                        var line = new StringBuilder();
                        for (int i = 0; i < mem_basic_info.RegionSize; i++)
                        {
                            //sw.WriteLine("0x{0} : {1}", ((int)mem_basic_info.BaseAddress + i).ToString("X"), (char)buffer[i]);
                            line.Append(buffer[i].ToString("X2"));
                        }

                        int maryJaneIsDeadIndex = 0;
                        int creditsIndex = 0;

                        if ((maryJaneIsDeadIndex = line.ToString().IndexOf(MARY_JANE_IS_DEAD)) > 0 &&
                            (creditsIndex = line.ToString().IndexOf(CREDITS)) > 0)
                        {
                            spideyAddress = Convert.ToInt64(mem_basic_info.BaseAddress.ToInt32() + maryJaneIsDeadIndex / 2 + MARY_JANE_IS_DEAD.Length / 2 + 32);
                            levelAddress = Convert.ToInt64(mem_basic_info.BaseAddress.ToInt32() + creditsIndex / 2 + CREDITS.Length / 2 + 734);
                            break;
                        }
                    }

                    // move to the next memory chunk
                    proc_min_address_l += mem_basic_info.RegionSize;
                    proc_min_address = new IntPtr(proc_min_address_l);
                }
            }
            else
            {
                // this will store any information we get from VirtualQueryEx()
                var mem_basic_info64 = new MEMORY_BASIC_INFORMATION64();
                int bytesRead = 0;  // number of bytes read with ReadProcessMemory
                var dwLength = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64)); // 28 = sizeof(MEMORY_BASIC_INFORMATION)

                while (proc_min_address_l < proc_max_address_l)
                {
                    var result = VirtualQueryEx(dosBoxProcess, proc_min_address, out mem_basic_info64, dwLength);
                    if (result == 0)
                    {
                        form.Invoke(new Action(() => { MessageBox.Show("VirtualQueryEx failed"); }));
                        break;
                    }

                    // if this memory chunk is accessible
                    // Could also just check if .Protect != PAGE_GUARD (possibly more to check) to access more memory areas
                    if (mem_basic_info64.Protect == AllocationProtectEnum.PAGE_READWRITE && mem_basic_info64.State == StateEnum.MEM_COMMIT)
                    {
                        var buffer = new byte[mem_basic_info64.RegionSize];

                        // read everything in the buffer above
                        ReadProcessMemory((int)dosBoxProcess, (int)mem_basic_info64.BaseAddress, buffer, (int)mem_basic_info64.RegionSize, ref bytesRead);

                        var line = new StringBuilder();
                        for (int i = 0; i < mem_basic_info64.RegionSize; i++)
                        {
                            //sw.WriteLine("0x{0} : {1}", ((int)mem_basic_info.BaseAddress + i).ToString("X"), (char)buffer[i]);
                            line.Append(buffer[i].ToString("X2"));
                        }

                        int maryJaneIsDeadIndex = 0;
                        int creditsIndex = 0;

                        if ((maryJaneIsDeadIndex = line.ToString().IndexOf(MARY_JANE_IS_DEAD)) > 0 &&
                            (creditsIndex = line.ToString().IndexOf(CREDITS)) > 0)
                        {
                            spideyAddress = Convert.ToInt64(mem_basic_info64.BaseAddress + maryJaneIsDeadIndex / 2 + MARY_JANE_IS_DEAD.Length / 2 + 32);
                            levelAddress = Convert.ToInt64(mem_basic_info64.BaseAddress + creditsIndex / 2 + CREDITS.Length / 2 + 734);
                            break;
                        }
                    }

                    // move to the next memory chunk
                    proc_min_address_l += mem_basic_info64.RegionSize;
                    proc_min_address = new IntPtr(proc_min_address_l);
                }
            }

            Interlocked.Exchange(ref _dosBoxProcess, dosBoxProcess.ToInt64());
            Interlocked.Exchange(ref _spideyAddress, spideyAddress);
            Interlocked.Exchange(ref _levelAddress, levelAddress);

            return (spideyAddress > 0 && levelAddress > 0);
        }

        public static byte[] ReadSpideyPosition()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);

            var spideyBuffer = new byte[6];
            if (spideyAddress != 0)
            {
                int bytesRead = 0;
                ReadProcessMemory(Convert.ToInt32(dosBoxProcess), Convert.ToInt32(spideyAddress), spideyBuffer, 6, ref bytesRead);
            }
            return spideyBuffer;
        }

        public static byte[] ReadLevelTitle()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var levelAddress = Interlocked.Read(ref _levelAddress);

            var levelBuffer = new byte[24];
            if (levelAddress != 0)
            {
                int bytesRead = 0;
                ReadProcessMemory(Convert.ToInt32(dosBoxProcess), Convert.ToInt32(levelAddress), levelBuffer, 24, ref bytesRead);
            }

            return levelBuffer;
        }
    }
}
