# Provisional roadmap

This sequence describes planned work, not completed features or a fixed delivery commitment. Revisit it based on prototype results, especially whether each system creates understandable economic decisions and useful automation.

| Phase | Planned scope | Status |
| --- | --- | --- |
| 0 | Project foundation, small architecture, automated tests, canonical content, and documentation pipeline | Implemented |
| 0.5 | .NET 10 toolchain and minimal Unity integration using the existing simulation | Implemented; verification scope in [Unity integration](UNITY.md) |
| 1 | Deterministic simulation, markets, finite inventories, ships, basic trading, and trade automation | In progress; Tasks 1.1, 1.2, and 1.2b implemented |
| 2 | Fixed-map scenarios, objectives, time limits, and roguelike runs | Planned |
| 3 | Constrained procedural world generation with economic viability checks | Planned |
| 4 | Modifiers, curses, difficulty levels, and challenge combinations | Planned |
| 5 | Lateral meta progression through starting packages, alternative production capabilities, and new economic problems | Planned |
| 6+ | Advanced competitors, piracy, political systems, optional combat, and other features justified by gameplay testing | Deferred |

Phase 0 includes logs, planks, a sawmill, one production conversion, validation, a console demo, headless tests, and generated reference output. Phase 0.5 exposes the same behavior through a Unity component and integration tests. Task 1.1 adds one fixed location, multiple runtime building instances, an abstract 64-bit tick, enable/disable commands, deterministic production scheduling, atomic world steps, and structured immutable results. Task 1.2 extends that world with one finite market, one trader, separate goods/cash accounts, and explicit trading. Task 1.2b adds exact stock-sensitive pricing, read-only trade previews, and reproducible pricing comparisons. All use the same canonical content and production operation; no ships or graphical market interface are implemented.

## Task 1.1 scope and remaining questions

The world example uses two sawmills and configurable initial stock. It advances five ticks, demonstrates controls and resource exhaustion, and compares repeated runs including reversed building input order. Tests cover scheduling, scarcity, state ownership, and failures; actual Unity verification and its limits are recorded in [Unity integration](UNITY.md).

The implemented one-batch-per-tick schedule is provisional. Tick duration, allocation fairness, production costs, and future arrival/consumption phases remain open. Task 1.2 orders trades before production as an experimental rule. Fixed priority/ID ordering can starve later facilities, and per-tick copying has not been benchmarked at scale. No save/replay compatibility or concurrent access contract is promised.

## Task 1.2: finite markets, trading, and accounting

Implemented: positive fixed bid/ask quotes, nonnegative 64-bit integer cash, separate inventories, ordered all-or-nothing buy/sell commands, four explicit stock/funds rejections, and complete exceptional tick rollback. The existing world inventory is market stock, also used by production; public-market ownership of factories is a prototype assumption.

The `--market` demo exposes goods/cash changes across four ticks, checks its documented final state, and compares repeated/reordered setups. Tests verify accounting, conservation, numeric boundaries, ordering, immutability, culture independence, and rollback. A Unity Edit Mode test exercises a sale feeding production and a later purchase using the staged DLL. [Unity integration](UNITY.md) records actual execution and limits; [Mechanics](MECHANICS.md) specifies the implemented rules.

Task 1.2's fixed quotes remain an unchanged experimental baseline alongside the dynamic alternatives below. No stock/cash replenishment, ships, routes, procedural content, extra goods, consumption, or automation has been added.

## Task 1.2b: dynamic market pricing experiments

Implemented: selectable linear and piecewise scarcity pricing alongside the fixed baseline; exact marginal totals for bulk orders; read-only quotes/previews using execution's calculation; immutable historical prices; and a bounded summation algorithm for large quantities. Prices follow authoritative inventory through trading and production, with no independent price clock. Whole-tick rollback and finite account constraints remain intact.

The `--pricing` runner compares high/moderate/severe stock conditions, production, repeated trades, finite liquidity, and numeric limits. It emits a deterministic JSON report with a read-only freshness check. Tests cover marginal sums, preview agreement, bounds, rounding, order splitting, closed inventory cycles, failures, and fixed-mode compatibility. The [experiment report](PRICING_EXPERIMENTS.md) contains observed comparisons and a provisional recommendation; [Unity integration](UNITY.md) records the verification actually performed.

These implementations establish pricing correctness under the stated contracts, not final balance or enjoyable gameplay. Clamping and integer rounding can flatten responses; the scarcity knee can amplify late purchases; finite cash may prevent an otherwise attractive trade. Targets smaller than 8 do not expose a distinct scarcity branch. The relationship between price depth and the existing production conversion still needs calibration.

## Recommended next task: price and production calibration

Run focused comparative scenarios using the existing goods, production conversion, and finite accounts. Vary reference values, targets, sensitivity, spread, and initial liquidity explicitly; observe whether the cost of logs, value of planks, production cadence, and cash concentration support understandable choices. Keep fixed and alternative curves available, and use the current report as the reproducible baseline. Compare decision consequences before selecting final defaults or adding economic systems.

This is proposed work, not implemented tuning or a permanent model choice. Do not add replenishment, consumption, transport, or automation merely to make prices move. See [Economy](ECONOMY.md) for the remaining modeling questions.

## Other follow-up work

1. **Verify the intended player backend and stripping configuration.** Run the Bootstrap smoke scene in an IL2CPP player if that is the intended shipping backend, recording platform and stripping level. Diagnose concrete compatibility failures before adding preservation rules.
2. **Calibrate local prices and production.** Follow the focused task above; defer consumption/demand systems until their role is justified.
3. **Plan transport and minimal trade automation.** Two fixed ports and one ship remain later Phase 1 work, after local transactions and the ordering of new economic phases are established.

These follow-ups are proposals, not selected final economic rules. Keep random behavior out until there is a concrete requirement and an explicit reproducible seed strategy.
