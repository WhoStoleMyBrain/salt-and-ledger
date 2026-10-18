# Architectural decisions

These records describe accepted decisions through Task 1.3b. Unresolved game rules live in [Economy](ECONOMY.md); hypotheses and deferred concepts live in [Ideas](IDEAS.md). New evidence can supersede a decision explicitly.

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

The generator describes only source facts. Authored [Mechanics](MECHANICS.md) specifies command behavior that cannot be inferred from content. There is no in-game encyclopedia UI yet.

## ADR 006: Verify the foundation without the Unity Editor

**Status:** Accepted.

GitHub Actions builds the standalone simulation, runs the C# and Python tests, validates content, checks generated-document freshness, and builds MkDocs strictly. Tests cover meaningful success, rejection, determinism, reference consistency, and generated-output behavior.

This keeps the foundation independently verifiable. CI does not certify Unity imports, Editor compilation, or player builds. Report the actual verification performed and retain practical local instructions for those integration points.

## ADR 007: Move standalone hosts to .NET 10 LTS

**Status:** Accepted; supersedes ADR 001's .NET 8 demo and test targets.

The demo and headless tests target `net10.0`; the reusable library keeps `netstandard2.1` and C# 8. `global.json` selects stable SDK 10.0.100 or a later installed 10.0 feature band. This accepts servicing updates while preventing an implicit major-version upgrade. CI installs .NET 10 and restores committed NuGet lock files with `--locked-mode`. Microsoft.NET.Test.Sdk is 18.11.0; the working xUnit framework and adapter remain unchanged.

## ADR 008: Stage one portable assembly into the existing Unity project

**Status:** Accepted; supersedes ADR 001's Phase 0 decision to omit a Unity project and implements ADR 003's host-owned JSON dependency.

Keep the existing Unity project at `unity/`, pinned to Editor 6000.6.6f1. Use `tools/prepare_unity.py` to validate content, rebuild the standalone simulation, and stage its single DLL plus canonical JSON with stable asset metadata. Ignore staged outputs and check their source/output hashes with `--check`. This avoids duplicated simulation source, machine-specific links, and hand-maintained content copies. It requires an explicit preparation step after simulation or content edits.

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
