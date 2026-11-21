#!/usr/bin/env python3
"""Verify each source sprite's opaque pixels inside its mapped 2x4-cell group."""
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sheet = Image.open(ROOT / "Assets/Imagenes/Pokemones.png").convert("RGB")
pixels = sheet.load()
mapping = json.loads(Path(__file__).with_name("mapping.json").read_text())["cells"]
source_dir = ROOT / "Assets/Resources/PokemonSprites"
names = {p.stem for p in source_dir.glob("*.png")}
portrait_dir = ROOT / "Assets/Resources/PokemonPortraits"
portraits = {p.stem for p in portrait_dir.glob("*.png")}
assert names <= set(mapping), names - set(mapping)
extra = set(mapping) - names
assert extra <= portraits, extra - portraits

for name, cell in mapping.items():
    gx, gy = cell[:2]
    size = cell[2] if len(cell) > 2 else 32
    if name in names or name in portraits:
        sprite = Image.open((source_dir if name in names else portrait_dir) / f"{name}.png").convert("RGBA")
        opaque = [(x, y, (r, g, b)) for y in range(sprite.height)
                  for x in range(sprite.width)
                  for r, g, b, a in (sprite.getpixel((x, y)),) if a == 255]
        assert opaque and sprite.width <= size and sprite.height <= size, name
        anchor = opaque[len(opaque) // 2]
        found = False
        for row in range(4):
            for col in range(2):
                for dy in range(size + 1 - sprite.height):
                    for dx in range(size + 1 - sprite.width):
                        x, y = gx + col * size + dx, gy + row * size + dy
                        if pixels[x + anchor[0], y + anchor[1]] != anchor[2]:
                            continue
                        if all(pixels[x + px, y + py] == color for px, py, color in opaque):
                            found = True
                            break
                    if found:
                        break
                if found:
                    break
            if found:
                break
        assert found, f"{name}: source pixels not found in mapped group ({gx}, {gy})"
    for view in ("front", "back", "side"):
        for i in range(2):
            frame = ROOT / f"Assets/Resources/PokemonAnimations/{name}/{view}_{i}.png"
            image = Image.open(frame).convert("RGBA")
            assert image.size == (size, size) and image.getbbox() is not None, str(frame)
            assert Path(str(frame) + ".meta").is_file(), str(frame)
print(f"Verified {len(mapping)} source matches and {len(mapping) * 6} generated frames ({len(extra)} portrait-only keys).")
