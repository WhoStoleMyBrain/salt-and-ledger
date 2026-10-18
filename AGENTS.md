# Repository guidance

Salt & Ledger is a Unity 6 maritime economic strategy project. This repository implements the Phase 0/0.5 foundation, Task 1.1 deterministic world, Task 1.2 finite trading/accounting prototype, Task 1.2b inventory-sensitive pricing experiments, and Task 1.3a manual two-port shipping. Read `docs/ARCHITECTURE.md`, `docs/GAME_DESIGN.md`, and `docs/MECHANICS.md` before changing its boundaries or behavior.

- Keep `src/SaltAndLedger.Simulation` independent of Unity, presentation, file-system policy, and documentation generation. Preserve `netstandard2.1` and C# 8 compatibility.
- Prefer small concrete types and explicit state transitions. Do not add speculative service layers, plugin systems, or interfaces for deferred features.
- Keep content in `data/content.json`; keep its structural contract in `data/content.schema.json`. Stable IDs are globally unique and must not depend on display names. Keep Python and C# semantic validation aligned.
- Use finite nonnegative integer inventory quantities and checked arithmetic. A failed production command must leave inventory unchanged. Preserve deterministic behavior; do not introduce wall-clock time or unseeded randomness into the simulation.
- Keep world steps atomic: exceptional failure must preserve both inventories, cash balances, building controls, and tick, including earlier staged attempts. Runtime instances reference immutable canonical definitions. Validate all commands first; apply controls, then trades in declared order, then production by ascending priority and ordinal instance ID. Expected stock/funds shortages are completed outcomes; do not retain unbounded history inside the world.
- Use nonnegative `long` cash and positive `long` prices with checked public numeric bounds. Trading conserves goods and combined cash. The existing location inventory is market stock shared with production; trader stock stays separate. Preserve the fixed-price baseline and keep experimental pricing parameters in runtime configuration. Do not add automatic replenishment.
- Preserve the single-location API. `MaritimeWorldState` owns exactly two independent ports, one trader cash account, one ship cargo inventory, and one global tick; never synchronize separate `WorldState` instances. Reuse the shared trade calculator and existing pricing/production operations. Cargo is the maritime trader's only goods inventory; all items occupy one capacity unit.
- Maritime steps validate first, then apply controls, docked trades in declared order, departure, production by ordinal port ID and existing building order, and one travel advance before a single atomic commit. Departure consumes one travel tick; arrival allows trading on the next step. Remote previews are hypothetical, never execution permission. Capacity shortage is ordinary rejection; illegal ship/port actions are exceptional and roll back every port, account, control, ship state, and tick.
- Price dynamic trades on reversible inventory edges with exact arithmetic and mathematical floor. Preserve same-edge `Ask >= Bid`, bounded positive prices, no profitable closed inventory cycles within one unchanged local pricing model without intervening production, and exact uninterrupted order-splitting totals. Cross-port transport profit is permitted. Preview and execution must share calculations; derive snapshot prices from stock, never a mutable price cache. Do not loop over requested units or distinct prices. Keep `UnitPrice` compatibility semantics explicit: dynamic bulk totals may differ from quantity times first marginal price.
- Document non-data-derived behavior in `docs/MECHANICS.md`. Label design proposals as hypotheses or deferred ideas rather than accepted rules.
- Do not hand-edit `docs/reference/` or `generated/encyclopedia.json`. Regenerate and commit those files with content changes.
- Regenerate `docs/experiments/pricing-results.json` with the `--pricing` demo after relevant experiment/calculation changes; use `--pricing --check` to verify it. Keep authored analysis aligned with measured output and distinguish pricing hypotheses from accounting contracts.
- The demo and headless tests use .NET 10; `global.json` selects a stable 10.0 SDK. Keep NuGet lock files and Unity's package manifest/lock synchronized with intentional dependency changes.
- Run `python tools/prepare_unity.py` after simulation or content changes. It rebuilds and stages the simulation DLL and canonical JSON under ignored `unity/Assets/Generated/`; never copy simulation source or another Newtonsoft assembly into Unity. Unity's pinned UPM package supplies JSON support.
- C#: PascalCase for public types/members, camelCase for local variables/parameters, four-space indentation. Python: snake_case, four-space indentation, standard-library solutions when sufficient. Follow adjacent code.
- Add focused tests for meaningful behavior changes and failure cases. Do not scaffold later roadmap features as part of foundation maintenance.
- Do not claim Unity integration was tested unless the Editor and relevant player build were actually exercised. Use `docs/UNITY.md` for local verification.

Run from the repository root as applicable:

```text
dotnet restore SaltAndLedger.sln --locked-mode
dotnet build src/SaltAndLedger.Simulation/SaltAndLedger.Simulation.csproj -c Release
dotnet test tests/SaltAndLedger.Simulation.Tests/SaltAndLedger.Simulation.Tests.csproj -c Release
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --world
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --market
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --pricing --check
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --shipping
python -m pip install -r requirements.txt
python tools/validate_data.py
python tools/generate_docs.py
python tools/generate_docs.py --check
python -m unittest discover -s tests/python -v
python -m mkdocs build --strict
python tools/prepare_unity.py
python tools/prepare_unity.py --check
```

Report what changed, which checks actually ran, and any unverified integration points.
