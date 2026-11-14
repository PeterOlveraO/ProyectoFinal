# Catálogo de los 50 Pokémon seleccionados

Este documento recoge la distribución de Pokémon acordada para el proyecto.
Es una referencia de la selección; no una comprobación del estado actual de su implementación en Unity.

## Resumen

| Grupo | Cantidad | Función prevista |
|---|---:|---|
| Jugables | 18 | Personajes que el jugador puede seleccionar. |
| Enemigos | 23 | Especies utilizadas para los enemigos normales. |
| Jefes | 9 | Legendarios y míticos para los encuentros con jefes. |
| **Total** | **50** | Especies distintas, sin repetir entre grupos. |

## Pokémon jugables

Son los personajes seleccionados para la colección del jugador. Jolteon fue la selección inicial original; actualmente se puede elegir gratuitamente una vez entre Jolteon, Charizard y Blastoise.

1. Jolteon.
2. Charizard.
3. Garchomp.
4. Vaporeon.
5. Flareon.
6. Snorlax.
7. Blastoise.
8. Dragonite.
9. Lucario.
10. Venusaur.
11. Gengar.
12. Gyarados.
13. Lapras.
14. Ditto.
15. Aerodactyl.
16. Kangaskhan.
17. Tauros.
18. Mew.

## Pokémon enemigos

Son las especies seleccionadas para los enemigos normales, separadas del catálogo de personajes jugables.

1. Nidoqueen.
2. Nidoking.
3. Butterfree.
4. Clefable.
5. Ninetales.
6. Beedrill.
7. Wigglytuff.
8. Vileplume.
9. Pidgeot.
10. Parasect.
11. Raticate.
12. Venomoth.
13. Fearow.
14. Golduck.
15. Arbok.
16. Raichu.
17. Golem.
18. Sandslash.
19. Arcanine.
20. Alakazam.
21. Pinsir.
22. Heracross.
23. Skarmory.

## Pokémon jefes

Son las especies seleccionadas para los encuentros especiales con jefes: siete legendarios y dos míticos. Mew es jugable; Mewtwo permanece exclusivamente como jefe.

| Pokémon | Clasificación |
|---|---|
| Articuno | Legendario |
| Zapdos | Legendario |
| Moltres | Legendario |
| Mewtwo | Legendario |
| Celebi | Mítico |
| Jirachi | Mítico |
| Raikou | Legendario |
| Entei | Legendario |
| Suicune | Legendario |

## Criterios de selección

- Se combinan especies comunes, populares, legendarias y míticas.
- Se mantienen formas finales o especies sin evolución.
- Cada especie pertenece a un solo grupo.
- La preparación prevista por especie es de seis sprites: dos de frente, dos de espalda y dos de perfil; 300 imágenes en total.
- La clasificación por grupos no implica habilidades exclusivas por especie; las estadísticas base sí varían por especie.

## Reglas actuales de progresión

- Inventario persistente de hasta 10 instancias, con equipamiento individual. No se puede liberar la última; liberar la equipada selecciona otra disponible.
- Inicial gratuito de rareza F/A/S con probabilidades 95/4/1 %. La elección solo se permite una vez.
- Gacha básico: 50 monedas, 15 especies comunes, rarezas F/A/S 75/20/5 %. Gacha premium: 125 monedas, Dragonite, Garchomp y Mew, rarezas 40/45/15 %. Dentro de cada máquina las especies son equiprobables.
- Los duplicados son instancias independientes; no hay mejoras por duplicados. Los IV son 0/15/31 por rareza F/A/S y la naturaleza se sortea entre 25 opciones.
- El saldo y el inventario se guardan juntos de forma atómica. Una tirada rechazada por saldo insuficiente o inventario lleno no debe cobrar.
- Las estadísticas combinan especie, IV y naturaleza. Al iniciar una partida se captura el Pokémon equipado; los EV comprados con puntos son temporales y se reinician al terminar o abandonar.
- Los controles existentes de movimiento, ataque, poder especial y pausa se conservan; el árbol EV se abre desde la partida o pausa.
