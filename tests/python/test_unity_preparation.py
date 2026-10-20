import shutil
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from tools.content import ContentError, ROOT
from tools.prepare_unity import (
    BUILT_DLL, GENERATED, MANIFEST, PROJECT, SIMULATION_DLL, SOURCE_CONTENT,
    UNITY_CONTENT, UNITY_ENCYCLOPEDIA, ENCYCLOPEDIA_PATH, check_prepared, metadata_outputs, prepare,
)


class UnityPreparationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        for path in (PROJECT, SOURCE_CONTENT, Path("data/content.schema.json"),
                     Path("global.json"), Path("tools/content.py"), Path("tools/prepare_unity.py"),
                     PROJECT.parent / "packages.lock.json", ENCYCLOPEDIA_PATH,
                     Path("tools/generate_docs.py"), Path("tools/player_help.py"),
                     Path("data/player-help.json"), Path("data/player-help.schema.json")):
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / path, target)
        self.source = self.root / PROJECT.parent / "Simulation.cs"
        self.source.write_text("// simulation source\n", encoding="utf-8")
        binary = self.root / BUILT_DLL
        binary.parent.mkdir(parents=True)
        binary.write_bytes(b"compiled simulation DLL")
        self.run = patch("tools.prepare_unity.subprocess.run", side_effect=self.fake_dotnet).start()
        self.addCleanup(patch.stopall)

    @staticmethod
    def fake_dotnet(command, **kwargs):
        return subprocess.CompletedProcess(command, 0, stdout="10.0.108\n")

    def prepare(self):
        prepare(self.root)

    def snapshot(self):
        return {path.relative_to(self.root): (path.read_bytes(), path.stat().st_mtime_ns)
                for path in self.root.rglob("*") if path.is_file()}

    def test_stages_exact_assets_and_never_copies_nuget_dependencies_or_sources(self):
        (self.root / BUILT_DLL.parent / "Newtonsoft.Json.dll").write_bytes(b"do not copy")
        (self.root / BUILT_DLL.parent / "SaltAndLedger.Simulation.pdb").write_bytes(b"do not copy")
        self.prepare()
        staged = {path.relative_to(self.root) for path in (self.root / GENERATED).rglob("*")
                  if path.is_file()}
        expected = set(metadata_outputs()) | {SIMULATION_DLL, UNITY_CONTENT, UNITY_ENCYCLOPEDIA}
        expected.remove(Path(str(GENERATED) + ".meta"))
        self.assertEqual(expected, staged)
        self.assertEqual((self.root / BUILT_DLL).read_bytes(), (self.root / SIMULATION_DLL).read_bytes())
        self.assertEqual((self.root / SOURCE_CONTENT).read_bytes(), (self.root / UNITY_CONTENT).read_bytes())
        self.assertEqual((self.root / ENCYCLOPEDIA_PATH).read_bytes(), (self.root / UNITY_ENCYCLOPEDIA).read_bytes())
        self.assertEqual([], check_prepared(self.root))
        build = self.run.call_args_list[1].args[0]
        self.assertIn("-p:RestoreLockedMode=true", build)
        self.assertIn("--no-incremental", build)
        self.assertIn("Release", build)

    def test_check_is_read_only_without_sdk_or_build_output(self):
        self.prepare()
        (self.root / BUILT_DLL).unlink()
        self.run.reset_mock()
        before = self.snapshot()
        self.assertEqual([], check_prepared(self.root))
        self.assertEqual(before, self.snapshot())
        self.run.assert_not_called()

    def test_missing_manifest_requires_preparation(self):
        self.assertIn("Missing staging manifest", "\n".join(check_prepared(self.root)))
        self.run.assert_not_called()

    def test_missing_or_modified_staged_binary_is_detected(self):
        self.prepare()
        (self.root / SIMULATION_DLL).unlink()
        self.assertIn("Missing generated file", "\n".join(check_prepared(self.root)))
        (self.root / SIMULATION_DLL).write_bytes(b"changed assembly")
        self.assertIn("Changed generated file", "\n".join(check_prepared(self.root)))

    def test_source_change_is_detected_even_when_bin_output_is_unchanged(self):
        self.prepare()
        original_binary = (self.root / BUILT_DLL).read_bytes()
        self.source.write_text("// modified source\n", encoding="utf-8")
        self.assertIn("Stale staging inputs", "\n".join(check_prepared(self.root)))
        self.assertEqual(original_binary, (self.root / BUILT_DLL).read_bytes())

    def test_added_or_removed_source_is_detected(self):
        self.prepare()
        added = self.source.with_name("Additional.cs")
        added.write_text("// new source\n", encoding="utf-8")
        self.assertIn("Stale staging inputs", "\n".join(check_prepared(self.root)))
        added.unlink()
        self.assertEqual([], check_prepared(self.root))
        self.source.unlink()
        self.assertIn("Stale staging inputs", "\n".join(check_prepared(self.root)))

    def test_sdk_policy_and_package_lock_changes_are_detected(self):
        for path in (Path("global.json"), PROJECT.parent / "packages.lock.json",
                     Path("tools/generate_docs.py"), Path("tools/player_help.py"),
                     Path("data/player-help.json"), Path("data/player-help.schema.json")):
            with self.subTest(path=path):
                self.prepare()
                target = self.root / path
                target.write_bytes(target.read_bytes() + b"\n")
                self.assertIn("Stale staging inputs", "\n".join(check_prepared(self.root)))

    def test_canonical_content_change_is_detected_and_preparation_copies_exact_bytes(self):
        self.prepare()
        source = self.root / SOURCE_CONTENT
        source.write_bytes(source.read_bytes() + b"\n")
        errors = "\n".join(check_prepared(self.root))
        self.assertIn("Stale staging inputs", errors)
        self.assertIn("Staged content differs", errors)
        self.prepare()
        self.assertEqual(source.read_bytes(), (self.root / UNITY_CONTENT).read_bytes())
        self.assertEqual([], check_prepared(self.root))

    def test_invalid_canonical_content_prevents_build_or_staging(self):
        (self.root / SOURCE_CONTENT).write_text('{"schemaVersion": 99}', encoding="utf-8")
        with self.assertRaises(ContentError):
            self.prepare()
        self.run.assert_not_called()
        self.assertFalse((self.root / GENERATED).exists())

    def test_conflicting_dll_is_reported_and_never_deleted_implicitly(self):
        self.prepare()
        conflicting = self.root / SIMULATION_DLL.parent / "Newtonsoft.Json.dll"
        conflicting.write_bytes(b"unexpected dependency")
        self.assertIn("Unexpected generated file", "\n".join(check_prepared(self.root)))
        before = self.snapshot()
        with self.assertRaisesRegex(ValueError, "Unexpected generated file"):
            self.prepare()
        self.assertEqual(before, self.snapshot())

    def test_build_failure_preserves_previous_stage(self):
        self.prepare()
        before = self.snapshot()
        self.run.side_effect = [subprocess.CompletedProcess([], 0, stdout="10.0.108\n"),
                                subprocess.CalledProcessError(1, ["dotnet", "build"])]
        with self.assertRaises(subprocess.CalledProcessError):
            self.prepare()
        self.assertEqual(before, self.snapshot())

    def test_non_dotnet_10_or_prerelease_sdk_is_rejected(self):
        for version in ("8.0.100", "11.0.100", "10.0.100-preview.1"):
            with self.subTest(version=version):
                self.run.side_effect = None
                self.run.return_value = subprocess.CompletedProcess([], 0, stdout=version)
                with self.assertRaisesRegex(ValueError, "stable .NET 10"):
                    self.prepare()
        self.assertFalse((self.root / GENERATED).exists())

    def test_repeated_preparation_builds_but_leaves_identical_assets_and_guids_untouched(self):
        self.prepare()
        before = self.snapshot()
        self.run.reset_mock()
        self.prepare()
        self.assertEqual(2, self.run.call_count)
        self.assertEqual(before, self.snapshot())

    def test_source_change_during_build_does_not_stage_mismatched_artifacts(self):
        def changing_build(command, **kwargs):
            if "build" in command:
                self.source.write_text("// changed during compilation\n", encoding="utf-8")
            return self.fake_dotnet(command, **kwargs)

        self.run.side_effect = changing_build
        with self.assertRaisesRegex(ValueError, "changed during the build"):
            self.prepare()
        self.assertFalse((self.root / GENERATED).exists())

    def test_corrupt_manifest_has_a_useful_error(self):
        self.prepare()
        for value in (b"not JSON", b"null", b'{"formatVersion": 1, "outputs": []}'):
            with self.subTest(value=value):
                (self.root / MANIFEST).write_bytes(value)
                self.assertTrue(check_prepared(self.root))

    def test_modified_metadata_and_staged_content_are_detected(self):
        self.prepare()
        (self.root / UNITY_CONTENT).write_text("{}", encoding="utf-8")
        metadata = self.root / Path(str(SIMULATION_DLL) + ".meta")
        metadata.write_text("incorrect metadata", encoding="utf-8")
        errors = "\n".join(check_prepared(self.root))
        self.assertIn("Stale generated metadata", errors)
        self.assertIn("Staged content differs", errors)

    def test_missing_stale_or_unsupported_encyclopedia_prevents_build(self):
        original = (self.root / ENCYCLOPEDIA_PATH).read_bytes()
        for value in (None, b"{}", original.replace(b'"formatVersion": 2', b'"formatVersion": 99')):
            with self.subTest(value=value is None):
                source = self.root / ENCYCLOPEDIA_PATH
                if value is None:
                    source.unlink()
                else:
                    source.write_bytes(value)
                with self.assertRaisesRegex(ContentError, "generated/encyclopedia.json"):
                    self.prepare()
                self.run.assert_not_called()
                source.write_bytes(original)

    def test_authored_help_change_requires_reference_regeneration_first(self):
        self.prepare()
        path = self.root / "data/player-help.json"
        value = json.loads(path.read_text(encoding="utf-8"))
        value["entries"][0]["description"] = "Changed authoritative help."
        path.write_text(json.dumps(value), encoding="utf-8")
        self.run.reset_mock()
        with self.assertRaisesRegex(ContentError, "Stale generated/encyclopedia.json"):
            self.prepare()
        self.run.assert_not_called()

    def test_modified_or_missing_staged_encyclopedia_is_detected(self):
        self.prepare()
        target = self.root / UNITY_ENCYCLOPEDIA
        target.write_bytes(b"{}")
        errors = "\n".join(check_prepared(self.root))
        self.assertIn("Staged encyclopedia differs", errors)
        target.unlink()
        self.assertIn("Missing generated file", "\n".join(check_prepared(self.root)))


if __name__ == "__main__":
    unittest.main()
