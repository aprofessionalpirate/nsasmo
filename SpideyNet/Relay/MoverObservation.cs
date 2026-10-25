using SpideyNet.Core;

namespace SpideyNet.Relay;

internal sealed record MoverObservation(byte Level, long Epoch, long Sequence, MoverState[] Movers);
