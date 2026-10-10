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
    private SessionView view = new("Disconnected", "", null, []);
    private string networkStatus = "Connecting";

    internal SessionView View => Volatile.Read(ref view);

    internal void Connect(int processId, Uri endpoint, string room, string name, InjectionMode mode, InteractionMode interactions,
        SharedMode shared)
    {
        if (running is not null) { throw new InvalidOperationException("Disconnect first."); }
        lifetime = new CancellationTokenSource();
        running = Run(processId, endpoint, room, name, mode, interactions, shared, lifetime.Token);
    }

    internal void FireWeb() => relay.FireWeb();

    internal async Task Disconnect()
    {
        if (lifetime is null) { return; }
        lifetime.Cancel();
        if (running is not null) { await running; }
        lifetime.Dispose();
        lifetime = null;
        running = null;
        Volatile.Write(ref view, new SessionView("Disconnected", "", null, []));
    }

    private async Task Run(int processId, Uri endpoint, string room, string name,
        InjectionMode mode, InteractionMode interactions, SharedMode shared, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        var network = NetworkLoop(endpoint, room, name, interactions, shared, linked.Token);
        try
        {
            // One worker owns discovery, reads, injection and restoration.
            await Task.Run(async () =>
            {
                using var game = new GameBridge(processId, linked.Token);
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(NetRules.TickMs));
                var tracks = new Dictionary<Guid, RemoteTrack>();
                var sceneTrack = new SharedTrack();
                long lastTick = -1;
                while (await timer.WaitForNextTickAsync(linked.Token))
                {
                    var local = game.Read();
                    if (interactions == InteractionMode.Experimental && game.ReadAction() == PlayerAction.FireWeb)
                    {
                        relay.FireWeb();
                    }
                    var state = relay.World;
                    if (shared == SharedMode.Experimental)
                    {
                        var scene = game.ReadScene();
                        relay.Publish(scene?.Frame, scene is null ? null : sceneTrack.Capture(scene, state));
                        if (scene is not null && sceneTrack.Accept(scene, state) is { } correction)
                        {
                            game.SyncScene(correction);
                        }
                    }
                    else { relay.Publish(local); }
                    if (interactions == InteractionMode.Experimental && mode == InjectionMode.Experimental)
                    {
                        game.Constrain(state?.Players.FirstOrDefault(p => p.Id == state.Self));
                    }
                    var players = state?.Players.Where(p => p.Id != state.Self).OrderBy(p => p.Id).ToArray() ?? [];
                    if (state is not null && state.Tick != lastTick)
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
                        lastTick = state.Tick;
                    }
                    foreach (var id in tracks.Keys.Where(id => !players.Any(p => p.Id == id)).ToArray()) { tracks.Remove(id); }
                    var frames = players.Select(p => tracks[p.Id].Sample(Environment.TickCount64))
                        .OfType<GameFrame>().ToArray();
                    game.Render(frames, mode);
                    var owner = state?.Shared?.Room?.Owner;
                    var sharedStatus = shared == SharedMode.Independent ? "" : owner is null ? "Waiting for shared scene"
                        : owner == state!.Self ? "Enemy owner: you" : $"Enemy owner: {state.Players.FirstOrDefault(p => p.Id == owner)?.Name ?? "waiting"}";
                    Volatile.Write(ref view, new SessionView(Volatile.Read(ref networkStatus),
                        local is null ? "Menu or transition" : game.Title, local, players, sharedStatus));
                }
            }, linked.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Volatile.Write(ref view, new SessionView($"Stopped: {error.Message}", "", null, []));
        }
        finally
        {
            relay.Publish(null);
            linked.Cancel();
            try { await network; }
            catch (Exception error)
            {
                Volatile.Write(ref view, new SessionView($"Stopped: {error.Message}", "", null, []));
            }
        }
    }

    private async Task NetworkLoop(Uri endpoint, string room, string name, InteractionMode interactions, SharedMode shared,
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
                        Volatile.Write(ref networkStatus, "Connected · 20 updates/sec");
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
