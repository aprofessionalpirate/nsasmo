using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace MultiSpideyWinForms
{
    public static class MemoryScanner
    {
        public static readonly bool Is64BitProcess = IntPtr.Size == 8;

        private static readonly byte[] DEAD_ENEMY_PLACEHOLDER = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x5F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        private static readonly byte[] PLAYER_DATA_HALF_PLACEHOLDER = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

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

        public static ConcurrentDictionary<int, byte> SpriteOverrides = new ConcurrentDictionary<int, byte>();

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

        public static void PrepSpideyData(int playerCount)
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);
            int bytesWritten;
            if (spideyAddress != 0)
            {
                //var playerData = new byte[ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE];
                //playerData[0] = 0xC0;
                //Buffer.BlockCopy(DEAD_ENEMY_PLACEHOLDER, 0, playerData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE);

                for (var playerIndex = 0; playerIndex < playerCount; ++playerIndex)
                {
                    var spideyIndex = playerIndex + (_highestEnemyCount - 1);
                    //playerData[1] = Convert.ToByte(spideyIndex);
                    var playerMemoryOffset = (ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE) * spideyIndex;

                    var singleByteBuffer = new byte[1];
                    // Just the essential enemy bits
                    // Blank sprite
                    var lSpriteAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 8;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lSpriteAddress), singleByteBuffer, 1, out int bytesRead);
                    if (singleByteBuffer[0] < 0x50 || singleByteBuffer[0] > 0x5F)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lSpriteAddress), new byte[] { 0x5F }, 1, out bytesWritten);
                    }

                    // To avoid corruption
                    var lCorruptionOneAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 31;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionOneAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] == 0x00)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionOneAddress), new byte[] { 0x01 }, 1, out bytesWritten);
                    }

                    // To avoid corruption
                    var lCorruptionTwoAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 34;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionTwoAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] == 0x00)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionTwoAddress), new byte[] { 0x01 }, 1, out bytesWritten);
                    }

                    // Enemy health
                    var lEnemyHealthAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 41;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lEnemyHealthAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] != 0x00)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lEnemyHealthAddress), new byte[] { 0x00 }, 1, out bytesWritten);
                    }
                }
            }
        }

        // TODO - looks like 0xC0 is not so straightforward - not just an array count
        // e.g. MJ = FF 04, Rat = C0 81??
        public static void WriteSpideyData(byte[] spideyData, SpideyLevel playerSpideyLevel, int playerOffset, int playerCount)
        {
            var dosBoxProcess = Interlocked.Read(ref _dosBoxProcess);
            var enemyCountAddress = Interlocked.Read(ref _enemyCountAddress);
            var spideyAddress = Interlocked.Read(ref _spideyAddress);
            int bytesWritten;

            var mySpideyLevel = SpideyLevels.GetSpideyLevel(ReadLevelData(dosBoxProcess));

            // TODO - fix teleporting enemy issue - prolly to do with how they get stored in array
            if (enemyCountAddress != 0 && spideyAddress != 0)
            {
                var desiredEnemyCount = Convert.ToByte(_highestEnemyCount + playerCount);
                var enemyCountBuffer = new byte[ENEMY_COUNT_DATA_SIZE];
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), enemyCountBuffer, ENEMY_COUNT_DATA_SIZE, out int bytesRead);
                var enemyCountNeedsUpdate = false;
                if (enemyCountBuffer[0] < desiredEnemyCount)
                {
                    enemyCountNeedsUpdate = true;

                    // Null out any extra enemy data that shouldn't be there
                    var invalidEnemies = _highestEnemyCount - mySpideyLevel.EnemyCount;
                    var enemyData = new byte[ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE];
                    //enemyData[0] = 0xC0;
                    Buffer.BlockCopy(DEAD_ENEMY_PLACEHOLDER, 0, enemyData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE);
                    while (invalidEnemies > 0)
                    {
                        // TODO - Try copy the first enemy to other spots as well so it loads the right values?

                        var enemyIndex = _highestEnemyCount - invalidEnemies - 1;
                        var enemyMemoryOffset = (ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE) * enemyIndex;
                        //WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset), new byte[] { 0xC0 }, 1, out bytesWritten);
                        //WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 1), new byte[] { 0x01 }, 1, out bytesWritten);

                        var singleByteBuffer = new byte[1];
                        // Just the essential enemy bits
                        // Blank sprite
                        var lSpriteAddress = spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 8;
                        ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lSpriteAddress), singleByteBuffer, 1, out bytesRead);
                        if (singleByteBuffer[0] < 0x50 || singleByteBuffer[0] > 0x5F)
                        {
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lSpriteAddress), new byte[] { 0x5F }, 1, out bytesWritten);
                        }

                        // To avoid corruption
                        var lCorruptionOneAddress = spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 31;
                        ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionOneAddress), singleByteBuffer, 1, out bytesRead);
                        if (singleByteBuffer[0] == 0x00)
                        {
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionOneAddress), new byte[] { 0x01 }, 1, out bytesWritten);
                        }

                        // To avoid corruption
                        var lCorruptionTwoAddress = spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 34;
                        ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionTwoAddress), singleByteBuffer, 1, out bytesRead);
                        if (singleByteBuffer[0] == 0x00)
                        {
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionTwoAddress), new byte[] { 0x01 }, 1, out bytesWritten);
                        }

                        // Enemy health
                        var lEnemyHealthAddress = spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 41;
                        ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lEnemyHealthAddress), singleByteBuffer, 1, out bytesRead);
                        if (singleByteBuffer[0] != 0x00)
                        {
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lEnemyHealthAddress), new byte[] { 0x00 }, 1, out bytesWritten);
                        }
                        /*
                        //enemyData[1] = Convert.ToByte(enemyIndex);
                        if (enemyIndex == 3)
                        {
                            // TODO - tidy up, need to skip bytes 3 and 4 as they contain web swing data
                            // TODO - also seems to cause instant win when going to end screen, maybe just need to send bare minimum?
                            //WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset), enemyData, 3, out bytesWritten);
                            //WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 5), enemyData.Skip(5).ToArray(), 45, out bytesWritten);
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 8), new byte[] { 0x5F }, 1, out bytesWritten);
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 31), new byte[] { 0x01 }, 1, out bytesWritten);
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset + 34), new byte[] { 0x01 }, 1, out bytesWritten);
                        }
                        else
                        {
                            WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + enemyMemoryOffset), enemyData, ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE, out bytesWritten);
                        }*/
                        --invalidEnemies;
                    }
                }
                var playerIndex = _highestEnemyCount + playerOffset - 1;
                var playerMemoryOffset = (ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE) * playerIndex;
                var playerData = new byte[ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE];
                //playerData[0] = 0xC0;
                //playerData[1] = Convert.ToByte(playerIndex);

                if (playerSpideyLevel.Number == mySpideyLevel.Number)
                {
                    Buffer.BlockCopy(spideyData, 0, playerData, ENEMY_HEADER_SIZE, SPIDEY_DATA_SIZE);
                    //WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset), playerData, ENEMY_HEADER_SIZE + SPIDEY_DATA_SIZE, out bytesWritten);

                    // Cutting out half the data seems to fix graphical glitching issues and disables interaction with other player's game
                    WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset), playerData, ENEMY_HEADER_SIZE + (SPIDEY_DATA_SIZE / 2), out bytesWritten);
                }
                else
                {
                    var singleByteBuffer = new byte[1];
                    // Just the essential enemy bits
                    // Blank sprite
                    var lSpriteAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 8;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lSpriteAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] < 0x50 || singleByteBuffer[0] > 0x5F)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lSpriteAddress), new byte[] { 0x5F }, 1, out bytesWritten);
                    }

                    // To avoid corruption
                    var lCorruptionOneAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 31;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionOneAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] == 0x00)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionOneAddress), new byte[] { 0x01 }, 1, out bytesWritten);
                    }

                    // To avoid corruption
                    var lCorruptionTwoAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 34;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionTwoAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] == 0x00)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lCorruptionTwoAddress), new byte[] { 0x01 }, 1, out bytesWritten);
                    }

                    // Enemy health
                    var lEnemyHealthAddress = spideyAddress + SPIDEY_DATA_SIZE + playerMemoryOffset + 41;
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lEnemyHealthAddress), singleByteBuffer, 1, out bytesRead);
                    if (singleByteBuffer[0] != 0x00)
                    {
                        WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(lEnemyHealthAddress), new byte[] { 0x00 }, 1, out bytesWritten);
                    }
                }

                if (enemyCountNeedsUpdate)
                {
                    WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), new byte[] { desiredEnemyCount }, ENEMY_COUNT_DATA_SIZE, out bytesWritten);
                }
            }

            /*
            // If first time doing this, needs to be done slowly (one by one + write enemy data first)
            if (enemyCountAddress != 0)
            {
                var desiredEnemyCount = Convert.ToByte(_highestEnemyCount + playerCount);
                var enemyCountBuffer = new byte[ENEMY_COUNT_DATA_SIZE];
                ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), enemyCountBuffer, ENEMY_COUNT_DATA_SIZE, out int bytesRead);
                while((enemyCountBuffer[0] < desiredEnemyCount) && (bytesRead == ENEMY_COUNT_DATA_SIZE))
                {
                    // TODO - detect level change while doing this?
                    WriteProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), new byte[] { Convert.ToByte(enemyCountBuffer[0] + 1) }, ENEMY_COUNT_DATA_SIZE, out bytesWritten);
                    ReadProcessMemory(new IntPtr(dosBoxProcess), new IntPtr(enemyCountAddress), enemyCountBuffer, ENEMY_COUNT_DATA_SIZE, out bytesRead);
                    Thread.Sleep(5000);
                }
            }*/
        }
    }
}
