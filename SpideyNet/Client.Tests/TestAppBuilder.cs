using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(SpideyNet.Client.Tests.TestAppBuilder))]

namespace SpideyNet.Client.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() => new App())
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
