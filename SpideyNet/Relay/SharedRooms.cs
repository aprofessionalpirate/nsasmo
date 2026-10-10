using SpideyNet.Core;

namespace SpideyNet.Relay;

internal sealed class SharedRooms
{
    private byte[]? puzzles;
    private readonly Dictionary<byte, RoomState> rooms = [];
    private long epoch;

    internal void Elect(IReadOnlyList<Peer> peers)
    {
        foreach (var level in rooms.Keys.ToArray())
        {
            var room = rooms[level];
            var owner = peers.FirstOrDefault(peer => peer.State?.Frame.Level == level && peer.SharedEpoch >= 0)?.Id ?? Guid.Empty;
            var movers = room.Movers.Select(mover =>
            {
                var riders = peers.Where(peer => peer.State?.Frame.Level == level
                    && MoverRules.Riding(peer.State.Frame, mover)).ToArray();
                var driver = riders.FirstOrDefault(peer => peer.Id == mover.Owner) ?? riders.FirstOrDefault();
                return mover with { Owner = driver?.Id ?? owner };
            }).ToArray();
            rooms[level] = room with { Owner = owner, Epoch = owner == room.Owner ? room.Epoch : ++epoch, Movers = movers };
        }
    }

    internal void Apply(Peer peer, SharedReport report)
    {
        puzzles ??= report.Seed.ToArray();
        foreach (var change in report.Changes) { SharedRules.Set(puzzles, change); }
        var level = peer.State!.Frame.Level;
        if (!rooms.TryGetValue(level, out var room))
        {
            rooms[level] = new RoomState(level, peer.Id, ++epoch, report.Enemies.ToArray(),
                (report.Movers ?? []).Select(mover => mover with { Owner = peer.Id }).ToArray());
            return;
        }
        if (report.Epoch != room.Epoch) { return; }
        if (!report.Enemies.Select(enemy => enemy.Mover).Order().SequenceEqual(room.Enemies.Select(enemy => enemy.Mover).Order()))
        {
            throw new InvalidDataException("Enemy identities changed within a room.");
        }

        var enemies = room.Enemies.Select(enemy =>
        {
            var observed = report.Enemies.Single(value => value.Mover == enemy.Mover);
            var motion = room.Owner == peer.Id ? observed : enemy;
            return motion with { Health = Math.Min(enemy.Health, observed.Health) };
        }).ToArray();
        var observedMovers = report.Movers ?? [];
        if (observedMovers.Length != room.Movers.Length || room.Movers.Any(mover =>
            !observedMovers.Any(observed => MoverRules.SameShape(mover, observed))))
        {
            throw new InvalidDataException("Moving object identities changed within a room.");
        }
        var movers = room.Movers.Select(mover => mover.Owner == peer.Id
            ? observedMovers.Single(observed => observed.Mover == mover.Mover) with { Owner = mover.Owner } : mover).ToArray();
        rooms[level] = room with { Enemies = enemies, Movers = movers };
    }

    internal SharedState? Snapshot(byte? level, long acknowledged)
    {
        if (puzzles is null) { return null; }
        var room = level.HasValue ? rooms.GetValueOrDefault(level.Value) : null;
        return new SharedState(puzzles.ToArray(), room, acknowledged, room?.Owner ?? Guid.Empty);
    }
}
