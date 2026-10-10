using SpideyNet.Core;
using SpideyNet.Transport;

internal static class DemoPeer
{
    internal static async Task Run(Uri endpoint, string room)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var client = new RelayClient();
        var origin = new GameFrame(2, 96, 147, 0, 94);
        client.Publish(origin);
        var connection = client.Run(endpoint, room, "Test Spider-Man", InteractionMode.Disabled, stop.Token);
        var tick = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(NetRules.TickMs));
            while (await timer.WaitForNextTickAsync(stop.Token))
            {
                if (connection.IsCompleted) { await connection; return; }
                var state = client.World;
                var local = state?.Players.FirstOrDefault(p => p.Id != state.Self)?.Frame;
                var anchor = local ?? origin;
                var baseX = local is null ? origin.X : local.X + 48;
                client.Publish(anchor with { X = (short)Math.Clamp(baseX + Math.Round(16 * Math.Sin(++tick / 10.0)), 0, 240) });
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally
        {
            stop.Cancel();
            try { await connection; }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }
}
