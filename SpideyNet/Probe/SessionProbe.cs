using SpideyNet.Client;
using SpideyNet.Core;
using SpideyNet.Game;
using SpideyNet.Transport;

namespace SpideyNet.Probe;

internal static class SessionProbe
{
    internal static async Task ReentryAsync(Uri endpoint, int firstId, int secondId)
    {
        CheckProcesses(firstId, secondId);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var firstGame = new GameBridge(firstId, stop.Token);
        using var secondGame = new GameBridge(secondId, stop.Token);
        await RetreatAsync(firstGame, stop.Token);
        await RetreatAsync(secondGame, stop.Token);
        var first = new Session();
        var second = new Session();
        var room = Guid.NewGuid().ToString("N");
        try
        {
            // Keep one peer in Midnight while the other reloads its native scene tables.
            first.Connect(firstId, endpoint, room, "Reentry first", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Experimental);
            second.Connect(secondId, endpoint, room, "Reentry second", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Experimental);
            await UntilAsync(() => first.View.Players.Length == 1 && second.View.Players.Length == 1, stop.Token);
            for (var repeat = 0; repeat < 5; repeat++)
            {
                Position(firstGame, 260);
                await UntilAsync(() => firstGame.Read()?.Level == 2, stop.Token);
                Position(firstGame, -20);
                await UntilAsync(() => firstGame.Read()?.Level == 1, stop.Token);
                await UntilAsync(() => first.View.Local?.Level == 1 && second.View.Players.Length == 1
                    && second.View.Players[0].Frame.Level == 1, stop.Token);
                await Task.Delay(300, stop.Token);
                if (first.View.Status.StartsWith("Stopped", StringComparison.Ordinal)
                    || second.View.Status.StartsWith("Stopped", StringComparison.Ordinal)
                    || firstGame.Inspect().RenderHex != secondGame.Inspect().RenderHex)
                {
                    throw new InvalidOperationException($"Room reentry stopped a session: {first.View.Status}; {second.View.Status}");
                }

                Console.WriteLine($"Reentry {repeat + 1}: both sessions active in Midnight with matching guest renderers.");
            }
        }
        finally
        {
            await Task.WhenAll(first.DisconnectAsync(), second.DisconnectAsync());
        }
    }

    internal static async Task WatchWebAsync(Uri endpoint, string room)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var observer = new RelayClient();
        var connection = observer.Run(endpoint, room, "Web observer", InteractionMode.Experimental, stop.Token, SharedMode.Experimental);
        try
        {
            // Observe real desktop commands without injecting another actor into either game.
            await UntilAsync(() => observer.World?.Players.Length >= 2, stop.Token);
            Console.WriteLine("Web observer ready: click Fire web or press V in the shooter's DOSBox.");
            while (observer.World?.Players.Any(player => player.Constraint == ConstraintKind.Frozen) != true)
            {
                await Task.Delay(10, stop.Token);
            }

            Console.WriteLine("Web observer: a desktop command froze a remote player.");
            await UntilAsync(() => observer.World?.Players.All(player => player.Constraint != ConstraintKind.Frozen) == true, stop.Token);
            Console.WriteLine("Web observer: freeze expired.");
        }
        finally
        {
            stop.Cancel();
            try
            {
                await connection;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    internal static async Task ReconnectAsync(Uri endpoint, int firstId, int secondId)
    {
        CheckProcesses(firstId, secondId);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var firstGame = new GameBridge(firstId, stop.Token);
        using var secondGame = new GameBridge(secondId, stop.Token);
        var firstBefore = firstGame.Inspect();
        var secondBefore = secondGame.Inspect();
        var first = new Session();
        var second = new Session();
        var room = Guid.NewGuid().ToString("N");
        try
        {
            // Stop and restart the relay externally after the ready message.
            first.Connect(firstId, endpoint, room, "Reconnect first", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Experimental);
            second.Connect(secondId, endpoint, room, "Reconnect second", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Experimental);
            await UntilAsync(() => first.View.Players.Length == 1 && second.View.Players.Length == 1, stop.Token);
            Console.WriteLine("Reconnect ready: stop the relay, wait for restoration, then restart it.");
            while (!first.View.Status.StartsWith("Reconnecting", StringComparison.Ordinal)
                || !second.View.Status.StartsWith("Reconnecting", StringComparison.Ordinal))
            {
                await Task.Delay(10, stop.Token);
            }

            await UntilAsync(() => first.View.Players.Length == 0 && second.View.Players.Length == 0, stop.Token);
            CheckStorage(firstBefore, firstGame.Inspect());
            CheckStorage(secondBefore, secondGame.Inspect());
            Console.WriteLine("Reconnect outage: guests removed and renderer/storage restored.");
            while (first.View.Players.Length != 1 || second.View.Players.Length != 1)
            {
                await Task.Delay(10, stop.Token);
            }

            await UntilAsync(() => firstGame.Inspect().RenderHex != firstBefore.RenderHex
                && secondGame.Inspect().RenderHex != secondBefore.RenderHex, stop.Token);
            Console.WriteLine("Reconnect recovered: both sessions rejoined and rendered guests after relay restart.");
        }
        finally
        {
            await Task.WhenAll(first.DisconnectAsync(), second.DisconnectAsync());
        }

        CheckStorage(firstBefore, firstGame.Inspect());
        CheckStorage(secondBefore, secondGame.Inspect());
    }

    internal static async Task RunAsync(Uri endpoint, int firstId, int secondId)
    {
        CheckProcesses(firstId, secondId);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var firstGame = new GameBridge(firstId, stop.Token);
        using var secondGame = new GameBridge(secondId, stop.Token);
        await RetreatAsync(firstGame, stop.Token);
        await RetreatAsync(secondGame, stop.Token);
        var firstBefore = firstGame.Inspect();
        var secondBefore = secondGame.Inspect();
        if (firstBefore.Frame?.Level != secondBefore.Frame?.Level)
        {
            throw new InvalidOperationException("Start both games in the same room.");
        }

        var first = new Session();
        var second = new Session();
        try
        {
            // Exercise the production session service, including its memory worker.
            Connect(first, second, firstId, secondId, endpoint, InjectionMode.Observe);
            await UntilAsync(() => first.View.Players.Length == 1 && second.View.Players.Length == 1, stop.Token);
            CheckStorage(firstBefore, firstGame.Inspect());
            CheckStorage(secondBefore, secondGame.Inspect());
            await Task.WhenAll(first.DisconnectAsync(), second.DisconnectAsync());
            Console.WriteLine("Session Observe: roster populated; renderer and reserved storage unchanged.");

            // Place the players within web range before enabling authoritative corrections.
            Position(firstGame, 11);
            Position(secondGame, 51);
            Connect(first, second, firstId, secondId, endpoint, InjectionMode.Experimental);
            await UntilAsync(() => first.View.Players.Length == 1 && second.View.Players.Length == 1, stop.Token);
            await UntilAsync(() => firstGame.Inspect().RenderHex != firstBefore.RenderHex, stop.Token);
            first.FireWeb();
            await UntilAsync(() => first.View.Players[0].Constraint == ConstraintKind.Frozen, stop.Token);
            Position(secondGame, 67);
            await UntilAsync(() => secondGame.Read() is { } frozen && Math.Abs(frozen.X - 51) <= 2, stop.Token);
            await UntilAsync(() => first.View.Players[0].Constraint == ConstraintKind.None, stop.Token);
            Position(secondGame, 67);
            await UntilAsync(() => Math.Abs(first.View.Players[0].Frame.X - 67) <= 2, stop.Token);
            Position(firstGame, 91);
            await UntilAsync(() => Math.Abs(second.View.Players[0].Frame.X - 91) <= 2, stop.Token);
            if (second.View.Players[0].Constraint != ConstraintKind.None)
            {
                throw new InvalidOperationException("Player crossing produced a position correction.");
            }

            Console.WriteLine("Session interactions: freeze and expiry work; players pass through each other.");

            // A double click or closing during disconnect can request shutdown twice.
            await Task.WhenAll(first.DisconnectAsync(), first.DisconnectAsync(), second.DisconnectAsync());
            CheckStorage(firstBefore, firstGame.Inspect());
            CheckStorage(secondBefore, secondGame.Inspect());
            Console.WriteLine("Session disconnect: concurrent requests completed; renderer and reserved storage restored.");
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"Session test failed. First: {System.Text.Json.JsonSerializer.Serialize(first.View)}; "
                + $"second: {System.Text.Json.JsonSerializer.Serialize(second.View)}", error);
        }
        finally
        {
            await Task.WhenAll(first.DisconnectAsync(), second.DisconnectAsync());
        }
    }

    private static async Task RetreatAsync(GameBridge game, CancellationToken token)
    {
        var frame = game.Read() ?? throw new IOException("Start the game.");
        if (frame.Level == 2)
        {
            // Foyer's enemy drains health during idle tests; return through its left exit.
            game.Constrain(new PlayerState(Guid.Empty, "Retreat probe", frame with { X = -20, Y = 147 }, 1, ConstraintKind.Position));
            await UntilAsync(() => game.Read()?.Level == 1, token);
        }

        if (game.Read()?.Level != 1)
        {
            throw new InvalidOperationException("Run session checks in Midnight, away from the native enemy.");
        }
    }

    private static void Connect(Session first, Session second, int firstId, int secondId, Uri endpoint, InjectionMode mode)
    {
        var room = Guid.NewGuid().ToString("N");
        var interactions = mode == InjectionMode.Observe ? InteractionMode.Disabled : InteractionMode.Experimental;
        first.Connect(firstId, endpoint, room, "Session first", mode, interactions, SharedMode.Independent);
        second.Connect(secondId, endpoint, room, "Session second", mode, interactions, SharedMode.Independent);
    }

    private static void CheckProcesses(int firstId, int secondId)
    {
        if (firstId == secondId)
        {
            throw new ArgumentException("Choose two distinct DOSBox processes.");
        }
    }

    private static void Position(GameBridge game, short x)
    {
        var frame = game.Read() ?? throw new IOException("Game left the test room.");
        game.Constrain(new PlayerState(Guid.Empty, "Session probe", frame with { X = x, Y = 147 }, 1, ConstraintKind.Position));
    }

    private static void CheckStorage(GameStatus before, GameStatus after)
    {
        if (before.RenderHex != after.RenderHex || before.SlotHash != after.SlotHash || before.EnemyCount != after.EnemyCount)
        {
            throw new InvalidOperationException("Session modified or failed to restore renderer/actor storage.");
        }
    }

    private static async Task UntilAsync(Func<bool> condition, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
