using System.ComponentModel;
using System.Diagnostics;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class PauseRaceProbe
{
    private const int Samples = 3000;

    internal static async Task RunAsync()
    {
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate Probe.exe.");
        using var worker = Process.Start(new ProcessStartInfo(executable, "--thread-churn-worker")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        }) ?? throw new IOException("Cannot start the thread-lifetime fixture.");
        var failures = 0;
        try
        {
            // Exercise the actual pause driver against threads exiting during enumeration.
            await worker.StandardOutput.ReadLineAsync();
            for (var sample = 0; sample < Samples; sample++)
            {
                try
                {
                    using var pause = new ThreadPause(worker.Id);
                }
                catch (Win32Exception error)
                {
                    failures++;
                    if (failures <= 3)
                    {
                        Console.WriteLine($"Pause failure {error.NativeErrorCode}: {error.Message}");
                    }
                }

                if (sample % 10 == 0)
                {
                    await Task.Delay(1);
                }
            }
        }
        finally
        {
            // Only the disposable fixture is stopped; neither DOSBox instance is touched.
            worker.Kill();
            await worker.WaitForExitAsync();
        }

        Console.WriteLine($"Thread lifetime race: {Samples} pause/resume cycles, {failures} failures.");
        if (failures > 0)
        {
            throw new InvalidOperationException("An exiting thread stopped a pause transaction.");
        }
    }

    internal static void RunWorker()
    {
        Console.WriteLine("Ready");
        while (true)
        {
            var thread = new Thread(() => { });
            thread.Start();
            thread.Join();
        }
    }
}
