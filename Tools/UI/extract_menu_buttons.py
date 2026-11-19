"""Extract blank button frames from the supplied sheet; no generated art/branding.

Run from the repository root: python3 Tools/UI/extract_menu_buttons.py
Requires Pillow. Text is rendered by TMP at runtime, not baked into sprites.
"""
from pathlib import Path
from PIL import Image


source = Image.open("Assets/Imagenes/botones.jpg").convert("RGBA")
destination = Path("Assets/Resources/UI/CompactMenus")
destination.mkdir(parents=True, exist_ok=True)
frames = {
    "Green": (57, 49, 235, 104),
    "Blue": (328, 49, 506, 104),
    "Amber": (328, 331, 506, 386),
    "Red": (57, 472, 235, 527),
}
for name, box in frames.items():
    frame = source.crop(box)
    # Keep the original bevel, outline, highlights and vertical fill gradient.
    # Replace only the lettering with a clean sample from the same scanline.
    for y in range(10, 37):
        samples = [frame.getpixel((x, y)) for x in range(10, 17)]
        fill = tuple(round(sum(p[c] for p in samples) / len(samples)) for c in range(4))
        for x in range(9, frame.width - 9):
            frame.putpixel((x, y), fill)
    # Remove only the outside white paper, preserving the bright bevel.
    pending = [(0, 0), (frame.width - 1, 0), (0, frame.height - 1),
               (frame.width - 1, frame.height - 1)]
    visited = set()
    while pending:
        x, y = pending.pop()
        if (x, y) in visited or not (0 <= x < frame.width and 0 <= y < frame.height):
            continue
        visited.add((x, y))
        r, g, b, _ = frame.getpixel((x, y))
        if min(r, g, b) < 210:
            continue
        frame.putpixel((x, y), (0, 0, 0, 0))
        pending.extend([(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)])
    frame.save(destination / (name + ".png"))
