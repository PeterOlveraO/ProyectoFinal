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
  Los bonos de ronda y contadores de rondas no cambian.
- El jugador ya no dispone de poder especial, invencibilidad activable ni daño
  global. El contacto de normales y jefes solo daña al jugador, nunca al enemigo.
  Se conserva la protección temporal de reaparición, que bloquea daño sin matar
  enemigos. `KillAll()` sigue siendo eliminación incondicional para gestión
  interna. No se cambian StopRun, cleanup, cadencia ni tecla R.
- No se modifican RunHud, precios/EV, APIs de disparos ni init/reset de tiendas.

## Revisión económica de integración (sin cambiar precios en esta rama)

Con los precios nuevos comunicados por coordinación:

- Disparo Ráfaga: estructura 110 + producto 140 = **250**.
- Fuerza: estructura 100 + producto 100 = **200**.
- Dos EV por atributo: 80 + 120 = 200; cuatro atributos = **800**.
- Ráfaga + Fuerza sin EV = **450**; combo con dos EV en los cuatro stats = **1250**.
- Puntos acumulados garantizados, sin compras ni drops, tras completar rondas
  1–7: **110, 250, 420, 620, 850, 1110, 1400**. Ronda 5 conserva el
  presupuesto de sus 14 bajas normales reemplazadas.
- El combo sin EV es asequible durante ronda 4; el completo durante ronda 7
  (desde 1110, tras 14 bajas normales de esa ronda). Ya no es correcto asumir
  el combo completo antes de ronda 4 o del primer jefe.
- Modelo de jefe: F neutral, 100% impactos, apuntado sostenido,
  intervalo 1 s; tiempos desde la primera bala, sin viaje ni obstáculos:

| Ronda | Ráfaga + Fuerza, sin EV (Jolteon / Charizard / Blastoise) | Más 2 EV por stat |
| --- | --- | --- |
| 5 | 14.12 / 11.24 / 11.24 s | 10 / 8.24 / 8.24 s (contrafactual: aún no asequible sin drops) |
| 10 | 22.12 / 18.12 / 18.12 s | 15.24 / 13.24 / 13.24 s |
| 20 | 38.12 / 31.24 / 31.24 s | 27.12 / 23.24 / 23.24 s |

Es cálculo de modelo, no playtesting ni ejecución del loop Unity.

## Validación

- Retirada del poder: se sustituyen sus dos regresiones por daño/contacto y
  protección de spawn; se conservan los 8 casos de `Game.Runtime.PlayMode.Tests`
  y los 4 de `Game.Rounds.PlayMode.Tests`, sin reducir mínimos del gate CI.
  Compilación externa de `Game.Runtime` y las 14 assemblies de tests correcta
  con `compile-proyectofinal.py --all-tests` en el worktree de retirada.
  Solo compilación Roslyn contra DLLs instaladas/cacheadas: no se ejecutaron
  NUnit, PlayMode, importación ni pruebas visuales, y no se usó el Editor compartido.
- Nuevas assemblies: `Game.Rounds.EditMode.Tests` (14 casos) y
  `Game.Rounds.PlayMode.Tests` (4 casos: conserva los 3 originales y añade 1).
- EditMode: frecuencia, crecimiento temprano, límites extremos, multiplicadores
  de jefe, selección real del catálogo y disponibilidad de sprites.
- PlayMode: aparición única/no refuerzos, daño letal y crédito único, compensación
  económica, transición a ronda normal, limpieza Stop y reinicio de presupuesto,
   reinicialización de stats y sustitución de visual en objeto reutilizado;
   regresión de contacto normal/jefe: daño proporcional a deltaTime, enemigos
   ilesos y sin recompensa por contacto, protección de spawn y daño tras expirar.
- Los fixtures requieren escena vacía, restauran Random/GameFlow y los contadores
  PlayerPrefs que usan; no crean/modifican el save de progresión.
- No se ejecutó Unity, ni se abrió/mutó el Editor compartido. El coordinador debe
  ejecutar ambas assemblies y suites existentes en Unity/CI.
- Validación histórica previa a retirar el poder: compilación externa ejecutada con
  `python3 /tmp/opencode/compile-proyectofinal.py --project /tmp/opencode/proyectofinal-rounds --all-tests`:
  Game.Runtime y las 13 assemblies de tests compilan sin errores (warnings
  anteriores de campos sin asignar y APIs obsoletas). Usa Roslyn y DLLs del Editor
  instalado/cacheadas: no ejecuta NUnit, no valida importación ni escena, ni
  sustituye Unity CI. La consola del Editor original se leyó sin mutaciones y
  devolvió 0 errores antes/después; no es prueba de compilación del worktree.
- Comprobaciones estáticas ejecutadas: `git diff --check`; parseo JSON de todos
  los asmdef; presencia de metas nuevos y GUIDs únicos; catálogo actual Mew y
  archivo de sprite existente; recuento actualizado de 14 + 4 casos declarados.
- No se realizaron pruebas de juego: comprobar ritmo real inicial (1–4), jefe 5,
  rondas 10/20 y tope avanzado antes de cerrar el criterio de ajuste de #29.
  Estos valores son una propuesta comprobable, no un balance validado jugando.
