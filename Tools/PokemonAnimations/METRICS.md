# Visible size and ground support

`metrics.py` reads existing PNG alpha channels offline and writes only
`Assets/Resources/PokemonVisualMetrics.json`. Regenerate after intentionally changing
art; verify without writing:

```sh
uv run --with pillow python Tools/PokemonAnimations/metrics.py
uv run --with pillow python Tools/PokemonAnimations/metrics.py --check
```

The deterministic JSON includes all 178 ordinary sources, 12 portrait-only sources,
and 1,140 directional frames. SHA-256 hashes identify every input image, the sheet,
and mapping. Generation verifies each frame against its mapped sheet cell, including
mirroring and the four duplicated-column exceptions. It also verifies centered pivots,
32 PPU, Point filtering, and non-readable importers without changing them.

Bounds use alpha > 0, exclusive pixel edges and a bottom-left origin. `bottom` is
the support edge; X stays at the existing centered canvas pivot, rather than tracking
each frame's alpha centroid. Thus changing direction/phase cannot introduce an
extra horizontal translation.

Runtime rules:

- Initial/fallback source: fit its own visible alpha height and width; retain its
  name and sprite until the first animation update. This handles both tightly cropped
  `poke_*` sources and padded `species_*` sources.
- Animated family: one uniform scale for all six frames,
  `min(targetHeight * PPU / maxVisibleHeight, maxWidth * PPU / maxVisibleWidth)`.
  The width cap preserves aspect ratio, including wide/64px species.
- Per-frame support: `(pivot.y - bottom) * scale / PPU`. Only the child sprite's
  scale/Y are adjusted. Poses with shorter silhouettes remain shorter rather than
  stretching or changing scale on every step.
- Unknown sources without metrics use their full rectangle, with no runtime pixel
  reads. Known sources missing animation frames retain their source alpha bounds.
- The player uses the same helper with its own height (`1.65`) and foot offset (`-0.5`).
  Changing the equipped source rebuilds the layout before rendering.

Normal enemy profile: height `1.35`, maximum width `2.25`. Bosses explicitly opt in
**after** `Attach` / `InitializeRound`, for example:

```csharp
enemy.GetComponent<PokemonEnemyVisual>()?.SetVisualProfile(1.9f, 3.2f);
```

This applies immediately to the current source/frame and updates the contact-shadow
size. Every `Attach` resets the visual profile to normal for reused actors. No boss
state is inferred before combat initialization.

The read-only Python check validates 3,990 source/frame/profile support and size
cases for normal, boss, and player profiles, both mirror states, and the duplicate
`poke_052`/`species_mew` family. `PokemonVisualMetricsTests.cs` exercises the imported
resources, original animation API, all six frames, missing animations, unknown sources,
and immediate boss resizing. Unity tests are to be run by the owning editor session.
