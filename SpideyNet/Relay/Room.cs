using System.Net.WebSockets;
using SpideyNet.Core;
using SpideyNet.Transport;

namespace SpideyNet.Relay;

internal sealed class Room(InteractionMode interactions, SharedMode sharedMode)
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Peer> peers = [];
    private long tick;
    private readonly SharedRooms shared = new();
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
        var receive = ReadLoop(socket, peer, lifetime.Token);
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
            foreach (var peer in peers.Values)
            {
                peer.Outbox.Writer.TryWrite(new WorldState(NetRules.Version, peer.Id, tick, players,
                    SharedMode == SharedMode.Experimental ? shared.Snapshot(peer.State?.Frame.Level, peer.SharedSerial) : null));
            }
        }
    }

    private async Task ReadLoop(WebSocket socket, Peer peer, CancellationToken token)
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
                    || update.Frame is null || !NetRules.Valid(update.Frame)
                    || update.Sequence <= (peer.State?.Sequence ?? 0))
                {
                    throw new InvalidDataException("Invalid or excessive update.");
                }

                Apply(peer, update, now);
                peer.LastSeen = now;
                if (update.Shared is not null)
                {
                    if (SharedMode != SharedMode.Experimental || !SharedRules.Valid(update.Shared))
                    {
                        throw new InvalidDataException("Invalid shared game state.");
                    }
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
        if (Interactions == InteractionMode.Experimental && previous is not null)
        {
            if (previous.Level != frame.Level) { peer.FrozenUntil = 0; }
            if (peer.FrozenUntil > now)
            {
                frame = frame with { X = previous.X, Y = previous.Y };
                constraint = ConstraintKind.Frozen;
            }
            else if (peers.Values.Any(p => p.Id != peer.Id && p.State is not null
                && now - p.LastSeen < NetRules.StaleMs && InteractionRules.Collides(previous, frame, p.State.Frame)))
            {
                frame = frame with { X = previous.X, Y = previous.Y };
                constraint = ConstraintKind.Collision;
            }
        }
        peer.State = new PlayerState(peer.Id, peer.Name, frame, update.Sequence, constraint);
        if (Interactions != InteractionMode.Experimental || update.Action != PlayerAction.FireWeb
            || constraint == ConstraintKind.Frozen || peer.WebReadyAt > now) { return; }

        peer.WebReadyAt = now + InteractionRules.CooldownMs;
        var target = peers.Values.Where(p => p.Id != peer.Id && p.State is not null
            && now - p.LastSeen < NetRules.StaleMs && InteractionRules.WebHits(frame, p.State.Frame))
            .OrderBy(p => Math.Abs(p.State!.Frame.X - frame.X)).FirstOrDefault();
        if (target?.State is null) { return; }
        target.FrozenUntil = now + InteractionRules.FreezeMs;
        target.State = target.State with { Constraint = ConstraintKind.Frozen };
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
