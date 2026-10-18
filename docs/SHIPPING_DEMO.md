# Manual two-port trading comparison

Run the executable scenario from the repository root:

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --shipping
```

The runner uses `MaritimeWorldState`, the same exact pricing/accounting as the local-market demos, and the canonical sawmill recipe. It prints each tick's trades, production, movement, balances, cargo, and both ports' marginal quotes. It checks every documented final balance and transaction total, cash conservation throughout the transcript, and identical transcripts for repeated runs and reversed port-configuration input order. CI runs this mode; it produces no separate generated artifact and does not change the existing pricing report.

## Handcrafted starting conditions

There are exactly two ports, one trader with **100 cash**, and one ship docked at Northport with **empty cargo and capacity 6**. Every item occupies one unit, regardless of good. The bidirectional route takes **2 ticks**. Both ports start with **1,000 cash**. Combined cash is **2,100** and remains unchanged.

| Port | Logs | Planks | Production |
| --- | ---: | ---: | --- |
| `northport` | 18 | 0 | None |
| `southhaven` | 2 | 0 | One enabled canonical sawmill |

Every quote uses experimental **linear** pricing. These are runtime scenario values, not canonical good prices. Parameter symbols and the exact edge formula are defined in [Mechanics](MECHANICS.md#inventory-sensitive-pricing-hypotheses).

| Port / good | Reference `P` | Target `T` | Sensitivity `k` | Minimum | Maximum | Per-side spread |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Northport logs | 10 | 12 | 6 | 1 | 50 | 1 |
| Northport planks | 24 | 6 | 12 | 1 | 60 | 1 |
| Southhaven logs | 20 | 6 | 12 | 1 | 60 | 1 |
| Southhaven planks | 12 | 6 | 6 | 1 | 50 | 1 |

Initial marginal bid/ask quotes are Northport logs **5/8**, Northport planks **33/none**, Southhaven logs **25/29**, and Southhaven planks **16/none**. An absent ask means the market has no stock. Remote inspection is permitted, but a quote or hypothetical preview never authorizes remote execution. Exact bulk totals sum marginal inventory edges; the first displayed ask multiplied by quantity is not necessarily the purchase cost.

## Manual itinerary and timing

Two runs start from the same state and follow the same itinerary. The smaller shipment buys **2 logs**; the full shipment buys **6 logs**. Neither run makes automated decisions.

| Starting tick | Submitted action | Committed consequence |
| --- | --- | --- |
| 0 | Attempt to buy 7 logs; buy the chosen 2 or 6 logs; depart for Southhaven | The first purchase rejects for capacity without changing goods/cash. The chosen purchase succeeds. Southhaven's initial 2 logs produce its first plank. Ship has 1 travel tick remaining. |
| 1, failed call | Attempt to sell logs at Southhaven while sailing | `InvalidOperationException`; the complete snapshot and tick remain unchanged. |
| 1, valid call | No commands | Ship arrives at Southhaven at the end of the tick, with cargo unchanged. |
| 2 | Sell all carried logs at Southhaven | Local bids determine revenue. One batch immediately consumes 2 delivered logs and makes the second market plank. |
| 3 | Buy 2 planks at Southhaven; depart for Northport | Purchase uses the committed stock from the previous tick. Ship has 1 travel tick remaining. |
| 4 | No commands | Ship arrives at Northport at the end of the tick. |
| 5 | Sell both planks at Northport | The expedition finishes at world tick 6, docked at Northport with empty cargo. |

Production runs at Southhaven every successful tick, even while the ship travels. The full log shipment permits two additional batches during the return journey; the small shipment does not. Movement itself changes no stock or price. Trading always precedes production, and arrival becomes tradeable on the following step.

## Measured comparison

These values are asserted by the executable scenario, including each leg's quantities and totals.

| Measurement | Small: 2 logs | Full: 6 logs |
| --- | ---: | ---: |
| Outbound cargo / capacity | 2 / 6 | 6 / 6 |
| Logs purchased and sold | 2 | 6 |
| Log purchase cost | 16 | 54 |
| Log sale revenue | 56 | 144 |
| Realized log profit | 40 | 90 |
| Planks purchased and sold on return | 2 | 2 |
| Plank purchase cost | 35 | 35 |
| Plank sale revenue | 64 | 64 |
| Realized plank profit | 29 | 29 |
| Total purchase cost | 51 | 89 |
| Total sale revenue | 120 | 208 |
| **Realized gross trading profit** | **69** | **119** |
| Successful ticks | 6 | 6 |
| Final cargo | Empty | Empty |
| Final trader cash | 169 | 219 |
| Final Northport cash | 952 | 990 |
| Final Southhaven cash | 979 | 891 |
| Final Northport logs / planks | 16 / 2 | 12 / 2 |
| Final Southhaven logs / planks | 0 / 0 | 0 / 2 |

Northport's log purchase marginal prices are **8, 8** for the small order and **8, 8, 9, 9, 10, 10** for the full order. Arrival finds Southhaven's logs exhausted by production, so selling logs crosses bids **29, 27** or **29, 27, 25, 23, 21, 19**. The return purchases cross plank asks **17, 18**; the final sales cross Northport plank bids **33, 31**. Those prices explain both the larger total profit and the lower profit per log for the full shipment: **20** versus **15**.

At the end, Northport log bid/ask is **6/9** for the small shipment or **8/11** for the full shipment. Its plank quote is **29/33** in both runs. Southhaven logs show **29/none** in both; its planks show **16/none** after the small shipment or **14/17** after the full shipment. These are quotes from the final inventories, not a separate price update.

Both decisions finish at the same port and tick with no unsold trader goods. Therefore sale revenue minus purchase cost equals the change in trader cash; no inventory valuation is hidden in the profit comparison. The local economies end in different states, including two extra Southhaven planks after the full shipment. The comparison does not value those public-market assets as trader property or prove that the larger shipment is generally optimal.

## What this establishes and what remains open

Finite capital, cargo, local price depth, production, and travel already change the consequences of a manual decision. Filling the ship makes more gross profit here, while each additional log faces a higher purchase price and a lower destination sale price. The capacity-7 rejection demonstrates that a displayed opportunity cannot bypass the ship's limit. Production changes destination prices before arrival; a remote current quote is not a guaranteed future price.

These ports are handcrafted. Production belongs to the public market; no player factory or warehouse exists. One trader owns one ship, capacity uses item counts rather than commodity weights, and perfect market information is available. Travel has no operating costs, fees, or risk. There is no automatic replenishment, consumption, external income, competing trader, or final economic balance. Gross profit here is deliberately not a claim about a complete operating business.

Task 1.3a's manual itinerary remains unchanged. Task 1.3b adds a separate [configured automation comparison](AUTOMATION.md), using the same `ShippingScenario` factory and existing commands. It reproduces the 69/119 outcomes at every tick, changes shipment size through a marginal buy limit, and demonstrates bounded waiting when destination liquidity is absent. Neither mode receives hidden stock transfers or a second clock.
