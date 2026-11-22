# Run shops — issues #23 / #27 / #28

`ShotLoadout` holds exactly one active type, not an inventory. `TryEquip` and
`RoundManager.EquipShot` reject all calls. Every successful `BuyShot` charges,
including the current or a previously purchased type. Failed authorization,
insufficient funds or an unbuilt arsenal leave points and active type unchanged.
Offer 0/1/2/3/4/5/6 means Burst/Fan/Piercing/Basic/Precision/Boomerang/Explosive. Basic starts equipped without charge;
returning to it later is a paid purchase at its own arsenal.

## Tycoon

Fifteen walk-through platforms occupy nine purchase regions: Z1 Basic/Power/Vitality,
Z2 Sprint/Burst, Z3 Ballistics/Fan, Z4 Armor/Piercing, Z5 RapidFire/Recovery,
Z6 Precision, Z7 Explosive, Z8 Boomerang, Z9 Reserve. Purchase regions are separate
from the three terrain biomes. Reserve flora with
`WorldShopPlacement.IsReservedForShop(terrain, point, decorationRadius)`.
`GetAnchor(terrain, shot, offer)` and `GetAnchorOffset(shot, offer)` expose the
shared deterministic anchors. Placement searches only three units from each anchor,
checking flat dry ground and a local approach. The terrain prepares these clearings.
`enableWorldZones == false` retains a fifteen-pad central hub within 12 world units,
reserved out to `TycoonReservationRadius` (15). The generic helper also accepts smaller requested counts.
Drinks initialize at execution order 0; arsenals at 200. Signs advertise
structure and product costs before building. No vendor collision can block paths.

First click **CONSTRUIR** pays only the structure and reveals the vendor.
A separate click buys the product. Server-side (gameplay API) checks enforce
construction, alive player, active unpaused run, horizontal/vertical proximity and
funds; hidden HUD is not the authorization mechanism. Perks are once per run;
shots can be repurchased. All structures/perks/shots reset on stop/retry/end.
`WorldShopPlacement.IsZoneAccessible` is the run-controller access hook for both
offers and transactions; bind/reset it with the world's lifetime.

Precision: 2D, 1.4 s, range 26, speed 22, one target.
Boomerang: D, 1.4 s, 9 units each leg, two distinct targets per leg, fixed return axis.
Explosive: D direct plus rounded 0.75D to at most three additional distinct targets
within radius 2, with height/obstacle checks. Its interval is 1.4 s and range 14.
Ballistics multiplies every shot's speed by 1.25 and range by 1.2 (both boomerang legs).
Damage modifiers are captured once per pattern. `AttackProjectile.CanTravelSegment`
guards flight and explosion segments when bound to the world's projectile traversal API.

Perk machines use `Assets/Imagenes/maquina.png`, imported as a single sprite.
`Resources/TycoonMachine.asset` references that sprite through `TycoonMachineArt`
so builds include it without Editor API calls or duplicate images.

## Coordinator verification (Unity not run in worker checkout)

1. Check all fifteen platforms/signs in their assigned zones, dry and walkable.
2. Try buying before building via API; no points lost. Build, verify no perk/shot
   was granted, buy separately, verify exact combined deduction.
3. Buy Burst → Fan → Burst; all three purchases charge. Move away: no free
   switching or selector exists. Buy Basic at its arsenal to return to Basic.
4. Pause/death/out-of-range/insufficient points reject construction and products.
5. Retry/abandon/defeat removes structures and effects. Confirm machine art in
   an actual Player build as well as Editor.
6. Mobile/wide safe areas: top label only (no selector); separate bottom build/
   purchase prompts do not overlap sticks. Manual shot axes/patterns unchanged.

Tests: `Game.Rounds.Shop.Tests` (placement, construction, machine resource, perk
transactions); `Game.ShotShop.EditMode.Tests` (single active shot, charges, atomic
failures, patterns); `Game.ShotShop.PlayMode.Tests` (runtime authorization,
construction gating, repeated charges, burst cancellation and projectile effects).
See `docs/tycoon-economy.md` for balance calculations.

Expanded tests cover all seven prices, construction/purchase separation, locked-zone
hooks, new projectile damage/caps/return axis, blast height/obstacles, captured damage
buffs, Ballistics on all patterns, and the fifteen-vendor central fallback.
The coordinator runs the Unity suites after integrating the shared world APIs.
