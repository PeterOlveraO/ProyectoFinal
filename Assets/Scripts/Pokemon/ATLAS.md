# Recortes de Pokemones.png

Las coordenadas son píxeles del PNG original de 1024 × 5408, con origen en la esquina superior izquierda. Cada posición identifica la primera celda de 32 × 32 del bloque: espalda en `y`, frente en `y + 32`, perfil en `y + 64`; la segunda pose está en `x + 32`. Los recortes conservan el lienzo de la celda y eliminan el color del fondo.

| Protagonista | x | y |
|---|---:|---:|
| Pikachu (gratis) | 576 | 128 |
| Charizard | 384 | 0 |
| Garchomp | 256 | 4256 |
| Vaporeon | 448 | 1024 |
| Eevee | 384 | 1024 |
| Snorlax | 0 | 1152 |
| Blastoise | 576 | 0 |
| Dragonite | 384 | 1152 |
| Lucario | 512 | 4256 |
| Bulbasaur | 0 | 0 |

Jefes: Articuno `(64, 1152)`, Zapdos `(128, 1152)` y Moltres `(192, 1152)`. El manifiesto completo de enemigos está en `Editor/PokemonAtlasGenerator.cs`.

Se verificaron las tres orientaciones de estos trece personajes. En varios casos las dos celdas de una vista son idénticas o son poses distintas, no necesariamente fotogramas de caminar; en esos casos no existe un ciclo de caminata real en el bloque seleccionado. La cuarta fila disponible en algunos bloques muestra el perfil opuesto, no se interpreta como un paso. Las aves tienen pares de poses, no un ciclo de caminar. No se inventan vistas o poses que falten.

Los 198 PNG y sus ajustes de importación están en `Assets/Resources/PokemonAtlas`, listos para incorporarse a una compilación. Para regenerarlos desde el PNG original, ejecutar desde la raíz `python3 Assets/Scripts/Pokemon/generate_atlas.py` (requiere ImageMagick 7), o usar **Tools > Pokemon > Regenerar atlas** en Unity. Ambos procedimientos usan los mismos nombres y coordenadas del código del generador de Unity. Cambiar los nombres o el orden de la lista de protagonistas puede afectar selecciones guardadas por índice.
