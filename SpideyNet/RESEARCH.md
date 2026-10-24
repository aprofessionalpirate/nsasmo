# Verified game layout

Build SHA-256: `0695C840053C920BF198FA203AE64F8A028AA28EA186351AFA0AD4DE9EFD93F0`.
DOSBox 0.74-3, normal CPU core. VGA is the only supported mode; its verified offsets follow.

The MZ image has data segment `0x0FB3` and code segment `0x2CE9`.
Subtracting them gives the host-memory code offset from DS: `0x1D360`.
Text marker `Mary Jane is DEAD!` ends at DS:`0x92B1`, the actor start.

| Field | DS offset / actor offset |
|---|---|
| Actor count, unsigned word | `0x928B` |
| Player actor | `0x92B1` |
| Actor stride | `0x32` (50 bytes) |
| Input | actor +1 |
| Signed X | actor +2 |
| Y / facing / character | actor +4 / +5 / +6 |
| Descriptor pointer | actor +31 |
| Bounds | actor +33 through +38 |
| Health | actor +39 |
| Attack fields | actor +48 / +49 |

VGA CS:`0x34E1` reads actor +6 as the sprite frame.
Actor +19 is an animation-script pointer, not a frame. Protocol 3 sends actor
+6 in `Pose`; guest records apply that byte while keeping local pointers.
Facing uses three renderer bits: axis swap, horizontal flip and vertical flip.
Value 4 is the ceiling pose. Rejecting it removed hanging players from snapshots.

Native web lines use DS:`0x9658`/`0x965A` for the endpoint and
DS:`0x965C`/`0x965E` for the origin. Actor +17 bit 1 enables drawing.
CS:`0x3944` draws the clipped line in VGA. Protocol 5 shares bounded endpoints;
remote lines are painted into the native playfield backing buffer.

Actor +17 bit 1 also controls animation-script parsing: CS:`0x0918` consumes
an extra two-byte web-origin field when it is set. It is not an independent
render toggle. The bridge must never set or clear it. Redirecting a shot is
permitted only while that native bit and actor +1 bit 7 (active shot) are set;
shot creation, timeout and release remain owned by the native animation.
Forcing the bit after a shot expires misaligns the next animation record.

The actor loop uses BP at actor +1:

```asm
0370 mov bp, 92B2h
0373 mov cx, [928Bh]
0380 mov bx, [bp+1Eh]   ; actor descriptor pointer
0385 test byte [bx+2], 80h
0389 je skip_ai
...
0397 add bp, 32h
```

The earlier 48-byte interpretation mislabeled the previous actor's final
two bytes as the next enemy's header. Zeroing actor +31/+32 breaks a pointer.
Sharing the player descriptor also lets guest updates affect player AI flags.

The draw loop is independent:

```asm
3497 mov bx, 92B2h
349A mov cx, [928Bh]    ; 8B 0E 8B 92
349E cld
349F push cx
34A0 push bx
34A1 push es
34A2 call draw_actor
...
34A8 add bx, 32h
34AC loop 349Fh
34AE ret
```

SpideyNet replaces only the four-byte count instruction with
`B9 0C 00 90` (`mov cx,12; nop`). The ordinary count stays unchanged.
Guest actors occupy indexes 5â€“11. Private inactive descriptors occupy the
unused storage starting at index 12. Unused gap sprites are hidden without
replacing their web-related fields. Code and data writes occur while DOSBox
threads are suspended; normal disconnect restores the original instruction.

This leaves room scripts and native enemy behavior intact. Multiplayer collision
and freeze decisions belong to the relay, since guests are excluded from AI.

# Shared scene layout

DS:`0x88` through `0x147` stores 768 switches in 192 bytes. Each two-bit pair
contains the state in its low bit and a script-dirty flag in its high bit.
CS:`0x1A9A` toggles the state and sets dirty; CS:`0x1AC8` consumes dirty flags.
The bridge preserves existing dirty flags and sets them for changed states.
Trigger records at DS:`0x1C8A` use eight bytes each, up to sixteen entries.
Record +3 bit 6 is the native contact/web latch. CS:`0x1A72` sets it after input;
script effects do not set it. Only the room owner publishes derived switch
changes; followers publish rising input latches. This prevents script feedback.
Record +3 bit 5 marks one-shot triggers: CS:`0x1AB0` skips toggling an already-on
switch. Midnight's helicopter contact (switch 4) uses this behavior.

DS:`0x148` through `0x447` stores 256 three-byte mover descriptors. The first
two bytes are signed Y/X tile displacement. Enemy descriptor flags use bit 7
for active status. CS:`0x09B2` hides a dead enemy and clears that flag.
Native enemy health is actor +39; it is shared separately from player health.

DS:`0x1B31` holds up to eight runtime object pointers. Their records start at
`0x1B41` and end before `0x1C81`. Non-actor records contain base X/Y, a local
descriptor pointer, width/height, then local tile bytes. `0x1C83` counts these
non-actor objects. Midnight's helicopter is mover 0, base X=8, Y=15, size 6x2.

CS:`0x27D4` removes old solid object cells; `0x282B` redraws and advances them.
External displacement corrections reproduce removal and rebuild object cells.
The VGA renderer uses 32 bytes per 8x8 tile across four planes.
DS:`0x98CA`, `0x98D6` and `0x98DA` contain local background/tile far pointers.
Changed cells are repainted in that background; native drawing copies it into
the display buffer. All pointers are validated locally within DOS memory.

# VGA graphics

Video selection stores the menu number minus one at DS:`0x4`. VGA is value 0;
the bridge rejects every other value before accessing graphics.
VGA uses BIOS mode `0x0D` (320x200, 16 colors), with a VGA palette set through
BIOS `0x1010`. The renderer count instruction remains CS:`0x349A`.

The native VGA palette maps index 3 to white (RGB DAC values 56,56,56), while
index 15 is yellow (56,48,0). The palette map is at DS:`0x9779` and RGB values
at DS:`0x978A`. Web pixels therefore use index 3, with the two high backing
planes cleared and two low planes set, replacing any background colour.

CS:`0x32A2` builds the VGA background from 32-byte tiles: eight bytes for each
of four planes. Each plane contains 176 rows of 32 bytes (`0x1600` bytes).
The complete backing buffer is `0x5800` bytes. CS:`0x3242` copies these planes
to video memory with sequencer masks 8, 4, 2, 1. External corrections repaint
the backing buffer using the same layout; the native renderer handles VRAM.

# Follower enemy AI

CS:`0x373` loads DS:`0x928B` into CX for the AI loop. Its calls at `0x38B`
and `0x38E` update input, animation and movement. Followers replace only this
instruction with `MOV CX,1; NOP`, leaving the local player active. Other loops
retain the native actor count. Ownership transfer and disconnect restore it.
Enemy character byte +6 is replicated with position; local graphics pointers
and prior draw coordinates remain local.

The isolated regression failed before this patch: X207 became X217 without
an owner update. Afterward X185 remained X185; restoring native AI moved it
to X193. The actor count stayed unchanged.

# Inactive actors and HUD corruption

CS:`0x0FEB` runs screen wrapping for every native actor, even after its active
descriptor flag is cleared. It reads bounds at actor +33..+38 and adjusts current
and previous positions, leaving those bounds unchanged. A hidden left bound of
-32 therefore adds 256 to X every frame. Bounds at Y=200 also trigger vertical
wrapping. Hiding the sprite frame alone does not make this safe.

The VGA renderer clips X using an eight-bit width at CS:`0x3588`..`0x35BF`.
Large coordinates can wrap that width and write outside the playfield into HUD
video memory. Isolated execution of the saved native routine at X=26354/Y=147
reproduced HUD writes without modifying a running game. Main graphics pointers,
portrait assets and playfield buffers matched the unaffected instance.

Inactive actors now use X=0, all bounds=0, drawn/previous Y=200, zero health and
the hidden frame, while retaining native pointers. This avoids native wrapping
and lets the renderer reject the off-screen sprite. Apply this when replicating
death, not just while drawing guests, so it remains safe after disconnect.
The `--inactive` diagnostic checks raw actor state after all patches are released.

# Native room travel

CS:`0x107C` stores the destination in DS:`0x007F`, then sets DS:`0x1D0E`.
The main loop consumes that request at CS:`0x045B`, saves outgoing enemy state
through CS:`0x1FA4`, and enters the native loader at CS:`0x02C2`.
CS:`0x2043` normally clears DS:`0x1D0E` before physics each frame. A host write
alone can therefore change the room index without loading its tables.

RoomTravel verifies the `A2 0E 1D` instruction at CS:`0x2043`, temporarily
replaces it with three NOPs and submits the destination. The loader clears the
request itself at CS:`0x032D`. Completion requires that acknowledgement, a valid
matching room/title and the bridge's settling interval. The driver restores the
instruction and translates current coordinates, bounds and previous coordinates
together. It preserves native animation pointers and flags. A normal disconnect
waits for an accepted travel request before cancelling the session.

# Player screen previews

A separate window-capture driver uses PrintWindow on the attached DOSBox client
area and rescales to 320 x 200. Its thread temporarily uses per-monitor DPI
awareness so physical capture pixels and client dimensions agree. Handles and
DPI context are restored after every capture. This does not inspect another
window or change foreground focus. RGB565 frames are Deflate-compressed;
receivers bound decompression to exactly 128,000 bytes. Protocol 6 carries
on-demand watch subscriptions and routes fresh images once per subscribed peer.

# Optional guest sprite compositor

The VGA sprite source is the process-local far pointer at DS:`0x98DE`.
A frame occupies 256 bytes: four 63-byte planes (24 x 21 pixels) plus padding.
Facing bit 0 selects the pre-transposed bank 96 frames later. Bits 1 and 2
mirror horizontally and vertically. Horizontal mirroring moves the 24-pixel
origin three units left, matching the native shifted scratch renderer.
All-one colour 15 is transparent, not opaque yellow sprite content.

SpriteRaster reads those assets and paints only visible pixels into the existing
256 x 176 four-plane backing buffer. It remaps red 2 and dark red 6 to a per-guest
suit/shade pair, retaining all other pixels. WebMemory owns one combined transient
patch set for sprites and webs; webs draw last in native white. Scene corrections
restore this layer before repainting moving scenery. Native guest sprites use
the existing hidden frame while the option is enabled. No additional code hook,
asset allocation, sprite pointer changes or global palette changes are needed.

# Shared colour rendering and preview scaling corrections

Shared scene application previously restored the guest overlay, resumed DOSBox,
and repainted it in a later Render call. The native background copier could run
in the gap. Session now submits a GameSceneUpdate with its Render call so restore,
scene application and repaint share one suspended transaction. Nested ThreadPause
instances retain mutex recursion without suspending the same threads repeatedly.

GDI's default BLACKONWHITE stretch mode ANDs removed pixels when shrinking.
Alternating red/cyan pixels therefore became black at 2x, 3x and 4x. The capture
driver now uses COLORONCOLOR and flushes GDI before reading the DIB bits directly.
See [SetStretchBltMode](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setstretchbltmode)
and [DIB synchronization](https://devblogs.microsoft.com/oldnewthing/20100923-00/?p=12773).

# Teleport collision origins

CS:`0x0EDD` rebuilds collision bounds from actor +29/+30 and the eight-facing
origin table at DS:`0xA9AD` (`07070700000700000700070700000007`). An odd X/Y
origin entry adds `21 - width/height`; the inclusive right/bottom adds size minus
one. Translating old bounds while changing facing leaves the box on the wrong
side of the sprite. PlayerPlacement matches that routine, validates the native
table, resets previous coordinates/facing, and preserves animation pointers,
health and native animation flags. Session refreshes the destination position
while loading rather than retaining only the first report.
