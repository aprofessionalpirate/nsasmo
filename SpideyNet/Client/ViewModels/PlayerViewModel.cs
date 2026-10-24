using CommunityToolkit.Mvvm.ComponentModel;
using SpideyNet.Core;

namespace SpideyNet.Client.ViewModels;

internal sealed partial class PlayerViewModel(Guid id) : ObservableObject
{
    internal Guid Id { get; } = id;

    [ObservableProperty]
    private string name = "";

    [ObservableProperty]
    private string room = "";

    [ObservableProperty]
    private string position = "";

    internal void Update(PlayerState player)
    {
        Name = player.Name;
        Room = RoomNames.Get(player.Frame.Level);
        Position = $"{player.Frame.X}, {player.Frame.ScreenY}";
    }
}
