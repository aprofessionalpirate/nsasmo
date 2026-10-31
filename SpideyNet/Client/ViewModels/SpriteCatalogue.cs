using SpideyNet.Core;

namespace SpideyNet.Client.ViewModels;

internal static class SpriteCatalogue
{
    internal static IReadOnlyList<SpriteChoice> Choices { get; } = PlayerSpriteRules.Choices.Select(Create).ToArray();

    private static SpriteChoice Create(byte sprite)
    {
        // Mary Jane's two-frame rescue script is separate from Mysterio's walk/turn scripts.
        var name = sprite switch
        {
            PlayerSpriteRules.SpiderMan => "Spider-Man",
            0x20 => "Mummy",
            0x26 => "Skeleton",
            0x2B => "Shark fin",
            0x2E => "Bat",
            0x32 => "Robot",
            0x35 => "Green monster",
            0x3A => "Rat",
            0x3E => "Mary Jane",
            0x40 => "Cowboy",
            0x46 => "Mysterio",
            _ => throw new ArgumentOutOfRangeException(nameof(sprite))
        };
        return new SpriteChoice(sprite, name);
    }
}
