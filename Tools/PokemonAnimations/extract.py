#!/usr/bin/env python3
"""Regenerate reviewed frames: uv run --with pillow python Tools/PokemonAnimations/extract.py."""
import argparse
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
    if Path(str(path) + ".meta").exists():
        return  # Preserve existing GUIDs and user import settings.
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
    width, height = image.size
    for x in range(width):
        q.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        q.extend(((0, y), (width - 1, y)))
    while q:
        x, y = q.popleft()
        if not (0 <= x < width and 0 <= y < height) or pixels[x, y] != bg:
            continue
        pixels[x, y] = (0, 0, 0, 0)
        q.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
    return image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="+", help="Extract only these mapped keys")
    parser.add_argument("--portraits", action="store_true", help="Also export the first front view to PokemonPortraits")
    args = parser.parse_args()
    sheet = Image.open(SHEET).convert("RGBA")
    entries = json.loads(MAPPING.read_text())["cells"]
    if args.only:
        entries = {name: entries[name] for name in args.only}
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT, True)
    # These sheets put different perspectives, not walk frames, in adjacent columns.
    # Duplicating a view is preferable to alternating between unrelated perspectives.
    single_column = {"poke_040", "poke_043", "poke_081", "poke_140"}
    for name, cell in entries.items():
        x, y = cell[:2]
        size = cell[2] if len(cell) > 2 else 32
        directory = OUT / name
        directory.mkdir(exist_ok=True)
        meta(directory, True)
        for view, row in (("back", 0), ("front", 1), ("side", 2)):
            for i in range(2):
                left, top = x + size * (0 if name in single_column else i), y + size * row
                frame = remove_background(sheet.crop((left, top, left + size, top + size)))
                if view == "side":
                    frame = frame.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                path = directory / f"{view}_{i}.png"
                frame.save(path, optimize=False)
                meta(path)
                if args.portraits and view == "front" and i == 0:
                    portrait = ROOT / "Assets/Resources/PokemonPortraits" / f"{name}.png"
                    portrait.parent.mkdir(exist_ok=True)
                    meta(portrait.parent, True)
                    frame.save(portrait, optimize=False)
                    meta(portrait)
    print(f"Generated {len(entries) * 6} frames for {len(entries)} source sprites.")


if __name__ == "__main__":
    main()
