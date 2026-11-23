"""Fail closed on missing, failed, skipped or incompletely discovered Unity tests."""

import argparse
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


# Floors, not exact counts: additional regression cases are welcome.
REQUIRED = {
    "editmode": {
        "Game.Terrain.Tests": 12,
        "Pokemon.Progression.Tests": 15,
        "Pokemon.Combat.Tests": 8,
    },
    "playmode": {"Game.Runtime.PlayMode.Tests": 8},
}

# These assemblies are integrated independently. Require their full regression
# floor only when their asmdef is present in this checkout, never from another
# worktree or from a CI result left by a different branch.
# Counts include individual NUnit TestCase instances, not just test methods.
CONDITIONAL = {
    "Game.PlayerAim.Tests": ("editmode", 15),
    "Game.WorldProps.Tests": ("editmode", 9),
    "Game.Rounds.Shop.Tests": ("editmode", 48),
    "Game.Terrain.PlayMode.Tests": ("playmode", 2),
    "Game.ShotShop.EditMode.Tests": ("editmode", 14),
    "Game.ShotShop.PlayMode.Tests": ("playmode", 41),
    "Game.DirectionalSprites.Tests": ("editmode", 18),
    "Game.Rounds.EditMode.Tests": ("editmode", 19),
    "Game.Rounds.PlayMode.Tests": ("playmode", 24),
    "Game.CompactMenus.Tests": ("playmode", 11),
    "Game.WorldZones.PlayMode.Tests": ("playmode", 13),
    "Game.Weather.EditMode.Tests": ("editmode", 12),
    "Game.Weather.PlayMode.Tests": ("playmode", 34),
}

# Features added to existing baseline assemblies must also be discovered.
FEATURE_FLOORS = {
    "Assets/Tests/EditMode/WorldTraversalTests.cs": [("editmode", "Game.Terrain.Tests", 44)],
    "Assets/Tests/PlayMode/DropRuntimeTests.cs": [("playmode", "Game.Runtime.PlayMode.Tests", 64)],
    "Assets/Tests/PlayMode/DesktopCombatTests.cs": [("playmode", "Game.Runtime.PlayMode.Tests", 73)],
    "Assets/Tests/PlayMode/DesktopInteractionTests.cs": [("playmode", "Game.Runtime.PlayMode.Tests", 88)],
    "Assets/Tests/DirectionalSprites/PokemonMaterialTests.cs": [("editmode", "Game.DirectionalSprites.Tests", 20)],
    "Assets/Tests/Weather/PlayMode/PokemonFogRenderingTests.cs": [("playmode", "Game.Weather.PlayMode.Tests", 38)],
    "Assets/Scripts/UI/CreditsView.cs": [("playmode", "Game.CompactMenus.Tests", 38)],
    "Assets/Scripts/Combat/AutoAimTargeting.cs": [("playmode", "Game.Rounds.PlayMode.Tests", 25)],
    "Assets/Scripts/UI/TestMenuView.cs": [
        ("playmode", "Game.CompactMenus.Tests", 33),
        ("editmode", "Pokemon.Progression.Tests", 25),
    ],
}


def required_for_checkout(project):
    required = {mode: dict(assemblies) for mode, assemblies in REQUIRED.items()}
    tests = Path(project) / "Assets" / "Tests"
    if not tests.is_dir():
        raise ValueError(f"{tests}: checkout test directory is missing")
    seen = set()
    for path in sorted(tests.rglob("*.asmdef")):
        try:
            definition = json.loads(path.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError) as error:
            raise ValueError(f"{path}: cannot read test asmdef: {error}") from error
        if (not isinstance(definition, dict) or not isinstance(definition.get("name"), str)
                or not definition["name"].strip()):
            raise ValueError(f"{path}: test asmdef must declare an assembly name")
        name = definition["name"]
        if name in seen:
            raise ValueError(f"{path}: duplicate test assembly name {name}")
        seen.add(name)
        if name in CONDITIONAL:
            mode, minimum = CONDITIONAL[name]
            required[mode][name] = minimum
    for relative, floors in FEATURE_FLOORS.items():
        if (Path(project) / relative).is_file():
            for mode, name, minimum in floors:
                required[mode][name] = max(required[mode].get(name, 0), minimum)
    return required


def validate(path, required):
    root = ET.parse(path).getroot()
    if root.tag != "test-run" or root.get("result") != "Passed":
        raise ValueError(f"{path}: test-run must be Passed")
    cases = root.findall(".//test-case")
    if not cases or any(case.get("result") != "Passed" for case in cases):
        raise ValueError(f"{path}: every discovered test must pass (no skipped/inconclusive tests)")
    assemblies = {}
    for suite in root.findall(".//test-suite[@type='Assembly']"):
        name = Path(suite.get("name", "")).name.removesuffix(".dll")
        if name in assemblies:
            raise ValueError(f"{path}: duplicate assembly {name}")
        assemblies[name] = len(suite.findall(".//test-case"))
    for name, minimum in required.items():
        if assemblies.get(name, 0) < minimum:
            raise ValueError(f"{path}: {name}: expected >= {minimum}, found {assemblies.get(name, 0)}")
    return len(cases)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifacts", type=Path)
    parser.add_argument("--project-root", type=Path, default=Path.cwd(),
                        help="Checkout to inspect for conditional test asmdefs (default: cwd)")
    args = parser.parse_args(argv)
    failures = []
    summary = []
    try:
        requirements = required_for_checkout(args.project_root)
    except ValueError as error:
        failures.append(str(error))
        summary.append(f"- Checkout: FAILED — {error}")
        requirements = {}
    for mode, required in requirements.items():
        expected = ", ".join(f"{name} >= {minimum}" for name, minimum in required.items())
        summary.append(f"- Required {mode}: {expected}")
        try:
            count = validate(args.artifacts / f"{mode}-results.xml", required)
            summary.append(f"- {mode}: {count} passed; all required assemblies present")
        except (OSError, ValueError, ET.ParseError) as error:
            failures.append(str(error))
            summary.append(f"- {mode}: FAILED — {error}")
    text = "# Unity regression gate\n\n" + "\n".join(summary) + "\n"
    args.artifacts.mkdir(parents=True, exist_ok=True)
    (args.artifacts / "gate-summary.md").write_text(text)
    print(text)
    for error in failures:
        print(f"::error::{error}", file=sys.stderr)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
