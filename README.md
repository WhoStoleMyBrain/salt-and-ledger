# Salt & Ledger

A deterministic maritime economic strategy game in Unity 6. Explore a seeded four-town archipelago, draft four mandatory Grand Commission goals, and build a company through physical trade, private production and automated cargo itineraries. Sandbox uses the same economy without a deadline.

The supported game is **Expanding Economy v3, circulation rules1, pooled storage2, itinerary1**. Classic, seeded-v1 and Living Cities-v2 playable modes, fixed challenge prototypes, TradingLab and Bootstrap have been retired. Pure geography/role generation and NPC route algorithms remain because the current game uses them. Git history preserves the retired prototypes.

## Run locally

Install the .NET10 SDK selected by `global.json`, Python with `requirements.txt`, and the Unity version in `unity/ProjectSettings/ProjectVersion.txt`.

```text
dotnet restore SaltAndLedger.sln --locked-mode
dotnet test SaltAndLedger.sln -c Release
python -m pip install -r requirements.txt
python tools/validate_data.py
python tools/generate_docs.py --check
python -m unittest discover -s tests/python -v
python -m mkdocs build --strict
python tools/prepare_unity.py
```

Open `unity/`, then `Assets/Scenes/ArchipelagoLab.unity` and Play. Start at the main menu, choose New Run, inspect the full map, draft the commission and Begin. The only build scene is ArchipelagoLab. See [Unity setup and verification](docs/UNITY.md).

```text
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --grand-commission --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --economic-circulation --check
```

These run real commands and validate current objectives, exact continuation, physical flows and both cash boundaries. The optional `--expanding-economy --reinvestment --check` is a longer prescribed-policy comparison; `--deep` sweeps are opt-in. Successful examples do not imply universal economic recovery.

## Project boundaries

- `src/SaltAndLedger.Simulation/`: authoritative C#8 / `netstandard2.1` simulation, independent of Unity and filesystem policy.
- `data/content.json`: canonical goods, buildings and recipes; `data/player-help.json`: shared mechanics help.
- `unity/Assets/SaltAndLedger/`: UI Toolkit host, Editor build command and integration tests.
- `samples/SaltAndLedger.Demo/`: current commission witnesses and bounded economic experiments.
- `tests/`: deterministic accounting, pricing, generation, production, itinerary, objective, persistence and documentation protection.
- `tools/`: content validation, shared documentation generation and reproducible DLL/content staging.

`prepare_unity.py` stages one simulation DLL and canonical JSON into ignored `Assets/Generated/`. Unity's pinned Newtonsoft package provides JSON; do not duplicate simulation sources or JSON assemblies. Generated reference outputs are committed and must be regenerated, never edited by hand.

## Read next

[Player guide](docs/PLAYER_GUIDE.md) · [Grand Commission](docs/GRAND_COMMISSION.md) · [Architecture](docs/ARCHITECTURE.md) · [Mechanics](docs/MECHANICS.md) · [Save policy](docs/SAVES.md) · [Roadmap](docs/ROADMAP.md) · [Cleanup verification](docs/REPOSITORY-CLEANUP.md).

Local saves support current schema6 only. Unsupported or damaged files fail without replacing the active session. This pre-release project does not promise migration of historical saves. GitHub Pages publication remains manual and is not part of ordinary validation.
