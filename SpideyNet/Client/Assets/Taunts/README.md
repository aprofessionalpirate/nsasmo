# Taunt audio

These five voice clips were extracted from the user-supplied `spidey3.mod`
(ProTracker `M.K.`, title `spidey3`). No backing instruments or song patterns are
mixed into the clips. The original module is not bundled.

Source SHA-256: `ebfac0862aa489554984187f4caf553ddaf85390a266a2152e2529ca75885c99`.

| Taunt ID / file | Instrument | Original sample name | Duration |
|---|---:|---|---:|
| Spider | 11 | st-02:sp_spider | 0.618 s |
| Man | 12 | st-02:sp_man | 0.382 s |
| SpiderMan | 13 | st-02:sp_spiderman2 | 1.368 s |
| SpinWeb | 14 | st-02:sp_spinweb | 1.004 s |
| OhPeter | 15 | st-02:sp_ohpeter2 | 1.232 s |

The module plays these instruments at multiple pitches. Standalone clips use the
PAL Amiga clock at period 428 (approximately 8287 Hz), interpolated to mono
22050 Hz / 16-bit PCM with short edge fades. The displayed label for SpinWeb is **Spin a web**. Other MOD labels follow the instrument names.
Use the client's local Preview button to audition them.

To reproduce the files with Python 3 and its standard library:

```powershell
python SpideyNet/tools/Extract-Taunts.py C:/path/to/spidey3.mod
```

WAVs are embedded in Client.exe; no additional audio files or player installation
is required. Only a validated clip ID crosses the relay. Playback uses a pinned
PCM buffer through the Windows audio driver, which stops the old clip before
releasing its memory. A new clip replaces the current clip, without a cooldown.

## Graphixmania sampled names

Fifteen additional clips come from the user-supplied `Graphixmania_2_part_6.sid`
(RSID v2, title `Graphixmania 2 (part 6)`, credited in its header to Tim Kleinert
and M. Hillebrand, `1990 Modern Arts`). Source SHA-256:
`6a56c39878c3ba6347c762946c72ac8d6381b451146537d896119526584a0989`.
The original SID and the decoder are not bundled with the application.

The SID was rendered with sidplayfp 3.2.0 to mono 22050 Hz / 16-bit PCM, with
synthesized voices 1, 2 and 3 muted, leaving the recorded sample channel.
Each taunt is one contiguous excerpt, including the repeated names actually used
in the tune. Name sequences were checked against a local transcript and repeated
sample waveforms. There are no synthesized or arbitrarily concatenated words.
Only DC removal, bounded level normalization and 7 ms edge fades are applied.

| Taunt | Source range | Duration |
| --- | --- | --- |
| I'm Matt | 3.24–4.06 s | 0.820 s |
| I'm Tim | 7.08–7.90 s | 0.820 s |
| I'm Joe | 10.92–11.74 s | 0.820 s |
| Matt | 3.50–3.98 s | 0.480 s |
| Tim | 7.34–7.82 s | 0.480 s |
| Joe | 11.18–11.70 s | 0.520 s |
| Matt Tim | 19.52–20.10 s | 0.580 s |
| Tim Joe | 19.79–20.42 s | 0.630 s |
| Joe Tim | 20.50–21.00 s | 0.500 s |
| Tim Matt | 21.01–21.45 s | 0.440 s |
| Matt Tim Joe | 19.52–20.42 s | 0.900 s |
| Joe Tim Tim Matt | 20.50–21.45 s | 0.950 s |
| Matt Matt | 19.28–19.79 s | 0.510 s |
| Tim Tim | 20.76–21.20 s | 0.440 s |
| Joe Joe | 20.14–20.76 s | 0.620 s |

To reproduce these files, render at least 24 seconds using the official sidplayfp
console player, then run the standard-library-only extractor:

```powershell
.\sidplayfp.exe '-m' '-f22050' '-p16' '-t24' '-u1' '-u2' '-u3' '-wsamples.wav' 'C:\Music\Graphixmania_2_part_6.sid'
python SpideyNet/tools/Extract-Sid-Taunts.py samples.wav
```

There are now 20 embedded taunts. Existing IDs 1–5 remain unchanged; the new IDs
are 6–20. Use protocol 14 clients and relay together so every participant has the
same catalogue. The no-cooldown/replacement playback behaviour is unchanged.
