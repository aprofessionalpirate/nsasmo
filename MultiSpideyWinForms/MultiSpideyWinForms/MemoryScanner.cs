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

        private static readonly IntPtr _minimumAddress = IntPtr.Zero;
        private static readonly long _minimumAddressLong = _minimumAddress.ToInt64();
        private static readonly long _maximumAddressLong = 0x7fffffff;
        private static readonly uint _dwLength = Is64BitProcess ? Convert.ToUInt32(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64))) : Convert.ToUInt32(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))); 

        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_WM_READ = 0x0010;

        private const string MARY_JANE_IS_DEAD = "4D617279204A616E65206973204445414421";
        private const string CREDITS = "4D7973746572696F202020202020202020202020202020204A2E4A6F6E6168204A616D65736F6E20202020202020202043686565736563616B652020202020202020202020202020456C76696520202020202020202020202020202020202020436F6C696E20202020202020202020202020202020202020466C6173682054686F6D70736F6E202020202020202020204A6F6520526F62657274736F6E2020202020202020202020444D502020202020202020202020202020202020202020204B69636B6168612074686520547269636B737465722020205357472020202020202020202020202020202020202020204B52412020202020202020202020202020202020202020204368616D706965202020202020202020202020202020202052696B2020202020202020202020202020202020202020204D617279204A616E652020202020202020202020202020205065746572205061726B657220202020202020202020202041756E74204D6179";

        public const int SPIDEY_DATA_SIZE = 48;
        public const int LOCATION_DATA_SIZE = 24;

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

        // Size in bytes = 28
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

        // Size in bytes = 48 (double check this)
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
            {
                return false;
            }

            if ((Is64BitProcess || IsProcessWow64(Process.GetCurrentProcess())) && !IsProcessWow64(process))
            {
                form.Invoke(new Action(() => { MessageBox.Show("DOSBox process is 64-bit, please use 32-bit DOSBox"); }));
                return false;
            }

            // Open the process with desired access level
            var dosBoxProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);
            var dosBoxProcessInt32 = dosBoxProcess.ToInt32();

            long spideyAddress = 0;
            long levelAddress = 0;

            var bytesRead = 0;  // Number of bytes read with ReadProcessMemory

            var currentAddress = _minimumAddress;
            var currentAddressLong = _minimumAddressLong;
            while (currentAddressLong < _maximumAddressLong)
            {
                int result;
                AllocationProtectEnum protection;
                StateEnum state;
                int regionSize;
                int baseAddress;
                if (!Is64BitProcess)
                {
                    result = VirtualQueryEx(dosBoxProcess, currentAddress, out MEMORY_BASIC_INFORMATION memoryBasicInformation, _dwLength);
                    protection = memoryBasicInformation.Protect;
                    state = memoryBasicInformation.State;
                    regionSize = Convert.ToInt32(memoryBasicInformation.RegionSize);
                    baseAddress = memoryBasicInformation.BaseAddress.ToInt32();
                }
                else
                {
                    result = VirtualQueryEx(dosBoxProcess, currentAddress, out MEMORY_BASIC_INFORMATION64 memoryBasicInformation, _dwLength);
                    protection = memoryBasicInformation.Protect;
                    state = memoryBasicInformation.State;
                    regionSize = Convert.ToInt32(memoryBasicInformation.RegionSize);
                    baseAddress = memoryBasicInformation.BaseAddress.ToInt32();
                }

                if (result == 0)
                {
                    form.Invoke(new Action(() => { MessageBox.Show("VirtualQueryEx failed"); }));
                    break;
                }

                // Check if this memory chunk is accessible
                // Could also just check if protection != PAGE_GUARD (possibly more to check) to access more memory areas
                if (protection == AllocationProtectEnum.PAGE_READWRITE && state == StateEnum.MEM_COMMIT)
                {
                    var buffer = new byte[regionSize];

                    // Read everything into buffer
                    ReadProcessMemory(dosBoxProcessInt32, baseAddress, buffer, regionSize, ref bytesRead);

                    // TODO - refactor this to make it faster, maybe don't convert everything to string?
                    var line = new StringBuilder();
                    for (var i = 0; i < regionSize; i++)
                    {
                        //sw.WriteLine("0x{0} : {1}", ((int)mem_basic_info.BaseAddress + i).ToString("X"), (char)buffer[i]);
                        line.Append(buffer[i].ToString("X2"));
                    }

                    var maryJaneIsDeadIndex = 0;
                    var creditsIndex = 0;

                    if ((maryJaneIsDeadIndex = line.ToString().IndexOf(MARY_JANE_IS_DEAD)) > 0 &&
                        (creditsIndex = line.ToString().IndexOf(CREDITS)) > 0)
                    {
                        spideyAddress = Convert.ToInt64(baseAddress + maryJaneIsDeadIndex / 2 + MARY_JANE_IS_DEAD.Length / 2);
                        levelAddress = Convert.ToInt64(baseAddress + creditsIndex / 2 + CREDITS.Length / 2 + 734);
                        break;
                    }
                }

                // Move to the next memory chunk
                currentAddress += regionSize;
                currentAddressLong += regionSize;
            }

            Interlocked.Exchange(ref _dosBoxProcess, dosBoxProcess.ToInt64());
            Interlocked.Exchange(ref _spideyAddress, spideyAddress);
            Interlocked.Exchange(ref _levelAddress, levelAddress);

            return (spideyAddress > 0 && levelAddress > 0);
        }

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

        public static byte[] ReadSpideyData()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);

            var spideyBuffer = new byte[SPIDEY_DATA_SIZE];
            if (spideyAddress != 0)
            {
                int bytesRead = 0;
                ReadProcessMemory(Convert.ToInt32(dosBoxProcess), Convert.ToInt32(spideyAddress), spideyBuffer, SPIDEY_DATA_SIZE, ref bytesRead);
            }
            return spideyBuffer;
        }

        public static byte[] ReadLocationData()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var levelAddress = Interlocked.Read(ref _levelAddress);

            var levelBuffer = new byte[LOCATION_DATA_SIZE];
            if (levelAddress != 0)
            {
                int bytesRead = 0;
                ReadProcessMemory(Convert.ToInt32(dosBoxProcess), Convert.ToInt32(levelAddress), levelBuffer, LOCATION_DATA_SIZE, ref bytesRead);
            }

            return levelBuffer;
        }
    }
}
