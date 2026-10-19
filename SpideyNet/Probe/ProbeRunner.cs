using System.Diagnostics;
using SpideyNet.Game;
using SpideyNet.Core;

namespace SpideyNet.Probe;

internal static class ProbeRunner
{
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length == 4 && args[0] == "--level-preview")
        {
            await LevelProbe.JoinAsync(new Uri(args[1]), args[2], int.Parse(args[3]));
            return;
        }

        if (args.Length == 4 && args[0] == "--level-pair")
        {
            await LevelProbe.RunAsync(new Uri(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            return;
        }

        if (args.Length == 1 && args[0] == "--pause-race")
        {
            await PauseRaceProbe.RunAsync();
            return;
        }

        if (args.Length == 1 && args[0] == "--thread-churn-worker")
        {
            PauseRaceProbe.RunWorker();
            return;
        }

        if (args.Length == 2 && args[0] == "--inactive")
        {
            await InactiveProbe.RunAsync(int.Parse(args[1]));
            return;
        }

        if (args.Length == 2 && args[0] is "--web" or "--web-stress")
        {
            await WebProbe.RunAsync(int.Parse(args[1]), args[0] == "--web-stress");
            return;
        }

        if (args.Length == 4 && args[0] == "--session-reentry")
        {
            await SessionProbe.ReentryAsync(new Uri(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            return;
        }

        if (args.Length == 4 && args[0] == "--session")
        {
            await SessionProbe.RunAsync(new Uri(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            return;
        }
        if (args.Length == 4 && args[0] == "--session-reconnect")
        {
            await SessionProbe.ReconnectAsync(new Uri(args[1]), int.Parse(args[2]), int.Parse(args[3]));
            return;
        }
        if (args.Length == 3 && args[0] == "--watch-web")
        {
            await SessionProbe.WatchWebAsync(new Uri(args[1]), args[2]);
            return;
        }

        const int ProbeSeconds = 30;
        if (args.Length == 2 && args[0] == "--enemy-still")
        {
            await EnemyProbe.Run(int.Parse(args[1]));
            return;
        }
        if (args.Length == 2 && args[0] == "--video")
        {
            await VideoProbe.RunAsync(int.Parse(args[1]));
            return;
        }
        if (args.Length == 4 && args[0] is "--pair" or "--pair-contact" or "--pair-transition")
        {
            var mode = args[0] switch { "--pair-contact" => PairMode.Contact, "--pair-transition" => PairMode.Transition, _ => PairMode.Scenes };
            await PairProbe.RunAsync(new Uri(args[1]), int.Parse(args[2]), int.Parse(args[3]), mode);
            return;
        }
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(ProbeSeconds * 2));
        var processes = Process.GetProcesses().Where(p => p.ProcessName.StartsWith("DOSBox", StringComparison.OrdinalIgnoreCase));
        foreach (var process in processes)
        {
            using (process)
            {
                Console.WriteLine($"DOSBox {process.Id}: {process.MainWindowTitle}");
                try
                {
                    using var game = new GameBridge(process.Id, limit.Token);
                    Console.WriteLine(game.Inspect());
                    if (args.Length == 2 && args[0] == "--scene" && args[1] == process.Id.ToString())
                    {
                        var scene = game.InspectScene();
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(scene));
                        continue;
                    }
                    if (args.Length != 2 || args[0] is not ("--inject" or "--transition") || args[1] != process.Id.ToString()) { continue; }
                    var before = game.Inspect();
                    if (before.Frame is null) { continue; }
                    var guest = before.Frame with { X = 96 };
                    for (var tick = 0; tick < ProbeSeconds * 1000 / NetRules.TickMs; tick++)
                    {
                        game.Render([guest], InjectionMode.Experimental);
                        if (args[0] == "--transition" && tick == 20)
                        {
                            game.Constrain(new PlayerState(Guid.NewGuid(), "Transition probe",
                                before.Frame with { X = 260 }, 1, ConstraintKind.Position));
                        }
                        await Task.Delay(NetRules.TickInterval, limit.Token);
                    }
                    Console.WriteLine($"Injected: {game.Inspect()}");
                    if (game.Inspect().Frame?.Level == before.Frame.Level && game.Inspect().EnemyCount != before.EnemyCount)
                    {
                        throw new InvalidOperationException("Injection changed the physics actor count.");
                    }
                    game.Render([], InjectionMode.Observe);
                    var after = game.Inspect();
                    Console.WriteLine($"Restored: {after}");
                    if (before.RenderHex != after.RenderHex)
                    {
                        throw new InvalidOperationException("Renderer instruction was not restored.");
                    }
                    if (before.Frame.Level == after.Frame?.Level
                        && (before.EnemyCount != after.EnemyCount || before.SlotHash != after.SlotHash))
                    {
                        throw new InvalidOperationException("Enemy table restoration did not match.");
                    }
                }
                catch (IOException error)
                {
                    Console.Error.WriteLine($"DOSBox {process.Id}: {error.Message}");
                    Environment.ExitCode = 1;
                }
            }
        }
    }
}
