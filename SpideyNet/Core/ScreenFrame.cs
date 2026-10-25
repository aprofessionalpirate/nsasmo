namespace SpideyNet.Core;

public sealed record ScreenFrame(long Serial, byte[] Data)
{
    public const int Width = 320;
    public const int Height = 200;
    public const int RawBytes = Width * Height * 2;
    public const int MaxBytes = RawBytes + 1024;
    public bool IsValid => Serial >= 0 && Data is { Length: > 0 and <= MaxBytes };
}
