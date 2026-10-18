# Configurable trade-route automation

Automation repeats the transactions you configure. You still choose the route, commodities, shipment sizes, acceptable prices, and cash reserve. It does not search for the best strategy or promise a profitable destination. A route can wait indefinitely when finite markets cannot satisfy your rules.

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

## Limits and next questions

This first policy supports one ship, two ports, and distinct outbound/return goods. It cannot forecast destination prices, reserve future market liquidity, discover commodities, sell mixed cargo intelligently, or guarantee sustained profit. Numeric overflow remains an explicit error. Finite markets can permanently strand goods or cash; the policy does not repair the economy. There is no route editor, background thread, extra canonical content, persistent policy stage, save format, or unlimited decision history.

The verified arithmetic comparisons establish mechanical equivalence and a meaningful threshold choice. They do not establish enjoyable automation, a sustainable economy, or optimal strategy. The next proposed milestone is a focused policy-control and recovery experiment: compare several finite-market starting conditions and show whether players can understand a wait, pause, change limits, and recover using the existing commands. Define success and recovery criteria before adding economic systems or a graphical editor. See the [roadmap](ROADMAP.md#recommended-next-milestone-policy-control-and-recovery) and the actual [Unity verification scope](UNITY.md).
