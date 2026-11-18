# iOS regression/build gate

The single workflow `.github/workflows/ios-build.yml` runs on branch pushes
(except `main`) and manual dispatch. It deliberately has no `pull_request`
trigger, so pushing a PR branch does not start duplicate builds.

Dependency chain: **unity-tests → unity-export → xcode-ipa**. Test failure,
missing XML, skipped/inconclusive cases or incomplete discovery blocks export.
XML and logs are uploaded with `always()`, including the gate diagnostic if
Unity/license setup fails.

## Unity tests

Unity **6000.5.11f1**, test-runner **v4**, builder **v6**, GameCI CLI **v0.1.73**.
The export path/build method is retained from successful run
[`43635888850`](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/43635888850)
on commit `267ba923e5d88b9705420ba8f75abd4b3c9515e2`.

The result validator requires all existing EditMode assemblies:

| Assembly | Minimum passed cases |
| --- | ---: |
| `Game.Terrain.Tests` | 12 |
| `Pokemon.Progression.Tests` | 15 |
| `Pokemon.Combat.Tests` | 8 |
| `Game.Runtime.PlayMode.Tests` | 8 |

The gate also reads the names declared by `Assets/Tests/**/*.asmdef` in **the
current checkout**. Each additional suite below becomes mandatory only when its
asmdef exists on that branch; moving/renaming the file does not disable it.
No lookup of another worktree, editor state, or graphics/gameplay assets is used.

| Conditional assembly | Mode | Minimum passed cases |
| --- | --- | ---: |
| `Game.PlayerAim.Tests` | EditMode | 15 |
| `Game.WorldProps.Tests` | EditMode | 5 |
| `Game.Rounds.Shop.Tests` | EditMode | 6 |
| `Game.Terrain.PlayMode.Tests` | PlayMode | 2 |
| `Game.ShotShop.EditMode.Tests` | EditMode | 10 |
| `Game.ShotShop.PlayMode.Tests` | PlayMode | 17 |
| `Game.DirectionalSprites.Tests` | EditMode | 10 |

Counts include each NUnit `[TestCase]` instance, not just test methods.
Missing/partially discovered conditional suites fail the gate even if all
baseline cases pass. An absent feature asmdef does not impose its suite or
asset requirements on independent branches. Invalid/duplicate asmdefs fail
closed, and `gate-summary.md` lists the required assemblies/floors.
For XML from a different checkout, pass `--project-root /path/to/checkout`;
CI uses its own working directory by default.

The PlayMode suite exercises real components across frames: projectile
pause/resume, single-target impact, enemy destruction/registry cleanup, projectile
range expiry, health/death/revive, special power/menu/cooldown, touch pointer
ownership/release and pooled object reuse. It does not load `World`, modify
PlayerPrefs/progression saves, or synthesize tests for trivial source changes.
Additional gameplay regressions can be added to the same test assembly.

`Assets/Scripts/Game.Runtime.asmdef` makes formerly `Assembly-CSharp` runtime
types accessible to test assemblies. Existing nested assemblies retain their
names. References are `Game.Terrain`, `Pokemon.Progression`, `Pokemon.Combat`,
`Unity.InputSystem`, `Unity.Cinemachine`, `Unity.TextMeshPro`, and
**`UnityEngine.UI`** (the actual uGUI assembly name, not `Unity.ugui`).
`Assets/Editor` stays outside this asmdef and can auto-reference public runtime
types. Tests use `UNITY_INCLUDE_TESTS` and are not included in normal iOS builds.

To run in Unity's Test Runner, select **EditMode / Run All**, then
**PlayMode / Run All** on the integrated branch. Do not open another Editor
against a CI worktree while a coordinator owns the project's Editor.
For CI-style XML collected by GameCI:

```sh
python3 ci/validate_test_results.py artifacts/unity-tests
```

Code coverage instrumentation is intentionally disabled; this is behavioral
regression coverage, not a measured line-coverage claim.

### GameCI parser compatibility

Run [`44229222400`](https://github.com/PeterOlveraO/ProyectoFinal/actions/runs/44229222400)
exposed an incompatibility **before Unity starts**: test-runner v4 translates
its `coverageEnabled: false` input into `--no-coverageEnabled`, but CLI v0.1.73
disables yargs boolean negation and rejects it as `noCoverageEnabled`.
The workflow therefore omits that input and sets the CLI's supported
`GAME_CI_COVERAGE_ENABLED: "false"` environment option instead. The wrapper's
default `true` emits no CLI flag, so the environment option reaches Docker as
`COVERAGE_ENABLED=false`. Both test platforms and the fail-closed XML gate stay
enabled. The builder/export CLI remains pinned to v0.1.73.

The released Linux v0.1.73 and v0.1.76 binaries were probed with the wrapper's
exact arguments (minus the unsupported negation). To repeat without Unity,
Docker, licensing or secrets, extract a CLI release archive and run:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 ci/probe_gameci_cli.py /path/to/game-ci
```

This diagnostic intercepts `docker run` with a temporary fake executable,
checks `RUN_TESTS=true`, both modes, artifact location and
`COVERAGE_ENABLED=false`, and requires the CLI to fail when the fake container
fails. It never starts Unity, never creates real test results, and is not used
as a substitute for the workflow's actual tests.

## IPA validation

Before publishing `ProyectoFinal-ipa`, `validate_ipa.py` checks ZIP integrity,
safe/unique members, exactly one `Payload/*.app`, readable binary/XML
`Info.plist`, bundle ID `com.peterolvera.proyectofinal`, the declared executable's
presence/permissions and an **arm64 MH_EXECUTE** Mach-O (thin or universal).
`validation.json` contains these facts plus the final IPA's SHA-256.

```sh
python3 ci/validate_ipa.py path/to/ProyectoFinal-unsigned.ipa --report validation.json
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s ci/tests -v
```

These checks do not prove signing, installation, rendering or touch-device
behavior. The artifact remains **unsigned** and requires re-signing (e.g.
AltStore/Sideloadly) or an Apple signing identity/profile. Physical iPhone smoke
testing remains a manual release check.
