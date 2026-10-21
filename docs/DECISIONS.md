# Architectural decisions

These records describe accepted decisions through Task 1.7. Unresolved game rules live in [Economy](ECONOMY.md); hypotheses and deferred concepts live in [Ideas](IDEAS.md). New evidence can supersede a decision explicitly.

## ADR 001: Keep the simulation independent of Unity

**Status:** Accepted boundary; .NET 8 host targets and the absence of a Unity project are superseded by ADR 007 and ADR 008.

Use a concrete C# library targeting `netstandard2.1` and C# 8 for simulation and content loading. Keep the console demo and tests in separate `net8.0` projects. Unity 6 is the intended presentation host; no Unity project is introduced by this foundation.

This boundary permits headless verification and future large experiment batches without the Editor. The target framework fits Unity 6's .NET Standard API profile. It does not prove compatibility with every Unity platform or scripting backend; local Editor and player verification remains necessary. Avoid speculative interfaces and host abstractions until a real integration needs them.

## ADR 002: Use JSON as the canonical content source

**Status:** Accepted.

Keep definitions in `data/content.json` with an explicit format version and a Draft 2020-12 schema in `data/content.schema.json`. Use stable globally unique IDs, separate from display names, for references. Version 1 contains only goods, buildings, and production recipes needed by the sample.

Schema checks structure. Semantic validation checks references, uniqueness, integer representation, and relevant relationships. Python validates authoring inputs and C# validates loaded runtime content; changes must keep their accepted content contracts aligned. No migration framework is introduced; unsupported format versions fail explicitly.

## ADR 003: Use Newtonsoft.Json for the portable loader

**Status:** Accepted.

The simulation uses Newtonsoft.Json 13.0.3. This avoids making the Unity-independent assembly depend on a newer standalone runtime API. The intended Unity integration uses Unity's official Newtonsoft package; its assembly should be supplied once by the host rather than copied into Unity alongside another package copy.

Package and assembly compatibility still require the local verification in [Unity integration](UNITY.md), especially for IL2CPP and stripping configurations.

## ADR 004: Keep production explicit, deterministic, and atomic

**Status:** Accepted atomic production contract; the Phase 0 restriction to explicit calls without a clock or scheduler is superseded by ADR 009.

One command executes one recipe batch against an inventory for a specified building. Quantities are bounded nonnegative integers, arithmetic is checked, and the entire result is validated before committing state. Missing inputs, a wrong building, or overflow must leave the inventory unchanged.

This gives tests a small meaningful state transition. It intentionally adds no time model, scheduler, market, or automation policy. The 2-logs-to-1-plank ratio is example content and does not settle future production balance.

## ADR 005: Generate both documentation formats from content

**Status:** Accepted.

Python produces Markdown under `docs/reference/` and structured data in `generated/encyclopedia.json` from validated content. Cross-references connect related entities. MkDocs builds the external documentation site. Generated artifacts are committed so their changes can be reviewed with source data; `--check` detects stale output without rewriting it.

The generator describes only source facts. Authored [Mechanics](MECHANICS.md) specifies command behavior that cannot be inferred from content. Task 1.7 extends this boundary to an offline consumer and shared authored help in ADR 027.

## ADR 006: Verify the foundation without the Unity Editor

**Status:** Accepted.

GitHub Actions builds the standalone simulation, runs the C# and Python tests, validates content, checks generated-document freshness, and builds MkDocs strictly. Tests cover meaningful success, rejection, determinism, reference consistency, and generated-output behavior.

This keeps the foundation independently verifiable. CI does not certify Unity imports, Editor compilation, or player builds. Report the actual verification performed and retain practical local instructions for those integration points.

## ADR 007: Move standalone hosts to .NET 10 LTS

**Status:** Accepted; supersedes ADR 001's .NET 8 demo and test targets.

The demo and headless tests target `net10.0`; the reusable library keeps `netstandard2.1` and C# 8. `global.json` selects stable SDK 10.0.100 or a later installed 10.0 feature band. This accepts servicing updates while preventing an implicit major-version upgrade. CI installs .NET 10 and restores committed NuGet lock files with `--locked-mode`. Microsoft.NET.Test.Sdk is 18.11.0; the working xUnit framework and adapter remain unchanged.

## ADR 008: Stage one portable assembly into the existing Unity project

**Status:** Accepted; supersedes ADR 001's Phase 0 decision to omit a Unity project and implements ADR 003's host-owned JSON dependency.

Keep the existing Unity project at `unity/`, pinned to Editor 6000.6.6f1. Use `tools/prepare_unity.py` to validate content, rebuild the standalone simulation, and stage its single DLL plus canonical JSON with stable asset metadata. Ignore staged outputs and check their source/output hashes with `--check`. This avoids duplicated simulation source, machine-specific links, and hand-maintained content copies. It requires an explicit preparation step after simulation or content edits. ADR 027 extends staging to the generated offline encyclopedia without adding another JSON dependency.

Unity owns the Newtonsoft assembly through pinned UPM package `com.unity.nuget.newtonsoft-json` 3.2.2 and `packages-lock.json`; the portable build retains NuGet 13.0.3. The inspected Unity upstream 13.0.2 DLL has the same strong-named assembly identity as the NuGet dependency. No NuGet JSON DLL is copied into Unity. Actual Editor and player checks, including their limits, are recorded in [Unity integration](UNITY.md).

The Bootstrap smoke component only orchestrates the existing loader and production command. Focused Edit Mode and Play Mode tests and a Windows Mono player build entry point verify that boundary. CI validates preparation and freshness without requiring a Unity license. Other backends and content-delivery systems remain separate verification work.

## ADR 009: Advance a small world through explicitly ordered integer ticks

**Status:** Accepted for Task 1.1; extends ADR 004 without changing its one-batch production contract. ADR 012 extends this schedule with trading before production.

Use one fixed runtime location with one inventory and explicit building instances that reference immutable canonical building and recipe definitions. A nonnegative signed 64-bit tick advances only when a host calls `WorldState.Step` successfully. A tick is an abstract economic step; no conversion to in-game hours, real seconds, or Unity frames is selected.

Validate the complete command sequence, check tick overflow, apply enabled-state commands in declared order, select enabled instances, then attempt one batch per instance in ascending priority and ordinal instance-ID order. The last command for an instance wins. Every attempt calls the existing `Production.TryRunBatch`. Resource shortage is an expected result. Neither content nor collection insertion order controls scheduling.

The one-batch scheduling rule is provisional production cadence, and fixed ordering may starve later instances. There is no fairness rotation, configurable automation, runtime priority change, or construction. New phases for markets, arrivals, and consumption need their own explicit ordering decision when implemented; no placeholder phase framework is added now.

## ADR 010: Commit complete world snapshots and return per-step results

**Status:** Accepted.

Keep the current world state as one immutable snapshot. Each step stages inventory and building state privately, then constructs the full next snapshot and structured result before a single commit assignment. Invalid commands, enumeration errors, and arithmetic overflow leave the prior inventory, flags, and tick unchanged, including earlier staged production. This extends per-batch atomicity to the complete tick without a generalized transaction framework.

Copying small state each tick favors a simple failure contract over unmeasured optimization. It has an allocation cost; measure representative workloads before changing the representation. Snapshots and instance values expose no mutation path, and the API supports one sequential caller, with reentrant steps rejected. Thread-safe simulation is not implemented.

Return `StepResult` with the starting tick, committed next snapshot, and ordered per-building outcomes. Changes contain only net-changed goods, sorted by ordinal ID. A shortage has no inventory changes; a successful zero-net batch can also have no changes, so its outcome remains explicit. Snapshot buildings and inventory have stable ordering as well. Do not retain step history in the world; hosts decide which results to keep. This supports inspection and deterministic comparison without claiming a persistent replay protocol or save format.

## ADR 011: Validate finite accounting with fixed local quotes

**Status:** Accepted for Task 1.2 as an experimental baseline; retained unchanged alongside ADR 013's dynamic alternatives.

Use the existing location inventory as market stock and give one trader a separate inventory. Keep two nonnegative `long` cash balances and `long` unit prices, with checked transaction arithmetic and indivisible integer currency units. An immutable runtime `MarketConfiguration` supplies initial accounts and unique per-good quotes. The trader buys at ask and sells at bid, requiring `Ask >= Bid > 0` to prevent profitable immediate round trips at unchanged quotes. No global content prices or schema changes are needed.

Calculate all four affected balances before applying a trade. Goods and cash are transferred, never implicitly created. Short stock or funds rejects the whole transaction; no partial fills, borrowing, fees, queues, or replenishment exist. Compute the checked requested total first, then check seller stock before buyer cash, giving stable rejection precedence. Receiver overflow remains exceptional.

Market-owned production is a temporary ownership assumption and does not decide how player-owned factories work. Currency scale, starting balances, and prices are experimental. Fixed quotes isolate accounting correctness from price formation; ADR 013 adds comparative pricing hypotheses without replacing this baseline.

## ADR 012: Execute trades before production within the atomic world step

**Status:** Accepted prototype ordering; extends ADR 009 and ADR 010.

Extend `Step` with optional ordered trade commands, preserving existing production-control source calls and behavior when no trading is configured. Validate all controls and trades, check tick overflow, stage private accounts and buildings, apply controls, execute trades in declared order, then run the existing production schedule. Construct the complete next snapshot and result before the single world commit.

Sold logs can feed same-tick production, purchased logs cannot, and new production output is tradeable only next tick. Expected stock/funds rejections return explicit `TradeOutcome` values and allow later commands to proceed. Exceptions roll back both inventories, both cash balances, all controls, production, and clock together; no successful-looking partial result escapes. This is an experimental phase rule, not a finalized economic time model.

`WorldSnapshot.Trading` is an optional immutable account snapshot; `Inventory` remains the sole market stock. `TradeAttempts` records ordered requests, quotes, outcomes, and before/after goods and cash. Fixed-price rejected requests retain their quoted total with zero actual changes; ADR 014 defines the added dynamic result and preview semantics. No account service, message bus, persistent transaction history, or second simulation clock is introduced.

## ADR 013: Price inventory transitions with exact bounded arithmetic

**Status:** Accepted execution contracts for Task 1.2b; linear/scarcity formulas and balancing parameters are provisional hypotheses.

Keep fixed pricing intact and add two concrete `InventoryPricingModel` modes. Both integrate with existing world trades. Associate one reference value with each edge between consecutive market stock levels, and price purchases/sales using the same edge in opposite directions. Bulk totals sum all traversed marginal prices. Require positive bounded side prices, asks no lower than bids on the same edge, conservation of goods/cash, exact uninterrupted order-splitting totals, and no profitable closed inventory cycles without external changes. These requirements remain valid if future experiments replace a formula.

Compare a linear response around a configured reference/target with a piecewise curve that adds a stronger scarcity response below one quarter of target. Apply mathematical floor to the exact rational reference, then add/subtract a per-side spread and clamp each side to configured bounds. The spread may compress at the bounds; zero spread is permitted. The exact formulas, derived knee, multiplier 3, and numerical parameters are experimental rather than permanent design rules. [Mechanics](MECHANICS.md) specifies the calculations; [Pricing experiments](PRICING_EXPERIMENTS.md) records their evidence and recommendation.

Use `BigInteger` intermediates with a checked `long` final total. Sum at most two affine segments using analytic bound crossings and a logarithmic Euclidean floor sum. This handles very large quantities without loops over units or price bands and without binary floating-point arithmetic. It adds no package dependency, Unity state, general pricing interface, or policy framework. Tests compare exact small-state marginal sums and check large numeric boundaries; larger-world allocation and throughput remain separate measurement work.

Prices are derived from current inventory and immutable runtime configuration. Production changes the next snapshot's quotes through its stock changes; an idle tick does not move them. No canonical content/schema change, separate price clock, demand simulation, or replenishment is required. Reference prices are scenario hypotheses, not global good values.

## ADR 014: Share preview and execution and expose marginal totals

**Status:** Accepted for Task 1.2b; extends ADR 011's result representation while preserving fixed-mode behavior.

Provide `WorldSnapshot.GetMarketQuote` for marginal prices and `PreviewTrade` for a complete non-mutating proposed transaction. Execution and preview share the same trade calculation and exact price summation. A snapshot derives quotes from its own authoritative stock, including all committed trades and production; retained snapshots remain historical. Configuration `Bid`/`Ask` getters throw for dynamic quotes to prevent callers from treating a configuration as a current price. A preview is not a reservation, and an earlier command in the step may invalidate its assumptions.

Represent a priced request with quantity, first and last marginal prices, total, and a uniform-price flag. Retain `UnitPrice` as the first marginal price and `TotalAmount` as the proposed total for compatibility; never imply their product matches a dynamic total. Add explicit executed quantity/amount, both zero for rejection. Dynamic seller-stock shortage returns no price breakdown; legacy price/total aliases are zero when unpriced. Priced affordability rejections retain their proposed breakdown without transferring anything.

Fixed trades keep their multiplication-first precedence. Dynamic trades check seller stock first, then market capacity for a sale, then compute the complete total and perform affordability/receiver checks. This prevents pricing nonexistent transitions beyond zero or maximum market stock. Numeric failure is exceptional and rolls back the whole tick. Marginal ask is absent at zero stock; marginal bid is absent at maximum stock. Other stock/account constraints are reported by the full preview. The phase order and single commit boundary from ADR 012 remain unchanged.

## ADR 015: Own the two-port prototype in one maritime world

**Status:** Selected before Task 1.3a implementation; accepted prototype ownership model.

Introduce a focused `MaritimeWorldState` alongside the existing single-location API. Its one immutable current snapshot owns exactly two ports, one trader cash account, one ship with capacity-limited cargo, and one global tick. Each port owns independent market stock, cash, quote configurations, and local buildings. Cargo belongs only to the ship; there is no trader warehouse or account copy per port. Runtime configuration supplies the handcrafted ports and one bidirectional route's positive integer travel duration.

Stage all ports, accounts, controls, and movement privately, then construct the complete result and next snapshot before one commit assignment. Never compose independent `WorldState` clocks. Reuse the shared internal trade calculator, existing `InventoryPricing`, and `Production.TryRunBatch`; retain the existing `WorldState` public API and behavior. This isolates the new ownership and movement rules without duplicating economic calculations or introducing a generic entity/service framework.

Validate all commands, check tick overflow, apply controls, execute trades at the starting dock in declared order, apply at most one departure, run production by ordinal port ID and then existing building order, and advance travel once before committing. The departure tick counts toward travel. Arrival permits trading on the following step. Remote quotes and hypothetical previews are allowed; actual trades require docking at their explicit port. Expected stock, funds, or cargo-capacity rejection is non-mutating; exceptional failure rolls back the entire maritime tick.

The model deliberately supports one trader and one ship, one capacity unit per item, market-owned production, perfect market information, and travel without cost or risk. Those assumptions and the pricing formulas remain provisional. No automation, automatic replenishment, consumption, procedural world, or persistent event history is introduced.

## ADR 016: Evaluate route policies without owning economic state

**Status:** Selected before Task 1.3b implementation; accepted prototype automation contract.

Keep route configuration immutable and route evaluation stateless. One outbound commodity and an optional distinct return commodity let the policy infer its stage from the committed ship location and cargo. `MaritimeWorldState` remains the sole owner of markets, cash, cargo, movement, and time. The evaluator produces at most one ordinary trade command plus an optional departure; it does not execute production or copy authoritative accounts into a second simulation.

Select the largest feasible quantity by bounded binary search over stock, capacity, exact marginal price limits, buyer funds, and the configured trader reserve. Reuse the existing pricing summation with a `BigInteger` budget comparison for large search probes, then obtain the selected transaction through the existing snapshot preview. Public transaction totals and receiver balances retain checked numeric bounds; unexpected arithmetic failures remain exceptional. Minimum shipment sizes apply to the combined cargo departing its source, not to sales. Partial sales retain unsold cargo and prevent departure. Unexpected or mixed cargo requires intervention.

Bind each immutable decision to its exact source snapshot. `TradeRouteAutomation.Execute` rejects a stale decision before stepping, freshly previews its trade, and submits only the decision's commands. One sequential host must not mix manual commands into that execution. This permits a successful purchase and departure in the same tick without changing manual rejection behavior or the existing atomic commit boundary. Pausing and replacing policies are host choices between steps; no persistent route stage or second clock exists. Waiting can remain indefinite in a finite economy, so a host runner must impose an explicit finite observation limit and report lack of progress without inventing replenishment.

## ADR 017: Host the trading laboratory with explicit UI actions

**Status:** Selected before Task 1.4 implementation; accepted presentation boundary.

Add a separate `TradingLab` scene using UI Toolkit, authored UXML/USS, and a small controller/session pair. Keep Bootstrap as the original production smoke. The session loads the prepared canonical content and constructs the existing two-port fixture with ordinary simulation configuration types. It references the staged simulation DLL, never the .NET 10 console assembly. A small host fixture duplicates setup parameters only; pricing, transactions, production, cargo capacity, route decisions, movement, and time remain exclusively in the simulation.

User actions choose either manual maritime commands or snapshot-bound automation for each step. Inspecting a port, editing a field, applying an immutable policy, and enabling or disabling automation do not advance time. Explicit buttons advance one tick or a finite sequence of ordinary ticks; neither frames nor elapsed real time drive the economy. Derive the ship indicator from the committed snapshot. Freshly preview a combined purchase/departure before submitting it, and refuse departure if that purchase cannot succeed.

Render authoritative snapshots, exact preview totals, and structured step results. Keep only a bounded recent-activity list in the host. Report cash and cargo independently; label realized gross trading profit only for a completed accounting interval whose initial and final cargo are empty. Do not introduce cargo valuation, persistent event sourcing, a generalized UI framework, new economic rules, or hidden recovery funding. Reset constructs a new test world and is explicitly separate from an in-world action.

The fixed scenarios make manual operation, configured automation, policy-limit recovery, and finite-liquidity blockage directly inspectable. Automated scene/control tests and interactive visual checks are separate evidence; record both actual results and limits in [Unity integration](UNITY.md). The [playtest guide](PLAYTEST.md) defines the player-facing checks without claiming that mechanical correctness establishes enjoyable gameplay.

## ADR 018: Separate regional days from instantaneous revisioned commands

**Status:** Accepted for Task 1.5. Adds a new compatibility boundary; ADR 009/012/015/016 retain their legacy tick semantics.

Introduce one focused `RegionalWorldState` for multiple ports and explicitly owned ships/accounts. Reuse `TradeCalculator`, `InventoryPricing`, `Production.TryRunBatch`, and the route evaluator through an actor-specific two-port snapshot view. Never synchronize independent local worlds or copy economic calculations into a new economy. The shared `ArchipelagoScenario` supplies identical setup to Unity and headless experiments.

Instant trade, departure, configuration, and enablement commit a checked revision only when state changes. A rejected/no-op action preserves snapshot identity. Bind decisions to the exact snapshot so successive same-day transactions cannot reuse stale proposals. Daily advancement stages actor actions, production, consumption, and voyage progress before one commit of day and revision. Any exceptional failure rolls back all fields and earlier staged actions.

Departure spends no day; travel completes after the configured number of subsequent day advances, and committed arrival permits immediate dockside trading. Distances and speeds use integers independently of visual coordinates. This intentionally differs from the older departure-tick model without changing its tests or historical examples.

## ADR 019: Model explicit goods sources and sinks with finite cash

**Status:** Accepted execution contracts for Task 1.5; town schedules, ceilings, prices, and threshold values remain provisional.

Evolve version-1 recipes additively with optional kind. Existing absent-kind recipes remain transformations with nonempty inputs; explicit extraction requires no inputs and creates only canonical outputs. Keep production arithmetic in the existing atomic batch operation. Per-facility intervals, deadlines, and output ceilings are runtime scenario configuration.

Successful due production schedules its next opportunity from completion. Disabled/input-short/output-limited batches remain due without partial output or catch-up accumulation; toggles preserve deadlines. Physical consumption removes a complete scheduled quantity or records unmet demand, then schedules its next opportunity without carrying an inventory debt.

Consumption has no household payment. No cash source, replenishment, subsidy, or hidden recovery exists. Record extraction, transformations, consumption, transfers, and physical transport separately; reconcile stock changes rather than claiming goods conservation across legitimate sources/sinks. All ordinary trades conserve cash. Deterministic NPC policies own real finite accounts/cargo and use the same transaction engine; stable priorities resolve competition.

## ADR 020: Present the region through a map and serialized playback

**Status:** Accepted for Task 1.5; preserves ADR 017's TradingLab and Bootstrap.

Add a separate UI Toolkit ArchipelagoLab scene with a dominant authored maritime map, selected-object context, collapsible trading/policy/industry detail, and bounded activity. Read port positions, rates, limits, cargo, quotes, decisions, and occupancy from the shared scenario/snapshots. NPC ships can be inspected but not manually controlled through the UI.

A main-thread host uses real elapsed time only to request whole economic days at selectable playback speeds. Bound catch-up, clear the accumulator on pause/reset/speed changes, and pause before manual trading/orders. Interpolate visual markers between authoritative states; rendering never determines travel duration or market access. Policy edits and inspection remain passive; explicit bounded dockside execution can sell, reload, and depart without adding artificial days.

Preserve exact previews, readable waits, finite accounts, and separate cash/cargo reporting. Verify scene/control behavior, actual graphical interaction, and player startup independently, recording what was actually exercised. A functioning map and reproducible long-run report do not establish human usability or economic balance.

## ADR 021: Give companies cash and factories finite private stock

**Status:** Selected for Task 1.6; scenario balance remains provisional.

Extend RegionalWorldState's existing atomic owner with explicit company accounts and canonical-recipe factories. Cash has one mutable company owner; ship snapshots project it for compatibility. Each factory has one finite private stockpile, avoiding ambiguous shared-stock attribution without introducing warehouse networks or fleet management. Public markets, ship cargo, and private goods remain distinct.

Instant cash-only construction pays the local market, starts with empty storage, and schedules the first batch one full interval later. Same-owner ship transfers require the actual dock and conserve goods without cash or time. Local private trades reuse TradeCalculator, while batches reuse Production.TryRunBatch. Daily procurement and optional sales use bounded exact quantity search, marginal limits, and reserves. No facility owns a second clock or economic calculator.

## ADR 022: Evaluate a fixed challenge from net actual deliveries

**Status:** Selected for Task 1.6; thresholds are provisional experiment configuration.

Attach one optional immutable challenge configuration and staged progress snapshot to the regional owner. Count successful company sales into the target market and subtract purchases from it, preserving a signed counter so buybacks cannot create repeated delivery credit. Transfers and nearby cargo do not count; delivered supply does not imply actual consumption. A cash threshold accompanies the primary supply objective.

Evaluate only after a complete atomic operation. The deadline's final daily phases may succeed before failure is declared; terminal states reject further economic mutation. Sandbox mode retains unlimited days. Use reproducible merchant and industrial trials to choose thresholds. Procedural goals, adaptive difficulty, and meta progression remain deferred.

## ADR 023: Present exact trade proposals without inventing cost basis

**Status:** Selected for Task 1.6; its historical-cost deferral is superseded by ADR 024.

Replace the ArchipelagoLab trading foldout with a contextual modal over the existing map. Supported pointer events open local or remote views without sending economic commands. Modal opening pauses playback; stock, limits, maximum quantities, and execution use authoritative snapshots and commands, including stale-snapshot protection.

Display exact total divided by executable quantity as the average execution price. Full historical acquisition cost needs attribution across mixed purchases, partial sales, manufacture, and transfers, so defer it explicitly. Report actual cash flows and private stock without labeling revenue or hypothetical liquidation as realized profit.

## ADR 024: Attribute material cost with FIFO open lots

**Status:** Selected for Task 1.7; supersedes ADR 023's historical-cost deferral.

Add one focused accounting component inside the existing regional working copy. Purchases create FIFO lots using actual executed totals; source-store FIFO transfers preserve original cost intervals and append them to the destination's receipt queue. Partial disposal uses cumulative integer prefix-floor differences with exact wider intermediates. Thus repeated splitting neither manufactures nor loses historical expenditure.

Private production withdraws gross recipe inputs and spreads their total material cost over actual output units in ordinal good order. This is deterministic equal-per-item allocation with exact remainders, not price-weighted valuation. Starter grants retain zero paid basis and explicit provenance. Construction remains separate capital expenditure. Released inventory basis supports realized gross margin; neither unmodeled wages/transport nor speculative liquidation becomes fabricated profit.

Keep live lots and company aggregates in snapshots, and return only the current committed operation's economic events. Hosts own bounded UI history or finite experiment journals. This supports audit and terminal results without unbounded per-day history copies or a second economic authority. Accounting failures participate in the same whole-operation rollback. FIFO at each receipt store is chosen for transparent provenance; this milestone does not introduce tax accounting or a depreciation model.

## ADR 025: Store industrial route direction explicitly

**Status:** Selected for Task 1.7; extends regional automation while preserving ADR 016's historical policy.

Use one immutable two-port industrial policy with market/private warehouse sources and destinations, optional return leg, finite shipment rules, thresholds/reserves, and exact marginal limits. Store outbound/return acquisition/delivery stage in the authoritative ship snapshot. Explicit stage avoids ambiguity for same-commodity return routes and partially delivered cargo.

The pure evaluator emits ordinary economic commands bound to the current snapshot. The regional owner validates and executes them through existing trade, transfer, and departure paths, with a fresh preview and the same four-action limit. Stage and historical cost roll back with all other state. Unsold goods remain owned and physically aboard; a wait cannot grant stock, cash, speed, or remote permission.

A new policy requires an empty ship docked at one endpoint. Toggling enablement or reapplying the same immutable policy retains stage. This focused state model replaces no working legacy API and introduces neither arbitrary scripting nor autonomous strategy discovery.

## ADR 026: Configure fixed runs with lateral charters

**Status:** Selected for Task 1.7; economic parameters remain provisional.

Add three handcrafted challenge definitions and three replacement starting-company charters over the existing regional scenario. Initialize starter factories and inventories explicitly, with grant provenance and no fake purchases. Cash alone is not total endowment; ship capacity, location, factory access, and goods create different opportunities. Packages do not stack or unlock permanent numeric upgrades.

Keep objective progress and terminal state in the regional owner. Preserve signed net supply; add actual scheduled regional consumption and company productive-margin goals as explicit cases. All outcomes evaluate after complete atomic operations, freeze economic mutation, and distinguish success, deadline failure, and deliberate abandonment.

Terminal results use authoritative measurements and a bounded four-part score. Compare challenge/charter groups rather than promise fair global normalization. The host handles menu/setup/pause/results navigation and confirms discarding active progress. Retry creates the selected initial state again; no persistent saves, incidental recovery service, or future modifier framework is implied.

## ADR 027: Share player help between the offline reference and website

**Status:** Selected for Task 1.7; extends ADR 005 and ADR 008.

Keep canonical recipe facts in `data/content.json`. Add a small schema-validated `data/player-help.json` for authored gameplay explanations whose references resolve against canonical and help IDs. Generate both external reference pages and encyclopedia format version 2 from these sources. Uniform presentation fields and typed canonical relationships support category navigation, search, cross-links, and future content without one-off screens.

Preparation refuses missing/stale encyclopedia output and stages the bundled `Resources/Encyclopedia.json` alongside the existing DLL and canonical JSON. Hash inputs and outputs in a versioned staging manifest. Unity supplies JSON support once through its pinned UPM package; offline reference browsing needs no network service or runtime-generated explanations.

Retain MkDocs as a static site, separate player navigation from development details, and inspect rendered desktop/mobile pages. Prepare a manual-only GitHub Pages workflow and explicit activation instructions, without silently publishing or changing repository settings. Local builds remain independent of publication.

## ADR 028: Generate geography from a versioned integer seed

**Status:** Selected for Task 1.8; geographic bounds are a provisional gameplay constraint, not proof that every strategy wins.

Keep the four existing economic roles and every initial economic parameter. Version 1 permutes four separated slots and applies bounded integer jitter using eleven specified uint mixing draws. Six direct distances are integer ceiling square roots derived from the generated coordinates. Existing ship speed and `SeaRoute.DurationDays` determine travel. This bounds work and voyage length without rejection loops, floating-point rounding, runtime-specific random implementations or seed-specific economic rescue.

Preserve authored Classic positions/distances as an explicit separate mode. Reuse `EnterpriseScenario` economic definitions and retain legacy overloads and historical report bytes. A complete immutable `RunIdentity` carries challenge, charter, mode, seed/version and difficulty into snapshots/results. Retry reconstructs that identity. Reroll is an explicit host request that changes the draft seed before preview/start confirmation. New comparisons include difficulty and exact world identity; scores on unlike seeds are not equivalent leaderboard positions.

## ADR 029: Complete delayed construction at the end of a daily commit

**Status:** Selected for Task 1.8; extends ADR 021 without changing Ascension 0 or starter endowments.

Add exactly `Normal` and `SlowFoundations` difficulty values. New player-owned purchases under Ascension 1 pay the full cost at order and create a pending owned site; completion requires two actual daily advances. Finish construction after voyages and before challenge evaluation, keeping pending sites out of all automatic factory phases of their completion day. Schedule the first production opportunity one full blueprint interval later. This makes day-0 ordering, day-2 completion and day-4 first eligibility explicit for a two-day bakery.

Use snapshot dates/status and a shared timing preview for host feedback, plus structured commencement/completion results and a zero-cash completion event. Do not add another clock or cost basis, charge twice or grant free materials. Manual local warehouse trades and physical owned transfers remain allowed under existing bounds. Starter factories are already operational, preserving charter history and its asymmetric response to this narrow modifier. All lifecycle changes share the existing atomic rollback and terminal freeze.

## ADR 030: Scale inherited UI layout tokens without touching the world

**Status:** Selected for Task 1.8; actual resolution/input evidence is recorded in [Unity integration](UNITY.md).

Replace the partial large-menu/reference toggle with four game-wide ArchipelagoLab text scales. Inherited root tokens control body/caption/headings and related control/table/map dimensions; responsive wrapping and internal scrolling maintain access to actions. Avoid a global visual transform that enlarges painting while leaving layout or hit targets behind.

Keep this preference in a small presentation helper with no simulation commands. Save only the scale through ordinary local PlayerPrefs and retain it across screen transitions and fresh runs. Other display settings remain session-only; there is no economic save or cloud preference service. Test world-state invariance separately from real runtime geometry, keyboard and pointer interaction.

## ADR 031: Close the living-city money cycle inside the regional owner

**Status:** Implemented for Task 1.9; financial conservation is a correctness contract, wages and welfare parameters are provisional economic assumptions.

Give each living port one aggregate household cash account, distinct from market and company accounts. Actual successful production transfers finite employer money to households; exact retail transfers household money back to the market and consumes only paid goods. No timed allowance, subsidy or outside balancing account exists. Municipal own-use remains a separately labeled unpaid physical sink. Keep all state and every checked transfer inside the existing atomic, revisioned owner.

Use the shared exact price and bounded quantity calculators for household purchases, with essential priority, partial fulfillment and separate stock/funds shortfalls. Production without full payroll funding cannot complete. Retain material FIFO unchanged and expense private wages separately; report material margin and cumulative surplus after all wages, including labor for still-held output. Do not call this a matched full-cost profit or invent freight/depreciation.

Observe at most six food opportunities and small counters, smooth bounded prosperity, apply durable-demand hysteresis and review population slowly. The recovery challenge uses actual paid food with an initial-population denominator and retained population, so deprivation cannot make the objective easier. All outcomes remain regional welfare rather than falsely attributed player-only delivery.

## ADR 032: Version economic roles independently from historical geography

**Status:** Implemented for Task 1.9; structural supply paths are guaranteed, strategy success and long-run solvency are not.

Keep Classic and seeded-v1 economics unchanged. Living Cities v2 calls v1 geography exactly and uses a domain-separated eleven-draw integer stream to permute four authored role packages and bounded population/liquidity bands. Generate actual public facilities and authoritative private blueprint eligibility together. A forest-furniture town has logging and furniture making but cannot construct a Sawmill; actual imported Planks remain necessary. No strategy evaluation, rejection loop or hidden economic repair enters generation.

Preview the same immutable configuration used to create the world. Adapt only charter locations to generated capabilities, retaining assets, cash and operational grants. Complete run identity includes version and living pricing choice, ensuring exact retry and distinct comparison groups. Mode-specific challenge lookup must not expand the historical challenge list used by old reports.

## ADR 033: Compare one finite-coverage target through the existing price engine

**Status:** Implemented experimental alternative for Task 1.9; StockOnly remains available and Coverage is not declared balanced.

Use the existing immutable inventory-price model with a household-coverage target derived from seven days of expected opportunities and one finite actual household budget. Prioritize essentials, retain affordability, cap target quantities and leave industrial goods/reference prices/spreads unchanged. Do not multiply all prices by wealth or invent expected wages to fund a hypothetical need.

Derive parameters at initialization and the daily boundary after welfare observation, then freeze them across instant trades. Previews, household retail, players, NPCs and factories share the same exact marginal mathematics. Historical snapshots retain their effective parameters, and same-day quote derivation cannot manufacture an immediate reversal gain. Measured A/B outcomes and long-run deadlocks remain evidence in [Living Cities](LIVING_CITIES.md), not reasons to introduce subsidies.
