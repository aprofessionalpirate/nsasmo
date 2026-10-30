using SpideyNet.Core;

namespace SpideyNet.Relay;

internal sealed class TauntLog
{
    private readonly Queue<(long At, TauntEvent Event)> events = new();
    internal long Serial { get; private set; }

    internal void Add(Guid player, string name, TauntId clip, long now)
    {
        events.Enqueue((now, new TauntEvent(++Serial, player, name, clip)));
        while (events.Count > TauntRules.MaxEvents)
        {
            events.Dequeue();
        }
    }

    internal TauntEvent[] Snapshot(long now)
    {
        // Repeat recent events across snapshots: a slow socket can drop a movement outbox item.
        while (events.TryPeek(out var item) && now - item.At >= TauntRules.LifetimeMs)
        {
            events.Dequeue();
        }

        return events.Select(item => item.Event).ToArray();
    }
}
