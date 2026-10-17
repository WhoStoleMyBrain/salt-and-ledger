# Salt & Ledger

A single-player maritime trading and economic strategy game intended for Unity 6. The long-term focus is building, optimizing, and automating trading networks within a simulated economy, inspired by Port Royale 2, Patrician 2, Antimatter Dimensions, and economic roguelikes.

This repository contains **Phase 0: a minimal headless foundation**. It loads validated definitions for logs, planks, a sawmill, and one production recipe; executes production one batch at a time; and generates wiki pages and encyclopedia data from the same definitions. It does not yet contain a Unity project or a playable game.

## Prerequisites

- .NET 8 SDK, including the .NET 8 runtime for the demo and tests. A newer SDK can build the projects, but does not necessarily install the .NET 8 runtime.
- Python 3.13 or newer and pip.
- Unity 6 is needed only for the separate local integration check in [Unity integration](docs/UNITY.md).

The simulation targets `netstandard2.1` with C# 8 and Newtonsoft.Json 13.0.3. The demo and test projects target `net8.0`. CI runs without the Unity Editor.

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
dotnet build src/SaltAndLedger.Simulation/SaltAndLedger.Simulation.csproj -c Release
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json
python tools/generate_docs.py
python -m mkdocs serve
```

The sample demonstrates the recipe `saw_planks`: one command consumes **2 logs and produces 1 plank** in a sawmill. Starting with 6 logs, the demo completes three batches, rejects a fourth for insufficient inputs, and ends with 0 logs and 3 planks. These quantities are proof-of-concept data, not a balance decision. The command has no simulated duration, prices, wages, or production policy.

## Verify changes

```powershell
dotnet test tests/SaltAndLedger.Simulation.Tests/SaltAndLedger.Simulation.Tests.csproj -c Release
python -m unittest discover -s tests/python -v
python tools/validate_data.py
python tools/generate_docs.py --check
python -m mkdocs build --strict
```

`--check` verifies that committed generated reference files match the canonical definitions without rewriting them. When content changes, run `python tools/generate_docs.py`, review the generated changes, and commit them with the data. The headless validation workflow runs these checks on GitHub Actions. Passing them does not establish Unity compilation or player compatibility; follow the [local Unity verification steps](docs/UNITY.md).

## Repository map

| Path | Purpose |
| --- | --- |
| `src/SaltAndLedger.Simulation/` | Unity-independent content loader and sample production logic |
| `samples/SaltAndLedger.Demo/` | Console demonstration using the canonical content |
| `tests/SaltAndLedger.Simulation.Tests/` | Standalone C# tests |
| `data/content.json` | Canonical example content |
| `data/content.schema.json` | JSON Schema for content format version 1 |
| `tools/` | Data validation and deterministic documentation generation |
| `tests/python/` | Schema, semantic validation, and generator checks |
| `docs/` | Authored design, architecture, and mechanics specifications |
| `docs/reference/` | Generated Markdown reference pages; do not edit manually |
| `generated/encyclopedia.json` | Generated structured reference for a future in-game encyclopedia |
| `.github/workflows/` | Headless CI |

Start with the [documentation index](docs/index.md), [design principles](docs/GAME_DESIGN.md), and [implemented architecture](docs/ARCHITECTURE.md). The [roadmap](docs/ROADMAP.md) is provisional; [economy](docs/ECONOMY.md) and [ideas](docs/IDEAS.md) distinguish accepted direction from open hypotheses and deferred work. Contribution and agent guidance is in [AGENTS.md](AGENTS.md).
