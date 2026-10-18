# Maintained mechanics specification

This page specifies implemented behavior through Task 1.2 that cannot be derived from content definitions alone. Update it with behavior changes. Generated reference pages describe only the canonical definitions and their relationships.

## Inventory

An inventory belongs to a validated content catalog. It contains one quantity for each known good; unspecified initial quantities default to zero. Quantities are nonnegative signed 32-bit integers, from 0 through 2,147,483,647 inclusive. Unknown goods and negative initial quantities are rejected.

These limits define an exact, bounded sample state representation. They do not decide future cargo units, storage capacity, market depth, or production balance. Reads do not mutate stock. A snapshot is detached and cannot be used to modify the inventory.

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

## World state and time

`WorldState` owns one fixed location, one shared production inventory, a collection of building instances, an optional market/trader configuration, and a nonnegative signed 64-bit `Tick`. The default tick is 0. A successful `Step` advances it by exactly 1 with checked arithmetic; a step starting at 9,223,372,036,854,775,807 fails without mutation. An empty world or a world with no enabled buildings still advances when stepped.

A tick is one abstract economic step. It has no conversion to hours, days, real seconds, or Unity frames. The host explicitly calls `Step`; the simulation creates no thread, timer, background loop, or random values.

A `BuildingInstance` has its own stable `Id`, a canonical `BuildingId`, a canonical `RecipeId`, an integer `Priority` (default 0), and `ProductionEnabled` (default `true`). Several instances can reference the same building definition and recipe. World construction validates all references and recipe/building associations, even for disabled instances. Runtime location and instance IDs match `[a-z][a-z0-9_]*` in full using ASCII characters; instance IDs must be unique within the world. These are runtime identities, separate from the globally unique canonical definition IDs. No new content definition is created for an instance or the location.

The host supplies initial stock; unspecified goods start at zero. It may also supply a nonnegative initial tick. State is copied into the world. Callers cannot change its inventory or building flags through retained constructor inputs or snapshots. Building instances are immutable values; a configuration change replaces the relevant value inside the next world snapshot. There is no construction, demolition, runtime priority change, or recipe-switching command.

## Finite market, ownership, and money

Supplying `market: new MarketConfiguration(...)` enables trading with one trader. The existing `WorldSnapshot.Inventory` is the market's stock: there is no second copy of those goods. Buildings consume and produce in that same stock. The trader has a separate inventory, stable runtime ID, and cash balance; production never touches the trader's inventory or either cash balance. Public-market ownership of production is a **prototype assumption**, not a permanent decision about player-owned factories.

Both cash balances use `long`, from 0 through 9,223,372,036,854,775,807. One unit is the smallest indivisible currency unit. The currency name and scale are experimental. Prices and accounting use no binary floating point. Transaction multiplication, debits, credits, and reported deltas use checked integer arithmetic. Combined holdings need not fit in one `long`; each account and transaction must. The implementation does not add balances together to authorize transfers.

`MarketQuote(goodId, bid, ask)` defines prices from the trader's perspective:

| Direction | Goods move | Cash moves | Unit price |
| --- | --- | --- | --- |
| `Buy` | Market to trader | Trader to market | `Ask` |
| `Sell` | Trader to market | Market to trader | `Bid` |

Require `Ask >= Bid > 0`. Quotes are unique by canonical good ID and immutable for the world's lifetime. A known good without a quote cannot be traded. An empty quote collection is permitted but makes every good untradeable. Prices live in runtime configuration, not canonical good definitions or the content schema. They are a **fixed experimental accounting baseline**: neither stock, production, transactions, nor elapsed ticks change them. An immediate same-quantity buy/sell round trip cannot increase trader cash when quotes are unchanged.

Initial market stock uses the existing constructor argument; the configuration supplies initial market cash, trader ID, initial trader cash, and optional trader stock. Unspecified trader goods start at zero. Runtime trader IDs follow the same ASCII ID syntax. Configuration collections are copied and immutable; canonical references are resolved when constructing the world. No automatic stock or cash replenishment, external income, borrowing, fees, or consumption exists.

Without market configuration, `Trading` is null. Existing constructor calls and production-control calls retain their behavior, and `TradeAttempts` is empty. Submitting a trade to such a world is an error.

## Trading commands and outcomes

`TradeCommand(direction, goodId, quantity)` requests a positive `int` quantity. Commands are immediate all-or-nothing requests for one step, with no partial fills, reservations, persistent orders, or queues.

After validating all command references, each trade computes `checked(quantity * unitPrice)` before checking shortages. An unrepresentable total is therefore an exception even if stock or cash would also be insufficient. Check the seller's stock first, then the buyer's cash. If both are insufficient, report the stock outcome. For a payable, fully stocked trade, calculate both resulting inventories and both cash balances, including receiver overflow checks, before applying any changes.

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
| Negative initial stock/cash/tick; nonpositive trade quantity/bid; ask below bid; invalid direction enum | `ArgumentOutOfRangeException` |
| Stock, cash, transaction total, or tick overflow | `OverflowException` |
| Trade without market configuration, or recursive `Step` | `InvalidOperationException` |

Runtime ID syntax is checked when constructing the corresponding instance or command. World construction resolves canonical references; `Step` resolves command targets. An exception thrown by a caller's command enumerable propagates without committing world state.

The world stages private inventories, cash, and building state for each step, then replaces one immutable snapshot only after constructing the complete result. This small copying approach is intentional for atomicity and clarity; it is not a generalized transaction system or a performance claim. The API requires one caller making sequential calls; concurrent or reentrant mutation is unsupported. External side effects in a caller-provided command enumerable are outside the world's state contract.

## Step results and snapshots

A completed `StepResult` contains:

- `Tick`: the tick at which commands and production ran. The first default-world result has `Tick == 0`.
- `After`: the immutable world snapshot after the step; the first result has `After.Tick == 1`. It includes location ID, market/production inventory, building state, and optional `Trading` state (`MarketCash`, `TraderId`, `TraderCash`, `TraderInventory`, and fixed `Quotes`).
- `TradeAttempts`: attempts in declared execution order. Each includes `Direction`, `GoodId`, requested `Quantity`, `UnitPrice`, requested `TotalAmount`, and explicit `Outcome`. `MarketInventoryChange`, `TraderInventoryChange`, `MarketCashChange`, and `TraderCashChange` expose before, after, and signed delta balances. Rejected attempts report the requested price/total but have zero deltas, not a payment.
- `ProductionAttempts`: attempts in actual production order. Each has building instance, building definition, and recipe identifiers, an outcome of `Produced` or `ResourceShortage`, and its inventory changes.

Each production inventory change identifies the good and its `Before`, `After`, and signed `Delta` quantities for that attempt, ordered by ordinal good ID. Only changed balances are included in production change lists. A production shortage has no changes; a successful recipe with no net stock change still has a `Produced` outcome. A step may have no attempt records and still advance its tick.

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

## Determinism and remaining scope

The same validated content, initial world state, and per-tick ordered commands produce the same states and structured outcomes. Reversing equivalent input building collections does not change results. Callers advance several ticks by calling `Step` repeatedly; no batch-step helper is provided.

Equivalent quote, stock, content, and building collection order does not change outcomes. Trade sequence order deliberately does. Ordinal IDs and integer arithmetic avoid culture-dependent financial behavior; the market demo formats numbers invariantly.

There is no dynamic price, automatic replenishment, production duration, cooldown, rate per minute, operating cost, wage, energy, transport, consumption, or configurable automation policy. Production repeats only because the host advances the world. Future arrival and consumption phases remain unresolved. Deterministic step results are useful for comparison; they are not a save format, durable replay protocol, event bus, or persistent event-sourcing system.
