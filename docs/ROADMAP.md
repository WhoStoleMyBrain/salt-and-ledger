# Provisional roadmap

This sequence describes planned work, not completed features or a fixed delivery commitment. Revisit it based on prototype results, especially whether each system creates understandable economic decisions and useful automation.

| Phase | Planned scope | Status |
| --- | --- | --- |
| 0 | Project foundation, small architecture, automated tests, canonical content, and documentation pipeline | Implemented |
| 0.5 | .NET 10 toolchain and minimal Unity integration using the existing simulation | Implemented; verification scope in [Unity integration](UNITY.md) |
| 1 | Deterministic simulation, markets, finite inventories, ships, basic trading, and trade automation | In progress; Tasks 1.1, 1.2, 1.2b, 1.3a, and 1.3b implemented |
| 2 | Fixed-map scenarios, objectives, time limits, and roguelike runs | Planned |
| 3 | Constrained procedural world generation with economic viability checks | Planned |
| 4 | Modifiers, curses, difficulty levels, and challenge combinations | Planned |
| 5 | Lateral meta progression through starting packages, alternative production capabilities, and new economic problems | Planned |
| 6+ | Advanced competitors, piracy, political systems, optional combat, and other features justified by gameplay testing | Deferred |

Phase 0 includes logs, planks, a sawmill, one production conversion, validation, a console demo, headless tests, and generated reference output. Phase 0.5 exposes the same behavior through a Unity component and integration tests. Task 1.1 adds one fixed location, multiple runtime building instances, an abstract 64-bit tick, enable/disable commands, deterministic production scheduling, atomic world steps, and structured immutable results. Task 1.2 extends that world with one finite market, one trader, separate goods/cash accounts, and explicit trading. Task 1.2b adds exact stock-sensitive pricing, read-only trade previews, and reproducible pricing comparisons. Task 1.3a adds two ports, one shared trader account, and one manually sailed cargo ship. Task 1.3b executes configurable route policies through the same maritime commands. All reuse the same canonical content, transaction/pricing calculations, and production operation. No graphical trading interface is implemented.

## Task 1.1 scope and remaining questions

The world example uses two sawmills and configurable initial stock. It advances five ticks, demonstrates controls and resource exhaustion, and compares repeated runs including reversed building input order. Tests cover scheduling, scarcity, state ownership, and failures; actual Unity verification and its limits are recorded in [Unity integration](UNITY.md).

The implemented one-batch-per-tick schedule is provisional. Tick duration, allocation fairness, production costs, and future consumption phases remain open. Task 1.2 orders trades before production as an experimental rule; Task 1.3a places travel progress and arrival after production. Fixed priority/ID ordering can starve later facilities, and per-tick copying has not been benchmarked at scale. No save/replay compatibility or concurrent access contract is promised.

## Task 1.2: finite markets, trading, and accounting

Implemented: positive fixed bid/ask quotes, nonnegative 64-bit integer cash, separate inventories, ordered all-or-nothing buy/sell commands, four explicit stock/funds rejections, and complete exceptional tick rollback. The existing world inventory is market stock, also used by production; public-market ownership of factories is a prototype assumption.

The `--market` demo exposes goods/cash changes across four ticks, checks its documented final state, and compares repeated/reordered setups. Tests verify accounting, conservation, numeric boundaries, ordering, immutability, culture independence, and rollback. A Unity Edit Mode test exercises a sale feeding production and a later purchase using the staged DLL. [Unity integration](UNITY.md) records actual execution and limits; [Mechanics](MECHANICS.md) specifies the implemented rules.

Task 1.2's fixed quotes remain an unchanged experimental baseline alongside the dynamic alternatives below. Its single-location demo remains unchanged by the later shipping and automation prototypes. Stock/cash replenishment, procedural content, extra goods, and consumption remain deferred.

## Task 1.2b: dynamic market pricing experiments

Implemented: selectable linear and piecewise scarcity pricing alongside the fixed baseline; exact marginal totals for bulk orders; read-only quotes/previews using execution's calculation; immutable historical prices; and a bounded summation algorithm for large quantities. Prices follow authoritative inventory through trading and production, with no independent price clock. Whole-tick rollback and finite account constraints remain intact.

The `--pricing` runner compares high/moderate/severe stock conditions, production, repeated trades, finite liquidity, and numeric limits. It emits a deterministic JSON report with a read-only freshness check. Tests cover marginal sums, preview agreement, bounds, rounding, order splitting, closed inventory cycles, failures, and fixed-mode compatibility. The [experiment report](PRICING_EXPERIMENTS.md) contains observed comparisons and a provisional recommendation; [Unity integration](UNITY.md) records the verification actually performed.

These implementations establish pricing correctness under the stated contracts, not final balance or enjoyable gameplay. Clamping and integer rounding can flatten responses; the scarcity knee can amplify late purchases; finite cash may prevent an otherwise attractive trade. Targets smaller than 8 do not expose a distinct scarcity branch. The relationship between price depth and the existing production conversion still needs calibration.

## Task 1.3a: two ports, one ship, manual trading

Implemented: one `MaritimeWorldState` owns two independent finite markets, a shared trader cash account, one ship with capacity-limited cargo, and one atomic global tick. Buy/sell commands require the starting dock. Manual departure uses one fixed bidirectional connection, with travel progressing once at the end of each tick, including departure. Arrival permits destination trading on the following step. Each port produces locally using the existing operation; both use existing fixed, linear, or scarcity pricing and the shared transaction calculator.

The `--shipping` demo compares 2-log and 6-log outbound shipments followed by identical 2-plank returns. Both end after six successful ticks with empty cargo; realized gross profits are 69 and 119 under the documented parameters. The runner checks exact balances, capacity rejection, illegal transit trading, repeatability, and reversed port input order. [Manual shipping comparison](SHIPPING_DEMO.md) records parameters, timing, measurements, and limits. [Unity integration](UNITY.md) records the integration checks actually executed.

Cargo uses one unit per item; the ship is the trader's sole warehouse. The ports are handcrafted, information is perfect, production is market-owned, and travel has no costs or risk. Only one trader and one ship are supported. Those are provisional scope choices, not final balance or gameplay claims.

## Task 1.3b: configurable trade-route automation

Implemented: immutable two-port policies with one distinct commodity per direction, shipment target/minimum, worst marginal buy/sell limits, a cash reserve, and optional empty return. Stateless evaluation selects the largest feasible quantity using bounded search over existing exact calculations, then returns ordinary commands or explicit wait/pause reasons. Snapshot-bound execution prevents stale buy-and-depart decisions and preserves manual timing, account conservation, and whole-tick rollback. Host enablement/reconfiguration adds no mutable route state or second clock.

The `--automation` demo reproduces the six-log manual expedition's 119 gross profit and the restrictive two-log expedition's 69, both over six ticks with identical manual states. A zero-liquidity destination instead produces three unchanged waits, then a bounded stop at tick 5 with all six logs retained. The [automation guide](AUTOMATION.md) records parameters, measurements, input-count conventions, exact waiting rules, and limits. Existing pricing reports and canonical content remain unchanged. [Unity integration](UNITY.md) records actual integration verification.

This policy does not forecast destination prices, guarantee recovery, optimize strategy, or resolve mixed cargo. Finite markets can remain blocked indefinitely; the host runner limits observation and reports no progress without repairing the economy. Mechanical equivalence and fewer prescribed action batches do not establish enjoyable or balanced gameplay.

## Recommended next milestone: policy control and recovery

Proposed next assignment: a small fixed-scenario experiment focused on explaining and recovering from blocked routes. Use existing commands to compare initial stock/liquidity and policy limits, measure temporary versus persistent waits, and exercise pause, manual intervention, and reconfiguration. Define what a player should understand and what recovery means before selecting a presentation layer or additional economic systems. The current reason enums and immutable results provide an inspectable starting point.

Keep this narrow: no additional ships/traders, warehouses, pathfinding, demand, replenishment, random risk, full route editor, or persistent event history merely to extend the experiment. This is a recommendation requiring a separate task, not an implemented gameplay system.

## Other follow-up work

1. **Verify the intended player backend and stripping configuration.** Run the Bootstrap smoke scene in an IL2CPP player if that is the intended shipping backend, recording platform and stripping level. Diagnose concrete compatibility failures before adding preservation rules.
2. **Calibrate local prices and production.** Vary price depth, conversion margins, and starting liquidity in reproducible scenarios; defer consumption/demand systems until their role is justified.
3. **Measure simulation cost before enlarging the world.** Snapshot copying and result allocation favor clear atomicity; current verification does not establish throughput for large maps or fleets.

These follow-ups are proposals, not selected final economic rules. Keep random behavior out until there is a concrete requirement and an explicit reproducible seed strategy.
