using SpideyNet.Core;

namespace SpideyNet.Client.ViewModels;

internal static class SpriteCatalogue
{
    internal static IReadOnlyList<SpriteChoice> Choices { get; } = new[]
        {
            new SpriteChoice(PlayerSpriteRules.SpiderMan, "Spider-Man")
        }
        .Concat(Enumerable.Range(PlayerSpriteRules.FirstEnemy, PlayerSpriteRules.LastEnemy - PlayerSpriteRules.FirstEnemy + 1)
            .Select(CreateEnemy)).ToArray();

    private static SpriteChoice CreateEnemy(int frame)
    {
        // Descriptive visual guesses, not official enemy names. Keep the native frame ID visible.
        var (name, first) = frame switch
        {
            >= 0x20 and <= 0x25 => ("Mummy", 0x20),
            >= 0x26 and <= 0x2A => ("Skeleton", 0x26),
            >= 0x2B and <= 0x2D => ("Shark fin", 0x2B),
            >= 0x2E and <= 0x31 => ("Bat", 0x2E),
            >= 0x32 and <= 0x34 => ("Robot", 0x32),
            >= 0x35 and <= 0x39 => ("Green monster", 0x35),
            >= 0x3A and <= 0x3D => ("Rat", 0x3A),
            >= 0x3E and <= 0x3F => ("Werewolf", 0x3E),
            >= 0x40 and <= 0x45 => ("Cowboy", 0x40),
            >= 0x46 and <= 0x4A => ("Grim Reaper", 0x46),
            _ => throw new ArgumentOutOfRangeException(nameof(frame))
        };

        return new SpriteChoice((byte)frame, $"{name} · pose {frame - first + 1} (0x{frame:X2})");
    }
}
