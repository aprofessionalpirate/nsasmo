namespace SpideyNet.Core;

public static class NetRules
{
    public const int Version = 12;
    public const int MaxPlayers = 8;
    public const int TickRate = 30;
    public const int LocalTickRate = 60;
    public const double TickMs = RateWindowMs / (double)TickRate;
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(TickMs);
    public static readonly TimeSpan LocalTickInterval = TimeSpan.FromSeconds(1d / LocalTickRate);
    public const int MaxMessageBytes = 256 * 1024;
    public const int MaxNameLength = 24;
    public const int MinRoomLength = 12;
    public const int MaxRoomLength = 64;
    public const int StaleMs = 2000;
    public const int MaxRate = TickRate * 2;
    public const string SocketPath = "/play";
    public const int RateWindowMs = 1000;
    public const int SendTimeoutSeconds = 2;
    public const int KeepAliveSeconds = 10;

    public static bool Valid(GameFrame frame)
    {
        return frame.Level is > 0 and <= GameLayout.MaxLevel
            && frame.X is >= -32 and <= GameLayout.Width + 32
            && frame.ScreenY is >= -32 and <= GameLayout.Height + 32 && (frame.Facing & ~GameLayout.FacingMask) == 0
            && (frame.Exits is null || frame.Exits.Up <= GameLayout.MaxLevel && frame.Exits.Right <= GameLayout.MaxLevel
                && frame.Exits.Down <= GameLayout.MaxLevel && frame.Exits.Left <= GameLayout.MaxLevel)
            && frame.Pose < GameLayout.PlayerCharacterLimit && WebRules.Valid(frame.Web);
    }
}
