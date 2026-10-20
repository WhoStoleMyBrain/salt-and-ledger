# Implemented architecture

Phase 0 separates content, simulation, host presentation, and reference generation using concrete modules. Later milestones add atomic finite markets, exact stock-sensitive pricing, maritime ownership, stateless route policies, and the TradingLab UI. Task 1.5 adds a focused regional owner for multiple ships/ports, instant actions, economic days, scheduled sources/sinks, and a map-first ArchipelagoLab. Task 1.6 extends that owner with company cash, private facilities and storage, bounded procurement/sales policies, and an optional supply challenge. Task 1.7 adds FIFO material accounting, explicit industrial route stages, deterministic starting charters and three terminal challenge choices, plus a complete run host and offline reference. All worlds share pricing, transaction, and production operations. Older public APIs and tick semantics remain intact; no independent per-port worlds or speculative service framework are introduced.

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
                            +-- MaritimeWorldState.Step --> immutable MaritimeStepResult
                            |       +-- two local markets, one trader account, one ship, one clock
                            |       +-- same trade calculator, pricing, and production operation
                            |       +-- route policy --> snapshot decision --> normal commands
                            |       +-- manual/automated shipping comparisons / headless and Unity tests
                            |       +-- Unity TradingLab session --> UI Toolkit controller / UXML / USS
                            +-- RegionalWorldState --> instant commits / atomic AdvanceDay
                            |       +-- ports/company accounts/ship cargo, day and state revision
                            |       +-- shared transactions/production/policy evaluator
                            |       +-- private facilities/storage/policies and optional challenge
                            |       +-- FIFO costs / committed events / industrial route stages
                            |       +-- RunScenario --> charter initialization / terminal results
                            |       +-- EnterpriseScenario --> enterprise report / Unity ArchipelagoLab
                            |       +-- ArchipelagoScenario --> historical two-good report
                            +-- original console demo / Unity Bootstrap smoke
```

## Modules and dependencies

| Module | Responsibility | Dependencies |
| --- | --- | --- |
| `src/SaltAndLedger.Simulation` | Canonical content, shared economic calculations, legacy/local/regional owners, company accounts, private industry, challenge rules, schedules, physical flows, policies, and shared scenarios | `netstandard2.1`, C# 8, Newtonsoft.Json 13.0.3 |
| `samples/SaltAndLedger.Demo` | Read content; demonstrate production/world/trading/shipping/automation; compare pricing hypotheses and emit reproducible experiment reports | `net10.0`, simulation project |
| `tests/SaltAndLedger.Simulation.Tests` | Headless tests of content, production, trading/accounting, pricing/preview invariants, scheduling, state ownership, determinism, and rollback | `net10.0`, simulation project, test dependencies |
| `unity` | Map-first ArchipelagoLab, legacy TradingLab, Bootstrap smoke, UI Toolkit hosts, integration tests, and separate build entry points | Unity 6000.6.6f1, staged simulation DLL, Unity Newtonsoft package 3.2.2 |
| `data` | Canonical content and Draft 2020-12 JSON Schema | No host or presentation dependency |
| `tools` | Validate authoring data; generate reference artifacts; prepare Unity runtime assets | Python, dependencies in `requirements.txt`; .NET 10 for Unity preparation |
| `tests/python` | Schema, semantic validator, and generation tests | Python test runner and pipeline dependencies |
| `docs` and `mkdocs.yml` | Authored specifications and external reference site | MkDocs |
| `.github/workflows` | Headless build, test, validation, freshness, site, and Unity staging checks | .NET 10 SDK and Python; no Unity Editor |

The simulation does not reference `UnityEngine`, access an Editor API, or decide where a host stores its content. The console host supplies JSON from a file; the Unity host supplies the same JSON from a `TextAsset`. The existing Unity project and its template packages remain in place; the integration adds no economic rules.

## Toolchain and Unity dependency boundary

`global.json` selects stable .NET 10 SDKs starting at 10.0.100 with `latestFeature` roll-forward within 10.0. This lets developers receive SDK servicing updates without adopting a later major runtime. The simulation's `netstandard2.1` and C# 8 targets remain unchanged. All three standalone projects have NuGet lock files, and CI restores with `--locked-mode`.

`python tools/prepare_unity.py` validates canonical content, builds the existing simulation project in Release, and stages only `SaltAndLedger.Simulation.dll`, `data/content.json`, and the current generated encyclopedia under `unity/Assets/Generated/`, with stable generated metadata. The content becomes `Resources/CanonicalContent.json`; the reference becomes `Resources/Encyclopedia.json`. Preparation rejects stale or missing generated reference data before building. No simulation source is copied or compiled a second way. Preparation must be rerun after relevant changes; `--check` compares source, configuration, and output hashes without rebuilding. These local artifacts and their manifest are ignored by Git.

Unity's versioned package manifest and lock file supply `com.unity.nuget.newtonsoft-json` 3.2.2. The NuGet build dependency remains Newtonsoft.Json 13.0.3. The inspected Unity package contains upstream 13.0.2, with the same strong-named assembly identity (`Newtonsoft.Json`, version `13.0.0.0`, public key token `30ad4fe6b2a6aeed`). Matching identity permits dependency resolution but does not establish behavior on every backend. Integration tests exercise the actual Unity package; [Unity integration](UNITY.md) records results and remaining platform checks. Do not stage the NuGet Newtonsoft DLL, demo, or headless test assemblies into Unity.

The Bootstrap component loads the staged content, invokes the existing production command for three successful batches and one rejected batch, and reports the resulting inventory. Unity tests and player verification cover the host boundary; standalone tests remain the main simulation regression suite. Runtime content distribution beyond this small `Resources` asset is deferred.

## Content contract

`data/content.json` declares `schemaVersion: 1` and definitions for goods, buildings, and recipes. IDs are stable, globally unique across entity kinds, and independent of display names. Recipes reference an existing building and existing goods. Definitions are data, not executable behaviors.

Python applies the JSON Schema and semantic checks. The C# loader implements the same accepted content contract without a runtime JSON Schema engine. Schema constraints alone are insufficient: reference integrity, global uniqueness, and strict JSON integer-token representation also require validation. For example, JSON Schema can consider `1.0` mathematically integral, but this content format rejects it and `1e0`; use `1`. Duplicate object keys and non-JSON numeric constants such as `NaN` or `Infinity` are rejected.

Content versioning is explicit, with only version 1 supported. Task 1.5 evolves it additively with optional recipe `kind`: omitted or `transformation` retains the original input-bearing contract; `extraction` requires empty inputs and positive outputs. Existing version-1 content remains accepted by the new reader. An older strict reader does not understand the new field, so stage the updated DLL and content together. There is no migration framework. Schema, both validators, fixtures, generated output, and specifications change together.

Version 1 also enforces the following restrictions:

- IDs match `[a-z][a-z0-9_]*` in full; names and descriptions are nonblank strings.
- Goods, buildings, recipes, and recipe outputs are nonempty arrays. Transformation inputs are nonempty; extraction inputs must be empty.
- Quantities are integer JSON tokens from 1 through 2,147,483,647. Inventory state can additionally contain zero.
- A good appears at most once on each side of a recipe. A good may appear on both sides; no extra economic assumption forbids that relationship.
- Unknown properties are rejected at every object level. References must target the correct entity kind, not merely an existing ID.

The format accepts standard JSON only: comments, trailing commas, duplicate keys, and nonstandard constants are invalid.

## C# API and state ownership

The public types live in `SaltAndLedger.Simulation`:

- `ContentLoader.Load(string json)` returns an immutable `ContentCatalog` after validation.
- `ContentCatalog` exposes read-only lists `Goods`, `Buildings`, and `Recipes`, with `GetGood(id)`, `GetBuilding(id)`, and `GetRecipe(id)` lookup methods.
- Definitions expose `Id`, `Name`, and `Description`; recipes also expose `BuildingId`, `Kind`, `Inputs`, and `Outputs`. Each recipe quantity has `GoodId` and `Quantity`.
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

### Single-location world runtime types

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

The working inventories, cash, and flags are not exposed. Internal `TradingState` stages one trader inventory and two balances; the shared trade calculation computes a complete attempt and every affected balance before a transfer is applied. Snapshot previews invoke that same calculation without applying it. The existing working location inventory is the market stock, shared with production. There is no redundant mutable market inventory or generic economic entity framework. Public-market production remains the legacy prototype's ownership model; Task 1.6 adds private factories within the separate regional owner below.

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

## Maritime ownership and shared calculations

`MaritimeWorldState` is a focused second world owner. It preserves the original `WorldState` constructor, commands, demos, and outcomes. It does **not** contain two local worlds or synchronize their clocks. [ADR 015](DECISIONS.md#adr-015-own-the-two-port-prototype-in-one-maritime-world) records the ownership choice selected before implementation.

| Type | Responsibility |
| --- | --- |
| `MaritimeWorldState` | Own exactly two ports, one trader, one ship, and a single atomic global tick |
| `PortConfiguration` | Copy one port's ID, initial stock/cash, quotes, and local buildings |
| `ShipConfiguration` | Copy one ship's ID, positive capacity, initial dock, and optional cargo |
| `MaritimeWorldSnapshot` | Expose ordered ports, shared trader ID/cash, ship, tick, and route duration; derive quotes and hypothetical previews for an explicit port |
| `PortSnapshot` | Expose one port's independent market inventory, cash, pricing configurations, and building controls |
| `ShipSnapshot` | Expose cargo and used capacity, docked port or voyage origin/destination, and remaining travel ticks |
| `PortTradeCommand` | Select the intended docked port and the existing direction/good/quantity trade request |
| `PortProductionCommand` | Change one building's enabled state within its explicit port |
| `SailCommand` | Request departure to the other port; at most one per step |
| `MaritimeStepResult` | Return the starting tick, complete next snapshot, port-qualified trade/production attempts, and ordered movement events |

Construction takes the catalog, exactly two `PortConfiguration` values, trader ID and starting cash, one `ShipConfiguration`, a positive `travelTicks`, and an optional nonnegative initial tick. All setup collections are copied. Port IDs are unique; building IDs are unique within their port and are addressed together with that port ID. The ship's starting port must exist. Snapshots sort ports by ordinal ID and retain each port's established priority/instance-ID building order.

The trader's cash exists once at world level. The ship's `Cargo` is the trader's sole maritime goods inventory. A port owns no trader inventory or cash copy: it contains only market stock, market cash, quotes, and local buildings. Every item occupies one cargo unit; checked aggregate capacity prevents separate goods from bypassing the limit. The route is one bidirectional connection with a common configured duration, not a graph or pathfinding system.

The shared internal `TradeCalculator` calculates price, stock/funds/capacity outcomes, and all affected balances for both preview and execution. `TradingState` uses it for the old local world; the maritime step supplies the selected port and the one working cargo/cash account. It invokes the existing immutable `MarketQuote` and `InventoryPricing`; no port-specific pricing or accounting algorithm exists. Each local production attempt still calls `Production.TryRunBatch` on that port's inventory. The worlds share economic operations while retaining their explicit scheduling and ownership code.

`Step(commands = null, trades = null, sailCommands = null)` fully validates all three sequences against the starting snapshot, checks tick overflow, and stages every port plus the ship and trader privately. It applies controls, executes trades at the starting dock in declared order, applies a valid departure, runs production by ordinal port ID and local building order, then advances travel once. It constructs all snapshots and results before the only current-state assignment. Any exception in either port rolls back every staged inventory, balance, control, movement, and the tick. Expected resource/capacity rejections still permit the rest of the tick to finish.

`snapshot.GetMarketQuote(portId, goodId)` derives local prices from the selected port's stock. `snapshot.PreviewTrade(portId, command)` is deliberately available remotely, including while sailing: it hypothetically applies the current cargo, shared cash, capacity, and selected market's rules **as if docked there**. It cannot authorize execution, reserve stock, or predict arrival-time prices. Actual `PortTradeCommand` execution requires matching the ship's starting dock. Neither quote inspection nor preview mutates state.

`TradeAttempts` wrap the existing `TradeAttempt` with `PortId`; production results similarly identify the port. Departure, progress, and arrival records contain ship ID, origin, destination, and before/after remaining travel ticks. A one-tick route emits all three in that step. The world retains no event history. Detached earlier snapshots preserve their port balances, historical quotes, cargo, and voyage state.

## Route automation over the maritime boundary

[ADR 016](DECISIONS.md#adr-016-evaluate-route-policies-without-owning-economic-state) selects a stateless decision layer. Location and distinct outbound/return cargo identify the current stage, so there is no mutable route state requiring separate rollback. `MaritimeWorldState` still owns every inventory, account, building, voyage, and tick. No economic or movement implementation is duplicated in the policy or Unity host.

| Type | Responsibility |
| --- | --- |
| `RouteLeg` / `TradeRoutePolicy` | Validate immutable commodity, shipment, marginal price, reserve, and empty-return choices |
| `RoutePolicyEvaluator` | Read one committed snapshot; choose at most one normal trade and optional departure, or a structured wait/pause |
| `AutomationDecision` | Retain the exact source snapshot, stage/action/reason, commands, selected quantity, and successful hypothetical preview |
| `TradeRouteAutomation` | Reject stale decisions, freshly preview the trade, and exclusively submit those commands through normal `Step` |
| `AutomationStep` | Pair one decision with its committed maritime result |
| `AutomationRunner` / `AutomationRunResult` | Optional synchronous host observation with finite tick/idle bounds, stop reason, and no retained full history |

The evaluator uses bounded binary search over existing monotone prices, stock, capacity, buyer cash, and reserve. It calls the same marginal edge function and exact sum as trading. A shared internal budget comparison permits large exact search probes without premature public-total conversion; the final selected trade passes the existing snapshot preview. Public receiver-balance overflow remains exceptional. [Mechanics](MECHANICS.md#configurable-route-policy) specifies minimum-shipment semantics, partial sales, cargo intervention, and reason precedence; the [automation guide](AUTOMATION.md) explains the player-facing choices and measured scenarios.

`TradeRouteAutomation.Execute` requires identity with the current authoritative snapshot and permits no unrelated commands or callback between preview and step. This keeps a same-tick purchase/departure aligned without redesigning manual rejection behavior. A host must serialize access, discard old decisions when replacing policy, and choose manual or automated execution per step. Policy enablement is host-owned; explicit paused execution is an empty step, while the bounded runner stops before a pause. Neither pause nor configuration replacement modifies economic state.

The runner counts committed trades, movement, and changed production as progress; it separately limits elapsed steps and consecutive no-progress ticks. It is a reusable headless host helper in the portable assembly, not a new authority or background scheduler. A reporting callback runs after commit and must not mutate the world. Its side effects and failures cannot be rolled back by an already committed step. Hosts decide what finite history, if any, to retain.

The `--shipping` and `--automation` demos share the small `ShippingScenario` factory. The latter asserts equivalent manual/automatic states each tick, threshold-dependent outcomes, bounded liquidity waits, and repeated/reordered/culture-independent transcripts. It produces no canonical entries or generated report requiring another pipeline.

## TradingLab presentation host

[ADR 017](DECISIONS.md#adr-017-host-the-trading-laboratory-with-explicit-ui-actions) keeps the playable lab outside the economic authority. `Assets/Scenes/TradingLab.unity` initializes the prepared canonical content and a small Unity-host scenario factory. The factory uses the existing configuration types and the shipping comparison's parameters; it does not reference the .NET 10 demo assembly. Bootstrap and its original production tests remain separate.

The host session owns the current `MaritimeWorldState`, selected scenario, immutable policy, enablement flag, and bounded presentation history. It exposes snapshots and previews to a UI Toolkit controller. UXML and USS define the interface; the controller translates selections and validated input into session calls, then refreshes from committed state. The Panel Settings asset uses `TradingLabTheme.tss`, which imports Unity's built-in default runtime theme with `unity-theme://default`; custom styles remain in `TradingLab.uss`. Canonical goods and recipe descriptions come from `ContentCatalog`. No host code calculates prices, chooses automated quantities, transfers inventory/cash, executes production, or advances a separate movement clock.

Manual actions submit existing `PortTradeCommand` and `SailCommand` values through `Step`. Combined buy/depart requires a fresh successful preview of the current snapshot before a single command batch. Automated actions evaluate the current immutable policy and use `TradeRouteAutomation` or the bounded runner, with no same-tick manual orders. Policy replacement discards the old pending decision. Selection, preview, parameter editing, policy application, and enablement changes are passive; only an explicit step action advances the world. Reset creates a new scenario world rather than repairing an existing economy.

The map reads dock/voyage state and remaining ticks from the snapshot; there is no frame-driven interpolation or autonomous simulation. The market panel distinguishes next marginal bid/ask from a complete order's first/last prices and exact total. Remote previews remain hypothetical and do not enable remote execution. Structured trades, movement, production, and decisions feed a finite host activity list; the world still retains no history. The displayed cash change is separate from realized gross profit, which requires an empty-cargo accounting interval. There is no speculative cargo valuation.

The lab deliberately has one fixed connection, one ship, three starting comparisons, and a narrow outbound policy form. [Playtest](PLAYTEST.md) documents its controls and verification flows. Scene/control tests verify wiring and economic outcomes; interactive screenshots and player execution establish a separate, explicitly recorded scope in [Unity integration](UNITY.md).

## Determinism and headless execution

For a given validated catalog, initial world state, pricing configuration, and ordered commands per tick, the implemented world produces the same state and structured outcomes. A nonnegative 64-bit integer tick advances once per successful step. It represents abstract simulation time, with no hours, days, or real-time conversion. Exact integer pricing and checked public numeric bounds prevent tick, stock, cash, or transaction-total overflow from silently changing state.

Production order is ascending priority then ordinal instance ID, independent of input collection order. Commands targeting the same instance deliberately use their declared order, with the last enabled-state command winning. Attempt order and changed-good order are explicit; callers do not need dictionary enumeration accidents to compare runs. Fixed production order can starve later buildings and makes no fairness guarantee.

`Step` has no implicit loop; the world demo calls it five times, repeats the scenario, and compares results with both identical and reversed building input collections. The market demo uses four ticks and repeats with reversed quote/stock setup order. The shipping demo compares two six-tick manual round trips and verifies both repeated and reversed-port input transcripts. Several steps use ordinary sequential calls, not a separate batch API. The simulation uses no random values, wall-clock inputs, Unity frames, or background scheduling. Trading precedes production; maritime travel progresses after production, and arrival enables trading on the next step. These legacy steps have no consumption phase; the regional owner specifies its physical sink phase below. Seed management, save formats, and durable replay compatibility remain unresolved. The current APIs assume one caller; concurrent mutation is not supported.

The `--pricing` host compares fixed, linear, and scarcity modes through the real world API. It records model parameters, previews, outcomes, stock/price movement, and cash changes in a deterministic JSON report, with a read-only `--check` mode for freshness. This report is a host-owned experiment artifact under `docs/experiments/`; it is separate from canonical content generation and `generated/encyclopedia.json`. [Pricing experiments](PRICING_EXPERIMENTS.md) explains the scenarios and conclusions. The runner does not imply a large-world throughput target or a verified thousands-of-runs benchmark.

## Documentation pipeline

The generator validates the source before deriving output. It owns all of `docs/reference/` and only `generated/encyclopedia.json` under `generated/`. Authored pages stay outside the generated directory.

| Output | Contents |
| --- | --- |
| `docs/reference/index.md` | Index of generated entities |
| `docs/reference/goods/{id}.md` | Good definition and consuming/producing recipe links |
| `docs/reference/buildings/{id}.md` | Building definition and recipe links |
| `docs/reference/recipes/{id}.md` | Building, inputs, outputs, and links to related definitions |
| `docs/reference/mechanics/{id}.md` | Shared authored player explanations and validated related links |
| `docs/reference/chains.md` | Canonical input/output relationships with recipe and building links |
| `generated/encyclopedia.json` | Sorted canonical and mechanics entries, common presentation fields, typed relationships and wiki paths |

The structured output declares `formatVersion: 2`, `sourceSchemaVersion: 1`, and `sourceHelpSchemaVersion: 1`. Each canonical or mechanics entry includes `paragraphs` and `relatedIds` for a common offline presentation. Shared authored mechanics come from schema-validated `data/player-help.json`; links resolve against globally unique canonical and help IDs. Each entity has a `wikiPath` relative to `docs/`; goods add `consumedByRecipeIds` and `producedByRecipeIds`, buildings add `recipeIds`, and recipes retain `buildingId`, `kind`, `inputs`, and `outputs`. The effective recipe kind is emitted even when its transformation default was omitted in source. Entity arrays sort by stable ID; recipe quantities sort by good ID. This is a reference artifact, not a saved game or a replacement runtime content format.

`python tools/generate_docs.py` writes the expected artifacts and removes stale files within its owned reference directory. `python tools/generate_docs.py --check` is read-only and fails for missing, changed, or unexpected files in that directory or a stale encyclopedia artifact. Both generation and validation accept `--source` for an alternate content file, defaulting to `data/content.json`; generation still uses the standard repository output paths.

Generated canonical pages cannot invent production time or prices absent from content. Generated player-help pages render their separate authored source. [Mechanics](MECHANICS.md) maintains the exact behavioral contracts; [Website](WEBSITE.md) describes shared generation, local use, and optional manual publication. Tests and freshness checks keep generated facts synchronized, while review keeps authored specifications aligned with behavior.

## Regional ownership and the map host

`RegionalWorldState`, introduced in Task 1.5 and extended in Tasks 1.6/1.7, owns local markets, company cash, ship cargo, private factory stock, historical accounting, schedules, policies/stages, voyages, optional challenge progress, day, and revision in one immutable current snapshot. It never composes separately ticking `WorldState` instances. Instant operations and one complete day use private staged state and a single final assignment; exceptions cannot partially commit an actor action or economic phase.

| Type | Responsibility |
| --- | --- |
| `RegionalPortConfiguration` / `RegionalShipConfiguration` | Immutable authored ownership, positions, priorities, policies, starting finite accounts, facilities, and demand |
| `SeaRoute` | Undirected integer distance and integer duration calculation for a ship speed |
| `ScheduledFacilityConfiguration` / `ConsumptionConfiguration` | Canonical references and explicit runtime cadence/output ceilings or physical demand |
| `RegionalWorldSnapshot` | Ordered ports, companies, ships, private facilities, current day/revision, challenge progress, quotes/previews, voyage durations, and derived dock counts |
| `RegionalResult` | Structured ship/warehouse trades, public/private production, construction, transfers, consumption, movements, decisions, and before/after snapshots for one committed operation |
| `RegionalAutomation` | Adapt an actor's two selected ports/account/cargo into the existing stateless route evaluator |
| `ArchipelagoScenario` | Historical two-good four-port configuration and player policy used by the preserved regional experiment |
| `ArchipelagoExperiments` | Console-only sixty-day strategies, deterministic JSON, flow/accounting assertions, and freshness checking |
| `EnterpriseScenario` | Shared six-good configuration, company capital, private blueprints/policy presets, and optional charter used by the current host and experiment |
| `EnterpriseExperiments` | Console-only ninety-day enterprise strategies and terminal challenge trials, deterministic JSON, complete cash/physical-flow assertions, and freshness checking |
| `ArchipelagoLabSession` | Main-thread host selection, playback, command submission, and bounded recent activity |
| `ArchipelagoLabController` / `ArchipelagoMap` | UI Toolkit controls, context rendering, map selection, and presentation-only interpolation |

The regional trade path calls the shared `TradeCalculator`; scheduled batches call `Production.TryRunBatch`; actor decisions call the existing policy evaluator through a detached two-port view. The view introduces no second economic owner or duplicate pricing/accounting algorithm. `RouteLeg` gains an optional per-purchase expenditure cap whose default preserves legacy behavior. Region-specific threshold gating and bounded same-day reevaluation wrap ordinary decisions.

`ArchipelagoLab.unity` is separate from TradingLab and Bootstrap. Its UXML/USS provides a dominant maritime map, selected-object context, contextual trade and private-industry modals, collapsible route/public-industry detail, and a bounded event display. Ports/ships/schedules/pricing read from shared definitions and snapshots. Frames schedule whole-day requests through one host; floating-point interpolation draws committed movement without changing simulation position or timing. The [mechanics specification](MECHANICS.md#regional-world-and-instant-commands) defines ordering, scheduling, revision, rollback, and playback precisely.

## Company ownership, private industry, and challenge

Task 1.6 extends the existing regional owner. `CompanyConfiguration`/`CompanySnapshot` identify one cash account per owner; ships and private facilities refer to that ID. Working ships no longer own mutable cash. `RegionalShipSnapshot.Cash` projects the company balance for API compatibility. Legacy constructor calls infer distinct company accounts from existing ship configuration; explicit accounts require zero ship InitialCash and may own multiple ships. Accounting sums companies once.

`IndustryBlueprint` references canonical building/recipe definitions and runtime construction cost, cadence, eligible ports, finite capacity, and optional output ceiling. Each `PrivateFacilitySnapshot` has separate owned inventory, controls, immutable policy, schedule, cumulative physical production, purchased-stock expenditure, and actual sale revenue. Construction, warehouse trades, private production, and transfers have structured result collections alongside the unchanged public/ship collections.

The world stages these additions in the same private working copy and commits once. Construction transfers company cash to the local market and schedules the first batch without producing goods. Transfers enforce ownership, docking, stock, and capacity. Local warehouse trades reuse TradeCalculator; factory batches reuse Production.TryRunBatch. `TradeQuantityCalculator` uses bounded binary search over the existing exact quote sums for UI maximums and factory policy quantities, including receiver cash/stock headroom. It does not alter legacy route selection semantics or manual overflow behavior.

Daily private procurement precedes public/private production, and optional private sales precede consumption. Policies never own state or execute unbounded zero-time chains. Explicit local facility management is allowed while the ship is elsewhere; physical transfers remain dock-bound. [Mechanics](MECHANICS.md) specifies the complete phase order and failure rules.

`ChallengeConfiguration`, `ChallengeSnapshot`, and internal `ChallengeRules` add one optional net-delivery objective to the same staged state. Only completed eligible ship/warehouse market trades update the signed counter. Evaluation occurs after the whole operation, including final company cash and day; terminal states reject economic mutation. No separate clock, external delivery ledger, or Unity-side victory calculation exists.

`EnterpriseScenario` is shared by the expanded ArchipelagoLab and `EnterpriseExperiments`. The historical `ArchipelagoScenario` uses `LegacyContent.Timber`, a projection retaining the original immutable canonical objects, so adding goods does not change old fixture/report semantics. The original full canonical loader still validates all six goods and their reference graph.

The trading and industry overlays are partial presentation controllers over the same session. Exact proposals and maximum quantities come from snapshots, and trade submission binds the source snapshot. Decimal average price is display-only division. Historical cost now comes from the authoritative FIFO accounting component described below. Neither a display average nor a current market quote is fed back into historical accounting.

## Accounting inside the regional commit

[ADR 024](DECISIONS.md#adr-024-attribute-material-cost-with-fifo-open-lots) extracts the focused `WorkingAccounting` component without moving authority out of `RegionalWorldState`. The world's private working copy contains both physical stores and accounting state. Successful trades, transfers, private batches, and construction call accounting hooks; failed/rejected actions cannot publish an accounting-only change. Snapshot/result construction precedes the same single current-state assignment.

| Type | Responsibility |
| --- | --- |
| `InventoryCostLot` | Original quantity/cost, stable lot ID, current interval, origin, and provenance |
| `GoodCostSnapshot` / `InventoryCostSnapshot` | Ordered open FIFO slices and held basis for a company-owned store |
| `CompanyFinancialSnapshot` | Actual purchases, revenue, released/held basis, construction, margin, and physical quantity aggregates |
| `AccountingSnapshot` | Immutable current company/store accounting and next lot identity |
| `EconomicEvent` / `EconomicGoodsMovement` | One operation's committed ownership, cash, cost, and gross transformation evidence |
| `WorkingAccounting` | Private mutable lot queues and aggregates, discarded on exceptional failure |

Store IDs are `ship:<id>` and `facility:<id>`. FIFO follows arrival order within each store/good; transfers append preserved original intervals. Prefix-floor cost allocation uses exact wider intermediates and checked public totals. Production withdraws gross canonical inputs before allocating their material cost across ordinal output units. This handles multi-input, multi-output, and same-good input/output recipes without current-price estimates. Grants retain zero actual expenditure and explicit provenance; capital remains separate.

The world retains only live lots and authoritative aggregates. `RegionalResult.EconomicEvents` returns the just-committed operation, stamped with its final day/revision. Hosts can build bounded financial history or a finite experiment journal, but cannot mutate the authoritative snapshot. No distributed event sourcing or replay promise is introduced. The [mechanics contract](MECHANICS.md#historical-inventory-costs-and-committed-events) defines exact allocation, reconciliation, and failure semantics.

## Explicit industrial policy stages

[ADR 025](DECISIONS.md#adr-025-store-industrial-route-direction-explicitly) adds `IndustrialRoutePolicy`, `IndustrialRouteLeg`, and a pure `IndustrialRouteEvaluator`. It does not replace the historical trade policy or its legacy timing. A regional ship stores either the existing market route or the industrial route, plus an explicit acquisition/delivery stage for each direction.

Each decision identifies the exact snapshot, current/next stage, ordinary trade/transfer/departure commands, and a readable wait/pause reason. `RegionalDecision.IndustrialDetail` carries it through existing actor dispatch. The world freshly previews market transactions and invokes the existing physical/financial operations. Warehouse routes can carry the same good in both directions because stage is authoritative rather than inferred from cargo.

Configuration, ownership validation, docking, finite capacity, retained cargo, numeric headroom, exact pricing, and the four-action bound remain explicit. Stage changes are part of the staged commit and roll back with accounting, cash, inventories, and voyages. A different industrial policy requires an empty ship at one of its ports; toggling or reapplying the same policy retains progress. No route discovery, arbitrary waypoint interpreter, or fleet optimizer is added.

## Fixed runs and result ownership

`RunScenario` is a small catalog of three `StartingCharter` values and three `RunChallenge` definitions over the same handcrafted `EnterpriseScenario`. It initializes cash, ship configuration/cargo, and optional `PrivateFacilityConfiguration` grants deterministically. The legacy enterprise factory and report remain available unchanged. The selected charter exists outside the changing world configuration and is supplied to terminal `RunResults`; it cannot be swapped as an economic operation.

`ChallengeRules` extends the original net-delivery rule with two explicit cases: actual regional scheduled consumption and productive enterprise performance. Progress remains in the same world snapshot. Consumption is a world-level measure, while enterprise production/margin belongs to the configured company. Final-operation evaluation and terminal guards apply to all cases, including confirmed voluntary abandonment. No generic expression engine or future difficulty-modifier hierarchy is introduced.

`RunResults` reads a terminal snapshot, its challenge, selected charter, and authoritative company financials. Its bounded deterministic objective/victory/time/solvency components do not value unsold goods or count sale turnover as unrestricted achievements. The [run guide](RUNS.md) defines selections and measured outcomes; [mechanics](MECHANICS.md#starting-charters-outcomes-and-scoring) specifies the exact score.

The Unity host owns screen navigation, paused playback, selected configuration, bounded presentation journal, and confirmation intent. It constructs/replaces worlds on start/retry and reads terminal state to stop playback and present results. Scene UI does not calculate victory, scores, material costs, or logistics quantities. In-memory ownership is not a persistent save contract. Actual navigation and player verification are recorded separately in [Unity integration](UNITY.md).

## Representative step measurements

A local Release/.NET 10.0.13 measurement ran 30 warmed, 90-day industrial scenarios using `Stopwatch` and per-thread allocated-byte counters around world steps, excluding setup. Median time was **37.201 ms per 90 days** (**0.413 ms/day**); the 95th-percentile 90-day run was **87.035 ms**. Median allocation was **300,005 bytes/day**. The day-90 state had at most eight open lots during the run and at most six economic events in one operation; a 1,000-day observation ended with six open lots and eleven held units.

These are machine observations, not CI performance thresholds or evidence of large-world scalability. They include immutable snapshots and staged copies, whose allocation is acceptable for this small low-frequency world but should remain visible as the simulation expands. Unsold repeated acquisitions can accumulate more open lots than this scenario; the design avoids accumulated event-history copies, not every possible inventory-growth cost. Reproduce meaningful scenarios before optimizing or widening scope.

## Boundaries and limitations

Fixed quotes remain the accounting baseline; linear/scarcity formulas and authored regional values are experimental. The legacy maritime owner remains two-port/single-ship. The expanded regional economy has four towns, six goods, public and private industries, finite company accounts, deterministic merchants, direct routes, explicit warehouse logistics and three fixed challenges with replacement starting charters. It has no household finances, hidden cash replenishment, route discovery/optimization, procedural generation, meta progression, random events, piracy, combat, multiplayer, or save system. Travel has no operating costs/risk, cargo uses item counts, and market information is perfect. The preserved public-market route policy still infers direction from distinct commodities. Industrial routes use explicit stages, permit the same commodity in both directions, and pause incompatible cargo without disposal. Waiting cannot guarantee recovery in finite markets. [Commerce and industry](ENTERPRISE.md) describes the current player-facing boundaries.

Headless compilation and tests exercise the portable library and pipeline. Unity import, Editor behavior, Mono player execution, and IL2CPP player execution are separate verification points; follow [Unity integration](UNITY.md) and report which checks actually ran.
