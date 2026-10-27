# Unity setup and verification

Use the Editor version pinned in `unity/ProjectSettings/ProjectVersion.txt` (Unity6000.6.6f1 at consolidation), a supported Windows Mono build module, .NET10 and Python dependencies from requirements.txt.

```text
dotnet restore SaltAndLedger.sln --locked-mode
python tools/validate_data.py
python tools/generate_docs.py --check
python tools/prepare_unity.py
python tools/prepare_unity.py --check
```

Open `unity/` and `Assets/Scenes/ArchipelagoLab.unity`. It is the sole build scene. Press Play: the main menu must open with no game exceptions. New Run previews the full map, then drafts four goals or starts explicit Sandbox. No separate Bootstrap/TradingLab setup is required.

Staging builds the portable DLL and places it, canonical JSON and format2 help under ignored `Assets/Generated/`. Unity's pinned Newtonsoft UPM package is the only JSON assembly. Do not copy simulation source into Assets. Commit source assets with their metadata; Library/Temp/Logs/Builds and generated staging are caches.

## Tests and build

Use Window > General > Test Runner for the current EditMode and PlayMode assemblies. Refresh/recompile after disk edits before running tests. When a project Editor is open, the Unity CLI/Pipeline test runner can drive that Editor; target the exact project path. Do not start a second Editor against the same project or mistake stale compiled tests for current verification.

Use **Salt & Ledger > Build Windows Mono Archipelago Lab** to produce `unity/Builds/ArchipelagoLab/Windows/SaltAndLedger.ArchipelagoLab.exe`. The build helper explicitly selects the current scene and restores the prior scripting backend/development setting. Test the built player, not only Editor compilation.

## Runtime acceptance

Follow [PLAYTEST](PLAYTEST.md) at 1366×768 and 1920×1080, including Default/Large and the other text sizes when layout changes. Confirm rendering and frame/day advancement, then pause and verify inspection leaves the economic fingerprint unchanged. Exercise Harbor trade and docked transfer, funded production, route planning/execution, Journal, exact save/load and the terminal acknowledgment. Verify Escape/focus/modal priority and safe failure on unsupported saves.

Screenshots of a running scene are visual evidence only; command/test assertions establish economics and save integrity. Keep curated actual captures distinct from art-direction moodboards. [Current verification](REPOSITORY-CLEANUP.md) records what was actually executed, counts, limitations and remaining debts. No web build or Pages publication is implied.
