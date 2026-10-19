# SpideyNet

.NET 10 rewrite with a VGA-only game bridge and an Avalonia 12.1.4 desktop client.
The UI uses CommunityToolkit.Mvvm, compiled bindings and asynchronous commands.
The game bridge requires Windows; the original project is unchanged.

Package versions for every SpideyNet project are maintained in
`Directory.Packages.props` using NuGet Central Package Management. Add package
versions there and keep project `PackageReference` entries versionless.

## Run locally

Install the .NET 10 SDK on Windows. From this folder:

```powershell
dotnet build SpideyNet.slnx -m:1
dotnet run --project Relay --no-build -- --urls http://localhost:5080
```

In another terminal:

```powershell
dotnet run --project Client --no-build
```

Run `./Launch-Game.ps1`, select **1: VGA**, choose English, then **1: Play New Game**.
The launcher uses its own temporary DOSBox configuration and leaves your existing settings intact.
VGA is the only supported video mode. Select **1: VGA** in the game menu.
The launcher uses `core=normal`; dynamic cores can cache patched instructions.
Override `GameDirectory` and `DosBox` parameters for other paths.
Refresh DOSBox in the client, select its process, and connect.
Both players must use the same relay address, private room key and
interaction and enemies/puzzles settings. Each runs their own DOSBox instance.

`Experimental` injects native actors. `Observe` only sends locations.
Up to eight players share a room; guests appear only in the same game room.
Remote actors use their own sprite frames and this process's graphics assets.
Independent standing/walking animations and ceiling poses were verified in two
VGA games. Space-fired web lines are shared with other players in the same screen.

## Internet play

Run Relay on a publicly reachable host behind HTTPS with WebSocket
forwarding. Use `wss://your-host` in every client. Clients make outbound
connections; no player port forwarding or UDP hole punching is required.
The relay host must be reachable. No hosted service is provisioned here.

Room keys are bearer secrets. The client generates a 96-bit random key.
Share it privately. Avoid logging `/play` query strings at the reverse proxy.
Use a dedicated relay for trusted players; this is not an account or
competitive anti-cheat system. Deploy one relay instance: rooms live in memory.

Network updates target 30 Hz; local input, sampling and rendering target 60 Hz.
Interpolation buffers two network updates (about 67 ms). Thirty updates/sec halves
update traffic compared with sixty; local interpolation fills the gaps. Actual
bandwidth depends on player count and shared scene data. Each connection has one receive worker
and one send worker, a one-snapshot outbox, message/rate limits, keepalive
deadlines, and a room cap. Clients reconnect with bounded backoff, expire stale
players and interpolate positions with a two-update delay. WebSocket/TCP favors
reachability; packet loss can delay later packets. It is not UDP/QUIC transport.

## Experimental interactions

Select `Experimental` under Player interaction on every client in the room.
Players pass through each other without collision corrections.
Press **V** in DOSBox or click **Fire web** to freeze the nearest player ahead within 64 coordinate units
for 1.2 seconds. The cooldown is 600 ms. Rooms and facing direction are checked
by the relay. This is a separate multiplayer command; it does not hook the
original game's Space key.

Ordinary **Space** shots publish their native endpoints and draw the line on peers
using each game's graphics buffer. This works with player interactions disabled.
With experimental interactions enabled, a line hitting another player attaches
to them and pulls the shooter toward them while Space is held. Release Space,
leave the screen or disconnect to detach. This approximates a tether; walls and
latency can affect the pull and need further playtesting. Player health and scoring
remain independent.

## Shared enemies and puzzles

Choose `Experimental` under **Enemies/puzzles** on every client. This requires
experimental rendering. Choose `Independent` for the previous behavior or Observe.
Client and relay must both use protocol version 5; replace older published builds.

The first active player in each game room owns enemy movement. Other players
receive position, facing, animation, health and death updates. Hits from any player reduce
shared health. Leaving or disconnecting transfers ownership and retains the
room's cached enemy state. Followers skip native enemy AI and display the owner's
state, interpolated at the local render rate. Owners preserve their live native
positions instead of rewinding to a previous network sample. Local player AI,
collision with the environment and puzzle loops remain active. Native enemy AI
resumes on ownership transfer, lost connection or disconnect. Healing and scripted
respawns are not modeled.

Switch states are shared globally, including reversible switches and changes
made in other rooms. Native dirty bits trigger local scripts to update doors,
tiles and scripted effects. Pending changes retry until acknowledged; a joining
player adopts the session's state rather than replacing it.
One player per room reports script results. Followers report native contact/web
latches, preventing replicated scripts from feeding their effects back as input.
Input latches are sampled at 60 Hz; brief events can be missed. Every puzzle and
scripted interaction across all 63 rooms has not been verified.

Moving scenery has separate ownership. A player standing on an object can drive
it even when another player owns the enemies. Helicopters and platforms share
their displacement and movement flags. The bridge rebuilds collision tiles and
the VGA background from local game assets, and carries local riders with position
corrections. No sprite pointers or remote tile payloads cross the network.
Rider detection is approximate; two human players riding together remain unverified.

Use a new room key when starting a new game. Shared progress lives in the relay;
resetting only DOSBox would reapply the previous session's state. Disconnect
removes rendering patches but keeps acquired progress in the local game.

## Memory constraints

Disassembly establishes a 50-byte actor stride and a two-byte actor count.
The apparent two-byte enemy headers are the previous actor's attack fields.
Actor indexes 5–11 hold guests; 12–16 provide private scratch storage.
Earlier indexes retain their shared web data.

The draw loop includes guest slots. Followers restrict the AI loop to the local
player; physics and room scripts retain their original count. Guests receive separate inactive
descriptors. A complete renderer signature and a supported video mode are required.

DOSBox threads are briefly suspended during writes and resumed in cleanup.
Disconnect restores the instruction and reserved bytes. Room transitions
restore the instruction and discard old-room backups. Unexpected count changes
stop injection. Room loading waits for a matching title and a short settling
period; mismatched shared object tables defer that update and retry. Dead actors
publish normalized invisible coordinates. Process suspension does not select an
emulator frame boundary.

Only domain state crosses the network: player/enemy coordinates, animation, health, switch
states, moving object identities and commands. Process-local sprite references
and moving object graphics come from local assets. Remote memory addresses and
raw actor records are never accepted.

Discovery requires both text markers, a plausible player and the exact room
title. It rejects ambiguous layouts, partial reads and menu state. Every update
rechecks the markers. The native driver owns its process handle and scans
bounded chunks from an x64 client. Live verification covers 32-bit DOSBox 0.74-3.

**Injection remains experimental.** Suspension prevents concurrent writes but
does not choose a frame boundary. Abruptly killing the client during suspension
can leave DOSBox paused; a killed client cannot restore the renderer patch.
Restart DOSBox after an abnormal exit. An emulator frame hook is the next step
for stronger guarantees. Other game builds and dynamic cores are unverified.

## Validate

```powershell
dotnet build Tests -m:1
dotnet run --project Tests --no-build
# With the relay running:
dotnet run --project Tests --no-build -- --relay ws://localhost:5080
# With DOSBox in a game room; read only:
dotnet run --project Probe --no-build
# Optional thirty-second injection/restoration check:
dotnet run --project Probe --no-build -- --inject <DOSBox-process-id>
# Inspect native switches, enemies and moving scenery:
dotnet run --project Probe --no-build -- --scene <DOSBox-process-id>
# Two games in the same room; changes switches, scenery and enemy health:
dotnet run --project Probe --no-build -- --pair ws://localhost:5080 <first-id> <second-id>
# Also test a native follower contact in Midnight:
dotnet run --project Probe --no-build -- --pair-contact ws://localhost:5080 <first-id> <second-id>
# Also defeat an enemy and move both games from Midnight to Foyer:
dotnet run --project Probe --no-build -- --pair-transition ws://localhost:5080 <first-id> <second-id>

# Show a guest and move/restore scenery, verifying graphics and collision buffers:
dotnet run --project Probe --no-build -- --video <process-id>

# Production session service: Observe, native PvP corrections and concurrent disconnect:
# Both games in Midnight (Foyer automatically retreats); first player facing right.
dotnet run --project Probe --no-build -- --session ws://localhost:5080 <first-id> <second-id>
# Stop the relay after "Reconnect ready"; restart after "Reconnect outage":
dotnet run --project Probe --no-build -- --session-reconnect ws://localhost:5080 <first-id> <second-id>
# Five Midnight -> Foyer -> Midnight trips, with the other player staying put:
dotnet run --project Probe --no-build -- --session-reentry ws://localhost:5080 <first-id> <second-id>
# Native web-coordinate round trip and tether cleanup, in Midnight:
dotnet run --project Probe --no-build -- --web <process-id>
# Repeated pull/render/release checks; disconnect that game's client first:
dotnet run --project Probe --no-build -- --web-stress <process-id>
# Isolated thread-lifetime regression; no game needed:
dotnet run --project Probe --no-build -- --pause-race
# Stabilize already defeated actors and check native wrapping after detach:
dotnet run --project Probe --no-build -- --inactive <process-id>
# With two desktop clients using Experimental interactions and shared scenes:
# Click Fire web, or manually press V in DOSBox, within sixty seconds.
dotnet run --project Probe --no-build -- --watch-web ws://localhost:5080 <room-key>
```

The session probes compile the production session service directly, keeping its
internal visibility. Live coverage and remaining checks are recorded in [TESTING.md](TESTING.md).
Run Probe from a terminal to see its output. It returns exit code 0 on success,
1 on failure, and 2 on timeout/cancellation. Failures print to stderr after cleanup.
Client session failures also write full exceptions to
`%LOCALAPPDATA%\SpideyNet\client-<process-id>.log`. Native thread errors identify
the failed operation. Logs omit room keys and network traffic.

Tests cover record validation, signed coordinates, actor construction,
interpolation, expiry, player crossing and web range. Relay checks use real sockets
for identities, room isolation, freeze expiry, web attachment/release, fragmentation and
room capacity. Probe hashes reserved bytes before and after restoration.
Malformed, oversized, binary and invalid-room messages are rejected in socket tests.
Shared-state checks cover switch dirty bits, acknowledgements, pending damage,
platform tile rebuilding and authority epochs. Socket checks cover enemy ownership,
follower damage, switches, rider authority, helicopter movement, late joins,
handoff, room reentry and death.

Two DOSBox instances verified helicopter displacement with matching collision
grids, follower switch contact, switch changes, follower damage, enemy death
and Midnight-to-Foyer handoff. Both renderer instructions were restored.
Lift (room 3) and Mummy's Revenge (room 17) also passed production-session
checks for Observe, guest rendering, shared enemy damage, collision flags and
exact renderer/reserved-storage/native-count restoration. These used disposable
level-start fixtures, not full playthroughs. Other rooms, simultaneous human
riders and long sessions remain unverified. See TESTING.md for coverage.

VGA checks displayed a second Spider-Man and displaced Midnight's helicopter.
Collision tiles, all four background planes, actor storage and the renderer restored
exactly. Long VGA sessions remain unverified.

The verified `SPIDEY.EXE` has SHA-256
`0695C840053C920BF198FA203AE64F8A028AA28EA186351AFA0AD4DE9EFD93F0`.
The corrected thirty-second rendering test displayed a second Spider-Man,
kept the physics count at 2, and restored reserved bytes and the renderer.
The initial layout failed visually and was replaced. Long-session stability
and play across separate Internet connections remain unverified.
A live Midnight-to-Foyer transition also completed while the hook was active.
A desktop-client test received a simulated peer through the relay and rendered
that moving peer inside DOSBox. Interaction rules have socket tests; two human
players have not yet tested PvP in separate DOSBox instances.
Desktop disconnect was also checked after relay loss; cancellation returns to
the disconnected UI without blocking its thread.

Release output is in `publish/client` and `publish/relay` after publishing:

```powershell
dotnet publish Client -c Release -o publish/client -m:1
dotnet publish Relay -c Release -o publish/relay -m:1
```

The client requires the .NET 10 runtime; the relay requires the
ASP.NET Core 10 runtime. Keep each published directory intact.

## Layers

```text
Avalonia views (compiled bindings)
    |
View models (commands and observable state)
    |
Client / clipboard services
    |
Session service
    +-- Game bridge ---- Process memory driver ---- DOSBox
    +-- Relay client --- WebSocket framing -------- Relay rooms
```

`Core` contains protocol and gameplay rules. `Transport` contains socket code.
`Game` encapsulates Windows memory access. `Client` owns UI/session lifetime.
`Relay` owns room membership and interaction decisions. `Probe` is diagnostic.
Views contain only window lifetime, screen sizing and UI refresh cadence. View
models depend on service interfaces; platform clipboard and process discovery
stay in service implementations. Shutdown awaits native restoration before exit.

Run the headless Avalonia and MVVM tests with:

```powershell
dotnet test Client.Tests -m:1
```

To check two disposable games in the same room (requires a running relay):

```powershell
dotnet run --project Probe -- --level-pair ws://localhost:5080 <first-pid> <second-pid>
```

The probe connects real production sessions and damages a native enemy; use test
games. It always disconnects both sessions before returning.

Research: [project wiki](https://github.com/aprofessionalpirate/nsasmo/wiki),
[sprite analysis](https://github.com/aprofessionalpirate/nsasmo/wiki/Sprite-Analysis),
[memory searching](https://github.com/aprofessionalpirate/nsasmo/wiki/Memory-Searching).
Transport: [ASP.NET Core WebSockets](https://learn.microsoft.com/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0).
