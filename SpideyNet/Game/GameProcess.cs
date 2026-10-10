using System.Diagnostics;

namespace SpideyNet.Game;

public sealed record GameProcess(int Id, string Title)
{
    public static GameProcess[] Find()
    {
        var games = new List<GameProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!process.ProcessName.StartsWith("DOSBox", StringComparison.OrdinalIgnoreCase)) { continue; }
                games.Add(new GameProcess(process.Id, $"{process.Id} · {process.MainWindowTitle}"));
            }
        }
        return games.ToArray();
    }
}
