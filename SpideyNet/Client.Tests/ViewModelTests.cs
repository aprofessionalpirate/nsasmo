using Avalonia.Headless.XUnit;
using SpideyNet.Client.Services;
using SpideyNet.Client.ViewModels;
using SpideyNet.Core;
using SpideyNet.Game;
using Xunit;

namespace SpideyNet.Client.Tests;

public sealed class ViewModelTests
{
    [AvaloniaFact]
    public void DefaultsEnableExperimentalInteractions()
    {
        var model = new MainWindowViewModel(new FakeClientService(), new FakeClipboardService());
        Assert.Equal(InteractionMode.Experimental, model.Interactions);
        Assert.Equal(InjectionMode.Experimental, model.Rendering);
        Assert.Equal(SharedMode.Experimental, model.Shared);
    }

    [AvaloniaFact]
    public void ObserveSelectionDisablesMemoryWritingOptions()
    {
        var model = new MainWindowViewModel(new FakeClientService(), new FakeClipboardService())
        {
            Interactions = InteractionMode.Experimental,
            Rendering = InjectionMode.Observe
        };
        Assert.Equal(InteractionMode.Disabled, model.Interactions);
        Assert.Equal(SharedMode.Independent, model.Shared);
        Assert.False(model.CanConfigureWorld);
    }

    [AvaloniaFact]
    public async Task ConnectionCommandsLockConfigurationAndRestoreItAsync()
    {
        var service = new FakeClientService();
        var clipboard = new FakeClipboardService();
        var model = new MainWindowViewModel(service, clipboard) { Interactions = InteractionMode.Experimental };
        await model.RefreshGamesCommand.ExecuteAsync(null);
        Assert.True(model.ConnectCommand.CanExecute(null));
        model.ConnectCommand.Execute(null);
        Assert.Equal(11, service.Connection?.ProcessId);
        Assert.False(model.CanEdit);
        Assert.False(model.RefreshGamesCommand.CanExecute(null));
        model.FireWebCommand.Execute(null);
        Assert.Equal(1, service.Shots);
        await model.CopyRoomKeyCommand.ExecuteAsync(null);
        Assert.Equal(model.RoomKey, clipboard.Text);
        await model.DisconnectCommand.ExecuteAsync(null);
        Assert.True(model.CanEdit);
        Assert.False(model.FireWebCommand.CanExecute(null));
        Assert.Equal(1, service.Disconnects);
        Assert.DoesNotContain("Enemy owner", model.Location);
    }

    [AvaloniaFact]
    public async Task FailedSessionUnlocksReconnectAndRetainsItsErrorAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);
        service.View = new SessionView("Stopped: simulated driver error", "", null, []);
        await model.RefreshAsync();
        Assert.True(model.CanEdit);
        Assert.True(model.ConnectCommand.CanExecute(null));
        Assert.Contains("simulated driver error", model.ErrorMessage);
    }

    [AvaloniaFact]
    public async Task RosterUpdatesPreserveRowsAndRemoveDepartedPlayersAsync()
    {
        var service = new FakeClientService();
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);
        var frame = new GameFrame(3, 100, 100, 0, 0x18);
        var player = new PlayerState(Guid.NewGuid(), "Guest", frame, 1);
        service.View = new SessionView("Connected", "Lift", frame, [player], "Enemy owner: you");
        await model.RefreshAsync();
        var row = Assert.Single(model.Players);
        var locationChanges = 0;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(model.Location))
            {
                locationChanges++;
            }
        };
        service.View = service.View with { Players = [player with { Frame = frame with { X = 110 } }] };
        await model.RefreshAsync();
        Assert.Same(row, Assert.Single(model.Players));
        Assert.Equal("110, 100", row.Position);
        Assert.Contains("Enemy owner: you", model.Location);
        Assert.Equal(0, locationChanges);
        service.View = service.View with { Players = [] };
        await model.RefreshAsync();
        Assert.False(model.HasPlayers);
    }

    [AvaloniaFact]
    public async Task ShutdownAwaitsDisconnectCleanupAsync()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakeClientService { DisconnectGate = gate };
        var model = new MainWindowViewModel(service, new FakeClipboardService());
        await model.RefreshGamesCommand.ExecuteAsync(null);
        model.ConnectCommand.Execute(null);
        var disconnect = model.DisconnectCommand.ExecuteAsync(null);
        var shutdown = model.ShutdownAsync();
        Assert.False(shutdown.IsCompleted);
        Assert.False(model.CanEdit);
        gate.SetResult();
        await Task.WhenAll(disconnect, shutdown);
        Assert.Equal(1, service.Disconnects);
        Assert.False(model.IsAttached);
    }

    [Fact]
    public void ObserveValidationRunsBeforeStartingSession()
    {
        var service = new ClientService();
        var connection = new ClientConnection(11, "ws://localhost:5080", "0123456789ABCDEF", "Test",
            InjectionMode.Observe, InteractionMode.Experimental, SharedMode.Independent);
        Assert.Throws<ArgumentException>(() => service.Connect(connection));
        Assert.Equal("Disconnected", service.View.Status);
        Assert.Throws<ArgumentException>(() => service.Connect(connection with { Endpoint = "https://example.com" }));
    }
}
