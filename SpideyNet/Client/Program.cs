using Avalonia;

namespace SpideyNet.Client;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppBuilder.Configure(() => new App())
            .UsePlatformDetect()
            .LogToTrace()
            .StartWithClassicDesktopLifetime(args);
    }
}
