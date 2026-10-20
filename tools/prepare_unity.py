"""Build and stage the portable simulation and canonical content for Unity.

Only the simulation DLL is copied: Unity's pinned UPM package supplies Newtonsoft.
The ignored staging manifest records source and output hashes. --check is read-only
and needs no SDK or bin/ output; it detects source changes since the last build.
"""

import argparse
import hashlib
import json
import subprocess
import sys
import uuid
from pathlib import Path

if __package__ in (None, ""):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from tools.content import ContentError, ROOT, parse_json, validate_content
from tools.generate_docs import ENCYCLOPEDIA_PATH, render_outputs
from tools.player_help import load_player_help

PROJECT = Path("src/SaltAndLedger.Simulation/SaltAndLedger.Simulation.csproj")
BUILT_DLL = PROJECT.parent / "bin/Release/netstandard2.1/SaltAndLedger.Simulation.dll"
SOURCE_CONTENT = Path("data/content.json")
GENERATED = Path("unity/Assets/Generated")
SIMULATION_DLL = GENERATED / "Simulation/SaltAndLedger.Simulation.dll"
UNITY_CONTENT = GENERATED / "Resources/CanonicalContent.json"
UNITY_ENCYCLOPEDIA = GENERATED / "Resources/Encyclopedia.json"
MANIFEST = Path("unity/.simulation-staging.json")


def _hash(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def metadata_outputs() -> dict[Path, bytes]:
    """Stable GUIDs let generated assets survive a clean checkout or rebuild."""
    outputs = {}
    folders = (GENERATED, SIMULATION_DLL.parent, UNITY_CONTENT.parent)
    for path in (*folders, SIMULATION_DLL, UNITY_CONTENT, UNITY_ENCYCLOPEDIA):
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "salt-and-ledger/" + path.as_posix()).hex
        text = f"fileFormatVersion: 2\nguid: {guid}\n"
        if path in folders:
            text += "folderAsset: yes\n"
        if path == SIMULATION_DLL:
            text += (
                "PluginImporter:\n"
                "  externalObjects: {}\n"
                "  serializedVersion: 2\n"
                "  iconMap: {}\n"
                "  executionOrder: {}\n"
                "  defineConstraints: []\n"
                "  isPreloaded: 0\n"
                "  isOverridable: 0\n"
                "  isExplicitlyReferenced: 0\n"
                "  validateReferences: 1\n"
                "  platformData:\n"
                "  - first:\n"
                "      '': Any\n"
                "    second:\n"
                "      enabled: 1\n"
                "      settings: {}\n"
            )
        else:
            importer = "TextScriptImporter" if path in (UNITY_CONTENT, UNITY_ENCYCLOPEDIA) else "DefaultImporter"
            text += f"{importer}:\n  externalObjects: {{}}\n"
        text += "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
        outputs[Path(str(path) + ".meta")] = text.encode("utf-8")
    return outputs


def source_hashes(root: Path) -> dict[str, str]:
    """Fingerprint compilation inputs and staging policy, never cached bin/obj/."""
    paths = {PROJECT, SOURCE_CONTENT, Path("data/content.schema.json"), Path("global.json"),
             Path("tools/prepare_unity.py"), Path("tools/content.py"), Path("tools/generate_docs.py"),
             Path("tools/player_help.py"), Path("data/player-help.json"),
             Path("data/player-help.schema.json"), ENCYCLOPEDIA_PATH}
    for path in (root / PROJECT.parent).rglob("*"):
        relative = path.relative_to(root)
        if path.is_file() and not {"bin", "obj"}.intersection(relative.parts):
            paths.add(relative)
    # These files can alter SDK builds without editing the simulation sources.
    for name in ("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props",
                 "NuGet.Config", "NuGet.config", "nuget.config"):
        for parent in (Path(), Path("src")):
            if (root / parent / name).is_file():
                paths.add(parent / name)
    return {path.as_posix(): _hash((root / path).read_bytes()) for path in sorted(paths)}


def _canonical_bytes(root: Path) -> bytes:
    content = (root / SOURCE_CONTENT).read_bytes()
    validate_content(parse_json(content.decode("utf-8")), root / "data/content.schema.json")
    return content


def _encyclopedia_bytes(root: Path, content: bytes) -> bytes:
    """Require the checked-in reference to match both authoritative sources."""
    source = root / ENCYCLOPEDIA_PATH
    if not source.is_file():
        raise ContentError("Missing generated/encyclopedia.json; run python tools/generate_docs.py.")
    catalog = parse_json(content.decode("utf-8"))
    player_help = load_player_help(catalog, root / "data/player-help.json",
                                  root / "data/player-help.schema.json")
    expected = render_outputs(catalog, player_help)[ENCYCLOPEDIA_PATH]
    actual = source.read_bytes()
    if actual != expected:
        raise ContentError("Stale generated/encyclopedia.json; run python tools/generate_docs.py.")
    return actual


def _unexpected_files(root: Path) -> list[str]:
    allowed = set(metadata_outputs()) | {SIMULATION_DLL, UNITY_CONTENT, UNITY_ENCYCLOPEDIA}
    return [f"Unexpected generated file: {path.relative_to(root).as_posix()}"
            for path in sorted((root / GENERATED).rglob("*"))
            if path.is_file() and path.relative_to(root) not in allowed]


def check_prepared(root: Path) -> list[str]:
    """Check the last successful staging operation without building or writing."""
    content = _canonical_bytes(root)
    encyclopedia = _encyclopedia_bytes(root, content)
    errors = _unexpected_files(root)
    if not (root / MANIFEST).is_file():
        return errors + ["Missing staging manifest; run python tools/prepare_unity.py."]
    try:
        manifest = json.loads((root / MANIFEST).read_text(encoding="utf-8"))
    except (ValueError, UnicodeError):
        return errors + ["Invalid staging manifest; run python tools/prepare_unity.py."]
    if not isinstance(manifest, dict) or manifest.get("formatVersion") != 2:
        return errors + ["Unsupported staging manifest; run python tools/prepare_unity.py."]
    if manifest.get("inputs") != source_hashes(root):
        errors.append("Stale staging inputs (source, content, schema, or build policy changed).")
    if not str(manifest.get("sdkVersion", "")).startswith("10."):
        errors.append("Staging manifest does not record a .NET 10 build.")
    expected_metadata = metadata_outputs()
    expected_paths = set(expected_metadata) | {SIMULATION_DLL, UNITY_CONTENT, UNITY_ENCYCLOPEDIA}
    hashes = manifest.get("outputs")
    if not isinstance(hashes, dict) or set(hashes) != {path.as_posix() for path in expected_paths}:
        return errors + ["Invalid staging manifest output list."]
    for path in sorted(expected_paths):
        if not (root / path).is_file():
            errors.append(f"Missing generated file: {path.as_posix()}")
            continue
        value = (root / path).read_bytes()
        if _hash(value) != hashes[path.as_posix()]:
            errors.append(f"Changed generated file: {path.as_posix()}")
        if path in expected_metadata and value != expected_metadata[path]:
            errors.append(f"Stale generated metadata: {path.as_posix()}")
        if path == UNITY_CONTENT and value != content:
            errors.append("Staged content differs from data/content.json.")
        if path == UNITY_ENCYCLOPEDIA and value != encyclopedia:
            errors.append("Staged encyclopedia differs from generated/encyclopedia.json.")
    return errors


def prepare(root: Path, dotnet: str = "dotnet") -> None:
    """Validate current references, rebuild, then stage the three runtime assets."""
    content = _canonical_bytes(root)
    encyclopedia = _encyclopedia_bytes(root, content)
    unexpected = _unexpected_files(root)
    if unexpected:
        raise ValueError("\n".join(unexpected) + "\nRemove unexpected files before staging.")
    inputs = source_hashes(root)
    version = subprocess.run([dotnet, "--version"], cwd=root, check=True,
                             capture_output=True, text=True).stdout.strip()
    if not version.startswith("10.") or "-" in version:
        raise ValueError(f"A stable .NET 10 SDK is required; selected SDK is {version!r}.")
    # Build every time; never assume an existing DLL matches the current sources.
    subprocess.run([dotnet, "build", PROJECT.as_posix(), "--configuration", "Release",
                    "--nologo", "--no-incremental", "-p:RestoreLockedMode=true"], cwd=root, check=True)
    if source_hashes(root) != inputs:
        raise ValueError("Staging inputs changed during the build; run preparation again.")
    outputs = metadata_outputs()
    outputs[SIMULATION_DLL] = (root / BUILT_DLL).read_bytes()
    outputs[UNITY_CONTENT] = content
    outputs[UNITY_ENCYCLOPEDIA] = encyclopedia
    manifest = {
        "formatVersion": 2,
        "sdkVersion": version,
        "inputs": inputs,
        "outputs": {path.as_posix(): _hash(value) for path, value in sorted(outputs.items())},
    }
    # Write the manifest last so an incomplete copy cannot look up to date.
    outputs[MANIFEST] = (json.dumps(manifest, indent=2, sort_keys=True) + "\n").encode("utf-8")
    for path, value in outputs.items():
        target = root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.is_file() or target.read_bytes() != value:
            target.write_bytes(value)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Read-only freshness check; no build or SDK needed.")
    parser.add_argument("--dotnet", default="dotnet", help="Path to a .NET 10 dotnet executable.")
    args = parser.parse_args()
    try:
        if args.check:
            errors = check_prepared(ROOT)
            if errors:
                print("\n".join(errors), file=sys.stderr)
                print("Run python tools/prepare_unity.py to rebuild and stage.", file=sys.stderr)
                return 1
            print("Unity simulation, canonical content and encyclopedia are up to date.")
        else:
            prepare(ROOT, args.dotnet)
            print(f"Prepared {SIMULATION_DLL.as_posix()}, {UNITY_CONTENT.as_posix()} and {UNITY_ENCYCLOPEDIA.as_posix()}.")
        return 0
    except (ContentError, OSError, ValueError, subprocess.CalledProcessError) as error:
        print(f"Unity preparation failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
