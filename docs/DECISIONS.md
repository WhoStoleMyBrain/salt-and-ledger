# Architectural decisions

These records describe accepted foundation decisions as of 2026-10-17. Unresolved game rules live in [Economy](ECONOMY.md); hypotheses and deferred concepts live in [Ideas](IDEAS.md). New evidence can supersede a decision explicitly.

## ADR 001: Keep the simulation independent of Unity

**Status:** Accepted.

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

**Status:** Accepted for the Phase 0 sample.

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
