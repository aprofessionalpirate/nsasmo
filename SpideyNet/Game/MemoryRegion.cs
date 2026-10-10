using System.Runtime.InteropServices;

namespace SpideyNet.Game;

[StructLayout(LayoutKind.Sequential)]
internal struct MemoryRegion
{
    internal nint BaseAddress;
    internal nint AllocationBase;
    internal uint AllocationProtect;
    internal ushort PartitionId;
    internal nuint RegionSize;
    internal uint State;
    internal uint Protect;
    internal uint Type;
}
