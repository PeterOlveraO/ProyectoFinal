#!/usr/bin/env python3
"""Reproducible, dependency-free original music and SFX synthesis for the game.

Only Python's standard library and ffmpeg are needed. No samples or melodies are
borrowed. Run from the repository root: python3 Tools/Audio/generate_audio.py
"""

from __future__ import annotations

import math
import random
import struct
import subprocess
import tempfile
import wave
from array import array
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
MUSIC_DIR = ROOT / "Assets/Resources/Audio/Music"
SFX_DIR = ROOT / "Assets/Resources/Audio/Sfx"
SAMPLE_RATE = 44_100
BPM = 120
BEAT = 60.0 / BPM
BAR = 4 * BEAT
MUSIC_BARS = 16
MUSIC_SECONDS = MUSIC_BARS * BAR
SCALE = (0, 2, 4, 5, 7, 9, 11)


def midi(note: float) -> float:
    return 440.0 * (2.0 ** ((note - 69.0) / 12.0))


def scale_note(degree: int, octave: int = 0) -> int:
    octaves, step = divmod(degree, len(SCALE))
    return 60 + SCALE[step] + (octaves + octave) * 12


def blank(seconds: float) -> array:
    return array("f", [0.0]) * int(round(seconds * SAMPLE_RATE))


def poly_blep(phase: float, increment: float) -> float:
    if phase < increment:
        x = phase / increment
        return x + x - x * x - 1.0
    if phase > 1.0 - increment:
        x = (phase - 1.0) / increment
        return x * x + x + x + 1.0
    return 0.0


def envelope(t: float, duration: float, attack: float, release: float) -> float:
    a = min(1.0, t / max(attack, 1e-4))
    r = min(1.0, max(0.0, duration - t) / max(release, 1e-4))
    return min(a, r)


def add_note(buffer: array, start: float, duration: float, note: float, amplitude: float,
             voice: str, seed: int = 0, pan: float = 0.0) -> None:
    first = max(0, int(start * SAMPLE_RATE))
    last = min(len(buffer), int((start + duration) * SAMPLE_RATE))
    if last <= first:
        return
    frequency = midi(note)
    inc = min(0.49, frequency / SAMPLE_RATE)
    phase = (first / SAMPLE_RATE * frequency) % 1.0
    attack = 0.006 if voice == "lead" else 0.012
    release = min(0.08, duration * 0.24)
    for i in range(first, last):
        t = (i - first) / SAMPLE_RATE
        env = envelope(t, duration, attack, release)
        if voice == "lead":
            sample = (1.0 if phase < 0.5 else -1.0)
            sample += poly_blep(phase, inc) - poly_blep((phase + 0.5) % 1.0, inc)
            sample *= 0.72
        elif voice == "bass":
            sample = 1.0 - 4.0 * abs(phase - 0.5)
        else:
            sample = math.sin(phase * math.tau) + 0.18 * math.sin(phase * math.tau * 2.0)
            sample *= 0.82
        buffer[i] += sample * env * amplitude
        phase += inc
        if phase >= 1.0:
            phase -= 1.0


def add_drum(buffer: array, start: float, kind: str, amplitude: float, rng: random.Random) -> None:
    duration = {"kick": 0.15, "snare": 0.12, "hat": 0.055}[kind]
    first = int(start * SAMPLE_RATE)
    last = min(len(buffer), first + int(duration * SAMPLE_RATE))
    phase = 0.0
    for i in range(first, last):
        t = (i - first) / SAMPLE_RATE
        if kind == "kick":
            frequency = 100.0 - 48.0 * min(1.0, t / duration)
            phase += math.tau * frequency / SAMPLE_RATE
            value = math.sin(phase) * math.exp(-t * 25.0)
        elif kind == "snare":
            value = (rng.random() * 2.0 - 1.0) * math.exp(-t * 32.0)
            value += 0.22 * math.sin(math.tau * 185.0 * t) * math.exp(-t * 24.0)
        else:
            value = (rng.random() * 2.0 - 1.0) * math.exp(-t * 75.0)
        buffer[i] += value * amplitude


SONGS = {
    "Menu": {
        "seed": 1701, "mode": "menu", "chords": [(48, (0, 4, 7)), (43, (0, 4, 7)),
            (45, (0, 3, 7)), (41, (0, 4, 7))],
        "a": [[(0, 7, .75), (1.5, 9, .35), (2, 11, .75), (3, 9, .65)],
              [(0, 7, .65), (1, 4, .45), (2, 5, .7), (3, 4, .5)],
              [(0, 2, .65), (1, 4, .45), (2, 7, .8), (3.25, 5, .45)],
              [(0, 4, .55), (1, 2, .45), (2, 0, .9), (3, 2, .45)]],
        "b": [[(0, 11, .45), (.75, 9, .4), (1.5, 7, .65), (2.5, 9, .4), (3, 11, .6)],
              [(0, 12, .7), (1.5, 11, .4), (2, 9, .8), (3.25, 7, .45)],
              [(0, 9, .5), (1, 7, .45), (2, 5, .75), (3, 4, .4)],
              [(0, 7, .6), (1.5, 5, .4), (2, 4, .7), (3.25, 2, .45)]],
    },
    "Journey": {
        "seed": 2237, "mode": "journey", "chords": [(43, (0, 4, 7)), (50, (0, 4, 7)),
            (40, (0, 3, 7)), (48, (0, 4, 7))],
        "a": [[(0, 7, .45), (.5, 9, .4), (1, 11, .8), (2.5, 9, .4), (3, 7, .65)],
              [(0, 5, .45), (.5, 7, .4), (1, 9, .8), (2.5, 7, .4), (3, 5, .65)],
              [(0, 4, .45), (.5, 5, .4), (1, 7, .8), (2.5, 9, .4), (3, 7, .65)],
              [(0, 2, .45), (.5, 4, .4), (1, 5, .8), (2.5, 4, .4), (3, 2, .65)]],
        "b": [[(0, 9, .5), (1, 11, .5), (1.75, 12, .45), (2.5, 11, .4), (3, 9, .65)],
              [(0, 7, .5), (1, 9, .5), (1.75, 11, .45), (2.5, 9, .4), (3, 7, .65)],
              [(0, 5, .5), (1, 7, .5), (1.75, 9, .45), (2.5, 7, .4), (3, 5, .65)],
              [(0, 4, .5), (1, 5, .5), (2, 7, .75), (3, 9, .45)]],
    },
    "Battle": {
        "seed": 3191, "mode": "battle", "chords": [(45, (0, 3, 7)), (41, (0, 4, 7)),
            (48, (0, 4, 7)), (43, (0, 4, 7))],
        "a": [[(0, 7, .35), (.5, 7, .35), (1.5, 9, .35), (2, 7, .35), (2.5, 5, .35), (3.5, 4, .35)],
              [(0, 5, .35), (.5, 5, .35), (1.5, 7, .35), (2, 9, .35), (2.5, 7, .35), (3.5, 5, .35)],
              [(0, 4, .35), (.5, 4, .35), (1.5, 7, .35), (2, 9, .35), (2.5, 7, .35), (3.5, 4, .35)],
              [(0, 2, .35), (.5, 2, .35), (1.5, 4, .35), (2, 5, .35), (2.5, 7, .35), (3.5, 5, .35)]],
        "b": [[(0, 12, .35), (.75, 11, .35), (1.5, 9, .35), (2, 7, .35), (3, 9, .35), (3.5, 11, .35)],
              [(0, 9, .35), (.75, 7, .35), (1.5, 5, .35), (2, 7, .35), (3, 9, .35), (3.5, 7, .35)],
              [(0, 7, .35), (.75, 5, .35), (1.5, 4, .35), (2, 5, .35), (3, 7, .35), (3.5, 9, .35)],
              [(0, 5, .35), (.75, 4, .35), (1.5, 2, .35), (2, 4, .35), (3, 5, .35), (3.5, 7, .35)]],
    },
    "Boss": {
        "seed": 4129, "mode": "boss", "chords": [(45, (0, 3, 7)), (43, (0, 4, 7)),
            (41, (0, 4, 7)), (40, (0, 4, 7, 10))],
        "a": [[(0, 12, .45), (.5, 11, .3), (1, 9, .45), (2, 7, .65), (3, 5, .35), (3.5, 7, .3)],
              [(0, 9, .45), (.5, 11, .3), (1, 12, .65), (2.5, 11, .35), (3, 9, .45)],
              [(0, 7, .45), (.5, 9, .3), (1, 11, .65), (2.5, 9, .35), (3, 7, .45)],
              [(0, 6, .4), (.5, 4, .3), (1, 2, .45), (2, 4, .45), (3, 6, .5)]],
        "b": [[(0, 12, .3), (.5, 14, .3), (1, 12, .35), (1.5, 11, .3), (2, 9, .45), (3, 7, .35), (3.5, 9, .3)],
              [(0, 11, .3), (.5, 12, .3), (1, 14, .35), (1.5, 12, .3), (2, 11, .45), (3, 9, .35), (3.5, 7, .3)],
              [(0, 9, .3), (.5, 11, .3), (1, 12, .35), (1.5, 11, .3), (2, 9, .45), (3, 7, .35), (3.5, 5, .3)],
              [(0, 7, .3), (.5, 6, .3), (1, 4, .35), (1.5, 2, .3), (2, 4, .45), (3, 6, .35), (3.5, 7, .3)]],
    },
}


def render_music(name: str, spec: dict) -> array:
    rng = random.Random(spec["seed"])
    music = blank(MUSIC_SECONDS)
    mode = spec["mode"]
    for bar in range(MUSIC_BARS):
        start = bar * BAR
        chord_root, chord_intervals = spec["chords"][bar % 4]
        phrase = "a" if bar % 16 < 4 or 8 <= bar % 16 < 12 else "b"
        motif_bar = bar % 4
        variation = 1 if 8 <= bar % 16 < 12 else 0
        for beat in range(4):
            # Warm rounded bass pulse; battle/boss add an offbeat pickup.
            root_note = chord_root - 12
            add_note(music, start + beat * BEAT, .37, root_note if beat % 2 == 0 else root_note + 7,
                     .14 if mode in ("menu", "journey") else .19, "bass")
            if mode in ("battle", "boss"):
                add_note(music, start + (beat + .5) * BEAT, .16, root_note + 7, .085, "bass")

        # Quiet broken-chord sparkle: eighth notes, with a slightly different order per bar.
        arp_order = (0, 1, 2, 1, 2, 1, 0, 1) if bar % 2 == 0 else (0, 2, 1, 2, 1, 2, 0, 1)
        for eighth, chord_index in enumerate(arp_order):
            interval = chord_intervals[chord_index % len(chord_intervals)]
            add_note(music, start + eighth * BEAT * .5, .19, chord_root + interval + 12,
                     .035 if mode in ("menu", "journey") else .026, "arp")

        if mode in ("battle", "boss"):
            add_drum(music, start, "kick", .12 if mode == "battle" else .14, rng)
            add_drum(music, start + 2 * BEAT, "snare", .055 if mode == "battle" else .07, rng)
            for eighth in range(8):
                if eighth % 2 or (mode == "boss" and eighth in (0, 4)):
                    add_drum(music, start + eighth * BEAT * .5, "hat", .018, rng)
        elif mode == "journey":
            add_drum(music, start, "kick", .035, rng)
            if bar % 2 == 1:
                add_drum(music, start + 2 * BEAT, "hat", .012, rng)

        events = spec[phrase][motif_bar]
        for beat, degree, duration in events:
            if variation and degree > 0 and bar % 2:
                degree = max(0, degree - 1)
            note = scale_note(degree, 1 if mode in ("battle", "boss") else 0)
            if mode == "boss" and bar % 4 == 3 and degree in (4, 6):
                note += 1  # a single leading-tone sting against the E7 cadence
            add_note(music, start + beat * BEAT, duration * BEAT, note,
                     .105 if mode in ("menu", "journey") else .115, "lead")

    # Gentle loop-edge taper (12 ms) prevents codec and playback clicks.
    edge = int(.012 * SAMPLE_RATE)
    for i in range(edge):
        gain = i / edge
        music[i] *= gain
        music[-1 - i] *= gain
    return music


def write_wav(path: Path, samples: array, peak_db: float) -> None:
    peak = max((abs(value) for value in samples), default=0.0)
    target = 10.0 ** (peak_db / 20.0)
    gain = min(1.0, target / peak) if peak else 1.0
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(SAMPLE_RATE)
        pcm = array("h")
        for value in samples:
            pcm.append(max(-32767, min(32767, int(value * gain * 32767))))
        if struct.pack("=I", 1) != struct.pack("<I", 1):
            pcm.byteswap()
        output.writeframes(pcm.tobytes())


def tone(t: float, frequency: float, waveform: str = "sine") -> float:
    phase = (t * frequency) % 1.0
    if waveform == "square":
        return 1.0 if phase < .5 else -1.0
    if waveform == "triangle":
        return 1.0 - 4.0 * abs(phase - .5)
    return math.sin(math.tau * phase)


def render_sfx(name: str) -> tuple[array, float]:
    specs = {
        "Ui": (.10, -16, "ui"), "Confirm": (.28, -14, "confirm"),
        "Purchase": (.52, -14, "purchase"), "Hurt": (.34, -12, "hurt"),
        "Clear": (1.48, -12, "clear"), "Defeat": (1.65, -12, "defeat"),
    }
    for element, kind in (("Fire", "fire"), ("Electric", "electric"), ("Water", "water"),
                          ("Grass", "grass"), ("Ice", "ice"), ("Arcane", "arcane"),
                          ("Stone", "stone"), ("Air", "air"), ("Normal", "normal")):
        specs["Shot" + element] = (.34, -11, "shot_" + kind)
        specs["Impact" + element] = (.27, -11, "impact_" + kind)
    duration, peak_db, kind = specs[name]
    count = int(duration * SAMPLE_RATE)
    rng = random.Random(8128 + sum(ord(c) for c in name))
    data = array("f", [0.0]) * count

    def add_tone(start: float, length: float, f0: float, f1: float, amp: float,
                 wave_name: str = "sine", harmonic: float = 0.0) -> None:
        first = int(start * SAMPLE_RATE)
        last = min(count, first + int(length * SAMPLE_RATE))
        for i in range(first, last):
            t = (i - first) / SAMPLE_RATE
            ratio = t / max(length, 1e-4)
            freq = f0 * ((f1 / f0) ** ratio)
            env = envelope(t, length, .004, min(.12, length * .45))
            value = tone(t, freq, wave_name)
            if harmonic:
                value = .72 * value + harmonic * tone(t, freq * 2.0, "sine")
            data[i] += amp * env * value

    def add_noise(start: float, length: float, amp: float, decay: float, tint: float = 0.0) -> None:
        first = int(start * SAMPLE_RATE)
        last = min(count, first + int(length * SAMPLE_RATE))
        previous = 0.0
        for i in range(first, last):
            t = (i - first) / SAMPLE_RATE
            white = rng.random() * 2.0 - 1.0
            previous = white * (1.0 - tint) + previous * tint
            data[i] += amp * previous * math.exp(-t * decay)

    if kind == "ui":
        add_tone(0, duration, midi(79), midi(83), .34, "sine")
    elif kind == "confirm":
        add_tone(0, .13, midi(72), midi(76), .25, "triangle")
        add_tone(.11, .17, midi(76), midi(79), .27, "triangle")
    elif kind == "purchase":
        for t, note in ((0, 76), (.12, 81), (.26, 84), (.38, 88)):
            add_tone(t, .19, midi(note), midi(note + 2), .22, "sine", .2)
    elif kind == "hurt":
        add_noise(0, .18, .22, 28, .35)
        add_tone(0, .27, 58, 39, .22, "triangle")
    elif kind == "clear":
        for t, note, length in ((0, 72, .28), (.25, 76, .28), (.5, 79, .32),
                                (.83, 84, .55), (1.0, 79, .38)):
            add_tone(t, length, midi(note), midi(note), .20, "sine", .16)
    elif kind == "defeat":
        for t, note, length in ((0, 71, .35), (.32, 67, .4), (.70, 64, .45), (1.1, 60, .52)):
            add_tone(t, length, midi(note), midi(note - 1), .20, "triangle")
        add_noise(.02, .34, .055, 12, .65)
    elif kind.startswith("shot_"):
        element = kind[5:]
        profiles = {
            "fire": (540, 180, "square", .14, .16), "electric": (1800, 420, "sine", .12, .16),
            "water": (700, 260, "sine", .10, .25), "grass": (980, 480, "triangle", .10, .32),
            "ice": (1500, 900, "sine", .12, .12), "arcane": (660, 1320, "sine", .13, .12),
            "stone": (220, 100, "triangle", .13, .22), "air": (1200, 550, "sine", .08, .5),
            "normal": (440, 260, "square", .12, .18),
        }
        f0, f1, wave_name, amp, noise = profiles[element]
        add_tone(0, duration, f0, f1, amp, wave_name, .12)
        add_noise(0, duration * .65, noise, 8 if element in ("fire", "stone") else 13,
                  .72 if element in ("air", "water") else .15)
        if element in ("electric", "arcane", "ice"):
            add_tone(.05, .18, f0 * 1.5, f1 * 1.8, .07, "sine")
    elif kind.startswith("impact_"):
        element = kind[7:]
        base = {"fire": 120, "electric": 900, "water": 500, "grass": 360, "ice": 1100,
                "arcane": 740, "stone": 95, "air": 650, "normal": 180}[element]
        add_tone(0, duration * .8, base, base * (.55 if element != "electric" else 1.7),
                 .19, "triangle" if element in ("stone", "normal", "grass") else "sine")
        add_noise(0, duration * .55, .13 if element in ("stone", "fire", "water") else .07,
                  24 if element != "water" else 15, .4 if element in ("water", "air") else .08)
        if element == "ice":
            add_tone(.035, .14, 1450, 880, .075, "sine")
        elif element == "electric":
            add_tone(.02, .16, 1200, 500, .08, "square")
    return data, peak_db


def encode_music() -> None:
    MUSIC_DIR.mkdir(parents=True, exist_ok=True)
    SFX_DIR.mkdir(parents=True, exist_ok=True)
    ffmpeg = subprocess.run(["ffmpeg", "-version"], stdout=subprocess.DEVNULL,
                            stderr=subprocess.DEVNULL, check=False).returncode == 0
    for name, spec in SONGS.items():
        print(f"Synthesizing {name}: {MUSIC_SECONDS:.1f}s, {BPM} BPM")
        samples = render_music(name, spec)
        with tempfile.TemporaryDirectory(prefix="retro-audio-") as temporary:
            source = Path(temporary) / (name + ".wav")
            write_wav(source, samples, -3.2)
            destination = MUSIC_DIR / (name + ".ogg")
            if ffmpeg:
                subprocess.run([
                    "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(source),
                    "-af", "loudnorm=I=-19:TP=-3:LRA=7:linear=false",
                    "-c:a", "libvorbis", "-q:a", "5", "-ar", str(SAMPLE_RATE), str(destination)
                ], check=True)
            else:
                destination = MUSIC_DIR / (name + ".wav")
                destination.write_bytes(source.read_bytes())
    for name in ("Ui", "Confirm", "Purchase", "Hurt", "Clear", "Defeat",
                 *(f"Shot{e}" for e in ("Fire", "Electric", "Water", "Grass", "Ice", "Arcane", "Stone", "Air", "Normal")),
                 *(f"Impact{e}" for e in ("Fire", "Electric", "Water", "Grass", "Ice", "Arcane", "Stone", "Air", "Normal"))):
        samples, peak_db = render_sfx(name)
        write_wav(SFX_DIR / (name + ".wav"), samples, peak_db)
    print(f"Wrote 4 music loops and 24 SFX files beneath {ROOT}")


if __name__ == "__main__":
    encode_music()
