using SpideyNet.Core;

namespace SpideyNet.Client;

internal sealed class TeleportRequest(Guid player)
{
    internal Guid Player { get; } = player;
    internal GameFrame? Destination { get; set; }
    internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal long CreatedAt { get; } = Environment.TickCount64;
}
