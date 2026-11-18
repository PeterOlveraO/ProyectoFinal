# Directional Pokémon frames

`mapping.json` records the top-left 32×32 cell of each two-column pose group
in `Assets/Imagenes/Pokemones.png`, keyed by the **existing sprite filename**.
The rows are back, front and left profile; each row has two cells. Frames are
extracted with a shared 32×32 canvas and centered pivot. The left profile is
mirrored for the renderer's right-facing source. Sheet background is removed
only when connected to the edge of a cell.

Regenerate and check (requires `uv`):

```sh
uv run --with pillow python Tools/PokemonAnimations/extract.py
uv run --with pillow python Tools/PokemonAnimations/verify.py
```

The verifier checks that every existing PNG's opaque pixels occur in its mapped
two-column, four-row group and that all six generated frames and their metadata
exist. It checks pixel provenance, not Pokémon species names. Unity EditMode
tests additionally check imported sprites and direction changes.

For `poke_040`, `poke_043`, `poke_081` and `poke_140`, adjacent columns depict
different perspectives rather than compatible animation frames. The extractor
duplicates the first frame in each view rather than alternate orientations.
Some other source poses may also have little or no visible frame-to-frame motion.
No image is synthesized, and no files in `PokemonSprites` are changed.

`species_jolteon` and `species_lucario` are additional prepared frame keys for
the visibly identified sheet groups (512,1024) and (512,4256), respectively.
They are not currently source PNGs in the existing pool and do not change it.
