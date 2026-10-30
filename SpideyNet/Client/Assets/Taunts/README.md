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
22050 Hz / 16-bit PCM with short edge fades. Labels follow the instrument names.
Use the client's local Preview button to audition them.

To reproduce the files with Python 3 and its standard library:

```powershell
python SpideyNet/tools/Extract-Taunts.py C:/path/to/spidey3.mod
```

WAVs are embedded in Client.exe; no additional audio files or player installation
is required. Only a validated clip ID crosses the relay. Playback uses a pinned
PCM buffer through the Windows audio driver, which stops the old clip before
releasing its memory. A new clip replaces the current clip, without a cooldown.
