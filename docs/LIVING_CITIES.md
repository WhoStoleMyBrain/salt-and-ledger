# Living Cities: finite people, production and money

Task 1.9 adds an explicit **Living Cities / generation v2** mode. Classic and seeded-v1 retain their historical economy, including unpaid all-or-nothing consumption and zero wages. The new mode keeps the same four towns, six canonical goods, exact price engine, companies, ships and atomic regional owner. It adds finite household accounts, paid retail consumption, production-linked wages, population, prosperity, town-specific capabilities and one city-recovery challenge.

These are implemented prototype rules, not a claim of balance. A town may hold valuable goods but lack money, or hold money but lack food. Some finite economies deteriorate or stop trading. No daily allowance, bailout, stock repair, adaptive deadline or hidden outside account prevents that outcome.

## Accounts and actual payments

Each town owns public stock and one **market cash** account. Its residents share a separate **household cash** account. Each player/NPC company owns one cash account, all its ships and its private facilities. Ship cash is a projection of company cash and must never be added again.

| Operation | Actual payer | Actual recipient | Physical effect |
| --- | --- | --- | --- |
| Company buys locally | Company | Town market | Public goods move to ship or private warehouse |
| Company sells locally | Town market | Company | Owned goods move into public stock |
| Public batch succeeds | Town market | Local households | Canonical production transforms or extracts public goods |
| Private batch succeeds | Owning company | Local households | Canonical production uses private inputs and retains private outputs |
| Household retail purchase | Local households | Town market | Only purchased Bread/Furniture is removed as consumption |
| Municipal Plank use | Nobody | Nobody | Actual public Planks are removed for municipal own-use |
| Construction | Company | Local market | An empty owned site is ordered; no free goods are granted |
| Ship/warehouse transfer | Nobody | Nobody | Goods and their original FIFO slices move between matching owned stores |

At initialization and every committed operation, `TotalCash` is the exact sum of all markets, all household accounts and all company accounts. It uses a wide aggregate, while each actual balance remains a checked nonnegative `long`. The owner checks the conserved total before committing. Explicit wage/retail records identify source account, destination account, port, day/revision and amount. Ordinary company transactions retain their existing economic events. An arithmetic error anywhere in a day rolls back every earlier transfer, batch, demographic observation, voyage, construction completion and challenge update.

For a small fixed-price example, begin with market cash 10, households 12 and company cash 20: combined cash **42**. A completed public batch pays 4, leaving market 6 and households 16. Residents then buy two Bread at ask 5, leaving market 16 and households 6. A company sale paid at bid 3 leaves market 13 and company 23. Combined cash is still 42. The retail purchase consumed two actual Bread; neither a wage nor a desired meal creates goods.

### Wages and honest company accounting

Only successful, due, enabled batches pay the configured aggregate wage. Input shortage, output limit, warehouse capacity, disabled state and unfinished construction do not pay. If the employer cannot fund the full wage, `PayrollShortage` prevents the whole batch and preserves all inputs/output. Successful production and payment share the same atomic operation. A blocked batch remains due; there are no catch-up batches or salaries for nonexistent work.

Private wages are **expensed when paid**, separately from material FIFO basis. Production still allocates only actual consumed input basis to output units. Thus:

```text
cash = initial cash - inventory purchases - construction - wages paid + sale revenue
material gross margin = sale revenue - released material FIFO basis
operating surplus after wages = material gross margin - all wages paid so far
```

`OperatingSurplusAfterWages` includes wages for batches whose outputs may still be held. It is a cumulative cash-cost measure, not a matched full-cost profit for the sold units. Wages are not silently capitalized into held inventory or counted twice. Freight, depreciation, overhead, taxation, financing and asset liquidation remain unmodeled. Historical runs have zero wages and retain their original material-margin reports.

## Food, durable goods and municipal needs

Living ports have exactly one essential Bread rule. Essential rules execute first, luxury rules next, and municipal own-use last, with ordinal rule IDs inside each kind and ordinal port order. Produced public goods can satisfy the same day's later consumption. Daily ship policies have already acted on the preceding committed state.

For household rules, desired quantity is `ceil(configured quantity × current population / population per unit)`. There is no fractional carry or hidden backlog. Generated towns desire **one Bread per 20 residents every four days**. A population of 20 requests one; 21 requests two. The integer threshold is deliberate and creates a sensitivity worth testing. Generated municipal demand uses a constant **one Plank every 12 days**, not a household purchase. Grain, Flour and Logs remain production inputs/trade goods without direct household consumption.

For a due retail need, first calculate desired quantity, then available stock, then the largest integer quantity affordable under the exact current marginal ask curve. The shared bounded quantity search and trade calculator perform the calculation. The resulting payment moves from households to the market, and precisely those purchased units are consumed. Full, partial and zero fulfillment are valid completed outcomes.

```text
available = min(desired, market stock)
unmet stock = desired - available
unmet affordability = available - purchased
```

For desired 4, stock 3, and funds sufficient for two units, consumption is 2, unmet stock is 1, and unmet affordability is 1. If marginal asks are 7 and 8, payment is **15**, not `2 × 7`. Zero household money leaves stocked food untouched. A receiving-market cash overflow is exceptional and rolls back the operation; it is not reported as household poverty.

Furniture is durable household demand: one per 80 residents every **24 days**, subject to eligibility below. A luxury purchase retains the exact hypothetical cost of the next essential food opportunity. That reserve uses current quotes and a hypothetical stock of `max(current food stock, next desired quantity)`; it reserves money, not actual stock or a future price. Municipal Planks may be partially consumed when fewer than the scheduled quantity exist and transfer no money. Every due rule advances its next deadline even after shortage; unmet need is an observation, not debt.

## Prosperity and slow population change

Each town retains at most **six essential-need observations**, plus small capped counters. An observation contains desired/paid-consumed quantities, stock and affordability shortfalls, payment, day and the demand corresponding to the initial population. Prosperity changes only after an essential opportunity, not every rendered frame or idle instant command.

Let `F` be the integer percentage of desired food actually consumed across the retained window. Let `P` be `min(100, floor(100 × remaining household cash / next food cost))`, using the hypothetical next-food quote described above. The target is `floor((3 × F + P) / 4)`. Prosperity moves toward it by `min(3, ceil(abs(target - current prosperity) / 8))`. It remains between 0 and 100. These weights and thresholds are transparent provisional choices; prosperity neither multiplies prices nor creates money.

An observation is healthy when its food is fully purchased and `P = 100`. Three consecutive healthy observations with prosperity at least **65** enable Furniture demand. Three consecutive observations with prosperity at most **40** or `P < 25` disable it. All towns initially have luxury disabled, even if their initial prosperity is high. This hysteresis requires sustained evidence; a single missed shipment does not flip the durable-demand state.

Generated towns review population every **12 days**, with bounds **10–80** and at most one resident gained/lost per review. Reviews require a full six-observation food history. Three consecutive healthy observations and prosperity at least 65 permit +1. Three consecutive observations consuming less than half the desired food, and prosperity at most 35, permit −1. Otherwise population stays unchanged. Growth/decline affects subsequent desired needs; no change grants or removes money. With four-day food opportunities, the first population change cannot occur before day 24.

The UI shows the latest demographic-review direction, observed shortfall causes, prosperity and actual household/market balances. `PurchasingPowerPercent` is the last essential-observation sample; an instant trade or later wage does not resample it. `PopulationDelta` likewise retains the last demographic-review change until the next review. Days of household supply is the floor of physical public stock divided by mean currently eligible household demand. It is an explanatory coverage figure, not a prediction of wages, imports, household solvency or future quotes. Non-household goods are marked not applicable for that metric.

## One daily owner and phase order

The regional owner stages: ship policies; private procurement; public production and wages; private production and wages; private sales; essential/luxury/municipal consumption; welfare/population observation and next-day pricing parameters; voyages; due construction completion; challenge evaluation. Then it publishes one immutable snapshot and revision. Daily wages and retail transfers cannot be triggered from Unity's presentation timer independently.

Ascension 0 still builds immediately. Ascension 1 newly purchased player sites pay once on day D, complete after voyages on D+2, skip automatic operation on that completion day and first become eligible after the full recipe interval. The Industrial Consortium's granted bakery starts operational at either ascension. Manual owned local trades and physical docked transfers keep their existing permissions. Missing capabilities remain server-side construction rejections, regardless of cash or UI selection.

## Economic generator version 2

`LivingCityGeneration.Generate(seed, version: 2, pricingMode)` preserves **exactly the v1 geography for that seed**. An independent uint state begins with the signed seed's bit pattern XOR `0xA511E9B3`. It uses the same defined Weyl increment/avalanche as [v1 geography](SEEDED_RUNS.md#generator-version-1). Three Fisher–Yates draws permute four role packages over ordinal town IDs; two draws per role select population/liquidity bands. There are exactly **11 economic draws**, separate from the 11 geography draws, and no rejection, rescue or strategy-evaluation loop.

| Role | Actual public industries | Population | Household starting cash | Prosperity | Market starting cash |
| --- | --- | --- | --- | ---: | --- |
| Food source | Farm, Lumber Camp, Bakery | 20/25/30 | Population × 30 | 65 | 1,200/1,300/1,400 |
| Processing | Mill, Sawmill | 20/25/30 | Population × 26 | 60 | 1,000/1,100/1,200 |
| Forest furniture | Lumber Camp, Furniture Workshop | 30/35/40 | Population × 42 | 75 | 1,400/1,500/1,600 |
| Vulnerable coast | Lumber Camp | 20/25/30 | Population × 22 | 40 | 300/350/400 |

Food-source public facilities run every two days; wages are Farm 12, Lumber Camp 10 and Bakery 10 per successful batch. Processing facilities run every two days and each pay 10. Forest-furniture logging runs every three days for wage 12; its workshop every five days for wage 16. Vulnerable-coast logging runs every three days for wage 12. Each has an explicit public output ceiling, so exhaustion of inputs, money or useful stock capacity can stop employment. Exact initial stocks, reference prices, limits and signatures are recorded in the generated report and source configuration rather than duplicated as canonical recipe facts here.

Private Sawmills are eligible only in the processing role. Private Bakeries are eligible in food-source, processing and vulnerable towns. Private Furniture Workshops are eligible in forest-furniture and vulnerable towns. Their respective wages are **6, 6 and 8** per completed batch; construction prices, intervals and capacities retain the existing blueprint values. Every generated world therefore has a Lumber Camp + Furniture Workshop town that cannot construct a Sawmill and must acquire actual Planks elsewhere.

The three existing NPC merchants are remapped by role: Logs travel food-source→processing, Planks processing→forest-furniture, and Grain/Flour run between food-source and processing. Policies retain ordinary exact prices, reserves, finite accounts and physical voyages; they do not guarantee supplies to every town. All six canonical production recipes exist somewhere, while no town is self-sufficient in both chains. Structural viability guarantees legal connected routes and potential supply paths, not household prosperity or a winning strategy.

| Seed | Food source | Processing | Forest furniture | Vulnerable target |
| --- | --- | --- | --- | --- |
| -42 | Northport | Southhaven | Westhaven | Eastport |
| 42 | Westhaven | Southhaven | Eastport | Northport |
| 99 | Southhaven | Northport | Eastport | Westhaven |

The complete generation signature includes geometry, roles, initial cash/population/prosperity, stocks, prices, schedules, needs, wages and private eligibility. Unsupported versions are rejected. Five full-signature golden hashes and eight role/population/liquidity golden vectors accompany a 258-seed structural sample. These tests guard implementation drift; the authored package matrix and bounded construction explain the all-seed structural guarantee.

## Identity, charter and recovery objective

A complete run identity includes challenge, charter, world mode, signed int32 seed, generator version, Ascension 0/1 and, for Living Cities, StockOnly/Coverage pricing. Retry reconstructs those exact choices and clears host state. Reroll changes a preview before explicit start. V1 identities never silently select v2 economics. The three charter cash/cargo/capacity/grant quantities stay unchanged; Merchant and Independent start at the generated food source, while Industrial's ship and operational granted bakery move together to the eligible processing town. Setup and results use this authoritative adapted charter.

**A City Back on Its Feet** targets the generated vulnerable coast and has a **day-90 deadline**. It requires all six latest Bread opportunities, at least **90% paid fulfillment**, prosperity at least **55**, population at least its initial value, and player company cash at least **100**. The denominator for each observation is the larger of current desired need and initial-population desired need. Shrinking the town cannot reduce the recovery target. Stock deliveries alone do not count: actual scheduled paid meals do, regardless of whether the player, NPCs or public production supplied them. This is explicitly a regional-welfare objective, not claimed personal attribution.

A terminal outcome uses the existing freeze, abandonment, retry and capped score framework. Objective points scale the observed sustained-window progress toward 90%; victory still requires every prosperity/population/cash/window condition. A final-day successful commit wins before the deadline failure check. Dumping cargo cannot finish immediately, although a sufficiently funded food stock can genuinely serve several later opportunities.

Hand-executed headless scripts using ordinary commands produced these stock-only Merchant-charter examples. The merchant enables the role-aware Bread route on day 0. The industry strategy pays for a processing-town bakery, applies the shared procurement policy, physically sails there, then enables warehouse-to-target shipping. No calibration was needed to obtain these examples.

| Seed | Merchant completion / cash | Industry Ascension 0 / cash | Industry Ascension 1 / cash |
| --- | --- | --- | --- |
| -42 | Day 28 / 859 | Day 44 / 231 | Day 48 / 215 |
| 42 | Day 28 / 664 | Day 51 / 144 | Day 51 / 101 |
| 99 | Day 24 / 650 | Day 36 / 113 | Day 36 / 155 |

Idle fails on day 90. Seed -42's target declines from 25 to 19 residents; seeds 42 and 99 decline from 20 to 14. Their prosperity ends at 25 with money still in household accounts and no Bread. This is physical supply failure, not fabricated household bankruptcy. These examples establish feasible actions in declared worlds, not universal victory or full balance.

### An actual liquidity recovery

On seed 42, default merchant automation reaches Northport on day 4 with ten Bread. On day 5 the market can buy eight, leaving cash **14**, household cash **452**, public Bread **8** and two aboard. Day 6's real logging wage of 12 leaves the market at **2** and households at **464**. The remaining cargo cannot be sold.

On day 8 residents buy one Bread for the exact ask **39**: market cash becomes **41**, households **425**, and public Bread **7**. On day 9 the same merchant sells one retained Bread for bid **37**, leaving market cash **4**. Retail has funded a real additional purchase; no balance was repaired. Later recovery is conditional on real food, wages and funds, and is not promised indefinitely. The same finite-market checks apply to NPCs and the player.

## Coverage pricing is an A/B hypothesis

StockOnly uses the authored inventory targets unchanged. Coverage reuses the same exact linear price engine, replacing only household-good targets at initialization and after each day's welfare update. Those immutable parameters stay frozen across every instant trade until the next daily boundary; current bid/ask quotes still respond to actual stock after each trade. This prevents a same-day trade from changing its own model through changed household conditions; retained snapshots and previews retain their own parameters.

The coverage horizon is **seven days**, bounded to **64 units** per good. Essential needs consume a hypothetical finite household budget first; eligible luxuries follow. For each, `ceil(7 / interval)` opportunities multiply the current population-scaled desired quantity. Exact affordability is measured against the original authored curve on a hypothetical shelf of `max(original target, desired coverage)`. Spend that hypothetical budget once across the needs, then set each dynamic household-good target to `max(1, affordable units)`. No future wages, desired-but-unfunded consumption, industrial demand forecast or prosperity multiplier is invented. Non-household prices and fixed quotes retain their original configuration.

This household-only hypothesis can flatten or sharply deepen prices when target units become small. Price bounds, exact bulk sums, same-edge bid/ask spread and no profitable immediate same-model reversal still apply. A lower food price can improve household access while lowering merchants' sale prices and changing future investment. Accounting correctness does not establish that this pricing choice improves the game; compare the measured report before choosing it.

## Reproduce and inspect the evidence

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --living-cities
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --living-cities --check
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --living-cities --deep .cache/living-cities-deep.json
```

The new [living-city report](experiments/living-city-results.json) is separate from all historical reports. Its normal run and freshness check were executed. The report is 3,446,531 bytes and contains 36 one-year comparisons: seeds −42, 42 and 99 × StockOnly/Coverage × Ascension 0/1 × idle/merchant/constructed-industry strategies. All use Merchant Company and leave the same NPC/public activity enabled. Two 1,825-day idle controls, 18 recovery trials and nine focused diagnostic worlds bring the total to **19,561 primary world-days**. Determinism replays and per-operation money, physical-flow and material-FIFO audits are additional work, not counted as independent worlds.

### What the experiments actually show

| Seed 42, Ascension 0, day 365 | StockOnly final company cash | Coverage final company cash | StockOnly regional Bread consumed | Coverage regional Bread consumed |
| --- | ---: | ---: | ---: | ---: |
| No player intervention | 600 | 600 | 108 | 108 |
| Merchant supply | 814 | 1,171 | 91 | 105 |
| Paid private bakery and shipping | 95 | 74 | 131 | 135 |

The coverage hypothesis changes cash distribution and some consumed quantities; it does not establish general welfare improvement. Merchant supply can move food away from another town and even reduce total regional consumption under a fixed policy. Industry creates more Bread and household income but incurs capital, materials and wages, and its low final cash is real. Neither final cash nor regional consumption alone ranks the quality of a strategy.

All 12 active recovery trials succeed and all six matched idle trials fail, with exact completion days shown above. These day-90 challenges can end successfully even when an unlimited continuation of the same policy later stagnates. Ascension 1 changes construction and subsequent trade timing: on seed −42 industrial recovery moves from day 44 to day 48; on seed 42 it completes on day 51 in both cases but leaves only 101 cash instead of 144.

The five-year seed-42 idle controls end with every town at population **10**, prosperity **25** and zero public Bread under both pricing models. The processing market retains five Logs but has only **6 cash**, below its **10-wage** processing requirement. Households still possess cash. The problem is cash distribution, missing food and blocked employment, not disappearing currency.

The executed optional deep sweep covers **25 seeds, 300 three-year comparisons, two ten-year idle controls, and the same 18 recovery/nine diagnostic cases: 338,571 primary world-days**. Every comparative world reaches population 10 in all towns by day 1,095; 100 of the 300 finish with zero Bread in all markets. Ten-year seed-42 idle cash and stock exactly match the five-year results. These are lasting stalls **under the prescribed policies**, not proof that no manual action could recover a town.

| Three-year deep sample | StockOnly | Coverage |
| --- | --- | --- |
| Merchant final cash, range / median | 675–937 / 800 | 826–1,286 / 1,008 |
| Industry final cash, range / median | 70–98 / 81.5 | 66–98 / 83.5 |
| Idle regional Bread consumed | 100–108 | 100–108 |
| Merchant regional Bread consumed | 88–126 | 88–124 |
| Industry regional Bread consumed | 124–139 | 122–147 |

Controlled fixtures separate mechanisms that a large seeded report can conflate:

- A funded public Bakery completes 365 batches, pays **4,380 wages**, and receives **4,380 retail money** for **730 Bread**. Its 100 market cash and 12 household cash return to their initial values. Finite granted inputs fund this duration; it is not a perpetual resource source.
- A Bakery with **11 market cash** cannot pay a **12** wage, even with inputs and households holding **1,000**. Across 180 days it makes no food and records **360** unmet food units from stock shortage. Need and household wealth cannot bypass the actual payer.
- A private employer pays **1,200 wages** over 180 days; households spend **1,200** on **200 Bread**. Company cash ends at **400**, market cash at **200**, and household cash at zero. Paid labor exposes a loss even though granted material inputs have zero paid basis.
- A zero-cash market initially rejects both player and NPC Plank sales. Two real Bread purchases for **12** then fund both one-Plank sales at **5** each. No employer or bailout is needed in this isolated liquidity example.
- A rich town enables durable demand on day **3**, buys **6 Furniture for 120** and **30 Bread for 180**, then loses its food supply from day **16**. Furniture demand disables on day **38**, despite **4,700 household cash** remaining; final prosperity is **25**. Wealth alone does not keep luxuries eligible.
- Matched fixed-population controls request **180** Bread at population 10 and **540** at population 30 over 180 days. The larger town consumes 400 and misses 140 for stock shortage, isolating population's physical demand effect.

### Performance and remaining questions

A console-only warmed benchmark stepped six four-town/four-ship worlds for 365 days each: **2,190 days in 5,006.012 ms**, or **2.2859 ms/day**, excluding report observers and serialization. The final check after concurrent Editor/build work ended measured the same stepping sample at **453.814 ms / 0.2072 ms per day**, illustrating the load sensitivity. Normal report generation took 11.685 seconds; a freshness run under concurrent Editor/test work took 58.176 seconds, and the final report run measured 7.941 seconds for its audited primary worlds before the separate stepping benchmark. The deep run with audits/replays took 144.858 seconds under concurrent load. These timings describe this development machine and workload, not a CI deadline or large-world scaling claim.

The normal sample observed at most **52 structured events per operation** and **16 open FIFO lots**; deep observed 52 and 18 respectively. Food history is capped at six observations, route evaluation at four dockside actions, and UI history remains bounded. Open inventory lots are retained until their goods leave and are not mathematically capped by the UI journal size.

The main unresolved problems are finite cash concentration, stock ceilings that stop wage income, integer demand jumps after growth, policy-limited imports and player attention. The next useful economic experiment should vary one explicit circulation or distribution rule at a time against these counterexamples and preserve exact reconciliation. No calibration or hidden subsidy was added to make the reported policies succeed. Extra commodities, fleets, city ownership, politics, loans, saves and further ascensions are not implemented solutions.

Actual UI/Editor/player evidence and limitations belong to [Unity verification](UNITY.md) and [Playtest](PLAYTEST.md). A headless victory alone does not certify pointer usability or a player backend. To update the published website after reviewing the changes on `main`, explicitly run GitHub Actions **Publish documentation**. Validation and a local MkDocs build do not publish; the Pages workflow remains manual-only.
