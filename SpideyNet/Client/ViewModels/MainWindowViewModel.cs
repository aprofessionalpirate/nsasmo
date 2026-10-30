using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Avalonia.Media.Imaging;
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
    [NotifyPropertyChangedFor(nameof(CanConfigureWorld), nameof(CanChooseColour))]
    [NotifyCanExecuteChangedFor(nameof(TeleportCommand))]
    private InjectionMode rendering = InjectionMode.Experimental;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FireWebCommand))]
    private InteractionMode interactions = InteractionMode.Experimental;

    [ObservableProperty]
    private SharedMode shared = SharedMode.Experimental;

    [ObservableProperty]
    private bool differentColours = true;

    [ObservableProperty]
    private SuitColour colour = SuitColour.Red;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChooseColour))]
    private SpriteChoice selectedSprite = new(PlayerSpriteRules.SpiderMan, "Spider-Man");

    public IReadOnlyList<SpriteChoice> Sprites { get; } = SpriteCatalogue.Choices;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanConfigureWorld), nameof(CanChooseColour))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand), nameof(RefreshGamesCommand),
        nameof(FireWebCommand), nameof(TeleportCommand), nameof(SpyCommand), nameof(StopSpyingCommand), nameof(SendTauntCommand))]
    private bool isAttached;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanConfigureWorld), nameof(CanChooseColour))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(RefreshGamesCommand), nameof(DisconnectCommand),
        nameof(FireWebCommand), nameof(TeleportCommand), nameof(SpyCommand), nameof(StopSpyingCommand), nameof(SendTauntCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string status = "Disconnected";

    [ObservableProperty]
    private string location = "Start a VGA game in DOSBox, then connect.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string errorMessage = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TeleportCommand), nameof(SpyCommand))]
    private PlayerViewModel? selectedPlayer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopSpyingCommand))]
    [NotifyPropertyChangedFor(nameof(IsSpying))]
    private Guid? watchedPlayer;

    [ObservableProperty]
    private Bitmap? preview;

    [ObservableProperty]
    private string previewStatus = "Select a player and choose Spy to view their game.";

    [ObservableProperty]
    private TauntChoice selectedTaunt = new(TauntId.SpiderMan, "Spider-Man");

    [ObservableProperty]
    private bool hearTaunts = true;

    [ObservableProperty]
    private string tauntStatus = "Preview locally, or send to everyone connected to your relay room.";

    public IReadOnlyList<TauntChoice> Taunts { get; } =
    [
        new(TauntId.Spider, "Spider"),
        new(TauntId.Man, "Man"),
        new(TauntId.SpiderMan, "Spider-Man"),
        new(TauntId.SpinWeb, "Spin web"),
        new(TauntId.OhPeter, "Oh, Peter")
    ];

    partial void OnHearTauntsChanged(bool value) => service.HearTaunts = value;

    private bool CanSendTaunt() => IsAttached && !IsBusy;

    [RelayCommand]
    private void PreviewTaunt()
    {
        try
        {
            service.PreviewTaunt(SelectedTaunt.Id);
            TauntStatus = $"Preview: {SelectedTaunt.Name}";
        }
        catch (Exception error)
        {
            ErrorMessage = $"Cannot play taunt: {error.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanSendTaunt))]
    private void SendTaunt()
    {
        TauntStatus = service.SendTaunt(SelectedTaunt.Id)
            ? $"Sent: {SelectedTaunt.Name}"
            : "Wait for a relay connection and a playable game; the taunt queue may also be full.";
    }

    private PlayerScreen? displayedScreen;
    public bool IsSpying => WatchedPlayer is not null;

    public ObservableCollection<GameProcess> Games { get; } = [];
    public ObservableCollection<PlayerViewModel> Players { get; } = [];
    public IReadOnlyList<InjectionMode> RenderingModes { get; } = Enum.GetValues<InjectionMode>();
    public IReadOnlyList<InteractionMode> InteractionModes { get; } = Enum.GetValues<InteractionMode>();
    public IReadOnlyList<SuitColour> SuitColours { get; } = Enum.GetValues<SuitColour>();
    public IReadOnlyList<SharedMode> SharedModes { get; } = Enum.GetValues<SharedMode>();
    public bool CanEdit => !IsAttached && !IsBusy;
    public bool CanConfigureWorld => CanEdit && Rendering == InjectionMode.Experimental;
    public bool CanChooseColour => CanConfigureWorld && SelectedSprite.Frame == PlayerSpriteRules.SpiderMan;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasPlayers => Players.Count != 0;

    private bool CanTarget() => IsAttached && !IsBusy && SelectedPlayer is not null;
    private bool CanTeleport() => CanTarget() && Rendering == InjectionMode.Experimental;
    private bool CanStopSpying() => IsAttached && WatchedPlayer is not null;

    [RelayCommand(CanExecute = nameof(CanTeleport))]
    private async Task TeleportAsync()
    {
        var target = SelectedPlayer;
        if (target is null)
        {
            return;
        }

        ErrorMessage = "";
        IsBusy = true;
        try
        {
            await service.TeleportAsync(target.Id);
        }
        catch (Exception error)
        {
            ErrorMessage = $"Cannot teleport: {error.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTarget))]
    private void Spy()
    {
        ClearPreview();
        WatchedPlayer = SelectedPlayer?.Id;
        service.Watch(WatchedPlayer);
        PreviewStatus = $"Watching {SelectedPlayer?.Name} · waiting for game screen";
    }

    [RelayCommand(CanExecute = nameof(CanStopSpying))]
    private void StopSpying()
    {
        service.Watch(null);
        WatchedPlayer = null;
        ClearPreview();
        PreviewStatus = "Select a player and choose Spy to view their game.";
    }

    private void ClearPreview()
    {
        var previous = Preview;
        Preview = null;
        previous?.Dispose();
        displayedScreen = null;
    }

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
            service.Connect(new ClientConnection(game.Id, Endpoint, RoomKey, PlayerName, Rendering, Interactions, Shared, DifferentColours, Colour, SelectedSprite.Frame));
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
            StopSpying();
            SelectedPlayer = null;
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
            : $"{view.Room} · X {view.Local.X}, Y {view.Local.ScreenY}";
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

        if (SelectedPlayer is not null && !Players.Contains(SelectedPlayer))
        {
            SelectedPlayer = null;
        }

        if (WatchedPlayer is { } watched)
        {
            var target = view.Players.FirstOrDefault(player => player.Id == watched);
            if (target is null)
            {
                StopSpying();
                PreviewStatus = "The watched player disconnected.";
            }
            else if (view.Screen is { } screen && screen.Player == watched)
            {
                if (!ReferenceEquals(screen, displayedScreen))
                {
                    try
                    {
                        var image = PreviewImageFactory.Create(screen.Frame);
                        ClearPreview();
                        Preview = image;
                        displayedScreen = screen;
                        PreviewStatus = $"{target.Name} · {RoomNames.Get(target.Frame.Level)} · live preview (4 fps)";
                    }
                    catch (Exception error) when (error is IOException or ArgumentException)
                    {
                        ClearPreview();
                        PreviewStatus = "The player's game screen is unavailable.";
                    }
                }
            }
            else
            {
                ClearPreview();
                PreviewStatus = $"{target.Name} · waiting for game screen (keep DOSBox restored)";
            }
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

        service.StopTaunts();
    }
}
