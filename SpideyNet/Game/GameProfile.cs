namespace SpideyNet.Game;

internal static class GameProfile
{
    internal const int PlayerDataOffset = 0x92B1;
    internal const int CodeFromData = 0x1D360;
    internal const int RenderOffset = 0x3497;
    internal const int CountInstruction = 3;
    internal const int ModeOffset = 4;
    internal static bool Supports(byte mode) => mode == (byte)VideoMode.Vga;
    internal static ReadOnlySpan<byte> RenderPrefix => [0xBB, 0xB2, 0x92];
    internal static ReadOnlySpan<byte> RenderCount => [0x8B, 0x0E, 0x8B, 0x92];
    internal static ReadOnlySpan<byte> RenderSuffix => [0xFC, 0x51, 0x53, 0x06, 0xE8, 0x0A, 0x00,
        0x07, 0x5B, 0x59, 0x81, 0xC3, 0x32, 0x00, 0xE2, 0xF1, 0xC3];
}
