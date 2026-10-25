namespace SpideyNet.Game;

internal sealed class FrameBoundary(ProcessMemory memory, long dataAddress) : IDisposable
{
    private const int TimeoutMs = 2000;
    private readonly long codeAddress = dataAddress + GameProfile.CodeFromData;
    private bool installed;

    internal IDisposable Pause()
    {
        if (!installed)
        {
            using var pause = memory.Pause();
            Install();
        }

        // Windows suspension alone can stop REP MOVSW between VGA planes. Only hand
        // a transaction to the caller once the native frame has finished copying/drawing.
        var started = Environment.TickCount64;
        while (true)
        {
            var pause = memory.Pause();
            try
            {
                var busy = memory.Read(codeAddress + FrameBoundaryPlan.Flag, 1)[0];
                if (busy == 0)
                {
                    return pause;
                }
                if (busy != 1)
                {
                    throw new IOException("The native frame marker changed; restart DOSBox.");
                }
            }
            catch
            {
                pause.Dispose();
                throw;
            }
            pause.Dispose();
            if (Environment.TickCount64 - started >= TimeoutMs)
            {
                throw new IOException("DOSBox did not finish its frame. Resume the game before reconnecting.");
            }
            Thread.Sleep(1);
        }
    }

    private void Install()
    {
        var plan = FrameBoundaryPlan.Build();
        var storage = memory.Read(codeAddress + FrameBoundaryPlan.Storage, plan.Length);
        if (!GameProfile.Supports(memory.Read(dataAddress + GameProfile.ModeOffset, 1)[0])
            || !memory.Read(codeAddress + FrameBoundaryPlan.Enter, 3).AsSpan().SequenceEqual(FrameBoundaryPlan.OriginalEnter)
            || !memory.Read(codeAddress + FrameBoundaryPlan.Exit, 3).AsSpan().SequenceEqual(FrameBoundaryPlan.OriginalExit)
            || !storage.AsSpan().SequenceEqual(FrameBoundaryPlan.OriginalStorage)
                && !storage.AsSpan(0, plan.Length - 1).SequenceEqual(plan.AsSpan(0, plan.Length - 1)))
        {
            throw new IOException("Unsupported or already-owned native frame boundary.");
        }

        // This verified renderer branch is unreachable in VGA. Reuse an existing stub
        // without rewriting its instructions: a previous detachment may still be returning.
        if (storage.AsSpan().SequenceEqual(FrameBoundaryPlan.OriginalStorage))
        {
            memory.Write(codeAddress + FrameBoundaryPlan.Storage, plan);
        }
        memory.Write(codeAddress + FrameBoundaryPlan.Flag, [1]);
        try
        {
            memory.Write(codeAddress + FrameBoundaryPlan.Enter, FrameBoundaryPlan.Call(FrameBoundaryPlan.Enter, FrameBoundaryPlan.Storage));
            memory.Write(codeAddress + FrameBoundaryPlan.Exit, FrameBoundaryPlan.Call(FrameBoundaryPlan.Exit, FrameBoundaryPlan.Storage + FrameBoundaryPlan.StubSize));
            installed = true;
        }
        catch
        {
            memory.Write(codeAddress + FrameBoundaryPlan.Enter, FrameBoundaryPlan.OriginalEnter.ToArray());
            memory.Write(codeAddress + FrameBoundaryPlan.Exit, FrameBoundaryPlan.OriginalExit.ToArray());
            throw;
        }
    }

    public void Dispose()
    {
        if (!installed)
        {
            return;
        }
        using var pause = memory.Pause();

        if (!memory.Read(codeAddress + FrameBoundaryPlan.Enter, 3).AsSpan()
                .SequenceEqual(FrameBoundaryPlan.Call(FrameBoundaryPlan.Enter, FrameBoundaryPlan.Storage))
            || !memory.Read(codeAddress + FrameBoundaryPlan.Exit, 3).AsSpan()
                .SequenceEqual(FrameBoundaryPlan.Call(FrameBoundaryPlan.Exit, FrameBoundaryPlan.Storage + FrameBoundaryPlan.StubSize)))
        {
            throw new IOException("The native frame hooks changed; restart DOSBox.");
        }

        // Unlink both hooks but leave their now-inert stub instructions intact. DOSBox
        // can be suspended on the tail jump itself; restoring those bytes would corrupt IP.
        memory.Write(codeAddress + FrameBoundaryPlan.Enter, FrameBoundaryPlan.OriginalEnter.ToArray());
        memory.Write(codeAddress + FrameBoundaryPlan.Exit, FrameBoundaryPlan.OriginalExit.ToArray());
        installed = false;
    }
}
