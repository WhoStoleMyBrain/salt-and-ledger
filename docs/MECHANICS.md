# Maintained mechanics specification

This page specifies implemented behavior, preserving historical contracts and explicit later ruleset boundaries that cannot be derived from content definitions alone. Generated reference pages describe canonical definitions and relationships. Pricing formulas and scenario values are experimental hypotheses; accounting, determinism, and atomicity are implementation contracts. The single-location and two-port maritime rules remain unchanged. The [regional rules](#regional-world-and-instant-commands) add living-economy days, company/private ownership, a fixed challenge, and zero-time actions through a separate owner.

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

A good may occur on both sides of a transformation recipe. Input availability is checked against the starting inventory, so a command cannot use its own future output to meet an input requirement. The calculation then subtracts inputs before adding outputs and checks the final balance for overflow. A batch that consumes and restores the same quantity can therefore succeed at the maximum stock value. An explicitly declared extraction recipe has no inputs and creates its configured outputs through this same checked operation. Its scheduled rate and local output ceiling are regional configuration, not hidden behavior of this low-level command.

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

## Regional world and instant commands

Task 1.5 introduces `RegionalWorldState` without changing `WorldState.Step`, `MaritimeWorldState.Step`, or TradingLab. Task 1.6 extends it with company accounts and private facilities; Task 1.7 adds historical material costs, industrial route stages, and complete runs. It owns ordered ports, one finite cash account per company, capacity-limited ship cargo, public/private facilities, consumption schedules, undirected routes, optional challenge progress, one nonnegative `long Day`, and a checked nonnegative `long Revision`. Explicitly configured companies may own multiple ships; the legacy constructor still infers a distinct account per ship. Collections use ordinal IDs; input collection order has no economic effect. A region has at least two ports and one ship.

`ExecuteTrade(shipId, portId, command)` and `Depart(shipId, destinationPortId)` execute instantaneously. They do not run any daily phases. Trades require the actual dock and use the same `TradeCalculator` as legacy execution and previews. Current `PreviewTrade` is hypothetical when inspecting a remote port; it grants no execution permission. An actor with enabled automation cannot receive manual trade/departure orders.

`ConfigureAutomation`, `SetAutomationEnabled`, and `SetProductionEnabled` are also zero-time operations. They replace configuration/control state without changing stocks, cash, travel progress, or day. Every operation stages state privately, constructs its result, and commits once. A changed operation increments revision by exactly one. A rejected stock/funds/capacity trade or unchanged enablement preserves the exact snapshot identity and revision. Exceptions, including revision overflow, preserve every prior field.

`EvaluateAutomation` is pure. `ExecuteAutomation` requires the exact current source snapshot and freshly validates its selected trade. Any intervening same-day trade or configuration commit invalidates that decision. A numerical day match is insufficient. Single-threaded host serialization remains required; no lock-free or concurrent mutation contract exists.

### Regional daily order

`AdvanceDay()` stages one complete day, with no callbacks or intermediate public commits:

1. Check day/revision bounds.
2. Run enabled actors by ascending `Priority`, then ordinal ship ID, using at most four policy actions per actor.
3. Procure private factory inputs by ordinal port ID, facility ID, and input good ID.
4. Process due public facilities by ordinal port ID, ascending facility priority, then ordinal facility ID.
5. Process due private facilities by ordinal port ID and facility ID.
6. Sell configured private output surplus by ordinal port ID, facility ID, and good ID.
7. Process due consumption by ordinal port ID: historical rules use ordinal rule ID; living rules use essential, luxury, then municipal kind and ordinal rule ID.
8. For living ports, observe food outcomes, update prosperity/luxury counters, review bounded population and derive the next day's immutable coverage pricing parameters.
9. Progress every voyage once, by ordinal ship ID, and resolve arrivals.
10. Complete due private construction by ordinal port ID and facility ID. Pending sites have skipped all automatic facility phases above, including this completion day's phases.
11. Evaluate the optional challenge against the completing day and final company cash.
12. Construct structured results and the immutable snapshot, then commit `Day + 1` and `Revision + 1`.

Every actor observes earlier actors' staged transactions. Historical NPC priorities 0 and 1, and the expanded scenario's grain merchant priority 2, precede the player's 10. A player instant command committed before this day naturally precedes the scheduled actors. UI selection and rendering order never alter precedence. Worlds without private facilities or a challenge retain the historical phase outcomes.

Production and consumption evaluate the completing day number. The first three-day extraction opportunity is the day-3 commit. Sold inputs from the actor phase can be processed that day. Produced output can satisfy consumption later in that day; it becomes externally tradeable after commit. Daily automation cannot buy output that has not yet been produced.

Exceptional failure rolls back all staged actor commands, cash, goods, schedules, construction state, controls, consumption counters, movement, day, and revision. Normal policy waits and ordinary trade shortages do not abort a day. The world retains no unbounded event history.

### Scheduled production and physical demand

`ScheduledFacilityConfiguration` references a normal canonical `BuildingInstance`, positive `IntervalDays`, optional nonnegative `OutputStockLimit`, and positive `FirstDueDay` (default interval). Its next due day, enablement, and last outcome live in the immutable snapshot. Facilities configured through a port must match its market building instances.

Only a due, enabled facility attempts a batch. Missing inputs return `InputShortage`. A whole batch that would exceed an output good's local ceiling returns `OutputLimit`; the check accounts for any same-good input subtraction. It does not truncate output or consume inputs. Otherwise the existing `Production.TryRunBatch` performs its usual checked atomic transformation or extraction.

Success schedules `NextDueDay = completingDay + IntervalDays`. Disabled, input-short, or output-limited facilities keep their deadline and retry on later daily advances. Reenabling preserves that deadline. There is no production during a toggle, no catch-up burst, and no reset that manufactures a free batch. Not-yet-due facilities retain their last completed outcome; the next due day explains their current schedule.

`ConsumptionConfiguration` defines a stable rule ID, canonical good, positive quantity/interval, first due day, and optional kind/population scale. The default `Legacy` kind preserves historical behavior: a due rule removes the whole quantity only if available; otherwise it removes zero and records the full request as unmet. The next opportunity is completing day + interval even after shortage. `LastUnmetQuantity` and checked cumulative `TotalUnmetQuantity` expose the result; unmet demand is not an inventory debt. An initially overdue schedule likewise attempts only once, with no catch-up loop. Living kinds and partial paid consumption are specified below.

Legacy consumption pays no money and models physical use only. Living household consumption pays the market; municipal own-use remains unpaid. Extraction is the explicit source, transformation consumes/produces canonical recipe quantities, and consumption is the explicit sink. Trades and cargo transport transfer ownership/location. Reconcile goods separately from monetary transfers; there is no monetary source or sink.

### Regional routes and arrival

`SeaRoute` is a direct, undirected positive integer distance between distinct existing ports. Duplicate undirected pairs are invalid. Each ship has a positive integer speed; duration is integer ceiling of distance/speed, at least one day. Classic uses its authored distances. Seeded generation derives integer distances from integer geographic coordinates once during setup; rendering and floating-point screen coordinates never enter travel calculations. No pathfinding or automatic intermediate voyage exists.

Departure changes docked state to a voyage with its complete duration still remaining. It consumes no day. Each subsequent `AdvanceDay` decrements remaining duration once. A three-day departure on day 0 arrives in the day-3 snapshot. Manual trading is legal immediately after that commit, still on day 3. Daily automation acts before arrival and therefore trades on the following daily actor phase unless the host explicitly requests a zero-time run after arrival.

All ships share these rules. Each movement result identifies ship, origin, destination, event kind, and remaining days. Snapshot dock counts derive from actual docked ship states. No second scene-owned occupancy list controls economic access.

### Regional policy execution

Regional policies reuse `TradeRoutePolicy`, `RouteLeg`, and the existing evaluator through a two-port snapshot projection for the selected actor. The projection references the same authoritative market snapshots and that ship's cargo/account; it is not a second world or an independently advancing market. Distinct outbound/return commodities, retained mixed/unexpected cargo pauses, exact binary quantity search, and price/funds/capacity constraints remain as specified above.

`RouteLeg.MaxPurchaseExpenditure` adds a positive per-order budget, default `long.MaxValue` for backward compatibility. Buying is bounded by both `cash - reserve` and this cap. `PurchaseExpenditureLimit` distinguishes a minimum shipment prevented by the cap. The cap does not reserve funds or accumulate spending across a voyage. Legacy policies using the default preserve their previous choices.

Each regional ship optionally has source-stock thresholds for outbound/return loading. A threshold prevents loading before the source reaches the configured amount, but does not prevent an already sufficient cargo from departing. NPC thresholds apply the same ordinary evaluator afterward; they cannot override payment or capacity.

`RunAutomation(shipId, maxActions)` accepts 1 through 4 actions. It privately reevaluates after each successful action and commits the complete zero-time chain once, incrementing revision once if changed. It stops on wait/pause, no trade or movement progress, sailing, or the action bound. A sale can therefore be followed by a return purchase/departure without arbitrary extra economic days. Partial unsold cargo is retained; a subsequent inability to sell ends the chain. A daily step uses this same bounded chain for each enabled actor and still commits only once for the entire day.

Configuration and enablement live in the regional snapshot, so changing them invalidates pending decisions. Applying a policy never executes it automatically. Explicitly executing an idle decision leaves the day and revision untouched. This differs intentionally from the legacy wrapper whose empty execution advances an abstract tick.

### ArchipelagoLab playback and observation

The new scene starts paused. Its main-thread host requests whole `AdvanceDay` operations at 1×, 3×, or 10×; the base interval is 1.5 real seconds per day. At most four days can be submitted in one frame. Excess stalled-frame time is capped rather than retained as an unlimited backlog. Pause, reset, and speed changes clear the accumulator. The same starting state and ordered commands over the same number of days have the same economic outcome at every playback speed.

Opening dockside trade, submitting a manual trade/order, running explicit dockside automation, or requesting Next day pauses playback. Port/ship selection, inspection, draft edits, and ordinary panel changes spend no day. Valid policy application/enablement can change revision but not economic quantities or time. Reset constructs a fresh scenario, clears activity, and restores paused 1× playback; it is not an in-world rescue.

Ship interpolation only draws movement between committed positions. Paused display settles at the current committed state. Actual market access, arrival timing, and dock counts always use snapshots. The UI keeps at most 60 recent structured activity entries. Cash changes and unsold cargo remain separate; the closed empty-cargo profit display requires completed player trading, empty ship cargo, and no private facilities. Task 1.7 private ownership additionally exposes released and held historical material basis and realized gross margin, separately from construction and unmodeled operating costs. The historical archipelago report additionally records intermediate closed intervals inside atomic policy chains.

## Company accounts and private facilities

The per-facility rules below describe historical `CompanyStorageRules.Legacy`. New v3 host runs select independently versioned `Pooled` storage: one company/town inventory and FIFO store; capacity equals all paid/granted local facility capacities including pending sites; operational same-recipe output ceilings sum. Policies and production use current shared stock in the existing ordinal order, while counters, wages and lifecycle remain per facility. Pending production timing is unchanged. The complete [shared storage contract](HARBOR.md#shared-stock-rules-version-2) defines contention, output headroom, policy reserves, transfers and valuation; old saves do not migrate.

`RegionalWorldSnapshot.Companies` owns one nonnegative checked `long` balance per company. Ships reference OwnerId; their Cash snapshot property projects this balance. Never sum ship Cash to reconcile a company with multiple ships. Explicit `CompanyConfiguration` requires zero InitialCash in each ship configuration; the legacy constructor infers one distinct company per ship from its existing initial balance.

`IndustryBlueprint` defines positive construction cost, interval and finite warehouse capacity, eligible ports, optional output ceiling, and canonical building/recipe IDs. A recipe must belong to that building. Private facility inventories use the same canonical goods and integer bounds as market/cargo inventories, but have distinct ownership and total item capacity.

`ConstructFacility` validates references, eligibility, unique facility ID and one-blueprint-per-owner/port. Insufficient company cash is an ordinary unchanged `InsufficientFunds` result. An accepted order pays the entire cost to the local market and creates empty enabled owned storage. It advances revision without spending a day or producing anything. Recipient balance, completion-date or first-production-date overflow throws and rolls back the entire order, including accounting.

### Ascension and construction lifecycle

`RunDifficulty` has exactly `Normal` (Ascension 0) and `SlowFoundations` (Ascension 1). A world fixes its validated difficulty at initialization; it cannot be changed during a run. Ascension 0 preserves the existing immediate `Built` outcome and first production at ordered day plus interval. Ascension 1 applies a two-day delay to purchased facilities owned by a company with a player ship. It returns `Started` at order, with `UnderConstruction` status. NPC-owned purchases remain ordinary immediate construction; the scenario adds no NPC building behavior.

The pending site exposes its ordered day, scheduled completion day, remaining days, earliest production day and immutable status. Its full cost is paid once at order. Two subsequent successful `AdvanceDay` operations complete a day-D order on the D+2 commit. Instant trades, policy edits, enablement and presentation do not count as construction days. Completion runs after voyages and before challenge evaluation in the same atomic daily operation. It returns `Completed`, changes the site to `Operational`, and emits a zero-cash `ConstructionCompleted` event. It neither pays again nor creates goods or cost basis.

Pending facilities perform no automatic procurement, private production or automatic output sales, including the automatic phases of the completion day. Their warehouse remains usable for explicit local market trades and physical owned transfers, under unchanged ownership/docking/capacity rules. Industrial shipping can likewise move genuinely stored goods through its ordinary transfer path; storage access grants no production exception. Procurement and sales can begin on the following daily operation if enabled. The first batch remains scheduled at completion plus the full normal interval, regardless of manually supplied inputs.

| Day of a bakery ordered on day 0 | Ascension 0 | Ascension 1 |
| --- | --- | --- |
| 0, order | Pays once; operational; first eligible batch day 2 | Pays once; pending for two days; first eligible batch day 4 |
| 1, first daily advance | Normal policies may act; no batch due | Pending; no automatic facility operation |
| 2, second advance | Batch may run if inputs/capacity permit | Completes after automatic phases; no batch or automatic facility trade |
| 3 | Ordinary schedule | Operational policies may act; no batch due |
| 4 | Ordinary schedule | First batch opportunity, still conditional on inputs, enablement and capacity |

`PreviewConstructionSchedule(ownerId, blueprintId)` calculates the same checked dates used by ordering, without reserving cash or establishing site eligibility. A starter facility is explicitly initialized `Operational`, with its historical initial schedule, grant provenance and zero construction expenditure. The Industrial Consortium therefore avoids the delay for its inherited bakery; buying another site still incurs it. This documented charter asymmetry is intentional, not a hidden economic compensation.

Private production consumes only the facility's inputs and creates only its outputs. Missing inputs, final output ceiling, final total capacity or insufficient configured payroll funding prevent the whole batch. Living wages are settled atomically for completed batches only. Success reuses the canonical atomic production operation and advances the deadline from completion. Blocked and disabled facilities remain due; enablement never creates a batch or resets the deadline. Checked lifetime batch/physical/purchase/sale counters are observations, not retained transaction history; counter overflow also rolls back the operation.

`ConfigureFacility` accepts immutable procurement/sales settings, nonnegative company reserve, unique input target/max-marginal-price rules, and unique output reserve/min-marginal-price rules. Rules must match the fixed recipe, with stock settings within warehouse capacity. No recipe switching exists. Enabled production and policies run without repeated manual action. Each daily policy phase attempts at most one selected transaction per configured good, in ordinal order. Input targets are separate ceilings, not reserved allocations of cash or storage; an earlier good/facility can use resources a later one needs.

`MaximumTradeQuantity` and `MaximumWarehouseTradeQuantity` return the largest currently executable quantity using bounded binary search over the existing exact price sums. Bounds include seller stock, destination stock/cargo headroom, payer funds and receiver cash headroom. Factory purchases add target quantity, worst marginal buy limit and company reserve; sales add retained output and worst marginal sell limit. Search probes compare exact totals before converting to public `long`. These helpers may choose a smaller executable order near numeric account limits; manual commands preserve their existing overflow exceptions, and legacy route selection remains unchanged.

Policy waits distinguish unavailable local stock, price limits, reserve/funds, full storage/market stock, and receiving cash-account limits. No phase creates stock or money to recover. A completed partial target purchase may leave more target stock to acquire on a future day; each selected transaction itself remains all-or-nothing.

### Local warehouse trades and physical transfers

`ExecuteWarehouseTrade(ownerId, facilityId, command, expectedSnapshot)` uses the same TradeCalculator and local public market as ship trading. Goods and cash really change ownership; company balance and market liquidity are finite. Its preview and maximum helper enforce ownership. Explicit management can trade while the owner's ship is elsewhere because the goods never leave that town. Purchased-stock expenditure records every warehouse purchase, not an attributed cost of consumed recipe inputs.

`TransferGoods` requires the named owner to own both ship and facility, the ship to be docked at that facility's port, and ship automation to be disabled. Quantity must be positive and direction/good valid. Insufficient source stock or destination capacity is an ordinary unchanged result; wrong owner, remote/sailing ship, invalid reference, or numeric failure throws with full rollback. Success moves the exact quantity, no cash, and no day. It increments revision once.

Ship and warehouse trade execution optionally accepts the exact source snapshot. A different snapshot is stale even on the same day and is rejected before execution. The UI uses this form. Snapshot previews remain read-only and hypothetical for remote ships; maximum ship quantity likewise grants no docking permission.

### Fixed supply challenge

The optional configuration names owner, target market/good, positive required net quantity, a future deadline day, and nonnegative minimum cash. Completed target-owner sales into the target market add executed quantity; its purchases subtract it. Both ship and warehouse trades share the same signed counter. Only the displayed delivered quantity is clamped at zero, so purchasing then reselling cannot mint credit. NPC trades, production, consumption, physical transfers, and arrival grant no credit.

Evaluation occurs after the complete staged operation. Success requires net quantity and final cash thresholds at or before the deadline. Otherwise day at or beyond the deadline fails. The final daily transaction can succeed through automatic delivery before the failure check. No intermediate success is committed if a later phase throws or the final state misses a condition. Success/failure records completion day and rejects further economic mutation; reads remain valid and host reset creates a new world. Sandbox has no challenge or deadline.

The [enterprise scenario](ENTERPRISE.md) defines the tested charter and provisional economic parameters. Supply delivered is not labeled household demand satisfied. Task 1.7 adds historical acquisition/input-material cost below. Speculative liquidation values remain absent; exact execution average is still display-only division of exact total by executable quantity.

## Historical inventory costs and committed events

Task 1.7 adds accounting to the same staged regional operation. `AccountingSnapshot` contains company aggregates and the open cost lots of `ship:<id>` and `facility:<id>` stores. Public market goods do not carry a company acquisition basis; the price actually paid when buying them creates one. Accounting observes actual successful changes through the existing economic operations and does not authorize a different price or transfer.

### FIFO and exact remainder allocation

FIFO is **receipt order within each store and commodity**. A purchase appends a new lot with its executed quantity and exact paid total. A transfer removes source FIFO slices and appends those preserved slices to the destination's queue, after stock already there. It does not sort the destination by original acquisition ID. A partial sale or production input withdrawal consumes the earliest available slices at that store.

For a lot with original quantity `Q`, original paid/material cost `C`, and the still-held interval `[s, s + q)`, its cost is:

```text
prefix(k) = floor(C * k / Q)
sliceCost(s, q) = prefix(s + q) - prefix(s)
```

`C` is nonnegative. Exact `BigInteger` intermediates evaluate the products/divisions; public costs and cumulative expenditure are checked `long` values. Lot and inventory quantities retain checked bounds. Transfer slices keep the original `Q`, `C`, start offset, ID, origin, and provenance. They are never re-averaged with a new denominator. Dividing, moving, and later selling a lot therefore releases exactly its original cost across the complete quantity.

Purchases, grants, and manufacture have distinct origins. A starter grant has zero **paid** basis and explicit charter/initial provenance; it is not claimed to have zero market value. Manufactured goods identify their recipe. A production event also records consumed slices, so the actual input provenance can be audited in the committed journal. Only still-open lots remain in current accounting state.

### Transformations and capital

A successful private batch consumes the recipe's **gross inputs**, even when a commodity also appears among the outputs. Their FIFO material costs are summed exactly. Output goods are sorted by ordinal good ID, then their units form one allocation interval. The batch's total input cost is apportioned across that complete output-unit interval with the same prefix-floor rule; each output good receives its contiguous portion. This deterministic rule assigns equal material cost per physical output unit before integer remainder allocation, not market-value weights.

Outputs are appended as manufactured lots. Full input material cost equals full output material cost. Extraction has no modeled material expenditure and hence zero material basis, while retaining manufactured/recipe provenance. Living production wages are real expenses recorded separately, not capitalized into these lots. No rule invents fuel, fees, depreciation or tax expenses.

Construction transfers cash to the town and increments `ConstructionExpenditure`. It creates no inventory basis, sale revenue, or repeated per-unit capital charge. A granted starter facility is marked `IsStarterAsset` with provenance and produces no construction expenditure. Blueprint construction cost is a current replacement quote, not a resale or liquidation value.

### Financial reconciliation and history

For each company, every committed regional snapshot preserves:

```text
Cash = InitialCash + SalesRevenue - InventoryPurchases - ConstructionExpenditure - WagesPaid
InventoryPurchases = CostOfGoodsSold + HeldInventoryCost
RealizedGrossMargin = SalesRevenue - CostOfGoodsSold
OperatingSurplusAfterWages = RealizedGrossMargin - WagesPaid
HeldGoods = Granted + Purchased + Produced - Consumed - Sold
```

The physical formula applies independently to each commodity. Transfers cancel within the company. The material-cost equality includes transformation reallocations and excludes capital and wages. Historical configurations have zero wages and retain their original financial results. Living wages are expensed immediately, including those for goods still held; operating surplus after wages is not matched full-cost profit. The implementation checks the basis identity when constructing company financial snapshots; experiments additionally reconcile actual cash and physical stores. Combined market+household+company cash remains conserved, with zero household accounts in historical worlds. Ship cash projections must not be counted again.

`RegionalResult.EconomicEvents` returns the current operation's committed purchases, sales, physical transfers, private production, construction payments and delayed construction completions. A completion event has zero cash, quantity and allocated cost; only the order contributes capital expenditure. Events carry committed day/revision, company, source/destination owner/store, port, relevant good and quantity, actual cash total and signed company change, allocated cost, recipe/facility, and gross consumed/produced cost slices as appropriate. Initial grant provenance is retained in initial lots and facility configuration. A host can retain a bounded journal without becoming an authority.

Rejected commands leave lots, aggregates, snapshot identity, and revision unchanged. Exceptional failure discards all staged accounting and all other world changes, including earlier actions in the same chain/day. Cost or aggregate overflow is exceptional, not permission to lose attribution. World snapshots retain current lots and aggregates, not an immutable copy of every past transaction. The amount of open-lot state can grow with unsold acquisitions; historical event count alone does not grow it.

Material gross margin excludes wages; living reports separately show actual paid wages and operating surplus after those expenses. Freight, overhead, financing and capital depreciation remain unmodeled. Held material basis is historical expenditure still attached to goods, not a current quote, predicted sale value or realized profit.

## Industrial two-port routes

`IndustrialRoutePolicy` is a separate immutable choice from the preserved `TradeRoutePolicy`. One ship has one active route mode. Each directional `IndustrialRouteLeg` names a canonical good, maximum/minimum shipment, market or owned-warehouse endpoints, optional matching facility IDs, source stock threshold, source warehouse reserve, maximum marginal buy price, minimum marginal sell price, and per-order expenditure cap. The policy supplies two connected ports, company cash reserve, optional return leg, and empty-return permission. A warehouse endpoint needs matching ownership and location; it does not require an unused public-market quote.

The authoritative ship stores one of four stages: `AcquireOutbound`, `DeliverOutbound`, `AcquireReturn`, `DeliverReturn`. This permits the same commodity on both legs without inferring direction from cargo. New industrial configuration requires an empty ship docked at either endpoint. At origin it begins outbound acquisition; at destination it begins return acquisition. Reapplying the **same immutable policy instance** only changes enablement and preserves stage. Enabling/disabling also preserves stage and voyage. A different policy instance must satisfy the empty/docked reconfiguration rules.

During acquisition, compatible cargo already meeting the departure minimum departs without requiring another purchase or loading toward the maximum. Otherwise the source must meet its threshold. Warehouse loading is bounded by stock after reserve, requested target, and free cargo capacity; it occurs only when the resulting shipment meets the minimum. Public buying uses shared exact bounded quantity search, price limits, reserve, expenditure cap, stock/capacity, payer funds, and receiver numeric headroom.

Delivery sells the largest eligible market quantity or unloads what the owned warehouse can hold. A partial delivery retains both residual cargo and delivery stage. Completion advances the stage; it never treats a transfer as revenue. Incompatible cargo pauses intact. Wrong-stage location or voyage also pauses with an explanation. An empty return can bypass an unavailable return purchase/loading only when explicitly permitted and the whole cargo is empty. It cannot bypass unsold outbound cargo or permit an empty outbound shipment.

The pure evaluator binds each decision to an exact snapshot. Execution checks freshness, stage, fresh market preview, actual dock, owner, stock, and capacity, then invokes ordinary trade, transfer, and departure operations. Public manual transfers still require automation disabled; the internal trusted automation path uses the same physical and accounting checks without that manual-control guard.

The existing **four-action maximum** bounds each zero-time chain, including a stage-only transition. A chain stops on sailing, wait/pause, no progress, or its action limit. Each action reevaluates the staged snapshot. Daily actor ordering and later production/consumption/voyage phases remain unchanged. The entire chain or day has one commit; failure rolls back physical movement, accounts, historical basis, stage, day, and revision together. Automation gains no prices, transport speed, stock, money, or permissions unavailable to equivalent manual economic actions.

## Starting charters, outcomes, and scoring

`RunScenario` selects one replacement `StartingCharter` and an optional fixed challenge over `EnterpriseScenario`. Charter cash, ship, cargo, starter facilities and inventories are explicit deterministic constructor inputs. Starter facilities are validated against the same blueprint eligibility, owner, capacity, canonical recipe, and one-blueprint-per-owner/port rules. Their first batch is due at initial day plus interval. They are not simulated purchases or construction commands, and initialization grants no production batch.

The selected package cannot change in a live world. Retry constructs a new world from the same selections; another charter requires another run. The legacy `EnterpriseScenario.Create` and original Coastal Supply constructor/default remain regression-compatible. All three new fixed challenges have deadline day **60** and require at least **100** company cash when succeeding:

| Challenge | Primary objective | Additional condition |
| --- | --- | --- |
| Coastal Supply Charter | Signed net **24 Bread** sold into Eastport by the selected company | Destination buybacks subtract credit |
| Regional Relief | **24 Bread** actually removed by successful scheduled consumption anywhere in the region | Regional performance, including public/NPC supply; sales/arrivals alone give no consumption credit |
| Enterprise Ambition | **12 Bread** actually produced in the company's private facilities | At least **200 realized gross margin**, derived from attributed sales |

Relief counts committed physical consumption of the configured good, not requested or unmet demand. Ambition reads the company's authoritative produced quantities and gross margin, so transfers and unsold output cannot manufacture its financial condition. All objectives evaluate after a complete operation with final cash and day. Success at/before the deadline takes precedence; otherwise reaching the deadline fails. Existing terminal guards freeze every economic command.

`AbandonChallenge` is an explicit zero-time changed commit recording `Abandoned` and the current day, retaining all accounting and holdings. It is distinct from failure or victory and is rejected for sandbox. Host confirmation protects deliberate loss of active progress; no persistent save, restore, or meta unlock is implied.

`RunResults` accepts only a terminal challenge snapshot and the selected charter. Score components are:

```text
fraction(x, target) = floor(500 * clamp(x, 0, target) / target)
Objective = fraction(objective quantity, required quantity)
  Enterprise Ambition uses min(Objective, fraction(realized margin, required margin))
Victory = 1000 on success; otherwise 0
Time = 10 * min(60, max(0, deadline - completed day)) on success; otherwise 0
Solvency = min(100, floor(company cash / 10))
Total = Objective + Victory + Time + Solvency
```

Intermediate score arithmetic is exact. Total score is bounded by **2,200**; trade count and gross sale turnover receive no separate points. Partial objective and solvency scores remain visible on failure/abandonment, without a victory or time bonus. Identified runs compare within challenge/charter/ascension/exact-world groups. Seeded identity includes seed and generator version; Classic is explicit. Legacy identity-null APIs retain the historical `challenge ID / charter ID` report field for byte compatibility, not a new cross-difficulty ranking. See [Economy](ECONOMY.md#task-17-complete-runs-and-attributed-results) for the historical examples and [Seeded runs](SEEDED_RUNS.md) for the expanded comparisons.

## Seeded geography and immutable run identity

`SeededArchipelago` version **1** accepts every signed 32-bit integer seed. The text parser accepts only ASCII digits with an optional leading `+` or `-`, no whitespace, separators, decimal point or exponent, and validates the int32 range. Host randomization chooses and displays a seed; the pure generator never accesses wall-clock time, Unity state or `System.Random`.

Generation starts with slots `(250,200)`, `(750,200)`, `(250,500)`, `(750,500)`. Three Fisher–Yates draws permute them; eight more add independent integer jitter from -40 through 40 to X/Y for the fixed ordinal East/North/South/West IDs. Each uint draw adds `0x9E3779B9` to its wrapping state, applies the versioned Murmur3 avalanche multipliers `0x85EBCA6B`/`0xC2B2AE35`, then takes an explicit modulo. The seed's signed bits initialize that state. There are exactly eleven draws, with no rejection or retry loop.

All four towns remain within X=210–790 and Y=160–540, separated by at least 220 chart units. Every pair gets one direct route: distance is the smallest positive integer `d` with `d*d >= dx*dx + dy*dy`, computed by bounded integer bisection. The maximum is 694, hence ordinary speed-100 ships take 3–7 days. Positions, sorted routes and the invariant signature come from one immutable geography. Distances are abstract chart units; direct lines imply no coastline collision or sea-pathfinding system.

Only positions and routes vary in **seeded v1**. Town IDs, industries, NPCs, inventories, accounts, pricing, demand, ship speeds, charters and challenge deadlines reuse the existing scenario. Classic uses the exact authored positions and six distances, with version 0 and no seed. Living Cities v2 is a separate opt-in economic mode described below. Legacy scenario overloads still construct Classic/Ascension 0 fixtures; prior golden reports remain separate from new reports.

`RunIdentity` captures challenge/null sandbox, charter, world mode, nullable seed, generator version, difficulty and living pricing mode. Coverage is legal only for Living Cities; legacy identities retain StockOnly and historical serialization. `RunScenario.Create(content, identity)` constructs from those selections; every snapshot/result retains them. The world validates matching identity/difficulty/challenge. **Retry Same World** reconstructs initial stocks, all accounts, cargo, facilities, policies, schedules and day/revision, clearing host state. **New World / Reroll** changes the draft seed and previews before a separate start. Navigation, reference browsing and text settings cannot reroll.

## Living cities: finite households and welfare

A port with `SettlementConfiguration` owns one finite household account and immutable `CitySnapshot` inside the same regional snapshot. Historical ports have no city state. Positive configured wages require living households. Completed public batches pay market→households; private batches pay company→households. Unfunded payroll returns `PayrollShortage` and produces nothing. Disabled, pending, input-starved, output-limited or capacity-blocked batches pay nothing. Stock, payroll, counters, FIFO and all accounts commit atomically. `TotalCash` sums markets+households+companies exactly and is checked on every instant/daily commit.

Essential and eligible luxury desired quantities use checked ceiling division: `ceil(quantity × population / populationPerUnit)`. Generated Bread requires one per 20 residents every four days. Furniture requires one per 80 every 24 days when enabled. Municipal Planks use the configured constant one every 12 days, remove available public stock and pay nobody. Direct household Grain/Flour/Logs needs are absent. Need deadlines advance after every due opportunity, including misses; no backlog or catch-up accrues.

`LivingRetail` shares snapshot preview and execution calculations. Retail picks the largest exact-price affordable integer quantity via the existing bounded search, transfers payment households→market and immediately removes purchased units. Let `available = min(desired, stock)`: unmet stock is `desired - available`, unmet affordability is `available - purchased`. A luxury purchase reserves the exact next-food cost calculated on hypothetical stock `max(current food stock, next desired quantity)` at the current quote. It reserves cash only. Receiver cash overflow remains exceptional and rolls back the whole operation.

After consumption, one essential observation is added to a window of at most six. Define `F = floor(100 × sum(consumed) / sum(desired))`, and `P = min(100, floor(100 × remaining household cash / next-food cost))`. Welfare target is `floor((3F + P)/4)`. Prosperity moves toward it by at most `min(3, ceil(abs(target-current)/8))`, with zero movement when equal. Healthy means full current fulfillment and `P=100`; deprivation means current consumption below half the requested amount. Counters cap at six.

Three consecutive healthy observations with prosperity at least 65 enable Furniture. Three consecutive observations with prosperity at most 40 or purchasing power below 25 disable it; both luxury counters cap at three. Initial luxury is disabled. Population reviews every 12 days in generated towns, requires all six food observations, and changes by at most one within 10–80. Three healthy observations plus prosperity at least 65 permit growth; three deprived observations plus prosperity at most 35 permit decline. Otherwise population stays fixed. Population changes affect later need calculations, never cash directly.

`PurchasingPowerPercent` retains the last essential-observation sample; `PopulationDelta` retains the last demographic-review change. Neither is a new instantaneous forecast following a trade or wage payment.

The optional Coverage adapter derives seven days of household needs, capped at 64 units per good, at initialization/end-of-day only. It processes essential then eligible luxury rules against one finite household budget, using exact authored-curve affordability on a hypothetical shelf `max(authored target, desired coverage)`. Dynamic household targets become `max(1, affordable coverage)`; other parameters/non-household prices remain unchanged. No future income or industrial-use forecast is invented. Parameters stay fixed across instant commands until the next daily boundary, while current quotes still respond to actual inventory; exact quote/execution and immediate same-model reversal guarantees remain intact.

### V2 economic generation and sustained recovery

`LivingCityGeneration` v2 calls unchanged v1 geography and uses a separate uint stream seeded with `seed bits XOR 0xA511E9B3`. Three Fisher–Yates role draws and eight population/liquidity draws total eleven; the same declared avalanche/modulo arithmetic applies. Four authored complementary role packages always provide all six canonical production paths globally, including logging+furniture without local Sawmill eligibility. Private eligibility comes from actual blueprint port lists. No strategy-driven retry/repair exists. Signatures cover generated geometry, roles, initial accounts/population/prosperity, stocks, prices, schedules, wages, needs and capabilities. [Living Cities](LIVING_CITIES.md#economic-generator-version-2) records exact bands and representative seeds.

Living `GetCharter(identity)` adapts only locations: Merchant/Independent start at the food source; Industrial ship/granted bakery start at processing. Cash, cargo, capacity and grant provenance remain unchanged. Classic/v1 retain original locations, industries, wage-free schedules, demands and historical `RunScenario.Challenges` enumeration. Living setup exposes one additional mode-specific recovery challenge.

City Recovery targets the generated vulnerable coast. By day 90 it requires six recent paid Bread opportunities, at least 90% fulfillment, initial population retained, prosperity at least 55 and company cash at least 100. Each observation's denominator is `max(actual desired, initial-population desired)`, preventing population shrinkage from reducing the target. Public/NPC contributions count as regional welfare; delivery alone does not. Progress percent is the lesser of fulfillment and window completion percentage; objective points apply the ordinary bounded fraction to target 90. All full success conditions are separately required. Existing final-operation evaluation, freeze, abandonment and retry apply.

## Presentation text scale

ArchipelagoLab uses Compact/Default/Large/Extra Large body sizes **14/17/21/25 px**, captions **12/15/19/23 px**, and headings **24/28/34/40 px**. Shared inherited USS tokens also adjust controls, map labels, table rows and panel sizing. Responsive classes, wrapping and internal scrolling handle smaller viewports; no paint-only global transform changes apparent text without its layout or hit targets. New controls inherit the active scale immediately.

`ArchipelagoTextScale` stores only this presentation preference using Unity PlayerPrefs under `SaltAndLedger.UiTextScale`. It survives screen transitions, retries and fresh runs and is saved locally for subsequent launches of that application environment. Editor/player preference stores can differ; it is neither cross-device synchronization nor an economic save. Other settings remain session-only. Scale changes issue no simulation commands and cannot alter day, revision, prices or outcomes. [Unity integration](UNITY.md) records actual visual/input verification separately from automated tests.

## Determinism and remaining scope

The same validated content, initial world state, and per-tick ordered commands produce the same states and structured outcomes. Reversing equivalent input building collections does not change results. Callers advance several ticks by calling `Step` repeatedly; the optional bounded automation helper issues those same sequential calls without a separate clock.

Equivalent quote, stock, content, and building collection order does not change outcomes. Trade sequence order deliberately does. Ordinal IDs and integer arithmetic avoid culture-dependent financial behavior; the market demo formats numbers invariantly.

The maritime demo additionally compares repeated and reversed-port input runs, uses invariant numeric formatting, and asserts its documented transaction totals, balances, six-tick itinerary, and empty final cargo. Local production order is independent of port configuration insertion order. Remote quote inspection changes no state.

Fixed, linear and scarcity pricing are compared in [Pricing experiments](PRICING_EXPERIMENTS.md); legacy shipping and automation reports retain their original ticks and economy. [Living archipelago](LIVING_ARCHIPELAGO.md) records the historical regional sink model; [Living Cities](LIVING_CITIES.md) records opt-in households, paid retail, wages and coverage comparisons. Dynamic formulas, welfare thresholds and scenario parameters remain provisional. Freight/overhead/depreciation, city ownership, politics, further ascensions, save formats, durable replay and persistent event sourcing remain unimplemented.

## Expanding Economy v3 quantitative and monetary contracts

The historical integer meanings above remain scale 1. Explicit v3 uses 10,000 physical subunits per good and 10,000 monetary subunits per coin. `int` stock remains bounded at 2,147,483,647 subunits; each cash balance is a checked nonnegative `long`. Complete recipe inputs/outputs and capacities scale once. Ship trades/transfers require whole-unit multiples; household retail and local warehouse stock may be fractional. The [v3 guide](EXPANDING_ECONOMY.md#exact-quantities-and-money) specifies examples and limits.

For proportional household need, retain exact numerator residual: `(desired, remainder) = DivRem(configuredScaledQuantity * population + previousRemainder, populationPerUnit)`. Current and initial-population baseline residuals are separate committed state. Preview does not consume carry. Need outcomes retain exact desired/purchased/stock-short/funds-short quantities and exact payment. Welfare combines ratios before its final integer indicator rounding. V2 keeps its old whole-unit ceiling. Bread cadence remains four days.

Pricing uses the same edge formulas above, with inventory target scaled and numeric price ratios unchanged. The first marginal rate is money-subunits per goods-subunit, equivalent numerically to coins/whole-good; exact total comes from the complete edge sum. Positive minimum cost, same-edge ask/bid ordering, bulk/split identity and unchanged-model reversal bounds apply at subunit resolution. No binary floating-point authoritative quantity, per-subunit loop or repeated conversion rounding is introduced.

V3 regional cash counts markets, households, companies and treasury. Global/all-account cash additionally counts the finite external authority. Each commit verifies initial regional cash + cumulative Mint issuance + external net inflow, and initial global cash + cumulative Mint issuance. Only explicit ready Mint exchanges create currency. External purchases transfer existing cash and record actual exported goods; retail, wages, construction and public procurement transfer existing cash. No currency retirement is modeled.

All player industry kinds, multiple same-kind sites, whole physical ship orders, finite outside contracts, public Mint ownership/reserve, shipwright materials/labor/timing and reference Company Value use the exact authored tables in [Expanding Economy](EXPANDING_ECONOMY.md). Private construction preserves Ascension 0/1. Public Mint and ship orders use separate four-day deadlines and complete after voyages with the ordinary pending-construction phase. Mint/contract/manual development previews bind the same snapshot as execution. A rejected ordinary shortage retains state identity; exceptional failure rolls back the complete operation.

FIFO slices retain `floor(C*end/Q)-floor(C*start/Q)` in raw subunits. Company wages are expense, not capitalized material basis. Mint receipts and surrendered-metal material basis are separate from sale gross margin; public commission payments are separate from privately owned capital. Pending ship/facility capital is counted once in Company Value until replaced by completed reference assets. Values use stable reference prices, not mutable local bids or fictional liquidation proceeds.

Save/load preserves all authoritative counters, residuals, open lots, effective quote parameters, pending deadlines, ship IDs/policies/voyages, public state and terminal outcomes. It neither advances time nor reevaluates challenges. Local storage policy belongs to the host; the portable codec validates the entire bounded schema before returning a replacement world.

## Grand Commission draft and objective contracts

The opt-in v3 [Grand Commission](GRAND_COMMISSION.md) uses mission draft version 1 independently of economic generator/content 3 and valuation 1. Each of four fixed categories deterministically shuffles four authored candidates through its own seed/version/round-salted integer stream and offers the first three. Resolve exactly one offered stable template ID from each category; bind actual role-relative ports and typed parameters into the identity signature. Selecting or revisiting offers never changes the economic world or advances time.

Victory requires all four selected cards currently met, company cash at least 1,000,000 monetary subunits (100 coins), and day at most 180. Day-180 success is evaluated before deadline failure. Existing terminal mutation guards apply to success, failure and abandonment. Net town/good supply is exact player-company sales minus purchases across ship and private warehouse market paths. Buybacks can reverse it; previews, NPC trades and private transfers cannot manufacture it. First docks and actual arrivals count toward bounded per-owned-ship visits; a commissioned second ship must reach a second town. Failed atomic operations publish no history.

Private production comes from successful funded batches and existing output/payroll totals. A New Industry requires the same selected operational Mill and Bakery in distinct towns to each have a funded batch; a same-town starter Bakery cannot substitute. Welfare uses the recovery town's latest six actual need observations with the greater current/initial demand denominator. Current cash, value, welfare and net supply remain reversible; historical output, visits and public deliveries are cumulative. A conjunction card's percentage is its lowest condition percentage, each clamped to 0–100 after integer floor. New objective score is `floor(500 * sum(four card percentages) / 400)`; old objective formulas and independent victory/time/solvency components remain unchanged.

The full global authored thresholds, exact stream algorithm and save boundaries are in [Grand Commission](GRAND_COMMISSION.md). Legacy schema-2 drafted saves preserve the resolved plan and bounded history; valid older schema-1 saves retain their original meanings. New pooled-storage runs use schema 3, preserving that same plan/history alongside the explicitly versioned town stockpiles. Loading validates and reconstructs without reevaluation, while Retry recreates the same complete identity from day zero.
