#!/usr/bin/env python3
"""Regenerate reviewed frames: uv run --with pillow python Tools/PokemonAnimations/extract.py."""
import hashlib
import json
import re
from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/PokemonAnimations"
SHEET = ROOT / "Assets/Imagenes/Pokemones.png"
MAPPING = Path(__file__).with_name("mapping.json")


def guid(path):
    return hashlib.md5(("directional-frames-v1:" + path).encode()).hexdigest()


def meta(path, folder=False):
    relative = path.relative_to(ROOT).as_posix()
    if folder:
        content = "fileFormatVersion: 2\nguid: " + guid(relative) + "\n"
        content += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        # Reuse Unity-generated importer metadata from an existing 32 PPU sprite,
        # changing only per-asset identifiers and the centered alignment.
        template = (ROOT / "Assets/Resources/PokemonSprites/poke_000.png.meta").read_text()
        content = re.sub(r"(?m)^guid: [0-9a-f]+$", "guid: " + guid(relative), template)
        content = re.sub(r"(?m)^    spriteID: [0-9a-f]+$",
                         "    spriteID: " + guid(relative + ":sprite"), content)
        content = content.replace("  alignment: 9\n", "  alignment: 0\n")
        content = "\n".join(line.rstrip() for line in content.splitlines()) + "\n"
    Path(str(path) + ".meta").write_text(content)


def remove_background(image):
    """Remove only the solid sheet background connected to a cell edge."""
    image = image.convert("RGBA")
    pixels = image.load()
    bg = pixels[0, 0]
    q = deque()
    for i in range(32):
        q.extend(((i, 0), (i, 31), (0, i), (31, i)))
    while q:
        x, y = q.popleft()
        if not (0 <= x < 32 and 0 <= y < 32) or pixels[x, y] != bg:
            continue
        pixels[x, y] = (0, 0, 0, 0)
        q.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
    return image


def main():
    sheet = Image.open(SHEET).convert("RGBA")
    entries = json.loads(MAPPING.read_text())["cells"]
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT, True)
    # These sheets put different perspectives, not walk frames, in adjacent columns.
    # Duplicating a view is preferable to alternating between unrelated perspectives.
    single_column = {"poke_040", "poke_043", "poke_081", "poke_140"}
    for name, (x, y) in entries.items():
        directory = OUT / name
        directory.mkdir(exist_ok=True)
        meta(directory, True)
        for view, row in (("back", 0), ("front", 1), ("side", 2)):
            for i in range(2):
                left, top = x + 32 * (0 if name in single_column else i), y + 32 * row
                frame = remove_background(sheet.crop((left, top, left + 32, top + 32)))
                if view == "side":
                    frame = frame.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                path = directory / f"{view}_{i}.png"
                frame.save(path, optimize=False)
                meta(path)
    print(f"Generated {len(entries) * 6} frames for {len(entries)} source sprites.")


if __name__ == "__main__":
    main()
