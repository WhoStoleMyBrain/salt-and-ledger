# Salt & Ledger

A single-player maritime trading and economic strategy game intended for Unity 6. The long-term focus is building, optimizing, and automating trading networks within a simulated economy, inspired by Port Royale 2, Patrician 2, Antimatter Dimensions, and economic roguelikes.

This repository contains **Phase 0 and Phase 0.5: the simulation foundation and minimal Unity integration**. It loads validated definitions for logs, planks, a sawmill, and one production recipe; executes production one batch at a time; and generates wiki pages and encyclopedia data from the same definitions. The console demo and Unity smoke scene use the same simulation assembly. There is no playable economic game yet.

## Prerequisites

- A stable .NET 10 SDK, including its runtime for the demo and tests. `global.json` permits SDK 10.0.100 or later stable 10.0 feature bands; it does not roll forward to .NET 11.
- Python 3.13 or newer and pip.
- Unity **6000.6.6f1**, matching `unity/ProjectSettings/ProjectVersion.txt`, for Editor/player verification. Standalone checks do not require Unity.

The simulation targets `netstandard2.1` with C# 8 and Newtonsoft.Json 13.0.3. The demo and test projects target `net10.0`. NuGet lock files pin resolved dependencies; Unity supplies its own single Newtonsoft assembly through UPM package 3.2.2. CI runs without the Unity Editor.

## Setup and run

Create a project-local Python environment from the repository root:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

On macOS/Linux, activate it with `source .venv/bin/activate` instead. If PowerShell activation is restricted, invoke `.\.venv\Scripts\python.exe` in place of `python`; changing machine execution policy is not required.

Then run:

```powershell
python -m pip install -r requirements.txt
python tools/validate_data.py
dotnet restore SaltAndLedger.sln --locked-mode
dotnet build SaltAndLedger.sln -c Release --no-restore
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json
python tools/generate_docs.py
python -m mkdocs serve
```

The sample demonstrates the recipe `saw_planks`: one command consumes **2 logs and produces 1 plank** in a sawmill. Starting with 6 logs, the demo completes three batches, rejects a fourth for insufficient inputs, and ends with 0 logs and 3 planks. These quantities are proof-of-concept data, not a balance decision. The command has no simulated duration, prices, wages, or production policy.

## Open the Unity smoke scene

After installing the Python dependencies, run from the repository root:

```powershell
python tools/prepare_unity.py
python tools/prepare_unity.py --check
```

Preparation validates content, rebuilds the existing simulation with locked NuGet dependencies, and stages its DLL plus canonical JSON under ignored `unity/Assets/Generated/`. Open `unity/` through Unity Hub using **6000.6.6f1**, let packages resolve, and open `Assets/Scenes/Bootstrap.unity`. Enter Play mode; the Console should report three successful batches, an unchanged rejected batch, and `Salt & Ledger smoke passed: logs=0, planks=3; rejected batch left inventory unchanged.`

Rerun preparation after simulation, content, or relevant build configuration changes. Do not copy simulation sources or a NuGet `Newtonsoft.Json.dll` into Unity. See [Unity integration](docs/UNITY.md) for Edit Mode/Play Mode tests, player build commands, and actual verification limits.

## Verify changes

```powershell
dotnet restore SaltAndLedger.sln --locked-mode
dotnet build SaltAndLedger.sln -c Release --no-restore
dotnet test SaltAndLedger.sln -c Release --no-build
python -m unittest discover -s tests/python -v
python tools/validate_data.py
python tools/generate_docs.py --check
python -m mkdocs build --strict
python tools/prepare_unity.py
python tools/prepare_unity.py --check
```

The documentation `--check` verifies committed reference files without rewriting them. When content changes, regenerate documentation, review it, and commit it with the data. The Unity preparation `--check` verifies local staged assets against source and output hashes. GitHub Actions runs these standalone checks; Editor and player verification remain separate.

For an intentional NuGet dependency change, run `dotnet restore SaltAndLedger.sln --force-evaluate`, review the changed `packages.lock.json` files, and rerun verification. Keep Unity's `Packages/manifest.json` and `Packages/packages-lock.json` together when changing UPM dependencies.

## Repository map

| Path | Purpose |
| --- | --- |
| `src/SaltAndLedger.Simulation/` | Unity-independent content loader and sample production logic |
| `samples/SaltAndLedger.Demo/` | Console demonstration using the canonical content |
| `tests/SaltAndLedger.Simulation.Tests/` | Standalone C# tests |
| `unity/` | Existing Unity 6 project, Bootstrap smoke component, and integration tests |
| `data/content.json` | Canonical example content |
| `data/content.schema.json` | JSON Schema for content format version 1 |
| `tools/` | Data validation, deterministic documentation generation, and Unity preparation |
| `tests/python/` | Schema, semantic validation, generator, and Unity preparation checks |
| `docs/` | Authored design, architecture, and mechanics specifications |
| `docs/reference/` | Generated Markdown reference pages; do not edit manually |
| `generated/encyclopedia.json` | Generated structured reference for a future in-game encyclopedia |
| `.github/workflows/` | Headless CI |

Start with the [documentation index](docs/index.md), [design principles](docs/GAME_DESIGN.md), and [implemented architecture](docs/ARCHITECTURE.md). The [roadmap](docs/ROADMAP.md) is provisional; [economy](docs/ECONOMY.md) and [ideas](docs/IDEAS.md) distinguish accepted direction from open hypotheses and deferred work. Contribution and agent guidance is in [AGENTS.md](AGENTS.md).
