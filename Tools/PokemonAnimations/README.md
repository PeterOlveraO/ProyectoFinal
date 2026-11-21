# Directional Pokémon frames

`mapping.json` records the top-left 32×32 cell of each two-column pose group
in `Assets/Imagenes/Pokemones.png`, keyed by the **existing sprite filename**.
The rows are back, front and left profile; each row has two cells. Frames are
extracted with a shared 32×32 canvas and centered pivot. The left profile is
mirrored for the renderer's right-facing source. Sheet background is removed
only when connected to the edge of a cell.
An optional third coordinate is the cell size: Lugia, Ho-Oh and Rayquaza use 64×64
cells. Their portraits and all six views preserve that same canvas.

Regenerate and check (requires `uv`):

```sh
uv run --with pillow python Tools/PokemonAnimations/extract.py
uv run --with pillow python Tools/PokemonAnimations/verify.py
```

The verifier checks that every source PNG or portrait's opaque pixels occur in its mapped
two-column, four-row group and that all six generated frames and their metadata
exist. It checks pixel provenance, not Pokémon species names. Unity EditMode
tests additionally check imported sprites and direction changes.

For `poke_040`, `poke_043`, `poke_081` and `poke_140`, adjacent columns depict
different perspectives rather than compatible animation frames. The extractor
duplicates the first frame in each view rather than alternate orientations.
Some other source poses may also have little or no visible frame-to-frame motion.
No image is synthesized, and no files in `PokemonSprites` are changed.

`species_jolteon` and `species_lucario` use portraits for the visibly identified
sheet groups (512,1024) and (512,4256). The ten additional special-gacha species
also use `PokemonPortraits`, so they do not enter the ordinary enemy sprite pool.

Extract just the new species (preserves existing metadata):

```sh
python3 Tools/PokemonAnimations/extract.py --portraits --only \
  species_tyranitar species_salamence species_metagross species_mewtwo \
  species_lugia species_ho-oh species_rayquaza species_celebi species_jirachi species_darkrai
```
