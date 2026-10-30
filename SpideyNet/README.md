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

## Find, join and watch players

The player list shows native room names and health out of 100 (the game uses
192 for full health). Health is read without changing game memory. Older clients
that do not report health appear as **Unknown**; update each client to share it.
Use protocol 9 clients and relay for the current feature set.

Select a player, then choose
**Teleport to player** to load their room and move to the position reported
as you arrive, using the latest report in the destination room. Facing and
collision origins match the target; normal game physics resumes immediately.
This requires Experimental rendering. Wait until the target
is fully inside their room and release your current web before teleporting.
The native game loads the destination; shared enemies and puzzles resume there.

Choose **Spy** to see that player's actual DOSBox screen and movements in the
client, including players in other rooms. Your own character stays in place.
**Stop spying** ends the stream. Spying also works with Observe rendering.
Keep the watched DOSBox window restored; minimized windows have no preview.
Only the DOSBox client area is captured, without changing focus.
Scaled windows use colour-preserving downsampling; update the watched player's
client as well to fix dark previews from older builds.

Previews use 320 x 200 pixels, RGB565 and compression, at up to four images/sec.
Capture starts only while watched. A shared capture serves multiple watchers;
the relay routes each image only to its subscribers. Position updates remain
at 30/sec. All clients and the relay must use protocol 9 for the current feature set.

## Voice taunts

Choose one of five extracted voice clips under **Taunts**, then choose **Preview
taunt** to hear it locally or **Send taunt** to send it to everyone connected to
your private relay room, including players on other game screens. The sender
also hears the relay-confirmed taunt. **Hear multiplayer taunts** mutes received
clips locally; Preview remains available while disconnected or muted.

There is no cooldown. A new clip replaces the current clip rather than queuing
delayed audio. A bounded network queue uses the existing update cadence.
Repeated movement snapshots do not replay taunts, and joins/reconnects skip old
history. Audio-device failures do not disconnect the game.

The five voice instruments come from the supplied `spidey3.mod` and are embedded
in the single-file client; only catalogue IDs cross the network. See the
[audio provenance and extraction notes](Client/Assets/Taunts/README.md).

**Update both clients and relay to protocol 9** for taunts. Older protocol 8
builds cannot connect to this relay.

## Choose your suit colour

Before connecting, choose **Your suit colour**: Red (the original suit), Cyan,
Green, Purple, Orange, Blue, White or Grey. With **Show chosen suit colours
(including yours)** enabled, your own Spider-Man and remote players use their
chosen colours. Blue clothing, eyes, outlines and shading retain their detail.
The choice follows each player's identity across rooms; duplicate colours are allowed.

Uncheck that option to display original suits locally. Other players can still
see your chosen colour. Observe mode never recolours game memory. Disconnect
before changing your selection. Upgrade the relay and all clients to protocol 9.

Rendering transactions wait until the native game has finished copying and drawing
all four VGA planes. Suspending DOSBox mid-copy is insufficient: an animated suit
can otherwise combine different poses in one displayed frame. Disconnect restores
the native call sites. A small inert trampoline remains in a VGA-inactive code
branch until DOSBox closes, so detachment cannot overwrite instructions it is
still returning through. No palette or sprite assets are changed.

Player positions also preserve the native game's wrapped top-edge coordinates:
Y=250 represents six pixels above the room. Recolouring, remote visibility,
interpolation and web interactions continue while a player is partially visible.
These wrapped positions require updated clients and relay; the current protocol is 9.

## Host the relay with Docker

Install Docker Desktop and use Linux containers. From the repository root:

```powershell
docker compose -f SpideyNet/compose.yaml up -d --build
Invoke-RestMethod http://127.0.0.1:5080/health
```

The health response reports protocol 9. Use `ws://127.0.0.1:5080` in the client.
Only the relay runs in Docker; DOSBox and the desktop client remain on Windows.
The server needs no host .NET installation. The client still needs its runtime.

The default port binding accepts connections only from this PC. It does not
enable Internet access. For trusted LAN play, explicitly select this PC's private
IPv4 address before starting (replace the example address):

```powershell
$env:RELAY_BIND_IP = "192.168.1.50"
docker compose -f SpideyNet/compose.yaml up -d --build
Remove-Item Env:RELAY_BIND_IP
```

Players then use `ws://192.168.1.50:5080`. Allow inbound TCP 5080 from the local
subnet on the Windows Firewall Private profile if needed. LAN traffic and room
keys are unencrypted. To return to local-only hosting, run the first command
again with `RELAY_BIND_IP` unset; Compose recreates the port mapping.

```powershell
docker compose -f SpideyNet/compose.yaml logs -f relay
docker compose -f SpideyNet/compose.yaml down
```

The container runs as a non-root user with a read-only filesystem, dropped Linux
capabilities, bounded logs, and memory/CPU/process limits. It mounts no host files
or Docker socket. Docker reduces access to the host; it does not make a public
service risk-free. Keep Docker Desktop updated. This setup provides no HTTPS;
Internet hosting still requires a trusted TLS endpoint and `wss://`.

Run one relay container. Restarting/rebuilding it disconnects players and clears
rooms and shared progress. Rebuild with `up -d --build` to pick up relay and base
image updates; matching clients must use the same protocol version.

## Play on a local network

Double-click [Run-Server-LAN.bat](../Run-Server-LAN.bat) at the repository root.
It publishes the Release relay to `outputs`, detects your private IPv4 address,
and prints the `ws://` connection URL for players. If several LAN connections
are available, choose the one your players use. Keep the console open while
hosting; it shows player logs. Press Ctrl+C to stop. The window waits for a key
when the script exits, including on build/startup failure.

You can also choose the address and port explicitly:

```powershell
.\Run-Server-LAN.bat -Address 192.168.178.45 -Port 5000
```

The launcher requires the .NET 10 SDK, binds only to the selected address and
does not change Windows Firewall rules. It works from any current directory.

On the relay computer, bind to its LAN address instead of localhost:

```powershell
.\Relay.exe --urls http://192.168.178.45:5000
```

Use `ws://192.168.178.45:5000` in every client. The address shown is an example;
substitute the relay computer's actual address. Plain `ws://` is allowed for
loopback, private IPv4 literals (10/8, 172.16/12, 192.168/16) and IPv6 unique-local
address literals. Use it only on a trusted LAN: room keys and traffic are unencrypted.
LAN hostnames and public addresses require `wss://`.

Open `http://192.168.178.45:5000/health` from a player computer to verify reachability;
it should return `{"protocol":9}`. If it times out, check the relay's listening
address, Windows Firewall inbound TCP port 5000 on the Private profile, and network
isolation between devices. With no endpoint configuration, the relay listens only
on `http://localhost:5000`, which another computer cannot reach.

`wss://` requires an HTTPS listener and a trusted certificate valid for the server
address. An HTTP listener cannot become HTTPS by changing the client's URL.
For a TLS relay, use `https://` for its health endpoint and `wss://` in the client.

## Internet play

Run Relay on a publicly reachable host behind HTTPS with WebSocket
forwarding. Use `wss://your-host` in every client. Clients make outbound
connections; no player port forwarding or UDP hole punching is required.
The relay host must be reachable. No hosted service is provisioned here.

For a single-task AWS deployment without a domain or load balancer, see
[the ECS Fargate trial](deploy/ecs/README.md). Its public IP changes when the task
is replaced. It provisions billable resources only when its deployment script runs.
Use [Run-Server.bat](../Run-Server.bat) at the repository root to build and host the
current source. After pulling a newer revision, run
[Update-Server.bat](../Update-Server.bat) to rebuild and replace that existing
server. It keeps the old service running during build/upload, then resets
sessions and prints the new endpoint after deployment. It uses the same saved
settings and also accepts `-SettingsPath` and `-BaseImages Cached`. No automatic
Git pull is performed. [Stop-Server.bat](../Stop-Server.bat) removes the ECS deployment,
logs and image repositories. The next run rebuilds and recreates them.

Room keys are bearer secrets. The client generates a 96-bit random key.
Share it privately. Avoid logging `/play` query strings at the reverse proxy.
Use a dedicated relay for trusted players; this is not an account or
competitive anti-cheat system. Deploy one relay instance: rooms live in memory.

The relay logs accepted player connections and disconnections at Information
level. Entries include player name/ID, socket IP/port, an independent room ID
and player count. Connections also show interaction/shared-world modes;
disconnects show duration, last game room/position and an ending reason. Room
keys and request query strings are not included. Behind a reverse proxy, the
logged socket address is the proxy's address.

Network updates target 30 Hz; local input, sampling and rendering target 60 Hz.
Interpolation buffers two network updates (about 67 ms). Thirty updates/sec halves
update traffic compared with sixty; local interpolation fills the gaps. Actual
bandwidth depends on player count and shared scene data. Each connection has one receive worker
and one send worker, a one-snapshot outbox, message/rate limits, keepalive
deadlines, and a room cap. Clients reconnect with bounded backoff, expire stale
players and interpolate positions with a two-update delay. WebSocket/TCP favors
reachability; packet loss can delay later packets. It is not UDP/QUIC transport.

## Experimental interactions

Player interactions default to `Experimental`. Every client in the room must
use the same setting; choose `Disabled` to opt out. Observe mode disables them.
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
Client and relay must both use protocol version 8; replace older published builds.

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
Rider contact is checked against the platform from the same player's report, rather
than a relay position that may be one tile behind. The current rider retains control
while in contact. A new driver adopts the cached position without losing a native
launch/stop command fired during handoff. Rider corrections move current bounds and
previous coordinates together.

Coloured player/web overlays restore touched cells from the current native tile map.
They do not restore old helicopter pixels after its artwork has moved. Native moving
scenery still advances in eight-pixel tile steps. Multiple human riders remain approximate;
a three-game follower launch/stop and owner handoff were tested.

Use a new room key when starting a new game. Shared progress lives in the relay;
resetting only DOSBox would reapply the previous session's state. Disconnect
removes rendering patches but keeps acquired progress in the local game.

Enemy ownership follows arrival order in each native game room: the first active
player to enter owns the enemies until they leave, disconnect or time out. The
next earliest occupant then takes over. Returning to the room counts as a new
arrival. Lift/helicopter driver ownership still follows rider contact separately.

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
# Top/left/right/bottom native clipping, including horizontal crouch and turn poses:
dotnet run --project Probe --no-build -- --edge-sprites <process-id>
# Pixel-for-pixel native pose references and rapid animated frame consistency:
dotnet run --project Probe --no-build -- --animated-sprites <process-id>
# Local suit choices and exact native state restoration:
dotnet run --project Probe --no-build -- --suits <process-id>
# Seven animated guest colours, native fallback and exact graphics restoration:
dotnet run --project Probe --no-build -- --colours <process-id>
# Seven socket peers and native coloured-room transitions; disposable game only:
dotnet run --project Probe --no-build -- --colours-session ws://localhost:5080 <process-id>
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

Client and relay publish as framework-dependent single-file Windows x64 executables.

To build and launch just the client, double-click
[Run-Client.bat](../Run-Client.bat) at the repository root. It publishes the Release
client to `outputs/Client.exe`, then opens it. Close any client running from that
location before rebuilding. The launcher requires the .NET 10 SDK, works from
any current directory, and waits for a key before its console closes. Publishing
or launch failures return a nonzero exit code.

For both executables in one folder, run `Build-Release.bat` from the repository
root (or double-click it). It requires the .NET 10 SDK and creates
`outputs/Client.exe` and `outputs/Relay.exe` beside the batch file. It works from
any current directory and stops with a nonzero exit code if either publish fails.
Build intermediates go into `artifacts/release`; both generated folders are ignored
by Git. The executables retain the single-file, framework-dependent settings.

To publish the projects separately from this folder:

```powershell
dotnet publish Client -c Release -o publish/client -m:1
dotnet publish Relay -c Release -o publish/relay -m:1
```

Distribute just the respective executable. The client requires the x64 .NET 10
runtime; the relay requires the x64 ASP.NET Core 10 runtime. Neither bundles a
runtime. The client bundles Avalonia's native libraries, which .NET extracts to
its temporary bundle cache on first launch. Debug symbols stay in build output.
When replacing an older folder deployment, remove its obsolete companion files.
The relay can target another OS/architecture with `-r`, for example
`dotnet publish Relay -c Release -r linux-x64 -o publish/relay-linux -m:1`.

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

Three-player diagnostics use the production session workers and require three
distinct disposable game processes in the same screen:

```powershell
dotnet run --project Probe -- --trio ws://localhost:5080 <pid1> <pid2> <pid3>
dotnet run --project Probe -- --trio-transitions ws://localhost:5080 <pid1> <pid2> <pid3>
```

The transition variant starts in Midnight (or retreats from Foyer). These checks
change native enemy health, puzzles, scenery and player positions, and await
disconnect cleanup. They are not intended for an in-progress saved game.

Research: [project wiki](https://github.com/aprofessionalpirate/nsasmo/wiki),
[sprite analysis](https://github.com/aprofessionalpirate/nsasmo/wiki/Sprite-Analysis),
[memory searching](https://github.com/aprofessionalpirate/nsasmo/wiki/Memory-Searching).
Transport: [ASP.NET Core WebSockets](https://learn.microsoft.com/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0).
