using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SpideyNet.Game;

internal sealed class ThreadPause : IDisposable
{
    private const uint SuspendAccess = 0x0002;
    private const uint QueryAccess = 0x0800;
    private const uint SynchronizeAccess = 0x00100000;
    private const int InvalidParameter = 87;
    private const uint Signaled = 0;
    private const uint Failed = uint.MaxValue;
    private readonly List<SafeWaitHandle> threads = [];
    private readonly Mutex transaction;
    private readonly int processId;
    private bool acquired;
    private bool ownsSuspension;
    [ThreadStatic]
    private static HashSet<int>? pausedProcesses;

    internal ThreadPause(int processId)
    {
        this.processId = processId;
        transaction = new Mutex(false, $"Local\\SpideyNet-Pause-{processId}");
        try
        {
            // Suspending DOSBox alone does not serialize two host-side memory transactions.
            // A named, recursive mutex also protects diagnostics and nested bridge calls.
            try
            {
                acquired = transaction.WaitOne(TimeSpan.FromSeconds(5));
            }
            catch (AbandonedMutexException error)
            {
                acquired = true;
                throw new IOException("A memory transaction ended abnormally; restart DOSBox before attaching.", error);
            }

            if (!acquired)
            {
                throw new IOException("Timed out waiting for another game memory transaction.");
            }

            // Nested bridge operations share the outer suspension as well as its mutex.
            // Re-enumerating and suspending SDL/audio threads adds avoidable stalls.
            pausedProcesses ??= [];
            if (!pausedProcesses.Add(processId))
            {
                return;
            }
            ownsSuspension = true;

            using var process = Process.GetProcessById(processId);
            foreach (ProcessThread thread in process.Threads)
            {
                using (thread)
                {
                    var handle = OpenThread(SuspendAccess | QueryAccess | SynchronizeAccess, 0, thread.Id);
                    if (handle.IsInvalid)
                    {
                        var error = Marshal.GetLastWin32Error();
                        handle.Dispose();
                        // A thread can exit after Process.Threads takes its snapshot.
                        if (error == InvalidParameter && !ContainsThread(process, thread.Id))
                        {
                            continue;
                        }

                        throw new Win32Exception(error, $"OpenThread({thread.Id}): {new Win32Exception(error).Message}");
                    }

                    // Hold the handle and verify its owner before suspension; IDs can be reused.
                    var owner = GetProcessIdOfThread(handle);
                    if (owner == 0)
                    {
                        var error = Marshal.GetLastWin32Error();
                        handle.Dispose();
                        throw new Win32Exception(error, $"GetProcessIdOfThread({thread.Id}): {new Win32Exception(error).Message}");
                    }

                    if (owner != processId || WaitForSingleObject(handle, 0) == Signaled)
                    {
                        handle.Dispose();
                        continue;
                    }

                    if (SuspendThread(handle) == Failed)
                    {
                        var error = Marshal.GetLastWin32Error();
                        // Kernel teardown may finish just after suspension reports failure.
                        var exited = WaitForSingleObject(handle, 10) == Signaled;
                        handle.Dispose();
                        if (exited)
                        {
                            continue;
                        }

                        throw new Win32Exception(error, $"SuspendThread({thread.Id}): {new Win32Exception(error).Message}");
                    }
                    threads.Add(handle);
                }
            }
        }
        catch { Dispose(); throw; }
    }

    private static bool ContainsThread(Process process, int threadId)
    {
        process.Refresh();
        var found = false;
        foreach (ProcessThread current in process.Threads)
        {
            using (current)
            {
                found |= current.Id == threadId;
            }
        }

        return found;
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
        if (ownsSuspension)
        {
            pausedProcesses!.Remove(processId);
            ownsSuspension = false;
        }
        if (acquired)
        {
            transaction.ReleaseMutex();
            acquired = false;
        }

        transaction.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeWaitHandle OpenThread(uint access, int inherit, int threadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetProcessIdOfThread(SafeWaitHandle thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeWaitHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SuspendThread(SafeWaitHandle thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(SafeWaitHandle thread);
}
