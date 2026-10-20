# Salt & Ledger

Salt & Ledger is a planned single-player maritime trading and economic strategy game for Unity 6. Its central activity is intelligent optimization and automation of trading networks within a simulated economy.

The current repository contains the Phase 0/0.5 foundation through Task 1.4's playable Unity TradingLab: a Unity-independent C# simulation, validated sample content, .NET 10 demos and tests, generated reference documentation, and a UI Toolkit trading interface alongside the original smoke scene. Local and maritime worlds advance through abstract integer ticks with finite markets, experimental prices, atomic trades, and immutable results. The maritime world owns two independent ports, one trader account, and one capacity-limited ship under one global clock. Stateless policies issue the same maritime commands under player-selected quantity/price rules. The Unity host presents manual trading, configurable automation, explicit time, and blocked-route explanations using the existing simulation assembly. An encyclopedia UI remains deferred.

## Read the project

- [Game design](GAME_DESIGN.md): non-negotiable principles and intended player experience.
- [Architecture](ARCHITECTURE.md): implemented modules, contracts, and dependencies.
- [Mechanics](MECHANICS.md): maintained contracts for production, finite trading, accounting, tick ordering, and rollback.
- [Content reference](reference/index.md): generated goods, building, and recipe definitions.
- [Economy](ECONOMY.md): accepted modeling goals and unresolved choices.
- [Pricing experiments](PRICING_EXPERIMENTS.md): reproducible fixed/linear/scarcity comparisons, measured limitations, and a provisional recommendation.
- [Manual shipping comparison](SHIPPING_DEMO.md): reproducible two-port expeditions, travel timing, cargo decisions, and realized gross profit.
- [Automation](AUTOMATION.md): configurable route rules, bounded waiting, player control, and manual-equivalent measured expeditions.
- [Trading Lab playtest](PLAYTEST.md): controls, manual and automated expeditions, blocked-route diagnosis, and policy recovery.
- [Roadmap](ROADMAP.md): provisional development sequence.
- [Decisions](DECISIONS.md): accepted foundation decisions and their tradeoffs.
- [Ideas](IDEAS.md): experiments and intentionally deferred concepts.
- [Unity integration](UNITY.md): reproducible preparation, Editor/player verification, and recorded limits.

## Canonical definitions and maintained rules

`data/content.json` is the source for both runtime content and generated reference pages. `data/content.schema.json` describes its structure; semantic validators check relationships and additional constraints. The generator also emits `generated/encyclopedia.json` for a future in-game reference consumer.

Generated reference pages describe only facts present in the data. Command semantics, abstract time, fixed/dynamic prices, monetary transactions, cargo, travel, policy evaluation, ordering, and error behavior are specified in [Mechanics](MECHANICS.md). Dynamic formulas remain experimental; replenishment, general demand simulation, and automatic strategy discovery are not implemented. Long-term design direction is not a claim that those mechanics exist today.
