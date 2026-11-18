# Auditoría visual de sprites del catálogo Pokémon

Revisión de `Assets/Resources/PokemonProgression/catalog.json` contra las imágenes existentes en `Assets/Resources/PokemonSprites/`. Se inspeccionaron visualmente las 18 imágenes individuales ampliadas y una hoja de contacto; además se revisó la hoja multipose `Assets/Imagenes/Pokemones.png` (1024 × 5408).

| Especie | `spriteName` | Evidencia visual |
|---|---|---|
| Jolteon | `poke_114` | Pokémon amarillo con orejas largas y collar claro puntiagudo; coincide con Jolteon. |
| Charizard | `species_charizard` | Dragón naranja alado, coincide con Charizard. |
| Garchomp | `species_garchomp` | Pokémon azul con rasgos de tiburón/aletas, coincide con Garchomp. |
| Vaporeon | `species_vaporeon` | Pokémon acuático azul con aleta dorsal, coincide con Vaporeon. |
| Flareon | `species_flareon` | Pokémon naranja con melena crema, coincide con Flareon. |
| Snorlax | `species_snorlax` | Cuerpo ancho azul verdoso y vientre crema, coincide con Snorlax. |
| Blastoise | `species_blastoise` | Tortuga azul con cañones, coincide con Blastoise. |
| Dragonite | `species_dragonite` | Dragón naranja con alas pequeñas, coincide con Dragonite. |
| Lucario | `poke_092` | Pokémon azul y negro con orejas largas y espinas naranjas, coincide con Lucario. |
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
- La captura `Captures/Issues7-13/issues-baseline-menu.png` muestra la imagen seleccionada antes de confirmar el inicial y confirma el problema visible: `poke_139` no coincide con Jolteon. Comparé los PNG existentes ampliados; `poke_114` sí muestra a Jolteon y está asignado en el catálogo. Corregí solo `spriteName`, conservando el `id` `jolteon` y todos los datos guardados.
- `Lucario` → `poke_092` y `Aerodactyl` → `poke_140` sí coinciden visualmente con sus nombres. No se dedujo identidad a partir del número del archivo ni del número de Pokédex.
- **Pistas para issue 8:** la imagen frontal corregida disponible es `Assets/Resources/PokemonSprites/poke_114.png` (14 × 16 px). `Pokemones.png` contiene grupos de poses y orientaciones, pero `Pokemones.png.meta` solo define un sprite con el rectángulo completo (1024 × 5408, `sliceOnImport: false`); no hay cortes ni nombres de frames. No encontré una correspondencia inequívoca entre esos grupos y los PNG individuales del catálogo, así que no anoto coordenadas de frente/espalda/perfil para evitar inventar recortes. Hace falta mapear los grupos directamente sobre la hoja antes de implementar animación.

Comprobaciones estáticas ejecutadas: JSON parseable, 18 IDs/nombres/referencias únicos, cero valores nulos, las 18 imágenes existen y no hay GUID duplicados entre sus `.meta`.
