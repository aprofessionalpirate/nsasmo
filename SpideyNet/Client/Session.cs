using System.Text.Json;
using SpideyNet.Core;
using SpideyNet.Game;
using SpideyNet.Transport;

namespace SpideyNet.Client;

internal sealed class Session
{
    private const int RetryLimitSeconds = 8;
    private readonly RelayClient relay = new();
    private CancellationTokenSource? lifetime;
    private Task? running;
    private Task? stopping;
    private SessionView view = new("Disconnected", "", null, []);
    private TeleportRequest? teleport;
    private bool canTeleport;
    private string networkStatus = "Connecting";

    internal Task TeleportAsync(Guid player)
    {
        if (running is null || !canTeleport)
        {
            throw new InvalidOperationException("Connect with Experimental rendering before teleporting.");
        }

        var request = new TeleportRequest(player);
        if (Interlocked.CompareExchange(ref teleport, request, null) is not null)
        {
            throw new InvalidOperationException("A teleport is already in progress.");
        }

        return request.Completion.Task;
    }

    internal void Watch(Guid? player) => relay.Watch(player);

    internal SessionView View => Volatile.Read(ref view);

    internal void Connect(int processId, Uri endpoint, string room, string name, InjectionMode mode, InteractionMode interactions,
        SharedMode shared, bool differentColours = false, SuitColour colour = SuitColour.Red)
    {
        if (running is not null) { throw new InvalidOperationException("Disconnect first."); }
        canTeleport = mode == InjectionMode.Experimental;
        relay.Watch(null);
        stopping = null;
        lifetime = new CancellationTokenSource();
        running = RunAsync(processId, endpoint, room, name, mode, interactions, shared, differentColours, colour, lifetime.Token);
    }

    internal void FireWeb() => relay.FireWeb();

    internal Task DisconnectAsync()
    {
        // Double clicks and form closing must await the same restoration work.
        return stopping ??= StopAsync();
    }

    private async Task StopAsync()
    {
        var cancellation = lifetime;
        if (cancellation is null)
        {
            return;
        }

        // Let an accepted native room load finish before restoring its temporary patch.
        // Cancellation halfway through would leave the room index ahead of its tables.
        canTeleport = false;
        if (Volatile.Read(ref teleport) is { } request)
        {
            try
            {
                await request.Completion.Task.WaitAsync(TimeSpan.FromSeconds(8));
            }
            catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException or TimeoutException)
            {
            }
        }

        cancellation.Cancel();
        try
        {
            if (running is not null)
            {
                await running;
            }
        }
        finally
        {
            cancellation.Dispose();
            lifetime = null;
            running = null;
            Volatile.Write(ref view, new SessionView("Disconnected", "", null, []));
        }
    }

    private async Task RunAsync(int processId, Uri endpoint, string room, string name,
        InjectionMode mode, InteractionMode interactions, SharedMode shared, bool differentColours, SuitColour colour, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        var network = NetworkLoopAsync(endpoint, room, name, interactions, shared, linked.Token);
        try
        {
            // One worker owns discovery, reads, injection and restoration.
            await Task.Run(async () =>
            {
                using var game = new GameBridge(processId, linked.Token);
                using var timer = new PeriodicTimer(NetRules.LocalTickInterval);
                var tracks = new Dictionary<Guid, RemoteTrack>();
                var sceneTrack = new SharedTrack();
                WorldState? lastState = null;
                Task<ScreenFrame?>? capture = null;
                long capturedAt = 0;
                long travelledAt = 0;
                while (await timer.WaitForNextTickAsync(linked.Token))
                {
                    var local = game.Read();
                    if (interactions == InteractionMode.Experimental && game.ReadAction() == PlayerAction.FireWeb)
                    {
                        relay.FireWeb();
                    }
                    var state = relay.World;
                    if (Volatile.Read(ref teleport) is { } request)
                    {
                        try
                        {
                            var target = state?.Players.FirstOrDefault(player => player.Id == request.Player)?.Frame
                                ?? throw new InvalidOperationException("The selected player is no longer connected.");
                            request.Destination ??= target;
                            if (target.Level == request.Destination.Level)
                            {
                                // Refresh the landing point while the native room loader runs.
                                request.Destination = target;
                            }
                            if (Environment.TickCount64 - request.CreatedAt > 7000)
                            {
                                throw new IOException("Teleport timed out while the game was loading.");
                            }

                            if (game.TryTeleport(request.Destination))
                            {
                                tracks.Clear();
                                lastState = null;
                                sceneTrack = new SharedTrack();
                                travelledAt = Environment.TickCount64;
                                relay.Teleported();
                                Interlocked.CompareExchange(ref teleport, null, request);
                                request.Completion.TrySetResult();
                            }
                        }
                        catch (Exception error)
                        {
                            game.CancelTeleport();
                            Interlocked.CompareExchange(ref teleport, null, request);
                            request.Completion.TrySetException(error);
                        }

                        local = game.Read();
                    }

                    // Image capture runs independently so a slow window cannot stall game updates.
                    if (capture is { IsCompleted: true })
                    {
                        try
                        {
                            if (await capture is { } image && state?.ScreenRequested == true)
                            {
                                relay.PublishScreen(image);
                            }
                        }
                        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
                        {
                            // An exiting/minimized game simply has no preview available.
                        }

                        capture = null;
                    }

                    if (state?.ScreenRequested == true && capture is null && Environment.TickCount64 - capturedAt >= 250)
                    {
                        capturedAt = Environment.TickCount64;
                        capture = game.CaptureScreenAsync();
                    }

                    GameSceneUpdate? sceneUpdate = null;
                    if (shared == SharedMode.Experimental)
                    {
                        var scene = game.ReadScene();
                        relay.Publish(scene?.Frame, scene is null ? null : sceneTrack.Capture(scene, state), colour);
                        if (scene is not null && sceneTrack.Accept(scene, state) is { } correction)
                        {
                            sceneUpdate = new GameSceneUpdate(correction, state?.Self, scene.Puzzles);
                        }
                        else if (scene is null || state?.Shared?.Room is not { } activeRoom
                            || activeRoom.Level != scene.Frame.Level || activeRoom.Owner == state.Self)
                        {
                            sceneUpdate = new GameSceneUpdate(null, null, null, ResumeEnemies: true);
                        }
                    }
                    else { relay.Publish(local, colour: colour); }
                    if (interactions == InteractionMode.Experimental && mode == InjectionMode.Experimental
                        && Volatile.Read(ref teleport) is null && Environment.TickCount64 - travelledAt > 500)
                    {
                        game.Constrain(state?.Players.FirstOrDefault(p => p.Id == state.Self));
                    }
                    var players = state?.Players.Where(p => p.Id != state.Self).OrderBy(p => p.Id).ToArray() ?? [];

                    // A reconnect can repeat a tick number with entirely new player identities.
                    // Each received snapshot must populate its tracks before sampling them.
                    if (state is not null && !ReferenceEquals(state, lastState))
                    {
                        foreach (var player in players)
                        {
                            if (!tracks.TryGetValue(player.Id, out var track))
                            {
                                track = new RemoteTrack();
                                tracks.Add(player.Id, track);
                            }
                            track.Add(player.Frame, Environment.TickCount64);
                        }
                        lastState = state;
                    }

                    foreach (var id in tracks.Keys.Where(id => !players.Any(p => p.Id == id)).ToArray()) { tracks.Remove(id); }
                    // Keep appearance paired with identity when an interpolation track expires.
                    var samples = players.Select(player => (Frame: tracks[player.Id].Sample(Environment.TickCount64), player.Colour))
                        .Where(sample => sample.Frame is not null).ToArray();
                    game.Render(samples.Select(sample => sample.Frame!).ToArray(), mode, differentColours, sceneUpdate,
                        samples.Select(sample => sample.Colour).ToArray(), colour);
                    var owner = state?.Shared?.Room?.Owner;
                    var sharedStatus = shared == SharedMode.Independent ? "" : owner is null ? "Waiting for shared scene"
                        : owner == state!.Self ? "Enemy owner: you" : $"Enemy owner: {state.Players.FirstOrDefault(p => p.Id == owner)?.Name ?? "waiting"}";
                    Volatile.Write(ref view, new SessionView(Volatile.Read(ref networkStatus),
                        local is null ? "Menu or transition" : game.Title, local, players, sharedStatus, relay.Screen));
                }
            }, linked.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            SessionDiagnostics.Record(error, processId);
            Volatile.Write(ref view, new SessionView($"Stopped: {error.Message}", "", null, []));
        }
        finally
        {
            Interlocked.Exchange(ref teleport, null)?.Completion.TrySetCanceled();
            relay.Watch(null);
            relay.Publish(null);
            linked.Cancel();
            try { await network; }
            catch (Exception error)
            {
                SessionDiagnostics.Record(error, processId);
                Volatile.Write(ref view, new SessionView($"Stopped: {error.Message}", "", null, []));
            }
        }
    }

    private async Task NetworkLoopAsync(Uri endpoint, string room, string name, InteractionMode interactions, SharedMode shared,
        CancellationToken token)
    {
        var retrySeconds = 1;
        while (!token.IsCancellationRequested)
        {
            Volatile.Write(ref networkStatus, "Connecting to relay");
            try
            {
                var connection = relay.Run(endpoint, room, name, interactions, token, shared);
                while (!connection.IsCompleted && !token.IsCancellationRequested)
                {
                    if (relay.World is not null)
                    {
                        Volatile.Write(ref networkStatus, $"Connected · {NetRules.TickRate} updates/sec");
                        retrySeconds = 1;
                    }
                    await Task.WhenAny(connection, Task.Delay(250, token));
                }
                await connection;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (ArgumentException error)
            {
                Volatile.Write(ref networkStatus, error.Message);
                return;
            }
            catch (Exception error) when (error is IOException or InvalidDataException
                or System.Net.WebSockets.WebSocketException or JsonException or OperationCanceledException)
            {
                Volatile.Write(ref networkStatus, $"Reconnecting: {error.Message}");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(retrySeconds), token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            retrySeconds = Math.Min(retrySeconds * 2, RetryLimitSeconds);
        }
    }
}
