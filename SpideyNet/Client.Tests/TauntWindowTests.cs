using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SpideyNet.Client.ViewModels;
using SpideyNet.Core;
using Xunit;

namespace SpideyNet.Client.Tests;

public sealed class TauntWindowTests
{
    [AvaloniaFact]
    public async Task SelectorPreviewsSendsRepeatedlyMutesAndStopsOnCloseAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var selector = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(combo => AutomationProperties.GetName(combo) == "Taunt selector");
        Assert.Equal(20, selector.ItemCount);
        Assert.Contains(model.Taunts, item => item.Id == TauntId.SpinWeb && item.Name == "Spin a web");
        Assert.Contains(model.Taunts, item => item.Id == TauntId.ImMatt && item.Name == "I'm Matt");
        Assert.Contains(model.Taunts, item => item.Id == TauntId.ImTim && item.Name == "I'm Tim");
        Assert.Contains(model.Taunts, item => item.Id == TauntId.ImJoe && item.Name == "I'm Joe");
        Assert.Contains(model.Taunts, item => item.Id == TauntId.MattTimJoe && item.Name == "Matt Tim Joe");
        Assert.Equal(TauntId.SpiderMan, model.SelectedTaunt.Id);
        Assert.False(model.SendTauntCommand.CanExecute(null));

        selector.SelectedItem = model.Taunts.Single(item => item.Id == TauntId.ImMatt);
        Dispatcher.UIThread.RunJobs();
        model.PreviewTauntCommand.Execute(null);
        Assert.Equal(TauntId.ImMatt, service.PreviewedTaunt);
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);
        Assert.True(model.SendTauntCommand.CanExecute(null));
        model.SendTauntCommand.Execute(null);
        model.SendTauntCommand.Execute(null);
        Assert.Equal(new[] { TauntId.ImMatt, TauntId.ImMatt }, service.SentTaunts);

        var mute = window.GetVisualDescendants().OfType<CheckBox>()
            .Single(check => Equals(check.Content, "Hear multiplayer taunts"));
        mute.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(service.HearTaunts);
        await model.ShutdownAsync();
        Assert.Equal(1, service.AudioStops);
        Assert.False(model.SendTauntCommand.CanExecute(null));
        window.Close();
    }
}
