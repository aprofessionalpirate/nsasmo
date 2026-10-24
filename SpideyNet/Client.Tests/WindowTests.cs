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
    public async Task ColourChoiceUsesCompiledBindingAndReachesSessionOptionsAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        await model.RefreshGamesCommand.ExecuteAsync(null);
        var choice = window.GetVisualDescendants().OfType<CheckBox>().Single();
        Assert.True(model.DifferentColours);
        choice.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.DifferentColours);
        choice.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.DifferentColours);
        var suit = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(combo => Equals(Avalonia.Automation.AutomationProperties.GetName(combo), "Your Spidey colour"));
        suit.SelectedItem = SpideyNet.Core.SuitColour.Purple;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(SpideyNet.Core.SuitColour.Purple, model.Colour);
        model.ConnectCommand.Execute(null);
        Assert.True(service.Connection?.DifferentColours);
        Assert.Equal(SpideyNet.Core.SuitColour.Purple, service.Connection?.Colour);
        Assert.False(suit.IsEffectivelyEnabled);
        Assert.False(choice.IsEffectivelyEnabled);
        await model.ShutdownAsync();
        model.Rendering = SpideyNet.Game.InjectionMode.Observe;
        Dispatcher.UIThread.RunJobs();
        Assert.False(choice.IsEffectivelyEnabled);
        Assert.False(suit.IsEffectivelyEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task PlayerSelectionEnablesBoundActionsAndDisplaysPreviewAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);
        var player = new SpideyNet.Core.PlayerState(Guid.NewGuid(), "Guest",
            new SpideyNet.Core.GameFrame(17, 90, 147, 0, 24), 1);
        service.View = service.View with { Players = [player] };
        await model.RefreshAsync();
        Dispatcher.UIThread.RunJobs();
        var roster = window.GetVisualDescendants().OfType<ListBox>().Single();
        roster.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(player.Id, model.SelectedPlayer?.Id);
        var spy = window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Spy"));
        Assert.True(spy.IsEffectivelyEnabled);
        spy.Command!.Execute(null);
        Assert.Equal(player.Id, service.WatchedPlayer);

        using var data = new MemoryStream();
        using (var encoder = new System.IO.Compression.DeflateStream(data, System.IO.Compression.CompressionLevel.Fastest, true))
        {
            encoder.Write(new byte[SpideyNet.Core.ScreenFrame.RawBytes]);
        }

        service.View = service.View with { Screen = new SpideyNet.Core.PlayerScreen(player.Id,
            new SpideyNet.Core.ScreenFrame(1, data.ToArray())) };
        await model.RefreshAsync();
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(model.Preview);
        Assert.Same(model.Preview, window.GetVisualDescendants().OfType<Image>().Single().Source);
        model.StopSpyingCommand.Execute(null);
        Assert.Null(model.Preview);
        await model.ShutdownAsync();
        window.Close();
    }

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
