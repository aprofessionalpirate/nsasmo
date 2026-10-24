# Relay player logging checks - 23 October 2026

Accepted WebSocket connections now log at Information level with name, player ID,
socket IP/port, anonymous room ID, player count and game modes. Disconnect logs
include the same identity, remaining count, duration, last game room/coordinates
and an ending reason. Room keys and query strings are excluded.

A live two-client test verified exactly one connect/disconnect pair per accepted
player, stable IDs, shared anonymous room correlation, native frame details,
normal closure and malformed JSON (`JsonException`). A secret embedded in both
the room key and malformed payload did not appear in relay output. The full
existing domain and real-socket suite passed against the updated relay, including
three-player interactions and shared-state checks; snapshot rate was 30.4/sec.

The updated framework-dependent single-file relay was published to `outputs`.

---

# Three-player feature checks - 19 October 2026

Three production session workers, three distinct disposable DOSBox 0.74-3 games,
VGA, normal CPU core at 3000 cycles, and an isolated localhost relay were used.
The native room-start fixtures are described in the earlier migration section.

| Check | Result |
|---|---|
| Midnight, Lift, Mummy's Revenge, Cardboard City | Three rosters, Observe storage protection, shared enemy damage, reversible puzzle changes, moving scenery and collision agreement passed |
| Three actors on screen | TaskKit saw the local player plus two guests in each Lift game |
| Native contact puzzle | A follower landed on Midnight's helicopter; launch and stop reached all three games |
| Lift rider | A follower became platform driver, moved it, was carried with it, and all three collision grids agreed |
| Interactions | Nearest target froze and expired; distant third peer stayed unaffected; players crossed without collision corrections |
| Physical V input | TaskKit key press froze a remote player through the production session |
| Enemy death | Native enemy became inactive in all three games before transitions |
| Transitions | Repeated Midnight -> Foyer -> Midnight trips by two peers while the third stayed; guest visibility was checked as 0/1/2 according to room membership |
| Ownership/rejoin | Actual room owner disconnected; surviving peers elected a new owner; rejoin restored both guests |
| Cleanup | Same-room runs restored exact renderer, reserved bytes and native actor count; transition runs restored native renderers |
| Three-player web sockets | Nearest target, stable attachment despite a closer third peer, retarget after room change, release and freeze isolation passed |
| Driver stress | Nested memory transactions, competing host threads, and 3000 pause/resume cycles passed with zero failures |
| Final verification | Release solution: zero warnings/errors; nine Avalonia/MVVM tests and all domain/real-socket checks passed; 30.3 relay snapshots/sec |

Experimental player interactions are now the UI default; choosing Observe still
disables interactions and shared-world writes.

Two race guards were added. Applying a shared puzzle snapshot now preserves
native switch bits that changed after the scene was sampled, allowing the next
capture to publish them. Memory pause transactions now use a named recursive
mutex per DOSBox process: suspending emulator threads alone did not stop two
host-side bridge instances from interleaving writes. Domain checks cover newer
On/Off input and unrelated remote changes; the driver regression verifies nested
and concurrent callers before the existing thread-exit stress test.

The native-contact fixture was aligned with the standing player's collision
bounds. Visibility checks count reserved actors only while the extended renderer
is active; stale unused bytes do not mean visible guests after native restoration.

These are controlled integration checks, not complete level playthroughs or
separate-Internet human play. Short TaskKit Space presses were not observed as
native shots at this CPU speed, so sustained physical Space input remains
unverified. Web geometry/raster, attachment and release retain deterministic and
real-socket coverage; physical V was confirmed.

Repeat with `Probe --trio <relay-uri> <pid1> <pid2> <pid3>` or, for Midnight/Foyer,
`Probe --trio-transitions <relay-uri> <pid1> <pid2> <pid3>`. These commands change
game state and must be used against disposable games. `--trio-preview` provides
a 75-second keyboard observation window. Session cleanup always runs on exit.

---

# Avalonia migration checks - 19 October 2026

Single-file packaging verified on 19 October: clean publishes contain only
`Client.exe` (28,570,342 bytes) and `Relay.exe` (271,793 bytes), respectively.
Both evaluate `PublishSingleFile=true`, `SelfContained=false`, `RuntimeIdentifier=win-x64`.
TaskKit verified the standalone published client opens and discovers DOSBox.
The standalone relay passed `/health` and the full domain/real-socket regression
suite on isolated port 5091 (30.7 snapshots/sec). Its test process was stopped.
The original relay and game processes were left running.

Windows, .NET SDK 10.0.402, Avalonia 12.1.4, DOSBox 0.74-3,
normal CPU core at 3000 cycles, VGA, localhost relay.

| Check | Result |
|---|---|
| Release build and publish | Entire solution built with zero warnings/errors; Client and Probe published; published client launched and visually checked |
| Domain and relay regressions | All existing checks passed, including web pull/release, invalid input, ownership, shared puzzles, room reentry and death; 30.1 relay snapshots/sec |
| Avalonia compiled XAML and MVVM | Eight headless tests passed: input/command binding, lifecycle, clipboard command, configuration locking, validation, stopped-session recovery and stable roster rows |
| Actual desktop client | TaskKit selected a disposable DOSBox, connected, displayed a real peer, removed its departing roster row, disconnected and closed successfully |
| High DPI | Window fits the 1920x1080 desktop at 150% scaling; scrollable content and complete labels |
| Lift (3) | Observe without actor/renderer writes; two guest renderers; two native enemies/two scenery objects; shared damage; collision flags agree; exact reserved bytes, renderer and native counts restored |
| Mummy's Revenge (17) | Same checks; one enemy/one scenery object; shared damage; exact native storage restoration |

Mummy's Revenge exposed a discovery bug: the old room-header byte is 8 in this
room, whereas the current room is 17. The bridge now reads the native current-room
variable at DS:007F for discovery, updates, transition handling and web cleanup.
The header marker and exact room title remain independent validation checks.

The Mummy fire pit animates tile artwork independently in each game. Comparing
all tile bytes incorrectly reported a collision mismatch. Diagnostics now retain
the exact tile hash and also expose a collision-flags hash (upper two bits), which
the generic room probe compares. This does not overwrite native animation.

UI fixes include clearing the location/owner on disconnect, assigning the entire
owner/location string once to avoid blinking, unlocking reconnect after worker
failure, and awaiting restoration before the window closes. The shutdown test
holds cleanup pending and verifies the window stays open until it completes.

These extra rooms used copies under ignored `.research/level-fixtures`. Only the
disposable executable's new-game room immediate (CS:04BF) and menu entry jump
(CS:015B -> CS:025A) were patched to enter a native initialized room without a
full playthrough. VGA and English were selected through TaskKit. Original game
assets and the user's existing game/client processes were left untouched.
These are room integration checks, not complete puzzle/playthrough coverage.

Repeat UI tests using `dotnet test Client.Tests -m:1` and native room checks using
`Probe --level-pair ws://localhost:5080 <first-pid> <second-pid>` against disposable
games. The latter applies enemy damage and awaits both session disconnects.

---

# Feature test run - 18 October 2026

Windows, .NET SDK 10.0.402, DOSBox 0.74-3, normal CPU core, VGA.
Two local DOSBox instances and one localhost relay were used. The game executable
matches the SHA-256 recorded in RESEARCH.md. TaskKit supplied visual verification.

## Results

| Feature | Verification | Result |
|---|---|---|
| Build | Entire solution, warnings treated as errors | Passed; zero warnings/errors |
| Domain rules | Existing Core/shared-state/video checks | Passed |
| Relay identity and isolation | Real sockets, distinct identities and separate room keys | Passed |
| Relay limits | Eight-player capacity; malformed, oversized, binary and invalid-frame rejection | Passed |
| Relay cadence and framing | Delivered snapshots and fragmented WebSocket messages | Passed; 30.4 snapshots/sec after network-rate change |
| VGA guest rendering | Live second Spider-Man visible in DOSBox | Passed |
| Moving scenery | Helicopter displacement; collision grid and VGA background changes | Passed |
| Restoration | Compare renderer, reserved actor storage, native count, collision grid and background before/after | Passed |
| Follower enemy AI | Enemy remains still in replica mode, moves again after restoration | Passed |
| Shared puzzles | Reversible switch On/Off and native follower contact in two games | Passed |
| Shared enemies | Follower damage, enemy death and room transition | Passed |
| Ownership | Socket ownership/rider checks; two-game room handoff; desktop client departure | Passed |
| Observe mode | Production session rosters populate without modifying renderer/reserved storage | Passed |
| Native PvP corrections | Production sessions enforce freeze/expiry and allow player crossing in actual game memory | Passed |
| Desktop connection | Two UI clients attach to distinct DOSBox instances and display rosters | Passed |
| Fire web button | Physical button click; separate relay observer sees remote freeze | Passed |
| Reconnect | Stop/restart relay during two production sessions; verify outage cleanup and guest rendering after rejoin | Passed |
| Repeated/concurrent disconnect | Two shutdown requests for one active session; compare restored renderer/storage | Passed after fix |
| Refresh DOSBox | Rebuilt UI disables refresh while connected | Passed after fix |
| Probe failure handling | Invalid process ID, duplicate process IDs, input timeout and successful live VGA read | Passed; errors exit cleanly rather than opening a Windows crash dialog |
| V shortcut | Replaced F8; TaskKit currently refuses input with "Foreground changed" | Physical input not verified |
| Room reentry regression | Five production-session Midnight -> Foyer -> Midnight trips; one peer stays put | Passed; sessions and guest renderers recovered each time |
| Enemy smoothing | Deterministic follower interpolation and immediate death checks; owner keeps live coordinates | Rule checks passed; human movement feel still needed |
| Remote Space webs | Protocol round trip, VGA clipping, live VGA line rendering and exact restoration | Passed with synthetic geometry; physical Space input still needed |
| Web attachment | Real socket hit/pull/release and room separation; native coordinate round trip and cleanup | Passed; wall interaction and human play feel unverified |
| Enemy owner label | Assign the complete label once per update | Fixed; final desktop visual check limited by TaskKit input refusal |

## Fixes

Concurrent calls to `Session.DisconnectAsync` previously both awaited the worker,
then attempted to dispose a shared cancellation source. The first continuation
cleared that field, so the second threw `NullReferenceException`. Disconnect
callers now share one shutdown task; reconnect starts a fresh lifecycle. The live
`--session` probe reproduced the exception before the fix and passes afterward.

The process refresh button stayed enabled during an attachment even though the
process selector was disabled. Refreshing can reset its displayed selection
without changing the running session's process. Refresh is now disabled while
connected, alongside the process selector.

Probe exceptions previously escaped the entry point, including timeouts waiting
for manual input, producing Windows Application Error dialogs. The entry point
now catches failures after worker cleanup and writes diagnostics to stderr.
Exit codes are 0 for success, 1 for failure, and 2 for timeout/cancellation.
Read failures during the default process scan also set a failing exit status.

## Repeating the desktop checks

Use the commands in README.md. `--session` links the production Session and
SessionView sources into the Windows diagnostic executable; it does not expose
private members or duplicate the worker implementation. Use distinct games, stand
on the ground with the first player facing right, and leave input idle during
the automatic tests. The session fixture allows two coordinate units of native position snapping. The
contact fixture centers the player over its tile; its old edge position could
miss when the game snapped the player toward a wall.

The Foyer enemy damages an idle player. The session fixture retreats from Foyer
to Midnight before its checks. The shared transition test visits Foyer briefly;
return to Midnight before leaving games running unattended.

For a physical V check, connect two clients to the same room with Experimental
rendering, interactions and shared scenes. Face the first player toward the other
within 64 coordinate units. Run `--watch-web`, focus that player's DOSBox, then
press V. The observer requires a relay-visible freeze and its subsequent expiry.
The Fire web button was observed successfully using this path; the V input path
remains a separate manual check. Temporary keyboard tracing was removed.

## Coverage limits

Live scene coverage is Midnight and Foyer. All 63 rooms, simultaneous human
platform riders, prolonged play, physical-keyboard V/Space, and separate Internet
connections remain unverified. This run used VGA throughout.
Health/score synchronization and scripted respawns remain unsupported. Remote web
lines use the graphics backing buffer rather than the single local-web draw
routine. Long sessions with simultaneous native tile redraws need playtesting.
For Space, hold a shot across the other player; verify the line appears on the
peer and the shooter moves toward the target. Releasing Space must detach.

The yellow portrait corruption was traced to inactive actor bounds. The original
hide helper put bounds at X=-32/Y=200. CS:0x0FEB still processes inactive actors
and repeatedly wraps positions without updating those bounds. The VGA renderer's
width clipping can overflow for large X, writing into the HUD. An isolated replay
of the actual draw routine at X=26354/Y=147 wrote 1344 bytes in HUD columns.

Hidden actors now retain in-room bounds at zero and use only drawn Y=200 for
clipping. Replicated deaths apply this geometry atomically. Both running games
passed `--inactive`: ten samples over five seconds after releasing all patches,
with X=0/Y=200 unchanged. A one-off recovery restored the damaged portrait from
validated native assets on both video pages and requested a native repaint;
TaskKit confirmed the repaired display. Actual health and game progress were kept.
Prolonged play remains unverified.

## Follow-up fixes

Room reentry previously tried to apply cached moving-object tables during native
loading. The driver now waits for a matching title, allows a short settling period,
and retries incompatible scene snapshots. Owner updates preserve live enemy and
platform positions; followers interpolate enemy motion between 30 Hz snapshots
at the 60 Hz local update rate. Player collision corrections were removed.

The ownership label previously received two different text assignments every UI
tick. It now receives its complete text once. Protocol 5 includes bounded native
web geometry and held input. The relay optionally attaches hit webs to players
and applies a bounded pull to the shooter; room departure and release detach.

## Thread-lifetime failure during web pulling

The reported client stopped with Windows error 87 ("The parameter is incorrect").
The old pause driver enumerated DOSBox threads, then opened and suspended each
one. A thread could exit between those steps, failing the whole transaction.
It also retrieved the native error after disposing a handle, risking lost error
context. The old client did not save exception stacks, so the exact originating
call for the reported incident cannot be recovered.

An isolated process that rapidly creates and exits threads reproduced the pause
failure: the old driver failed 206 of 300 cycles, with SuspendThread errors.
The corrected driver holds thread handles, verifies process ownership, and skips
only threads confirmed to have exited. Failed suspension allows up to 10 ms for
kernel teardown to complete; genuine access/suspension failures still abort before
memory writes. Already suspended threads are resumed during cleanup.

The corrected driver passed 3000 pause/resume cycles with zero failures. A live
Midnight game passed 30 pull/render/release cycles with renderer, reserved guest
storage and native web state restored. This uses synthetic authorized pull frames,
not a physical Space press. Client failures now log full exceptions and native
operation names for any recurrence.

## Remote web animation regression (23 October 2026)

The supplied nine-second Lift recording shows a leftward web, abrupt player
position changes and a brief corrupted sprite near the upper-left edge.
The old tether driver forcibly set actor +17 bit 1 on every correction and
cleared it on release. That bit also changes native animation-script parsing;
it cannot safely be treated as a standalone web-render switch.

A replay of the actual DOS instructions from a saved Lift memory snapshot
reproduced the failure when a held shot expired. At frame 106 the old writes
changed the animation pointer from the normal sequence to a misaligned record,
producing pose 36 (outside the player's range) and X=-2 from relay X=104.
Without flag writes, the same transition produced valid poses 24, 8 and 9,
then resumed the shot normally. An 18,000-frame replay passed repeated shot
timeouts without invalid player poses or large position jumps. The driver now redirects coordinates only for
an already active native shot and leaves animation flags and release to the game.

The Windows `--web` probe now checks that a relay tether cannot activate an idle
native animation. The `--web-stress` probe retains its repeated idle correction,
guest rendering and restoration checks. Both require an idle Midnight fixture.
Additional isolated driver checks cover 28 input/animation-flag combinations and
verify that only the eight coordinate bytes of an active shot can change.
The complete domain and two-/three-player socket suites also passed.

This is a native-code replay and memory-driver regression, not yet confirmation
of the corrected behavior in a new recording from the two human players.

## Player room tools and spectator previews (24 October 2026)

Thirteen Avalonia headless tests pass, including named room rows, retained
selection, teleport/spy command routing, Observe-mode teleport restrictions,
preview bindings, malformed image rejection, disconnect cleanup and removing
a watched player. The complete domain and socket suites pass at approximately
30 movement snapshots/sec. New socket checks cover large image frames, capture
subscriptions, different game rooms, stale image expiry, changing targets,
stopping, room-key isolation and teleporting out of an existing freeze.

The production GameBridge travel/capture probe passed Lift, Mummy's Revenge,
Cardboard City, Black Thunder, Midnight and Foyer with native room loading,
requested coordinates and nonblank game images. The production Session probe
passed cross-room and same-room teleports, successive real DOSBox images through
the relay, stopping previews and disconnecting while a room load was pending.

TaskKit desktop verification used two separate VGA DOSBox games. The Avalonia
client displayed a remote player as Lift rather than 3. Selecting its row enabled
the new buttons. Spy displayed that player's full screen, HUD and movement at
four images/sec while the viewing game remained in Midnight. Teleport to player
moved the viewing game into Lift and rendered both players there. Stop spying
removed the image; Disconnect restored the UI and native renderer. No Foyer
fixture was left running unattended.

The first capture replay exposed DPI cropping, now fixed by matching physical
capture coordinates. Captures in the verified rooms compressed to about 8-20 KB.
The first travel attempt exposed the per-frame clearing of the native load flag;
the temporary verified instruction patch and loader acknowledgement fix it.
Sprite recolouring was added in the follow-up below without modifying the shared
VGA palette or sprite assets.

Additional native diagnostic commands (run against isolated test games):

```powershell
dotnet run --project Probe -- --travel 1234 "$env:TEMP\spideynet-preview.rgb565"
dotnet run --project Probe -- --player-tools ws://localhost:5080 1234
```

## Optional guest sprite colours (24 October 2026)

The full Release build passes with zero warnings. All fourteen Avalonia headless
checks pass, including the new checkbox binding, default-off setting, connection
option and disabled configuration while attached or observing. Raster checks
cover all seven suit/shade pairs, eight facing values, transparent and opaque
pixels, unchanged eyes/clothing/outlines, and clipping at the screen edges.

The live VGA Midnight probe displayed seven differently coloured animated guests
alongside the unchanged local player and native enemy, with a white remote web.
A suspended transaction verified exact background restoration when switching to
native rendering, then exact reserved bytes, renderer instruction and actor count
on disconnect. Animation and both flip axes were exercised during the display.
These are synthetic guests; human multiplayer overlap/latency testing remains useful.

An eight-player socket session (one native game plus seven simulated peers) also
passed coloured rendering after native travel through Lift, Mummy's Revenge,
Cardboard City and Midnight, followed by disconnect and native renderer/count
restoration. The complete relay suite passed at 30.1 snapshots/sec. A deliberate
invalid relay URI confirmed Tests.exe returns exit code 1 with stderr output.

An initial invalid-coordinate test fixture threw an uncaught exception and opened
a Tests.exe application-error dialog. The fixture now uses protocol-valid bounds.
The test executable also catches failures, reports the exception to stderr and
returns exit code 1 instead of invoking Windows crash reporting.

## LAN relay launcher (24 October 2026)

Run-Server-LAN.bat published and hosted the relay from a different working
folder. Automatic discovery selected the private Ethernet address instead of
the Hyper-V adapter. A health request returned protocol 6 and a real WebSocket
connection succeeded on the LAN listener. Explicit address/port selection also
passed with built-in Windows PowerShell. Invalid loopback addresses and port 0
returned nonzero exit codes before publishing. Ctrl+C stopped both test relays.
Cross-device firewall reachability still depends on the host's Private-profile
inbound rule; the launcher does not change firewall settings.
