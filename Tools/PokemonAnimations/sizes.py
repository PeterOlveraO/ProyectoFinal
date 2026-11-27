#!/usr/bin/env python3
"""Reproducible Pokédex measurements for every in-game sprite family (stdlib only)."""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
COMMIT = "e4f981e5cb03c1fe33d436b1ee8e1197fbb0af84"
URL = f"https://raw.githubusercontent.com/PokeAPI/pokeapi/{COMMIT}/data/v2/csv/pokemon.csv"
SHA256 = "eb8a7f59134bf3763a0f3072a7794cb097413e6f6027c380a92dd8d63082ab80"
SNAPSHOT = HERE / "pokemon.csv"
OUTPUT = ROOT / "Assets/Resources/PokemonPhysicalSizes.json"


def generate():
    raw = SNAPSHOT.read_bytes()
    assert hashlib.sha256(raw).hexdigest() == SHA256, "Source snapshot changed"
    pokemon = {row["identifier"]: row for row in csv.DictReader(io.StringIO(raw.decode()))}
    identities = json.loads((HERE / "identities.json").read_text())["pokemon"]
    cells = json.loads((HERE / "mapping.json").read_text())["cells"]
    metrics = json.loads((ROOT / "Assets/Resources/PokemonVisualMetrics.json").read_text())
    assert set(cells) == {family["name"] for family in metrics["families"]}
    assert set(identities) == {name for name in cells if name.startswith("poke_")}
    families, seen_cells = [], {}
    for name, cell in sorted(cells.items()):
        identifier = identities[name] if name in identities else name.removeprefix("species_")
        row = pokemon[identifier]
        height, weight = int(row["height"]) / 10, int(row["weight"]) / 10
        assert height > 0 and weight > 0, name
        position = tuple(cell)
        assert seen_cells.setdefault(position, identifier) == identifier, f"Conflicting aliases: {name}"
        families.append({
            "name": name, "pokemon": identifier,
            "pokedexId": int(row["species_id"]), "pokemonId": int(row["id"]),
            "heightMeters": height, "weightKilograms": weight,
        })
    by_name = {family["name"]: family for family in families}
    catalog = json.loads((ROOT / "Assets/Resources/PokemonProgression/catalog.json").read_text())
    for species in catalog["species"]:
        assert by_name[species["spriteName"]]["pokemon"] == species["id"], species["id"]
    sources = list((ROOT / "Assets/Resources/PokemonSprites").glob("*.png"))
    sources += list((ROOT / "Assets/Resources/PokemonPortraits").glob("*.png"))
    assert {path.stem for path in sources} == set(cells), "Unmapped source/portrait"
    result = {
        "version": 1, "worldUnitsPerMeter": 1.0,
        "measurement": "Standard Pokedex height (dm / 10) and weight (hg / 10), not a sample average. Height is the maximum animated visible vertical envelope; preserve aspect ratio.",
        "sourceUrl": URL, "sourceSha256": SHA256,
        "identitiesSha256": hashlib.sha256((HERE / "identities.json").read_bytes()).hexdigest(),
        "mappingSha256": hashlib.sha256((HERE / "mapping.json").read_bytes()).hexdigest(),
        "families": families,
    }
    return json.dumps(result, ensure_ascii=False, indent=2) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fetch", action="store_true", help="Download the pinned source snapshot")
    parser.add_argument("--check", action="store_true", help="Verify without changing generated data")
    args = parser.parse_args()
    if args.fetch:
        with urllib.request.urlopen(URL, timeout=30) as response:
            data = response.read()
        assert hashlib.sha256(data).hexdigest() == SHA256
        SNAPSHOT.write_bytes(data)
    text = generate()
    if args.check:
        assert OUTPUT.read_text() == text, "Run sizes.py to regenerate physical sizes"
    else:
        OUTPUT.write_text(text)
    families = json.loads(text)["families"]
    print(f"Verified {len(families)} families, {len({f['pokedexId'] for f in families})} species, "
          f"{len({f['pokemonId'] for f in families})} forms; "
          f"{min(f['heightMeters'] for f in families)}–{max(f['heightMeters'] for f in families)} m.")


if __name__ == "__main__":
    main()
