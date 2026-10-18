# Provisional roadmap

This sequence describes planned work, not completed features or a fixed delivery commitment. Revisit it based on prototype results, especially whether each system creates understandable economic decisions and useful automation.

| Phase | Planned scope | Status |
| --- | --- | --- |
| 0 | Project foundation, small architecture, automated tests, canonical content, and documentation pipeline | Implemented |
| 0.5 | .NET 10 toolchain and minimal Unity integration using the existing simulation | Implemented; verification scope in [Unity integration](UNITY.md) |
| 1 | Deterministic simulation, markets, finite inventories, ships, basic trading, and trade automation | In progress; Task 1.1 implemented |
| 2 | Fixed-map scenarios, objectives, time limits, and roguelike runs | Planned |
| 3 | Constrained procedural world generation with economic viability checks | Planned |
| 4 | Modifiers, curses, difficulty levels, and challenge combinations | Planned |
| 5 | Lateral meta progression through starting packages, alternative production capabilities, and new economic problems | Planned |
| 6+ | Advanced competitors, piracy, political systems, optional combat, and other features justified by gameplay testing | Deferred |

Phase 0 includes logs, planks, a sawmill, one production conversion, validation, a console demo, headless tests, and generated reference output. Phase 0.5 exposes the same behavior through a Unity component and integration tests. Task 1.1 adds one fixed location, multiple runtime building instances, an abstract 64-bit tick, enable/disable commands, deterministic production scheduling, atomic world steps, and structured immutable results. It reuses the existing content and batch operation without adding markets, ships, or a graphical world interface.

## Task 1.1 scope and remaining questions

The world example uses two sawmills and configurable initial stock. It advances five ticks, demonstrates controls and resource exhaustion, and compares repeated runs including reversed building input order. Tests cover scheduling, scarcity, state ownership, and failures; actual Unity verification and its limits are recorded in [Unity integration](UNITY.md).

The implemented one-batch-per-tick schedule is provisional. Tick duration, allocation fairness, production costs, and future ordering of trade, arrivals, and consumption remain open. Fixed priority/ID ordering can starve later facilities, and per-tick copying has not been benchmarked at scale. No save/replay compatibility or concurrent access contract is promised.

## Proposed Task 1.2: finite markets and trading

Build one fixed local market and one trader using the existing goods, each with finite inventory and cash. Use explicit fixed prices as an initial experimental baseline. Choose bounded integer money units and decide the trade phase's relationship to production before implementation.

The task should provide explicit buy/sell commands, atomic goods-and-cash transfers, deterministic command ordering, structured outcomes, and a headless scenario showing stock depletion and liquidity limits. Test conservation, affordability boundaries, expected rejections, invalid requests, overflow rollback, and repeated-run equivalence. Keep the existing production behavior and Unity smoke working. [Economy](ECONOMY.md) describes the modeling questions this prototype should answer.

Exclude dynamic pricing, ships, routes, procedural content, new goods, consumption, and advanced automation from Task 1.2. Finite trading must be understandable before transport or automated strategies amplify its effects.

## Other follow-up work

1. **Verify the intended player backend and stripping configuration.** Run the Bootstrap smoke scene in an IL2CPP player if that is the intended shipping backend, recording platform and stripping level. Diagnose concrete compatibility failures before adding preservation rules.
2. **Evaluate the finite-market prototype.** Use reproducible scenarios to decide whether scarcity and cash limits create useful tradeoffs; only then select experiments in price formation and demand.
3. **Plan transport and minimal trade automation.** Two fixed ports and one ship remain later Phase 1 work, after local transactions and the ordering of new economic phases are established.

These follow-ups are proposals, not selected final economic rules. Keep random behavior out until there is a concrete requirement and an explicit reproducible seed strategy.
