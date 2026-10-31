namespace SpideyNet.Core;

public static class PlayerSpriteRules
{
    public const byte SpiderMan = 0;
    public const byte FirstEnemy = 0x20;
    public const byte LastEnemy = 0x4A;
    public static IReadOnlyList<byte> Choices { get; } = Array.AsReadOnly<byte>(
        [SpiderMan, 0x20, 0x26, 0x2B, 0x2E, 0x32, 0x35, 0x3A, 0x3E, 0x40, 0x46]);

    public static bool Valid(byte sprite) => Choices.Contains(sprite);

    public static int SourceFrame(GameFrame player, byte sprite)
    {
        if (!Valid(sprite))
        {
            throw new ArgumentException("Unsupported player sprite.", nameof(sprite));
        }
        if (sprite == SpiderMan)
        {
            return player.Pose + ((player.Facing & 1) != 0 ? 0x60 : 0);
        }

        // Follow native walk scripts, excluding their turn/stun frames from the walking loop.
        // Use Spider-Man's animation phase so local and remote appearances stay in step.
        var phase = player.Pose & 3;
        var moving = player.Pose < 16;
        var turning = player.Pose is 25 or 26 or 27;
        return sprite switch
        {
            0x20 => Pose(moving, turning, phase, 0x24, 0x25, [0x20, 0x21, 0x22, 0x23]),
            0x26 => Pose(moving, turning, phase, 0x26, 0x2A, [0x27, 0x28, 0x29, 0x26]),
            0x2B => Pose(moving, turning, phase, 0x2B, 0x2D, [0x2B, 0x2C]),
            0x2E => Pose(moving, turning, phase, 0x30, 0x2E, [0x2F, 0x30, 0x31, 0x2E]),
            0x32 => Pose(moving, turning, phase, 0x32, 0x34, [0x33, 0x32]),
            0x35 => Pose(moving, turning, phase, 0x35, 0x39, [0x36, 0x37, 0x38, 0x35]),
            0x3A => Pose(moving, turning, phase, 0x3A, 0x3C, [0x3B, 0x3A]),
            0x3E => Pose(moving, turning, phase, 0x3E, 0x3F, [0x3E, 0x3F]),
            0x40 => Pose(moving, turning, phase, 0x40, 0x41, [0x42, 0x43, 0x44, 0x45]),
            0x46 => Pose(moving, turning, phase, 0x46, 0x4A, [0x47, 0x48, 0x49, 0x46]),
            _ => throw new ArgumentException("Unsupported player sprite.", nameof(sprite))
        };
    }

    private static byte Pose(bool moving, bool turning, int phase, byte idle, byte turn, ReadOnlySpan<byte> walk)
    {
        // Standing/crouching keeps a recognizable resting pose instead of moonwalking in place.
        return moving ? walk[phase * walk.Length / 4] : turning ? turn : idle;
    }
}
