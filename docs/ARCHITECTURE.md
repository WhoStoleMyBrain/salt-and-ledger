# Implemented architecture

Phase 0 separates content, simulation, host presentation, and reference generation using concrete modules. Later milestones add atomic finite markets, exact stock-sensitive pricing, maritime ownership, stateless route policies, and the TradingLab UI. Task 1.5 adds a focused regional owner for multiple ships/ports, instant actions, economic days, scheduled sources/sinks, and a map-first ArchipelagoLab. All worlds share pricing, transaction, and production operations. Older public APIs and tick semantics remain intact; no independent per-port worlds or speculative service framework are introduced.

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
                            |       +-- multiple ports/ship accounts, day and state revision
                            |       +-- shared transactions/production/policy evaluator
                            |       +-- ArchipelagoScenario --> headless report / Unity ArchipelagoLab
                            +-- original console demo / Unity Bootstrap smoke
```

## Modules and dependencies

| Module | Responsibility | Dependencies |
| --- | --- | --- |
| `src/SaltAndLedger.Simulation` | Canonical content, shared economic calculations, legacy/local/regional owners, schedules, physical flows, stateless policies, and shared archipelago configuration | `netstandard2.1`, C# 8, Newtonsoft.Json 13.0.3 |
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

`python tools/prepare_unity.py` validates canonical content, builds the existing simulation project in Release, and stages only `SaltAndLedger.Simulation.dll` and `data/content.json` under `unity/Assets/Generated/`, with stable generated metadata. The content becomes `Resources/CanonicalContent.json`. No simulation source is copied or compiled a second way. Preparation must be rerun after relevant changes; `--check` compares source, configuration, and output hashes without rebuilding. These local artifacts and their manifest are ignored by Git.

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

`Step` has no implicit loop; the world demo calls it five times, repeats the scenario, and compares results with both identical and reversed building input collections. The market demo uses four ticks and repeats with reversed quote/stock setup order. The shipping demo compares two six-tick manual round trips and verifies both repeated and reversed-port input transcripts. Several steps use ordinary sequential calls, not a separate batch API. The simulation uses no random values, wall-clock inputs, Unity frames, or background scheduling. Trading precedes production; maritime travel progresses after production, and arrival enables trading on the next step. Consumption phases, seed management, save formats, and durable replay compatibility remain unresolved. The current APIs assume one caller; concurrent mutation is not supported.

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

The structured output declares `formatVersion: 1` and `sourceSchemaVersion: 1`. Each entity has a `wikiPath` relative to `docs/`; goods add `consumedByRecipeIds` and `producedByRecipeIds`, buildings add `recipeIds`, and recipes retain `buildingId`, `kind`, `inputs`, and `outputs`. The effective recipe kind is emitted even when its transformation default was omitted in source. Entity arrays sort by stable ID; recipe quantities sort by good ID. This is a reference artifact, not a saved game or a replacement runtime content format.

`python tools/generate_docs.py` writes the expected artifacts and removes stale files within its owned reference directory. `python tools/generate_docs.py --check` is read-only and fails for missing, changed, or unexpected files in that directory or a stale encyclopedia artifact. Both generation and validation accept `--source` for an alternate content file, defaulting to `data/content.json`; generation still uses the standard repository output paths.

Generated pages cannot define production time, prices, or behavior absent from the data. [Mechanics](MECHANICS.md) explicitly maintains those rules. Tests and freshness checks keep generated facts synchronized, while review keeps authored specifications aligned with behavior.

## Regional ownership and the map host

`RegionalWorldState` is the compatibility boundary for Task 1.5. It owns all local markets, per-ship trader accounts/cargo, schedules, policies, voyages, day, and revision in one immutable current snapshot. It never composes separately ticking `WorldState` instances. Instant operations and one complete day use private staged state and a single final assignment; exceptions cannot partially commit an actor action or economic phase.

| Type | Responsibility |
| --- | --- |
| `RegionalPortConfiguration` / `RegionalShipConfiguration` | Immutable authored ownership, positions, priorities, policies, starting finite accounts, facilities, and demand |
| `SeaRoute` | Undirected integer distance and integer duration calculation for a ship speed |
| `ScheduledFacilityConfiguration` / `ConsumptionConfiguration` | Canonical references and explicit runtime cadence/output ceilings or physical demand |
| `RegionalWorldSnapshot` | Ordered ports and ships, current day/revision, quotes/previews, voyage durations, and derived dock counts |
| `RegionalResult` | Structured trades, production, consumption, movements, decisions, and before/after snapshots for one committed operation |
| `RegionalAutomation` | Adapt an actor's two selected ports/account/cargo into the existing stateless route evaluator |
| `ArchipelagoScenario` | Shared four-port factory and player policy used by the demo and Unity |
| `ArchipelagoExperiments` | Console-only sixty-day strategies, deterministic JSON, flow/accounting assertions, and freshness checking |
| `ArchipelagoLabSession` | Main-thread host selection, playback, command submission, and bounded recent activity |
| `ArchipelagoLabController` / `ArchipelagoMap` | UI Toolkit controls, context rendering, map selection, and presentation-only interpolation |

The regional trade path calls the shared `TradeCalculator`; scheduled batches call `Production.TryRunBatch`; actor decisions call the existing policy evaluator through a detached two-port view. The view introduces no second economic owner or duplicate pricing/accounting algorithm. `RouteLeg` gains an optional per-purchase expenditure cap whose default preserves legacy behavior. Region-specific threshold gating and bounded same-day reevaluation wrap ordinary decisions.

`ArchipelagoLab.unity` is separate from TradingLab and Bootstrap. Its UXML/USS provides a dominant maritime map, a selected-object context panel, collapsible trade/route/industry detail, and a bounded event display. Ports/ships/schedules/pricing read from shared definitions and snapshots. Frames schedule whole-day requests through one host; floating-point interpolation draws committed movement without changing simulation position or timing. The [mechanics specification](MECHANICS.md#regional-world-and-instant-commands) defines ordering, scheduling, revision, rollback, and playback precisely.

## Boundaries and limitations

Fixed quotes remain the accounting baseline; linear/scarcity formulas and authored regional values are experimental. The legacy maritime owner remains two-port/single-ship. The regional sandbox supports the requested four towns, one player, two deterministic merchants, direct routes, scheduled extraction/processing, and physical consumption. It has no household finances, hidden cash replenishment, route discovery/optimization, procedural generation, objectives, progression, random events, piracy, combat, multiplayer, or save system. Travel has no operating costs/risk; production is market-owned, cargo uses item counts, and market information is perfect. Stateless policy inference still requires distinct directional commodities and pauses for mixed cargo. Waiting cannot guarantee recovery in finite markets.

Headless compilation and tests exercise the portable library and pipeline. Unity import, Editor behavior, Mono player execution, and IL2CPP player execution are separate verification points; follow [Unity integration](UNITY.md) and report which checks actually ran.
