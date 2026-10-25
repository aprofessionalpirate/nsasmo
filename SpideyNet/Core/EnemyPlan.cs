using System.Buffers.Binary;

namespace SpideyNet.Core;

public static class EnemyPlan
{
    public const int Stride = GameLayout.RecordSize;
    public const int FirstSlot = 5;
    public const int NativeCount = 5;
    public const int Capacity = 17;
    public const int MaxGuests = NetRules.MaxPlayers - 1;
    public const int RenderCount = NativeCount + MaxGuests;
    public const int DescriptorSlot = RenderCount;
    public const int DescriptorSize = 8;
    public const int ReservedSize = (Capacity - FirstSlot) * Stride;
    private const int RenderPrefixSize = 24;
    private const int SizeOffset = 29;
    private const short HiddenX = -32;
    private const byte HiddenY = 200;

    public static byte[] Build(ReadOnlySpan<byte> local, GameFrame? remote, ushort descriptor)
    {
        if (local.Length != GameLayout.RecordSize)
        {
            throw new ArgumentException("Incomplete local player record.");
        }
        var bytes = new byte[Stride];
        var actor = bytes.AsSpan();
        actor[GameLayout.CharacterOffset] = GameLayout.HiddenCharacter;
        local.Slice(SizeOffset, sizeof(ushort)).CopyTo(actor[SizeOffset..]);
        // Each guest owns an inactive descriptor; it cannot alter local AI flags.
        BinaryPrimitives.WriteUInt16LittleEndian(actor[GameLayout.DescriptorOffset..], descriptor);
        if (remote is null)
        {
            BinaryPrimitives.WriteInt16LittleEndian(actor[GameLayout.XOffset..], HiddenX);
            BinaryPrimitives.WriteInt16LittleEndian(actor[GameLayout.BoundsLeftOffset..], HiddenX);
            BinaryPrimitives.WriteInt16LittleEndian(actor[GameLayout.BoundsRightOffset..], HiddenX);
            actor[GameLayout.YOffset] = HiddenY;
            actor[GameLayout.BoundsTopOffset] = HiddenY;
            actor[GameLayout.BoundsBottomOffset] = HiddenY;
            return bytes;
        }
        if (!NetRules.Valid(remote))
        {
            throw new ArgumentException("Invalid remote player state.");
        }

        // Sprite references come from this process, never from network bytes.
        local[..RenderPrefixSize].CopyTo(actor);
        actor[1] = 0;
        BinaryPrimitives.WriteInt16LittleEndian(actor[GameLayout.XOffset..], remote.X);
        actor[GameLayout.YOffset] = remote.Y;
        actor[GameLayout.FacingOffset] = remote.Facing;
        actor[GameLayout.CharacterOffset] = remote.Pose;
        var dx = remote.X - BinaryPrimitives.ReadInt16LittleEndian(local[GameLayout.XOffset..]);
        var dy = remote.Y - local[GameLayout.YOffset];
        foreach (var index in new[] { GameLayout.BoundsLeftOffset, GameLayout.BoundsRightOffset, GameLayout.PreviousXOffset })
        {
            var x = BinaryPrimitives.ReadInt16LittleEndian(local[index..]) + dx;
            BinaryPrimitives.WriteInt16LittleEndian(actor[index..], checked((short)x));
        }
        actor[GameLayout.BoundsTopOffset] = unchecked((byte)(local[GameLayout.BoundsTopOffset] + dy));
        actor[GameLayout.BoundsBottomOffset] = unchecked((byte)(local[GameLayout.BoundsBottomOffset] + dy));
        actor[GameLayout.PreviousYOffset] = remote.Y;
        actor[GameLayout.PreviousFaceOffset] = remote.Facing;
        return bytes;
    }
}
