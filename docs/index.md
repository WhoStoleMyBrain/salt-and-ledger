# Salt & Ledger

Salt & Ledger is a planned single-player maritime trading and economic strategy game for Unity 6. Its central activity is intelligent optimization and automation of trading networks within a simulated economy.

The current repository contains the Phase 0/0.5 foundation, Task 1.1 deterministic world, and Task 1.2 finite trading/accounting prototype: a Unity-independent C# simulation, validated sample content, .NET 10 demos and tests, generated reference documentation, and a minimal Unity 6 smoke scene. A fixed world advances through abstract integer ticks, with shared production stock, a separate trader inventory, finite cash, fixed experimental quotes, atomic trades, and immutable results. The Unity host uses the existing simulation assembly; no playable economic game or encyclopedia UI has been implemented.

## Read the project

- [Game design](GAME_DESIGN.md): non-negotiable principles and intended player experience.
- [Architecture](ARCHITECTURE.md): implemented modules, contracts, and dependencies.
- [Mechanics](MECHANICS.md): maintained contracts for production, finite trading, accounting, tick ordering, and rollback.
- [Content reference](reference/index.md): generated goods, building, and recipe definitions.
- [Economy](ECONOMY.md): accepted modeling goals and unresolved choices.
- [Roadmap](ROADMAP.md): provisional development sequence.
- [Decisions](DECISIONS.md): accepted foundation decisions and their tradeoffs.
- [Ideas](IDEAS.md): experiments and intentionally deferred concepts.
- [Unity integration](UNITY.md): reproducible preparation, Editor/player verification, and recorded limits.

## Canonical definitions and maintained rules

`data/content.json` is the source for both runtime content and generated reference pages. `data/content.schema.json` describes its structure; semantic validators check relationships and additional constraints. The generator also emits `generated/encyclopedia.json` for a future in-game reference consumer.

Generated reference pages describe only facts present in the data. Command semantics, abstract time, fixed market quotes, monetary transactions, ordering, and error behavior are specified in [Mechanics](MECHANICS.md). Dynamic pricing and replenishment are not implemented. Long-term design direction is not a claim that those mechanics exist today.
