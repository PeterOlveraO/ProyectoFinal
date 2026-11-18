"""Fail closed on missing, failed, skipped or incompletely discovered Unity tests."""

import argparse
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("artifacts", type=Path)
    args = parser.parse_args()
    failures = []
    summary = []
    for mode, required in REQUIRED.items():
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
