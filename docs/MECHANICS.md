# Maintained mechanics specification

This page specifies implemented behavior through Task 1.3b that cannot be derived from content definitions alone. Update it with behavior changes. Generated reference pages describe only the canonical definitions and their relationships. Pricing formulas are implemented experimental hypotheses; accounting, deterministic execution, and atomicity are implementation contracts. The original single-location API remains available; the maritime rules below extend ownership and scheduling in a separate world type.

## Inventory

An inventory belongs to a validated content catalog. It contains one quantity for each known good; unspecified initial quantities default to zero. Quantities are nonnegative signed 32-bit integers, from 0 through 2,147,483,647 inclusive. Unknown goods and negative initial quantities are rejected.

These limits define an exact, bounded sample state representation. Maritime cargo additionally has the total item-count capacity specified below; the per-good bounds do not settle market depth or production balance. Reads do not mutate stock. A snapshot is detached and cannot be used to modify the inventory.

## One production command

`Production.TryRunBatch(inventory, buildingId, recipeId)` attempts exactly one complete batch:

1. Resolve the building and recipe, and require the recipe to belong to that building.
2. Check that all required inputs are available.
3. Calculate every resulting input and output stock using checked integer arithmetic.
4. Apply the complete change only when all checks succeed.

Success returns `true`. Insufficient inputs return `false`. Unknown identifiers, a wrong building/recipe association, and output overflow are errors. Every rejected or failed command leaves the inventory unchanged; there is no partial consumption or partial output.

A good may occur on both sides of a recipe. Input availability is checked against the starting inventory, so a command cannot use its own future output to meet an input requirement. The calculation then subtracts inputs before adding outputs and checks the final balance for overflow. A batch that consumes and restores the same quantity can therefore succeed at the maximum stock value.

The `buildingId` selects a canonical definition and validates recipe compatibility. This low-level command still requires no world or building instance and advances no clock. The world scheduler below calls this same command; it does not implement a second production algorithm. No inventory reservation, queue, or concurrent execution contract exists.

## Example

The canonical recipe `saw_planks` belongs to `sawmill`, consumes 2 `logs`, and produces 1 `planks` per successful command.

| Before | Command outcome | After |
| --- | --- | --- |
| 2 logs, 0 planks | Successful batch | 0 logs, 1 plank |
| 1 log, 0 planks | Rejected: insufficient logs | 1 log, 0 planks |
| 2 logs, 2,147,483,647 planks | Rejected: output overflow | 2 logs, 2,147,483,647 planks |

The console demo starts at 6 logs and 0 planks. Three commands succeed; a fourth returns `false`. It ends at 0 logs and 3 planks.

The ratio is demonstration data. It does not imply physical conservation units, an economic price relationship, or a balanced production chain.

## Single-location world state and time

`WorldState` owns one fixed location, one shared production inventory, a collection of building instances, an optional market/trader configuration, and a nonnegative signed 64-bit `Tick`. The default tick is 0. A successful `Step` advances it by exactly 1 with checked arithmetic; a step starting at 9,223,372,036,854,775,807 fails without mutation. An empty world or a world with no enabled buildings still advances when stepped.

A tick is one abstract economic step. It has no conversion to hours, days, real seconds, or Unity frames. The host explicitly calls `Step`; the simulation creates no thread, timer, background loop, or random values.

A `BuildingInstance` has its own stable `Id`, a canonical `BuildingId`, a canonical `RecipeId`, an integer `Priority` (default 0), and `ProductionEnabled` (default `true`). Several instances can reference the same building definition and recipe. World construction validates all references and recipe/building associations, even for disabled instances. Runtime location and instance IDs match `[a-z][a-z0-9_]*` in full using ASCII characters; instance IDs must be unique within the world. These are runtime identities, separate from the globally unique canonical definition IDs. No new content definition is created for an instance or the location.

The host supplies initial stock; unspecified goods start at zero. It may also supply a nonnegative initial tick. State is copied into the world. Callers cannot change its inventory or building flags through retained constructor inputs or snapshots. Building instances are immutable values; a configuration change replaces the relevant value inside the next world snapshot. There is no construction, demolition, runtime priority change, or recipe-switching command.

## Single-location finite market, ownership, and money

Supplying `market: new MarketConfiguration(...)` enables trading with one trader. The existing `WorldSnapshot.Inventory` is the market's stock: there is no second copy of those goods. Buildings consume and produce in that same stock. The trader has a separate inventory, stable runtime ID, and cash balance; production never touches the trader's inventory or either cash balance. Public-market ownership of production is a **prototype assumption**, not a permanent decision about player-owned factories.

Both cash balances use `long`, from 0 through 9,223,372,036,854,775,807. One unit is the smallest indivisible currency unit. The currency name and scale are experimental. Prices and accounting use no binary floating point. Executable transaction totals, debits, credits, and reported deltas obey checked integer bounds. Dynamic price calculations use wider exact integer intermediates before checking the public `long` total. Combined holdings need not fit in one `long`; each account and transaction must. The implementation does not add balances together to authorize transfers.

`MarketQuote(goodId, bid, ask)` defines prices from the trader's perspective:

| Direction | Goods move | Cash moves | Unit price |
| --- | --- | --- | --- |
| `Buy` | Market to trader | Trader to market | `Ask` |
| `Sell` | Trader to market | Market to trader | `Bid` |

Require `Ask >= Bid > 0`. This constructor retains the **fixed experimental accounting baseline**: neither stock, production, transactions, nor elapsed ticks change those unit prices. Its existing multiplication-first error precedence and the `--market` example remain unchanged.

`MarketQuote(goodId, pricing)` instead selects immutable inventory-sensitive parameters. Quote configurations are unique by canonical good ID and immutable for the world's lifetime. A known good without a quote cannot be traded. An empty quote collection is permitted but makes every good untradeable. Fixed quotes and dynamic parameters live in runtime configuration, not canonical good definitions or the content schema. They may differ between goods and scenarios.

Initial market stock uses the existing constructor argument; the configuration supplies initial market cash, trader ID, initial trader cash, and optional trader stock. Unspecified trader goods start at zero. Runtime trader IDs follow the same ASCII ID syntax. Configuration collections are copied and immutable; canonical references are resolved when constructing the world. No automatic stock or cash replenishment, external income, borrowing, fees, or consumption exists.

Without market configuration, `Trading` is null. Existing constructor calls and production-control calls retain their behavior, and `TradeAttempts` is empty. Submitting a trade to such a world is an error.

## Inventory-sensitive pricing hypotheses

`InventoryPricing(model, referencePrice, targetStock, sensitivity, minPrice, maxPrice, spread)` supports `InventoryPricingModel.Linear` and `InventoryPricingModel.Scarcity`. Both work through the same world and trade API as fixed prices.

| Parameter | Symbol | Contract and meaning |
| --- | --- | --- |
| `referencePrice` | `P` | Positive `long`; reference value at the target transition |
| `targetStock` | `T` | Positive `int`; inventory scale, not replenishment or a stock guarantee |
| `sensitivity` | `k` | Nonnegative `long`; size of the linear response to a target-sized stock change |
| `minPrice`, `maxPrice` | `L`, `U` | Positive `long` bounds with `L <= P <= U` |
| `spread` | `h` | Nonnegative `long` added to buying and subtracted from selling before each side is bounded |

All parameter values are exact integers. Negative sensitivity/spread, nonpositive target/prices, reversed bounds, a reference outside the bounds, and an unknown model enum are invalid. Zero sensitivity makes a constant curve; zero spread is allowed and permits cost-neutral reversals. The parameters are experiments, not canonical good values or selected final balance.

### Values belong to inventory transitions

Define transition `n` as the edge between market stocks `n` and `n - 1`, for `1 <= n <= int.MaxValue`. Both directions use that same edge, avoiding a buy/sell mismatch caused by evaluating two different stock boundaries.

The exact rational reference values are:

```text
K = max(1, floor(T / 4))
Linear:   R(n) = P + k * (T - n) / T
Scarcity: R(n) = P + k * (T - n) / T + 3 * k * max(K - n, 0) / K

Ask(n) = clamp(floor(R(n)) + h, L, U)
Bid(n) = clamp(floor(R(n)) - h, L, U)
```

The divisions in `R` are exact rational operations; sum the terms before taking the mathematical floor. Floor means rounding toward negative infinity, including when abundant stock makes the raw value negative. It is not C# truncation toward zero. `clamp(x, L, U)` limits the final side price to the inclusive bounds. This keeps every executable marginal price positive and representable as `long`, even when an intermediate reference value or spread operation exceeds that range.

The scarcity model equals the linear model at and above `K`. Below `K`, its additional term increases the slope of the scarcity response. The unrounded curve is continuous at the knee; integer prices still form steps, and the slope change can make late purchases much more expensive. For targets below 8, `K == 1` and no executable edge lies below the knee, so both dynamic models coincide. The knee fraction and coefficient 3 are explicit provisional hypotheses, not independently tunable hidden state.

The spread parameter is a per-side adjustment, not a promised observed bid/ask gap. Clamping each side can compress it, including to zero when both sides reach the same floor or ceiling. On the same edge `Ask(n) >= Bid(n) > 0`. Both side prices are nonincreasing as `n` increases. At a current stock `S`, the next purchase uses `Ask(S)`, while the next sale uses `Bid(S + 1)`; these are different adjacent edges.

### Quantity totals and performance

For a purchase of `Q` from market stock `S`, sum `Ask(n)` for edges `S, S - 1, ..., S - Q + 1`. For a sale, sum `Bid(n)` for edges `S + 1, S + 2, ..., S + Q`. Each unit is priced at its own transition; multiplying the initial marginal quote by `Q` is generally incorrect.

The calculator uses `BigInteger` intermediates, at most two affine curve segments, analytic intersections with price bounds, and a logarithmic Euclidean floor-sum calculation. It does not iterate once per unit, per stock level, or per distinct price. Requested quantities can reach `int.MaxValue`; the complete total must still fit a positive `long`, and an unrepresentable total throws `OverflowException`. Mathematical intermediates crossing `long` bounds are permitted; public prices, totals, balances, and quantities remain bounded.

The pure `InventoryPricing.PriceAtEdge(direction, upperStock)` and `Quote(direction, marketStock, quantity)` methods expose the same curve calculation for experiments. They know no trader stock or cash. A pure `Quote` purchase beyond market stock throws `ArgumentOutOfRangeException`; a sale beyond maximum market stock throws `OverflowException`. Use the world's `PreviewTrade` to obtain normal stock/funds rejection outcomes rather than calling this lower-level price calculator directly.

Reversing a purchase crosses the same edges at bids no greater than the asks paid. Any closed sequence returning market stock to its starting value crosses each edge equally often in both directions, so it cannot increase trader cash without an intervening economic change. Splitting a purchase or sale preserves its exact total when no other trade, production, or configuration change intervenes. Flooring each edge before summation and clamping both directions on that edge preserve these properties. These are accounting/no-arbitrage contracts for one local market. Transport between differently priced markets and intervening production can create profitable opportunities; these tests do not establish their balance.

## Read-only quotes and previews

`WorldSnapshot.GetMarketQuote(goodId)` derives a `MarketPrice` from that snapshot's inventory and immutable configuration. It exposes `GoodId`, `MarketStock`, and nullable marginal `Bid` and `Ask`. At zero market stock, `Ask` is null; at `int.MaxValue` market stock, `Bid` is null. These nulls describe impossible market stock movements. A displayed marginal price does not promise that the trader has goods/cash, that the market can pay, or that the receiving account has capacity.

`WorldSnapshot.PreviewTrade(command)` returns the same structured `TradeAttempt` calculation used by execution, without changing stock, balances, controls, or tick. It includes stock/funds rejection when appropriate and throws for invalid references, missing market configuration, or trade arithmetic failure just as execution does. In a preview, `Traded`, executed quantity/amount, and before/after balances describe what would happen, not a completed transfer. A successful preview matches a subsequent trade calculated against the same state. It evaluates one trade, not the complete tick: tick overflow or a later production failure can still abort `Step`. Earlier trades in a step can also change the state seen by a later trade, so a preview is not a reservation or a promise about a future step.

`Trading.Quotes` holds configurations, not cached current prices. `MarketQuote.IsFixed` identifies the baseline; `Pricing` is null for fixed quotes. The legacy `MarketQuote.Bid` and `Ask` properties are available for fixed configurations only and throw `InvalidOperationException` for dynamic ones. Use the snapshot quote API for current marginal prices. Historical snapshots continue to derive historical prices after the live world moves on.

Production consumes market logs and adds market planks. The committed snapshot derives prices from the resulting stock, so consuming logs can raise their quotes and producing planks can lower theirs, subject to rounding and bounds. Prices do not change solely because the tick advances. There is no separate price clock, asynchronous update, demand state, or mutable price cache.

## Trading commands and outcomes

`TradeCommand(direction, goodId, quantity)` requests a positive `int` quantity. Commands are immediate all-or-nothing requests for one step, with no partial fills, reservations, persistent orders, or queues.

After validating all command references, fixed-price trades retain the Task 1.2 order: compute `checked(quantity * unitPrice)`, check seller stock, check buyer cash, then check all resulting inventories and cash balances. A fixed total overflow therefore throws even if stock or cash would also be insufficient.

Dynamic trades check the seller's stock before pricing. An insufficient-stock request returns the appropriate rejection with no price calculation (`Price == null`). A fully stocked sale that would exceed the market's `int.MaxValue` stock throws before pricing, since no corresponding inventory transition exists. Otherwise calculate the complete marginal total, check buyer cash, and check all remaining receiver/account limits before applying anything. This explicit precedence change avoids quoting nonexistent dynamic transitions. For both modes, seller stock takes precedence over buyer cash, and exact stock/affordability are sufficient.

Each trade calculates both resulting inventories and both cash balances before applying any changes. Stock or cash insufficiency is a normal rejection; an unrepresentable total or receiver balance remains exceptional. A priced but unaffordable dynamic request retains its full proposed price breakdown even though it executes nothing.

| `TradeOutcome` | Meaning |
| --- | --- |
| `Traded` | Entire requested goods and money transfer succeeded |
| `InsufficientMarketInventory` | Market cannot supply the requested purchase |
| `InsufficientTraderInventory` | Trader cannot supply the requested sale |
| `InsufficientTraderFunds` | Trader cannot afford the purchase at ask |
| `InsufficientMarketFunds` | Market cannot afford the sale at bid |

The four insufficient-resource outcomes leave both participants unchanged for that attempt. Later commands still execute and the tick can commit. Exact stock and exact affordability are sufficient; balances may reach zero. A successful trade conserves the traded good's combined quantity and combined cash. Production is a separate recipe transformation and is not included in goods-conservation claims.

## Tick order and production controls

`Step(commands = null, trades = null)` performs these phases in order:

1. Fully enumerate and validate all production controls, then all trades. Null sequences mean no commands; null members, unknown references, or missing quotes reject the step before any operation runs.
2. Check that advancing the tick will not overflow.
3. Prepare private inventories, cash balances, and building state.
4. Apply controls in declared order. `SetProductionEnabledCommand(instanceId, enabled)` affects this same step; the last command for an instance wins.
5. Execute trades in declared order. Each sees all earlier successful trades in this step. Record successes and expected rejections.
6. Select enabled buildings and attempt one complete batch each using `Production.TryRunBatch`, in ascending `Priority` then ordinal instance `Id` order. Negative priorities run first. Each attempt sees inventory left by trades and earlier production, including earlier batches' outputs. Record success or resource shortage.
7. Construct the complete next snapshot and step result.
8. Commit that snapshot once. Its tick is the starting tick plus 1.

**Trading precedes production.** Sold logs can feed a sawmill in the same tick; bought logs are unavailable to that tick's production. Newly produced planks cannot be bought until the next tick. This phase order is an experimental rule, not a permanent commitment to the eventual time model. Reordering economically dependent trades can change outcomes: with no market logs and a cashless trader holding 2 logs, selling 2 before buying 1 can fund and stock the purchase; buying first is rejected.

The one-batch-per-enabled-instance rule is a temporary prototype scheduling rule, not a final production rate or capacity balance. Buildings are not retried later in the same tick if an earlier attempt had insufficient inputs. Disabled buildings produce no attempt record. Configuration commands do not produce separate events.

Building collection insertion order, dictionary enumeration order, content collection order, object creation order, and frame timing do not determine who runs first. Command sequence order is deliberately meaningful. Fixed priority and ID ordering can repeatedly favor one building when stock is scarce; there is no fairness rotation or automatic priority adjustment.

### Resource contention

With equal priorities, enabled instances `sawmill_a` and `sawmill_b`, and 2 logs / 0 planks:

| Attempt order | Instance | Outcome | Shared inventory afterward |
| --- | --- | --- | --- |
| 1 | `sawmill_a` | Produced | 0 logs, 1 plank |
| 2 | `sawmill_b` | Resource shortage | 0 logs, 1 plank |

The shortage is a normal completed attempt. The step commits and advances the clock. On the next tick, with no new stock, both enabled instances report shortage. Disabling `sawmill_a` at the start of a tick with 2 available logs instead lets `sawmill_b` produce in that tick.

## Atomic steps and errors

Command validation failures, command enumeration failures, invalid configuration, and arithmetic overflow are exceptional conditions. They throw and return no `StepResult`. A failed `Step` leaves the previous tick, both inventories, both cash balances, and every production flag unchanged, including earlier staged trades and production. The next valid call retries that same tick. For example, if a sale supplies logs and the first mill can add a plank but the second would overflow plank stock, neither the sale nor either batch commits. Controls and clock also remain unchanged.

| Invalid input or operation | Error contract |
| --- | --- |
| Null required argument or runtime ID | `ArgumentNullException` |
| Malformed runtime ID, duplicate instance/quote, null collection member, or recipe/building mismatch | `ArgumentException` |
| Unknown canonical reference, initial-stock good, command target, or missing quote | `KeyNotFoundException` |
| Negative initial stock/cash/tick; nonpositive trade quantity/bid; ask below bid; invalid direction/model enum; invalid dynamic bounds, target, sensitivity, or spread | `ArgumentOutOfRangeException` |
| Stock, cash, transaction total, or tick overflow | `OverflowException` |
| Trade/quote without market configuration, fixed `Bid`/`Ask` access on dynamic configuration, or recursive `Step` | `InvalidOperationException` |

Runtime ID syntax is checked when constructing the corresponding instance or command. World construction resolves canonical references; `Step` resolves command targets. An exception thrown by a caller's command enumerable propagates without committing world state.

The world stages private inventories, cash, and building state for each step, then replaces one immutable snapshot only after constructing the complete result. This small copying approach is intentional for atomicity and clarity; it is not a generalized transaction system or a performance claim. The API requires one caller making sequential calls; concurrent or reentrant mutation is unsupported. External side effects in a caller-provided command enumerable are outside the world's state contract.

## Step results and snapshots

A completed `StepResult` contains:

- `Tick`: the tick at which commands and production ran. The first default-world result has `Tick == 0`.
- `After`: the immutable world snapshot after the step; the first result has `After.Tick == 1`. It includes location ID, market/production inventory, building state, and optional `Trading` state (`MarketCash`, `TraderId`, `TraderCash`, `TraderInventory`, and fixed/dynamic quote configurations). Current marginal quotes are derived from this snapshot, including production's stock changes.
- `TradeAttempts`: attempts in declared execution order. Each includes `Direction`, `GoodId`, requested `Quantity`, explicit `Outcome`, and nullable `Price`. A price breakdown contains `Quantity`, `FirstMarginalPrice`, `LastMarginalPrice`, `TotalAmount`, and `IsUniform`. `ExecutedQuantity` and `ExecutedAmount` are zero on rejection and equal the completed request on success. `MarketInventoryChange`, `TraderInventoryChange`, `MarketCashChange`, and `TraderCashChange` expose before, after, and signed delta balances.
- `ProductionAttempts`: attempts in actual production order. Each has building instance, building definition, and recipe identifiers, an outcome of `Produced` or `ResourceShortage`, and its inventory changes.

Each production inventory change identifies the good and its `Before`, `After`, and signed `Delta` quantities for that attempt, ordered by ordinal good ID. Only changed balances are included in production change lists. A production shortage has no changes; a successful recipe with no net stock change still has a `Produced` outcome. A step may have no attempt records and still advance its tick.

For compatibility, `TradeAttempt.UnitPrice` is the first marginal execution price, or zero when `Price` is null; `TotalAmount` is the proposed total, or zero when unpriced. A zero placeholder is not a valid executable price. Existing fixed-price attempts are uniformly priced and preserve their original values. For dynamic orders, do not infer `UnitPrice * Quantity == TotalAmount` unless `Price.IsUniform` is true. Rejected attempts have zero actual changes; their proposed total, when available, is not a payment.

Snapshots, attempt lists, and change lists are read-only and detached from future state changes. Inventory entries and buildings have stable ordering in snapshots. Keeping an earlier result does not expose or mutate live world state. The world retains only its current snapshot, not an ever-growing event history; a host that wants diagnostic history must choose what to retain.

## Executable market example

Run `dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --market`. The example uses one sawmill, logs bid/ask 2/3, and planks bid/ask 4/5. Starting market stock is 2 logs, 0 planks, cash 20; the trader starts with 4 logs, 0 planks, cash 7. All quantities and prices are experimental scenario configuration.

| Starting tick | Trades in order, before production | Ending market (logs, planks, cash) | Ending trader (logs, planks, cash) |
| --- | --- | --- | --- |
| 0 | Buy 1 log; sell 3 logs; buy 1 plank rejected for market stock | 2, 1, 17 | 2, 0, 10 |
| 1 | Buy 1 plank; buy 2 logs rejected for trader funds; buy 3 logs rejected for market stock | 0, 1, 22 | 2, 1, 5 |
| 2 | Sell 2 logs; buy 1 existing plank; sold logs feed production | 0, 1, 23 | 0, 2, 4 |
| 3 | Buy 1 plank rejected for trader funds; sell 2 planks; buy 2 planks; production shortage | 0, 1, 25 | 0, 2, 2 |

The final tick is 4. Cash totals 27 throughout; three production batches transform the six initial combined logs into three combined planks. `MarketTests.DocumentedMarketScenarioMatchesEveryTick` verifies this table; the demo checks its final state and compares complete repeated/reordered-setup transcripts.

## Maritime world, ports, and cargo

`MaritimeWorldState` owns exactly two independently configured ports, one trader ID and cash balance, one ship, and one nonnegative `long` global tick. `PortConfiguration` supplies each port's runtime ID, initial market stock/cash, immutable fixed or dynamic pricing, and zero or more local buildings. Exactly two distinct port IDs are required. Building instance IDs are unique within each port; commands qualify a building by both port and instance ID. Runtime IDs use the existing ASCII syntax. Initial stock, cash, references, and recipe compatibility retain their established validation and numeric bounds.

The trader's only goods inventory is `Ship.Cargo`. There is no separate warehouse, per-port cargo, or per-port trader cash. `ShipConfiguration` supplies a stable ID, positive `int` cargo capacity, known initial dock, and optional nonnegative initial cargo. Each item of every canonical good consumes **one capacity unit**. The total across goods must fit the configured capacity; initial cargo exceeding it is invalid. Aggregate arithmetic must not wrap around, including when separate goods individually fit their `int` limits.

Docked ship snapshots have `DockedPortId` and no active voyage. Sailing snapshots instead have `OriginPortId`, `DestinationPortId`, and positive `RemainingTravelTicks`; the dock is absent. Arrival clears the active voyage and leaves zero remaining ticks. Cargo and trader cash remain unchanged by departure, ordinary travel, and arrival.

Each port has its own market stock, cash, and pricing. Local production consumes and produces only in that port's inventory. Buying transfers goods from that market to ship cargo and shared trader cash to that port; selling reverses those transfers. Production never consumes ship cargo. Trades conserve each good across both ports and the ship, and conserve all three cash accounts. Production is the only implemented goods transformation and uses the canonical recipe; no operation creates or destroys cash.

These ports are handcrafted runtime configuration, not generated canonical content. Market-owned production, item-count capacity, perfect information, one trader/ship, and travel without fees, upkeep, or risk are provisional assumptions. There is no automatic replenishment or consumption.

### Maritime trades, previews, and errors

`PortTradeCommand(portId, direction, goodId, quantity)` names its intended market explicitly. All trade commands in a step must target the ship's **starting dock**. A known remote port or any trade while sailing throws `InvalidOperationException`; an unknown port/good/quote throws `KeyNotFoundException`. Commands cannot trade at the departure destination or use an arrival from later in the same step. There are no partial fills or remote transfers.

The shared transaction calculation retains existing seller-stock and funds outcomes and adds `TradeOutcome.InsufficientCargoCapacity` for a purchase exceeding remaining total cargo space. A rejected purchase changes no part of cargo, market stock, or cash, but other valid commands and the tick may continue. Selling uses the available ship goods and the selected port's cash. Earlier declared sales can free capacity or earn cash for a later purchase in the same step.

For fixed pricing, the checked total is calculated first, preserving the baseline's overflow precedence. Seller stock is checked next, then purchase capacity, then buyer funds and checked receiving balances. Dynamic pricing checks seller stock and purchase capacity before calculating the exact inventory-edge total, then checks funds and receiving balances. Consequently a dynamic capacity rejection is unpriced, while a fixed rejection can retain its proposed total. Impossible dynamic sale stock beyond `int.MaxValue` remains exceptional. Receiver stock/cash or transaction overflow throws; the whole step rolls back. No per-unit loop or duplicate pricing formula is introduced.

`MaritimeWorldSnapshot.GetMarketQuote(portId, goodId)` permits remote quote inspection from that snapshot's local stock. `PreviewTrade(portId, TradeCommand)` likewise permits hypothetical remote previews, even in transit. It calculates the economic outcome **as if the current ship were docked at that port**, using current cargo, free capacity, shared cash, and local accounts/prices; it does not check or change location. Its `Traded` or executed fields remain hypothetical, as in the single-location preview. Execution independently enforces the actual starting dock. A preview does not reserve inventory, advance time, guarantee arrival-time prices, or cover later production/tick failures.

### Departure and arrival timing

One positive integer `travelTicks` defines both directions of the sole connection. `SailCommand(destinationPortId)` requires a docked ship and the other known port. Sailing again while underway or selecting the current dock throws `InvalidOperationException`; an unknown destination throws `KeyNotFoundException`. More than one sailing command in a step throws `ArgumentException`. A docked ship with no departure stays docked.

Trading operates at the starting dock, then departure is applied. Travel progresses exactly once at the end of each successful tick, **including the departure tick**. Arrival becomes effective in the committed snapshot when the remaining time reaches zero. Destination trading begins on the next call to `Step`.

| Starting tick for a two-tick route | Commands | Committed ship state |
| --- | --- | --- |
| 0 | Buy at Northport and depart for Southhaven | Sailing, 1 travel tick remaining |
| 1 | No trades or departure | Docked at Southhaven |
| 2 | Sell at Southhaven | Sale can execute using Southhaven's current stock and prices |

A one-tick route arrives at the end of the departure step and still cannot trade at its destination during that step. The attempted illegal trade in the [shipping demonstration](SHIPPING_DEMO.md) throws without consuming time, after which a valid empty step completes the journey.

### One atomic maritime tick

`Step(commands = null, trades = null, sailCommands = null)` has these phases:

1. Fully enumerate and validate every production control, trade, and sailing command, including references and starting-position legality.
2. Check tick overflow and prepare private working ports, buildings, cargo, shared cash, and ship state.
3. Apply port-qualified production controls in declared order; the last control for an instance wins.
4. Execute manual trades in declared order at the starting dock.
5. Apply the valid departure if supplied.
6. Run production by **ordinal port ID**, then ascending building priority and ordinal instance ID; each enabled instance attempts one batch.
7. Advance travel once and resolve arrival.
8. Construct detached immutable snapshots and all structured results.
9. Commit the entire world with one state assignment.

Sold logs may feed that port's sawmill in the same tick; output produced later cannot be bought until the next tick. All ports produce under the same clock, regardless of ship location. Remote ports do not have independent ticks or loops. Expected stock/funds/capacity rejection records an attempt and permits the tick to complete.

Production controls are host-level scenario commands and may target either port, including while the ship is sailing. They do not represent player factory ownership or physical ship interaction. Trading and departure retain their separate location restrictions.

Any exceptional failure preserves **all ports, all building controls, shared trader cash, ship cargo, dock/voyage state, and global tick**, including earlier staged operations. A later production overflow in the other port therefore cancels an earlier trade and departure. Failed command enumeration, invalid later commands, total/balance overflow, reentrancy, and tick overflow also leave the prior snapshot intact. The next valid call retries that tick. Calls require one sequential host; concurrent mutation is unsupported.

### Maritime results and snapshots

`MaritimeStepResult` reports the starting `Tick`, immutable `After` world snapshot, ordered `TradeAttempts` with `PortId` and the existing `TradeAttempt`, and ordered `ProductionAttempts` with `PortId` and the existing `ProductionAttempt`. Their price, executed quantity, cash, and inventory-change semantics are unchanged. Trader inventory changes in a maritime trade refer to ship cargo.

`MovementEvents` contain ordered `Departure`, `Progress`, and `Arrival` records as applicable, identifying ship, origin, destination, and before/after remaining travel ticks. A new departure records the configured voyage; progress records its one decrement; an arrival records completion. A one-tick journey emits all three. A stationary docked tick emits none. These records describe only a committed step and are not retained as an unbounded history in the world.

`MaritimeWorldSnapshot.Ports` is ordered by ordinal port ID. Each port exposes detached inventory, market cash, quote configurations, and immutable building controls. `TraderId` and `TraderCash` occur once; `Ship` exposes detached cargo, total `CargoUnits`, capacity, and location/voyage. Retained snapshots preserve historical quotes, cargo, and voyage state after later trades or sailing; movement events belong to the step result. Constructor collections and snapshots provide no mutation path into live state.

## Configurable route policy

`TradeRoutePolicy(originPortId, destinationPortId, outbound, returnLeg = null, cashReserve = 0, allowEmptyReturn = false)` is immutable runtime configuration. `RouteLeg(goodId, maxPurchaseQuantity, minShipmentQuantity, maxBuyPrice, minSellPrice)` selects one commodity, a total source-loading target, a departure minimum, and marginal price bounds. Both quantities are positive `int`, minimum cannot exceed target, both prices are positive `long`, and reserve is nonnegative `long`. IDs use the established runtime syntax. Ports must differ; an optional return commodity must differ from the outbound one. Without a return leg, empty return must be explicitly allowed. No relation is required between buy and sell price limits: the player may deliberately accept a loss.

Evaluation resolves both ports and requires each configured commodity and its quote at both ports; unknown references throw `KeyNotFoundException`, including when disabled. Constructor errors remain exceptional. A valid configuration whose minimum exceeds this ship's capacity pauses with `MinimumExceedsCapacity`. Policies are authored runtime choices, not new canonical entities, and changing one does not mutate the world.

### Stage, action, and retained cargo

`RoutePolicyEvaluator.Evaluate(snapshot, policy, enabled = true)` is pure and stateless. It returns an immutable `AutomationDecision`: `Before` (the exact input snapshot), `Tick`, `Stage`, `Action`, `Reason`, optional `Trade`/`Departure` commands and successful hypothetical `Preview`, and `SelectedQuantity` (zero without a trade). Ship position is available through `Before.Ship`. No world account, inventory, clock, or mutable route phase is owned by the evaluator.

The evaluation order is reference validation, disabled state, minimum/capacity compatibility, unexpected/mixed cargo, then physical stage. `Pause/Disabled`, `Pause/MinimumExceedsCapacity`, `Pause/UnexpectedCargo`, and `Pause/MixedCargo` use stage `Suspended` and no commands. Unexpected means a positive amount of a non-policy good; mixed means positive outbound and return cargo. These pauses require host intervention or enablement/configuration changes; the evaluator never discards goods to resolve them.

| Committed state | Stage and next action |
| --- | --- |
| Sailing with compatible cargo | `InTransit`: `ContinueSailing/Sailing`, no commands |
| At origin with return cargo | `ReturnSale`: sell the largest eligible amount |
| At origin without return cargo | `OutboundLoading`: load outbound goods and depart when a useful shipment is ready |
| At destination with outbound cargo | `OutboundSale`: sell the largest eligible amount |
| At destination without outbound cargo | `ReturnLoading`: load return goods, or return empty only as configured |

At most one trade and one optional departure are produced per evaluation. Sales never depart in the same tick; return loading waits until a subsequent snapshot has no outbound cargo. Sales can be partial and have no minimum shipment requirement. Unsold cargo remains aboard and prevents loading the other commodity or sailing away. Existing source cargo of the intended good counts toward the loading target: buy up to `maxPurchaseQuantity - carried`, bounded by stock and free capacity. If carried plus the largest feasible purchase meets the minimum, return `BuyAndDepart/PurchaseReady`. Do not buy subminimum increments. If no qualifying purchase is possible but existing cargo already meets the minimum, return `Depart/ShipmentReady`, including cargo already above the target.

At the destination, `allowEmptyReturn` permits `Depart/EmptyReturnAllowed` only when cargo is entirely empty and either no return leg exists or its loading decision would wait. It does not override a pause, ignore unsold cargo, or authorize empty outbound departure. Enabling this choice can deliberately bypass waiting for return stock/prices/funds; it is not a prediction that waiting would be unprofitable.

### Bounded quantity selection and wait reasons

For purchases, the upper bound is the minimum of remaining loading target, source inventory, and free capacity. Purchase budget is `max(0, traderCash - cashReserve)`; reserve constrains new purchases without blocking sales or already-loaded departure. For sales, the bound is cargo of that good and `int.MaxValue - marketStock`, with market cash as the budget. The loading maximum/minimum do not cap sales.

Select the largest feasible quantity by monotone binary search, with at most 31 probes for the public `int` range. Require the final marginal ask to be at most the buy limit, or the final marginal bid to be at least the sell limit. Existing curves make that final unit the worst price in the order. Fixed quotes obey the same test. Reuse the existing `PriceAtEdge` and shared exact total calculation; there is no per-unit probing or first-price-times-quantity approximation. Search probes compare exact `BigInteger` totals against the bounded budget before converting to a public total. Thus a huge unaffordable search probe can shrink normally without overflowing the public trade total. Once selected, the quantity must pass the existing `MaritimeWorldSnapshot.PreviewTrade` with `Traded` and the same exact complete amount.

Receiver cash overflow and unexpected arithmetic/configuration failures are exceptional. They are not swallowed, treated as waits, or automatically retried with a smaller quantity. A full receiving market inventory is an ordinary `MarketInventoryFull` wait because no positive sale quantity exists. This selection contract changes no manual trade's overflow/rejection precedence.

When source cargo is below the minimum and no qualifying purchase exists, reason precedence is `InsufficientInventory`, then `BuyPriceLimit`, then `CashReserve` if total trader cash could fund the needed minimum but the reserve prevents it, otherwise `InsufficientTraderFunds`. Reasons describe what prevents a useful shipment, not why every additional unit fails. At a sale with zero eligible quantity, precedence is `MarketInventoryFull`, then `SellPriceLimit`, then `InsufficientMarketFunds`. Waiting emits no trade or sailing command. A later evaluation reassesses current stock, cash, and prices; no outcome promises future recovery in a finite economy.

### Safe execution and bounded host observation

`TradeRouteAutomation.Execute(world, decision)` rejects a decision unless `ReferenceEquals(world.Snapshot(), decision.Before)`. It freshly previews any proposed trade and checks successful quantity/amount agreement, then submits only the decision's normal commands to `MaritimeWorldState.Step`. There is no intervening callback, unrelated manual command, or alternate economic execution path. `Advance(world, policy, enabled = true)` performs evaluation and execution consecutively, returning `AutomationStep { Decision, Result }`. One sequential host is required; this does not create a concurrent access guarantee.

Buy-and-depart uses a successful preview of the authoritative starting state. A stale decision cannot let a rejected purchase accidentally initiate travel. The ordinary step still performs full validation, trade, departure, production, travel, and one commit; a later overflow rolls everything back. Manual behavior remains unchanged, including its permission to sail after an ordinary rejected trade. Evaluation/no-op decisions alone mutate nothing. Explicit execution of a wait or pause advances an ordinary empty tick; it does not freeze production or existing movement.

The host owns enablement and which immutable policy to use next. It may disable automated calls, step manually, and replace policies between steps without changing current or historical state. Discard pending decisions on replacement: a decision intentionally retains its earlier chosen commands. No same-tick mixing of manual and automatic orders is supported by the execution wrapper.

`AutomationRunner.Run(world, policy, maxTicks, maxNoProgressTicks, observer = null, enabled = true)` is an optional synchronous host helper; both limits must be positive `int`. It evaluates from each committed snapshot and executes one normal step. `Pause` stops before executing and reports `Disabled` or `Paused`. Otherwise, successful trades, movement events, or changed production inventory reset its consecutive no-progress count; tick changes alone do not. It stops with `NoProgress` when the idle limit is reached, otherwise `TickLimit` at the step bound. If both bounds are reached in one step, `NoProgress` takes precedence. The finite tick bound still applies when unrelated production keeps resetting the idle count.

`AutomationRunResult` contains `StopReason`, `StepsExecuted`, `ConsecutiveNoProgressTicks`, `Before`, `After`, and nullable `LastDecision`. It retains no complete history and owns no continuing route stage. A stopped observation is not a world pause or proof of permanent deadlock. Restarting the helper reassesses the current world with fresh host counters. An optional observer runs after each committed step, must not mutate the world, and owns any retained diagnostics. Observer errors propagate after commit, and observed mutation is rejected after it has occurred; the callback is outside the world's atomicity contract. A multi-tick run is not one transaction.

The [automation guide](AUTOMATION.md) records the executable full-load, restrictive-price, and liquidity-blocked comparisons. Equivalent manual and automated itineraries use the same six ticks and produce the same accounts and inventory; the automated route receives no hidden economic or movement advantage.

## TradingLab interaction rules

Task 1.4 exposes the existing maritime world in a Unity UI Toolkit scene. It adds presentation and host choices, without changing the economic rules above. The [playtest guide](PLAYTEST.md) gives the controls and reproducible expeditions.

Selecting a port or good, changing a quantity, obtaining a preview, editing policy fields, applying a policy or preset, and toggling automation consume **no tick**. The scenario selector is also passive; **Reset scenario** explicitly constructs a new starting world, clears the recent activity, and disables automation. It is a testing operation, not an in-world injection of goods or money.

**Next tick** executes one ordinary step, using the active policy when automation is enabled and no commands otherwise. **Advance 3 ticks** uses up to three ordinary steps. Automation has explicit one-tick and **Run up to 6 ticks** controls; the bounded run also stops after three consecutive ticks without trade, movement, or changed production, or before a policy pause. There is no background advancement. A runner stop does not disable the policy or prove permanent economic deadlock.

Manual buy, sell, sail, and combined buy/depart each consume one tick when executed. Manual actions require automation to be disabled; trades require the selected port to be the starting dock. The interface validates positive integer quantities before constructing commands. Remote or sailing previews remain available as hypothetical economic inspections, with actual execution unavailable. Normal stock, cash, and capacity rejection is explained separately from malformed input or unexpected simulation errors. A combined buy/depart refreshes its preview against the current snapshot and refuses the action if the purchase is not executable, so it cannot depart empty after a rejected purchase. Ordinary simulation command semantics remain unchanged.

The route form edits only outbound log limits and the cash reserve. Applying a valid form constructs a new immutable policy and discards the previous decision; editing alone does not apply it. The fixed return leg targets/minimums two planks, with maximum marginal buy price 18 and minimum marginal sell price 31. The full and restrictive presets differ only in the outbound buy ceiling, 10 versus 8. The current decision shows the stage, action, readable reason, quantity, exact preview amount, and departure intent. The ship indicator uses committed dock/voyage fields; arrival changes the displayed dock and enables destination trading on the next action.

The host retains at most 40 recent activity entries derived from structured results, with newest entries first. This bounded display does not add simulation history or persistence. Trader cash and its change since reset are shown separately from cargo. Realized gross trading profit is reported only when a completed empty-cargo comparison permits purchase costs and sale revenue to be compared; unsold cargo is shown without speculative valuation or a realized-loss label. No policy change creates liquidity, restocks a market, discards cargo, or otherwise repairs the world.

## Determinism and remaining scope

The same validated content, initial world state, and per-tick ordered commands produce the same states and structured outcomes. Reversing equivalent input building collections does not change results. Callers advance several ticks by calling `Step` repeatedly; the optional bounded automation helper issues those same sequential calls without a separate clock.

Equivalent quote, stock, content, and building collection order does not change outcomes. Trade sequence order deliberately does. Ordinal IDs and integer arithmetic avoid culture-dependent financial behavior; the market demo formats numbers invariantly.

The maritime demo additionally compares repeated and reversed-port input runs, uses invariant numeric formatting, and asserts its documented transaction totals, balances, six-tick itinerary, and empty final cargo. Local production order is independent of port configuration insertion order. Remote quote inspection changes no state.

Fixed, linear, and scarcity pricing are compared in [Pricing experiments](PRICING_EXPERIMENTS.md); manual two-port decisions are compared in [Manual shipping comparison](SHIPPING_DEMO.md), with configured policies compared in [Automation](AUTOMATION.md). The dynamic formulas and scenario parameters remain provisional. There is no automatic replenishment, production duration, cooldown, rate per minute, operating cost, wage, energy, or consumption. Production and travel advance only because the host steps the world. Consumption phases remain unresolved. Deterministic step results are useful for comparison; they are not a save format, durable replay protocol, event bus, or persistent event-sourcing system.
