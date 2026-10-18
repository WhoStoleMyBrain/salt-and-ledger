# Salt & Ledger

Salt & Ledger is a planned single-player maritime trading and economic strategy game for Unity 6. Its central activity is intelligent optimization and automation of trading networks within a simulated economy.

The current repository contains the Phase 0/0.5 foundation and Task 1.1 deterministic world simulation: a Unity-independent C# simulation, validated sample content, a .NET 10 console demonstration and tests, generated reference documentation, and a minimal Unity 6 smoke scene. A fixed world advances through abstract integer ticks, with two sawmills sharing finite stock, explicit production controls, stable scheduling, and immutable step results. The Unity host uses the existing simulation assembly; no playable economic game or encyclopedia UI has been implemented.

## Read the project

- [Game design](GAME_DESIGN.md): non-negotiable principles and intended player experience.
- [Architecture](ARCHITECTURE.md): implemented modules, contracts, and dependencies.
- [Mechanics](MECHANICS.md): maintained contracts for production, world ticks, controls, scheduling, and rollback.
- [Content reference](reference/index.md): generated goods, building, and recipe definitions.
- [Economy](ECONOMY.md): accepted modeling goals and unresolved choices.
- [Roadmap](ROADMAP.md): provisional development sequence.
- [Decisions](DECISIONS.md): accepted foundation decisions and their tradeoffs.
- [Ideas](IDEAS.md): experiments and intentionally deferred concepts.
- [Unity integration](UNITY.md): reproducible preparation, Editor/player verification, and recorded limits.

## Canonical definitions and maintained rules

`data/content.json` is the source for both runtime content and generated reference pages. `data/content.schema.json` describes its structure; semantic validators check relationships and additional constraints. The generator also emits `generated/encyclopedia.json` for a future in-game reference consumer.

Generated reference pages describe only facts present in the data. Command semantics, abstract time, ordering, and error behavior are specified in [Mechanics](MECHANICS.md). Markets and monetary transactions remain future work. Long-term design direction is not a claim that those mechanics exist today.
