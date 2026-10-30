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
        Assert.Equal(5, selector.ItemCount);
        Assert.Equal(TauntId.SpiderMan, model.SelectedTaunt.Id);
        Assert.False(model.SendTauntCommand.CanExecute(null));

        selector.SelectedItem = model.Taunts.Single(item => item.Id == TauntId.OhPeter);
        Dispatcher.UIThread.RunJobs();
        model.PreviewTauntCommand.Execute(null);
        Assert.Equal(TauntId.OhPeter, service.PreviewedTaunt);
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);
        Assert.True(model.SendTauntCommand.CanExecute(null));
        model.SendTauntCommand.Execute(null);
        model.SendTauntCommand.Execute(null);
        Assert.Equal(new[] { TauntId.OhPeter, TauntId.OhPeter }, service.SentTaunts);

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
