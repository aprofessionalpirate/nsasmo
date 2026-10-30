using SpideyNet.Client.Services;
using SpideyNet.Game;
using SpideyNet.Core;

namespace SpideyNet.Client.Tests;

internal sealed class FakeClientService : IClientService
{
    public SessionView View { get; set; } = new("Disconnected", "", null, []);
    public bool HearTaunts { get; set; } = true;
    internal TauntId? PreviewedTaunt { get; private set; }
    internal List<TauntId> SentTaunts { get; } = [];
    public void PreviewTaunt(TauntId clip) => PreviewedTaunt = clip;
    internal int AudioStops { get; private set; }
    public void StopTaunts() => AudioStops++;
    public bool SendTaunt(TauntId clip)
    {
        SentTaunts.Add(clip);
        return true;
    }

    internal ClientConnection? Connection { get; private set; }
    internal int Disconnects { get; private set; }
    internal int Shots { get; private set; }
    internal Guid? TeleportedPlayer { get; private set; }
    internal Guid? WatchedPlayer { get; private set; }
    internal TaskCompletionSource? DisconnectGate { get; set; }

    public Task<GameProcess[]> FindGamesAsync(CancellationToken token) => Task.FromResult(new[]
    {
        new GameProcess(11, "11 · First DOSBox"), new GameProcess(22, "22 · Second DOSBox")
    });

    public void Connect(ClientConnection connection)
    {
        Connection = connection;
        View = new SessionView("Connected", "Midnight", null, []);
    }

    public async Task DisconnectAsync()
    {
        Disconnects++;
        if (DisconnectGate is { } gate)
        {
            await gate.Task;
        }

        View = new SessionView("Disconnected", "", null, []);
    }

    public Task TeleportAsync(Guid player)
    {
        TeleportedPlayer = player;
        return Task.CompletedTask;
    }

    public void Watch(Guid? player) => WatchedPlayer = player;

    public void FireWeb() => Shots++;
}
