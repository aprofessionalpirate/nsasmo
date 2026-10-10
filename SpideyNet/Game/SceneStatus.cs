using SpideyNet.Core;

namespace SpideyNet.Game;

public sealed record SceneStatus(GameScene? Scene, PuzzleTrigger[] Triggers, string TileHash,
    string Video = "", string BackgroundHash = "");
