using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Tests;

internal static class TrioInteractionChecks
{
    internal static async Task RunAsync(Uri endpoint)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var peers = Enumerable.Range(0, 3).Select(_ => new RelayClient()).ToArray();
        var frame = new GameFrame(1, 40, 100, 0, 0x18);
        var key = Guid.NewGuid().ToString("N");
        for (var index = 0; index < 3; index++)
        {
            peers[index].Publish(frame with { X = (short)(40 + index * 60) });
        }

        var connections = peers.Select((peer, index) => peer.Run(endpoint, key, $"Trio socket {index}",
            InteractionMode.Experimental, stop.Token)).ToArray();
        try
        {
            await UntilAsync(() => peers.All(peer => peer.World?.Players.Length == 3), stop.Token);
            peers[0].FireWeb();
            await UntilAsync(() => peers[0].World?.Players.Any(player => player.Name == "Trio socket 1"
                && player.Constraint == ConstraintKind.Frozen) == true, stop.Token);
            if (peers[0].World!.Players.Single(player => player.Name == "Trio socket 2").Constraint != ConstraintKind.None)
            {
                throw new InvalidOperationException("A distant third player was frozen.");
            }

            await UntilAsync(() => peers[0].World?.Players.All(player => player.Constraint == ConstraintKind.None) == true, stop.Token);

            // A line intersecting two bodies must attach to the nearer one, then retain it.
            var web = new WebState(200, 107, 47, 107);
            peers[0].Publish(frame with { Web = web, WebHeld = true });
            await UntilAsync(() => peers[1].World?.Players.Any(player => player.Name == "Trio socket 0"
                && player.Constraint == ConstraintKind.WebPull && player.Frame.Web?.EndX == 107) == true, stop.Token);
            peers[2].Publish(frame with { X = 70 });
            await Task.Delay(200, stop.Token);
            if (peers[1].World!.Players.Single(player => player.Name == "Trio socket 0").Frame.Web?.EndX != 107)
            {
                throw new InvalidOperationException("A nearer third player stole an existing web attachment.");
            }

            // Leaving the room invalidates the old target; the remaining intersected peer can be selected.
            peers[1].Publish(frame with { Level = 2, X = 100 });
            await UntilAsync(() => peers[2].World?.Players.Any(player => player.Name == "Trio socket 0"
                && player.Constraint == ConstraintKind.WebPull && player.Frame.Web?.EndX == 77) == true, stop.Token);
            peers[0].Publish(frame with { X = 60 });
            await UntilAsync(() => peers[2].World?.Players.Any(player => player.Name == "Trio socket 0"
                && player.Constraint == ConstraintKind.None && player.Frame.Web is null) == true, stop.Token);
            Console.WriteLine("Three-player sockets: nearest freeze, stable web target, room-change retarget and release passed.");
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
