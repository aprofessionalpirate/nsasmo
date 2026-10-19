using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SpideyNet.Client.ViewModels;
using Xunit;

namespace SpideyNet.Client.Tests;

public sealed class WindowTests
{
    [AvaloniaFact]
    public async Task ClosingWaitsForSessionRestorationAsync()
    {
        var gate = new TaskCompletionSource();
        var service = new FakeClientService { DisconnectGate = gate };
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);

        // The game must be restored before the desktop lifetime can exit.
        window.Close();
        Assert.True(window.IsVisible);
        Assert.Equal(1, service.Disconnects);
        gate.SetResult();
        await model.ShutdownAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsVisible);
        Assert.False(model.IsAttached);
    }

    [AvaloniaFact]
    public async Task CompiledBindingsAcceptInputAndExecuteConnectionCommandAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        await model.RefreshGamesCommand.ExecuteAsync(null);

        var name = window.GetVisualDescendants().OfType<TextBox>()
            .Single(control => AutomationProperties.GetName(control) == "Your name");
        name.Focus();
        name.SelectAll();
        window.KeyTextInput("Headless player");
        Assert.Equal("Headless player", model.PlayerName);

        var connect = window.GetVisualDescendants().OfType<Button>()
            .Single(control => Equals(control.Content, "Connect"));
        var point = connect.TranslatePoint(new Point(connect.Bounds.Width / 2, connect.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.IsAttached);
        Assert.Equal("Headless player", service.Connection?.Name);
        Assert.False(name.IsEffectivelyEnabled);
        await model.ShutdownAsync();
        window.Close();
    }
}
