using System.Net.WebSockets;
using SpideyNet.Core;

namespace SpideyNet.Transport;

public sealed class RelayClient
{
    private WorldState? world;
    private ClientUpdate? latest;
    private long receivedAt;
    private int pendingAction;

    public WorldState? World => Environment.TickCount64 - Interlocked.Read(ref receivedAt) < NetRules.StaleMs
        ? Volatile.Read(ref world) : null;

    public void Publish(GameFrame? frame, SharedReport? shared = null) => Volatile.Write(ref latest,
        frame is null ? null : new ClientUpdate(NetRules.Version, 0, frame, Shared: shared));
    public void FireWeb() => Interlocked.Exchange(ref pendingAction, (int)PlayerAction.FireWeb);

    public async Task Run(Uri endpoint, string room, string name, InteractionMode interactions, CancellationToken token,
        SharedMode shared = SharedMode.Independent)
    {
        if (endpoint.Scheme != "wss" && !(endpoint.Scheme == "ws" && endpoint.IsLoopback))
        {
            throw new ArgumentException("Use wss:// for remote relays; ws:// is local only.");
        }
        if (room.Length is < NetRules.MinRoomLength or > NetRules.MaxRoomLength)
        {
            throw new ArgumentException("Room key must contain 12–64 characters.");
        }
        if (name.Length is 0 or > NetRules.MaxNameLength)
        {
            throw new ArgumentException("Name must contain 1–24 characters.");
        }

        var address = new UriBuilder(endpoint)
        {
            Path = NetRules.SocketPath,
            Query = $"room={Uri.EscapeDataString(room)}&name={Uri.EscapeDataString(name)}&pvp={(int)interactions}&shared={(int)shared}"
        };
        using var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(NetRules.KeepAliveSeconds);
        socket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(NetRules.KeepAliveSeconds);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        await socket.ConnectAsync(address.Uri, lifetime.Token);
        var send = SendLoop(socket, lifetime.Token);
        var receive = ReadLoop(socket, lifetime.Token);

        try
        {
            await await Task.WhenAny(send, receive);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            throw new OperationCanceledException(token);
        }
        finally
        {
            lifetime.Cancel();
            socket.Abort();
            try { await Task.WhenAll(send, receive); }
            catch (Exception) when (lifetime.IsCancellationRequested) { }
            Volatile.Write(ref world, null);
        }
    }

    private async Task SendLoop(ClientWebSocket socket, CancellationToken token)
    {
        using var timer = new PeriodicTimer(NetRules.TickInterval);
        long sequence = 0;
        while (await timer.WaitForNextTickAsync(token))
        {
            var frame = Volatile.Read(ref latest);
            if (frame is null)
            {
                continue;
            }

            // Only the latest position is sent; historical frames never queue.
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(NetRules.SendTimeoutSeconds));
            var action = (PlayerAction)Interlocked.Exchange(ref pendingAction, (int)PlayerAction.Move);
            await Wire.Send(socket, frame with { Sequence = ++sequence, Action = action }, deadline.Token);
        }
    }

    private async Task ReadLoop(ClientWebSocket socket, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var state = await Wire.Receive<WorldState>(socket, token);
            if (state is null)
            {
                throw new IOException("Relay disconnected.");
            }
            if (state.Version != NetRules.Version || state.Players is null
                || state.Players.Length > NetRules.MaxPlayers
                || state.Self == Guid.Empty || state.Tick < 0
                || state.Players.Select(p => p?.Id).Distinct().Count() != state.Players.Length
                || state.Shared is not null && !SharedRules.Valid(state.Shared)
                || state.Players.Any(p => p is null || p.Id == Guid.Empty || p.Frame is null
                    || p.Name is null || p.Name.Length is 0 or > NetRules.MaxNameLength
                    || !Enum.IsDefined(p.Constraint) || !NetRules.Valid(p.Frame)))
            {
                throw new InvalidDataException("Invalid relay snapshot.");
            }

            Interlocked.Exchange(ref receivedAt, Environment.TickCount64);
            Volatile.Write(ref world, state);
        }
    }
}
