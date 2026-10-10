using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SpideyNet.Game;

internal sealed class ThreadPause : IDisposable
{
    private const uint SuspendAccess = 0x0002;
    private const uint Failed = uint.MaxValue;
    private readonly List<SafeWaitHandle> threads = [];

    internal ThreadPause(int processId)
    {
        using var process = Process.GetProcessById(processId);
        try
        {
            foreach (ProcessThread thread in process.Threads)
            {
                using (thread)
                {
                    var handle = OpenThread(SuspendAccess, 0, thread.Id);
                    if (handle.IsInvalid || SuspendThread(handle) == Failed)
                    {
                        handle.Dispose();
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                    threads.Add(handle);
                }
            }
        }
        catch { Dispose(); throw; }
    }

    public void Dispose()
    {
        // Resume every suspended thread, even when a memory transaction fails.
        foreach (var thread in threads)
        {
            ResumeThread(thread);
            thread.Dispose();
        }
        threads.Clear();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWaitHandle OpenThread(uint access, int inherit, int threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(SafeWaitHandle thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(SafeWaitHandle thread);
}
