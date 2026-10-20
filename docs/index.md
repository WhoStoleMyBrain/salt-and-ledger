# Salt & Ledger

Salt & Ledger is a planned single-player maritime trading and economic strategy game for Unity 6. Its central activity is intelligent optimization and automation of trading networks within a simulated economy.

The repository implements the foundation through Task 1.5's living archipelago: a Unity-independent C# simulation, canonical content, .NET 10 experiments/tests, generated reference documentation, and a map-first Unity UI Toolkit sandbox. Four towns extract logs, process and consume planks, and share finite markets with the player and two NPC merchants. Instant trading and departure are separate from economic days; geographic routes and bounded policies move physical cargo. The new regional owner reuses the established transaction, pricing, and production operations. Bootstrap, the two-port TradingLab, and their abstract-tick demos remain regression tools. An encyclopedia UI remains deferred.

## Read the project

- [Game design](GAME_DESIGN.md): non-negotiable principles and intended player experience.
- [Architecture](ARCHITECTURE.md): implemented modules, contracts, and dependencies.
- [Mechanics](MECHANICS.md): maintained contracts for production, finite trading, accounting, tick ordering, and rollback.
- [Content reference](reference/index.md): generated goods, building, and recipe definitions.
- [Economy](ECONOMY.md): accepted modeling goals and unresolved choices.
- [Pricing experiments](PRICING_EXPERIMENTS.md): reproducible fixed/linear/scarcity comparisons, measured limitations, and a provisional recommendation.
- [Manual shipping comparison](SHIPPING_DEMO.md): reproducible two-port expeditions, travel timing, cargo decisions, and realized gross profit.
- [Automation](AUTOMATION.md): configurable route rules, bounded waiting, player control, and manual-equivalent measured expeditions.
- [Living archipelago](LIVING_ARCHIPELAGO.md): towns, sources/sinks, NPC shipping, daily time, geography, and measured sixty-day strategies.
- [Playtest guides](PLAYTEST.md): ArchipelagoLab and legacy TradingLab controls, expeditions, blocked-route diagnosis, and policy recovery.
- [Roadmap](ROADMAP.md): provisional development sequence.
- [Decisions](DECISIONS.md): accepted foundation decisions and their tradeoffs.
- [Ideas](IDEAS.md): experiments and intentionally deferred concepts.
- [Unity integration](UNITY.md): reproducible preparation, Editor/player verification, and recorded limits.

## Canonical definitions and maintained rules

`data/content.json` is the source for both runtime content and generated reference pages. `data/content.schema.json` describes its structure; semantic validators check relationships and additional constraints. The generator also emits `generated/encyclopedia.json` for a future in-game reference consumer.

Generated reference pages describe only canonical facts, including explicit extraction recipes. Command semantics, legacy abstract ticks and regional days, fixed/dynamic prices, transactions, schedules, cargo, travel, policies, ordering, and error behavior are specified in [Mechanics](MECHANICS.md). Dynamic formulas and scenario values remain experimental. Extraction is explicit, consumption is physical without household payments, and automatic strategy discovery remains deferred. Long-term design direction is not a claim that those mechanics exist today.
