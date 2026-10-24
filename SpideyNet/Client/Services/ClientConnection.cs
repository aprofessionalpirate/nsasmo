using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Client.Services;

internal sealed record ClientConnection(int ProcessId, string Endpoint, string Room, string Name,
    InjectionMode Rendering, InteractionMode Interactions, SharedMode Shared, bool DifferentColours = false, SuitColour Colour = SuitColour.Red);
