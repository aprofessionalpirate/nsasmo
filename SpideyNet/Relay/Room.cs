using System.Net.WebSockets;
using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Relay;

internal sealed class Room(InteractionMode interactions, SharedMode sharedMode)
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Peer> peers = [];
    private long tick;
    private long roomEntryOrder;
    private readonly TauntLog taunts = new();
    private readonly SharedRooms shared = new();
    internal Guid Id { get; } = Guid.NewGuid();
    internal InteractionMode Interactions { get; } = interactions;
    internal SharedMode SharedMode { get; } = sharedMode;

    internal Peer? Join(string name)
    {
        lock (gate)
        {
            if (peers.Count >= NetRules.MaxPlayers)
            {
                return null;
            }

            var peer = new Peer(name);
            peers.Add(peer.Id, peer);
            return peer;
        }
    }

    internal int Count { get { lock (gate) { return peers.Count; } } }
    internal void Leave(Peer peer) { lock (gate) { peers.Remove(peer.Id); } }

    internal async Task Run(WebSocket socket, Peer peer, CancellationToken token)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        var send = SendLoop(socket, peer, lifetime.Token);
        var receive = ReadLoopAsync(socket, peer, lifetime.Token);
        try
        {
            await await Task.WhenAny(send, receive);
        }
        finally
        {
            Leave(peer);
            lifetime.Cancel();
            socket.Abort();
            try { await Task.WhenAll(send, receive); }
            catch (Exception) when (lifetime.IsCancellationRequested) { }
        }
    }

    internal void Broadcast()
    {
        lock (gate)
        {
            var now = Environment.TickCount64;
            tick++;
            shared.Elect(peers.Values.Where(p => p.State is not null && now - p.LastSeen < NetRules.StaleMs).ToArray());
            var players = peers.Values.Where(p => p.State is not null
                && now - p.LastSeen < NetRules.StaleMs).Select(p => p.State!).ToArray();
            var recentTaunts = taunts.Snapshot(now);
            foreach (var peer in peers.Values)
            {
                // Route fresh game images only to watchers; movement snapshots stay at 30 Hz.
                var watched = peers.GetValueOrDefault(peer.Watching ?? Guid.Empty);
                var screen = watched?.Screen is { } image && now - watched.ScreenAt < NetRules.StaleMs
                    && !ReferenceEquals(peer.SentScreen, image) ? new PlayerScreen(watched.Id, image) : null;
                peer.SentScreen = watched?.Screen;
                var requested = peers.Values.Any(other => other.Watching == peer.Id && now - other.LastSeen < NetRules.StaleMs);
                peer.Outbox.Writer.TryWrite(new WorldState(NetRules.Version, peer.Id, tick, players,
                    SharedMode == SharedMode.Experimental ? shared.Snapshot(peer.State?.Frame.Level, peer.SharedSerial) : null, requested, screen, recentTaunts, taunts.Serial));
            }
        }
    }

    private async Task ReadLoopAsync(WebSocket socket, Peer peer, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var update = await Wire.Receive<ClientUpdate>(socket, token);
            if (update is null)
            {
                return;
            }

            lock (gate)
            {
                var now = Environment.TickCount64;
                if (now - peer.RateStart >= NetRules.RateWindowMs)
                {
                    peer.RateStart = now;
                    peer.RateCount = 0;
                }
                if (++peer.RateCount > NetRules.MaxRate || update.Version != NetRules.Version
                    || update.Frame is null || !NetRules.Valid(update.Frame) || !Enum.IsDefined(update.Colour) || !PlayerSpriteRules.Valid(update.Sprite)
                    || update.Sequence <= (peer.State?.Sequence ?? 0)
                    || update.Taunt is { } clip && !Enum.IsDefined(clip))
                {
                    throw new InvalidDataException("Invalid or excessive update.");
                }

                if (update.Watching == peer.Id || update.Watching == Guid.Empty || update.Screen is { IsValid: false })
                {
                    throw new InvalidDataException("Invalid player preview request.");
                }

                if (peer.Watching != update.Watching)
                {
                    peer.SentScreen = null;
                }

                peer.Watching = update.Watching;
                if (update.Screen is { } screen)
                {
                    if (now - peer.ScreenAt < 100)
                    {
                        throw new InvalidDataException("Excessive player preview rate.");
                    }

                    peer.Screen = screen;
                    peer.ScreenAt = now;
                }

                Apply(peer, update, now);
                peer.LastSeen = now;
                if (update.Taunt is { } taunt)
                {
                    taunts.Add(peer.Id, peer.Name, taunt, now);
                }

                if (update.Shared is not null)
                {
                    if (SharedMode != SharedMode.Experimental || !SharedRules.Valid(update.Shared))
                    {
                        throw new InvalidDataException("Invalid shared game state.");
                    }
                    // Never seed the destination with enemy/mover records from the room being left.
                    if (update.Frame.Level != peer.State!.Frame.Level)
                    {
                        continue;
                    }

                    // Keep contact geometry paired with the same position update. A cached
                    // platform can be a tile behind the rider's native simulation.
                    peer.Movers = new MoverObservation(peer.State!.Frame.Level, update.Shared.Epoch,
                        update.Sequence, update.Shared.Movers ?? []);
                    peer.SharedEpoch = update.Shared.Epoch;
                    shared.Apply(peer, update.Shared);
                    peer.SharedSerial = update.Shared.Serial;
                }
            }
        }
    }

    private void Apply(Peer peer, ClientUpdate update, long now)
    {
        if (!Enum.IsDefined(update.Action)) { throw new InvalidDataException("Unknown action."); }
        var frame = update.Frame;
        var constraint = ConstraintKind.None;
        var previous = peer.State?.Frame;

        if (update.Action == PlayerAction.Teleport)
        {
            // A deliberate relocation must not be rewound by an earlier push or tether.
            peer.Push = null;
            peer.WebTarget = null;
        }

        if (Interactions == InteractionMode.Experimental && previous is not null && update.Action != PlayerAction.Teleport)
        {
            if (peer.Push is { } pending && !pending.Accepts(frame.Level))
            {
                peer.Push = null;
            }
            if (peer.Push is { } push)
            {
                // Ignore native movement/collision corrections while the push is in progress.
                var finished = push.Finished(frame, now);
                var position = push.Sample(frame, now);
                frame = frame with { Level = position.Level, X = position.X, Y = position.Y, Exits = position.Exits, Web = null, WebHeld = false };
                constraint = ConstraintKind.Pushed;
                if (finished)
                {
                    peer.Push = null;
                }
            }
        }
        if (Interactions == InteractionMode.Experimental && constraint != ConstraintKind.Pushed && update.Action != PlayerAction.Teleport)
        {
            frame = ApplyWeb(peer, frame, now);
            if (peer.WebTarget is not null)
            {
                constraint = ConstraintKind.WebPull;
            }
        }

        // Arrival in this game room, rather than relay join order, determines enemy authority.
        // A timed-out player reappearing in place also joins behind the active occupants.
        if (previous?.Level != frame.Level || now - peer.LastSeen >= NetRules.StaleMs)
        {
            peer.RoomEntryOrder = ++roomEntryOrder;
            peer.SharedEpoch = -1;
        }

        peer.State = new PlayerState(peer.Id, peer.Name, frame, update.Sequence, constraint, update.Colour, update.Sprite);
        if (Interactions != InteractionMode.Experimental || update.Action != PlayerAction.PushPlayer
            || constraint == ConstraintKind.Pushed || peer.WebReadyAt > now) { return; }

        peer.WebReadyAt = now + InteractionRules.CooldownMs;
        var target = peers.Values.Where(p => p.Id != peer.Id && p.State is not null
            && now - p.LastSeen < NetRules.StaleMs && InteractionRules.CanPush(frame, p.State.Frame))
            .OrderBy(p => Math.Pow(p.State!.Frame.X - frame.X, 2)
                + Math.Pow(p.State.Frame.ScreenY - frame.ScreenY, 2)).ThenBy(p => p.Id).FirstOrDefault();
        if (target?.State is null) { return; }
        // A new impulse replaces the old one and releases any web tether on its target.
        target.Push = new PushMotion(frame, target.State.Frame, now);
        target.WebTarget = null;
        target.State = target.State with { Constraint = ConstraintKind.Pushed };
    }

    private GameFrame ApplyWeb(Peer peer, GameFrame frame, long now)
    {
        if (!frame.WebHeld)
        {
            peer.WebTarget = null;
            return frame;
        }

        var candidates = peers.Values.Where(other => other.Id != peer.Id && other.State is not null
            && other.State.Frame.Level == frame.Level && now - other.LastSeen < NetRules.StaleMs).ToArray();
        var target = candidates.FirstOrDefault(other => other.Id == peer.WebTarget);
        var continuing = target is not null;
        target ??= candidates.Where(other => WebRules.Hits(frame, other.State!.Frame))
            .OrderBy(other => Math.Abs(other.State!.Frame.X - frame.X)).FirstOrDefault();
        peer.WebTarget = target?.Id;
        if (target?.State is null)
        {
            return frame;
        }

        var start = continuing && peer.State?.Frame.Level == frame.Level ? peer.State.Frame : frame;
        return WebRules.Pull(frame with { X = start.X, Y = start.Y }, target.State.Frame, (now - peer.LastSeen) / 1000d);
    }

    private static async Task SendLoop(WebSocket socket, Peer peer, CancellationToken token)
    {
        await foreach (var state in peer.Outbox.Reader.ReadAllAsync(token))
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(NetRules.SendTimeoutSeconds));
            await Wire.Send(socket, state, deadline.Token);
        }
    }
}
