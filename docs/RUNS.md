# Runs, company charters, and results

Choose one handcrafted challenge and one starting company charter, then build a strategy using the same four-town economy. A run ends when its objective and cash requirement are met, its deadline expires, or you deliberately abandon it. Sandbox uses the same economic rules without a scored objective or deadline.

The menu, setup, pause, reference, and results screens form the current Unity host's run flow. [Playtest](PLAYTEST.md) documents controls; [Unity integration](UNITY.md) distinguishes automated, actual Editor, and standalone-player verification. Runs exist only in application memory. There is no persistent save, unlock history, or recovery after closing the application.

## Navigate a run

**New Run** and **Sandbox** both open setup, with the corresponding challenge or unlimited option selected. Review the challenge, charter, initial assets, tradeoffs, objective, and deadline before starting. The title also offers **Encyclopedia**, **Settings**, and **Exit Game**; it has no misleading Continue or save option.

During gameplay, Escape opens the pause menu without advancing a day. **Resume** restores the playback state from before the pause. The menu also opens the encyclopedia, company ledger, and settings, or offers **Restart this run**, **Return to Main Menu**, and **Exit Game**. An active challenge additionally offers **Abandon and inspect results**.

Restarting, returning to the main menu, or exiting confirms loss of an active run. Explicit abandonment creates a terminal abandoned result. Confirmed return to the main menu abandons an active challenge and discards the host's in-memory session; starting again creates a new world. Results offer **Retry**, **New Run**, **Main Menu**, and **Encyclopedia**.

Escape first cancels a confirmation, or closes the open trade/industry modal, without also triggering a lower screen. In the encyclopedia it follows entry history and then returns to the parent screen; other secondary screens return to their parent. On the pause menu, Escape resumes. This ordering supports both keyboard events and the Input System navigation-cancel path. Actual Editor and standalone behavior must be checked separately as recorded in [Unity integration](UNITY.md).

## Select a starting company

Select exactly one charter before starting. It replaces the other packages; nothing stacks and there is no income or production multiplier. An active run cannot switch its charter. Retry initializes the same selected challenge and charter from scratch, including its original assets and provenance.

| Charter | Liquid cash | Ship and initial port | Granted goods/facilities | Opportunity and tradeoff |
| --- | ---: | --- | --- | --- |
| Merchant Company | 600 | 10-unit hold, Northport | None | Most immediate spending flexibility; a factory competes with shipment capital |
| Industrial Consortium | 300 | 8-unit hold, Westhaven | One private bakery, with 2 Flour and 2 Logs in its warehouse | Immediate production infrastructure and local inputs; less cash and a smaller hold |
| Independent Traders | 480 | 12-unit hold, Northport | 4 Grain and 2 Logs aboard | Larger capacity and flexible cargo; lower liquidity and half the hold occupied initially |

All three ships have speed **100** and use the same integer geographic routes. The granted bakery uses the ordinary two-day schedule, 40-unit storage, and 30-unit output ceiling. It starts enabled, with procurement and automatic sales disabled. Its initial inputs can produce two batches without purchases, after which more inputs must be acquired physically. Starting cash and assets are explicit scenario initialization, not fake in-run purchases or construction payments.

### Compare endowments rather than cash alone

Initial local exact purchase previews provide a limited replacement-cost estimate for granted goods:

| Charter | Starting cash | Facility construction quote | Goods replacement quote | Comparable quoted components, excluding the ship |
| --- | ---: | ---: | ---: | ---: |
| Merchant Company | 600 | 0 | 0 | 600 |
| Industrial Consortium | 300 | 300 | 45: 2 Flour cost 29; 2 Logs cost 16 | 645 |
| Independent Traders | 480 | 0 | 43: 4 Grain cost 23; 2 Logs cost 20 | 523 |

These starting-snapshot previews are executable quote estimates, not money the company paid, historical cost basis, or guaranteed resale value. Ship buying/resale prices are not modeled, so the 8-, 10-, and 12-unit holds remain explicit unvalued capabilities. The larger hold also does not increase a route's configured shipment target automatically.

The Industrial Consortium has more installed capacity but less liquid cash and a smaller ship. Independent Traders trades liquidity for capacity and initially occupied cargo. These are provisional lateral choices, not equally valued competitive builds. The headless trials establish feasibility; observed play remains necessary to assess whether the tradeoffs are clear and useful.

## Three handcrafted challenges

Each challenge uses the current map and has a **day-60 deadline** and a **100-cash minimum** at successful completion. Cash below 100 does not cause an immediate loss: the company can recover before the deadline. There is no automatic grant, restock, or subsidy.

| Challenge | Required objective | What actually counts |
| --- | --- | --- |
| Coastal Supply Charter | Supply 24 net Bread to Eastport | Player-company completed Bread sales to Eastport minus its purchases there, through either ship or warehouse |
| Regional Relief | Have 24 Bread consumed by scheduled regional demand | Actual completed Bread consumption across all towns, including public/NPC-supported supply |
| Enterprise Ambition | Produce 12 Bread privately and earn 200 realized gross margin | The company's actual private Bread output plus its sales revenue less released historical inventory basis |

Coastal Supply retains a signed delivery counter. Buying Eastport Bread and selling it back adds no net credit. Loading, unloading, voyages, public production, and NPC sales do not count as player delivery. The displayed delivery quantity clamps negative progress to zero; the underlying debt remains.

Regional Relief measures **regional performance**, not personal attribution of every consumed unit. Arriving cargo and market sales alone grant no consumption progress. A scheduled demand event must actually remove stock. A partially stocked demand request consumes nothing under the existing all-or-nothing consumption rule.

Enterprise Ambition counts the owner's private production, not public factories or simply holding purchased Bread. Its margin covers all company sales and includes the actual zero paid basis of granted goods; starter proceeds are not disguised as paid manufacturing costs. The production requirement prevents merely selling initial grants from meeting the whole objective. Construction spending is reported separately from material gross margin, and unmodeled wages, operating costs, and depreciation remain excluded.

### Completion, failure, and abandonment

The simulation evaluates the objective after the entire authoritative operation, using its final cash and day. A successful instant command can finish a run without advancing time. On the deadline's final daily advance, ship actions, private procurement, production, sales, consumption, and voyages finish before the success/failure decision. An exceptional failure rolls back the entire operation, including any provisional progress.

If all requirements are satisfied at or before the deadline, the state becomes `Succeeded`. Otherwise a committed day at the deadline becomes `Failed`. There is no extra manual action window after the day-60 failure. A deliberate abandonment produces `Abandoned` at the current day while retaining the actual financial and physical state. It grants no victory or time bonus.

All three terminal states reject further economic mutations and day advancement. Inspection and the encyclopedia remain available. Retry creates a new selected starting state; starting another run chooses a new configuration. Sandbox has no challenge terminal condition or score.

## Read the result

The completed result identifies the challenge, starting charter, outcome, completion day/deadline, objective progress, and cash. Financial details distinguish:

- Inventory purchases and actual sale revenue.
- Historical cost released by completed sales and realized material gross margin.
- Construction expenditure.
- Remaining goods and their paid historical basis.
- Granted and constructed facilities, actual private output, and net target-market supply.
- Host-observed commands and automation usage when reported.

Transferred inventory keeps its cost basis; it is not a sale or expense. Manufacturing allocates consumed input basis to actual outputs. Remaining basis is an acquisition measurement, not a claim about what finite markets would pay today. Granted inventory has explicit provenance and zero company purchase expenditure.

The simulation retains authoritative aggregates and live inventory lots, not an unlimited history in each snapshot. Per-operation economic events feed the host's bounded ledger and complete headless experiment journals. A UI history may show only recent events even though cumulative financial totals cover the whole run.

### Exact scoring formula

`RunResults` calculates four integer components. Division floors toward zero after nonnegative clamping:

```text
progress_fraction(value, target) = min(500, floor(max(0, value) * 500 / target))

Objective:
  Coastal Supply  = progress_fraction(net delivered Bread, 24)
  Regional Relief = progress_fraction(regional Bread consumed, 24)
  Enterprise      = min(progress_fraction(private Bread produced, 12),
                        progress_fraction(realized gross margin, 200))

Victory = 1000 if Succeeded, otherwise 0
Time    = 10 * min(60, max(0, deadline - completion day)) if Succeeded, otherwise 0
Solvency = min(100, floor(company cash / 10))
Total = Objective + Victory + Time + Solvency
```

Failed and abandoned runs can retain partial objective and solvency points; those points do not turn them into victories. Every component is capped. Overproduction, additional sale volume, or repeated buy/sell loops cannot create unbounded points. Exact same-market pricing and signed Coastal Supply credit separately preserve the economic protections.

Results belong to a **challenge/charter comparison group**. They are not a universal leaderboard: starting grants, facilities, ship capacity, and strategies differ. Current scoring does not normalize an unmodeled ship valuation or claim perfect package balance.

For example, the Merchant Company completes Coastal Supply on day 29 with 1,014 cash: **500 objective + 1,000 victory + 310 time + 100 solvency = 1,910**. The Industrial Consortium completes the same challenge on day 39 with 1,027 cash: **500 + 1,000 + 210 + 100 = 1,810**. The second result uses a different endowment and comparison group.

## Reproduce complete runs

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --runs
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --runs --check
```

The [generated report](experiments/run-results.json) contains one active strategy and one idle trial for each of the nine challenge/charter combinations. Each active strategy succeeds; all nine idle trials fail on day 60. Every committed operation reconciles event cash, purchases, revenue, released basis, construction, and held basis. Repeated runs and `tr-TR` culture match. Recreating the selected package restores the complete initial state, and terminal worlds reject another daily advance.

| Challenge | Charter and strategy | Completion day | Final cash | Realized material gross margin | Score |
| --- | --- | ---: | ---: | ---: | ---: |
| Coastal Supply | Merchant Company, merchant | 29 | 1,014 | 414 | 1,910 |
| Coastal Supply | Industrial Consortium, industrial shipping | 39 | 1,027 | 814 | 1,810 |
| Coastal Supply | Independent Traders, merchant | 29 | 918 | 438 | 1,901 |
| Regional Relief | Merchant Company, merchant | 48 | 1,114 | 514 | 1,720 |
| Regional Relief | Industrial Consortium, industrial shipping | 56 | 1,210 | 1,022 | 1,640 |
| Regional Relief | Independent Traders, merchant | 48 | 1,018 | 538 | 1,720 |
| Enterprise Ambition | Merchant Company, constructed bakery | 15 | 475 | 286 | 1,997 |
| Enterprise Ambition | Industrial Consortium, granted bakery | 15 | 515 | 337 | 2,001 |
| Enterprise Ambition | Independent Traders, constructed bakery | 21 | 423 | 310 | 1,932 |

The merchant script sells any starting cargo at its actual initial market, then enables the existing Northport–Eastport Bread policy. The industrial script uses the granted bakery or pays 300 to construct one in Westhaven, enables the shared local procurement policy, sails there if necessary, and applies the private Bread shipping preset. That preset targets eight Bread, waits for eight private units, retains no output reserve, requires a four-unit minimum, sells at marginal bids of at least 24, and permits an empty return. Daily automation performs physical transfers and voyages; no repeated manual loading is scripted.

These scripts are feasible examples, not optimal strategies. Independent Traders' initial grants sell for 24 in these trials, influencing its cash and reported margin. Industrial Consortium has no in-run construction payment, unlike the two constructed-bakery cases. Its starter inputs carry grant provenance and no paid basis. Those differences explain part of the results and preclude ranking the rows as equally endowed competitors.

The separate [industrial logistics comparison](AUTOMATION.md#reproducible-industrial-shipping-comparison) measures a six-unit shipment/reserve policy, manual equivalence, blocked conditions, changed demand, and a 90-day horizon. Its parameters and results differ intentionally from the eight-unit complete-run strategy. The [Task 1.6 enterprise report](ENTERPRISE.md#reproduce-the-experiment) preserves the earlier manual industrial strategy and remains a regression fixture.

## Current limits and next questions

There are no saves, persistent unlocks, ascensions, curses, procedural maps, online rankings, or automatic strategy search. All charters are available immediately. Finite liquidity and unfavorable prices can still block an enterprise. Changing a policy is a strategic choice, not a promise that any configuration can recover.

The next design step is observed startup-to-results playtesting: can players understand the charter tradeoffs, configure a complete supply chain, distinguish sales from consumption, and explain their final cash and margin? Numerical feasibility and scoring reproducibility are necessary evidence, not a substitute for that observation.
