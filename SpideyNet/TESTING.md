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
