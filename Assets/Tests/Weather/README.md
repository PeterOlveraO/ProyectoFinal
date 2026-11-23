# Weather integration and verification

Unity 6000.5.11f1 / URP 17.5. New runtime code belongs to `Game.Runtime`.

```csharp
// GameMenuController.Awake:
Game.Weather.WeatherController.EnsureExists(gameObject);

// ShowMainMenu (also covers abandoning a run), never Pause/Resume/StartRound:
if (Game.Weather.WeatherController.Instance != null)
    Game.Weather.WeatherController.Instance.ResetWeather();

// HUD/test-menu API:
var weather = Game.Weather.WeatherController.Instance;
weather.Changed += Refresh; // Action, no parameters; unsubscribe when the HUD disables.
string label = Game.Weather.WeatherController.DisplayName(weather.Current);
weather.NextWeather(); // Programmatic selections while paused queue a target until resume.
```

`M` only cycles on a fresh key press during `GameFlow.Playing` with positive time scale
and no selected TMP/legacy input field. Selection is session-only. A 2.4-second smooth
blend retargets from the currently visible palette. There are no round or save-system
subscriptions. Reset snaps to the original sunny lighting and clears rain, even in pause.

## Rendering contract

- Runtime global URP Volume: exposure, multiplication tint, saturation, zero contrast.
  It affects both URP-lit terrain and unlit/default sprites, before ScreenSpaceOverlay UI.
- Runtime camera post-processing/mask changes are restored; its update mode is preserved.
  Manual volume stacks refresh only after `VolumeManager.isInitialized`, including via
  `activeRenderPipelineCreated` when the controller is enabled before the first render.
  Disabling weather unregisters its Volume and refreshes a borrowed manual stack with
  the original mask. The pipeline callback is unsubscribed on disable/destruction.
  Existing sun,
  ambient and fog are restored on disable/destruction; no sun or shared profile is replaced.
- Atmospheric exponential-squared fog is applied by URP Lit before the black unlit
  WorldZoneFog surface. Graphics Settings use Custom fog stripping, retaining `FOG_EXP2`
  for runtime weather even though World.unity starts without fog.
  Fog density is `0.022`: the distant orthographic gameplay camera made `0.034` obscure
  almost the whole terrain in native-player QA.
- Local precipitation: one emitter, maximum 160 particles, no collision/shadows/depth
  texture/noise texture, manually simulated only on gameplay ticks. Shader loaded directly
  from `Resources/Weather/LocalRain` is included in builds. Closed rectangles are updated
  only on access-mask/world changes. `Blend DstColor One` also preserves black when an
  unlocked foreground drop projects over a closed background.
- Disabled controller retains bounded native resources for re-enable; destruction disposes
  its profile **and components**, runtime material, emitter/root, and camera data it created.

## Tests supplied

- `Game.Weather.EditMode.Tests`: six-state sequence/wrap, smooth endpoints, mid-blend
  retargeting continuity, paused elapsed time, invalid/repeated input, reset and Spanish labels.
- `Game.Weather.PlayMode.Tests`: actual Input System M edges/held key, blocked/game-over/
  time-scale/text-input rejection, particle lifetime and position freeze (including real
  frames at time scale 1), post-processing for unlit sprites, menu reset, singleton idempotency,
  camera/light/fog/ambient restore, native resource cleanup, particle budget, unlock masks.
- A GPU readback test draws the real rain shader: visible ground brightens, pure black
  remains black, and the closed-zone fragment is discarded. It skips a Null graphics device.

Run PlayMode tests in the runner's empty scene, not during the populated World play session.

### Deterministic batchmode input and first render

CI run `45100814541` passed EditMode but exposed six keyboard failures and an early URP
initialization error in the initially empty PlayMode scene.

- The fixture clones/restores `InputSystem.settings` instead of editing the project asset.
  It selects Manual updates, `IgnoreFocus`, routes editor input to the player, and restores
  `Application.runInBackground` and the previous keyboard in `finally`.
  **Settings ownership:** `InputManager.settings` (package 1.20, lines 190–194) destroys the
  outgoing object when its flags equal `HideAndDontSave`. An empty batchmode scene can
  supply exactly that temporary original; `Instantiate` also copies those flags. The
  fixture temporarily protects that original with `DontSave`, gives the clone `DontSave`,
  reinstalls the original **by reference**, then restores its exact flags. Only a detached
  clone is destroyed. Setup/teardown assert both liveness and reference identity, preventing
  the first passing test from leaving destroyed settings for every later fixture
  (observed in PlayMode job `286a423ad3004354b8680f05718744d7`).
- Every injected state advances **explicitly** with `InputUpdateType.Manual`. The typed
  `InputSystem.Update(InputUpdateType)` is internal in package 1.20, so this test-only
  adapter resolves that verified overload by reflection. The public parameterless overload
  chooses `InputManager.defaultUpdateType`, which can select Editor state based on focus.
- `ButtonControl.wasPressedThisFrame` initializes its edge history lazily. The fixture first
  samples a released M and completes another input frame. Assertions check update type,
  queued button state and every expected press edge, in addition to the original weather
  sequence, held-key, blocker and event-count assertions.
- The ViaScripting cleanup test renders a 16×16 target for one frame and asserts
  `VolumeManager.isInitialized` **before** calling the setter. It then verifies that the
  runtime preserves that mode, updates the actual cached stack with weather, and restores
  the stack's original exposure on disable. A separate synchronous regression starts with
  the serialized camera mode already set to ViaScripting and creates/destroys weather in
  the same frame, so it can exercise cleanup before the first render. No unexpected logs
  are suppressed.

Source references (installed packages):
`InputSystem/Runtime/InputManager.cs` (`defaultUpdateType` and focus routing),
`InputSystem/Runtime/InputSystem.cs` (typed `Update`),
`InputSystem/Runtime/Controls/ButtonControl.cs` (lazy frame-press tracking);
URP `Runtime/UniversalAdditionalCameraData.cs` (`CameraExtensions.UpdateVolumeStack`,
UUM-91000 initialization guard) and `Runtime/UniversalRenderPipeline.cs`
(`UpdateVolumeFramework`, first-render creation of manual stacks).

The owning session runs compilation/tests/captures/build; this module's author did not start
or refresh Unity, run tests, enter Play mode or build.
