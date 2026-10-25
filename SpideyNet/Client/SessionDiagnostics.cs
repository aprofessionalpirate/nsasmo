namespace SpideyNet.Client;

internal static class SessionDiagnostics
{
    internal static void Record(Exception error, int gameProcessId)
    {
        try
        {
            // Keep native operation names and stacks without logging room keys or traffic.
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpideyNet");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"client-{Environment.ProcessId}.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
            {
                File.WriteAllText(path, "");
            }

            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} DOSBox {gameProcessId}{Environment.NewLine}{error}{Environment.NewLine}");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never replace the original session failure.
        }
    }
}
