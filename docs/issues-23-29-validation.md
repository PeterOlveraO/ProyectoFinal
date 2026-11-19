# Validación de las issues 23–29

## Alcance y ramas

Base: `8e8c03d365283f0c227c14e57ca36796e4094772` de `main`.

| Issues | Rama | Responsabilidad |
| --- | --- | --- |
| #23, #27, #28 | `feat/issues-23-27-28-tycoon-economy` | Disparo único, construcción de vendedores y economía |
| #24 | `feat/issue-24-flora` | Flora, colisiones y rutas |
| #25, #29 | `feat/issues-25-29-boss-difficulty` | Jefes y dificultad por ronda |
| #26 | `feat/issue-26-compact-menus` | Menús compactos con las imágenes proporcionadas |

Los cuatro subagentes de implementación usan GPT-6.1 Sol. La integración y
revisión se realizan en `integration/issues-23-29`.

## Comprobaciones requeridas

- Pruebas EditMode y PlayMode completas, incluidas las nuevas regresiones.
- Compras atómicas, bloqueo previo a construcción y reinicio por partida.
- Un jefe por ronda especial, sin refuerzos, con avance normal al derrotarlo.
- Progresión de vida, daño y velocidad en rondas iniciales y avanzadas.
- Flora sin edificios decorativos y rutas de acceso a vendedores.
- Menús que conservan el fondo visible, controles táctiles y pausa.
- Workflow de integración aprobado antes de actualizar `main`.
- Nueva ejecución del workflow sobre el commit final de `main`.
- Descarga del IPA de esa ejecución y validación local de ZIP/CRC,
  `Payload/*.app`, identificador de aplicación y ejecutable Mach-O arm64.

## Validación local observada

- Unity `6000.5.11f1`, escena `Assets/Scenes/World.unity`.
- Compilación externa del runtime completo y 14 ensamblados de tests aprobada.
- En el Editor: **138/138 EditMode y 48/48 PlayMode**, sin fallos ni omitidos.
- **28/28** tests Python de validación de resultados e IPA aprobados.
- El intento de abrir un Editor aislado mediante Unity CLI falló por licencia.
  La revisión jugable se realizó mediante MCP en el Editor ya abierto, después
  de aplicar la integración y conservar una copia del trabajo local previo.
- La revisión usa un archivo temporal de progresión y puntos de prueba; no
  representa una partida económica completa desde cero.

### Menús

Principal, pausa y EV comprobados visualmente con el mapa visible. Pausa y EV
mantienen `Time.timeScale = 0`; volver a la partida reanuda el juego. Las pruebas
de layout cubren seis tamaños, safe area, textos y áreas táctiles mínimas.

- [Principal](evidence/issues23-29-main-compact.png)
- [Pausa](evidence/issues23-29-pause-compact.png)
- [EV](evidence/issues23-29-ev-compact.png)

### Zona tycoon y compras

La escena generó las **9 plataformas**: cinco máquinas de perks y cuatro
arsenales. Desde el spawn se recorrieron físicamente las nueve rutas con el
`CharacterController`, en pasos de 0,12 unidades y gravedad. Solo se recolocó
al jugador al principio de cada ruta; todas llegaron a menos de 0,20 unidades
del destino en 70–76 pasos.

La compra de Fuerza antes de construir fue rechazada. Mediante el botón real
del HUD: 2000 → 1900 puntos al construir (sin perk), y 1900 → 1800 al comprar.
El vendedor muestra el sprite `maquina` proporcionado.

Después, construir/comprar Ráfaga dejó 1550 puntos; construir/comprar Abanico,
1200; volver a comprar Ráfaga, 1060. Con solo 139 puntos, otra compra de Ráfaga
fue rechazada sin cobro ni cambio del disparo activo. El equipamiento gratuito
también fue rechazado.

- [Máquina construida y perk adquirido](evidence/issues23-29-tycoon-machine.png)
- [Cálculos de economía](tycoon-economy.md)

### Flora

La escena real generó **104 elementos de flora**, frente a los 91 elementos
totales (incluidos edificios) registrados en la validación anterior con el mismo
mapa. El catálogo ya no coloca edificios decorativos.

### Jefes y ritmo

El jefe de la ronda 5 fue **Mew**, único enemigo, con 216 HP, 39 de daño/s y
velocidad 3,12. La revisión de balance detectó la eliminación instantánea por
el poder especial: se corrigió para quitar 25% de HP máximo por activación
y conservar al jefe durante contacto invencible.

En combate automatizado dentro de la escena real, Jolteon F con Ráfaga y Fuerza
derrotó al jefe 5 en **21,93 segundos**, moviéndose en círculo con el controlador
y disparando proyectiles reales. Conservó 99/99 HP y no usó el poder especial.
Es una comprobación reproducible de combate, no una prueba humana en iPhone.

El jefe 20 tenía **576 HP**. Con dos EV en cada atributo, Ráfaga y Fuerza
(daño 7, velocidad 6), el mismo recorrido automatizado lo derrotó en
**41,71 segundos**, conservando 117/117 HP y sin usar el poder especial.
Tras esa victoria se ejecutó la actualización del gestor: `Fighting → Clearing
→ Preparing (ronda 21) → Fighting`. La ronda 21 volvió a ser normal, con
46 enemigos previstos y el primero aparecido. Para comprobar la transición
sin esperar, se agotaron los temporizadores de preparación y limpieza.

- [Jefe en partida](evidence/issues23-29-boss-mew.png)
- [Fórmulas y análisis de dificultad](rounds-boss-difficulty.md)

### Cierre de la revisión

Se salió de PlayMode con el gestor en `Idle`, evitando liquidar la partida de
prueba. Se restauraron los contadores originales (15 enemigos, una ronda y
29 monedas); el contador de derrotas no cambió al cerrar. La progresión usada
en combate permaneció en su archivo temporal. Consola final: sin errores ni
advertencias.

Se cotejaron los hashes de los 1088 registros del trabajo local previo:
solo difieren los tres `.meta` de las imágenes aportadas, cuyos ajustes de
importación forman parte de la implementación. Los originales están respaldados.

## CI e IPA

En curso. La aprobación y los artefactos se registran en el PR de integración
#34 y en los PRs #30–#33; el resultado final de `main` debe corresponder al
commit exacto que figure en su workflow.

El commit de evidencia posterior a `f59f67f` modifica únicamente este documento
y cinco capturas. El workflow de integración valida `f59f67f`; el workflow final
de `main` también incluirá la evidencia y comprobará su SHA exacto.
