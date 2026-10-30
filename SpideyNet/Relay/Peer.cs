using System.Threading.Channels;
using SpideyNet.Core;

namespace SpideyNet.Relay;

internal sealed class Peer(string name)
{
    internal Guid Id { get; } = Guid.NewGuid();
    internal string Name { get; } = name;
    internal PlayerState? State { get; set; }
    internal long RoomEntryOrder { get; set; }
    internal long LastSeen { get; set; } = Environment.TickCount64;
    internal long RateStart { get; set; } = Environment.TickCount64;
    internal int RateCount { get; set; }
    internal long FrozenUntil { get; set; }
    internal long WebReadyAt { get; set; }
    internal Guid? WebTarget { get; set; }
    internal Guid? Watching { get; set; }
    internal ScreenFrame? Screen { get; set; }
    internal long ScreenAt { get; set; }
    internal ScreenFrame? SentScreen { get; set; }
    internal MoverObservation? Movers { get; set; }
    internal long SharedEpoch { get; set; } = -1;
    internal long SharedSerial { get; set; }
    internal Dictionary<int, long> PuzzlePulseSerials { get; } = [];
    internal Channel<WorldState> Outbox { get; } = Channel.CreateBounded<WorldState>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true, SingleWriter = true });
}
