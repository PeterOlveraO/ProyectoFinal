#!/usr/bin/env python3
"""Import the requested original Pokémon audio; requires FFmpeg and libgme.

Sources and hashes are recorded in sources-pokemon.json. The soundtrack is rendered
from the original Game Boy music data, not reconstructed from a melody transcription.
"""
import argparse
import ctypes
import ctypes.util
import hashlib
import json
import math
import subprocess
import tempfile
import urllib.request
import wave
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / "Assets/Resources/Audio"
ARCHIVE = "https://fi.zophar.net/soundfiles/gameboy-gbs/pokemon-red/Pokemon%20Red%20%28EMU%29.zophar.zip"
CRIES = {
    "jolteon": 135, "charizard": 6, "flareon": 136, "vaporeon": 134,
    "blastoise": 9, "gyarados": 130, "venusaur": 3, "lapras": 131,
    "mew": 151, "gengar": 94, "aerodactyl": 142, "garchomp": 445,
    "dragonite": 149, "lucario": 448, "snorlax": 143, "ditto": 132,
    "kangaskhan": 115, "tauros": 128,
    "tyranitar": 248, "salamence": 373, "metagross": 376, "mewtwo": 150,
    "lugia": 249, "ho-oh": 250, "rayquaza": 384, "celebi": 251,
    "jirachi": 385, "darkrai": 491,
}
# Original engine: note units * note_speed(12) * tempo(152) / 256 frames.
# After a 64-unit introduction the channels loop at 320,16,512,16 units.
# Their common cycle is 2560 units; retain it instead of cutting a musical phrase.
FRAME = 70224 / 4194304
INTRO = (64 * 12 * 152 / 256 + 1) * FRAME
LOOP = math.lcm(320, 16, 512, 16) * 12 * 152 / 256 * FRAME


def run(*args):
    return subprocess.run(args, check=True, capture_output=True, text=True)


def fetch(url, target, manifest):
    with urllib.request.urlopen(url, timeout=60) as response:
        data = response.read()
    target.write_bytes(data)
    manifest.append({"url": url, "sha256": hashlib.sha256(data).hexdigest()})


def render_gbs(path, output):
    library = ctypes.util.find_library("gme")
    if not library:
        raise RuntimeError("libgme is required to render the original soundtrack")
    gme = ctypes.CDLL(library)
    pointer = ctypes.c_void_p
    gme.gme_open_file.argtypes = [ctypes.c_char_p, ctypes.POINTER(pointer), ctypes.c_int]
    gme.gme_open_file.restype = ctypes.c_char_p
    gme.gme_start_track.argtypes = [pointer, ctypes.c_int]
    gme.gme_start_track.restype = ctypes.c_char_p
    gme.gme_play.argtypes = [pointer, ctypes.c_int, ctypes.POINTER(ctypes.c_short)]
    gme.gme_play.restype = ctypes.c_char_p
    gme.gme_ignore_silence.argtypes = [pointer, ctypes.c_int]
    gme.gme_delete.argtypes = [pointer]

    def checked(error):
        if error:
            raise RuntimeError(error.decode())

    emulator = pointer()
    checked(gme.gme_open_file(str(path).encode(), ctypes.byref(emulator), 44100))
    try:
        gme.gme_ignore_silence(emulator, 1)
        checked(gme.gme_start_track(emulator, 11))  # Lavender Town, zero-based track.
        with wave.open(str(output), "wb") as wav:
            wav.setnchannels(2)
            wav.setsampwidth(2)
            wav.setframerate(44100)
            buffer = (ctypes.c_short * 88200)()
            for _ in range(math.ceil(INTRO + LOOP + 1)):
                checked(gme.gme_play(emulator, 88200, buffer))
                wav.writeframes(bytes(buffer))
    finally:
        gme.gme_delete(emulator)


def import_cries(names, temp, sources):
    import array
    for name in names:
        url = f"https://raw.githubusercontent.com/PokeAPI/cries/main/cries/pokemon/legacy/{CRIES[name]}.ogg"
        original = temp / f"{name}.ogg"
        sources[:] = [entry for entry in sources if entry["url"] != url]
        fetch(url, original, sources)
        # Preserve the original pitch/timing; only normalize level and sample format.
        decoded = temp / f"{name}.wav"
        run("ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(original),
            "-ac", "1", "-ar", "44100", str(decoded))
        with wave.open(str(decoded), "rb") as wav:
            pcm = array.array("h", wav.readframes(wav.getnframes()))
        peak = max(abs(sample) for sample in pcm) / 32768
        gain = 10 ** (-12 / 20) / max(peak, 1e-9)
        run("ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(decoded),
            "-af", f"volume={gain}", str(DEST / "Cries" / f"{name}.wav"))
        print("Imported", name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only-cries", nargs="+", choices=list(CRIES),
                        help="Import selected cries while preserving the soundtrack and existing source records")
    args = parser.parse_args()
    (DEST / "Cries").mkdir(parents=True, exist_ok=True)
    (DEST / "Music").mkdir(parents=True, exist_ok=True)
    manifest = Path(__file__).parent / "sources-pokemon.json"
    if args.only_cries:
        report = json.loads(manifest.read_text())
        with tempfile.TemporaryDirectory(prefix="pokemon-cries-") as folder:
            import_cries(args.only_cries, Path(folder), report["sources"])
        manifest.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n")
        return
    sources = []
    with tempfile.TemporaryDirectory(prefix="pokemon-audio-") as folder:
        temp = Path(folder)
        archive = temp / "red.zip"
        fetch(ARCHIVE, archive, sources)
        gbs = temp / "red.gbs"
        with zipfile.ZipFile(archive) as bundle:
            gbs.write_bytes(bundle.read("DMG-APAE-USA.gbs"))
        render_gbs(gbs, temp / "render.wav")
        trimmed = temp / "loop.wav"
        run("ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(temp / "render.wav"), "-ss", str(INTRO), "-t", str(LOOP),
            "-af", f"afade=t=in:d=0.003,afade=t=out:st={LOOP-.003}:d=0.003",
            "-ac", "1", "-ar", "44100", str(trimmed))
        measurement = run("ffmpeg", "-hide_banner", "-i", str(trimmed),
                          "-af", "loudnorm=I=-18:TP=-3:LRA=9:print_format=json",
                          "-f", "null", "-").stderr
        values, _ = json.JSONDecoder().raw_decode(measurement[measurement.rfind("{"):])
        normalize = ("loudnorm=I=-18:TP=-3:LRA=9:linear=true:"
                     f"measured_I={values['input_i']}:measured_TP={values['input_tp']}:"
                     f"measured_LRA={values['input_lra']}:measured_thresh={values['input_thresh']}:"
                     f"offset={values['target_offset']}")
        run("ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(trimmed),
            "-af", normalize, "-ar", "44100", "-c:a", "libvorbis", "-q:a", "5",
            str(DEST / "Music/LavenderTown.ogg"))
        import_cries(CRIES, temp, sources)
    report = {"soundtrack": "Lavender Town — Pokémon Red (Junichi Masuda, 1996)",
              "loop_seconds": LOOP, "intro_trim_seconds": INTRO,
              "cry_version": "PokéAPI legacy (original pre-Gen-VI cries)",
              "rights": "Pokémon audio belongs to its respective rights holders; not original project composition.",
              "sources": sources}
    manifest.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n")
    print(f"Lavender Town: {LOOP:.6f}s, plus {len(CRIES)} original cries")


if __name__ == "__main__":
    main()
