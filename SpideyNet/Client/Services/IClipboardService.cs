namespace SpideyNet.Client.Services;

internal interface IClipboardService
{
    Task CopyTextAsync(string text);
}
