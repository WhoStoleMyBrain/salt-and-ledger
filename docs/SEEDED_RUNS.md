# Seeded archipelago and Slow Foundations

This page preserves Task 1.8's geography-only **v1** contract and experiment. Task 1.9 adds an explicit [Living Cities v2](LIVING_CITIES.md) mode; a v1 seed never silently selects its different economy. Task 1.8 varies the geography of the existing four-town economy and adds one optional construction modifier. Goods, recipes, town roles, price parameters, demand, public industry, NPCs, starting charters and challenge thresholds remain authored. No seed receives extra cash, stock or time to rescue a strategy. [Run choices and scoring](RUNS.md), [economic analysis](ECONOMY.md), and the [generated experiment](experiments/seeded-run-results.json) provide the corresponding rules and measurements.

## Choose and share a world

New Run and Sandbox setup offer **Fixed / Classic**, **Seeded Archipelago v1**, and **Living Cities v2**, the three starting charters and Ascension 0/1. Classic/v1 keep the three historical goals; Living Cities offers the city-recovery objective. Preview the chosen positions, six direct connections and voyage durations before starting.

A seed is a signed 32-bit decimal integer, from **-2147483648 through 2147483647**. ASCII digits and one optional leading `+` or `-` are accepted; leading zeros are harmless. Whitespace, decimal points, grouping separators, exponents, non-ASCII numerals and values outside that range are rejected. `-0`, `+0` and `0` identify the same world. The host selects random seeds only when explicitly requested; the portable generator never reads a clock or random global state.

Record the complete identity when sharing a run: challenge, starting charter, world mode, seed when present, generator version and ascension. **Retry Same World** reuses that identity and restores initial funds, cargo, markets, facilities, policies, schedules, accounting lots, day and revision; the host clears its activity journal. **New World / Reroll** changes the setup seed, shows its preview and requires a subsequent explicit start. **Change Setup** returns to editable selections. Reading the encyclopedia, changing text size or opening a menu does not reroll anything.

Classic has no seed and uses generator version `0`. Geography-only mode uses version `1`; Living Cities explicitly uses economic version `2` with the identical v1 geography for that seed. Unsupported mode/version combinations are rejected. Living identity also includes StockOnly/Coverage pricing. There is no save-game or durable replay format. Copying a seed alone does not specify the charter, challenge or difficulty. Results qualify their comparison group with those choices and the exact world identity; scores from different seeds or ascensions are not equivalent leaderboard placements. Legacy headless result APIs retain their historical group strings for identity-free fixtures only.

## Generator version 1

`SeededArchipelago.Generate(seed)` is a pure function in the simulation library. `RunScenario.PreviewGeography(identity)` and world creation call that same function. `EnterpriseScenario` still owns the sole economic configuration; generated geography supplies only positions and sea routes. `ArchipelagoScenario` and old enterprise/run entry points retain their original behavior.

The chart uses the existing abstract **1000 by 700** coordinate plane. The four slots are `(250,200)`, `(750,200)`, `(250,500)` and `(750,500)`.

1. Interpret the signed seed as its unchanged 32-bit unsigned bit pattern.
2. Each draw adds `0x9E3779B9` to an unsigned state, with modulo-2^32 overflow. Apply the integer avalanche: XOR with right shift 16, multiply by `0x85EBCA6B`, XOR with right shift 13, multiply by `0xC2B2AE35`, then XOR with right shift 16. Multiplication also wraps modulo 2^32.
3. Three Fisher-Yates draws permute the slots, processing indices 3, 2, 1 and selecting `draw % (index + 1)`. This deliberately versioned modulo mapping is deterministic; it is not a statistical claim of perfect uniformity.
4. In ordinal port-ID order (`eastport`, `northport`, `southhaven`, `westhaven`), two draws add independent X/Y jitter `draw % 81 - 40`.
5. For each of the six unordered town pairs, compute integer squared separation and the smallest positive integer distance whose square is at least that value. This is **ceiling Euclidean distance**, calculated by bounded integer bisection, with no floating-point rounding.

Exactly eleven draws are consumed. There is no rejection sampling, retry loop, role reassignment based on economic success, or input collection ordering dependency. Positions and routes are immutable and ordinally ordered. A stable signature records version, all positions and all distances without culture-dependent formatting or process hashes.

### Bounds and meaning

Generated X lies in **210–790** and Y in **160–540**. Vertically adjacent slots remain at least `300 - 80 = 220` units apart; horizontally adjacent slots remain at least `500 - 80 = 420` units apart. Every pair therefore has separation at least 220. The longest possible delta is `(580,380)`, so distance is at most `ceil(sqrt(580² + 380²)) = 694`.

Every generated map has exactly four distinct towns and six positive, direct, undirected routes. All starting ships retain speed **100**, producing voyages of **3–7 economic days** through the existing `SeaRoute.DurationDays(speed)` integer ceiling. Other positive speeds use the same distance and duration calculation. Each square-root search needs at most ten bisections. These structural bounds hold for every int32 seed; the tests additionally sample 4,098 seeds and preserve exact golden vectors for representative and boundary seeds.

Distances are abstract chart units, not nautical miles. Routes are straight connections; island art does not imply terrain collision avoidance or pathfinding. The host draws authoritative positions and reads distances/durations from the same routes used by departures. Presentation scale and ship interpolation cannot change sailing time.

### Example maps

| Town | Seed 0 position | Seed 42 position |
| --- | --- | --- |
| Eastport | 717, 173 | 715, 475 |
| Northport | 789, 473 | 246, 502 |
| Southhaven | 286, 202 | 787, 160 |
| Westhaven | 269, 534 | 222, 200 |

| Connection | Seed 0 distance / days | Seed 42 distance / days |
| --- | --- | --- |
| Eastport–Northport | 309 / 4 | 470 / 5 |
| Eastport–Southhaven | 432 / 5 | 324 / 4 |
| Eastport–Westhaven | 576 / 6 | 565 / 6 |
| Northport–Southhaven | 572 / 6 | 641 / 7 |
| Northport–Westhaven | 524 / 6 | 303 / 4 |
| Southhaven–Westhaven | 333 / 4 | 567 / 6 |

Classic instead retains Northport `(340,120)`, Southhaven `(570,550)`, Eastport `(850,250)`, Westhaven `(120,400)` and its separately authored distances 200/300/400/600. The new coordinate-derived rule is intentionally not applied to Classic. Existing deterministic report bytes remain regression fixtures.

## Ascension 0 and 1

**Ascension 0: Normal** preserves instant private construction. **Ascension 1: Slow Foundations** delays newly purchased player facilities by two complete economic days. Both are freely selectable per run; there are no unlocks, stacking curses or permanent bonuses.

Construction validates eligibility, uniqueness, affordability and checked dates before committing. Ordering transfers the full cost to the local market once, creates the owned site, increments revision and spends no day. Pending sites remain visible and cannot automatically procure, produce or sell. Ordinary manual local purchases and docked warehouse transfers are allowed: ownership, physical location, stock, cash and capacity checks still apply. Storing inputs never starts early production.

Completion occurs after voyage advancement and before challenge evaluation in the same atomic daily commit. Sites completing that day have already skipped all private economic phases, so there is no completion-day procurement, production or sale. Completion charges nothing and grants no goods. The normal production interval starts at completion, and input shortages can delay actual production beyond the first eligible day.

| Day / action | Ascension 0 bakery | Ascension 1 bakery |
| --- | --- | --- |
| Day 0: order for 300 | Pay 300; operational; first eligible day 2 | Pay 300; under construction; completion day 2; first eligible day 4 |
| First AdvanceDay, day 1 | Operational, not yet due | Under construction, one day remaining; no automatic trades or production |
| Second AdvanceDay, day 2 | May produce if funded inputs and space permit | Completes at the final construction phase; no batch or automatic trade |
| Day 3 | Ordinary scheduling | Operational procurement may run; production not yet due |
| Day 4 | Ordinary scheduling | First production opportunity; inputs and capacity are still required |

Disabled, input-starved and output-limited facilities retain the existing due-date behavior after becoming operational. Any late exceptional failure rolls back construction completion together with earlier trades, accounting, production, arrivals, challenge progress and day/revision. A terminal challenge prevents further construction orders or day advancement, including pending completions.

The **Industrial Consortium's granted bakery is already operational**. Its starter inventory and first eligible day 2 remain unchanged at either ascension. This is a deliberate, visible asymmetry: a merchant or independent company purchasing a bakery loses two days of construction availability; an inherited bakery does not. Do not infer that Ascension 1 makes every strategy harder.

## Readability and industry controls

Settings now exposes **Game-wide text size**. Compact body/caption tokens are 14/12 px, Default 17/15, Large 21/19, and Extra Large 25/23; headings, numeric fields, buttons, tables, map labels and hit targets receive corresponding layout sizes. Scale changes use layout styles rather than a paint-only transform. Long forms and overlays use internal scrolling. The text preference persists locally through Unity PlayerPrefs across menu transitions, new runs and standalone launches; it does not save the economic world. Other presentation settings remain application-session preferences.

Industry inspection distinguishes construction, production cadence, shortages, purchasing/sales limits and warehouse capacity. The recommended procurement preset is a visible draft until explicitly applied. A shipping setup still needs the player's owned ship and real matching warehouses; local procurement is a public-market purchase and loading private goods is a physical transfer. Detailed controls remain available. Actual observed screen coverage, keyboard/pointer checks and screenshots belong to [Unity verification](UNITY.md) and [Playtest](PLAYTEST.md); headless economic success does not certify every UI layout.

## Reproduce the experiment

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --seeded-runs
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --seeded-runs --check
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --seeded-runs --deep .cache/task18-seeded-deep.json
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --seeded-runs --deep --check .cache/task18-seeded-deep.json
```

Normal CI uses these 25 stable seeds:

```text
-2147483648, -2147483647, -1000000007, -104729, -12345,
-1024, -42, -7, -1, 0, 1, 2, 3, 7, 17, 42, 99,
12345, 65535, 65536, 104729, 123456789, 1000000007,
2147483646, 2147483647
```

Each seed and the Classic control run all three challenges, three charters, two ascensions and four strategies: **1,872 finite trials**. Deep mode adds the first 100 signed values of `0x9E3779B9 * i` for `i = 1..100`, using wrapped uint arithmetic, deduplicates and sorts. Its 125 seeds plus Classic produce **9,072 trials**. Deep output is optional and kept outside the committed normal report.

Every strategy uses actual public commands and the shared world. Active strategies sell their starting cargo at their real starting dock. Their remaining rules are explicit:

| Strategy | Prescribed actions and limits |
| --- | --- |
| Idle | No player commands or policies; ordinary public/NPC economy and granted production continue |
| Merchant | Sail to Northport if necessary; enable the existing Northport–Eastport Bread route: maximum 10, minimum 4, buy ceiling 32, sell floor 24, per-order expenditure cap 280, company reserve 100, empty return allowed |
| Local factory | Use the granted Westhaven bakery or pay 300 for one; enable local procurement and local sales; do not transport Bread |
| Industrial | Use the same granted/purchased bakery and procurement, keep local output sales disabled, sail to Westhaven, then apply the existing private Bread shipping policy to Eastport |

Bakery procurement targets three Flour at a maximum marginal ask of 24 and three Logs at 18, retaining company cash reserve 100. Local sales retain zero Bread and require marginal bid at least 20. The industrial route loads up to eight Bread when at least eight are held, requires a minimum shipment of four, retains no warehouse output reserve, sells at bids of at least 24 and allows an empty return. The route cash reserve is 100. These settings are shared scenario presets; the runner does not discover strategies or alter them after seeing a seed.

The report records layouts once, joined to each run by mode/seed/version; per-run identity, outcome, day, cash, score and progress; actual capital and completion dates; paid inventory, sales, released and held FIFO basis; private production, NPC activity, physical shipments, blocked decisions, factory waits and accurate setup-command/policy counts. Per-good summaries separately record initial/final stock, extraction, gross transformation inputs/outputs and consumption. There are no retained per-day snapshots or thousands of journal entries.

Every operation checks combined cash, each company account, committed financial events, nonnegative goods/cash, capacity, physical stock changes and FIFO basis. Departures must use the authoritative distance/duration. Each finished run rejects further advancement and recreates the same initial world. Classic plus seeds `int.MinValue`, `int.MaxValue`, `-42` and `99` repeat every strategy/charter/challenge/ascension combination with reversed configuration order and `tr-TR`; serialized reports must match. The full normal report additionally has read-only freshness checking.

## Measured difficulty and efficiency

The committed 25-seed sample provides a winning prescribed strategy for **all 450 seeded challenge/charter/ascension combinations**. This establishes observed feasibility within that sample; the geometric proof guarantees navigability for every seed, not universal victory for every strategy or seed. There was no economic tuning in this milestone.

The optional **125-seed deep batch was also executed successfully**: 9,072 trials including Classic, with a benchmark win for all 2,250 seeded challenge/charter/ascension combinations. It produced the same strategy success/failure pattern and the same completion-day ranges as the normal sample. Among its 375 Ascension 1 industrial Regional Relief trials, **130 finished exactly on day 60**, compared with none at Ascension 0. The deep artifact remains ignored under `.cache/task18-seeded-deep.json`; it is reproducible with the command above, not part of the normal CI artifact.

Each table entry covers 75 seeded runs: 25 seeds times three charters, for the named ascension and strategy.

| Challenge / strategy | Ascension 0 wins; successful day range | Ascension 1 wins; successful day range |
| --- | --- | --- |
| Coastal Supply / merchant | 75/75; 28–50 | 75/75; 28–50 |
| Coastal Supply / industrial | 75/75; 28–44 | 75/75; 28–46 |
| Regional Relief / merchant | 75/75; 44–56 | 75/75; 44–56 |
| Regional Relief / industrial | 75/75; 52–56 | 75/75; 52–60 |
| Enterprise Ambition / local factory | 75/75; 12 | 75/75; 12–14 |
| Enterprise Ambition / industrial | 75/75; 12–18 | 75/75; 12–20 |

Idle loses every trial. Local factories lose Coastal Supply because Westhaven local sales do not supply Eastport. They also lose Regional Relief: Westhaven's demand can consume at most 20 Bread by day 60, with only two initially consumed elsewhere in these scripts, leaving **22/24** despite positive manufacturing revenue and material margin. Shipping fixes that physical distribution problem. Merchant-only strategies lose Enterprise Ambition even with enough margin because they do not produce the required private Bread.

Geography reverses which active strategy completes Coastal Supply faster for the **same Merchant charter**:

| Seed | Ascension | Merchant day / cash / margin | Industrial day / cash / margin |
| --- | ---: | --- | --- |
| 99 | 0 | 28 / 984 / 384 | 39 / 861 / 769 |
| -42 | 0 | 43 / 1,118 / 518 | 28 / 926 / 697 |
| 99 | 1 | 28 / 984 / 384 | 41 / 875 / 776 |
| -42 | 1 | 43 / 1,118 / 518 | 30 / 961 / 708 |

The industrial branch pays 300 capital and retains real material basis, while the merchant branch buys no facility. Higher material margin therefore does not imply more available cash. The two-day construction shift can also change later market timing and sale proceeds; Ascension 1 is not a blanket reduction in every financial metric. At seed -42, its industrial branch completes two days later but has slightly higher final cash and margin. These are distinct world/difficulty comparison groups, not universal rankings.

For that seed -42 industrial example, Ascension 0 buys 382 of materials and sells for 1,008, releasing 311 of basis while retaining 71 in four Bread and two Logs. Its cash is `600 - 300 - 382 + 1008 = 926`; its realized material margin is `1008 - 311 = 697`. Ascension 1 buys 347, releases 300 and retains 47 in two Bread and three Logs, yielding cash 961 and margin 708. The report observes local input-stock shortages on seven versus nine daily snapshots and source-loading waits on six versus eight decisions. These are real finite-stock/reserve/timing effects; there is no requirement to inflate recipe yield to explain them.

The current default Bread route remains very forgiving: every sampled merchant Coastal/Relief trial succeeds after setup, and Ascension 1 does not affect a company that constructs nothing. The Industrial Consortium also bypasses the delay when using only its starter bakery. Some industrial Relief runs reach victory exactly on day 60, making construction timing relevant, but the milestone does not establish broad roguelite difficulty. Next calibration should test one disclosed pressure at a time, preserving the Classic benchmark, and observe whether players can diagnose route duration, private-stock reserves and local-demand limits. No deadline, recipe, price or charter change is justified as settled balance by these measurements alone.

This historical v1 report's material margin excludes wages, voyage operating costs, depreciation and asset liquidation value. Living Cities records actual wages separately and retains the material-only FIFO contract. Held stock is not realized profit. NPC competition, local stock/prices, finite cash, reserve rules and voyage timing are recorded consequences; the scripts cannot establish human attention cost or enjoyable play. Platform-specific UI/player verification remains separately documented.
