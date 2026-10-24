using SpideyNet.Core;

namespace SpideyNet.Game;

public sealed record GameSceneUpdate(SharedState? State, Guid? LocalPlayer, byte[]? Puzzles, bool ResumeEnemies = false);
