using SpideyNet.Client;
using SpideyNet.Core;
using SpideyNet.Transport;
using SpideyNet.Game;

namespace SpideyNet.Probe;

internal static class SpriteProbe
{
    internal static async Task SessionAsync(Uri endpoint, int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        using var game = new GameBridge(processId, stop.Token);
        var original = game.Read() ?? throw new IOException("Start a VGA game first.");
        var before = game.Inspect();
        var room = $"colours-{Guid.NewGuid():N}";
        var peers = Enumerable.Range(0, EnemyPlan.MaxGuests).Select(_ => new RelayClient()).ToArray();
        var workers = peers.Select((peer, index) => peer.Run(endpoint, room, $"Colour {index + 1}",
            InteractionMode.Disabled, stop.Token)).ToArray();
        var session = new Session();
        try
        {
            Publish(original.Level);
            session.Connect(processId, endpoint, room, "Colour viewer", InjectionMode.Experimental,
                InteractionMode.Disabled, SharedMode.Independent, true, SuitColour.Purple);
            await WaitAsync(() => session.View.Players.Length == EnemyPlan.MaxGuests, session, stop.Token);
            foreach (var level in new byte[] { 3, 17, 25, original.Level })
            {
                Publish(level);
                await WaitAsync(() => session.View.Players.Length == EnemyPlan.MaxGuests
                    && session.View.Players.All(player => player.Frame.Level == level), session, stop.Token);
                await session.TeleportAsync(session.View.Players[0].Id);
                await WaitAsync(() => session.View.Local?.Level == level, session, stop.Token);
                await Task.Delay(750, stop.Token);
                if (game.Inspect().RenderHex != Convert.ToHexString(RenderPatch.Build(EnemyPlan.RenderCount - 1)))
                {
                    throw new IOException("The coloured guest session lost its renderer after travel.");
                }
                Console.WriteLine($"Seven coloured network guests rendered in {RoomNames.Get(level)}.");
            }
        }
        finally
        {
            await session.DisconnectAsync();
            stop.Cancel();
            try
            {
                await Task.WhenAll(workers);
            }
            catch (OperationCanceledException)
            {
            }

            using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!game.TryTeleport(original))
            {
                await Task.Delay(20, restore.Token);
            }
        }

        if (game.Inspect().RenderHex != before.RenderHex || game.Inspect().EnemyCount != before.EnemyCount)
        {
            throw new IOException("The colour session did not restore the native renderer/count.");
        }
        Console.WriteLine("Eight-player production session, room transitions and disconnect restoration passed.");

        void Publish(byte level)
        {
            for (var index = 0; index < peers.Length; index++)
            {
                peers[index].Publish(new GameFrame(level, (short)(40 + index * 29), 147, (byte)(index % 8), 24,
                    index == 0 ? new WebState(90, 110, 48, 150) : null), colour: (SuitColour)(index + 1));
            }
        }
    }

    private static async Task WaitAsync(Func<bool> ready, Session session, CancellationToken token)
    {
        var started = Environment.TickCount64;
        while (!ready())
        {
            if (session.View.Status.StartsWith("Stopped:") || Environment.TickCount64 - started > 10000)
            {
                throw new IOException($"Colour session timed out: {session.View.Status}");
            }
            await Task.Delay(30, token);
        }
    }

    internal static async Task RunAsync(int processId)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        using var game = new GameBridge(processId, stop.Token);
        var local = game.Read() ?? throw new IOException("Start a VGA game first.");
        var guests = Enumerable.Range(0, EnemyPlan.MaxGuests)
            .Select(index => local with { X = (short)(40 + index * 29), Y = 147, Pose = 24, Facing = 0, Web = null }).ToArray();

        // Freeze one transaction so native scenery cannot invalidate the restoration comparison.
        using (var pause = game.BeginFrameTransaction())
        {
            var before = game.Inspect();
            var background = game.InspectScene()!.BackgroundHash;
            game.Render(guests, InjectionMode.Experimental, true);
            if (game.InspectScene()!.BackgroundHash == background)
            {
                throw new IOException("Coloured sprites did not paint the backing buffer.");
            }

            // Exercise every native pose/bank combination; coloured guests must never leave
            // an on-screen native placeholder that can fetch unrelated sprite-bank data.
            for (byte facing = 0; facing < 8; facing++)
            {
                for (byte pose = 0; pose < GameLayout.PlayerCharacterLimit; pose++)
                {
                    var frames = guests.Select(guest => guest with { Facing = facing, Pose = pose }).ToArray();
                    game.Render(frames, InjectionMode.Experimental, true);
                    var table = Convert.FromHexString(game.Inspect().TableHex);
                    for (var slot = EnemyPlan.FirstSlot; slot <= EnemyPlan.MaxGuests; slot++)
                    {
                        var actor = table.AsSpan((slot - 1) * EnemyPlan.Stride, EnemyPlan.Stride);
                        if (actor[GameLayout.FacingOffset] != 0
                            || actor[GameLayout.CharacterOffset] != GameLayout.HiddenCharacter
                            || actor[GameLayout.YOffset] < GameLayout.Height)
                        {
                            throw new IOException($"Visible native placeholder for coloured pose {pose}, facing {facing}.");
                        }
                    }
                }
            }
            Console.WriteLine("All 32 poses and eight facings keep native placeholders hidden.");

            game.Render(guests, InjectionMode.Experimental);
            if (game.InspectScene()!.BackgroundHash != background)
            {
                throw new IOException("Switching to native sprites did not restore the backing buffer.");
            }

            game.Render([], InjectionMode.Observe);
            var after = game.Inspect();
            if (before.SlotHash != after.SlotHash || before.RenderHex != after.RenderHex || before.EnemyCount != after.EnemyCount)
            {
                throw new IOException("Sprite renderer restoration failed.");
            }
        }
        Console.WriteLine("Seven colours, native fallback, backing buffer and reserved-byte restoration passed.");

        try
        {
            using var timer = new PeriodicTimer(NetRules.LocalTickInterval);
            var started = Environment.TickCount64;
            while (Environment.TickCount64 - started < 40000 && await timer.WaitForNextTickAsync(stop.Token))
            {
                var elapsed = Environment.TickCount64 - started;
                var facing = (byte)((elapsed / 4000) % 8);
                var pose = (byte)(24 + elapsed / 150 % 4);
                var frames = guests.Select((guest, index) => guest with
                {
                    Pose = pose,
                    Facing = facing,
                    Web = index == 0 ? new WebState(90, 110, 48, 150) : null
                }).ToArray();
                game.Render(frames, InjectionMode.Experimental, true);
            }
        }
        finally
        {
            game.Render([], InjectionMode.Observe);
        }
        Console.WriteLine("Animated colours and white remote web restored after display test.");
    }
}
