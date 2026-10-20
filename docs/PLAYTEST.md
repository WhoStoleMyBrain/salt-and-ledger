# Playtest guides

Use [Living archipelago playtest](#living-archipelago-playtest) for the new four-town map. The following TradingLab guide preserves the original two-port regression comparisons and their tick-based transactions.

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

Record the scenario, tick, action, expected outcome, and observed confusion. Successful automated checks alone do not demonstrate usability or enjoyment. Legacy TradingLab has no objectives, saving, competitors, consumption, replenishment, cargo valuation, autonomous time, extra routes, or general route editor. ArchipelagoLab below adds its separately specified living-world features. Prices and scenario values remain experimental.

## Recorded Editor views

These actual Game view captures accompany the checks in [Unity integration](UNITY.md#task-14-tradinglab-verification).

![TradingLab at 1366×768: Southhaven cannot fund the retained six-log shipment.](images/tradinglab-1366.png)

![TradingLab at 1920×1080: the completed full expedition and expanded market pricing parameters.](images/tradinglab-1920.png)

## Living archipelago playtest

Run `python tools/prepare_unity.py` and its `--check`, open `unity/` in Unity **6000.6.6f1**, and open **`Assets/Scenes/ArchipelagoLab.unity`**. Enter Play mode. The world starts **paused, day 0**, with Ledger at Northport, cash 200, capacity 8, empty cargo, and player automation disabled. Two NPC merchants are already enabled but do not act until a day is advanced. Bootstrap and TradingLab remain separate scenes.

Use a 1366×768 or 1920×1080 Game view. The map is the main space; the selected-object panel scrolls independently. The gold **P** marker is Ledger; blue merchant markers are inspectable NPCs. The **Inspect ship…** fleet selector lists every ship and its player/NPC ownership, including ships whose sailing markers overlap. Port labels include authoritative dock counts. Select ports for stock, next marginal buy/sell prices, market cash, industries, and demand. Select ships for their real cargo, owner cash, voyage, and current decision. None of those selections spends a day.

### Controls and time

| Control | Effect |
| --- | --- |
| Play / Pause | Start/stop main-thread whole-day playback; pausing clears pending elapsed time |
| Next day | Pause and commit exactly one complete economic day |
| 1× / 3× / 10× | Change presentation cadence; base interval 1.5 seconds/day, bounded to four days per frame |
| Reset world | Construct the same fresh day-0 scenario, clear activity, disable player automation, restore paused 1× |
| Port / ship marker | Inspect that authoritative object; no day or revision change |
| Inspect ship… | Select any ship from the fleet, including coincident sailing markers; no command or time change |
| Select your ship, then port | Propose a direct voyage and show distance/duration; no departure until confirmed |
| Confirm voyage | Pause playback and start the player's voyage immediately; no day passes |
| Dockside trading foldout | Opening pauses playback for a stable manual interaction |
| Good / Qty | Refresh hypothetical exact previews; valid quantities are positive whole numbers |
| Buy now / Sell now | Immediate all-or-nothing transfer at the actual dock; revision changes, day does not |
| Route fields / Apply route | Draft then validate both directions; application changes policy/revision but not stocks, cash, or day |
| Enable player automation | Set future actor scheduling; no immediate trade or day advance |
| Run dockside actions | Pause playback; run at most four player policy actions now without daily production, consumption, or travel |
| Captain's log | Inspect at most 60 recent structured events; no time or hidden simulation history |

Manual orders require player automation disabled and the actual dock. A remote preview is economic information, not permission to trade. A paused world remains paused after a manual trade/order. Resume explicitly when ready. Changing speed does not change the results for the same ordered commands and number of economic days.

### Verify instant transactions and a geographic voyage

1. Reset. Select Northport and open **Dockside trading**. Select Logs, quantity 2. The initial exact buy total is 20; inspect first/last marginal prices separately.
2. Buy. Day remains 0; cash becomes 180 and cargo holds two logs. Northport stock and the next quote update immediately.
3. Buy two more after reading the new preview, which costs 22. Day is still 0; cash is 158, cargo four logs, and the state revision has advanced twice.
4. Try a quantity exceeding remaining cargo room. Execution must be unavailable or reject the whole request without spending a day or changing goods/cash. Invalid text must not submit a command.
5. Click **Select your ship**, then Southhaven. Inspect distance 300 and duration 3 days. Nothing sails until **Confirm voyage**.
6. Confirm. Day remains 0, the ship is underway with three days remaining, and trades are unavailable.
7. Click **Next day** three times. Remaining travel is two, one, then docked. NPCs, production, and demand advance in those same days. Select NPC markers and towns to inspect their movement and changing dock counts.
8. On committed day 3, select Southhaven and sell the four logs using the fresh exact preview. The sale succeeds immediately on day 3 if the displayed market can pay; no extra arrival/trading day is required.

Do not expect the old TradingLab 119/69 outcomes here: the geography, independent merchants, daily schedules, and instant trading are intentionally different.

### Watch a living world without player intervention

Reset, leave player automation disabled, and use playback. At day 1, both NPCs have purchased real cargo and departed on their three-day routes. Their source stocks, accounts, and local quotes reflect those purchases. At day 3 they arrive; at the following daily actor phase they can sell and return. The ledger stays idle unless ordered.

Expand Northport's industry detail to inspect +2 logs every 3 days and ceiling 18. Southhaven shows the canonical 2-log → 1-plank recipe every 2 days, input availability, next opportunity, enablement, and waiting outcome. Eastport consumes a plank every 4 days; shortages record unmet demand without creating goods. Westhaven's unused logs eventually stop at its configured ceiling.

Pause during a voyage. The marker should settle at the committed position, and no additional economic day should appear while paused. Resume at 3× then 10× and verify legible moving markers, selectable objects, and occupancy changes. The fastest mode may skip intermediate visible days, but authoritative arrival and market access still follow exact daily commits.

### Configure both directions and recover from a restrictive rule

Reset, open **Your trade route**, and inspect origin/destination, both commodity selectors, both target/minimum quantities, both marginal buy/sell limits, both purchase caps, reserve, and empty-return permission. Numeric fields are drafts until **Apply route**. Return None requires empty return; equal outbound/return goods are explicitly rejected. Invalid drafts must preserve the prior policy.

Set outbound buy maximum to **1**, apply, and enable automation. The decision should explain why no minimum log shipment meets that price. **Run dockside actions** makes no trade and spends no day. Raise the ceiling to **14**, apply, and run again: the initial six-log purchase/departure is feasible, with exact total 66, leaving cash 134, six logs, day 0, and three travel days remaining. The expenditure cap and reserve must still be enforced.

Advance to arrival. Run dockside actions to unload and, if current return stock/prices permit, buy return planks and depart during the same day. Inspect every exact transaction and retained cargo. Change a sell floor upward to create a wait, then lower it to a feasible value; this changes permission, not cash or inventory. A price edit cannot solve actual market insolvency.

Disable automation without moving the clock. Legal manual controls should become available according to the ship's real location; an existing voyage continues only when days advance. Reenable or replace the route to evaluate the committed cargo/location rather than resetting a hidden phase. Changing ports while carrying incompatible cargo may require deliberate manual intervention.

### Check ownership, competition, and presentation

- Select each NPC while docked and sailing. Use **Inspect ship…** when voyage markers coincide. Cargo, owner cash, destination, remaining days, and policy reason should be inspectable; player manual controls must never command that NPC.
- Compare an idle reset with an enabled player route. All actors use the same stocks and prices; player purchases can reduce what a later merchant can load.
- Expand local industry/demand and pricing details. Values should match the selected town's actual schedule/configuration, including ceilings, input shortages, unmet demand, and recent real deliveries.
- Read exact bulk previews before acting. A first marginal quote times quantity may be wrong; sale revenue and market liquidity are distinct constraints.
- At both desktop sizes, ensure town/ship markers and primary clock/voyage controls remain reachable while detail panels scroll. Check overlap offsets, selected highlighting, readable waiting reasons, and collapsible activity.
- When cargo is unsold, cash change must not be labeled realized profit/loss. Reset is a fresh test world, not a recovery action or subsidy.

Record the day, revision, selected object, action, expected result, actual result, and confusion. The [headless comparison](LIVING_ARCHIPELAGO.md#reproducible-sixty-day-experiments) provides reproducible economic evidence; it is not a substitute for observed human understanding. Actual graphical/test/player verification and screenshot evidence are listed in [Unity integration](UNITY.md).

### Recorded archipelago views

These actual Editor Game view captures use the verified runtime resolutions and final map/fleet controls. Both show day 7 with the player idle while NPC merchants have purchased, transported, and sold real goods. [Unity integration](UNITY.md#task-15-living-archipelago-verification) records the separate manual, policy, playback, motion, and test evidence.

![ArchipelagoLab at 1366×768 on day 7: four towns, all three ships, and Southhaven's scheduled sawmill and recent merchant activity.](images/archipelago-1366.png)

![ArchipelagoLab at 1920×1080 on day 7: Northport's lumber extraction, unmet plank demand, current prices, and actual NPC purchases.](images/archipelago-1920.png)
