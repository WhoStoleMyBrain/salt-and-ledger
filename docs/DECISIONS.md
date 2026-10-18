# Architectural decisions

These records describe accepted decisions through Task 1.2. Unresolved game rules live in [Economy](ECONOMY.md); hypotheses and deferred concepts live in [Ideas](IDEAS.md). New evidence can supersede a decision explicitly.

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

**Status:** Accepted for Task 1.2 as an experimental baseline.

Use the existing location inventory as market stock and give one trader a separate inventory. Keep two nonnegative `long` cash balances and `long` unit prices, with checked transaction arithmetic and indivisible integer currency units. An immutable runtime `MarketConfiguration` supplies initial accounts and unique per-good quotes. The trader buys at ask and sells at bid, requiring `Ask >= Bid > 0` to prevent profitable immediate round trips at unchanged quotes. No global content prices or schema changes are needed.

Calculate all four affected balances before applying a trade. Goods and cash are transferred, never implicitly created. Short stock or funds rejects the whole transaction; no partial fills, borrowing, fees, queues, or replenishment exist. Compute the checked requested total first, then check seller stock before buyer cash, giving stable rejection precedence. Receiver overflow remains exceptional.

Market-owned production is a temporary ownership assumption and does not decide how player-owned factories work. Currency scale, starting balances, and prices are experimental. Fixed quotes isolate accounting correctness from future price formation; Task 1.2b should compare pricing hypotheses before adopting a dynamic model.

## ADR 012: Execute trades before production within the atomic world step

**Status:** Accepted prototype ordering; extends ADR 009 and ADR 010.

Extend `Step` with optional ordered trade commands, preserving existing production-control source calls and behavior when no trading is configured. Validate all controls and trades, check tick overflow, stage private accounts and buildings, apply controls, execute trades in declared order, then run the existing production schedule. Construct the complete next snapshot and result before the single world commit.

Sold logs can feed same-tick production, purchased logs cannot, and new production output is tradeable only next tick. Expected stock/funds rejections return explicit `TradeOutcome` values and allow later commands to proceed. Exceptions roll back both inventories, both cash balances, all controls, production, and clock together; no successful-looking partial result escapes. This is an experimental phase rule, not a finalized economic time model.

`WorldSnapshot.Trading` is an optional immutable account snapshot; `Inventory` remains the sole market stock. `TradeAttempts` records ordered requests, quotes, outcomes, and before/after goods and cash. Rejected requests retain their quoted total with zero actual changes. No account service, message bus, persistent transaction history, or second simulation clock is introduced.
