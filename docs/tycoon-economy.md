# Economía de partida — #28

## Supuestos y fórmula

No cambia ninguna recompensa ni el dinero global. El modelo conservador usa los
valores por defecto de RoundManager: `N(r)=min(48,6+2(r−1))` enemigos equivalentes,
10 puntos por baja y `50+10(r−1)` por completar ronda.
**Ingreso(r) = 10×N(r)+50+10(r−1)**.
Bonus aleatorios no se presupuestan: adelantan compras pero no son necesarios.
Los jefes de rondas múltiplos de **5** sustituyen enemigos y deben otorgar los
mismos puntos por baja que los sustituidos en conjunto (contrato acordado con
el agente de rondas). Por eso ni la tabla ni los precios dependen de vencer un
jefe por una recompensa inflada. Cambiar inspector/recompensas requiere recalcular.

| Ronda | Equivalentes normales | Bajas | Completar | Ingreso | Acumulado sin gastar |
|---:|---:|---:|---:|---:|---:|
| 1 | 6 | 60 | 50 | 110 | 110 |
| 2 | 8 | 80 | 60 | 140 | 250 |
| 3 | 10 | 100 | 70 | 170 | 420 |
| 4 | 12 | 120 | 80 | 200 | 620 |
| 5 (jefe) | 14 | 140 | 90 | 230 | 850 |
| 10 (jefe) | 24 | 240 | 140 | 380 | 2450 |
| 15 (jefe) | 34 | 340 | 190 | 530 | 4800 |
| 20 (jefe) | 44 | 440 | 240 | 680 | 7900 |
| 25 (jefe) | 48 (tope) | 480 | 290 | 770 | 11630 |

## Precios anteriores → nuevos

Las estructuras no existían antes (coste 0). La ronda indicada es la primera
**completada**, ahorrando desde cero sin otras compras. Construir no entrega el
producto: se paga por separado. Productos y estructuras aumentan el coste real.

| Compra | Producto antes | Producto nuevo | Estructura nueva | Combinado | Ronda antes → nueva |
|---|---:|---:|---:|---:|---:|
| Fuerza | 30 | 100 | 100 | 200 | 1 → 2 |
| Vitalidad | 45 | 140 | 120 | 260 | 1 → 3 |
| Sprint | 35 | 90 | 100 | 190 | 1 → 2 |
| Escudo | 45 | 160 | 140 | 300 | 1 → 3 |
| Cadencia (perk RÁFAGA) | 60 | 180 | 160 | 340 | 1 → 3 |
| Disparo Ráfaga | 60 | 140 | 110 | 250 | 1 → 2 |
| Disparo Abanico | 90 | 200 | 150 | 350 | 1 → 3 |
| Disparo Perforante | 120 | 280 | 200 | 480 | 2 → 4 |
| Volver al Básico | gratis | 80 | 80 | 160 | 0 → 2 |
| Primer EV | 20 | 80 | — | 80 | 1 → 1 |
| Décimo EV (solo ese nivel) | 110 | 440 | — | 440 | 1 → 4 |

EV nivel cero `80+40×nivel` (antes `20+10×nivel`), tope 10 por stat.
Perks siguen siendo una vez por tipo/partida. Cada compra de disparo vuelve a
cobrar el producto completo, incluso si era el activo o se compró antes;
la estructura se paga una vez por partida. No hay inventario para saltar el coste.

## Recorridos reproducibles

- **Inicial**: ronda 1 aporta 110, permite construir Ráfaga (110); quedan 0 y
  sigue Básico. Ronda 2 aporta 140: compra/equipa Ráfaga (140). Alternativa:
  ahorrar 190 para Sprint (estructura+perk) durante ronda 2.
- **Antes del primer jefe**: Perforante completo (480) cabe en las cuatro
  primeras rondas (620), con 140 restantes; no necesita premio especial del jefe.
- **Ronda 10**: 2450 no compra todos los nueve edificios+productos (2530);
  el conjunto requiere ronda 11. No incluye EV ni recompras.
- **Avanzada**: los 10 EV de un stat cuestan 2600. Todos los vendedores/productos
  + ese stat cuestan 5130: ronda 15 aún no basta (4800), ronda 16 sí (5360).
  Conservar Perforante, pasar a Ráfaga y volver a Perforante resta 700 adicionales
  solo de productos. Los cuatro stats al máximo exigen 10400, no 2600.

## Pruebas y alcance

`TycoonEconomyTests` fija 9 casos de ingresos (incluidos jefes y tope), 5 de perks,
4 de disparos y 2 recorridos de presupuesto: **20 casos** en
`Game.Rounds.Shop.Tests`. Las otras suites prueban cobro real, fallos atómicos,
construcción separada, prohibición antes de construir y reset.
No se ejecutó Unity en este worktree por coordinación/licencia del Editor; quedan
pendientes CI y pruebas jugables del coordinador (dificultad, tiempo de viaje,
legibilidad móvil y supervivencia frente al jefe). Son pruebas deterministas de
presupuesto, no una afirmación de playtest humano.
