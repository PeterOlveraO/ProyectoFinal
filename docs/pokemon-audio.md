# Audio original de Pokémon

Integrado por petición del usuario. Estos recursos son audio del juego original,
no composiciones nuevas del proyecto.

| Recurso | Procedencia |
| --- | --- |
| Pueblo Lavanda | Pokémon Red, Junichi Masuda, 1996. Pista GBS 11 (base cero) del archivo público de [Zophar's Domain](https://www.zophar.net/music/gameboy-gbs/pokemon-red). |
| Gritos de las 18 especies | [PokéAPI/cries](https://github.com/PokeAPI/cries), versión `legacy`: sonidos originales anteriores a la revisión de sexta generación, conservando su tono. El repositorio identifica Showdown, Veekun y The Sounds Resource como sus fuentes. |

Pokémon y su audio pertenecen a sus respectivos titulares, incluidos Nintendo,
Game Freak y Creatures. Los archivos descargados y sus SHA-256 se registran en
[`Tools/Audio/sources-pokemon.json`](../Tools/Audio/sources-pokemon.json).

## Preparación

```sh
python3 Tools/Audio/import_pokemon_audio.py
```

Requiere Python, FFmpeg y `libgme`. Renderiza la música desde los datos originales
de Game Boy, normaliza el loop a −18 LUFS y los gritos a un pico de −12 dBFS.
No se altera la velocidad ni el tono de los gritos.

El loop conserva el ciclo común de los cuatro canales tras la introducción:
2560 unidades musicales, calculadas con el tempo 152 y el reloj original de Game Boy.
Se aplica una envolvente de 3 ms en los bordes para evitar clics.
El OGG final dura **305.386963 s** y mide **−18.0 LUFS-I / −4.7 dBTP**.
Los 18 gritos duran entre **0.268 y 1.433 s**, todos con pico de **−12.0 dBFS**.

## Uso en el juego

- Pueblo Lavanda domina menú, exploración/preparación y combate normal.
- Los cambios entre esas situaciones mantienen la posición de reproducción.
- Los jefes introducen brevemente la composición de combate intenso.
- Suenan los gritos al elegir un inicial, seleccionar una ficha, obtener un Pokémon,
  comenzar una partida y entrar a la ronda de jefe.
- Una voz reservada evita que los gritos se superpongan al navegar rápidamente.
- Los disparos e impactos tienen menor ganancia para dar prioridad a la música.
- Los controles General, Música y Efectos siguen funcionando de manera independiente.
