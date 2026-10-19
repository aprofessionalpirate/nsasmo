using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpideyNet.Client.Services;
using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Client.ViewModels;

internal sealed partial class MainWindowViewModel(IClientService service, IClipboardService clipboard) : ObservableObject
{
    [ObservableProperty]
    private string endpoint = "ws://localhost:5080";

    [ObservableProperty]
    private string roomKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    [ObservableProperty]
    private string playerName = Environment.UserName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private GameProcess? selectedGame;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfigureWorld))]
    private InjectionMode rendering = InjectionMode.Experimental;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FireWebCommand))]
    private InteractionMode interactions = InteractionMode.Experimental;

    [ObservableProperty]
    private SharedMode shared = SharedMode.Experimental;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanConfigureWorld))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand), nameof(RefreshGamesCommand), nameof(FireWebCommand))]
    private bool isAttached;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanConfigureWorld))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(RefreshGamesCommand), nameof(DisconnectCommand), nameof(FireWebCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string status = "Disconnected";

    [ObservableProperty]
    private string location = "Start a VGA game in DOSBox, then connect.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string errorMessage = "";

    public ObservableCollection<GameProcess> Games { get; } = [];
    public ObservableCollection<PlayerViewModel> Players { get; } = [];
    public IReadOnlyList<InjectionMode> RenderingModes { get; } = Enum.GetValues<InjectionMode>();
    public IReadOnlyList<InteractionMode> InteractionModes { get; } = Enum.GetValues<InteractionMode>();
    public IReadOnlyList<SharedMode> SharedModes { get; } = Enum.GetValues<SharedMode>();
    public bool CanEdit => !IsAttached && !IsBusy;
    public bool CanConfigureWorld => CanEdit && Rendering == InjectionMode.Experimental;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasPlayers => Players.Count != 0;

    private bool CanConnect() => CanEdit && SelectedGame is not null;
    private bool CanDisconnect() => IsAttached && !IsBusy;
    private bool CanFireWeb() => IsAttached && !IsBusy && Interactions == InteractionMode.Experimental;

    partial void OnRenderingChanged(InjectionMode value)
    {
        if (value == InjectionMode.Observe)
        {
            // Observe never writes memory; keep dependent options valid when switching modes.
            Interactions = InteractionMode.Disabled;
            Shared = SharedMode.Independent;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task RefreshGamesAsync(CancellationToken token)
    {
        ErrorMessage = "";
        IsBusy = true;
        try
        {
            var selectedId = SelectedGame?.Id;
            var games = await service.FindGamesAsync(token);
            Games.Clear();
            foreach (var game in games)
            {
                Games.Add(game);
            }

            SelectedGame = Games.FirstOrDefault(game => game.Id == selectedId) ?? Games.FirstOrDefault();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            ErrorMessage = $"Cannot find DOSBox: {error.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private void Connect()
    {
        ErrorMessage = "";
        try
        {
            var game = SelectedGame ?? throw new InvalidOperationException("Launch DOSBox, then refresh.");
            service.Connect(new ClientConnection(game.Id, Endpoint, RoomKey, PlayerName, Rendering, Interactions, Shared));
            IsAttached = true;
            Status = "Connecting to relay";
        }
        catch (Exception error)
        {
            ErrorMessage = error.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    private async Task DisconnectAsync()
    {
        IsBusy = true;
        try
        {
            await service.DisconnectAsync();
            IsAttached = false;
            Players.Clear();
            OnPropertyChanged(nameof(HasPlayers));
            Status = "Disconnected";
            Location = "Start a VGA game in DOSBox, then connect.";
        }
        catch (Exception error)
        {
            ErrorMessage = $"Cannot disconnect: {error.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFireWeb))]
    private void FireWeb() => service.FireWeb();

    [RelayCommand]
    private async Task CopyRoomKeyAsync()
    {
        try
        {
            await clipboard.CopyTextAsync(RoomKey);
        }
        catch (Exception error)
        {
            ErrorMessage = $"Cannot copy the room key: {error.Message}";
        }
    }

    internal async Task RefreshAsync()
    {
        if (!IsAttached || IsBusy)
        {
            return;
        }

        var view = service.View;
        Status = view.Status;
        var locationText = view.Local is null ? view.Room
            : $"{view.Room} · X {view.Local.X}, Y {view.Local.Y}";
        if (!string.IsNullOrEmpty(view.Shared))
        {
            locationText += $" · {view.Shared}";
        }

        Location = locationText;

        // Keep row instances stable so frequent position updates preserve selection.
        foreach (var row in Players.Where(row => !view.Players.Any(player => player.Id == row.Id)).ToArray())
        {
            Players.Remove(row);
        }

        foreach (var player in view.Players)
        {
            var row = Players.FirstOrDefault(row => row.Id == player.Id);
            if (row is null)
            {
                row = new PlayerViewModel(player.Id);
                Players.Add(row);
            }

            row.Update(player);
        }

        OnPropertyChanged(nameof(HasPlayers));
        if (view.Status.StartsWith("Stopped:", StringComparison.Ordinal))
        {
            // Finish restoration and unlock reconnect while retaining the failure on screen.
            await DisconnectAsync();
            ErrorMessage = view.Status;
        }
    }

    internal async Task ShutdownAsync()
    {
        RefreshGamesCommand.Cancel();
        if (RefreshGamesCommand.ExecutionTask is { } refresh)
        {
            await refresh;
        }

        if (DisconnectCommand.ExecutionTask is { IsCompleted: false } disconnect)
        {
            await disconnect;
        }

        if (IsAttached)
        {
            await DisconnectAsync();
        }
    }
}
