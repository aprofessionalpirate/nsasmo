using SpideyNet.Game;

namespace SpideyNet.Client.Services;

internal interface IClientService
{
    SessionView View { get; }
    Task<GameProcess[]> FindGamesAsync(CancellationToken token);
    void Connect(ClientConnection connection);
    Task DisconnectAsync();
    void FireWeb();
    Task TeleportAsync(Guid player);
    void Watch(Guid? player);
}
