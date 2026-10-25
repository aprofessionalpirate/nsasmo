using SpideyNet.Probe;

try
{
    // Let probe workers unwind and restore game patches before reporting failure.
    await ProbeRunner.RunAsync(args);
    return Environment.ExitCode;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Probe timed out or was cancelled. Check the required games, relay and expected input.");
    return 2;
}
catch (Exception error)
{
    // Diagnostic failures belong in stderr, rather than a Windows crash dialog.
    Console.Error.WriteLine($"Probe failed: {error}");
    return 1;
}
