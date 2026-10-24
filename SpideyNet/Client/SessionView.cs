using SpideyNet.Core;

namespace SpideyNet.Client;

internal sealed record SessionView(string Status, string Room, GameFrame? Local, PlayerState[] Players, string Shared = "", PlayerScreen? Screen = null);
