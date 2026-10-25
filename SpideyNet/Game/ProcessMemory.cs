using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SpideyNet.Game;

internal sealed class ProcessMemory : IDisposable
{
    private const uint QueryAccess = 0x0400;
    private const uint ReadAccess = 0x0010;
    private const uint WriteAccess = 0x0020;
    private const uint OperationAccess = 0x0008;
    private const uint Committed = 0x1000;
    private const uint Guard = 0x0100;
    private const uint NoAccess = 0x0001;
    private const int ChunkSize = 1024 * 1024;
    private const int OverlapSize = 4096;
    private readonly SafeProcessHandle handle;
    private readonly int processId;

    internal ProcessMemory(int processId)
    {
        this.processId = processId;
        handle = OpenProcess(QueryAccess | ReadAccess | WriteAccess | OperationAccess, 0, processId);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    internal ThreadPause Pause() => new(processId);

    internal byte[] Read(long address, int length)
    {
        var data = new byte[length];
        if (!ReadProcessMemory(handle, (nint)address, data, (nuint)length, out var read)
            || read != (nuint)length)
        {
            throw new IOException($"Memory read failed at 0x{address:X}.");
        }
        return data;
    }

    internal void Write(long address, byte[] data)
    {
        if (!WriteProcessMemory(handle, (nint)address, data, (nuint)data.Length, out var written)
            || written != (nuint)data.Length)
        {
            throw new IOException($"Memory write failed at 0x{address:X}.");
        }
    }

    internal IEnumerable<(long Address, byte[] Data)> Scan(CancellationToken token)
    {
        long address = 0;
        while (VirtualQueryEx(handle, (nint)address, out var region,
            (nuint)Marshal.SizeOf<MemoryRegion>()) != 0)
        {
            token.ThrowIfCancellationRequested();
            var start = (long)region.BaseAddress;
            var length = checked((long)region.RegionSize);
            var next = checked(start + length);
            if (next <= address) { yield break; }
            address = next;
            if (region.State != Committed || (region.Protect & (Guard | NoAccess)) != 0)
            {
                continue;
            }

            for (long offset = 0; offset < length; offset += ChunkSize - OverlapSize)
            {
                token.ThrowIfCancellationRequested();
                var size = (int)Math.Min(ChunkSize, length - offset);
                byte[] data;
                try { data = Read(start + offset, size); }
                catch (IOException) { continue; }
                yield return (start + offset, data);
            }
        }
    }

    public void Dispose() => handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, int inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        byte[] buffer, nuint size, out nuint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(SafeProcessHandle process, nint address,
        byte[] buffer, nuint size, out nuint written);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualQueryEx(SafeProcessHandle process, nint address,
        out MemoryRegion region, nuint length);
}
