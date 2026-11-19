# Run shops — issues #23 / #27 / #28

`ShotLoadout` holds exactly one active type, not an inventory. `TryEquip` and
`RoundManager.EquipShot` reject all calls. Every successful `BuyShot` charges,
including the current or a previously purchased type. Failed authorization,
insufficient funds or an unbuilt arsenal leave points and active type unchanged.
Offer 0/1/2/3 means Burst/Fan/Piercing/Basic. Basic starts equipped without charge;
returning to it later is a paid purchase at its own arsenal.

## Tycoon

Nine walk-through platforms form one compact hub around spawn, nominal radius 9,
maximum radius 12 world units. Reserve **15 units around SpawnPosition** against
flora. `WorldShopPlacement.TycoonReservationRadius`, `DrinkStations.Positions` and
`ShotStations.Positions` expose the contract. Placement checks flat dry terrain,
static collisions and connectivity from spawn. It never falls back to dispersed
vendors. Drinks initialize at execution order 0; arsenals at 200. Signs advertise
structure and product costs before building. No vendor collision can block paths.

First click **CONSTRUIR** pays only the structure and reveals the vendor.
A separate click buys the product. Server-side (gameplay API) checks enforce
construction, alive player, active unpaused run, horizontal/vertical proximity and
funds; hidden HUD is not the authorization mechanism. Perks are once per run;
shots can be repurchased. All structures/perks/shots reset on stop/retry/end.

Perk machines use `Assets/Imagenes/maquina.png`, imported as a single sprite.
`Resources/TycoonMachine.asset` references that sprite through `TycoonMachineArt`
so builds include it without Editor API calls or duplicate images.

## Coordinator verification (Unity not run in worker checkout)

1. Check all nine platforms/signs in the spawn clearing, dry and walkable.
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
See `Documentation/TycoonEconomy.md` for balance calculations.

CI gate counts after these issues: `Game.Rounds.Shop.Tests` **35**,
`Game.ShotShop.EditMode.Tests` **11**, `Game.ShotShop.PlayMode.Tests` **24**:
**70 total / 37 added** over the base branch. Counts include parameterized cases.
