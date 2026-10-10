namespace SpideyNet.Core;

public static class NetRules
{
    public const int Version = 2;
    public const int MaxPlayers = 8;
    public const int TickMs = 50;
    public const int MaxMessageBytes = 8192;
    public const int MaxNameLength = 24;
    public const int MinRoomLength = 12;
    public const int MaxRoomLength = 64;
    public const int StaleMs = 2000;
    public const int MaxRate = 40;
    public const string SocketPath = "/play";
    public const int RateWindowMs = 1000;
    public const int SendTimeoutSeconds = 2;
    public const int KeepAliveSeconds = 10;

    public static bool Valid(GameFrame frame)
    {
        return frame.Level is > 0 and <= GameLayout.MaxLevel
            && frame.X is >= -32 and <= GameLayout.Width + 32
            && frame.Y <= GameLayout.Height + 32 && frame.Facing is 0 or 1 or 2 or 5;
    }
}
