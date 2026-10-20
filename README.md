# Salt & Ledger

A single-player maritime trading and economic strategy game intended for Unity 6. The long-term focus is building, optimizing, and automating trading networks within a simulated economy, inspired by Port Royale 2, Patrician 2, Antimatter Dimensions, and economic roguelikes.

This repository contains **Task 1.7's complete run experience**: three starting charters and three challenges over four finite town markets, six canonical goods, private factories, automated warehouse shipping, and historical inventory-cost accounting. The Unity main menu, run setup, pause/results screens, ledger and offline encyclopedia surround the existing map and trading host. One portable simulation powers the game and reproducible experiments. Bootstrap, TradingLab, and historical scenarios remain compatibility fixtures.

## Play the living archipelago

Run `python tools/generate_docs.py` and `python tools/prepare_unity.py` to stage the simulation, canonical content and encyclopedia. Open `unity/` in Unity **6000.6.6f1**, open **`Assets/Scenes/ArchipelagoLab.unity`**, and press Play. The title screen offers New Run, Sandbox and Encyclopedia. Review a challenge and one starting charter before starting; the world begins paused on day 0.

Select Ledger and right-click a town, or use its visible Trade action, to open the centered trading overlay. Compare town stock and cargo, adjust quantity, and inspect exact totals and average execution prices. Local trades happen immediately; remote views explain the docking requirement. Opening trade pauses playback. **Next day**, **Play/Pause**, and **1×/3×/10×** control economic playback.

Select a town to construct or manage private industry. Procurement and industrial routes can connect a bakery's own stock to a ship and another town's market. Transfers require physical docking and preserve ownership and exact cost basis. The Ledger distinguishes liquid cash, purchases, sales revenue, released cost, realized gross margin and construction spending.

The **Coastal Supply Charter** requires 24 net Bread supplied to Eastport, **Regional Relief** measures 24 Bread actually consumed across towns, and **Enterprise Ambition** requires private output plus attributed margin. Each has a day-60 deadline and a 100-cash solvency condition; sandbox has no deadline. Escape respects open overlays before opening the pause menu. Completed runs freeze, show an auditable score and allow retry or another setup. Runs are in memory only.

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --runs
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --runs --check
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --industrial-logistics --check
```

The complete-run report demonstrates all nine charter/challenge pairings and nine idle failures, with per-operation financial reconciliation. Merchant and industrial charters complete the main supply challenge on days 29 and 39 respectively. The industrial comparison runs a real repeating supply chain and checks exact manual equivalence. These are feasible authored strategies, not claims of optimal or final balance. Read [Runs and charters](docs/RUNS.md), [Player guide](docs/PLAYER_GUIDE.md), [Playtest](docs/PLAYTEST.md), and the actual [Unity verification record](docs/UNITY.md).

Historical `--archipelago` and `--enterprise` reports remain unchanged and retain their original strategies and measurements. [Living archipelago](docs/LIVING_ARCHIPELAGO.md) and [Commerce and industry](docs/ENTERPRISE.md) distinguish those fixtures from the new run scenarios.

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

The original sample demonstrates the recipe `saw_planks`: one command consumes **2 logs and produces 1 plank** in a sawmill. Starting with 6 logs, the demo completes three batches, rejects a fourth for insufficient inputs, and ends with 0 logs and 3 planks. The catalog now contains six goods, six buildings, and six recipes. The explicit one-batch command remains usable without a world or clock. Legacy mode demonstrations use a two-good projection referencing the same canonical objects, preserving their transcripts and report fixtures without duplicating definitions.

## Run the deterministic world example

```powershell
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --world
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --world 2
```

The optional final argument sets initial logs from 0 through 2,147,483,647; the default is 6. The fixed scenario has one port, one shared inventory, and two instances of the canonical sawmill. The host calls `Step` five times and prints per-tick controls, ordered attempts, inventory changes, and final stock. With the default stock:

| Starting tick | Controls at start | Production outcome |
| --- | --- | --- |
| 0 | Disable A | B produces; 4 logs / 1 plank remain |
| 1 | Enable A, disable B | A produces; 2 logs / 2 planks remain |
| 2 | Enable B | A produces, then B reports shortage; 0 logs / 3 planks remain |
| 3 | None | Both report shortage; stock is unchanged |
| 4 | Disable both | No attempts; the clock still advances |

The final world tick is **5**, with **0 logs and 3 planks**. The example reruns the same scenario and compares the complete transcript, then compares another run with reversed building input order. A mismatch fails explicitly. Different initial stock changes the outcomes while keeping the controls and schedule fixed.

Each enabled building in this legacy world attempts one batch per abstract tick, ordered by ascending priority and ordinal instance ID. Controls affect that tick. Shortage is an expected outcome; exceptional failure rolls back the entire tick. Immutable snapshots and structured results expose outcomes without unbounded history. See [Mechanics](docs/MECHANICS.md) and [Architecture](docs/ARCHITECTURE.md). These original ticks remain abstract; the separate regional owner uses daily schedules. Final balance, wages, and production-optimization policies remain unresolved.

## Run the finite market example

```powershell
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --market
```

One sawmill produces into the existing location inventory, now the market's stock. The trader owns separate goods and cash. This public-market ownership of production is a prototype assumption, not a permanent factory-ownership model. Prices are fixed experimental bid/ask quotes: logs 2/3, planks 4/5. The trader buys at ask and sells at bid. Money uses checked `long` arithmetic in indivisible units; prices, currency scale, and initial balances are experimental.

The market starts with 2 logs and 20 cash; the trader starts with 4 logs and 7 cash. Four ticks demonstrate purchases, sales, stock/funds rejections, and production using sold logs. Trades execute in declared order before production: sold logs can be consumed this tick; newly produced planks are tradeable next tick. The final state is market **0 logs, 1 plank, 25 cash** and trader **0 logs, 2 planks, 2 cash**, at tick **4**. Combined cash stays **27**. There is no automatic replenishment or income.

Expected shortages reject one whole trade and allow later commands to continue. Invalid requests or overflow roll back the entire tick, including both inventories, cash, production, controls, and clock. The demo prints each transaction's before/after balances, checks its final state, and compares repeated/reordered setup runs. A headless test verifies the [documented per-tick example](docs/MECHANICS.md#executable-market-example).

## Run the pricing experiments

```powershell
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --pricing
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --pricing --check
```

The first command prints a comparison and writes `docs/experiments/pricing-results.json`; the second checks that the committed report matches a fresh run without rewriting it. Supply an optional final output path to generate or check a separate report. The pricing report is separate from canonical reference generation; do not edit its measurements by hand.

The runner compares the fixed baseline with **linear** and **piecewise scarcity** pricing through the existing world API. Scenarios cover abundant stock, moderate/severe scarcity, sawmill production, repeated trades and order splitting, finite liquidity, and numeric limits. Every dynamic bulk total sums the inventory transitions it crosses, using exact integer arithmetic and a bounded summation algorithm. No per-unit execution loop, price clock, or artificial replenishment is introduced.

Snapshot marginal quotes and read-only trade previews use execution's own calculation. Reports expose first/last marginal prices and the complete total; the first price multiplied by quantity is generally not a dynamic trade's cost. Reversing trades cannot create cash profit without another economic change, and splitting an uninterrupted order preserves its total. Fixed pricing and the original `--market` transcript remain unchanged.

Read [Pricing experiments](docs/PRICING_EXPERIMENTS.md) for parameters, measured comparisons, limitations, and the provisional recommendation, and [Mechanics](docs/MECHANICS.md#inventory-sensitive-pricing-hypotheses) for exact formulas and boundaries. These curves are experiments; passing accounting tests does not establish balance or enjoyable gameplay.

## Run the manual shipping comparison

```powershell
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --shipping
```

The handcrafted scenario connects Northport and Southhaven with a two-tick route. One trader has one cash account and one ship with capacity 6; its cargo is the trader's only goods inventory. Each port has independent finite stock, cash, and linear pricing, and Southhaven owns one active sawmill. A single `MaritimeWorldState` owns both ports, the ship, trader, and global tick; it reuses the existing transaction, pricing, and production calculations.

Two equivalent runs buy 2 or 6 logs, sail to Southhaven, sell there, then carry 2 planks back to Northport. Both finish at tick 6 with empty cargo. Measured gross trading profits are **69** and **119**, respectively; starting trader cash is 100. The full load earns more here but less profit per log and leaves a different local market state. This is a controlled comparison, not a claim about optimal strategy or final balance.

The demo prints per-tick accounts, local quotes, production, and voyage progress; rejects an oversized purchase; shows that trading while sailing fails without advancing time; and verifies exact results across repeated and reversed-port setup runs. Read [Manual shipping comparison](docs/SHIPPING_DEMO.md) for complete parameters and measured balances. Travel has no costs or risks, capacity counts items, market-owned production remains provisional, and no automatic replenishment is implemented.

## Run configurable route automation

```powershell
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --automation
```

Choose shipment targets/minimums, maximum marginal buy prices, minimum marginal sell prices, a cash reserve, and optional empty return. A stateless policy evaluates the committed maritime snapshot and submits the same manual commands, with at most one trade per tick and a safe optional departure. `MaritimeWorldState` still owns every account, cargo unit, voyage, and global tick. Automation executes the player's rules; it does not discover strategies or guarantee profit.

The demo shares the manual starting fixture. A log buying limit of 10 carries six logs and realizes **119 gross profit in six ticks**; lowering it to 8 carries two and realizes **69**, also in six ticks. Both return with two planks and finish empty. Every committed state matches its equivalent manual itinerary. With destination cash set to zero, the ship instead retains six logs and stops after three unchanged waiting ticks, at tick 5; it reports the liquidity reason and no completed-interval profit. No impossible sale is repeatedly submitted.

The runner prints decisions, exact prices, commands, transactions, cargo, local markets, and bounded stop reasons. It asserts repeated, reversed-port, and alternate-culture output. Four useful manual action batches become one policy configuration; both still need six host advances. See [Automation](docs/AUTOMATION.md) for exact rules, measurements, pausing/reconfiguration, safe execution, and limitations.

## Play the Unity Trading Lab

After installing the Python dependencies, run from the repository root:

```powershell
python tools/prepare_unity.py
python tools/prepare_unity.py --check
```

Preparation validates content and generated-reference freshness, rebuilds the existing simulation with locked NuGet dependencies, and stages its DLL plus canonical JSON and offline encyclopedia under ignored `unity/Assets/Generated/`. Open `unity/` through Unity Hub using **6000.6.6f1**, let packages resolve, open **`Assets/Scenes/TradingLab.unity`** for the historical two-port lab, and enter Play mode.

Inspect Northport and Southhaven, preview exact bulk prices, buy and sell cargo, and sail the two-tick route. Time advances only through explicit actions. Enable the full or restrictive route policy to compare the documented six-tick expeditions; the zero-liquidity scenario shows why a ship can retain goods despite an attractive price. Pause, change a limit, or intervene manually without a hidden clock. Follow [Playtest](docs/PLAYTEST.md) for controls, complete itineraries, blocked-route diagnosis, and a recovery experiment.

The separate **`Assets/Scenes/Bootstrap.unity`** remains the production smoke. Entering Play mode there should report three successful batches, an unchanged rejected batch, and `Salt & Ledger smoke passed: logs=0, planks=3; rejected batch left inventory unchanged.`

Rerun preparation after simulation, content, or relevant build configuration changes. Do not copy simulation sources or a NuGet `Newtonsoft.Json.dll` into Unity. See [Unity integration](docs/UNITY.md) for Edit Mode/Play Mode tests, player build commands, and actual verification limits.

## Verify changes

```powershell
dotnet restore SaltAndLedger.sln --locked-mode
dotnet build SaltAndLedger.sln -c Release --no-restore
dotnet test SaltAndLedger.sln -c Release --no-build
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --world
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --market
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --pricing --check
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --shipping
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --automation
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --archipelago --check
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --enterprise --check
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --industrial-logistics --check
dotnet run --project samples/SaltAndLedger.Demo -c Release --no-build -- data/content.json --runs --check
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
| `src/SaltAndLedger.Simulation/` | Unity-independent economic operations, legacy/regional worlds, company ownership, private industry, challenge rules, schedules, routes, and shared scenarios |
| `samples/SaltAndLedger.Demo/` | Legacy demonstrations plus reproducible pricing, living-archipelago, and enterprise reports |
| `tests/SaltAndLedger.Simulation.Tests/` | Standalone C# tests |
| `unity/` | Unity 6 ArchipelagoLab and TradingLab scenes/UI, Bootstrap smoke, and integration tests |
| `data/content.json` | Canonical goods, buildings, and production recipes |
| `data/content.schema.json` | JSON Schema for content format version 1 |
| `data/player-help.json` | Validated authored mechanics shared by the offline encyclopedia and website |
| `tools/` | Data validation, deterministic documentation generation, and Unity preparation |
| `tests/python/` | Schema, semantic validation, generator, and Unity preparation checks |
| `docs/` | Authored design, architecture, and mechanics specifications |
| `docs/experiments/pricing-results.json` | Reproducible measurements emitted by the pricing runner; checked independently of canonical references |
| `docs/experiments/archipelago-results.json` | Five sixty-day strategies with daily state, physical flows, transactions, and accounting checks |
| `docs/experiments/enterprise-results.json` | Five ninety-day enterprise strategies and separate charter trials, with company/private-stock accounting and six-good flow reconciliation |
| `docs/reference/` | Generated Markdown reference pages; do not edit manually |
| `docs/experiments/industrial-logistics-results.json` | Private shipping, manual equivalence, restrictive policies, supply/liquidity and demand variants |
| `docs/experiments/run-results.json` | All nine charter/challenge pairings, idle failures, exact accounting and scored results |
| `generated/encyclopedia.json` | Versioned offline encyclopedia generated from canonical definitions and shared player help |
| `.github/workflows/` | Headless CI |

Start with the [documentation index](docs/index.md), [design principles](docs/GAME_DESIGN.md), and [implemented architecture](docs/ARCHITECTURE.md). The [roadmap](docs/ROADMAP.md) is provisional; [economy](docs/ECONOMY.md) and [ideas](docs/IDEAS.md) distinguish accepted direction from open hypotheses and deferred work. Contribution and agent guidance is in [AGENTS.md](AGENTS.md).
