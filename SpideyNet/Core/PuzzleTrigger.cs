namespace SpideyNet.Core;

public sealed record PuzzleTrigger(int Index, byte X, byte Y, TriggerKind Kind, string Script = "");
