# The living archipelago

Task 1.5 established the four-town maritime sandbox. Task 1.6 extends the same `Assets/Scenes/ArchipelagoLab.unity` with the shared `EnterpriseScenario`: six goods, three NPC merchants, private factories and finite stockpiles, production policies, a contextual trading overlay, and an optional supply charter. Read [Commerce and industry](ENTERPRISE.md) for the current playable configuration and [Playtest](PLAYTEST.md) for current controls.

The new `--enterprise` report compares ninety sandbox days and separate charter trials. It reconciles private and public stock with extraction, transformation, and consumption, and counts company cash once even when that company owns several assets. The measured merchant charter succeeds on day 29; a Westhaven bakery with physical shipping succeeds on day 46. Initial combined cash is 25,800. These are provisional values and feasibility evidence, not final balance.

**The remaining sections preserve the historical Task 1.5 fixture and its measurements.** `--archipelago` and `--archipelago --check` still use `ArchipelagoScenario`, two goods, two NPCs, and the original player account/capacity. `LegacyContent.Timber` projects the canonical definition objects without copying economic definitions. The sixty-day report remains byte-for-byte reproducible; it does not describe the current ArchipelagoLab starting economy.

## One shared scenario

`src/SaltAndLedger.Simulation/ArchipelagoScenario.cs` remains the authoritative historical runtime configuration for the `--archipelago` experiment. It was also the Task 1.5 Unity configuration. The current host uses `EnterpriseScenario.cs`; both reference canonical goods, buildings, recipes, names, and descriptions from `data/content.json`. Neither scenario defines a second economic engine.

| Town | Map position | Initial logs / planks | Initial market cash | Local production | Physical consumption |
| --- | --- | ---: | ---: | --- | --- |
| Northport | (340, 120) | 14 / 0 | 2,500 | Lumber Camp: 2 logs every 3 days; output ceiling 18 | 1 plank every 5 days |
| Southhaven | (570, 550) | 4 / 5 | 4,000 | Sawmill: 2 logs → 1 plank every 2 days; plank ceiling 12 | None |
| Eastport | (850, 250) | 0 / 2 | 4,000 | None | 1 plank every 4 days |
| Westhaven | (120, 400) | 12 / 1 | 2,200 | Lumber Camp: 2 logs every 4 days; output ceiling 16 | 1 plank every 6 days |

Map coordinates use an authored 1,000 × 700 plane. They position the drawing; simulation travel uses the separate integer sea distances below. All facilities operate on their town's market stock. They have no private warehouse, labor account, or operating cost.

All eight market quotes use the existing linear inventory model with minimum price 1 and spread 1:

| Market / good | Reference price | Target stock | Sensitivity | Maximum price |
| --- | ---: | ---: | ---: | ---: |
| Northport logs | 10 | 12 | 6 | 40 |
| Northport planks | 28 | 5 | 12 | 60 |
| Southhaven logs | 20 | 8 | 12 | 60 |
| Southhaven planks | 14 | 6 | 8 | 50 |
| Eastport logs | 16 | 6 | 6 | 50 |
| Eastport planks | 30 | 5 | 10 | 60 |
| Westhaven logs | 9 | 10 | 5 | 40 |
| Westhaven planks | 26 | 4 | 12 | 60 |

Targets are pricing scales, not stock guarantees. Every bulk quote uses exact inventory-edge totals. Remote prices are current information, not a promise about arrival prices or market funds.

## Days, instant actions, and revisions

The regional world starts at day 0, revision 0. One economic tick means one in-game day **only in this new sandbox**.

Buying, selling, departure, production enablement, route application, and automation enablement are instantaneous commands. A successful state change increments a checked revision once without advancing the day. A rejected trade or unchanged enablement keeps the exact previous snapshot and revision. Invalid commands and exceptional failures preserve the entire prior world.

Several trades can therefore commit during one day, immediately changing cargo, cash, stock, quotes, and previews. Each automation decision refers to its exact source snapshot. Another same-day trade or policy change makes the earlier decision stale even though its day number still matches.

`AdvanceDay()` is one atomic transaction:

1. Run enabled ship policies in ascending actor priority, then ordinal ship ID. Each actor receives at most four same-day actions and sees earlier actors' staged changes.
2. Run due production by ordinal port ID, then facility priority and ID.
3. Run due consumption by ordinal port and rule ID.
4. Progress every voyage once and resolve arrivals.
5. Commit day + 1 and revision + 1 together with all accounts, schedules, controls, cargo, and ship states.

Any exception rolls back the complete day, including an earlier merchant purchase, production batch, consumption result, or voyage change. No actor has an independent world clock. Goods produced during this day can satisfy this day's consumption; they become available for subsequent dockside actions after the daily commit. Automation's trade phase precedes production, so it cannot buy that output earlier in the same daily transaction.

## Production, shortages, and consumption

The additive version-1 content extension introduces optional recipe `kind`. Missing kind means `transformation`, which still requires nonempty inputs. `extraction` requires empty inputs and nonempty outputs. The canonical `harvest_logs` recipe belongs to `lumber_camp` and creates 2 logs; `saw_planks` remains the original 2-logs-to-1-plank transformation. No imaginary input commodity was added.

The portable production command remains the one batch calculator. Scheduling and output ceilings belong to each regional facility configuration, not to canonical recipe definitions:

- The first opportunity is day `intervalDays`, unless explicitly configured otherwise.
- A successful batch schedules its next opportunity at completion day + interval.
- Disabled production, missing inputs, or a whole batch exceeding an output ceiling leave the facility due. It retries on a later day and never accumulates catch-up batches.
- A toggle preserves the deadline. It cannot create an instant batch or reset a shortage into free output.
- A ceiling checks each output's resulting local stock. A blocked batch creates nothing and consumes nothing; it is never silently truncated.

Consumption is an all-or-nothing physical sink. If the complete requirement is available, remove it. Otherwise remove nothing and record the whole request as unmet. Every attempted consumption opportunity schedules the next at the current completion day + interval, including unmet demand; missed demand does not accumulate as a future inventory debt. The cumulative unmet counter is an observation.

There are **no household payments, wages, subsidies, cash replenishment, or monetary sources/sinks**. The model represents physical use of goods, not consumer finances. Extraction, transformation, consumption, trading, and transport are recorded separately so accounting does not incorrectly assert that logs or planks are globally conserved across legitimate sources and sinks. Cash remains conserved across all configured operations.

## Geography and voyages

Each route is an undirected direct sea connection. Duration is `max(1, ceiling(distance / shipSpeed))`, calculated with integers. All three starting ships have speed 100 distance units per day.

| Connection | Distance | Voyage days at speed 100 |
| --- | ---: | ---: |
| Northport ↔ Westhaven | 200 | 2 |
| Northport ↔ Southhaven | 300 | 3 |
| Northport ↔ Eastport | 400 | 4 |
| Southhaven ↔ Eastport | 300 | 3 |
| Southhaven ↔ Westhaven | 400 | 4 |
| Eastport ↔ Westhaven | 600 | 6 |

A three-day departure issued on day 0 enters transit with three days remaining. Day 1 leaves two, day 2 leaves one, and day 3 commits arrival. Manual trading is available immediately after that arrival, still on day 3. An automated departure in the day-0 actor phase likewise receives its first travel advance in the day-1 commit. An arriving enabled policy acts at the next daily actor phase, or through an explicitly requested bounded dockside run.

There is no route search, teleportation, sailing cost, weather, or collision model. Cargo always remains on its actual ship while underway. The map derives positions and occupancy counts from committed ship states. Smooth interpolation only draws the transition between those positions; it cannot authorize early trade or arrival.

## Ownership and merchant policies

| Ship | Owner account | Initial dock | Cash | Capacity | Daily priority |
| --- | --- | --- | ---: | ---: | ---: |
| Timber Merchant | `timber_company` | Northport | 300 | 5 | 0 |
| Plank Merchant | `plank_company` | Southhaven | 400 | 3 | 1 |
| Ledger | `player` | Northport | 200 | 8 | 10 |

Each owner has one cash balance and one ship's cargo, with no separate trader warehouse. Initial combined cash is **13,600**. NPCs receive no special prices, remote access, capacity exemption, or recovery funding.

The Timber Merchant waits for at least 10 Northport logs before loading. It targets 5 logs, requires at least 3, accepts marginal asks up to 14, spends at most 70 on a purchase, sails to Southhaven, and sells at marginal bids of at least 10. It returns empty after unloading.

The Plank Merchant waits for at least 5 Southhaven planks. It targets 3, requires at least 2, accepts marginal asks up to 24, spends at most 72, sails to Eastport, and sells at marginal bids of at least 20. It also returns empty. Both NPC reserves are zero. Stock thresholds apply before loading; adequate already-carried cargo can depart without a fresh threshold check.

Policies select the largest feasible quantity using the existing exact bounded search and preview. Partial sales retain cargo; further loading/departure waits until unloading is complete. Each failed condition produces a readable reason and no impossible transaction. After a successful sale, a return purchase or permitted empty departure can occur in the same bounded zero-time chain. A wait, pause, voyage, no-progress result, or four-action limit ends that actor's chain.

NPC priorities precede the enabled player policy at daily boundaries. Instant player orders are separately committed before the next requested day; selecting a town never changes that precedence. This timing distinction is explicit player agency, not simultaneous execution or a hidden NPC advantage.

## Historical Task 1.5 player choices

The map offers current information about all four towns and three ships. Select ports for stocks, quotes, liquidity, industries, demand, and recent deliveries. Select a ship marker or the **Inspect ship…** fleet list for its account, cargo, owner, voyage, and policy. The list keeps ships individually selectable when sailing markers coincide. NPC inspection grants no control.

Manual trading and sailing require player automation to be disabled. Opening dockside trade or issuing a manual order pauses playback. With Ledger selected, click another port, inspect its distance/duration, then confirm departure. Merely selecting a destination does not move the ship.

Both route directions are editable: ports, commodity, target/minimum, worst marginal buy/sell price, purchase expenditure cap, reserve, and empty return. Return commodity may be None when empty return is enabled. Distinct outbound/return goods remain required so policy stage can be inferred from physical cargo. An expenditure cap limits one purchase order, not lifetime or voyage spending; existing source cargo counts toward the shipment target.

The default player route is Northport → Southhaven, logs target 6/minimum 2/buy ≤14/sell ≥14/spend ≤100; return planks target 3/minimum 1/buy ≤24/sell ≥18/spend ≤72. Reserve is 20 and empty return is allowed. These are editable hypotheses, not an optimal strategy.

The [playtest guide](PLAYTEST.md) distinguishes the current enterprise controls from retained historical verification captures. Task 1.6 replaces the old trade foldout with a centered overlay and changes the default player route to Northport–Eastport bread. The existing TradingLab remains available for its historical six-tick comparisons.

## Reproducible sixty-day experiments

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --archipelago
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --archipelago --check
```

The first command writes [archipelago-results.json](experiments/archipelago-results.json); `--check` recomputes without modifying it. An optional final path selects a different report. The runner compares repeated runs, reversed port/route/ship configuration, and `tr-TR` culture. CI runs freshness verification. The report contains initial configurations, each day's stocks/prices/accounts/schedules/ship states, every structured operation, merchant waits, source/sink quantities, and strategy summaries. It contains no timestamps or machine paths.

All five strategies start with the same scenario and receive exactly 60 daily advances:

- **Idle:** player issues no orders; both NPCs run normally.
- **Manual West:** depart for Westhaven, repeatedly buy six logs when executable at worst ask ≤14 while retaining cash 20, carry them to Southhaven, sell the full load if payable, and return empty. This strategy accepts the actual sale price without a floor.
- **Restrictive:** enable the default Northport route with buy ceiling 9 and outbound expenditure cap 36.
- **Aggressive:** enable the default player policy unchanged.
- **Switch to West:** start Aggressive; at the first Northport dock on/after day 20, pause automation, sell any return planks normally, and reposition to Westhaven. Apply the equivalent Westhaven–Southhaven route on arrival. This occurs on day 25, with application on day 27, in the recorded run.

| After 60 days | Idle | Manual West | Restrictive | Aggressive | Switch to West |
| --- | ---: | ---: | ---: | ---: | ---: |
| Player cash | 200 | 245 | 200 | 552 | 672 |
| Held logs / planks | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 3 | 3 / 0 |
| Realized profit across closed empty-cargo intervals | 0 | 45 | 0 | 412 | 485 |
| Open cargo interval cash change | 0 | 0 | 0 | -60 | -13 |
| Player units sold | 0 | 36 | 0 | 49 | 58 |
| Player cargo units completing a voyage | 0 | 36 | 0 | 49 | 61 |
| All actors' cargo units completing a voyage | 66 | 87 | 66 | 70 | 100 |
| Player sailing days | 0 | 50 | 0 | 41 | 54 |
| Submitted player inputs | 0 | 25 | 1 | 1 | 5 |
| Completed NPC shipments | 16 | 13 | 16 | 5 | 8 |
| Extracted logs | 44 | 66 | 44 | 44 | 62 |
| Logs transformed / planks made | 48 / 24 | 58 / 29 | 48 / 24 | 46 / 23 | 56 / 28 |
| Planks consumed / unmet | 16 / 21 | 16 / 21 | 16 / 21 | 19 / 18 | 27 / 10 |
| Completely inactive days | 1 | 0 | 1 | 0 | 0 |

A completed shipment is a sale that leaves cargo empty; partial sale orders are counted separately. Transported units count actual cargo on each committed arrival, including goods that remain unsold afterward. Player inputs count submitted orders and route/enablement changes, excluding clock advances. They measure prescribed execution effort, not player cognition or enjoyment. Realized profit closes only when purchased cargo has actually been sold; cash changes in the last two open positions are not labeled realized losses and their cargo is not hypothetically valued.

Every operation reconciles global goods changes with extraction/transformation deltas minus consumption, and checks cash conservation. In Idle, initial totals of 30 logs and 8 planks become `30 + 44 - 48 = 26` logs and `8 + 24 - 16 = 16` planks. Total cash remains 13,600 in every strategy.

### What the measurements show

The idle Timber Merchant completes nine shipments, buys/sells 45 logs for 544/1,136, and ends with cash 892. The Plank Merchant completes seven shipments, buys 24 planks for 404, sells 21 for 517, and ends with cash 513 and three planks aboard. Its eleven sale orders include partial unloading; a price-floor wait is not a failed accounting operation. Both repeatedly purchase, travel, unload, and return without player intervention.

Northport ends with 9 logs and no planks; Westhaven reaches its 16-log production ceiling and has no planks. Southhaven ends with 1 log and 5 planks. Eastport has 8 planks and fulfills all 15 scheduled units. The 21 unmet units are Northport's 12 and Westhaven's 9: towns not supplied by either NPC route, despite goods elsewhere. This is a physical distribution problem; it is not solved by inventing consumer income or globally moving inventories.

Player intervention changes those same markets. Aggressive trading reduces NPC shipments from 16 to 5 by competing for source logs and Southhaven planks. Switching to West uses otherwise constrained extraction and supplies more consumption, but finishes with unsold logs. Manual West moves more logs into the sawmill and earns only 45 across its closed trades: extra supply lowers destination bids and interacts with the Timber Merchant's sale floor. These outcomes expose tradeoffs rather than establish a universally best route.

Inactivity counts a day with no trade, successful production, consumed goods, or voyage event. It does not treat a failed demand opportunity or a changing day number as useful activity. A one-day idle period is not proof of deadlock. The baseline records temporary source-threshold and sale-price waits; it does not deplete all cash within sixty days. The longer regression checks finite accounts over 600 days without claiming permanent sustainability.

## Historical limitations and current follow-up

Consumption has no payment model, production has no costs, information is perfect, item cargo has no weight, and geography is handcrafted. Fixed NPC rules serve only selected towns and may retain stock because of a price floor even when another market needs it. Same-day manual action can precede the next scheduled merchant phase. All are visible prototype assumptions.

Long-horizon finite liquidity, source thresholds, price floors, and unserved demand need further calibration. Task 1.6 now supplies private construction and one fixed objective, while save/load, route search, procedural generation, weather, combat, and multiplayer remain outside scope. The current interface and ninety-day enterprise comparisons require observed human playtesting before claiming approachable decision making.

Recommended next milestone: observe players choose between trading and industrial investment, distinguish private stock from public markets, and diagnose demand, distribution, price constraints, and liquidity. Adjust demonstrated presentation problems and run longer-horizon industry/logistics experiments while keeping source/sink and no-subsidy accounting visible. Actual Editor, player, test, screenshot, and tooling evidence is recorded in [Unity integration](UNITY.md).
