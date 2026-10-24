using SpideyNet.Core;

namespace SpideyNet.Game;

internal static class LevelNames
{
    internal static bool Matches(byte level, string title) => level <= GameLayout.MaxLevel && title == RoomNames.Get(level);
}
