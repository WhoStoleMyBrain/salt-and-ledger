# Commerce, industry, and the supply charter

Task 1.6 extends the existing ArchipelagoLab with a centered trading overlay, private factories, and an optional fixed challenge. `EnterpriseScenario` is the shared expanded configuration for Unity and the new headless experiment. The historical two-good `ArchipelagoScenario` and its sixty-day report remain regression fixtures.

Canonical names, descriptions, buildings, and recipe quantities come from the [generated reference](reference/index.md). The six goods are Logs, Planks, Grain, Flour, Bread, and Furniture. Farming is explicit extraction; milling, baking, and furniture production connect raw materials to finished goods. Baking and sawmilling compete for logs, while furniture production and local construction compete for planks. Scenario schedules, prices, construction costs, and demand are runtime configuration rather than duplicated canonical definitions.

The farm's canonical batch produces four grain, supplying two milling batches before logistics and demand intervene. This is a provisional source rate, not automatic market replenishment. The [farm recipe](reference/recipes/harvest_grain.md), [milling recipe](reference/recipes/mill_flour.md), [baking recipe](reference/recipes/bake_bread.md), and [furniture recipe](reference/recipes/make_furniture.md) provide generated quantities and cross-references.

| Town | Public specialization | Recurring demand |
| --- | --- | --- |
| Northport | Lumber every 3 days, grain every 3 days, bakery every 3 days | Planks and furniture |
| Westhaven | Lumber every 3 days, mill every 2 days | Planks and bread |
| Southhaven | Sawmill every 2 days | Bread and furniture |
| Eastport | Furniture workshop every 5 days | Bread, planks, furniture |

The original timber and plank merchants remain, joined by one grain/flour merchant between Northport and Westhaven. That route physically carries grain to the mill and flour back toward the public bakery. No town contains every stage of either production chain. Initial stocks, finite accounts, ceilings, pricing parameters, routes, and all cadences are specified in `EnterpriseScenario.cs` and captured in the experiment's configuration and initial state. The company starts with 600 cash and Ledger with capacity 10; combined initial company/market cash is 25,800.

## Trade at a town

Select Ledger and right-click a town, or use the visible Trade action. Opening trade pauses playback without spending an economic day. The centered overlay shows town and ship stocks, current marginal bids/asks, company cash, town liquidity, cargo capacity, and the current day.

Select a good and direction, then enter or adjust quantity. The 1, 5, and maximum shortcuts prepare an order; only execution changes the economy. Maximum comes from the simulation's bounded exact-price calculation. Read the **exact total** and **average execution price** before submitting. Average is exact integer total divided by the selected executable quantity for display; it is not another monetary calculation or the first marginal quote multiplied by quantity. First and last marginal prices show the stock pressure within the order.

Trades execute the complete requested quantity or reject it. After a successful trade, authoritative stock, cash, cargo, prices, and previews refresh immediately, and another trade can occur on the same day. A preview belongs to one immutable snapshot; stale execution is rejected. Opening, closing, or editing a proposal never advances time.

Remote markets display current information and explain the docking restriction. A valid Sail Here action is a separate explicit departure command; the visible prices are not guaranteed at arrival. NPC inspection never grants player control. Escape, Close, or clicking the modal backdrop closes the overlay and keeps playback paused. Resume explicitly.

Full historical acquisition cost basis is deferred. Mixed purchases, partial disposals, manufactured output, and transfers need explicit cost attribution. The current quote is not a purchase-history estimate, and privately produced goods are not assigned zero acquisition cost.

## Invest in private industry

Select a town and open private industry management. Blueprints reference canonical building and recipe IDs. Their cost, interval, capacity, and eligible towns come from `EnterpriseScenario`, not UI constants. Construction is immediate and does not advance the day:

1. Validate the owner, location, blueprint, and unique facility ID.
2. Transfer the full construction payment from the company to the local public market.
3. Create an empty private stockpile with finite total item capacity.
4. Schedule the first batch for current day plus the blueprint interval.

There are no construction material requirements or completion delays in this prototype. A company may build at most one instance of each blueprint per town. Construction is a capital allocation decision; it grants no free goods or immediate batch.

| Private choice | Cost | Interval | Storage | Output ceiling | Eligible towns |
| --- | ---: | ---: | ---: | ---: | --- |
| Sawmill | 280 | 2 days | 40 items | 20 | Northport, Westhaven, Southhaven |
| Bakery | 300 | 2 days | 40 items | 30 | Northport, Westhaven, Eastport |
| Furniture Workshop | 360 | 3 days | 40 items | 15 | Southhaven, Eastport |

Each facility's inventory belongs to its owner and is separate from both the town market and ship cargo. Production consumes private inputs and retains private outputs. A batch requires all inputs, sufficient final total storage space, and any configured output ceiling. A shortage or blocked output consumes nothing and leaves the facility due. Successful production schedules its next opportunity from the completing day. Toggling a facility preserves its deadline.

One authoritative company account pays for all its ships and factories. Ship cash displays are views of that account, not additional balances. The implementation accepts multiple ships referencing an explicitly configured company, but this milestone adds no fleet-management interface.

## Supply, sell, or transport

Configure each recipe input's target stock and maximum marginal purchase price. A procurement policy buys only from that facility's local market, uses normal finite market transactions, and preserves its configured company cash reserve. Each configured input gets at most one bounded purchase attempt per day.

New construction starts with procurement and automatic sales off. The industry panel offers a suggested policy from the shared scenario; **Apply** commits that draft. Editing fields alone does not purchase inputs or change the active policy. Keep automatic sales off when retaining finished goods for a shipment.

Optional automatic sales keep a chosen output reserve and sell surplus only when the worst marginal bid meets the configured minimum and the market can pay. Production and these policies run while the facility is enabled. Missing stock, price limits, full storage, company funds, or market liquidity produce visible waits; automation does not replenish anything or find remote inputs.

Manual warehouse trades likewise change ownership and transfer actual cash through the shared trade calculator. Remote management of an owned factory is permitted because its transactions remain local. It does not permit remote ship loading.

For physical transport, dock Ledger at the factory's town and disable its route automation. The warehouse interface shows ship cargo and private stock side by side. Transfer a specified quantity in either direction, subject to available stock and destination capacity. The ship, stockpile, and requested owner must agree. Transfers conserve goods and ownership, move no cash, and spend no day. Sailing or remotely located ships cannot withdraw or deliver stock.

The current ship route policy trades with public markets; it does not load private warehouses automatically. Industrial shipping therefore uses explicit warehouse loading and normal voyages. Local factory procurement and sales continue without restocking clicks once configured. Broader warehouse route automation and recipe switching remain deferred.

The daily phase order is ship policies, private procurement, public production, private production, private automatic sales, public consumption, and voyages. Challenge evaluation follows those phases. A single atomic commit publishes the entire day; an exception rolls back all prior staged activity.

## Read cash flows honestly

Inspect each factory's construction cost, days since construction, batches, input consumption, output production, private stock, next deadline, and wait reason. Actual procurement expenditure and sale revenue are recorded separately. Revenue minus recorded input purchases is not full profit when supplied goods came through a ship or unsold inputs/output remain. Construction also uses company cash.

Unsold stock has no fabricated realized value. Compare company cash, capital expenditure, purchases, sales, and remaining goods together. Consumption removes goods without household payments; cash is finite and no subsidies or resets replenish it. Construction payments and all trades conserve combined company and market cash.

## The Coastal Supply Charter

The optional challenge uses the same towns and clock as the unlimited sandbox: supply **24 net Bread to Eastport by day 60**, retaining **at least 100 company cash** when completing the objective. The interface reads these conditions and progress from authoritative configuration.

Credit comes from completed player-company sales of the target good into the destination market, whether sold from a ship or private warehouse. Purchases of that good from that destination subtract from a **signed** net delivery counter. Buying and reselling the same stock cannot create credit. NPC trades, transfers, public production, and cargo merely arriving nearby grant no credit. Delivery is not described as fulfilled household consumption.

Success requires the configured net supply and cash threshold by the deadline. The final day's automatic phases may satisfy the objective before the end-of-day failure check. If they do not, the challenge fails at that daily commit; there is no extra manual trading window afterward. A terminal result stops economic commands. Reset creates a fresh world and clears progress; sandbox mode has no deadline.

The reproducible experiment selects provisional balance values and demonstrates merchant and manufacturing/shipping approaches. This establishes feasibility under stated assumptions, not optimal play or complete balance.

## Reproduce the experiment

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --enterprise
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --enterprise --check
```

The report compares ninety-day sandbox strategies and separate bounded challenge trials. It records actual construction, purchases, production, sales, company cash, private/public inventories, shortages, consumption, and NPC activity. Its cash and physical-flow reconciliation includes every owner and private stockpile. `--check` recomputes without changing the report. Historical pricing and archipelago reports retain their separate freshness checks.

The [generated report](experiments/enterprise-results.json) produced these measurements:

| Ninety-day strategy | Final cash | Construction | Purchases | Sale revenue | Private batches |
| --- | ---: | ---: | ---: | ---: | ---: |
| Idle | 600 | 0 | 0 | 0 | 0 |
| Merchant | 1,029 | 0 | 1,827 | 2,256 | 0 |
| Furniture workshop | 356 | 360 | 671 | 787 | 16 |
| Local food industry | 705 | 300 | 793 | 1,198 | 30 |
| Manufacturing and shipping | 1,637 | 300 | 793 | 2,130 | 30 |

Purchase and revenue totals include the player's actual ship and warehouse transactions. Final stocks differ and remain listed separately in the report; this is not a ranked profit comparison. The workshop's lower final cash exposes the opportunity cost of capital and limited input supply. Shipping privately made bread to Eastport earns more cash than the same food investment's local sales under these particular assumptions.

In separate challenge runs, idle play fails at day 60. The merchant succeeds on day 29 with 24 net bread and 1,014 cash; the hybrid succeeds on day 46 with 28 net bread and 1,123 cash. The merchant configures a Northport–Eastport bread route with a 100 cash reserve. The hybrid builds a Westhaven bakery, configures local input procurement, retains output, and physically shuttles bread from its private stockpile to Eastport. These are deterministic feasibility examples, not a claim that every factory or policy succeeds.

See [Playtest](PLAYTEST.md) for controls and recovery checks, [Mechanics](MECHANICS.md) for precise contracts, and [Unity integration](UNITY.md) for actual verification evidence and remaining platform limitations.
