using SpideyNet.Game;
using SpideyNet.Core;

namespace SpideyNet.Client.Services;

internal interface IClientService
{
    SessionView View { get; }
    bool HearTaunts { get; set; }
    void PreviewTaunt(TauntId clip);
    void StopTaunts();
    bool SendTaunt(TauntId clip);
    Task<GameProcess[]> FindGamesAsync(CancellationToken token);
    void Connect(ClientConnection connection);
    Task DisconnectAsync();
    void FireWeb();
    Task TeleportAsync(Guid player);
    void Watch(Guid? player);
}
