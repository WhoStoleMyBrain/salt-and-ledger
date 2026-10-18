# Implemented architecture

Phase 0 separates content, simulation, host presentation, and reference generation using a small set of concrete modules. Phase 0.5 connects the same simulation to a minimal Unity host. Task 1.1 adds an explicitly stepped world using the existing production operation. Task 1.2 adds finite market/trader accounts and atomic trades to those same steps. Task 1.2b adds exact inventory-sensitive pricing, shared trade preview/execution calculations, and reproducible headless experiments while retaining fixed prices. These layers introduce no speculative services or extension points for future systems.

```text
data/content.json + data/content.schema.json
    |
    +-- Python schema + semantic validation
    |       +-- Markdown --> docs/reference/ --> MkDocs
    |       +-- structured reference --> generated/encyclopedia.json
    |
    +-- C# strict loading + semantic validation
            +-- immutable ContentCatalog
                    +-- Inventory + Production.TryRunBatch
                            +-- WorldState.Step --> immutable StepResult
                            |       +-- optional market/trader accounts and pricing configuration
                            |       +-- shared exact price / trade preview calculations
                            |       +-- console world/market/pricing runs / headless and Unity tests
                            +-- original console demo / Unity Bootstrap smoke
```

## Modules and dependencies

| Module | Responsibility | Dependencies |
| --- | --- | --- |
| `src/SaltAndLedger.Simulation` | Content loading, immutable definitions, inventory, atomic production/trading, exact stock-sensitive prices, deterministic world steps and results | `netstandard2.1`, C# 8, Newtonsoft.Json 13.0.3 |
| `samples/SaltAndLedger.Demo` | Read content; demonstrate production/world/trading; compare pricing hypotheses and emit reproducible experiment reports | `net10.0`, simulation project |
| `tests/SaltAndLedger.Simulation.Tests` | Headless tests of content, production, trading/accounting, pricing/preview invariants, scheduling, state ownership, determinism, and rollback | `net10.0`, simulation project, test dependencies |
| `unity` | Bootstrap smoke scene/component, integration tests, and a player build entry point | Unity 6000.6.6f1, staged simulation DLL, Unity Newtonsoft package 3.2.2 |
| `data` | Canonical content and Draft 2020-12 JSON Schema | No host or presentation dependency |
| `tools` | Validate authoring data; generate reference artifacts; prepare Unity runtime assets | Python, dependencies in `requirements.txt`; .NET 10 for Unity preparation |
| `tests/python` | Schema, semantic validator, and generation tests | Python test runner and pipeline dependencies |
| `docs` and `mkdocs.yml` | Authored specifications and external reference site | MkDocs |
| `.github/workflows` | Headless build, test, validation, freshness, site, and Unity staging checks | .NET 10 SDK and Python; no Unity Editor |

The simulation does not reference `UnityEngine`, access an Editor API, or decide where a host stores its content. The console host supplies JSON from a file; the Unity host supplies the same JSON from a `TextAsset`. The existing Unity project and its template packages remain in place; the integration adds no economic rules.

## Toolchain and Unity dependency boundary

`global.json` selects stable .NET 10 SDKs starting at 10.0.100 with `latestFeature` roll-forward within 10.0. This lets developers receive SDK servicing updates without adopting a later major runtime. The simulation's `netstandard2.1` and C# 8 targets remain unchanged. All three standalone projects have NuGet lock files, and CI restores with `--locked-mode`.

`python tools/prepare_unity.py` validates canonical content, builds the existing simulation project in Release, and stages only `SaltAndLedger.Simulation.dll` and `data/content.json` under `unity/Assets/Generated/`, with stable generated metadata. The content becomes `Resources/CanonicalContent.json`. No simulation source is copied or compiled a second way. Preparation must be rerun after relevant changes; `--check` compares source, configuration, and output hashes without rebuilding. These local artifacts and their manifest are ignored by Git.

Unity's versioned package manifest and lock file supply `com.unity.nuget.newtonsoft-json` 3.2.2. The NuGet build dependency remains Newtonsoft.Json 13.0.3. The inspected Unity package contains upstream 13.0.2, with the same strong-named assembly identity (`Newtonsoft.Json`, version `13.0.0.0`, public key token `30ad4fe6b2a6aeed`). Matching identity permits dependency resolution but does not establish behavior on every backend. Integration tests exercise the actual Unity package; [Unity integration](UNITY.md) records results and remaining platform checks. Do not stage the NuGet Newtonsoft DLL, demo, or headless test assemblies into Unity.

The Bootstrap component loads the staged content, invokes the existing production command for three successful batches and one rejected batch, and reports the resulting inventory. Unity tests and player verification cover the host boundary; standalone tests remain the main simulation regression suite. Runtime content distribution beyond this small `Resources` asset is deferred.

## Content contract

`data/content.json` declares `schemaVersion: 1` and definitions for goods, buildings, and recipes. IDs are stable, globally unique across entity kinds, and independent of display names. Recipes reference an existing building and existing goods. Definitions are data, not executable behaviors.

Python applies the JSON Schema and semantic checks. The C# loader implements the same accepted content contract without a runtime JSON Schema engine. Schema constraints alone are insufficient: reference integrity, global uniqueness, and strict JSON integer-token representation also require validation. For example, JSON Schema can consider `1.0` mathematically integral, but this content format rejects it and `1e0`; use `1`. Duplicate object keys and non-JSON numeric constants such as `NaN` or `Infinity` are rejected.

Content versioning is explicit, with only version 1 supported. There is no migration framework. If the contract changes, update schema, both validators, fixtures, generated output, and the relevant specifications together.

Version 1 also enforces the following restrictions:

- IDs match `[a-z][a-z0-9_]*` in full; names and descriptions are nonblank strings.
- Goods, buildings, recipes, recipe inputs, and recipe outputs are nonempty arrays.
- Quantities are integer JSON tokens from 1 through 2,147,483,647. Inventory state can additionally contain zero.
- A good appears at most once on each side of a recipe. A good may appear on both sides; no extra economic assumption forbids that relationship.
- Unknown properties are rejected at every object level. References must target the correct entity kind, not merely an existing ID.

The format accepts standard JSON only: comments, trailing commas, duplicate keys, and nonstandard constants are invalid.

## C# API and state ownership

The public types live in `SaltAndLedger.Simulation`:

- `ContentLoader.Load(string json)` returns an immutable `ContentCatalog` after validation.
- `ContentCatalog` exposes read-only lists `Goods`, `Buildings`, and `Recipes`, with `GetGood(id)`, `GetBuilding(id)`, and `GetRecipe(id)` lookup methods.
- Definitions expose `Id`, `Name`, and `Description`; recipes also expose `BuildingId`, `Inputs`, and `Outputs`. Each recipe quantity has `GoodId` and `Quantity`.
- `Inventory` is created for a catalog, optionally with initial quantities. `GetQuantity(id)` reads stock; `Snapshot()` returns a detached read-only view of all stock.
- `Production.TryRunBatch(inventory, buildingId, recipeId)` performs one explicit production transition. It returns `true` on success and `false` when inputs are insufficient. Invalid IDs, an incompatible building, and arithmetic overflow are rejected without mutation.

Inventory owns mutable quantities; the catalog provides immutable definitions. The production command computes and checks its result before applying it. The [maintained mechanics specification](MECHANICS.md) defines the behavior that generated content cannot explain.

```csharp
using System.Collections.Generic;
using SaltAndLedger.Simulation;

// The host supplies JSON text from data/content.json.
ContentCatalog content = ContentLoader.Load(json);
var inventory = new Inventory(content, new Dictionary<string, int>
{
    ["logs"] = 2,
    ["planks"] = 0
});

bool produced = Production.TryRunBatch(inventory, "sawmill", "saw_planks");
// produced == true; logs == 0; planks == 1.
```

### World runtime types

Canonical `BuildingDefinition` and recipe data remain immutable shared catalog entries. `BuildingInstance` identifies one actual runtime facility through `Id`, `BuildingId`, `RecipeId`, `Priority`, and `ProductionEnabled`. Multiple instances reference the same definitions without copying content. All instance properties are read-only. A world configuration change replaces the instance value in its next snapshot; it cannot modify canonical definitions.

| Type | Responsibility |
| --- | --- |
| `WorldState` | Validate one location and its instances, own the current immutable snapshot, and execute one atomic `Step` |
| `WorldSnapshot` | Expose detached world/accounts state; derive current marginal prices with `GetMarketQuote` and a read-only `PreviewTrade` |
| `BuildingInstance` | Hold stable runtime identity, canonical references, priority, and enabled flag |
| `SetProductionEnabledCommand` | Request one enabled-state change at the start of a step |
| `StepResult` | Describe the starting `Tick`, committed `After` snapshot, ordered `TradeAttempts`, and ordered `ProductionAttempts` |
| `ProductionAttempt` | Identify the instance, building definition, and recipe; report `Outcome` and `InventoryChanges` |
| `InventoryChange` | Describe one changed good with `GoodId`, `Before`, `After`, and `Delta` |
| `MarketConfiguration` | Copy initial market cash, trader ID/cash/inventory, and fixed/dynamic quote configurations; resolve canonical goods when constructing a world |
| `MarketQuote` | Immutable per-good fixed `Bid`/`Ask` or `InventoryPricing` parameters; `IsFixed` distinguishes them |
| `InventoryPricing` | Validated immutable `Linear` or `Scarcity` parameters; no clock, world reference, or mutable price state |
| `MarketPrice` | Derived `GoodId`, `MarketStock`, and nullable next marginal `Bid`/`Ask` for one snapshot |
| `TradePrice` | Exact proposed quantity, first/last marginal prices, total, and uniform-price flag |
| `TradeCommand` | Request a `Buy` or `Sell` from the trader's perspective, with a canonical good and positive `int` quantity |
| `TradingSnapshot` | Detached `MarketCash`, `TraderId`, `TraderCash`, `TraderInventory`, and immutable pricing configurations in `Quotes` |
| `TradeAttempt` | Requested quantity, nullable marginal-price breakdown, actual executed quantity/amount, explicit outcome, and both parties' goods/cash changes |
| `CashChange` | Immutable `long` before/after balances and checked signed delta |

`WorldState(content, locationId, buildings, initialQuantities = null, initialTick = 0, market = null)` validates and copies setup state. Existing source calls retain their behavior; without a market, `Trading` is null. `Snapshot()` exposes its immutable current snapshot; `Tick` and `LocationId` are also available directly. Initial quantities have the existing inventory rules. Runtime IDs use the same ASCII syntax as definition IDs but a separate identity namespace; building instance IDs are unique within the world. Priorities are signed integers. The building list has a stable priority-then-ordinal-ID order, and snapshot inventories and quotes sort by ordinal good ID.

```csharp
var world = new WorldState(content, "home_port", new[]
{
    new BuildingInstance("sawmill_b", "sawmill", "saw_planks"),
    new BuildingInstance("sawmill_a", "sawmill", "saw_planks")
}, new Dictionary<string, int> { ["logs"] = 2 });

StepResult first = world.Step();
// first.Tick == 0; first.After.Tick == 1.
// sawmill_a produces before sawmill_b encounters a resource shortage.
StepResult second = world.Step(new[]
{
    new SetProductionEnabledCommand("sawmill_a", false)
});
// Only sawmill_b attempts production in this step; stock is still exhausted.
```

### Commit boundary and ownership

`Step(commands = null, trades = null)` consumes and validates both complete command sequences before applying controls. It checks tick overflow, prepares private working state, applies controls in declared order, executes trades in declared order, then calls `Production.TryRunBatch` once for each enabled instance in priority/ID order. Stock/funds shortages are recorded normally. Invalid commands, an enumeration error, or arithmetic overflow throw and leave the old snapshot intact. A reentrant `Step` is rejected with `InvalidOperationException`; callers must still serialize access, since the world is not thread-safe.

The working inventories, cash, and flags are not exposed. Internal `TradingState` stages one trader inventory and two balances; the shared trade calculation computes a complete attempt and every affected balance before a transfer is applied. Snapshot previews invoke that same calculation without applying it. The existing working location inventory is the market stock, shared with production. There is no redundant mutable market inventory or generic economic entity framework. Public-market production ownership is an experimental prototype assumption; player-owned factories remain undecided.

Only after the complete next snapshot and step result exist does one snapshot assignment commit the world. Therefore an exception after an earlier successful trade or batch rolls back goods, cash, controls, and clock together. Constructor input mutation and retained results cannot change the live world. No mutable `Inventory` is returned from the world API. The world does not retain the initial configuration: current account state and immutable quotes live in its current snapshot.

```csharp
var market = new MarketConfiguration(new[]
{
    new MarketQuote("logs", bid: 2, ask: 3),
    new MarketQuote("planks", bid: 4, ask: 5)
}, initialMarketCash: 20, traderId: "trader", initialTraderCash: 7,
    initialTraderInventory: new Dictionary<string, int> { ["logs"] = 2 });
var tradingWorld = new WorldState(content, "harbor", new[]
{
    new BuildingInstance("sawmill_a", "sawmill", "saw_planks")
}, market: market);
var sale = tradingWorld.Step(trades: new[] { new TradeCommand(TradeDirection.Sell, "logs", 2) });
// Market cash 16, trader cash 11; the sold logs produce 1 market plank this tick.
var purchase = tradingWorld.Step(trades: new[] { new TradeCommand(TradeDirection.Buy, "planks", 1) });
// Market cash 21, trader cash 6; trader owns the plank. No logs remain to produce.
```

The Unity Edit Mode trade test exercises this example with the staged library. Quotes are scenario/runtime data; no canonical schema, good definition, or documentation-generation change is necessary. Detailed numeric bounds and rejection precedence live in [Mechanics](MECHANICS.md).

### Pricing calculation and visibility

`new MarketQuote(goodId, bid, ask)` remains the fixed-price path. `new MarketQuote(goodId, new InventoryPricing(...))` selects linear or piecewise scarcity pricing for that good. These are small concrete configuration modes, not a pricing plugin interface. Configurations are immutable and copied into the snapshot's ordered quote collection. Their legacy `Bid`/`Ask` getters throw for dynamic configurations; callers obtain state-dependent prices through `snapshot.GetMarketQuote(goodId)` instead of accidentally displaying an initial or cached price.

The pure pricing calculation maps consecutive inventory transitions to bid/ask values. Buying and reversing that purchase traverse the same edges in opposite directions. Quantities are summed exactly rather than charged at the first marginal quote. Mathematical floor and final-side bounds are explicit; `BigInteger` is a .NET Standard API used only for intermediate arithmetic, with no new NuGet or Unity package. At most two affine segments, analytic bound crossings, and a logarithmic floor-sum algorithm keep work independent of the requested unit count. [Mechanics](MECHANICS.md#inventory-sensitive-pricing-hypotheses) specifies formulas, limits, and rejection precedence.

`snapshot.PreviewTrade(command)` uses the same account checks, price summation, and result construction as execution. It does not advance time or mutate anything. Both paths expose first/last marginal prices, proposed total, and executed quantity/amount; in a preview these fields and the balance changes are hypothetical. A shortage that prevents dynamic pricing has no price breakdown. Fixed attempts keep their former `UnitPrice` and `TotalAmount` values. For dynamic attempts, `UnitPrice` is only a compatibility alias for the first marginal price, and its product with quantity need not equal the total.

Current marginal quotes are derived from each snapshot's authoritative inventory. Production therefore affects the next completed snapshot's quotes without a separate update phase. Retained snapshots retain their own historical prices; cash and stock feasibility for a whole request belongs to `PreviewTrade`. The marginal quote omits ask at zero market stock and bid at maximum stock. It does not reserve goods or guarantee funds.

This copies small runtime state per tick to make the failure contract understandable. It has allocation and copying costs that should be measured before adapting it for large worlds; no performance target is claimed. There is no transaction framework, event bus, retained event history, save format, or replay engine. Hosts may retain selected `StepResult` objects for their own diagnostics. The world itself retains only its current snapshot.

## Determinism and headless execution

For a given validated catalog, initial world state, pricing configuration, and ordered commands per tick, the implemented world produces the same state and structured outcomes. A nonnegative 64-bit integer tick advances once per successful step. It represents abstract simulation time, with no hours, days, or real-time conversion. Exact integer pricing and checked public numeric bounds prevent tick, stock, cash, or transaction-total overflow from silently changing state.

Production order is ascending priority then ordinal instance ID, independent of input collection order. Commands targeting the same instance deliberately use their declared order, with the last enabled-state command winning. Attempt order and changed-good order are explicit; callers do not need dictionary enumeration accidents to compare runs. Fixed production order can starve later buildings and makes no fairness guarantee.

`Step` has no implicit loop; the world demo calls it five times, repeats the scenario, and compares results with both identical and reversed building input collections. The market demo uses four ticks and repeats with reversed quote/stock setup order. Several steps use ordinary sequential calls, not a separate batch API. The simulation uses no random values, wall-clock inputs, Unity frames, or background scheduling. Trading precedes production; future arrival and consumption phases remain unresolved, as do seed management, save formats, and durable replay compatibility. The current API assumes one caller; concurrent mutation is not supported.

The `--pricing` host compares fixed, linear, and scarcity modes through the real world API. It records model parameters, previews, outcomes, stock/price movement, and cash changes in a deterministic JSON report, with a read-only `--check` mode for freshness. This report is a host-owned experiment artifact under `docs/experiments/`; it is separate from canonical content generation and `generated/encyclopedia.json`. [Pricing experiments](PRICING_EXPERIMENTS.md) explains the scenarios and conclusions. The runner does not imply a large-world throughput target or a verified thousands-of-runs benchmark.

## Documentation pipeline

The generator validates the source before deriving output. It owns all of `docs/reference/` and only `generated/encyclopedia.json` under `generated/`. Authored pages stay outside the generated directory.

| Output | Contents |
| --- | --- |
| `docs/reference/index.md` | Index of generated entities |
| `docs/reference/goods/{id}.md` | Good definition and consuming/producing recipe links |
| `docs/reference/buildings/{id}.md` | Building definition and recipe links |
| `docs/reference/recipes/{id}.md` | Building, inputs, outputs, and links to related definitions |
| `generated/encyclopedia.json` | Sorted reference entities with IDs, source facts, wiki paths, and relationship IDs |

The structured output declares `formatVersion: 1` and `sourceSchemaVersion: 1`. Each entity has a `wikiPath` relative to `docs/`; goods add `consumedByRecipeIds` and `producedByRecipeIds`, buildings add `recipeIds`, and recipes retain `buildingId`, `inputs`, and `outputs`. Entity arrays sort by stable ID; recipe quantities sort by good ID. This is a reference artifact, not a saved game or a replacement runtime content format.

`python tools/generate_docs.py` writes the expected artifacts and removes stale files within its owned reference directory. `python tools/generate_docs.py --check` is read-only and fails for missing, changed, or unexpected files in that directory or a stale encyclopedia artifact. Both generation and validation accept `--source` for an alternate content file, defaulting to `data/content.json`; generation still uses the standard repository output paths.

Generated pages cannot define production time, prices, or behavior absent from the data. [Mechanics](MECHANICS.md) explicitly maintains those rules. Tests and freshness checks keep generated facts synchronized, while review keeps authored specifications aligned with behavior.

## Boundaries and limitations

Fixed quotes remain the accounting baseline; linear and scarcity curves are experimental stock-sensitive alternatives. No automatic stock/cash replenishment, multiple ports, ships, trade routes, world generation, objectives, modifiers, progression, random events, competing merchants, piracy, combat, multiplayer, automation policy, or elaborate UI exists here. Keep those in the provisional [roadmap](ROADMAP.md), not in unused foundation code.

Headless compilation and tests exercise the portable library and pipeline. Unity import, Editor behavior, Mono player execution, and IL2CPP player execution are separate verification points; follow [Unity integration](UNITY.md) and report which checks actually ran.
