# Trading Lab playtest

TradingLab lets you operate the existing finite two-port economy. It is a small experiment in understanding prices, cargo, travel, and route rules. There is one ship, two goods, and no background clock, replenishment, save system, or win condition.

## Open and run

1. From the repository root, install the Python requirements and run `python tools/prepare_unity.py`, then `python tools/prepare_unity.py --check`.
2. Open `unity/` with Unity **6000.6.6f1**. Let import finish.
3. Open **`Assets/Scenes/TradingLab.unity`** and enter Play mode. The starting world has tick 0, trader cash 100, an empty six-unit ship at Northport, and automation disabled.
4. Give the Game view enough room. Use 1920×1080 or 1366×768 for the intended desktop checks; each of the three columns scrolls independently. Inspect the footer for input/action feedback.

The separate Bootstrap scene remains a production smoke. For Editor tests, the Windows development-player build, and actual verification results, see [Unity integration](UNITY.md). A built TradingLab player starts directly in this scene and stays open for interaction.

## Read the screen

The top strip shows the current tick, trader cash and cash change, total cargo/capacity, individual cargo goods, and accounting status. Cash spent on unsold goods is not automatically a realized loss. A realized gross-profit figure is available after successful trading when the ship is back at Northport with empty cargo; it compares cash against this scenario's empty-cargo starting state. There are no travel costs or other cash flows in this prototype.

**Ports & markets** shows the selected market, cash, stock, next marginal **SELL / BID** and **BUY / ASK** prices, local production, and an expandable pricing-parameter explanation. Click either port to inspect it without moving the ship or spending a tick. The highlighted port is your selection; the connection indicator and ship label show the actual dock or voyage. While sailing, the remaining tick count comes from committed simulation state. Remote prices are current information, not guaranteed arrival-time prices.

**Manual orders & time** shows the selected good's canonical description and both buy/sell previews for the entered quantity. A preview separates the exact total from the first and last marginal prices. Buying six logs initially costs **54**, with unit prices rising from **8 to 10**; buying two costs **16**. Multiplying the first price by six would understate the order. A missing or rejected preview explains the constraint instead of suggesting an executable trade.

**Route automation** shows the applied rule, editable outbound fields, fixed return settings, and the next stage/action/reason. Its proposal is an inspection until you explicitly advance. Recent activity records trades, rejections, production, departure/arrival, and changing policy decisions; it retains at most 40 entries and suppresses repeated unchanged shortages/decisions.

## Controls and time

| Control | Effect |
| --- | --- |
| Port buttons, Good, Quantity | Select or preview; no time passes. Quantity must be a positive whole number. |
| Buy / Sell (1 tick) | Submit one all-or-nothing trade at the selected dock, then run the ordinary production/travel step. |
| Buy + depart (1 tick) | Freshly verify a successful purchase, then buy and start sailing to the other port in the same tick. An unexecutable purchase cannot cause an empty departure. |
| Sail (1 tick) | Depart for the other port; the departure tick is the first of two travel ticks. |
| Next tick (1) | Advance exactly one tick. With automation enabled, use its current decision; otherwise submit no commands. |
| Advance 3 ticks | Execute up to three ordinary ticks; enabled automation can stop early for intervention/no progress. |
| Automation enabled | Enable or pause future policy execution; no tick passes. Disable it before manual orders. |
| Full preset / Restrictive preset | Immediately apply that policy and update the form; no tick passes and the world is not reset. |
| Policy fields / Apply policy | Editing creates a draft. Apply validates and installs it without changing markets, cargo, or tick. Invalid fields preserve the existing policy. |
| Auto tick (1) | Advance exactly one ordinary tick with the enabled policy's commands. An explicitly stepped wait/pause still permits production or existing travel to advance. |
| Run up to 6 ticks | Execute at most six automated steps; stop sooner on a pause or after three consecutive ticks without progress. |
| Scenario selector / Reset scenario | Selection alone does nothing. Reset starts a new selected world at tick 0, applies its preset, clears activity, and disables automation. |

Trading requires the ship to be docked at the selected port; remote inspection does not permit remote execution. Manual orders are unavailable while automation is enabled. Arrival commits at the end of the second travel tick, so the following action may trade at the destination. Every committed trading tick also runs local production. Inspecting, editing, applying, and pausing never advances the economy.

## Complete a manual expedition

Choose **Standard** and **Reset scenario**. Leave automation disabled. Use the combined purchase/departure buttons to reproduce the six-tick comparison:

| Starting tick | Your action | Expected committed state |
| --- | --- | --- |
| 0 | Northport, Logs, quantity 6: **Buy + depart (1 tick)** | Cash 46, six logs aboard, sailing to Southhaven with one tick left |
| 1 | **Next tick (1)** | Docked at Southhaven, still six logs and cash 46 |
| 2 | Select Southhaven: **Sell (1 tick)**, quantity 6 | Receive 144; cash 190, empty cargo; the sawmill uses delivered logs |
| 3 | Select Planks, quantity 2: **Buy + depart (1 tick)** | Pay 35; cash 155, two planks aboard, sailing to Northport |
| 4 | **Next tick (1)** | Docked at Northport with two planks |
| 5 | Select Northport: **Sell (1 tick)**, quantity 2 | Receive 64; tick 6, cash 219, empty cargo, realized gross profit 119 |

Check the changing marginal prices and the activity panel after each action. The full order's total should match its immediate preview. Selecting Southhaven during travel may show hypothetical trade economics, but its actual trade buttons must remain unavailable. The sawmill's output belongs to Southhaven's market, not to your ship.

For the smaller manual comparison, reset and buy only **2 logs** in the first action; sell those two at Southhaven, then use the same two-plank return. It ends at tick 6 with cash **169** and gross profit **69**. See [Manual shipping comparison](SHIPPING_DEMO.md) for the complete accounts and assumptions.

## Compare automation

Choose **Standard**, reset, enable automation, and inspect **NEXT DECISION** before running. It should propose buying six logs and departing, with total cost 54. Click **Run up to 6 ticks**. The ship returns empty to Northport at tick 6 with cash **219** and gross profit **119**. The next loading decision waits because the depleted Northport market exceeds the current buying limit; this is not continuous free profit.

Choose **Restrictive**, reset, enable automation, and run up to six ticks. The initial maximum buy price is **8** instead of **10**, so only two logs meet the rule. The return leg remains two planks. Expect tick 6, empty cargo at Northport, cash **169**, and gross profit **69**. Changing a preset on an already altered world does not recreate these initial comparisons; reset first when comparing them.

Outbound defaults are Logs, target 6, minimum 2, minimum sale price 19, and reserve 0. The target is total cargo to load, including already carried logs. The price limits constrain the worst marginal unit, not an average. The fixed return leg is Planks, target/minimum 2, maximum buy 18, minimum sell 31, with no empty return. These rules do not forecast future destination prices or guarantee market funds.

## Diagnose a liquidity blockage

Choose **No destination cash**, reset, enable automation, and run up to six ticks. The ship buys six logs for 54, arrives at Southhaven, then waits because **the destination market cannot afford a sale**. The bounded run stops at tick **5** after three unchanged waits. Cash remains **46**, all six logs remain aboard, and the cash change of **-54** is tied up in cargo; it is not labeled realized loss.

Select Southhaven and inspect its zero cash and the decision explanation. Lowering the minimum sale price and applying it cannot give the market money: the policy still evaluates actual bids and finite funds. Disable automation without advancing time and inspect legal manual choices. The selected policy cannot solve this liquidity blockage by waiting or changing a price limit. Do not infer that every possible manual strategy is impossible; the lab makes no automatic rescue or global deadlock claim. Reset is a new test world, not an economic recovery action.

## Recover from an overly restrictive policy

1. Reset **Standard**. Set **Maximum buy / unit** to **7**, click **Apply policy**, and enable automation.
2. The next decision should wait because the buy-price ceiling prevents the minimum shipment. Inspect tick 0, cash 100, and empty cargo. No economic action has happened.
3. Change the ceiling to **10** and apply. The world is still unchanged, but the fresh decision now proposes six logs for 54 and departure.
4. Click **Auto tick (1)**. The trade and departure occur together: tick 1, cash 46, six logs, one travel tick left.
5. Disable automation. Verify that pausing leaves that snapshot unchanged. **Next tick (1)** now advances travel manually; at Southhaven you may sell the cargo through the manual controls. Reenabling automation reevaluates the resulting state.

This changes permission to perform a feasible existing trade. It creates no inventory or liquidity and does not claim every blocked economy is recoverable.

## What to observe

- Before buying, can you predict the exact total and explain why six logs cost more per unit than two?
- Can you distinguish the selected market from the ship's actual location, and arrival from a completed destination trade?
- Can you identify whether stock, capacity, your cash, market cash, or a policy limit prevents an action?
- Does the decision explain why automation buys, sails, sells, or waits? Can you apply a draft deliberately and pause without expecting a tick?
- Can you distinguish a limit-change recovery from an illiquid market and from resetting the test world?
- At both intended desktop sizes, can you read values and reach controls using the independent column scrollbars? Record clipping, ambiguous labels, or missing feedback.

Record the scenario, tick, action, expected outcome, and observed confusion. These observations should guide the next UI iteration; successful automated checks alone do not demonstrate usability or enjoyment. The current lab has no objectives, saving, competitors, consumption, replenishment, cargo valuation, autonomous time, extra routes, or general route editor. Prices and scenario values remain experimental.

## Recorded Editor views

These actual Game view captures accompany the checks in [Unity integration](UNITY.md#task-14-tradinglab-verification).

![TradingLab at 1366×768: Southhaven cannot fund the retained six-log shipment.](images/tradinglab-1366.png)

![TradingLab at 1920×1080: the completed full expedition and expanded market pricing parameters.](images/tradinglab-1920.png)
