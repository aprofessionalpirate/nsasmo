using SpideyNet.Core;
using SpideyNet.Client.Audio;
using SpideyNet.Game;

namespace SpideyNet.Client.Services;

internal sealed class ClientService : IClientService
{
    private readonly Session session;
    private readonly TauntPlayer taunts = new(new WaveAudioOutput());
    private bool hearTaunts = true;
    private readonly object audioGate = new();

    internal ClientService()
    {
        session = new Session(ReceiveTaunt);
    }

    public bool HearTaunts
    {
        get => Volatile.Read(ref hearTaunts);
        set
        {
            lock (audioGate)
            {
                Volatile.Write(ref hearTaunts, value);
                if (!value)
                {
                    taunts.Stop();
                }
            }
        }
    }

    public void PreviewTaunt(TauntId clip) => taunts.Play(clip);

    public void StopTaunts() => taunts.Stop();

    public bool SendTaunt(TauntId clip) => session.SendTaunt(clip);

    private void ReceiveTaunt(TauntEvent taunt)
    {
        lock (audioGate)
        {
            if (!HearTaunts)
            {
                return;
            }

            try
            {
                taunts.Play(taunt.Clip);
            }
            catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception)
            {
                // An unavailable audio device must not disconnect the game or memory worker.
                SessionDiagnostics.Record(error, 0);
            }
        }
    }

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

        if (!PlayerSpriteRules.Valid(connection.Sprite))
        {
            throw new ArgumentException("Choose a supported player sprite.");
        }

        if (!Enum.IsDefined(connection.Colour))
        {
            throw new ArgumentException("Choose a supported Spidey colour.");
        }

        session.Connect(connection.ProcessId, endpoint, room, name,
            connection.Rendering, connection.Interactions, connection.Shared, connection.DifferentColours, connection.Colour, connection.Sprite);
    }

    public async Task DisconnectAsync()
    {
        await session.DisconnectAsync();
        taunts.Stop();
    }

    public Task TeleportAsync(Guid player) => session.TeleportAsync(player);

    public void Watch(Guid? player) => session.Watch(player);

    public void PushPlayer() => session.PushPlayer();
}
