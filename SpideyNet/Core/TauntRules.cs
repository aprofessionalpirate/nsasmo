namespace SpideyNet.Core;

public static class TauntRules
{
    public const int MaxEvents = 32;
    public const int LifetimeMs = 8000;

    public static bool Valid(TauntEvent[]? events, long serial)
    {
        if (serial < 0 || events is { Length: > MaxEvents })
        {
            return false;
        }

        // Only catalogue IDs and bounded identities cross the wire, never audio bytes or paths.
        long previous = 0;
        foreach (var item in events ?? [])
        {
            if (item is null || item.Sequence <= previous || item.Sequence > serial
                || item.Player == Guid.Empty || string.IsNullOrWhiteSpace(item.Name)
                || item.Name.Length > NetRules.MaxNameLength || item.Name.Any(char.IsControl)
                || !Enum.IsDefined(item.Clip))
            {
                return false;
            }

            previous = item.Sequence;
        }

        return true;
    }
}
