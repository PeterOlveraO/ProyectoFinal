from pathlib import Path
import plistlib
import stat
import struct
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

from ci import validate_ipa, validate_test_results


def macho(cpu=validate_ipa.CPU_TYPE_ARM64, filetype=2):
    return struct.pack("<IIIIIIII", 0xFEEDFACF, cpu, 0, filetype, 0, 0, 0, 0)


class IpaValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "test.ipa"

    def write_ipa(self, bundle=validate_ipa.BUNDLE_ID, binary=None, mode=0o100755,
                  executable="Game", prefix="Payload/Game.app", extra=None):
        info = plistlib.dumps({"CFBundleIdentifier": bundle, "CFBundleExecutable": executable},
                              fmt=plistlib.FMT_BINARY)
        with zipfile.ZipFile(self.path, "w") as archive:
            archive.writestr(prefix + "/Info.plist", info)
            member = zipfile.ZipInfo(prefix + "/Game")
            member.create_system = 3
            member.external_attr = mode << 16
            archive.writestr(member, macho() if binary is None else binary)
            for name, data in (extra or {}).items():
                archive.writestr(name, data)

    def test_accepts_real_zip_layout_binary_plist_and_arm64_header(self):
        self.write_ipa()
        result = validate_ipa.validate(self.path)
        self.assertEqual(result["app"], "Payload/Game.app")
        self.assertEqual(result["bundleIdentifier"], validate_ipa.BUNDLE_ID)
        self.assertEqual(len(result["sha256"]), 64)

    def test_accepts_fat_arm64_executable_slice(self):
        binary = macho()
        fat = struct.pack(">II", 0xCAFEBABE, 1)
        fat += struct.pack(">IIIII", validate_ipa.CPU_TYPE_ARM64, 0, 28, len(binary), 0)
        self.write_ipa(binary=fat + binary)
        self.assertEqual(validate_ipa.validate(self.path)["architecture"], "arm64")

    def test_rejects_simulator_x86_executable(self):
        self.write_ipa(binary=macho(cpu=0x01000007))
        with self.assertRaisesRegex(ValueError, "arm64"):
            validate_ipa.validate(self.path)

    def test_rejects_dylib_or_non_macho_executable(self):
        for binary in (macho(filetype=6), b"not a binary"):
            with self.subTest(binary=binary):
                self.write_ipa(binary=binary)
                with self.assertRaisesRegex(ValueError, "arm64"):
                    validate_ipa.validate(self.path)

    def test_rejects_fake_arm64_universal_header(self):
        fat = struct.pack(">II", 0xCAFEBABE, 1)
        fat += struct.pack(">IIIII", validate_ipa.CPU_TYPE_ARM64, 0, 28, 32, 0)
        self.write_ipa(binary=fat + macho(cpu=0x01000007))
        with self.assertRaisesRegex(ValueError, "arm64"):
            validate_ipa.validate(self.path)

    def test_rejects_wrong_bundle(self):
        self.write_ipa(bundle="com.example.wrong")
        with self.assertRaisesRegex(ValueError, "CFBundleIdentifier"):
            validate_ipa.validate(self.path)

    def test_rejects_missing_executable_or_path_traversal_in_plist(self):
        for executable in ("Missing", "../Game"):
            with self.subTest(executable=executable):
                self.write_ipa(executable=executable)
                with self.assertRaises((KeyError, ValueError)):
                    validate_ipa.validate(self.path)

    def test_rejects_non_executable_and_symlink(self):
        for mode in (stat.S_IFREG | 0o644, stat.S_IFLNK | 0o777):
            with self.subTest(mode=mode):
                self.write_ipa(mode=mode)
                with self.assertRaisesRegex(ValueError, "regular file"):
                    validate_ipa.validate(self.path)

    def test_rejects_missing_or_ambiguous_payload_app(self):
        self.write_ipa(prefix="Game.app")
        with self.assertRaisesRegex(ValueError, "Payload"):
            validate_ipa.validate(self.path)
        self.write_ipa(extra={"Payload/Other.app/Info.plist": b"bad"})
        with self.assertRaisesRegex(ValueError, "Payload"):
            validate_ipa.validate(self.path)

    def test_rejects_unsafe_zip_paths_and_non_zip(self):
        self.write_ipa(extra={"../untrusted": b"bad"})
        with self.assertRaisesRegex(ValueError, "Unsafe"):
            validate_ipa.validate(self.path)
        self.path.write_bytes(b"not a zip")
        with self.assertRaises(zipfile.BadZipFile):
            validate_ipa.validate(self.path)


class TestResultValidationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "results.xml"
        self.root = ET.Element("test-run", result="Passed")
        for name, count in validate_test_results.REQUIRED["editmode"].items():
            suite = ET.SubElement(self.root, "test-suite", type="Assembly", name=name + ".dll")
            for index in range(count):
                ET.SubElement(suite, "test-case", name=f"Test{index}", result="Passed")

    def validate(self):
        ET.ElementTree(self.root).write(self.path)
        return validate_test_results.validate(self.path, validate_test_results.REQUIRED["editmode"])

    def test_accepts_all_35_existing_editmode_tests(self):
        self.assertEqual(self.validate(), 35)

    def test_rejects_missing_suite_even_if_total_is_35(self):
        removed = self.root[0]
        self.root.remove(removed)
        for index in range(12):
            ET.SubElement(self.root[0], "test-case", name=f"Extra{index}", result="Passed")
        with self.assertRaisesRegex(ValueError, "Game.Terrain.Tests"):
            self.validate()

    def test_rejects_partial_discovery(self):
        self.root[0].remove(self.root[0][0])
        with self.assertRaisesRegex(ValueError, "expected >= 12"):
            self.validate()

    def test_rejects_failed_skipped_and_inconclusive_cases(self):
        for result in ("Failed", "Skipped", "Inconclusive"):
            with self.subTest(result=result):
                self.root[0][0].set("result", result)
                with self.assertRaisesRegex(ValueError, "every discovered test"):
                    self.validate()

    def test_rejects_failure_at_root_even_when_cases_passed(self):
        self.root.set("result", "Failed")
        with self.assertRaisesRegex(ValueError, "test-run"):
            self.validate()

    def test_rejects_zero_tests_and_missing_results(self):
        self.root.clear()
        self.root.set("result", "Passed")
        with self.assertRaisesRegex(ValueError, "every discovered test"):
            self.validate()
        with self.assertRaises(OSError):
            validate_test_results.validate(self.path.parent / "missing.xml", {})

    def test_playmode_requires_at_least_eight_executed_tests(self):
        self.root = ET.Element("test-run", result="Passed")
        suite = ET.SubElement(self.root, "test-suite", type="Assembly", name="Game.Runtime.PlayMode.Tests.dll")
        for index in range(8):
            ET.SubElement(suite, "test-case", name=f"Integration{index}", result="Passed")
        ET.ElementTree(self.root).write(self.path)
        required = validate_test_results.REQUIRED["playmode"]
        self.assertEqual(validate_test_results.validate(self.path, required), 8)
        suite.remove(suite[0])
        ET.ElementTree(self.root).write(self.path)
        with self.assertRaisesRegex(ValueError, "expected >= 8"):
            validate_test_results.validate(self.path, required)


if __name__ == "__main__":
    unittest.main()
