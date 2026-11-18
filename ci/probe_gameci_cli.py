"""Probe the released GameCI parser and Docker arguments WITHOUT running Unity.

This diagnostic is not a test runner or a replacement for the XML regression gate.
The fake docker executable deliberately fails before any container can start.
"""

import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile


FAKE_DOCKER = """#!/usr/bin/env python3
import json
import os
import sys
with open(os.environ["GAMECI_PROBE_CAPTURE"], "a") as stream:
    stream.write(json.dumps(sys.argv[1:]) + "\\n")
if len(sys.argv) > 1 and sys.argv[1] == "run":
    print("GAMECI_PROBE_STOP: no container or Unity process was started", file=sys.stderr)
    sys.exit(86)
sys.exit(0)
"""


def probe(binary):
    binary = Path(binary).resolve(strict=True)
    args = [
        str(binary), "test", "--docker", "--dockerShmSize=1025m", "--engine=unity",
        "--testPlatforms=playmode;editmode;COMBINE_RESULTS",
        "--coverageOptions=generateAdditionalMetrics;generateHtmlReport;generateBadgeReport;dontClear",
        "--artifactsPath=artifacts/unity-tests", "--dockerIsolationMode=default",
        "--containerRegistryRepository=unityci/editor", "--containerRegistryImageVersion=3",
    ]
    with tempfile.TemporaryDirectory(prefix="gameci-parser-probe-") as directory:
        root = Path(directory)
        docker = root / "docker"
        docker.write_text(FAKE_DOCKER)
        docker.chmod(0o755)
        capture = root / "docker-argv.jsonl"
        # No secrets, user CLI configuration, real Docker or licensing needed.
        env = {
            key: value for key, value in os.environ.items()
            if not key.startswith(("UNITY_", "GAME_CI_"))
        }
        env.update({
            "HOME": str(root),
            "PATH": str(root) + os.pathsep + os.environ["PATH"],
            "GAMECI_PROBE_CAPTURE": str(capture),
            "GAME_CI_COVERAGE_ENABLED": "false",
            "CI": "true",
        })
        result = subprocess.run(args, env=env, text=True, capture_output=True, timeout=60)
        output = result.stdout + result.stderr
        calls = [json.loads(line) for line in capture.read_text().splitlines()] if capture.exists() else []
        runs = [call for call in calls if call and call[0] == "run"]
        if "Unknown argument" in output or len(runs) != 1:
            raise ValueError(f"CLI did not reach one intercepted Docker run:\n{output}")
        if result.returncode == 0 or "GAMECI_PROBE_STOP" not in output:
            raise ValueError(f"CLI must fail when the fake container fails:\n{output}")
        docker_args = runs[0]
        values = [docker_args[i + 1] for i, value in enumerate(docker_args[:-1]) if value == "--env"]
        required = {
            "COVERAGE_ENABLED=false",
            "RUN_TESTS=true",
            "TEST_PLATFORMS=playmode;editmode;COMBINE_RESULTS",
            "ARTIFACTS_PATH=artifacts/unity-tests",
        }
        missing = required - set(values)
        if missing:
            raise ValueError(f"Missing Docker environment values: {sorted(missing)}")
        print(json.dumps({
            "binary": str(binary),
            "parser": "accepted",
            "docker_environment": sorted(required),
            "container_failure_blocks_cli": True,
            "unity_executed": False,
        }, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("binary", type=Path, help="Extracted game-ci release executable")
    args = parser.parse_args()
    try:
        probe(args.binary)
    except (OSError, ValueError, subprocess.TimeoutExpired) as error:
        print(f"Parser probe FAILED: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
