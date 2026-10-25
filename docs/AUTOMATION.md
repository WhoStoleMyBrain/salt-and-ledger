# Configurable trade-route automation

Automation repeats the transactions you configure. You still choose the route, commodities, shipment sizes, acceptable prices, and cash reserve. It does not search for the best strategy or promise a profitable destination. A route can wait indefinitely when finite markets cannot satisfy your rules.

New v3 pooled runs use [ordered cargo itineraries](#ordered-cargo-itineraries) through **Company → Fleet → Route planner**. One ship can carry mixed goods through several ordered market and company-stock instructions at each of multiple ports. The sections before that contract preserve the legacy two-port API and TradingLab comparison. [Regional automation](#regional-automation-in-archipelagolab) and [industrial shipping](#industrial-shipping-between-private-stock-and-markets) remain available in older runs and saves with their original behavior.

Run the controlled comparison from the repository root:

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --automation
```

The manual `--shipping` mode remains available. Both demos use the same `ShippingScenario` factory: the [documented Northport/Southhaven markets](SHIPPING_DEMO.md#handcrafted-starting-conditions), capacity 6, trader cash 100, two-tick travel, linear pricing, and the canonical Southhaven sawmill. Only the blocked example changes Southhaven's initial cash. Policy parameters are runtime choices, not new canonical goods or content definitions.

## Configure an expedition

```csharp
var policy = new TradeRoutePolicy("northport", "southhaven",
    outbound: new RouteLeg("logs", maxPurchaseQuantity: 6,
        minShipmentQuantity: 2, maxBuyPrice: 10, minSellPrice: 19),
    returnLeg: new RouteLeg("planks", maxPurchaseQuantity: 2,
        minShipmentQuantity: 2, maxBuyPrice: 18, minSellPrice: 31),
    cashReserve: 0, allowEmptyReturn: false);

// One normal world tick, evaluated and executed without intervening commands.
AutomationStep step = TradeRouteAutomation.Advance(world, policy);
// Inspect step.Decision and step.Result; the world remains authoritative.
```

| Setting | Player-facing meaning |
| --- | --- |
| Origin / destination | The two existing ports, with one fixed route in each direction |
| Outbound / optional return commodity | What to load at each source and sell at the other port; the two goods must differ |
| Maximum purchase quantity | Target total shipment when loading at that source; existing same-good cargo counts toward it |
| Minimum shipment quantity | Minimum total cargo of that leg's good before loading/departure; sales may be smaller |
| Maximum buy price | Highest acceptable marginal unit price anywhere in the order |
| Minimum sell price | Lowest acceptable marginal unit price anywhere in the order |
| Cash reserve | Cash to preserve after a purchase; it does not stop sales or charge travel |
| Allow empty return | Permit an empty ship at the destination to return if no qualifying return purchase is available |

Quantities and limits must be positive, minimum quantity must not exceed the target, reserve must be nonnegative, and ports must differ. A missing return commodity requires an explicitly allowed empty return. Unknown ports/goods or missing quotes in either port throw when evaluating. There is no rule requiring the sell limit to exceed the buy limit: accepting a loss is a player choice. Minimum shipment exceeding ship capacity pauses with `MinimumExceedsCapacity` rather than generating impossible orders.

## What the ship does

At Northport, first sell any return cargo. Once empty, load outbound logs and depart in the same tick if the shipment meets the rules. At Southhaven, first sell outbound logs. After all are sold, consider the return purchase on the next tick. A sale never includes departure, and one tick contains at most one automated trade. While sailing, issue no orders and advance the existing world step. There is no extra automation clock.

Existing outbound cargo at its source can be topped up toward the target. A qualifying smaller shipment departs when no further eligible units can be bought; cargo already above the target is not discarded. Below-minimum cargo waits without buying subminimum increments. Partial sales are permitted, even below the loading minimum: sell the largest eligible portion, retain every unsold unit, and reassess next tick. Do not sail away from unsold cargo. A configured empty return is possible only with completely empty cargo at the destination; it never abandons goods or authorizes an empty outbound voyage.

The evaluator infers the stage from location and cargo. It stores no mutable route phase. Cargo outside the policy or a mixture of outbound and return goods pauses with `UnexpectedCargo` or `MixedCargo`. Manual intervention or a suitable replacement policy can resolve these conditions without silently changing inventories.

### Quantity and marginal prices

The policy chooses the largest eligible quantity, bounded by the target, stock, cargo room, buyer cash, and reserve. For sales, available cargo, market stock room, and market cash are the bounds; the loading target and minimum do not restrict unloading. Buying crosses nondecreasing asks and selling crosses nonincreasing bids, so the last marginal price is the worst price in each order.

At Northport, the first six log asks are **8, 8, 9, 9, 10, 10**. A maximum of 10 permits six logs costing **54**, while a maximum of 8 permits two costing **16**. Multiplying the first ask by six would incorrectly produce 48. Southhaven's arrival-time log bids are **29, 27, 25, 23, 21, 19**. A minimum of 19 permits all six, generating **144**; a minimum of 25 permits only three, leaving the other three on board. Those residual goods prevent return loading until sold or handled manually.

Quantity selection uses bounded binary search, at most 31 probes across the positive `int` range. It calls the existing marginal edge function and exact pricing sum; it never loops once per possible unit or duplicates a pricing formula. Search probes compare exact `BigInteger` totals with the finite cash budget, allowing a huge unaffordable proposal to shrink without treating representable smaller purchases as errors. The final selected transaction must pass `MaritimeWorldSnapshot.PreviewTrade`. Receiver balance overflow and unexpected errors still throw; they are not converted into waiting or hidden by retries. [Mechanics](MECHANICS.md#configurable-route-policy) gives the complete contract.

## Execution, waiting, and player control

Each immutable decision exposes its source snapshot/tick/position, stage, action, reason enum, selected quantity, optional normal trade/departure commands, and successful hypothetical preview. A UI can explain a wait using structured fields rather than parsing demo text.

`TradeRouteAutomation.Execute(world, decision)` requires the exact current snapshot, freshly previews its selected trade, and sends only those commands through the ordinary atomic `Step`. A stale decision or a decision from another world fails before stepping. The host must serialize access and keep manual commands out of this execution. This makes buy-and-depart safe without changing manual behavior: a manually submitted rejected purchase can still be followed by departure. Later production or tick failures retain the existing complete rollback of markets, cargo, cash, controls, movement, and time.

| Condition | Policy response |
| --- | --- |
| Not enough stock for a minimum shipment | Wait: `InsufficientInventory` |
| Minimum shipment would cross expensive asks | Wait: `BuyPriceLimit` |
| Minimum shipment exceeds available cash / reserve budget | Wait: `InsufficientTraderFunds` / `CashReserve` |
| No carried unit meets the minimum selling price | Wait: `SellPriceLimit`; retain cargo |
| Market cannot buy one eligible unit | Wait: `InsufficientMarketFunds`; retain cargo |
| Market cannot hold another unit | Wait: `MarketInventoryFull` |
| Disabled, incompatible cargo, or impossible minimum | Pause with the corresponding structured reason |

Waiting emits no impossible trade. A normal empty tick still permits production and travel, then the next evaluation sees the newly committed state. Waiting expresses a condition to reassess, not a promise that it will resolve. There is no automatic replenishment, external income, price override, cargo disposal, or internal retry loop.

The host owns enablement and the current immutable policy reference. Pass `enabled: false` to evaluation, or stop making automated calls, and continue using normal manual `world.Step(...)`. Replacing the policy between steps does not mutate current markets, cargo, historical snapshots, or past decisions. Discard any pending decision when changing policy: it intentionally retains the old immutable choice. Explicitly executing a pause decision or calling `Advance(..., enabled: false)` performs an empty ordinary tick; the bounded runner below instead stops before executing a pause. Pausing automation does not freeze the world or an existing voyage.

```csharp
AutomationRunResult observation = AutomationRunner.Run(world, policy,
    maxTicks: 12, maxNoProgressTicks: 3,
    observer: step => Console.WriteLine(step.Decision.Reason));
```

This optional synchronous host loop reports `TickLimit`, `NoProgress`, `Paused`, or `Disabled`; it retains only initial/final snapshots and the last decision. A successful trade, movement event, or changed production inventory counts as progress. Tick advancement alone does not. The two positive finite bounds prevent endless observation even if unrelated production keeps resetting the idle counter. `NoProgress` is a host observation, not a proof that the route can never recover or a change to its enabled policy. Disabled/structurally paused policies stop before advancing the world. The observer runs **after** each commit, must not mutate the world, and owns any retained diagnostics. Observer exceptions cannot undo a completed step; a run is not one multi-tick transaction.

## Measured scenarios

The executable asserts every balance below. It compares repeated runs, reversed port setup order, and `tr-TR` culture output. A and B are also compared against the original manual command sequence at **every committed tick**, including both markets, controls, quotes, cash, cargo, and voyage state.

| Measurement | A: log buy limit 10 | B: log buy limit 8 | C: Southhaven cash 0 |
| --- | ---: | ---: | ---: |
| Logs bought / sold | 6 / 6 | 2 / 2 | 6 / 0 |
| Planks bought / sold | 2 / 2 | 2 / 2 | 0 / 0 |
| Purchase cost | 89 | 51 | 54 |
| Sale revenue | 208 | 120 | 0 |
| Realized gross profit, completed interval | **119** | **69** | Not reported: unsold cargo |
| Ticks consumed | 6 | 6 | 5 |
| Successful / rejected automated trades | 4 / 0 | 4 / 0 | 1 / 0 |
| Final dock / cargo | Northport / empty | Northport / empty | Southhaven / 6 logs |
| Trader cash | 219 | 169 | 46 |
| Northport cash | 990 | 952 | 1,054 |
| Southhaven cash | 891 | 979 | 0 |
| Northport logs / planks | 12 / 2 | 16 / 2 | 12 / 0 |
| Southhaven logs / planks | 0 / 2 | 0 / 0 | 0 / 1 |
| Runner stop | `TickLimit` | `TickLimit` | `NoProgress` |

A and B keep log sell minimum 19, return plank target/minimum 2, buy maximum 18, sell minimum 31, reserve 0, and no empty return. C uses A's policy and changes only destination cash from 1,000 to 0; its initial combined cash is therefore 1,100 instead of 2,100. All transfers conserve the appropriate starting combined cash.

The six-tick itinerary is unchanged: buy/depart at tick 0, arrive after tick 1, sell logs at tick 2, buy planks/depart at tick 3, arrive after tick 4, and sell planks at tick 5. The runner's six-step limit marks a completed comparison interval with empty cargo. The policy is still enabled and would evaluate another expedition if called again; both final source asks now exceed the configured limits, so its next decision is `Wait/BuyPriceLimit`.

C buys six logs for 54 and arrives normally. At ticks 2, 3, and 4 it waits with `InsufficientMarketFunds`; no sale or further voyage is issued. Southhaven's initial logs made one plank on tick 0, but production later stops for lack of inputs. After three consecutive unchanged ticks, the runner stops at world tick 5 with all six logs retained. A cash change of -54 is tied up in unsold cargo and is not reported as a completed trading profit or loss. Restarting the same policy against unchanged markets would still wait.

### Manual attention comparison

Equivalent manual and automatic expeditions earn the same **119** (six logs) or **69** (two logs), take six ticks, and leave identical local inventories and liquidity. Automation has no discounts, faster travel, or special execution permission. Changing the buy threshold is the only cause of B's smaller shipment and different economic result.

For this comparison, an explicit manual decision is one action batch: outbound buy/depart, outbound sale, return buy/depart, or return sale. That is **four manual action batches versus one policy configuration**, excluding initial world setup and observation limits for both. This measures a reduction in prescribed input, not player cognition or a usability test. Both need six host advances; the automation evaluates six decisions internally. The original manual demo additionally performs **one deliberate capacity rejection** and **one illegal transit request** that consumes no tick. These are fault demonstrations, separate from its four useful batches. The automated itinerary submits no rejected transactions.

## Operate the policy in TradingLab

Open `Assets/Scenes/TradingLab.unity` for the playable host described in [Playtest](PLAYTEST.md). **Full preset** and **Restrictive preset** apply the measured policies above without advancing time. The editable outbound fields are maximum marginal buying price, minimum marginal selling price, total shipment target, departure minimum, and cash reserve. Field edits remain drafts until **Apply policy**; a valid application replaces the immutable policy and reevaluates from the same snapshot. Return defaults are displayed: two planks, buying at most 18 and selling at least 31 per marginal unit.

Enabling or disabling automation also leaves the world untouched. When enabled, **Next tick** and **Advance up to 3 ticks** use the policy; the automation panel additionally provides a one-tick action and **Run up to 6 ticks**. A bounded run stops after six steps, three consecutive no-progress ticks, or a policy pause. Its decision panel and recent activity distinguish loading, sailing, selling, waiting, and intervention. Disable automation to use manual trading or sailing; a step never mixes manual and policy orders.

For a recovery that uses no economic rescue, reset Standard, set the outbound buy ceiling to **7**, apply, and enable automation. Evaluation waits because the required log shipment exceeds that limit. Raise the ceiling to **10** and apply: the world tick, cash, and stock are still unchanged, but the next decision can buy six logs and depart. Only the next explicit tick performs that action. This demonstrates recovery from a restrictive rule, not replenishment.

The **No destination cash** scenario instead retains six logs at Southhaven because that market cannot pay. Lowering the sale limit does not supply money. Pausing permits inspection and legal manual intervention; reset begins another test world. The lab does not promise that every finite-market blockage is recoverable under the selected policy.

## Limits and next questions

The legacy comparison supports one ship, two ports, and distinct outbound/return goods. It cannot forecast destination prices, reserve liquidity, discover commodities, sell mixed cargo intelligently, or guarantee sustained profit. Numeric overflow remains an explicit error. Finite markets can permanently strand goods or cash; the policy does not repair the economy. TradingLab has no general route editor or background clock. The new regional host below adds those explicitly requested presentation/scheduling capabilities while preserving the policy's economic limits.

The verified arithmetic comparisons establish mechanical equivalence and a meaningful threshold choice. They do not establish enjoyable automation, sustainability, or optimal strategy. Task 1.4 makes those comparisons and a limit-change recovery playable. Task 1.5 extends the world and presentation below; the need for observed playtesting remains. See the [roadmap](ROADMAP.md#recommended-next-milestone-observed-playtesting) and actual [Unity verification scope](UNITY.md).

## Regional automation in ArchipelagoLab

The new map exposes every configured leg: origin/destination port, outbound good, optional distinct return good, maximum purchase quantity, minimum shipment, worst acceptable marginal buy/sell prices, per-purchase expenditure cap, shared cash reserve, and empty-return permission. Blank UI spend caps mean `long.MaxValue`. Selecting None for return requires empty return. Drafts are passive; **Apply route** validates and replaces the immutable policy without buying, sailing, or advancing a day.

`RouteLeg` accepts optional `maxPurchaseExpenditure`, defaulting to `long.MaxValue`. The available buying budget is the smaller of that cap and cash after reserve. The cap applies to one purchase, not cumulative voyage spending. Existing cargo counts toward the target as before. A minimum prevented by the cap reports `PurchaseExpenditureLimit`; limits still constrain the worst marginal unit rather than the first displayed quote or an average.

The regional world stores each actor's policy and enablement in its committed snapshot. That configuration change increments revision, making old decisions stale within the same day. An enabled actor cannot also receive manual trade or sailing orders. Pausing automation spends no day and leaves existing voyages in place.

At each daily boundary, enabled ships act in priority/ordinal-ID order. The NPC priorities precede the player's default priority. The world stages up to four actions per actor, reevaluating after each change. Selling outbound cargo, purchasing return cargo, and departing can happen at one economic time, provided each operation succeeds. Unsold cargo remains aboard and blocks return loading. A wait, pause, sailing state, lack of progress, or action bound stops the chain. The entire daily transaction still commits once; an exception later in production or another actor rolls everything back.

**Run dockside actions (up to 4)** pauses playback and explicitly executes the player's bounded chain now. It consumes no day, runs no production/consumption/travel, and commits one revision if anything changes. It does not secretly run NPC policies. **Next day** or playback instead resolves all scheduled actors and economic phases together. Arrival at a daily commit permits immediate manual trade or explicit dockside automation; automatic daily execution otherwise acts at the next boundary.

The regional adapter projects the actor's selected ports, account, and cargo into the existing evaluator. It does not create independently ticking markets or duplicate price/accounting formulas. NPC source thresholds only gate loading before ordinary evaluation; the merchants obey the same stock, exact price, reserve/budget, cash, capacity, and movement checks.

The [sixty-day comparison](LIVING_ARCHIPELAGO.md#reproducible-sixty-day-experiments) records player input counts, cash, closed profit, unsold cargo, shipment totals, and effects on NPC activity. Restrictive buying can remain inactive for all sixty days; aggressive buying can starve an NPC's source threshold; alternative-route supply can deepen destination stock and reduce bids. No policy discovers a strategy, predicts arrival-time prices, creates money, or guarantees recovery. The [playtest guide](PLAYTEST.md#living-archipelago-playtest) provides the map controls and diagnostic checks.

## Private production policies

Task 1.6 adds a separate small `IndustryPolicy` for a fixed canonical factory recipe. Each input has a target quantity and maximum marginal buy price; each output may have a warehouse reserve and minimum marginal sale price. The policy also controls procurement/sales enablement and company cash reserve. Applying a draft is instantaneous and executes nothing.

On each day, enabled factories buy local inputs before public/private production, then optionally sell surplus private output before public consumption. Facilities are ordered by ordinal port and facility ID, rules by ordinal good ID. At most one transaction per configured good occurs in each phase. Bounded search uses exact shared price totals and the worst marginal price, local stock, company/market cash, receiving account headroom, and storage capacity. It never buys one unit in an unbounded retry loop.

Missing inputs, exhausted market cash, price limits, full storage, cash reserves, or a full receiving account create explicit waits. Already satisfied targets and retained output reserves are distinguished from shortages. An enabled factory continues on its schedule without repetitive toggles; disabling it pauses both production and policies without resetting its deadline.

Local warehouse transactions may be managed remotely, but the goods remain at that town. Ship/storage transfer requires the actual dock and correct ownership. Task 1.7 connects these private inventories to the industrial shipping policy below. The historical Task 1.6 experiment still uses its original explicit warehouse loading and voyages.

See [Commerce and industry](ENTERPRISE.md) for the tested defaults and historical comparison. Factory purchased-stock expenditure and sale revenue are cash flows. Task 1.7's company accounting separately follows actual acquisition basis through physical transfers, private manufacturing, held stock, and completed sales.

## Living-city presets and financial waits

Living Cities v2 uses the same policy evaluators and commands. Default routes resolve generated roles rather than historical Northport/Eastport names. `LivingCityScenario.MerchantPolicy` carries Bread from food source to vulnerable coast: target 10, minimum 4, buy ceiling 34, sell floor 26, purchase cap 300, reserve 100 and empty return. No policy edit changes household or market balances.

The living bakery procurement preset targets four Flour at marginal ask at most 28 and four Logs at most 24, with company reserve 80; output stays private unless local sales are explicitly enabled. Completed bakery batches additionally pay wage 6. The industrial shipping preset uses the actual owned warehouse, by default in the processing town, and sends Bread to the vulnerable town: target eight, minimum two, source threshold four, sale floor 26, reserve 80 and empty return. Construction, procurement, wages, loading and voyages are real ordinary operations.

Payroll shortage can stop output even with inputs present. Stock ceilings can stop public jobs and hence local household income. Resident purchases replenish market liquidity only by transferring actual household money for actual stocked goods. NPCs and the player face identical finite-market sale checks; neither gets a high-need exception. A correctly configured route may remain blocked indefinitely after money or goods stop circulating.

The [living-city comparison](LIVING_CITIES.md) measures these prescribed merchant/industry strategies against idle play, both pricing hypotheses and both ascensions. Historical reports below retain their zero-wage, unpaid-consumption fixtures and original numbers.

## Industrial shipping between private stock and markets

An industrial route assigns one ship to two directly connected ports. Each directional leg carries one chosen good and selects a source and destination operation:

| Endpoint | Source operation | Destination operation |
| --- | --- | --- |
| Public market | Buy with company cash at exact marginal asks | Sell cargo into finite market stock/cash at exact marginal bids |
| Owned warehouse | Load actual private stock into the docked ship | Unload actual cargo into that company's warehouse |

The outbound and optional return legs may carry the same good. A useful return can buy factory inputs at the destination and unload them privately at the origin. This is a fixed two-port policy, not a waypoint language, automatic route discovery, or fleet optimizer.

Choose the shipment target and departure minimum, source-stock threshold, private source reserve, marginal buying/selling limits, purchase expenditure cap, company cash reserve, and empty-return permission. Warehouse reserves apply to private loading only. The source threshold checks total source stock before a new shipment; the reserve determines how much remains after loading. For example, waiting for **8 Bread**, retaining **2**, and loading **6** sends a complete shipment while preserving two private units.

Existing compatible cargo already meeting the departure minimum can leave immediately. Otherwise the evaluator selects the largest additional quantity within the shipment target and capacity. A purchase must also fit stock, exact total cost, marginal limit, account headroom, cash reserve, and the per-order expenditure cap. The minimum is a departure rule; destination sales may complete partially.

The policy never moves private goods remotely. Its source and destination facilities must belong to the ship's company and be at the configured endpoint. Every transfer uses the ordinary ownership/docking/capacity operation and carries its original cost-basis slices. It moves no cash and spends no day. Market endpoints use the same price calculator and execution path as manual trade; private endpoints require no public quote.

### Stage, waits, and recovery

The ship retains one authoritative stage:

1. Acquire outbound goods at the origin, then depart.
2. Deliver outbound goods at the destination.
3. Acquire return goods, or take an allowed empty return, then depart.
4. Deliver return goods at the origin, then repeat.

The stage changes inside the same atomic commit as the corresponding goods movement. A voyage retains its delivery stage; it does not infer direction from commodity identity. This supports same-good return legs without accidentally selling new return stock at the wrong port. Cargo incompatible with the active leg pauses the policy intact rather than being discarded or guessed into another shipment.

A blocked or partial sale leaves unsold cargo aboard and prevents return acquisition. A full destination warehouse likewise unloads only its available capacity and retains the rest. Waiting reasons identify stock thresholds/reserves, marginal limits, cash reserves, expenditure caps, finite liquidity, storage, account headroom, or incompatible location/cargo. A return leg with no qualifying shipment can depart empty only when explicitly allowed and the hold is empty.

Dockside execution reevaluates after each staged action and stops after **four actions**, a wait/pause, departure, or no progress. Acquiring goods and departing form one action; a completed delivery can proceed to return acquisition at the same economic time. Completing an empty return advances its stage once. A chain cannot cycle indefinitely because the next acquisition departs on a real voyage.

Daily ship actions still precede private procurement and production. A factory batch completed today becomes available to an explicitly requested dockside chain after that commit, or the next daily ship phase. The policy adds no clock, free travel, procurement discount, or early batch.

Applying a different industrial policy requires an empty ship docked at one of its two ports. This prevents existing cargo from being silently reassigned to another route stage. Pausing/resuming preserves stage and cargo; reapplying the same policy object does not reset either. To replace a route while goods are blocked aboard, pause automation and first sell or transfer them through ordinary legal operations. Moving the ship manually away from the stage's required port causes a clear pause until location or configuration is resolved.

Only one route mode is active per ship. Applying an industrial route replaces its public-market route; applying a public-market route clears the industrial policy. Configuration and enablement are passive. A changed configuration invalidates pending decisions even within the same day, and exceptional failure later in a chain or daily phase rolls back goods, basis, cash, stage, and time together.

### Reproducible industrial shipping comparison

Run from the repository root:

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --industrial-logistics
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --industrial-logistics --check
```

The [generated report](experiments/industrial-logistics-results.json) uses `EnterpriseScenario`: construct a Westhaven bakery for **300**, enable its standard local input procurement, physically sail the player ship from Northport to Westhaven, then send six Bread to Eastport whenever private stock reaches eight while retaining two. The marginal sale floor is **25**, and the return is empty. Initial company cash is **600**, cargo capacity is **10**, and the Westhaven–Eastport voyage takes **six days**. The observation ends on day **90**.

The baseline includes a separate manual world. After every automatic dockside chain, ordinary manual trade/transfer/departure commands reproduce it; both worlds then advance the same whole day. Every comparison checks physical inventory, cash, schedules, voyages, and full FIFO accounting. Revisions, route control/stage fields, and event grouping differ because manual commands commit separately. Automation receives no economic bonus.

| Policy/condition | Bread sold at Eastport | Final cash | Inventory purchases | Sale revenue | Released basis | Held basis | Blocked days |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Automatic, identical manual economy | 42 | 1,586 | 793 | 2,079 | 556 | 237 | 6 |
| Sale floor 1,000 | 0 | 100 | 200 | 0 | 0 | 200 | 82 |
| Bakery disabled | 0 | 300 | 0 | 0 | 0 | 0 | 88 |
| Eastport starts with zero cash | 0 | 100 | 200 | 0 | 0 | 200 | 82 |
| Bakery interval four days | 36 | 1,457 | 625 | 1,782 | 453 | 172 | 14 |
| Eastport bread demand every two days | 42 | 1,586 | 793 | 2,079 | 556 | 237 | 6 |

All cases pay the same **300 construction capital**, separately from inventory purchases. The zero-liquidity variant explicitly starts the whole region with less money; it never drains, replenishes, or repairs an existing run. The slower bakery changes only its configured production interval. Stronger demand changes only Eastport's bread-consumption interval from four to two days: baseline demand consumes **38 Bread** and misses **6**, while stronger demand consumes **40** and misses **50**. It does not increase sale revenue in this particular route because the baseline demand already clears each delivery before the next arrives. Actual consumption and unmet requests remain separate from goods sold into the market.

The baseline sells seven six-unit shipments and completes **30 private batches**, retaining **18 Bread and 3 Logs**. Realized material gross margin is **2,079 − 556 = 1,523**. Liquid cash changes by **986** after the separate construction expenditure and the **237** of acquisition basis still held. None of these numbers is labeled net profit: wages, operating costs, depreciation, and ship resale values are not simulated.

Both the restrictive-floor and cashless-market cases retain **six Bread aboard**. Their factories eventually reach the configured cash reserve instead of receiving rescue funding. Blocked days count distinct economic days with a wait/pause, including waiting for a qualifying production batch; sailing is excluded.

The manual baseline requires **28 economic commands** after the shared setup, compared with one industrial-route configuration: **27 prescribed inputs avoided**. This excludes day advancement, construction, input-policy setup, and player thought. It measures execution workload rather than proven usability. The report repeats the run, reverses equivalent configuration order, checks `tr-TR` culture, and reconciles every operation's cash, goods, released basis, and held basis.

A focused automated test additionally exercises useful return cargo: a bakery sends private Bread, buys Flour at the destination, physically returns, and unloads Flour into its own input storage while ordinary local procurement supplies Logs. No manual warehouse loading or unloading is needed during the repeated cycle. The historical two-port policy has no multi-stop instructions. The new itinerary below adds explicitly authored ordered work; strategy optimization and automatic recovery remain outside both policies.

## Ordered cargo itineraries

Task 2.3 adds immutable `CargoItineraryPolicy` version 1 for player ships in new v3 pooled-stock runs. It has **2–4 unique port stops**, **1–8 ordered commands per stop**, and **at most 24 commands total**. The last stop connects back to the first through a real sea route. Every port, connection, commodity and owned company-stock endpoint is validated before application. Existing NPC merchants keep their original two-port policies and finite budgets.

| Instruction | Physical and financial effect | Applicable limits |
| --- | --- | --- |
| Buy from public market | Market stock moves into the docked hold; company pays the market | Maximum marginal ask, company cash reserve, per-order expenditure cap |
| Sell to public market | Named cargo moves into the local market; finite market cash pays the company | Minimum marginal bid |
| Load from company town stock | Local owned stock moves into the docked hold; no payment | Quantity retained in that town's company stock |
| Unload to company town stock | Named cargo moves into the local owned store; no payment | Actual remaining company storage capacity |

Every instruction has a positive **maximum whole units per visit**, a positive **minimum whole quantity** (default one), and **Skip for this visit** or **Wait at this stop**. The maximum is an order cap, not a cargo target. Execution chooses the largest quantity satisfying current stock, space, exact prices, real funds and applicable limits. It executes that quantity once and advances even if less than the maximum. If less than the minimum is possible, Skip advances without a transaction; Wait retains that exact instruction without spending. Earlier completed orders are never repeated because a later one waits.

Only the named good is affected. Bread, Flour and Logs may remain aboard together within the combined hold capacity; unmentioned or unsold goods stay aboard. A full hold produces a specific capacity blocker. A partial sale is a completed instruction and can leave some of that good aboard for later explicitly configured work. Skipping a bad sale can therefore constrain later purchases. Automation never empties incompatible cargo, tops up stock or loans money.

Price limits constrain the worst marginal unit, not the first quote or execution average. A price limit of zero is unrestricted; a blank UI spending cap is unrestricted within the real account bounds. Purchase reserve and spending cap apply independently to each purchase. A warehouse reserve applies only to loading and can retain fractional local stock, while the transferred amount remains whole. Current remote quotes and source-stock previews are informational: they reserve nothing and can change before arrival. All paid actions and transfers reuse the ordinary preview, transaction and FIFO paths.

### Clock, cursor and safe edits

Each ship stores its exact stop index, next action index, pass/cycle, last committed decision and blocker, last quantity, enabled flag and daily operation budget. At most **eight operations per ship per economic day** include successful commands, intentional skips and departure. Eight commands at one stop can leave departure for the next day. Repeated **Run dockside actions**, save/load or pausing cannot reset the budget. A wait stops the chain; an all-blocked route cannot spin. Historical routes keep their original four-action chain limit.

Departure requires every stop instruction to have executed or intentionally skipped. It spends no day and starts the ordinary integer-duration voyage; only subsequent day advancement permits arrival. During travel the stored stop names the destination and the next action is zero. The displayed pass advances when leaving the last listed stop, so applying at a middle stop can start with a partial pass; it is not proof of a completed round trip. Ships retain the existing priority/ordinal-ID execution order, sharing company cash once. A later exception rolls back all earlier staged ship actions, cursor/budget changes, cash and FIFO together.

Opening the planner pauses playback. Drafting, selecting ships and inspecting fields spend no time or money and do not require an empty hold. **Apply draft** preserves cargo and leaves automation paused. Replacing a policy requires manual control, an actual dock included in the new plan, no partially executed old stop, and no itinerary operations already performed on the current day. A stop is replaceable before its first action or after all its commands; after work today, advance a day before replacing it. Reapplying identical policy content preserves progress. Only one market, industrial or cargo-itinerary policy can be active on a ship.

**Manual control · route actions paused** disables future route actions without changing the itinerary, voyage or cargo. Ordinary manual permissions then apply. To resume, the ship must be at the exact next stop or on its matching incoming voyage; manual travel elsewhere does not reset the cursor. A partially completed waiting stop must satisfy its remaining orders before replacement. Inspect the specific stock, funds, reserve or price blocker instead of assuming that pausing alone advances or cancels it.

### Plan and inspect in Unity

From **Company → Fleet**, choose the ship's **Route planner**; Harbor and Industry also link to that ship's planner. The live header shows actual dock/voyage, next stop/action, cargo capacity and the authoritative current reason. **Cargo and committed route state** expands the held commodities and used daily budget.

Each stop is a compact card. Choose its town, add/remove/reorder stops, and expand an action to choose its instruction, good, maximum, minimum, skip/wait and relevant financial/storage limits. Only that action's applicable fields are shown. Maximum/minimum fields accept whole units; coin and warehouse reserves accept up to four decimal places. Invalid fields remain a draft and prevent application. **Reload applied route** discards draft edits in favor of the actual policy. **Inspect harbor** and **Open current harbor** return to the existing Harbor trading surface; **Back** restores the previous screen or overlay.

**Draft Grain–Flour–Bread loop** resolves the current world's economic roles and creates this unapplied example:

| Stop | Ordered instructions |
| --- | --- |
| A — vulnerable coast with an owned Bakery | Unload up to 2 Flour; unload up to 2 Logs; load up to 4 Bread |
| B — agricultural food source | Sell up to 4 Bread; buy up to 4 Grain; buy up to 2 Logs |
| C — processing town | Sell up to 4 Grain; buy up to 2 Flour; sail back to A |

All example orders have minimum one and Skip on shortage. Purchases keep 100 company coins and cap each order at 200 coins. You must actually build the eligible Bakery and fund inputs and wages; the preset creates nothing. Starting empty at A is valid: its first absent-input/output orders skip, and later arrivals deliver real inputs. The ordinary ship phase precedes factory production, so newly supplied inputs can fund a batch later that day, while newly produced Bread becomes available for a subsequent visit or explicit dockside action.

### Exact continuation and older runs

`RunIdentity.ItineraryVersion` is independent of generator/content version 3, company storage rules 2, valuation version 1 and the objective draft. Existing APIs and older identities default to zero, omit the new fields and preserve historical comparison groups. New host v3 pooled starts explicitly select version 1 and append `/itinerary-v1`; changing a route never rerolls the economy or changes its identity.

Version-1 identities use save **schema 4**, even before assigning an itinerary. The complete policy and exact stop/action/pass, enabled state, last decision and day budget are authoritative saved state. Restoring mid-voyage or after two completed stop actions resumes the same next work without replaying earlier transactions. Validation rejects impossible cursors, routes, goods, ownership, missing company stores, conflicting policies, incompatible enabled voyages and invalid budgets before replacing the active host. Terminal saves remain frozen. Valid schema 1/2 and old pooled schema 3 retain their original fields, fingerprints, controls and economic meaning. Retry preserves the original version and begins its initial world; it does not load an applied later itinerary from a saved slot.

### Bounded ordinary-world witness

The focused headless test uses **seed 99, Merchant Company, StockOnly, Ascension 0, Sandbox, pooled storage and itinerary version 1**. It pays 300 coins for a Westhaven Bakery, sails from Southhaven to Westhaven and enables the unmodified example on day 3. After that, it submits only whole-day advances through day 60. The public processing industry and all three ordinary NPC routes remain active; there are no supplied test goods, subsidies or manual cargo moves.

The ship physically returns through Westhaven → Southhaven → Northport → Westhaven on days **17, 31, 45 and 59**. Seven funded Bakery batches produce **14 Bread** and pay **42 coins in wages**. The route buys/sells 16 Grain, buys 8 Flour and 8 Logs, unloads 8 of each input, loads 12 Bread and sells 8 Bread. Final owned stock is 6 Bread, 1 Flour and 1 Log.

| Day-60 measure | Coins |
| --- | ---: |
| Company cash | 1,548.5336 |
| Reference Company Value | 2,553.5336 |
| Inventory purchases / sale revenue | 209.6664 / 300.2 |
| Released material basis / held basis | 128.3332 / 81.3332 |
| Realized material gross margin | 171.8668 |
| Operating surplus after wages | 129.8668 |

Construction is separate capital expenditure. Reference Company Value is not realized income. All-account money remains **11,730 coins**, with zero Mint issuance and zero external inflow. Each operation reconciles physical production/consumption and all owners' cash, FIFO basis and quantities. Reversing equivalent configuration order reproduces identical output and final state. This is a bounded operational witness, not a claim of permanent profitability or an actual Unity interaction check.

```text
dotnet test tests/SaltAndLedger.Simulation.Tests/SaltAndLedger.Simulation.Tests.csproj -c Release --filter FullyQualifiedName~CargoItineraryTests --logger "console;verbosity=detailed"
```

The 25 focused tests passed, including scarce stock/cash, finite market liquidity, mixed/full holds, reserves, exact marginal caps, skip/wait, partial-order completion, repeated-call daily limits, three independent ships, manual economic equivalence, stale decisions and exceptional rollback. Actual Editor/player interaction, target-resolution screenshots and full integration checks are recorded separately in [Unity](UNITY.md) and [Playtest](PLAYTEST.md).
