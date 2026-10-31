using System.Net.WebSockets;
using System.Threading.Channels;
using SpideyNet.Core;

namespace SpideyNet.Transport;

public sealed class RelayClient
{
    private readonly Channel<TauntId> pendingTaunts = Channel.CreateBounded<TauntId>(TauntRules.MaxEvents);
    private readonly Channel<TauntEvent> receivedTaunts = Channel.CreateBounded<TauntEvent>(
        new BoundedChannelOptions(TauntRules.MaxEvents) { FullMode = BoundedChannelFullMode.DropOldest });
    private WorldState? world;
    private ClientUpdate? latest;
    private long receivedAt;
    private int pendingAction;
    private PlayerScreen? screen;
    private ScreenFrame? pendingScreen;
    private WatchRequest? watching;
    private long screenReceivedAt;

    public PlayerScreen? Screen
    {
        get
        {
            var image = Volatile.Read(ref screen);
            return image?.Player == Volatile.Read(ref watching)?.Player
                && Environment.TickCount64 - Interlocked.Read(ref screenReceivedAt) < NetRules.StaleMs ? image : null;
        }
    }

    public bool SendTaunt(TauntId clip)
    {
        return Enum.IsDefined(clip) && World is not null && Volatile.Read(ref latest) is not null
            && pendingTaunts.Writer.TryWrite(clip);
    }

    public TauntEvent[] TakeTaunts()
    {
        var events = new List<TauntEvent>();
        while (receivedTaunts.Reader.TryRead(out var item))
        {
            events.Add(item);
        }

        return events.ToArray();
    }

    public void Watch(Guid? player)
    {
        Volatile.Write(ref watching, player is { } id ? new WatchRequest(id) : null);
        Volatile.Write(ref screen, null);
    }

    public void PublishScreen(ScreenFrame image) => Volatile.Write(ref pendingScreen, image);

    public WorldState? World => Environment.TickCount64 - Interlocked.Read(ref receivedAt) < NetRules.StaleMs
        ? Volatile.Read(ref world) : null;

    public void Publish(GameFrame? frame, SharedReport? shared = null, SuitColour colour = SuitColour.Red, byte sprite = PlayerSpriteRules.SpiderMan) => Volatile.Write(ref latest,
        frame is null ? null : new ClientUpdate(NetRules.Version, 0, frame, Shared: shared, Colour: colour, Sprite: sprite));
    public void PushPlayer() => Interlocked.CompareExchange(ref pendingAction, (int)PlayerAction.PushPlayer, (int)PlayerAction.Move);
    public void Teleported() => Interlocked.Exchange(ref pendingAction, (int)PlayerAction.Teleport);

    public async Task Run(Uri endpoint, string room, string name, InteractionMode interactions, CancellationToken token,
        SharedMode shared = SharedMode.Independent)
    {
        if (!RelayEndpointPolicy.Allows(endpoint))
        {
            throw new ArgumentException("Use wss:// for public relays; ws:// supports loopback and private LAN IP addresses.");
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
        var receive = ReadLoopAsync(socket, lifetime.Token);

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
            while (pendingTaunts.Reader.TryRead(out _))
            {
            }

            while (receivedTaunts.Reader.TryRead(out _))
            {
            }

            Volatile.Write(ref screen, null);
            Volatile.Write(ref pendingScreen, null);
            Interlocked.Exchange(ref pendingAction, (int)PlayerAction.Move);
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
            var taunt = pendingTaunts.Reader.TryRead(out var clip) ? (TauntId?)clip : null;
            await Wire.Send(socket, frame with { Sequence = ++sequence, Action = action,
                Watching = Volatile.Read(ref watching)?.Player, Screen = Interlocked.Exchange(ref pendingScreen, null), Taunt = taunt }, deadline.Token);
        }
    }

    private async Task ReadLoopAsync(ClientWebSocket socket, CancellationToken token)
    {
        long? lastTaunt = null;
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
                || !TauntRules.Valid(state.Taunts, state.TauntSerial)
                || lastTaunt is { } previousTaunt && state.TauntSerial < previousTaunt
                || state.Players.Any(p => p is null || p.Id == Guid.Empty || p.Frame is null
                    || p.Name is null || p.Name.Length is 0 or > NetRules.MaxNameLength
                    || !Enum.IsDefined(p.Constraint) || !Enum.IsDefined(p.Colour) || !PlayerSpriteRules.Valid(p.Sprite) || !NetRules.Valid(p.Frame)))
            {
                throw new InvalidDataException("Invalid relay snapshot.");
            }

            if (state.Screen is { } preview)
            {
                if (preview.Player == Guid.Empty || preview.Frame is null || !preview.Frame.IsValid)
                {
                    throw new InvalidDataException("Invalid player preview.");
                }

                if (preview.Player == Volatile.Read(ref watching)?.Player)
                {
                    Volatile.Write(ref screen, preview);
                    Interlocked.Exchange(ref screenReceivedAt, Environment.TickCount64);
                }
            }

            // A fresh connection starts at the current serial, so joins/reconnects never replay history.
            if (lastTaunt is { } serial)
            {
                foreach (var item in state.Taunts ?? [])
                {
                    if (item.Sequence > serial)
                    {
                        receivedTaunts.Writer.TryWrite(item);
                    }
                }
            }

            lastTaunt = state.TauntSerial;
            Interlocked.Exchange(ref receivedAt, Environment.TickCount64);
            Volatile.Write(ref world, state);
        }
    }
}
