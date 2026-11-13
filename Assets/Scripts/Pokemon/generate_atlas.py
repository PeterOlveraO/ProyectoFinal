#!/usr/bin/env python3
"""Generate the checked-in sprite PNGs and Unity import settings from the atlas.

Run from the project root with Python 3 and ImageMagick 7: python3
Assets/Scripts/Pokemon/generate_atlas.py. Coordinates and names are read from the
Unity generator and runtime catalog, so there is only one coordinate manifest.
"""
import hashlib
import re
import struct
import subprocess
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'Assets/Imagenes/Pokemones.png'
OUTPUT = ROOT / 'Assets/Resources/PokemonAtlas'
EDITOR = (ROOT / 'Assets/Scripts/Pokemon/Editor/PokemonAtlasGenerator.cs').read_text()
LIBRARY = (ROOT / 'Assets/Scripts/Pokemon/PokemonSpriteLibrary.cs').read_text()
META = (ROOT / 'Assets/Resources/PokemonSprites/poke_000.png.meta').read_text()


def block(text, name):
    return re.search(r'\b' + name + r'\s*=\s*\{(.*?)\}', text, re.S).group(1)


def png(data):
    def chunk(name, payload):
        body = name + payload
        return struct.pack('>I', len(payload)) + body + struct.pack('>I', zlib.crc32(body))
    scanlines = b''.join(b'\0' + data[row * 128:(row + 1) * 128] for row in range(32))
    return (b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 32, 32, 8, 6, 0, 0, 0))
            + chunk(b'IDAT', zlib.compress(scanlines, 9)) + chunk(b'IEND', b''))


def main():
    raw = subprocess.check_output(['magick', str(SOURCE), '-depth', '8', 'rgba:-'])
    if len(raw) != 1024 * 5408 * 4:
        raise ValueError('El atlas debe medir 1024 x 5408 RGBA')
    OUTPUT.mkdir(parents=True, exist_ok=True)
    count = 0
    for names_key, positions_key in [('PlayerNames', 'Players'), ('EnemyNames', 'Enemies'), ('BossNames', 'Bosses')]:
        names = re.findall(r'"([^"]+)"', block(LIBRARY, names_key))
        positions = [(int(x), int(y)) for x, y in re.findall(r'new Vector2Int\((\d+),\s*(\d+)\)', block(EDITOR, positions_key))]
        if len(names) != len(positions):
            raise ValueError('Catálogo y coordenadas no coinciden: ' + names_key)
        for name, (x, y) in zip(names, positions):
            for direction, dy in [('back', 0), ('front', 32), ('side', 64)]:
                for frame in range(2):
                    left, top = x + frame * 32, y + dy
                    origin = (top * 1024 + left) * 4
                    background = raw[origin:origin + 3]
                    cell = bytearray()
                    for row in range(32):
                        start = ((top + row) * 1024 + left) * 4
                        line = bytearray(raw[start:start + 128])
                        for col in range(32):
                            if line[col * 4:col * 4 + 3] == background:
                                line[col * 4 + 3] = 0
                        cell.extend(line)
                    filename = f'{name}_{direction}_{frame}.png'
                    (OUTPUT / filename).write_bytes(png(cell))
                    guid = hashlib.md5(('pokemon-atlas-v2/' + filename).encode()).hexdigest()
                    metadata = re.sub(r'(?m)^guid: [0-9a-f]+$', 'guid: ' + guid, META)
                    (OUTPUT / (filename + '.meta')).write_text(
                        '\n'.join(line.rstrip() for line in metadata.splitlines()) + '\n')
                    count += 1
    (OUTPUT / 'atlas-v2.txt').write_text('Assets/Imagenes/Pokemones.png')
    print(f'Generados {count} sprites en {OUTPUT.relative_to(ROOT)}')


if __name__ == '__main__':
    main()
