namespace SpideyNet.Core;

public static class PlayerSpriteRules
{
    public const byte SpiderMan = 0;
    public const byte FirstEnemy = 0x20;
    public const byte LastEnemy = 0x4A;

    public static bool Valid(byte sprite) => sprite == SpiderMan || sprite is >= FirstEnemy and <= LastEnemy;

    public static int SourceFrame(GameFrame player, byte sprite)
    {
        if (!Valid(sprite))
        {
            throw new ArgumentException("Unsupported player sprite.", nameof(sprite));
        }

        // Only Spider-Man has a verified pre-transposed bank. Enemy frames rotate in software.
        return sprite == SpiderMan ? player.Pose + ((player.Facing & 1) != 0 ? 0x60 : 0) : sprite;
    }
}
