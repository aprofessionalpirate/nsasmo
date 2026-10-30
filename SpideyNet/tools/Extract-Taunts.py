"""Extract the five voice instruments from the supplied ProTracker module."""

import argparse
import hashlib
from pathlib import Path
import struct
import wave

SOURCE_SHA256 = "ebfac0862aa489554984187f4caf553ddaf85390a266a2152e2529ca75885c99"
VOICES = {11: "Spider", 12: "Man", 13: "SpiderMan", 14: "SpinWeb", 15: "OhPeter"}
OUTPUT_RATE = 22050
# A module changes pitch at each note; C-3 (period 428) gives one consistent standalone pitch.
SOURCE_RATE = 3546894.6 / 428


def extract(source, output):
    data = source.read_bytes()
    if hashlib.sha256(data).hexdigest() != SOURCE_SHA256 or data[1080:1084] != b"M.K.":
        raise ValueError("Expected the supplied spidey3.mod; its SHA-256 does not match.")

    song_length = data[950]
    pattern_count = max(data[952:952 + song_length]) + 1
    offset = 1084 + pattern_count * 1024
    output.mkdir(parents=True, exist_ok=True)
    for instrument in range(1, 32):
        header = 20 + (instrument - 1) * 30
        size = int.from_bytes(data[header + 22:header + 24], "big") * 2
        raw = data[offset:offset + size]
        if len(raw) != size:
            raise ValueError("Truncated module sample data.")

        if instrument in VOICES:
            # ProTracker stores signed 8-bit PCM; interpolate to common 22.05 kHz / 16-bit WAV.
            signed = [value if value < 128 else value - 256 for value in raw]
            count = round(size * OUTPUT_RATE / SOURCE_RATE)
            pcm = bytearray()
            for frame in range(count):
                position = frame * SOURCE_RATE / OUTPUT_RATE
                left = min(int(position), size - 1)
                right = min(left + 1, size - 1)
                value = (signed[left] + (signed[right] - signed[left]) * (position - left)) * 256
                # Five-millisecond fades avoid clicks at standalone sample boundaries.
                gain = max(0, min(1, frame / 110, (count - 1 - frame) / 110))
                pcm.extend(struct.pack("<h", round(value * gain)))

            target = output / (VOICES[instrument] + ".wav")
            with wave.open(str(target), "wb") as audio:
                audio.setparams((1, 2, OUTPUT_RATE, 0, "NONE", "not compressed"))
                audio.writeframes(pcm)
            print(f"{target.name}: sample {instrument}, {count / OUTPUT_RATE:.3f}s")

        offset += size

    if offset != len(data):
        raise ValueError("Unexpected trailing module data.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Path to the original spidey3.mod")
    parser.add_argument("--output", type=Path,
                        default=Path(__file__).resolve().parents[1] / "Client" / "Assets" / "Taunts")
    args = parser.parse_args()
    extract(args.source, args.output)
