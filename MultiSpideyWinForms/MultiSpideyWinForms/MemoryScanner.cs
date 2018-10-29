using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MultiSpideyWinForms
{
    public static class MemoryScanner
    {
        public static readonly bool Is64BitProcess = IntPtr.Size == 8;

        private static readonly byte[] DEAD_ENEMY_PLACEHOLDER = new byte[] { 0x00, 0x24, 0xF3, 0xFF, 0x34, 0x00, 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5F, 0x00, 0x00, 0x89, 0x1D, 0x01, 0x00, 0x8D, 0xA5, 0x82, 0xA5, 0x00, 0x04, 0x01, 0x00, 0x00, 0x00, 0x0D, 0x10, 0x53, 0x01, 0xFB, 0xFF, 0x07, 0x00, 0x39, 0x48, 0x00, 0x00, 0x00, 0x83, 0xA5, 0xF2, 0xFF, 0x33, 0x00 };
        private static readonly byte[] PLAYER_DATA_HALF_PLACEHOLDER = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x0E, 0x0E, 0x98, 0x92, 0x22, 0x00, 0x2F, 0x00, 0x9A, 0xA7, 0xC0, 0xC0, 0x00, 0x9A, 0x9B, 0x22, 0x00, 0x93, 0x02 };
        private static readonly byte[] PLAYER_DATA_HALF_PLACEHOLDER_CRAZY = new byte[] { 0x04, 0x01, 0x00, 0x00, 0x00, 0x0D, 0x10, 0x53, 0x01, 0xFB, 0xFF, 0x07, 0x00, 0x39, 0x48, 0xC0, 0x00, 0x00, 0x83, 0xA5, 0xF2, 0xFF, 0x33, 0x00 };

        private static long _spideyLevelCheatAddress;
        private static long _enemyCountAddress;
        private static long _spideyAddress;
        private static long _levelAddress;
        private static long _dosBoxProcess;

        private static byte _highestEnemyCount;

        private static readonly IntPtr _minimumAddress = IntPtr.Zero;
        private static readonly long _minimumAddressLong = _minimumAddress.ToInt64();
        private static readonly long _maximumAddressLong = 0x7fffffff;
        private static readonly uint _dwLength = Is64BitProcess ? Convert.ToUInt32(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION64))) : Convert.ToUInt32(Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))); 

        private const string K_KEYS = "4B2D4B657973";
        private const string DEAD = "4445414421";
        private const string ERROR_READING_FILES = "4572726F722072656164696E672066696C6573";
        private const int SPIDEY_LEVEL_CHEAT_OFFSET = 40;
        private const int ENEMY_COUNT_OFFSET = -38;
        private const int LEVEL_OFFSET = 1266;
        private const int LEVEL_NAME_OFFSET = 1279;
        private const int SPIDEY_X_OFFSET = 2;
        private const int SPIDEY_Y_OFFSET = 4;

        public const int SPIDEY_LEVEL_CHEAT_DATA_SIZE = 1;
        public const int ENEMY_COUNT_DATA_SIZE = 1;
        public const int ENEMY_HEADER_SIZE = 2;
        public const int SPIDEY_DATA_SIZE = 48;
        public const int LEVEL_DATA_SIZE = 1;
        public const int LEVEL_NAME_DATA_SIZE = 24;
        public const int SPIDEY_X_DATA_SIZE = 1;
        public const int SPIDEY_Y_DATA_SIZE = 1;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int CloseHandle(IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

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

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }

        [Flags]
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

        [Flags]
        public enum StateEnum : uint
        {
            MEM_COMMIT = 0x1000,
            MEM_FREE = 0x10000,
            MEM_RESERVE = 0x2000
        }

        [Flags]
        public enum TypeEnum : uint
        {
            MEM_IMAGE = 0x1000000,
            MEM_MAPPED = 0x40000,
            MEM_PRIVATE = 0x20000
        }

        public static bool GetMemoryAddresses(out string error, IntPtr window, bool getSpideyCheat = false)
        {
            error = "";

            GetWindowThreadProcessId(window, out uint processId);
            var process = Process.GetProcessById((int)processId);

            if (process == null)
            {
                return false;
            }

            if ((Is64BitProcess || IsProcessWow64(Process.GetCurrentProcess())) && !IsProcessWow64(process))
            {
                error = "DOSBox process is 64-bit, please use 32-bit DOSBox";
                return false;
            }

            // Open the process with desired access level
            var dosBoxProcess = OpenProcess(ProcessAccessFlags.QueryInformation | ProcessAccessFlags.VMRead | ProcessAccessFlags.VMWrite, false, process.Id);

            long spideyLevelCheatAddress = 0;
            long enemyCountAddress = 0;
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
                uint regionSize;
                IntPtr baseAddress;
                if (!Is64BitProcess)
                {
                    result = VirtualQueryEx(dosBoxProcess, currentAddress, out MEMORY_BASIC_INFORMATION memoryBasicInformation, _dwLength);
                    protection = memoryBasicInformation.Protect;
                    state = memoryBasicInformation.State;
                    regionSize = memoryBasicInformation.RegionSize;
                    baseAddress = memoryBasicInformation.BaseAddress;
                }
                else
                {
                    result = VirtualQueryEx(dosBoxProcess, currentAddress, out MEMORY_BASIC_INFORMATION64 memoryBasicInformation, _dwLength);
                    protection = memoryBasicInformation.Protect;
                    state = memoryBasicInformation.State;
                    regionSize = Convert.ToUInt32(memoryBasicInformation.RegionSize);
                    baseAddress = memoryBasicInformation.BaseAddress;
                }

                if (result == 0)
                {
                    error = "VirtualQueryEx failed";
                    break;
                }

                // Check if this memory chunk is accessible
                // Could also just check if protection != PAGE_GUARD (possibly more to check) to access more memory areas
                if (protection == AllocationProtectEnum.PAGE_READWRITE && state == StateEnum.MEM_COMMIT)
                {
                    var buffer = new byte[regionSize];

                    // Read everything into buffer
                    ReadProcessMemory(dosBoxProcess, baseAddress, buffer, regionSize, out bytesRead);

                    // TODO - refactor this to make it faster, maybe don't convert everything to string?
                    var lineBuilder = new StringBuilder();
                    for (var i = 0; i < regionSize; i++)
                    {
                        //sw.WriteLine("0x{0} : {1}", ((int)mem_basic_info.BaseAddress + i).ToString("X"), (char)buffer[i]);
                        lineBuilder.Append(buffer[i].ToString("X2"));
                    }

                    var deadIndex = 0;
                    var errorReadingFilesIndex = 0;

                    var line = lineBuilder.ToString();
                    if ((deadIndex = line.IndexOf(DEAD)) > 0 &&
                        (errorReadingFilesIndex = line.IndexOf(ERROR_READING_FILES)) > 0)
                    {
                        enemyCountAddress = baseAddress.ToInt32() + (deadIndex / 2) + (DEAD.Length / 2) + ENEMY_COUNT_OFFSET;
                        spideyAddress = baseAddress.ToInt32() + (deadIndex / 2) + (DEAD.Length / 2);
                        levelAddress = baseAddress.ToInt32() + (errorReadingFilesIndex / 2) + (ERROR_READING_FILES.Length / 2) + LEVEL_OFFSET;

                        // TODO - find a more appropriate spot to put this
                        _highestEnemyCount = SpideyLevels.HighestEnemyCount;
                        if (getSpideyCheat)
                        {
                            var spideyCheatIndex = line.IndexOf(K_KEYS);
                            spideyLevelCheatAddress = baseAddress.ToInt32() + (spideyCheatIndex / 2) + (K_KEYS.Length / 2) + SPIDEY_LEVEL_CHEAT_OFFSET;
                        }
                        break;
                    }
                }

                // Move to the next memory chunk
                currentAddress = IntPtr.Add(currentAddress, Convert.ToInt32(regionSize));
                currentAddressLong += regionSize;
            }

            Interlocked.Exchange(ref _dosBoxProcess, dosBoxProcess.ToInt64());
            Interlocked.Exchange(ref _enemyCountAddress, enemyCountAddress);
            Interlocked.Exchange(ref _spideyAddress, spideyAddress);
            Interlocked.Exchange(ref _levelAddress, levelAddress);
            if (getSpideyCheat) Interlocked.Exchange(ref _spideyLevelCheatAddress, spideyLevelCheatAddress);

            return (enemyCountAddress > 0 && spideyAddress > 0 && levelAddress > 0);
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

        public static void CloseDosBoxHandle()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);

            if (dosBoxProcess != 0)
            {
                CloseHandle(new IntPtr(dosBoxProcess));
            }
        }

        public static byte ReadSpideyLevelCheatData()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyLevelCheatAddress = Interlocked.Read(ref _spideyLevelCheatAddress);

            var spideyLevelCheatBuffer = new byte[SPIDEY_LEVEL_CHEAT_DATA_SIZE];
            if (spideyLevelCheatAddress != 0)
            {
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyLevelCheatAddress), spideyLevelCheatBuffer, SPIDEY_LEVEL_CHEAT_DATA_SIZE, out int bytesRead);
            }
            return spideyLevelCheatBuffer[0];
        }

        public static byte[] ReadSpideyData()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);

            var spideyBuffer = new byte[SPIDEY_DATA_SIZE];
            if (spideyAddress != 0)
            {
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress), spideyBuffer, SPIDEY_DATA_SIZE, out int bytesRead);
            }
            return spideyBuffer;
        }

        public static byte ReadLevelData()
        {
            return ReadLevelData(Interlocked.Read(ref _dosBoxProcess));
        }

        private static byte ReadLevelData(long dosBoxProcess)
        {
            var levelAddress = Interlocked.Read(ref _levelAddress);

            var levelBuffer = new byte[LEVEL_DATA_SIZE];
            if (levelAddress != 0)
            {
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(levelAddress), levelBuffer, LEVEL_DATA_SIZE, out int bytesRead);
            }

            return levelBuffer[0];
        }

        public static byte[] ReadLevelNameData()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var levelAddress = Interlocked.Read(ref _levelAddress);
            var levelNameAddress = levelAddress + (LEVEL_NAME_OFFSET - LEVEL_OFFSET);

            var levelNameBuffer = new byte[LEVEL_NAME_DATA_SIZE];
            if (levelNameAddress != 0)
            {
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(levelNameAddress), levelNameBuffer, LEVEL_NAME_DATA_SIZE, out int bytesRead);
            }

            return levelNameBuffer;
        }

        public static byte ReadEnemyCountData()
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var enemyCountAddress = Interlocked.Read(ref _enemyCountAddress);

            var enemyCountBuffer = new byte[ENEMY_COUNT_DATA_SIZE];
            if (enemyCountAddress != 0)
            {
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), enemyCountBuffer, ENEMY_COUNT_DATA_SIZE, out int bytesRead);
            }
            return enemyCountBuffer[0];
        }

        public static void WriteSpideyLevelCheatData(byte spideyLevel)
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyLevelCheatAddress = Interlocked.Read(ref _spideyLevelCheatAddress);

            if (spideyLevelCheatAddress != 0)
            {
                WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyLevelCheatAddress), new byte[] { spideyLevel }, SPIDEY_LEVEL_CHEAT_DATA_SIZE, out int bytesWritten);
            }
        }

        public static void WriteSpideyXAndYPosition(byte spideyXPos, byte spideyYPos)
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);

            if (spideyAddress != 0)
            {
                WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_X_OFFSET), new byte[] { spideyXPos }, SPIDEY_X_DATA_SIZE, out int bytesWritten);
                WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_Y_OFFSET), new byte[] { spideyYPos }, SPIDEY_Y_DATA_SIZE, out bytesWritten);
            }
        }

        public static void WriteSpideyData(byte[] spideyData, SpideyLevel playerSpideyLevel, int playerOffset, int playerCount)
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var enemyCountAddress = Interlocked.Read(ref _enemyCountAddress);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);
            int bytesWritten;

            if (enemyCountAddress != 0)
            {
                var newEnemyCount = Convert.ToByte(_highestEnemyCount + playerCount);
                WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), new byte[] { newEnemyCount }, ENEMY_COUNT_DATA_SIZE, out bytesWritten);
            }

            var mySpideyLevel = SpideyLevels.GetSpideyLevel(ReadLevelData(dosBoxProcess));

            if (spideyAddress != 0)
            {
                // Null out any extra enemy data that shouldn't be there
                var invalidEnemies = _highestEnemyCount - mySpideyLevel.EnemyCount;
                while (invalidEnemies > 0)
                {
                    var enemyIndex = _highestEnemyCount - invalidEnemies - 1;
                    var enemyMemoryOffset = (ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE) * enemyIndex;
                    var enemyData = new byte[ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE];
                    enemyData[0] = 0xC0;
                    enemyData[1] = Convert.ToByte(enemyIndex);
                    Buffer.BlockCopy(DEAD_ENEMY_PLACEHOLDER, 0, enemyData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE);
                    WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset), enemyData, ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE, out bytesWritten);
                    --invalidEnemies;
                }

                var playerIndex = _highestEnemyCount + playerOffset - 1;
                var playeMemoryOffset = (ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE) * playerIndex;
                var playerData = new byte[ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE];
                playerData[0] = 0xC0;
                playerData[1] = Convert.ToByte(playerIndex);

                if (playerSpideyLevel.Number == mySpideyLevel.Number)
                {
                    Buffer.BlockCopy(spideyData, 0, playerData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE);

                    // Cutting out half the data seems to fix graphical glitching issues and disables interaction with other player's game
                    // Maybe not
                    //Buffer.BlockCopy(spideyData, 0, playerData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE / 2);
                    //Buffer.BlockCopy(PLAYER_DATA_HALF_PLACEHOLDER, 0, playerData, ENEMY_HEADER_SIZE + (SPIDEY_DATA_SIZE / 2), SPIDEY_DATA_SIZE / 2);
                }
                else
                {
                    Buffer.BlockCopy(DEAD_ENEMY_PLACEHOLDER, 0, playerData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE);
                }
                WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + playeMemoryOffset), playerData, ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE, out bytesWritten);
            }
        }
    }
}
