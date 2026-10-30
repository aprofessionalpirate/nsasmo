using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SpideyNet.Client.Services;
using SpideyNet.Client.ViewModels;
using SpideyNet.Core;
using SpideyNet.Game;
using Xunit;

namespace SpideyNet.Client.Tests;

public sealed class PlayerSpriteWindowTests
{
    [AvaloniaFact]
    public async Task SpriteSelectionUsesBindingsAndReachesTheSessionAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        var window = new MainWindow { DataContext = model };
        window.Show();
        await model.RefreshGamesCommand.ExecuteAsync(null);
        var selector = window.GetVisualDescendants().OfType<ComboBox>()
            .Single(combo => AutomationProperties.GetName(combo) == "Your sprite");
        Assert.Equal(44, selector.ItemCount);
        Assert.Equal(PlayerSpriteRules.SpiderMan, model.SelectedSprite.Frame);
        selector.SelectedItem = model.Sprites.Single(sprite => sprite.Frame == 0x32);
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.CanChooseColour);
        model.ConnectCommand.Execute(null);
        Assert.Equal((byte)0x32, service.Connection?.Sprite);
        Assert.False(selector.IsEffectivelyEnabled);
        await model.ShutdownAsync();
        model.Rendering = InjectionMode.Observe;
        Dispatcher.UIThread.RunJobs();
        Assert.False(selector.IsEffectivelyEnabled);
        window.Close();
    }

    [Fact]
    public void InvalidSpriteIsRejectedBeforeMemoryAccess()
    {
        var service = new ClientService();
        var connection = new ClientConnection(1, "ws://localhost:5080", "0123456789ABCDEF", "Test",
            InjectionMode.Experimental, InteractionMode.Disabled, SharedMode.Independent, Sprite: 0x5F);
        Assert.Throws<ArgumentException>(() => service.Connect(connection));
        Assert.Equal("Disconnected", service.View.Status);
    }
}
