# Issue 10 — run shot shop

## Contract and behaviour

- `RoundManager.Shots` is a run-only `ShotLoadout`; `EquipShot(ShotType)` equips an
  owned type anywhere for free. `BuyShot(int offer)` validates active gameplay,
  living player, proximity and run points. Offer indices 0/1/2 mean burst/fan/piercing.
  No coins, progression or persistent ownership are changed.
- `ShotStations` starts at execution order **200**, after drinks at **0**. It calls
  `WorldShopPlacement.FindPositions(terrain, 3, 104729, drinks.Positions)`.
  Positions are ground coordinates and read-only. Placement failures log an error,
  with no partial shop. Requires issue9's helper and `DrinkStations.Positions`.
- Basic: free, one bullet. Burst: **60** points, three full-damage pulses **0.12 s**
  apart. Fan: **90**, three full-damage bullets at **−16° / 0° / +16°**. Piercing:
  **120**, one bullet, maximum **three distinct living enemies**, nearest first.
- All axes come from the existing manual input (camera-relative attack joystick,
  desktop-only mouse fallback retained from issue7). Patterns never select an
  enemy direction. Burst snapshots axis and damage at its first pulse, but each
  pulse originates at the player's current position. Holding aim is required;
  releasing, pausing, dying, disabling, resetting or switching cancels pending
  pulses. No coroutine, `Invoke`, late callback or catch-up volley.
- Cooldown is measured from the first pulse. RapidFire still multiplies it by
  0.7; Power still adds 3 to the Pokémon combat damage, for every pattern.
  Projectiles freeze during pause. End/stop/restart clears in-flight bullets and
  pending bursts immediately, and resets ownership/equipped type to Basic.
- Swept horizontal collision prevents mobile frame skips; bounded arrays of three
  targets and at most three passes over the enemy list avoid per-frame allocations.
  Range/speed remain unchanged and patterns spawn at most three bullets per trigger.

## UI and manual verification

The existing safe-area uGUI/TMP HUD is built by code; no scene or prefab edits.
World stands show type and price before purchase. The top-center loadout shows
the equipped type and four 150×82 buttons; owned types equip free, locked ones
cannot be bought remotely. A nearby offer shows name, price, buy+equip or free
equip status. Shot offer occupies y=155..265 and drink y=35..135 (650 wide,
centered, at the existing 1920×1080 reference). They do not overlap each other or
the visible pinned joystick circles (centers x≈480/1440, radii 110/120 at 16:9).
Noninteractive labels/backgrounds do not intercept joystick rays.

Coordinator must verify in the merged Editor (not controlled from this checkout):

1. 16:9 and wide landscape with mobile safe area: labels readable, no clipping,
   no stick/power-button overlap, drag each stick while tapping an offer.
2. Earn 59/60, 89/90, 119/120 points; check insufficient funds, exact deduction,
   automatic equip, and no second charge. Global coins must stay unchanged.
3. Acquire two types, move away, switch both and Basic using the top panel.
4. Point beside/away from an enemy: fan stays centered on the stick, burst keeps
   its captured axis; piercing damages each of three enemies only once.
5. Pause during a burst: no further pulses; bullets freeze. No purchases or
   equips through paused/hidden HUD. Resume, defeat, abandon, retry and return
   to menu: no old pulses/projectiles or acquired loadout survive a run.

## Tests

- `Game.ShotShop.EditMode.Tests`: free Basic, exact prices, failed transactions,
  invalid types, no repeated charge, ownership reset and signed fan angles.
- `Game.ShotShop.PlayMode.Tests`: empty test scene; runtime authorization,
  proximity, points, owned switching, three captured pulses, all cancellation
  paths, CombatStats/Power/RapidFire, fixed fan projectiles, swept nearest-first
  piercing, no duplicate damage, Basic first-hit, pause freeze and run cleanup.
- References the coordinator's `Game.Runtime` assembly (CI change). Integrate
  issue9 and CI assembly before running Unity Test Runner. Direct compilation
  can substitute issue9's source files read-only without changing this branch.
