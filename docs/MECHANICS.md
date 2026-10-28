# Exact mechanics contracts

These contracts describe the supported current v3 economy. Prototype worlds and old save schemas are retired. Canonical recipe definitions come from the generated reference; mutable-world rules remain explicit below and in the focused subsystem documents.

## Atomic regional operations

RegionalWorldState alone owns every account, physical store, ship, schedule, construction, policy and objective. Commands validate first, stage changes privately and commit one immutable snapshot. Exceptional failure rolls back all fields and earlier staged daily attempts. Expected stock/funds/capacity shortages are completed outcomes. A changed command increments revision; rejected/no-op commands preserve the snapshot. Instant commands never advance time, and previews require the exact snapshot.

AdvanceDay runs actors by priority/ordinal ID, private procurement, due four-day fiscal review, public production by port/priority/ID, private production, private sales, consumption by port/kind/ID, welfare/population and next-day quote parameters, voyages, due construction, then objectives. NPCs precede the player's priority10. Every phase reads earlier staged results. No unbounded event history is retained.

## Production, construction and storage

Canonical extraction creates its declared outputs; conversion consumes gross inputs and creates declared outputs. V3 applies each recipe quantity in10,000-subunit physical units. A batch requires all inputs, output space and full actual-employer payroll. Rejection changes none of them. Success sets next due day to current day plus interval; blocked/disabled/output-limited sites stay due without catch-up bursts. Toggles preserve deadlines.

Private sites share one physical/FIFO company pool per town, with individual schedules, enabled states, operating rates, priorities and production counters. Pool capacity sums granted/paid sites including pending ones. Same-recipe output ceilings sum operational sites including disabled ones, never pending sites. Private production runs by ordinal port, High/Normal/Low priority, then ordinal facility ID, reading fresh shared stock. Public production and NPC merchant ordering are unchanged.

Operating rate is an integer0–100, default100. Each facility stores ProductionCredit0–199, initially0. At an enabled positive-rate due opportunity, add the rate only when credit is below100. If it remains below100, record a rate-skipped opportunity and move the deadline forward one interval. Otherwise one full batch is admitted: shortages retain that credit and deadline for retry, without adding credit again. Success subtracts100 and moves the deadline forward one interval. Disabled/rate0 preserves phase and deadline and consumes nothing. Changing controls cannot reset phase or generate catch-up batches. Inputs, outputs and wages always use complete canonical batches. Twelve-day signals count these admitted scheduled batches, separately from installed full-rate capability; they promise neither available inputs nor successful execution.

Each actual pool owns one warehouse policy, independent of factory controls. Per-good Buy target/maximum marginal price and Sell reserve/minimum marginal price use exact local units; zero price means unrestricted. Both enabled requires BuyTarget<=SellReserve, preventing buying from directly generating sellable surplus. Known goods need not appear in local recipes. Targets/reserves cannot exceed actual capacity. Cash reserve limits buying, not wages. New warehouse rules are all off. Buying precedes private production and selling follows it, with at most one transaction per good per phase in stable port/owner/good order. Ordinary exact prices, capacity, finite cash, FIFO, commercial fees and net-sale objective history apply; counters belong to the pool. No input reservation or hidden replenishment is introduced.

Ascension0 builds immediately. Ascension1 pays on D, completes after voyages on D+2 and first produces one full interval later. Pending factories cannot produce even on completion day. Their real paid pool capacity is already usable by warehouse rules, manual local trades and actual-dock transfers. Starter grants are operational. Transfers preserve ownership and FIFO, move no cash/day and respect both capacities.

## Physical shipping

Routes have positive integer distances and speeds; duration is ceiling(distance/speed). Rendering never determines travel. Departure consumes no day; each AdvanceDay decrements voyage once. Committed arrival permits immediate dockside commands. A remote quote is informational. Enabled automation blocks mixed manual ship commands; pausing preserves cargo, voyage and progress.

Current player itineraries and NPC research policies use the same ordinary trade/transfer/departure paths. See [automation](AUTOMATION.md). Fleet construction consumes real local Planks and payments; pending plus completed owned ships cannot exceed three.

## Exact inventory-edge pricing

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

`TradingConfiguration.Quotes` holds configurations, not cached current prices. `MarketQuote.IsFixed` identifies the baseline; `Pricing` is null for fixed quotes. The fixed-only `MarketQuote.Bid` and `Ask` properties are available for fixed configurations only and throw `InvalidOperationException` for dynamic ones. Use the snapshot quote API for current marginal prices. Historical snapshots continue to derive historical prices after the live world moves on.

Production consumes market logs and adds market planks. The committed snapshot derives prices from the resulting stock, so consuming logs can raise their quotes and producing planks can lower theirs, subject to rounding and bounds. Prices do not change solely because the tick advances. There is no separate price clock, asynchronous update, demand state, or mutable price cache.

## Trading commands and outcomes

`TradeCommand(direction, goodId, quantity)` requests a positive `int` quantity. Commands are immediate all-or-nothing requests for one step, with no partial fills, reservations, persistent orders, or queues.

After validating all command references, fixed-price trades use this order: compute `checked(quantity * unitPrice)`, check seller stock, check buyer cash, then check all resulting inventories and cash balances. A fixed total overflow therefore throws even if stock or cash would also be insufficient.

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

## Historical inventory costs and committed events

Accounting shares the staged regional operation. `AccountingSnapshot` contains company aggregates and the open cost lots of `ship:<id>` and `company:<owner>:<port>` stores. Public market goods do not carry a company acquisition basis; the price actually paid when buying them creates one. Accounting observes actual successful changes through the existing economic operations and does not authorize a different price or transfer.

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

Outputs are appended as manufactured lots. Full input material cost equals full output material cost. Extraction has no modeled material expenditure and hence zero material basis, while retaining manufactured/recipe provenance. Production wages and commercial service fees are real expenses recorded separately, not capitalized into these lots. Unmodeled fuel or depreciation is never invented.

Construction transfers cash to the town and increments `ConstructionExpenditure`. It creates no inventory basis, sale revenue, or repeated per-unit capital charge. A granted starter facility is marked `IsStarterAsset` with provenance and produces no construction expenditure. Blueprint construction cost is a current replacement quote, not a resale or liquidation value.

### Financial reconciliation and history

For each company, every committed regional snapshot preserves:

```text
Cash = InitialCash + SalesRevenue + MintReceipts - InventoryPurchases
     - ConstructionExpenditure - WagesPaid - FeesPaid - PublicCommissionExpenditure
InventoryPurchases = CostOfGoodsSold + HeldInventoryCost + MintMaterialCost
RealizedGrossMargin = SalesRevenue - CostOfGoodsSold
OperatingSurplusAfterWages = RealizedGrossMargin - WagesPaid
OperatingSurplusAfterExpenses = OperatingSurplusAfterWages - FeesPaid
HeldGoods = Granted + Purchased + Produced - Consumed - Sold - Surrendered
```

The physical formula applies independently to each commodity. Transfers cancel within the company. The material-cost equality includes transformation reallocations and excludes capital, wages and fees. Wages are expensed immediately, including those for goods still held; operating surplus after wages is not matched full-cost profit. The implementation checks the basis identity when constructing company financial snapshots; experiments additionally reconcile actual cash and physical stores. Regional money includes markets, households, companies and treasury, and equals initial regional money plus minted currency and net external inflow. All-account money also includes the finite external authority and equals its initial total plus minted currency. Ship cash projections must not be counted again.

`RegionalResult.EconomicEvents` returns the current operation's committed purchases, sales, physical transfers, private production, construction payments and delayed construction completions. A completion event has zero cash, quantity and allocated cost; only the order contributes capital expenditure. Events carry committed day/revision, company, source/destination owner/store, port, relevant good and quantity, actual cash total and signed company change, allocated cost, recipe/facility, and gross consumed/produced cost slices as appropriate. Initial grant provenance is retained in initial lots and facility configuration. A host can retain a bounded journal without becoming an authority.

Rejected commands leave lots, aggregates, snapshot identity, and revision unchanged. Exceptional failure discards all staged accounting and all other world changes, including earlier actions in the same chain/day. Cost or aggregate overflow is exceptional, not permission to lose attribution. World snapshots retain current lots and aggregates, not an immutable copy of every past transaction. The amount of open-lot state can grow with unsold acquisitions; historical event count alone does not grow it.

Material gross margin excludes wages and service fees; reports show these actual expenses separately. Other freight, overhead, financing and capital depreciation remain unmodeled. Held material basis is historical expenditure still attached to goods, not a current quote, predicted sale value or realized profit.

## Current run outcomes

Grand Commission is the sole scored challenge. Four typed drafted goals and100 liquid company coins must hold together by day180. Evaluation follows each atomic operation; success takes precedence on the deadline. Success/failure/abandonment freezes mutations. Sandbox has no deadline or score. Retry recreates the complete immutable identity; [schema6 saves](SAVES.md) restore exact state without stepping.

One solvency point per10 coins is capped at100; victory adds1000; remaining-day points are10 per day capped at600. Grand Commission objective points are specified below. Starting grants are real endowments with zero paid basis, not fictitious purchases or earned profit.

## Expanding Economy v3 quantitative and monetary contracts

Small fixed-price fixtures can use scale1. Current v3 uses 10,000 physical subunits per good and 10,000 monetary subunits per coin. `int` stock remains bounded at 2,147,483,647 subunits; each cash balance is a checked nonnegative `long`. Complete recipe inputs/outputs and capacities scale once. Ship trades/transfers require whole-unit multiples; household retail and local warehouse stock may be fractional. The [v3 guide](EXPANDING_ECONOMY.md#exact-quantities-and-money) specifies examples and limits.

For proportional household need, retain exact numerator residual: `(desired, remainder) = DivRem(configuredScaledQuantity * population + previousRemainder, populationPerUnit)`. Current and initial-population baseline residuals are separate committed state. Preview does not consume carry. Need outcomes retain exact desired/purchased/stock-short/funds-short quantities and exact payment. Welfare combines ratios before its final integer indicator rounding. Bread cadence remains four days.

Pricing uses the same edge formulas above, with inventory target scaled and numeric price ratios unchanged. The first marginal rate is money-subunits per goods-subunit, equivalent numerically to coins/whole-good; exact total comes from the complete edge sum. Positive minimum cost, same-edge ask/bid ordering, bulk/split identity and unchanged-model reversal bounds apply at subunit resolution. No binary floating-point authoritative quantity, per-subunit loop or repeated conversion rounding is introduced.

V3 regional cash counts markets, households, companies and treasury. Global/all-account cash additionally counts the finite external authority. Each commit verifies initial regional cash + cumulative Mint issuance + external net inflow, and initial global cash + cumulative Mint issuance. Only explicit ready Mint exchanges create currency. External purchases transfer existing cash and record actual exported goods; retail, wages, construction and public procurement transfer existing cash. No currency retirement is modeled.

All player industry kinds, multiple same-kind sites, whole physical ship orders, finite outside contracts, public Mint ownership/reserve, shipwright materials/labor/timing and reference Company Value use the exact authored tables in [Expanding Economy](EXPANDING_ECONOMY.md). Private construction preserves Ascension 0/1. Public Mint and ship orders use separate four-day deadlines and complete after voyages with the ordinary pending-construction phase. Mint/contract/manual development previews bind the same snapshot as execution. A rejected ordinary shortage retains state identity; exceptional failure rolls back the complete operation.

FIFO slices retain `floor(C*end/Q)-floor(C*start/Q)` in raw subunits. Company wages are expense, not capitalized material basis. Mint receipts and surrendered-metal material basis are separate from sale gross margin; public commission payments are separate from privately owned capital. Pending ship/facility capital is counted once in Company Value until replaced by completed reference assets. Values use stable reference prices, not mutable local bids or fictional liquidation proceeds.

Save/load preserves all authoritative counters, residuals, open lots, effective quote parameters, pending deadlines, ship IDs/policies/voyages, public state and terminal outcomes. It neither advances time nor reevaluates challenges. Local storage policy belongs to the host; the portable codec validates the entire bounded schema before returning a replacement world.

## Grand Commission draft and objective contracts

The opt-in v3 [Grand Commission](GRAND_COMMISSION.md) uses mission draft version 1 independently of economic generator/content 3 and valuation 1. Each of four fixed categories deterministically shuffles four authored candidates through its own seed/version/round-salted integer stream and offers the first three. Resolve exactly one offered stable template ID from each category; bind actual role-relative ports and typed parameters into the identity signature. Selecting or revisiting offers never changes the economic world or advances time.

Victory requires all four selected cards currently met, company cash at least 1,000,000 monetary subunits (100 coins), and day at most 180. Day-180 success is evaluated before deadline failure. Existing terminal mutation guards apply to success, failure and abandonment. Net town/good supply is exact player-company sales minus purchases across ship and private warehouse market paths. Buybacks can reverse it; previews, NPC trades and private transfers cannot manufacture it. First docks and actual arrivals count toward bounded per-owned-ship visits; a commissioned second ship must reach a second town. Failed atomic operations publish no history.

Private production comes from successful funded batches and existing output/payroll totals. A New Industry requires the same selected operational Mill and Bakery in distinct towns to each have a funded batch; a same-town starter Bakery cannot substitute. Welfare uses the recovery town's latest six actual need observations with the greater current/initial demand denominator. Current cash, value, welfare and net supply remain reversible; historical output, visits and public deliveries are cumulative. A conjunction card's percentage is its lowest condition percentage, each clamped to 0–100 after integer floor. New objective score is `floor(500 * sum(four card percentages) / 400)`; victory/time/solvency components are separate.

The authored thresholds and stream algorithm are in [Grand Commission](GRAND_COMMISSION.md). Current schema6 restores the resolved plan and bounded history without reevaluation. Retry reconstructs the same complete identity from day zero.

## Ordered cargo itinerary contract

`CargoItineraryPolicy` version 1 applies only to player ships with explicit v3, pooled storage and `RunIdentity.ItineraryVersion == 1`. It contains 2–4 unique connected port stops, 1–8 commands per stop and at most 24 total. Each immutable command chooses BuyFromMarket, SellToMarket, LoadFromCompanyTownStock or UnloadToCompanyTownStock, a known good, positive whole-cargo maximum/minimum, and Skip or Wait below minimum. Buy/sell use the ordinary exact quote and finite accounts; transfers use the ordinary matching dock/owner/store path and move original FIFO slices without payment. The full user-facing limits and example are in [Automation](AUTOMATION.md#ordered-cargo-itineraries).

Policy validation is structural: an owned company warehouse need not exist at a planned stop. Assignment, preview, execution and schema6 restoration accept such future logistics. Missing company storage is a normal `MissingCompanyStock` Skip/Wait blocker, never an exception or permission to create assets. Existing cargo/space constraints can take precedence. Once ordinary construction creates the real owned store, the unchanged instruction uses it. Structural route and actual-store accounting validation remain strict.

An instruction selects the largest qualifying whole quantity, executes once per visit and advances, including a partial quantity above minimum. It is not a hold target or a retry-until-maximum order. Skip advances with no transaction; Wait retains the exact action and stops the chain. Unmentioned mixed cargo remains aboard. Each staged action reevaluates current stock, capacity, market/company cash, marginal threshold, purchase reserve/cap and warehouse reserve. No action executes remotely or grants public/private inventory interchangeably.

The authoritative `CargoItineraryProgress` retains stop/action, cycle, last decision/reason/quantity, and OperationsDay/OperationsUsed. Successful actions, skips and departure each consume one of eight per ship per day. Wait/paused/sailing inspection consumes no operation budget; recording a changed last decision can still commit one revision. Repeated dockside calls and restore cannot reset the budget. ActionIndex equal to command count means ready to depart; departure advances to the next stop/action zero and increments cycle only when leaving the last listed stop. That cycle counts policy passes, not necessarily full circuits from an initially middle-stop application. Travel uses normal days and cannot loop instantly.

`ConfigureItinerary` is snapshot-bound and passive. Replacement requires disabled automation, an actual dock in the proposed plan, and an old cursor at zero or after every stop command. Any already-consumed current-day itinerary budget also prevents replacement until the next day. Equivalent policy content retains cursor and budget. Pausing preserves the ship's voyage/cargo; enabling requires its next stop or the corresponding incoming route. Manual relocation remains paused and cannot forge itinerary arrival. The active market, industrial and itinerary policies are mutually exclusive. All progress, accounting and last-decision changes share the existing atomic world commit; a later exception restores the entire old state.

Current RunIdentity defaults to itinerary version1 and includes it in comparisons. Schema6 restores bounded typed policy/cursor/budget alongside exact cargo and FIFO, rejecting inconsistent indices, goods, stores, modes or enabled locations before returning a world. Retry recreates the initial identity and terminal saves remain frozen.

## Economic circulation revision 1

New modern v3 identity selects economic rules1 (generator/content3, storage2, itinerary1). Explicit revision0 research controls are diagnostic only. The [complete circulation contract](ECONOMIC_CIRCULATION_REVIEW.md#revision-1-contracts) defines the only new transfers: a 2% ordinary company sale handling fee with cumulative per-company/town floor rounding and market/household split; and mutually exclusive bounded payroll levy or available-food relief every four days. Gross market affordability, exact prices and material FIFO remain authoritative. Fees are separate company expenses and household service receipts, never production wages.

The daily phase inserts municipal review after actors/private procurement and before public production. Shadow production reserves one feasible upcoming public batch per enabled facility in normal order. The levy protects twice the next essential budget; relief protects ready payroll and requires real stock. Shortfall,12coins and10%unreserved payer cash bound each transfer. Next review dates and totals are part of the atomic state, including no-transfer reviews. Loading cannot repeat them. Existing Mint/external boundaries remain the only monetary expansion/inflow paths.

Revision1 Coverage floors luxury targets at half the authored target, at least one whole good (Furniture2); it does not change eligibility or consumption. Initial public imports are finite: at least four initial Bread opportunities plus bounded role inputs. Historical pricing/generation is unchanged. `OperatingSurplusAfterExpenses = RealizedGrossMargin - WagesPaid - FeesPaid`; it excludes capital and Mint/public transactions. Fee prefixes and complete fiscal state are saved in schema6.
