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
            var owner = peers.Where(peer => peer.State?.Frame.Level == level && peer.SharedEpoch >= 0)
                .OrderBy(peer => peer.RoomEntryOrder).FirstOrDefault()?.Id ?? Guid.Empty;
            var movers = room.Movers.Select(mover =>
            {
                var riders = peers.Where(peer => peer.State?.Frame.Level == level
                    && Riding(peer, room, mover)).ToArray();
                var driver = riders.FirstOrDefault(peer => peer.Id == mover.Owner) ?? riders.FirstOrDefault();
                // A rider can trigger launch before the relay grants control. Adopt that
                // native command on handoff, but keep the cached position to avoid a jump.
                var motion = driver is not null && driver.Id != mover.Owner
                    ? driver.Movers!.Movers.Single(value => MoverRules.SameShape(value, mover)).Motion : mover.Motion;
                return mover with { Owner = driver?.Id ?? owner, Motion = motion };
            }).ToArray();
            rooms[level] = room with { Owner = owner, Epoch = owner == room.Owner ? room.Epoch : ++epoch, Movers = movers };
        }
    }

    private static bool Riding(Peer peer, RoomState room, MoverState mover)
    {
        var observation = peer.Movers;
        if (observation is null || observation.Level != room.Level || observation.Sequence != peer.State!.Sequence)
        {
            return false;
        }

        // New drivers must first acknowledge this room's state. An existing driver can
        // retain contact while acknowledging an enemy-owner epoch change.
        if (observation.Epoch != room.Epoch && mover.Owner != peer.Id)
        {
            return false;
        }
        var observed = observation.Movers.SingleOrDefault(value => MoverRules.SameShape(value, mover));
        return observed is not null && MoverRules.Riding(peer.State.Frame, observed);
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
