using SpideyNet.Core;
using SpideyNet.Game;

namespace SpideyNet.Client.Services;

internal sealed class ClientService : IClientService
{
    private readonly Session session = new();

    public SessionView View => session.View;

    public Task<GameProcess[]> FindGamesAsync(CancellationToken token)
    {
        return Task.Run(GameProcess.Find, token);
    }

    public void Connect(ClientConnection connection)
    {
        // Reject invalid combinations before starting the network or memory worker.
        if (!Uri.TryCreate(connection.Endpoint.Trim(), UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("ws" or "wss"))
        {
            throw new ArgumentException("Enter a WebSocket relay address, such as ws://localhost:5080.");
        }

        var room = connection.Room.Trim();
        var name = connection.Name.Trim();
        if (room.Length is < NetRules.MinRoomLength or > NetRules.MaxRoomLength || room.Any(char.IsControl))
        {
            throw new ArgumentException($"Room keys must contain {NetRules.MinRoomLength}–{NetRules.MaxRoomLength} characters.");
        }

        if (name.Length is 0 or > NetRules.MaxNameLength || name.Any(char.IsControl))
        {
            throw new ArgumentException($"Enter a player name of up to {NetRules.MaxNameLength} characters.");
        }

        if (connection.Rendering == InjectionMode.Observe
            && (connection.Interactions != InteractionMode.Disabled || connection.Shared != SharedMode.Independent))
        {
            throw new ArgumentException("Observe mode requires disabled interactions and independent enemies/puzzles.");
        }

        session.Connect(connection.ProcessId, endpoint, room, name,
            connection.Rendering, connection.Interactions, connection.Shared);
    }

    public Task DisconnectAsync() => session.DisconnectAsync();

    public void FireWeb() => session.FireWeb();
}
