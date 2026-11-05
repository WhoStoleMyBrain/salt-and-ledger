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

Staging builds the portable DLL and places it, canonical JSON, the default world-generation configuration and format2 help under ignored `Assets/Generated/`. Unity's pinned Newtonsoft UPM package is the only JSON assembly. Do not copy simulation source into Assets. Commit source assets with their metadata; Library/Temp/Logs/Builds and generated staging are caches.

## Tests and build

Use Window > General > Test Runner for the current EditMode and PlayMode assemblies. Refresh/recompile after disk edits before running tests. When a project Editor is open, the Unity CLI/Pipeline test runner can drive that Editor; target the exact project path. Do not start a second Editor against the same project or mistake stale compiled tests for current verification.

Use **Salt & Ledger > Build Windows Mono Archipelago Lab** to produce `unity/Builds/ArchipelagoLab/Windows/SaltAndLedger.ArchipelagoLab.exe`. The build helper explicitly selects the current scene and restores the prior scripting backend/development setting. Test the built player, not only Editor compilation.

## Runtime acceptance

Follow [PLAYTEST](PLAYTEST.md) at 1366×768 and 1920×1080, including Default/Large and the other text sizes when layout changes. Confirm rendering and frame/day advancement, then pause and verify inspection leaves the economic fingerprint unchanged. Exercise Harbor trade and docked transfer, funded production, route planning/execution, Journal, exact save/load and the terminal acknowledgment. Verify Escape/focus/modal priority and safe failure on unsupported saves.

Screenshots of a running scene are visual evidence only; command/test assertions establish economics and save integrity. Keep curated actual captures distinct from art-direction moodboards. [Artwork restoration verification](TASK-3.0a-VERIFICATION.md), [approved UX verification](TASK-3.0-VERIFICATION.md), [factory and warehouse verification](TASK-2.8-VERIFICATION.md), [route automation verification](TASK-2.7-VERIFICATION.md) and [economic visibility verification](TASK-2.6-VERIFICATION.md) record executed checks, counts, limitations and remaining debts. No web build or Pages publication is implied.


## Procedural world configuration and local verification (Task 3.1)

`tools/prepare_unity.py` stages `data/world-generation.default.json` as
`Resources/WorldGeneration.json` alongside the DLL, canonical content and encyclopedia.
The existing New Run flow uses that configuration for the map preview, commission draft,
charter, and actual run. The configuration fingerprint is part of the run identity.

To use a custom file, copy an example from `data/world-generation-examples/` or edit the
default JSON and save it as `world-generation.json` under `Application.persistentDataPath`.
The setup screen's **Construction and economy details** foldout shows the current source
and provides **Reload world configuration**. New Run reloads that file automatically.
Invalid files or infeasible settings show an actionable setup error and disable starting;
they do not replace an active or saved world. Removing the override and reloading selects
the bundled default. Retry and Load preserve the configuration embedded in the run even
when the local override has changed. The file limit is 128 KiB.

The host now fits the actual generated town bounds, recreates map markers when a new world
has different towns or lanes, and labels public production and construction eligibility from
simulation data. Voyage previews and itinerary estimates use the authoritative connected
path. Ship markers follow its physical legs; passing a transit town does not dock, trade,
or award a visit. Fleet and deadline displays read the selected configuration.

The Cloud verification compiled and exercised the real `ArchipelagoLabSession`,
`EconomicText`, `VoyageVisualInterval`, and `LocalWorldGenerationConfiguration` without Unity dependencies. It
checked 8-, 10-, and 12-town file configurations, multi-lane voyages, exact save/load and
continuation, Retry, invalid-file isolation, and animation intervals bounded by committed
progress (including same-day construction and arrival). A Roslyn syntax pass covered all Unity C#
sources. Reproduce these portable checks with
`dotnet run --project tests/SaltAndLedger.UnityHost.Checks -c Release --no-build`
after the locked solution build. These checks do **not** establish Editor compilation, visual layout, input handling,
or Play Mode success.

Run the following remaining checks in the pinned local Editor:

1. Run both EditMode and PlayMode test assemblies, including
   `ProceduralWorldIntegrationTests`, `ExpandingMapTests`, `MapViewportTests`, and the
   setup, commission, Harbor, Industry, Fleet and route experience tests. Refresh the
   staged DLL first; do not use a stale assembly.
2. At 1366×768 and 1920×1080, use seeds 42, 99 and −42 at Default and Large text scales.
   Inspect every town in both preview and gameplay; check distinct names, nonoverlapping
   hit targets, route captions, actual public industries, private eligibility and artwork.
3. Start a Sandbox, buy cargo, select a town requiring intermediate lanes, and sail.
   Confirm displayed lane-by-lane travel, final arrival, unchanged cargo during transit,
   and immediate final-dock trading. Repeat with an automated itinerary; transit towns
   must not execute its stop commands.
4. Construct an eligible industry and ship, verify the configured fleet limit, and inspect
   the independent Mint, Silver-source and shipyard locations. Save during a multi-lane
   voyage, quit Play Mode, Load, and confirm exact continuation.
5. Test an 8-town and a 12-town override in succession without restarting the scene.
   Preview, start and Retry each; old town markers must disappear and the configuration
   identity must stay stable. Change `deadlineDays`, then check setup, draft review,
   Objectives, the day counter and saved identity against the same selected horizon.
6. Try malformed JSON and contradictory lane distances. Starting must remain disabled
   with a useful error. Restore valid JSON and reload; verify the active saved run was
   not changed. Finally test the Windows Mono player build as described above.
