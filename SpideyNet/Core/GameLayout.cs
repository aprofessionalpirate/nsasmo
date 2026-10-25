using System.Buffers.Binary;

namespace SpideyNet.Core;

public static class GameLayout
{
    public const int RecordSize = 50;
    public const int LevelOffset = 1266;
    public const int TitleOffset = 13;
    public const int TitleSize = 24;
    public const int MaxLevel = 63;
    public const int Width = 256;
    public const int Height = 176;
    public const int PlayerCharacterLimit = 0x20;
    public const byte FacingMask = 0x07;
    public const int XOffset = 2;
    public const int YOffset = 4;
    public const int FacingOffset = 5;
    public const int CharacterOffset = 6;
    public const int PoseOffset = CharacterOffset;
    public const int DescriptorOffset = 31;
    public const int BoundsLeftOffset = 33;
    public const int BoundsRightOffset = 35;
    public const int BoundsTopOffset = 37;
    public const int BoundsBottomOffset = 38;
    public const int PreviousXOffset = 44;
    public const int PreviousYOffset = 46;
    public const int PreviousFaceOffset = 47;
    public const int HealthOffset = 39;
    public const int MaxHealth = 0xC0;
    public const byte HiddenCharacter = 0x5F;

    public static GameFrame? Decode(ReadOnlySpan<byte> record, byte level)
    {
        // Reject menus and incomplete records before publishing state.
        if (record.Length != RecordSize || level is 0 or > MaxLevel || record[CharacterOffset] >= PlayerCharacterLimit)
        {
            return null;
        }

        return new GameFrame(level, BinaryPrimitives.ReadInt16LittleEndian(record[XOffset..]),
            record[YOffset], record[FacingOffset], record[PoseOffset], Health: record[HealthOffset]);
    }
}
