# Provisional roadmap

This sequence describes planned work, not completed features or a fixed delivery commitment. Revisit it based on prototype results, especially whether each system creates understandable economic decisions and useful automation.

| Phase | Planned scope | Status |
| --- | --- | --- |
| 0 | Project foundation, small architecture, automated tests, canonical content, and documentation pipeline | Implemented |
| 0.5 | .NET 10 toolchain and minimal Unity integration using the existing simulation | Implemented; verification scope in [Unity integration](UNITY.md) |
| 1 | Deterministic simulation, finite markets, shipping, automation, private industry, and a living economy slice | Tasks 1.1 through 1.7 add six goods, four towns, private factories, industrial logistics, and historical material-cost attribution |
| 2 | Scenario objectives, time limits, and replayable runs | Task 1.7 adds three handcrafted challenges, selected starting companies, terminal results, and capped scoring; no procedural or persistent progression systems |
| 3 | Constrained procedural world generation with economic viability checks | Planned |
| 4 | Modifiers, curses, difficulty levels, and challenge combinations | Planned |
| 5 | Lateral meta progression through starting packages, alternative production capabilities, and new economic problems | Three replacement starting packages available immediately; persistent unlocks and broader progression remain planned |
| 6+ | Advanced competitors, piracy, political systems, optional combat, and other features justified by gameplay testing | Deferred |

Phase 0 includes logs, planks, a sawmill, one production conversion, validation, a console demo, headless tests, and generated reference output. Phase 0.5 exposes the same behavior through a Unity component and integration tests. Task 1.1 adds one fixed location, multiple runtime building instances, an abstract 64-bit tick, enable/disable commands, deterministic production scheduling, atomic world steps, and structured immutable results. Task 1.2 extends that world with one finite market, one trader, separate goods/cash accounts, and explicit trading. Task 1.2b adds exact stock-sensitive pricing, read-only trade previews, and reproducible pricing comparisons. Task 1.3a adds two ports, one shared trader account, and one manually sailed cargo ship. Task 1.3b executes configurable route policies through the same maritime commands. Task 1.4 makes that world playable through a focused Unity UI Toolkit interface. All reuse the same canonical content, transaction/pricing calculations, and production operation.

## Task 1.1 scope and remaining questions

The world example uses two sawmills and configurable initial stock. It advances five ticks, demonstrates controls and resource exhaustion, and compares repeated runs including reversed building input order. Tests cover scheduling, scarcity, state ownership, and failures; actual Unity verification and its limits are recorded in [Unity integration](UNITY.md).

The legacy one-batch-per-abstract-tick schedule remains intact. Task 1.5 adds a separate provisional daily cadence and explicit consumption phase rather than changing that API. Allocation fairness and unmodeled labor/operating costs remain open; Task 1.7 attributes actual private input-material expenditure. Fixed priority/ID ordering can starve later facilities. Representative small-world measurements do not establish large-map scalability. No save/replay compatibility or concurrent access contract is promised.

## Task 1.2: finite markets, trading, and accounting

Implemented: positive fixed bid/ask quotes, nonnegative 64-bit integer cash, separate inventories, ordered all-or-nothing buy/sell commands, four explicit stock/funds rejections, and complete exceptional tick rollback. The existing world inventory is market stock, also used by production; public-market ownership of factories is a prototype assumption.

The `--market` demo exposes goods/cash changes across four ticks, checks its documented final state, and compares repeated/reordered setups. Tests verify accounting, conservation, numeric boundaries, ordering, immutability, culture independence, and rollback. A Unity Edit Mode test exercises a sale feeding production and a later purchase using the staged DLL. [Unity integration](UNITY.md) records actual execution and limits; [Mechanics](MECHANICS.md) specifies the implemented rules.

Task 1.2's fixed quotes remain an unchanged experimental baseline alongside the dynamic alternatives below. Its single-location demo remains unchanged by later milestones and has no extraction or consumption schedule. Task 1.5 introduces those explicit physical flows only in the regional scenario; hidden stock/cash replenishment remains excluded.

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

## Task 1.4: first playable Unity Trading Lab

Implemented: a separate TradingLab scene with a two-port map, ship/cargo/cash status, canonical market information, exact trade previews, manual trading/sailing, explicit tick controls, editable outbound automation limits, bounded execution, and a finite recent-activity display. The Standard, Restrictive, and No destination cash scenarios expose the existing comparison outcomes. Pausing, applying policy edits, and inspecting markets leave time and the economy unchanged; reset clearly starts a new test world. Bootstrap remains the production smoke.

The [playtest guide](PLAYTEST.md) provides a manual expedition, automated comparisons, a price-limit recovery, and a liquidity-blocked diagnosis. [Unity integration](UNITY.md) records actual scene/control, visual, and player verification separately. This is a working host for experiments, not proof of usability, economic sustainability, final balance, or a complete game.

## Task 1.5: living archipelago

Implemented: one regional owner with instant revisioned trade/departure, daily atomic advancement, four finite towns, scheduled extraction/processing, physical consumption, three independently owned ships/accounts, integer geographic routes, and bounded same-day policies. The map-first ArchipelagoLab exposes port/ship selection, confirmed voyages, smooth presentation, pause/step/speed controls, exact trading, and fully editable outbound/return rules. Bootstrap, TradingLab, and legacy examples remain available.

The [sixty-day report](LIVING_ARCHIPELAGO.md#reproducible-sixty-day-experiments) demonstrates recurring NPC shipments and materially different player strategies under one starting world. It also reveals unmet demand at unserved towns, price-floor waits, idle production ceilings, and competition that reduces NPC deliveries. These are measured limitations, not reasons for hidden subsidies or a claim that the economy is balanced. [Unity integration](UNITY.md) records actual tests, visuals, and player checks.

## Task 1.6: commerce, industry, and economic challenge

Implemented: a centered contextual trading overlay with authoritative exact totals and average execution prices; six canonical goods and interconnected food/timber chains; company accounts; private facilities with finite storage; real construction payments; local procurement and surplus-sale policies; and physical ship/warehouse transfers. `EnterpriseScenario` supplies the current ArchipelagoLab and the new deterministic report. `LegacyContent.Timber` reuses canonical definition objects to preserve older demonstrations and the unchanged Task 1.5 report.

The optional Coastal Supply Charter requires 24 net Bread supplied to Eastport by day 60 with at least 100 company cash. Completed owner transactions determine progress; target-market buybacks subtract credit. The same authoritative clock controls production, voyages, and the deadline. Merchant and manufacturing/shipping trials succeed on days 29 and 46 respectively; idle play fails at day 60. The unlimited sandbox remains available.

Five ninety-day experiments reconcile all company/market cash and six-good physical flows, recording capital, purchases, actual revenue, private output, and held goods separately. They show different outcomes rather than equalizing strategies: workshop investment leaves cash 356, local food production 705, and manufacturing/shipping 1,637 from an initial 600. These are cash observations with different unsold assets, not ranked profitability claims. See [Commerce and industry](ENTERPRISE.md) for assumptions, rules, and the report; [Unity integration](UNITY.md) records actual verification scope.

Task 1.6 deliberately deferred historical acquisition-cost attribution. Task 1.7 implements it below; the earlier report's fields and measured strategies remain unchanged. Current market quotes are never used as fabricated acquisition history. Scenario balance, long-horizon liquidity, and human usability remain open.

## Task 1.7: the complete run experience

The current run foundation offers a main menu, challenge/charter setup, pause/navigation, confirmed active-run replacement or abandonment, terminal results, and retry without relaunching. Three replacement charters trade liquidity, initial private industry, cargo, capacity, and location. All are available immediately; there are no stacking upgrades, saves, persistent unlocks, curses, or ascensions. [Runs](RUNS.md) records the exact initial assets and their limited replacement-quote comparisons.

Coastal Supply preserves its signed net-delivery protection. Regional Relief counts actual scheduled Bread consumption across the region, including public and NPC contributions. Enterprise Ambition requires private Bread production, attributed realized material gross margin, and cash. All use the same authoritative clock, day-60 deadline, terminal freeze, and transparent capped results score. The complete-run experiment demonstrates active success and idle failure for every challenge/charter pairing; it does not claim every strategy is viable or packages are equally advantaged.

Industrial policies connect private production to real loading, voyages, destination sales/unloading, and optional useful return inputs. Explicit stages support same-good directional legs; finite stock, warehouse reserves, exact price constraints, cash, capacity, and ownership remain enforced. A four-action dockside bound removes artificial transaction-day delays while preserving one clock. Manual/automatic comparisons reconcile identically, and blocked shipments retain cargo.

FIFO open acquisition lots retain exact historical paid basis through partial sales, physical transfers, and private transformations. Structured per-operation events and authoritative aggregates distinguish inventory purchases, released and held basis, sale revenue, construction capital, and liquid cash. Starter grants keep provenance and zero paid expenditure. This is material-cost accounting, not a complete model of labor, depreciation, freight fees, taxes, or realizable asset values.

The offline in-game encyclopedia consumes the generated canonical reference and shared authored help; the external MkDocs site separates player help from development evidence. Canonical recipes are not duplicated in the host. GitHub Pages preparation remains separate from publishing status, documented in [Website](WEBSITE.md). [Unity integration](UNITY.md) records actual Editor/player checks and remaining platform limitations rather than treating a headless test as visual verification.

The new `--runs` and `--industrial-logistics` reports are separate from historical pricing, archipelago, and enterprise regression fixtures. [Automation](AUTOMATION.md#reproducible-industrial-shipping-comparison) explains actual shipment/input reductions and finite blockage. The complete-run and reference changes add no procedural generation, general route programming, fleet optimization, multiplayer, or online service.

## Recommended next milestone: observed playtesting

Proposed next assignment: observe players from application startup through a completed result without source-code explanations. Ask them to select a charter, interpret the objective and bulk execution price, choose between a shipment and a factory, configure an entire industrial route, and diagnose a blocked shipment. Have them use the encyclopedia and explain the difference between liquid cash, granted assets, held basis, sale revenue, material margin, and construction spending.

Record whether ownership, actual consumption versus delivery, route stage, deadlines, abandonment, and retry are understandable. Compare attention required by merchant and industrial approaches without treating fewer commands as proof of enjoyment. Review whether Independent Traders' larger hold and Industrial Consortium's lower liquidity create meaningful choices under different objectives.

Pair observed confusion with focused presentation fixes, then extend deterministic liquidity and industry-policy observations beyond ninety days. Investigate input shortages, output price floors, unsold goods, and capital opportunity costs before changing balance or scoring. Do not add procedural maps, extra commodities, household finance, random risk, or unrelated systems merely to keep activity running. Human playtesting and resulting revisions are a proposed assignment, not completed user-research evidence.

## Other follow-up work

1. **Verify the intended player backend and stripping configuration.** Run the Bootstrap smoke scene in an IL2CPP player if that is the intended shipping backend, recording platform and stripping level. Diagnose concrete compatibility failures before adding preservation rules.
2. **Calibrate the enterprise network and run choices.** Vary existing construction costs, procurement limits, source thresholds, production/consumption schedules, sale floors, and finite initial liquidity. Keep all challenge/charter feasibility and idle-failure checks. Distinguish temporary stock shortage, distribution failure, policy blockage, and liquidity exhaustion without introducing hidden money. Do not normalize scores across packages using an invented ship value.
3. **Measure simulation cost before enlarging the world.** Snapshot copying and result allocation favor clear atomicity. Live cost lots depend on held acquisitions; bounded event journals do not imply a constant bound on all inventory-lot counts. Extend representative performance/allocation measurements before adding larger maps or fleets.

These follow-ups are proposals, not selected final economic rules. Keep random behavior out until there is a concrete requirement and an explicit reproducible seed strategy.
