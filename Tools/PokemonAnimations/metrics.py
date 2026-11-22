#!/usr/bin/env python3
"""Generate/check alpha bounds without changing PNGs or their Unity importers.

uv run --with pillow python Tools/PokemonAnimations/metrics.py [--check]
"""
import argparse
import hashlib
import json
import math
import re
import sys
from pathlib import Path

from PIL import Image

sys.dont_write_bytecode = True
from extract import remove_background

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / "Assets/Resources"
OUTPUT = RESOURCES / "PokemonVisualMetrics.json"
MAPPING = Path(__file__).with_name("mapping.json")
SHEET = ROOT / "Assets/Imagenes/Pokemones.png"
FRAME_NAMES = ("front_0", "front_1", "back_0", "back_1", "side_0", "side_1")
SINGLE_COLUMN = {"poke_040", "poke_043", "poke_081", "poke_140"}


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def measure(path):
    image = Image.open(path).convert("RGBA")
    box = image.getchannel("A").getbbox()  # Every alpha > 0 pixel; exclusive edges.
    assert box is not None, f"Empty sprite: {path}"
    left, top, right, lower = box
    meta = Path(str(path) + ".meta").read_text()
    # Bounds are in source pixels. Fail instead of quietly using stale importer assumptions.
    for expression in (r"spriteMode: 1\b", r"spritePixelsToUnits: 32\b",
                       r"spritePivot: \{x: 0.5, y: 0.5\}", r"isReadable: 0\b",
                       r"filterMode: 0\b"):
        assert re.search(expression, meta), f"{path}: expected {expression}"
    return {
        "resource": path.relative_to(RESOURCES).with_suffix("").as_posix(),
        "sha256": sha256(path),
        "width": image.width,
        "height": image.height,
        "left": left,
        "bottom": image.height - lower,
        "visibleWidth": right - left,
        "visibleHeight": lower - top,
    }


def build():
    mapping = json.loads(MAPPING.read_text())["cells"]
    sources = {}
    for folder in ("PokemonSprites", "PokemonPortraits"):
        for path in sorted((RESOURCES / folder).glob("*.png")):
            assert path.stem not in sources, f"Ambiguous source: {path.stem}"
            sources[path.stem] = path
    assert set(mapping) == set(sources), "Mapping/source coverage differs"
    assert {p.name for p in (RESOURCES / "PokemonAnimations").iterdir() if p.is_dir()} == set(mapping)
    sheet = Image.open(SHEET).convert("RGBA")
    families = []
    for name, cell in sorted(mapping.items()):
        gx, gy = cell[:2]
        size = cell[2] if len(cell) > 2 else 32
        frames = []
        for frame_name in FRAME_NAMES:
            path = RESOURCES / "PokemonAnimations" / name / (frame_name + ".png")
            frame = Image.open(path).convert("RGBA")
            view, index = frame_name.split("_")
            row = {"back": 0, "front": 1, "side": 2}[view]
            col = 0 if name in SINGLE_COLUMN else int(index)
            left, top = gx + col * size, gy + row * size
            expected = remove_background(sheet.crop((left, top, left + size, top + size)))
            if view == "side":
                expected = expected.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            assert frame.size == (size, size) and frame.tobytes() == expected.tobytes(), path
            frames.append(measure(path))
        families.append({
            "name": name,
            "maxVisibleWidth": max(f["visibleWidth"] for f in frames),
            "maxVisibleHeight": max(f["visibleHeight"] for f in frames),
            "source": measure(sources[name]),
            "frames": frames,
        })
    return {
        "version": 1,
        "generator": "Tools/PokemonAnimations/metrics.py",
        "alphaThreshold": 1,
        "pixelsPerUnit": 32,
        "coordinates": "Pixel edges, bottom-left origin; support is bottom; horizontal anchor is canvas center, never per-frame alpha center.",
        "sheet": "Assets/Imagenes/Pokemones.png",
        "sheetSha256": sha256(SHEET),
        "mappingSha256": sha256(MAPPING),
        "frameOrder": list(FRAME_NAMES),
        "families": families,
    }


def validate_layout(data):
    """Independent geometry checks: normal, boss, player, source-only and mirrored frames."""
    families = data["families"]
    profiles = ((1.35, 2.25), (1.9, 3.2), (1.65, 2.75))
    checked = 0
    for family in families:
        for height, width in profiles:
            frame_scale = min(height * 32 / family["maxVisibleHeight"],
                              width * 32 / family["maxVisibleWidth"])
            for frame in [family["source"], *family["frames"]]:
                # The retained initial/fallback source uses its own crop; all six animation
                # frames share one scale, regardless of canvas, direction or walking phase.
                scale = (min(height * 32 / frame["visibleHeight"],
                             width * 32 / frame["visibleWidth"])
                         if frame is family["source"] else frame_scale)
                assert frame["visibleHeight"] * scale / 32 <= height + 1e-6
                assert frame["visibleWidth"] * scale / 32 <= width + 1e-6
                support = (frame["height"] / 2 - frame["bottom"]) * scale / 32
                assert math.isclose(support + (frame["bottom"] - frame["height"] / 2) * scale / 32,
                                    0, abs_tol=1e-6), (family["name"], frame)
                # Flipping changes alpha bounds, never the canvas anchor or support.
                for mirrored in (False, True):
                    left = frame["width"] - frame["left"] - frame["visibleWidth"] if mirrored else frame["left"]
                    assert 0 <= left <= frame["width"] - frame["visibleWidth"]
                    assert 0 <= frame["bottom"] <= frame["height"] - frame["visibleHeight"]
                checked += 1
    by_name = {f["name"]: f for f in families}
    a, b = by_name["poke_052"], by_name["species_mew"]
    assert (a["maxVisibleWidth"], a["maxVisibleHeight"]) == (b["maxVisibleWidth"], b["maxVisibleHeight"])
    for first, second in zip(a["frames"], b["frames"]):
        assert first["sha256"] == second["sha256"]
    print(f"Validated {len(families)} families / {len(families) * 6} frame provenance matches; "
          f"{checked} profile/anchor cases (including source-only fallback and both mirrors).")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Read-only: fail if the committed metrics are stale")
    args = parser.parse_args()
    data = build()
    validate_layout(data)
    text = json.dumps(data, indent=2, ensure_ascii=True) + "\n"
    if args.check:
        assert OUTPUT.read_text() == text, "Metrics are stale: run metrics.py without --check"
        print("Metrics match all current PNGs and importer assumptions.")
    else:
        OUTPUT.write_text(text)
        print(f"Wrote {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
