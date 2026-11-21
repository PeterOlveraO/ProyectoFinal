# Gacha especial: 13 Pokémon y ruleta visual

Diez especies adicionales, extraídas de la plantilla del proyecto
`Assets/Imagenes/Pokemones.png`:

| Categoría | Nuevos Pokémon |
| --- | --- |
| Pseudolegendarios | Tyranitar, Salamence, Metagross |
| Legendarios | Mewtwo, Lugia, Ho-Oh, Rayquaza |
| Míticos | Celebi, Jirachi, Darkrai |

Se suman a Dragonite, Garchomp y Mew. El catálogo jugable llega a **28 especies**.
El gacha especial conserva su precio de **125 monedas** y probabilidades de clase
**F 40% / A 45% / S 15%**. Las 13 especies comparten la misma probabilidad de selección.

## Ruleta

Ambas ofertas muestran sus candidatos mediante sprites, sin listas de nombres.
La banda se desplaza suavemente cada 1.6 segundos y destaca el Pokémon central.
Usa tiempo no escalado, por lo que continúa con la partida pausada.
Reutiliza siete casillas y los sprites cargados; no crea objetos cada fotograma.
Es una vista previa del catálogo: las tiradas siguen pasando por `ProgressionService`.

![Ruleta real en funcionamiento](evidence/gacha-special/ruleta.gif)

[Captura panorámica](evidence/gacha-special/gacha-16x9.png) ·
[Captura 4:3](evidence/gacha-special/gacha-4x3.png) ·
[Tyranitar obtenido](evidence/gacha-special/tyranitar-obtenido-4x3.png)

![Diez incorporaciones extraídas de la plantilla](evidence/gacha-special/nuevos-pokemon.png)

## Recursos

- Diez retratos y 60 vistas de movimiento, verificadas píxel a píxel contra la plantilla.
- Las especies grandes conservan celdas de 64×64 en retrato y animación.
- Fondo transparente, filtro Point, pivote centrado y 32 PPU.
- Los nuevos retratos viven en `PokemonPortraits`, fuera del pool visual de enemigos normales.
- Diez gritos originales adicionales con procedencia y hashes en `Tools/Audio/sources-pokemon.json`.
- Apariencias elementales acordes: roca, dragón, acero, psíquico, fuego y sombra.

La extracción reproducible está documentada en [Tools/PokemonAnimations](../Tools/PokemonAnimations/README.md).

## Validación

- **144/144 EditMode**, incluido catálogo, recursos de las 13 opciones y guardados.
- **84/84 PlayMode**, incluida animación con `Time.timeScale = 0`, saldo intacto y
  reapertura sin duplicar casillas.
- Verificador de procedencia: **190 fuentes y 1140 fotogramas**.
- Captura real de 24 segundos: los **13 candidatos** pasaron por el centro con tiempo
  pausado, sin gastar monedas ni cambiar el inventario; cero desbordamientos TMP.
- En un guardado temporal, **45 tiradas** alcanzaron las 13 especies, cobraron 125 cada
  una y permitieron equiparlas y liberarlas. [Registro](evidence/gacha-special/draw-audit.txt).
- La rotación existente de jefes incluye automáticamente las nuevas especies
  legendarias/míticas, con los mismos valores de combate y recompensas de jefe.

Jobs locales: `e4a6e9e9a8a54d258aa311f8c483e593` (EditMode) y
`07addcd1f95443489751e985f21ea86b` (PlayMode).
