using SpideyNet.Core;

namespace SpideyNet.Game;

public sealed record GameStatus(GameFrame? Frame, string Title, ushort EnemyCount, string SlotHash,
    string ActorHex, string TableHex, string RenderHex);
