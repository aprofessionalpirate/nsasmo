"""Extract short sampled-name taunts from sidplayfp's isolated mono sample channel."""

import argparse
from array import array
from pathlib import Path
import sys
import wave

# Contiguous excerpts from the supplied Graphixmania_2_part_6.sid, not synthesized combinations.
RANGES = [
    ('ImMatt', 3.24, 4.06),
    ('ImTim', 7.08, 7.9),
    ('ImJoe', 10.92, 11.74),
    ('Matt', 3.5, 3.98),
    ('Tim', 7.34, 7.82),
    ('Joe', 11.18, 11.7),
    ('MattTim', 19.52, 20.1),
    ('TimJoe', 19.79, 20.42),
    ('JoeTim', 20.5, 21.0),
    ('TimMatt', 21.01, 21.45),
    ('MattTimJoe', 19.52, 20.42),
    ('JoeTimTimMatt', 20.5, 21.45),
    ('MattMatt', 19.28, 19.79),
    ('TimTim', 20.76, 21.2),
    ('JoeJoe', 20.14, 20.76),
]


def _write_clip(samples, rate, start, end, destination):
    clip = samples[round(start * rate):round(end * rate)]
    mean = sum(clip) / len(clip)
    centered = [sample - mean for sample in clip]
    gain = min(2.5, 28000 / max(abs(sample) for sample in centered))
    fade = round(rate * 0.007)
    result = array("h")
    for index, sample in enumerate(centered):
        envelope = min(1, index / (fade - 1), (len(clip) - 1 - index) / (fade - 1))
        result.append(max(-32768, min(32767, round(sample * gain * envelope))))
    if sys.byteorder != "little":
        result.byteswap()
    with wave.open(str(destination), "wb") as output:
        output.setparams((1, 2, rate, 0, "NONE", "not compressed"))
        output.writeframes(result.tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rendered_wav", type=Path, help="Isolated sample-channel WAV: mono 22050 Hz / 16-bit PCM")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] / "Client/Assets/Taunts")
    args = parser.parse_args()
    with wave.open(str(args.rendered_wav), "rb") as source:
        if (source.getnchannels(), source.getsampwidth(), source.getframerate()) != (1, 2, 22050):
            parser.error("Render mono 22050 Hz / 16-bit PCM before extraction.")
        samples = array("h", source.readframes(source.getnframes()))
    if sys.byteorder != "little":
        samples.byteswap()
    if len(samples) < round(max(end for _, _, end in RANGES) * 22050):
        parser.error("The rendered WAV does not contain all required excerpts.")
    args.output.mkdir(parents=True, exist_ok=True)
    for name, start, end in RANGES:
        _write_clip(samples, 22050, start, end, args.output / (name + ".wav"))
        print(name + ": " + format(end - start, ".3f") + " seconds")


if __name__ == "__main__":
    main()
