# Issues #25 / #29: rondas y dificultad

## Decisiones

- Cada 5 rondas (5, 10, 15, …) hay exactamente un jefe, sin refuerzos.
- Se filtra el catálogo existente por `Legendary` o `Mythical`, excluyendo
  `PseudoLegendary`, y se rota por ID ordenado. Actualmente el único candidato es
  **Mew**. No se añaden especies, sprites ni cambios a la colección del jugador.
- Se carga el catálogo de Resources directamente: seleccionar un jefe no necesita
  inicializar la colección persistente ni usar el Pokémon equipado.
- La fábrica sigue creando objetos nuevos y la muerte usa Destroy; no existe un
  pool de enemigos en este flujo. Aun así `InitializeRound` y el inicializador de
  zonas restablecen vida, daño, marca de jefe, flash y manager de zona.
  Reasignar un visual sustituye/desactiva el billboard anterior.

## Valores (normal)

Para `n = max(0, ronda - 1)`:

- Velocidad: `min(4.6, 2.8 + 0.08 n)` (antes 2.2 inicial: +27.3%).
- Vida: `min(200, 10 + 2 n)`.
- Daño de contacto/segundo: `min(35, 10 + 0.75 n)`.
- Jefe: misma velocidad, **12× vida**, **3× daño** respecto al normal comparable.

| Ronda | Velocidad | Vida normal | Daño/s normal | Vida jefe | Daño/s jefe |
| --- | --- | --- | --- | --- | --- |
| 1 | 2.8 | 10 | 10 | — | — |
| 5 | 3.12 | 18 | 13 | 216 | 39 |
| 10 | 3.52 | 28 | 16.75 | 336 | 50.25 |
| 20 | 4.32 | 48 | 24.25 | 576 | 72.75 |
| 100 | 4.6 | 200 | 35 | 2400 | 105 |

Los límites protegen rondas extremas y permiten seguir escapando. Por diseño el
crecimiento se detiene al alcanzar cada límite. El escalado ya no depende del
antiguo salto de vida por tier: tier se conserva para compatibilidad de zona.
Los tres campos viejos serializados de velocidad de RoundManager dejan de usarse;
esto evita que World.unity conserve 2.2 pese a cambiar el valor por defecto.
`RoundDifficulty` es la fuente única del balance de rondas.

## Contrato economía / HUD

- API pública `RoundManager.IsBossRound : bool`; true en preparación/combate/
  cierre de ronda de jefe, false en Idle/GameOver.
- `RoundManager.BossName : string`; null fuera de esas fases.
- Ambos se actualizan antes de `Changed` al preparar cada ronda.
- Métricas públicas de enemigo: `EnemyTriangle.Speed : float`,
  `ContactDamage : float` (daño/s), `IsBoss : bool`, además de `Health` y
  `MaxHealth` existentes. Son las mismas cifras usadas por movimiento y contacto.
- Helper público `RoundDifficulty.Stats(int round, bool boss = false)` devuelve
  `EnemyRoundStats` con campos `health : int`, `speed : float`,
  `contactDamage : float`; `IsBoss(int round)` consulta la frecuencia.
- `EnemyTriangle.InitializeRound(int zone, EnemyRoundStats stats, bool boss)`
  configura/reinicializa por completo el combate del enemigo. No altera el
  sprite: los llamadores que reutilicen objetos deben reasignarlo con
  `PokemonEnemyVisual.Attach`, que ya reemplaza el anterior.
- El jefe entrega puntos y monedas de baja por el **cupo normal reemplazado**:
  `min(maxEnemiesPerRound, baseEnemies + enemiesPerRound * (round - 1))`,
  mínimo 1. No requiere cambios a precios ni tiendas.
- Ejemplo ronda 5 con defaults: cupo 14, 140 puntos + 14 monedas por jefe,
  más bono habitual de ronda (90 puntos + 35 monedas).
- Drop adicional sigue al 30%, pero entrega `pointsPerKill × cupo`. Conserva
  el valor esperado de los drops normales, con mayor varianza. `BonusDrops`
  cuenta un solo evento real; `BonusPointsPerDrop` conserva su semántica base.
- Las estadísticas de enemigos derrotados cuentan **una baja**, no 14.
  Los bonos de ronda y contadores de rondas no cambian. La muerte por poder
  especial conserva su comportamiento existente (puede matar al jefe).
- No se modifican RunHud, precios/EV, APIs de disparos ni init/reset de tiendas.

## Validación

- Nuevas assemblies: `Game.Rounds.EditMode.Tests` (14 casos) y
  `Game.Rounds.PlayMode.Tests` (3 casos).
- EditMode: frecuencia, crecimiento temprano, límites extremos, multiplicadores
  de jefe, selección real del catálogo y disponibilidad de sprites.
- PlayMode: aparición única/no refuerzos, daño letal y crédito único, compensación
  económica, transición a ronda normal, limpieza Stop y reinicio de presupuesto,
  reinicialización de stats y sustitución de visual en objeto reutilizado.
- Los fixtures requieren escena vacía, restauran Random/GameFlow y los contadores
  PlayerPrefs que usan; no crean/modifican el save de progresión.
- No se ejecutó Unity, ni se abrió/mutó el Editor compartido. El coordinador debe
  ejecutar ambas assemblies y suites existentes en Unity/CI.
- Comprobaciones estáticas ejecutadas: `git diff --check`; parseo JSON de todos
  los asmdef; presencia de metas nuevos y GUIDs únicos; catálogo actual Mew y
  archivo de sprite existente; recuento de 14 + 3 casos declarados.
- No se realizaron pruebas de juego: comprobar ritmo real inicial (1–4), jefe 5,
  rondas 10/20 y tope avanzado antes de cerrar el criterio de ajuste de #29.
  Estos valores son una propuesta comprobable, no un balance validado jugando.
