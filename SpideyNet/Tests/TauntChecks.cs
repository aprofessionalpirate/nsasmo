using SpideyNet.Core;
using SpideyNet.Relay;

namespace SpideyNet.Tests;

internal static class TauntChecks
{
    internal static void Run()
    {
        var player = Guid.NewGuid();
        var log = new TauntLog();
        log.Add(player, "Sender", TauntId.Spider, 100);
        log.Add(player, "Sender", TauntId.Man, 100);
        var events = log.Snapshot(101);
        Check(events.Length == 2 && events[0].Sequence == 1 && events[1].Sequence == 2,
            "Taunts have ordered identities without a cooldown");
        Check(TauntRules.Valid(events, log.Serial), "Valid taunt history");
        Check(!TauntRules.Valid([events[0] with { Clip = (TauntId)999 }], log.Serial), "Reject unknown clips");
        Check(!TauntRules.Valid([events[0] with { Player = Guid.Empty }], log.Serial), "Reject empty sender");
        Check(!TauntRules.Valid([events[0] with { Name = "Bad\nname" }], log.Serial), "Reject control characters");
        Check(!TauntRules.Valid([events[1], events[0]], log.Serial), "Reject reordered events");
        Check(!TauntRules.Valid(events, 1), "Reject future events");

        for (var index = 0; index < 100; index++)
        {
            log.Add(player, "Sender", TauntId.SpiderMan, 200);
        }

        Check(log.Snapshot(201).Length == TauntRules.MaxEvents && log.Serial == 102, "Bound retained history during bursts");
        Check(log.Snapshot(200 + TauntRules.LifetimeMs).Length == 0 && log.Serial == 102, "Expiry preserves the replay watermark");
        Console.WriteLine("Taunts: catalogue validation, ordering, no cooldown, bounded history and expiry passed.");
    }

    private static void Check(bool value, string name)
    {
        if (!value)
        {
            throw new InvalidOperationException(name);
        }
    }
}
