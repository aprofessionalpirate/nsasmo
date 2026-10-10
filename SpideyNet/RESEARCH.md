# Verified game layout

Build SHA-256: `0695C840053C920BF198FA203AE64F8A028AA28EA186351AFA0AD4DE9EFD93F0`.
DOSBox 0.74-3, normal CPU core. VGA and CGA share the actor, puzzle and renderer offsets below.

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

VGA CS:`0x34E1` and CGA CS:`0x36CE` read actor +6 as the sprite frame.
Actor +19 is an animation-script pointer, not a frame. Protocol 3 sends actor
+6 in `Pose`; guest records apply that byte while keeping local pointers.
Facing uses three renderer bits: axis swap, horizontal flip and vertical flip.
Value 4 is the ceiling pose. Rejecting it removed hanging players from snapshots.

Native web lines use DS:`0x9658`/`0x965A` for the endpoint and
DS:`0x965C`/`0x965E` for the origin. Actor +17 bit 1 enables drawing.
CS:`0x3944` draws the clipped line in VGA/CGA. These records are not yet shared.

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
Guest actors occupy indexes 5–11. Private inactive descriptors occupy the
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
The CGA renderer uses 16 bytes per 8x8 tile and a 64-byte background row.
DS:`0x98CA`, `0x98D6` and `0x98DA` contain local background/tile far pointers.
Changed cells are repainted in that background; native drawing copies it into
the display buffer. All pointers are validated locally within DOS memory.

# VGA graphics

Video selection stores the menu number minus one at DS:`0x4`: VGA=0, EGA=1,
Tandy=2, CGA=3. The bridge accepts VGA and CGA; EGA/Tandy remain unverified.
VGA uses BIOS mode `0x0D` (320x200, 16 colors), with a VGA palette set through
BIOS `0x1010`. The renderer count instruction remains CS:`0x349A`.

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
