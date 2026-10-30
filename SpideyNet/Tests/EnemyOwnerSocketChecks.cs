using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Tests;

internal static class EnemyOwnerSocketChecks
{
    internal static async Task RunAsync(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var secondStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        var first = new RelayClient();
        var second = new RelayClient();
        var third = new RelayClient();
        var key = Guid.NewGuid().ToString("N");
        var frame = new GameFrame(1, 20, 147, 0, 24);
        var report = new SharedReport(new byte[SharedRules.PuzzleBytes], [], 0, []);
        var connections = new List<Task>();
        try
        {
            first.Publish(frame, report);
            connections.Add(first.Run(endpoint, key, "First relay connection", InteractionMode.Disabled, stop.Token, SharedMode.Experimental));
            await UntilAsync(() => first.World?.Shared?.Room?.Owner == first.World?.Self
                && first.World is not null, stop.Token);
            second.Publish(frame with { Level = 2 }, report);
            connections.Add(second.Run(endpoint, key, "First room arrival", InteractionMode.Disabled, secondStop.Token, SharedMode.Experimental));
            await UntilAsync(() => second.World is not null && second.World.Shared?.Room?.Owner == second.World.Self, stop.Token);
            third.Publish(frame with { Level = 2 }, report);
            connections.Add(third.Run(endpoint, key, "Second room arrival", InteractionMode.Disabled, stop.Token, SharedMode.Experimental));
            await UntilAsync(() => third.World?.Shared?.Room?.Owner == second.World?.Self, stop.Token);

            // An older relay connection entering later must not preempt the room's first occupant.
            first.Publish(frame with { Level = 2 }, report);
            await UntilAsync(() => first.World?.Shared?.Room?.Level == 2, stop.Token);
            var settled = first.World!.Shared!.Room!;
            Check(settled.Owner == second.World!.Self, "Arrival order overrides relay connection order");
            await Task.Delay(200, stop.Token);
            Check(first.World.Shared!.Room!.Owner == settled.Owner && first.World.Shared.Room.Epoch == settled.Epoch,
                "Ordinary same-room updates preserve ownership and epoch");

            second.Publish(frame, report);
            await UntilAsync(() => first.World?.Shared?.Room?.Owner == third.World?.Self, stop.Token);
            second.Publish(frame with { Level = 2 }, report);
            await UntilAsync(() => second.World?.Shared?.Room?.Level == 2, stop.Token);
            Check(second.World!.Shared!.Room!.Owner == third.World!.Self, "Returning former owner joins behind existing occupants");
            third.Publish(frame with { Level = 3 }, report);
            await UntilAsync(() => first.World?.Shared?.Room?.Owner == first.World?.Self, stop.Token);

            // Losing active membership resets arrival priority even without closing the socket.
            first.Publish(null);
            await UntilAsync(() => second.World?.Shared?.Room?.Owner == second.World?.Self, stop.Token);
            first.Publish(frame with { Level = 2 }, report);
            await UntilAsync(() => first.World?.Players.Any(player => player.Id == first.World.Self) == true, stop.Token);
            Check(first.World!.Shared!.Room!.Owner == second.World!.Self, "Timed-out player does not reclaim priority on return");
            secondStop.Cancel();
            await UntilAsync(() => first.World?.Shared?.Room?.Owner == first.World?.Self, stop.Token);
            Console.WriteLine("Enemy owner sockets: native-room arrival order, stable epochs, transitions, returning players, timeout and disconnect handoff passed.");
        }
        finally
        {
            stop.Cancel();
            try
            {
                await Task.WhenAll(connections);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task UntilAsync(Func<bool> ready, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!ready())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
