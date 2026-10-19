using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace SpideyNet.Client.Services;

internal sealed class ClipboardService(TopLevel window) : IClipboardService
{
    public async Task CopyTextAsync(string text)
    {
        var clipboard = window.Clipboard ?? throw new InvalidOperationException("The clipboard is not available.");
        await clipboard.SetTextAsync(text);
    }
}
