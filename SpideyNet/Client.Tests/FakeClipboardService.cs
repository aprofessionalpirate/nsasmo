using SpideyNet.Client.Services;

namespace SpideyNet.Client.Tests;

internal sealed class FakeClipboardService : IClipboardService
{
    internal string? Text { get; private set; }

    public Task CopyTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}
