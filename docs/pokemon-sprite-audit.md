# Auditoría visual de sprites del catálogo Pokémon

Revisión de `Assets/Resources/PokemonProgression/catalog.json` contra las imágenes existentes en `Assets/Resources/PokemonSprites/` y los frames frontales verificados de Terra. La hoja frontal actual, con las 18 especies etiquetadas y ampliadas, está en [`docs/evidence/pokemon-catalog-contact.png`](evidence/pokemon-catalog-contact.png). La hoja complementaria de seis frames por especie (`front_0`, `front_1`, `back_0`, `back_1`, `side_0`, `side_1`) es [`docs/evidence/pokemon-directional-review.png`](evidence/pokemon-directional-review.png).

| Especie | `spriteName` | Evidencia visual |
|---|---|---|
| Jolteon | `species_jolteon` | Frame frontal verificado de Terra: orejas amarillas puntiagudas y collar claro. |
| Charizard | `species_charizard` | Dragón naranja alado, coincide con Charizard. |
| Garchomp | `species_garchomp` | Pokémon azul con rasgos de tiburón/aletas, coincide con Garchomp. |
| Vaporeon | `species_vaporeon` | Pokémon acuático azul con aleta dorsal, coincide con Vaporeon. |
| Flareon | `species_flareon` | Pokémon naranja con melena crema, coincide con Flareon. |
| Snorlax | `species_snorlax` | Cuerpo ancho azul verdoso y vientre crema, coincide con Snorlax. |
| Blastoise | `species_blastoise` | Tortuga azul con cañones, coincide con Blastoise. |
| Dragonite | `species_dragonite` | Dragón naranja con alas pequeñas, coincide con Dragonite. |
| Lucario | `species_lucario` | Frame frontal verificado de Terra: cuerpo azul/negro, orejas largas y espinas. |
| Venusaur | `species_venusaur` | Pokémon verde con gran flor rosa, coincide con Venusaur. |
| Gengar | `species_gengar` | Pokémon compacto morado y puntiagudo, coincide con Gengar. |
| Gyarados | `species_gyarados` | Serpiente azul con cresta y bigotes, coincide con Gyarados. |
| Lapras | `species_lapras` | Pokémon azul con caparazón y cuello largo, coincide con Lapras. |
| Ditto | `species_ditto` | Figura pequeña amorfa morada, coincide con Ditto. |
| Aerodactyl | `poke_140` | Pokémon púrpura alado con mandíbula prominente, coincide con Aerodactyl. |
| Kangaskhan | `species_kangaskhan` | Pokémon grande con bolsa y cría, coincide con Kangaskhan. |
| Tauros | `species_tauros` | Toro con tres colas, coincide con Tauros. |
| Mew | `species_mew` | Pokémon pequeño rosado con cola larga, coincide con Mew. |

## Resultado

- **18/18** entradas tienen `id`, nombre y `spriteName` únicos; no hay campos nulos ni referencias de imagen faltantes.
- Rectificación de la inspección anterior: fue incorrecto asignar `poke_114` a Jolteon y `poke_092` a Lucario. El mapeo visual confirmado en `Pokemones.png` es `poke_114` → grupo Meowth en `(320, 384)` y `poke_092` → grupo Swampert en `(704, 2400)`. Por ello el catálogo ahora apunta a los retratos frontales dedicados `species_jolteon` y `species_lucario`, copiados sin modificación desde `/tmp/opencode/proyectofinal-issue8/Assets/Resources/PokemonAnimations/species_jolteon/front_0.png` y `/tmp/opencode/proyectofinal-issue8/Assets/Resources/PokemonAnimations/species_lucario/front_0.png`.
- Las coordenadas de origen confirmadas para los grupos correctos en `Pokemones.png` son Jolteon `(512, 1024)` y Lucario `(512, 4256)`. Las capturas de evidencia recibidas son `/tmp/opencode/issue8_eeveelutions.png` y `/tmp/opencode/issue8_lucario_real.png`; también se inspeccionaron los PNG `front_0.png` fuente antes de copiarlos.
- La revisión direccional complementaria confirma las seis poses de las 18 especies; confirma también las identidades de los otros Pokémon y que `Aerodactyl` → `poke_140` se conserva correctamente.
- `PokemonCollection.GetSprite` busca ahora primero `Resources/PokemonPortraits/<spriteName>` y usa `Resources/PokemonSprites` como fallback. Los retratos nuevos permanecen fuera de `PokemonSprites`, de modo que el pool existente de 178 sprites para enemigos no los incluye. Los IDs de especie y datos de guardado no cambian.
- Los dos PNG fuente son 32 × 32 px; sus `.meta` usan GUID únicos, Sprite (Single), 32 PPU, filtro Point y pivote centrado.

Comprobaciones estáticas ejecutadas: JSON parseable, 18 IDs/nombres/referencias únicos, cero valores nulos, las 18 imágenes existen y no hay GUID duplicados entre sus `.meta`.
