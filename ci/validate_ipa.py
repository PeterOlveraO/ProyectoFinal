"""Validate the packaged IPA itself, without extraction or third-party dependencies.

This is a structural/device-architecture check, not signing or installability proof.
"""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import plistlib
import stat
import struct
import sys
import zipfile


BUNDLE_ID = "com.peterolvera.proyectofinal"
CPU_TYPE_ARM64 = 0x0100000C


def is_arm64_executable(data):
    # A 64-bit Mach-O executable, not an arm64 dylib or a renamed text file.
    if len(data) >= 32 and data[:4] in (b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xcf"):
        endian = "<" if data[:4] == b"\xcf\xfa\xed\xfe" else ">"
        cpu, _, filetype = struct.unpack_from(endian + "III", data, 4)
        return cpu == CPU_TYPE_ARM64 and filetype == 2  # MH_EXECUTE

    # Universal Mach-O: validate the actual arm64 slice, not just its fat header.
    fat_formats = {
        b"\xca\xfe\xba\xbe": (">", False), b"\xbe\xba\xfe\xca": ("<", False),
        b"\xca\xfe\xba\xbf": (">", True), b"\xbf\xba\xfe\xca": ("<", True),
    }
    if len(data) < 8 or data[:4] not in fat_formats:
        return False
    endian, fat64 = fat_formats[data[:4]]
    count = struct.unpack_from(endian + "I", data, 4)[0]
    stride = 32 if fat64 else 20
    table_end = 8 + count * stride
    if count == 0 or table_end > len(data):
        return False
    for index in range(count):
        entry = 8 + index * stride
        cpu = struct.unpack_from(endian + "I", data, entry)[0]
        offset, size = struct.unpack_from(endian + ("QQ" if fat64 else "II"), data, entry + 8)
        if cpu == CPU_TYPE_ARM64 and offset >= table_end and size >= 32 and offset + size <= len(data):
            # Slices must be thin; do not recursively accept malicious nested fat files.
            slice_data = data[offset:offset + size]
            if slice_data[:4] in (b"\xcf\xfa\xed\xfe", b"\xfe\xed\xfa\xcf"):
                return is_arm64_executable(slice_data)
    return False


def validate(path):
    with zipfile.ZipFile(path) as archive:
        entries = archive.infolist()
        names = [entry.filename for entry in entries]
        if len(names) != len(set(names)):
            raise ValueError("Duplicate ZIP members")
        for name in names:
            if name.startswith("/") or "\\" in name or ".." in name.split("/"):
                raise ValueError(f"Unsafe ZIP member: {name}")
        bad = archive.testzip()
        if bad is not None:
            raise ValueError(f"ZIP CRC failed: {bad}")
        apps = {
            PurePosixPath(name).parts[1] for name in names
            if len(PurePosixPath(name).parts) >= 2
            and PurePosixPath(name).parts[0] == "Payload"
            and PurePosixPath(name).parts[1].endswith(".app")
        }
        if len(apps) != 1:
            raise ValueError(f"Expected one Payload/*.app, found {len(apps)}")
        app = f"Payload/{apps.pop()}"
        info = plistlib.loads(archive.read(f"{app}/Info.plist"))
        if info.get("CFBundleIdentifier") != BUNDLE_ID:
            raise ValueError(f"Unexpected CFBundleIdentifier: {info.get('CFBundleIdentifier')}")
        executable = info.get("CFBundleExecutable")
        if not isinstance(executable, str) or not executable or executable in (".", "..") or "/" in executable or "\\" in executable:
            raise ValueError("Invalid CFBundleExecutable")
        member = archive.getinfo(f"{app}/{executable}")
        mode = member.external_attr >> 16
        if not stat.S_ISREG(mode) or not mode & 0o111:
            raise ValueError("CFBundleExecutable must be a regular file with executable permissions")
        if not is_arm64_executable(archive.read(member)):
            raise ValueError("CFBundleExecutable is not an arm64 Mach-O executable")
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return {
        "ipa": Path(path).name,
        "app": app,
        "bundleIdentifier": BUNDLE_ID,
        "executable": executable,
        "architecture": "arm64",
        "sha256": digest.hexdigest(),
        "signing": "unsigned; requires re-signing before installation",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("ipa", type=Path)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    try:
        report = json.dumps(validate(args.ipa), indent=2) + "\n"
    except (OSError, ValueError, KeyError, zipfile.BadZipFile, plistlib.InvalidFileException) as error:
        print(f"::error::Invalid IPA: {error}", file=sys.stderr)
        return 1
    if args.report:
        args.report.write_text(report)
    print(report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
