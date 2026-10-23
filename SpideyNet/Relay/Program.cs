using System.Text.Json;
using SpideyNet.Core;
using SpideyNet.Relay;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
var app = builder.Build();
const int MaxRooms = 128;
var rooms = new Dictionary<string, Room>(StringComparer.Ordinal);
var gate = new object();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(NetRules.KeepAliveSeconds),
    KeepAliveTimeout = TimeSpan.FromSeconds(NetRules.KeepAliveSeconds)
});
app.MapGet("/health", () => Results.Ok(new { protocol = NetRules.Version }));
app.Map(NetRules.SocketPath, async context =>
{
    var key = context.Request.Query["room"].ToString();
    var name = context.Request.Query["name"].ToString().Trim();
    var pvp = context.Request.Query["pvp"].ToString();
    var interactions = pvp == "1" ? InteractionMode.Experimental : InteractionMode.Disabled;
    var sharedMode = context.Request.Query["shared"].ToString() == "1" ? SharedMode.Experimental : SharedMode.Independent;
    if (!context.WebSockets.IsWebSocketRequest || key.Length is < NetRules.MinRoomLength or > NetRules.MaxRoomLength
        || name.Length is 0 or > NetRules.MaxNameLength || name.Any(char.IsControl))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    Room room;
    Peer? peer;
    lock (gate)
    {
        if (!rooms.TryGetValue(key, out room!))
        {
            if (rooms.Count >= MaxRooms)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
            room = new Room(interactions, sharedMode);
            rooms.Add(key, room);
        }
        if (room.Interactions != interactions || room.SharedMode != sharedMode)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            return;
        }
        peer = room.Join(name);
    }
    if (peer is null)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        return;
    }

    long? connectedAt = null;
    var disconnectReason = "Connection closed";
    try
    {
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        connectedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        // Use an independent room ID to correlate players without logging the bearer key.
        app.Logger.LogInformation(
            "Player connected: {PlayerName} ({PlayerId}); address {RemoteAddress}:{RemotePort}; room {RoomId}; players {PlayerCount}; interactions {Interactions}; shared world {SharedMode}",
            peer.Name, peer.Id, context.Connection.RemoteIpAddress, context.Connection.RemotePort,
            room.Id, room.Count, interactions, sharedMode);

        await room.Run(socket, peer, context.RequestAborted);
    }
    catch (Exception error) when (error is IOException or InvalidDataException or System.Net.WebSockets.WebSocketException
        or OperationCanceledException or JsonException)
    {
        // Exception types explain failures without including request URLs or payload contents.
        disconnectReason = context.RequestAborted.IsCancellationRequested ? "Request cancelled" : error.GetType().Name;
    }
    finally
    {
        room.Leave(peer);
        if (connectedAt is { } started)
        {
            var last = peer.State;
            app.Logger.LogInformation(
                "Player disconnected: {PlayerName} ({PlayerId}); address {RemoteAddress}:{RemotePort}; room {RoomId}; players remaining {PlayerCount}; duration {DurationSeconds:F1}s; last game room {GameLevel}; position {X},{Y}; reason {Reason}",
                peer.Name, peer.Id, context.Connection.RemoteIpAddress, context.Connection.RemotePort,
                room.Id, room.Count, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds,
                last?.Frame.Level, last?.Frame.X, last?.Frame.Y, disconnectReason);
        }
    }
});
using var shutdown = new CancellationTokenSource();
var broadcast = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(NetRules.TickInterval);
    try
    {
        while (await timer.WaitForNextTickAsync(shutdown.Token))
        {
            lock (gate)
            {
                foreach (var key in rooms.Keys.ToArray())
                {
                    if (rooms[key].Count == 0) { rooms.Remove(key); continue; }
                    rooms[key].Broadcast();
                }
            }
        }
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
});
try { await app.RunAsync(); }
finally { shutdown.Cancel(); await broadcast; }
