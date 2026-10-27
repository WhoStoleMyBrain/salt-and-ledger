# The Expanding Economy

Task 2.0 is an explicit **Expanding Economy / ruleset v3** selection. It uses the existing atomic regional simulation with exact quantities, expandable private production, a bounded player fleet, public monetary institutions, reference Company Value and durable local run state. The current game uses this ruleset with circulation1 and pooled stock; retired player modes are recoverable from Git history.

The rules and authored numbers below describe the implementation contract. Construction prices, wage rates, monetary ratios and objective thresholds are **provisional design hypotheses**. Numerical tests, economic comparisons and actual Unity interactions are separate evidence; the [acceptance record](REPOSITORY-CLEANUP.md) separates these evidence types. Nothing in the generator guarantees a winning strategy, a healthy idle economy or perpetual growth.

## Exact quantities and money

V3 stores **10,000 physical subunits per whole good** and **10,000 monetary subunits per displayed coin**. Inventory fields are nonnegative checked `int` subunits, up to 2,147,483,647, equivalent to 214,748.3647 goods. Cash fields are nonnegative checked `long` monetary subunits. Wider `BigInteger` intermediates protect products and aggregates; converting back outside the public bounds throws and rolls back the whole operation. Legacy inventories retain scale 1 and their original API meanings.

One physical scale applies to market stock, private stock, cargo, recipe execution, Silver, consumption and FIFO lots. Complete canonical batches multiply their declared ingredient quantities by 10,000. There is no fractional production batch. A ship trade or transfer must be a positive multiple of 10,000, while household retail and local warehouse trades may move subunits. A market holding 2.75 Bread can sell two whole Bread to a ship, leaving 0.75. Loading cannot evade the hold's actual total quantity capacity.

Bread remains due every four days. Desired quantity is proportional:

| Population | Desired Bread per opportunity |
| ---: | ---: |
| 10 | 0.50 |
| 15 | 0.75 |
| 20 | 1.00 |
| 21 | 1.05 |
| 30 | 1.50 |

For scaled configured quantity `Q`, population `P`, denominator `N` and retained remainder `R`, compute `DivRem(Q*P + R, N)`. The quotient is this opportunity's desired physical subunits; the remainder carries to the next opportunity. Preview leaves it unchanged. Current-population and initial-population baseline needs retain separate remainders. This supports finer authored fractions without repeatedly discarding their residual. Municipal Planks remain a constant unpaid physical own-use sink.

Retail determines available stock first, then the largest affordable exact-price subunit quantity. `unmet stock = desired - available`; `unmet affordability = available - purchased`. Only purchased quantities disappear and exactly their payment moves household→market. With desired 0.75, stock 0.60 and funds for 0.40, consumption is 0.40, stock shortfall 0.15 and affordability shortfall 0.20. Bread and Furniture never automatically print or retire money.

### Pricing retains the shared inventory-edge formula

The existing exact bounded price algorithm operates at physical-subunit resolution. Scale inventory targets by 10,000; keep numeric reference, minimum, maximum, sensitivity and spread as monetary-subunits per physical-subunit, numerically equal to coins per whole good. For example, a marginal value of 20 means 20 monetary subunits for the next physical subunit, or an equivalent rate of 20 coins/good. It is not a whole-order total.

For upper inventory edge `n`, the [existing formula](MECHANICS.md#values-belong-to-inventory-transitions) floors the exact rational curve, applies the side spread and bounds the positive result. Purchases sum ask edges from stock down; sales sum bid edges upward. Prefix-floor sums remain logarithmic/bounded; there is no loop per requested subunit or distinct price. Every subunit costs at least one monetary subunit. Uninterrupted order splitting telescopes across the same edges, including fractional boundaries, and same-edge bid never exceeds ask. These guarantees concern an unchanged local model; voyages and production can create real cross-port opportunities.

`UnitPrice` continues to identify the first marginal subunit rate. The exact bulk total can differ from quantity times that initial rate. Formatting and execution averages are presentation-only. Coverage is still the provisional alternative to StockOnly: funded household targets are rederived only at initialization and daily boundaries, then remain immutable across instant commands.

Food histories retain at most six exact observations. Welfare combines exact fulfillment and purchasing-power ratios before rounding its final integer indicator; it does not repeatedly round each fractional meal to an integer good. Prosperity remains bounded, slowly smoothed, with the existing sustained luxury and demographic conditions. Population growth changes needs, not bank balances. Recovery evaluates the larger current/initial-population need so shrinking population cannot manufacture progress.

## Geography, roles and actual productive capacity

The v3 generator calls unchanged v1 geography and unchanged v2 role generation. A seed therefore has identical integer positions and six route distances, and the same separate eleven-draw role/population/liquidity permutation. V3 applies a deterministic scaled configuration and new eligibility matrix. It never evaluates strategies, rejects a seed, repairs stock or rerolls to obtain a desirable outcome.

| Private industry | Eligible roles | Construction coins | Batch interval | Wage coins/batch | Warehouse / output ceiling, whole goods |
| --- | --- | ---: | ---: | ---: | ---: |
| Farm | Food source | 240 | 2 | 8 | 60 / 48 |
| Lumber Camp | Food source, forest furniture, vulnerable coast | 220 | 2 | 6 | 40 / 36 |
| Mill | Processing | 260 | 2 | 6 | 40 / 24 |
| Sawmill | Processing | 280 | 2 | 6 | 40 / 20 |
| Bakery | Food source, processing, vulnerable coast | 300 | 2 | 6 | 40 / 30 |
| Furniture Workshop | Forest furniture, vulnerable coast | 360 | 3 | 8 | 40 / 15 |
| Silver Mine | Forest furniture | 320 | 4 | 12 | 20 / 8 |

Every role appears once in every generated world and all towns are connected, so all six old production types and the new Silver Mine are legally available somewhere. Forest furniture deliberately has no Sawmill capability and imports Planks. Processing imports Grain/Logs; food-source industry needs Flour. Eligibility is enforced by construction, not inferred from displayed names.

Multiple facilities of the same kind may coexist in v3. Their IDs, policies, production deadlines and counters remain distinct. Legacy storage keeps separate owned warehouses; new pooled runs share one company store per town under the [storage rules](HARBOR.md#shared-stock-rules-version-2). There is no one-Mill-per-town rule. Ascension 0 is instant; Ascension 1 new private sites complete at the end of D+2 and first produce one full interval later. Pending sites skip automatic procurement/production/sales, including their completion day. Starter grants are already operational. Those rules are unchanged for older modes.

An additional Mill adds a real potential Flour batch every two days, requiring two actual Grain and a funded wage. A complete food chain still needs extraction, processing, Logs for fuel, bakery capacity, storage, customers and transport. Missing inputs, payroll, output ceiling and warehouse capacity prevent the complete batch and its wage. A blocked due site does not accumulate catch-up batches. Wages for private extraction are actual company expenditure, never free starter income.

V3 charters start with Merchant **1,800**, Industrial **1,200**, or Independent **1,500 coins**. Whole hold sizes and physical starter grants remain 10/8/12 units, an operational bakery with two Flour/two Logs for Industrial, or four Grain/two Logs aboard for Independent. Merchant/Independent start at food source; Industrial starts at processing. These explicitly authored investment budgets replace the v3 opening only; they never replenish later balances.

## Money boundaries and finite outside commerce

Regional accounts are all markets, households, companies and the public treasury. The external trading authority is outside that boundary, with one finite cash account. Ship cash is merely the company's projection and is never counted again.

```text
regional cash = initial regional cash + minted currency + external net inflow
all-account cash = initial all-account cash + minted currency
```

There is no currency retirement in this milestone. Every commit checks both identities, including instantaneous development commands. Ordinary trade, payroll, retail, construction and treasury expenditure are zero-sum transfers. Population and day advancement cannot create cash. Mint issuance is the sole modeled increase to all-account money.

The external authority begins with **1,200 coins**, with no periodic reset. The processing harbor offers these player-company cargo contracts:

| Contract | Total whole quantity | Coins per good | Window, inclusive | Maximum payment |
| --- | ---: | ---: | --- | ---: |
| Royal Planks | 10 Planks | 25 | Days 0–120 | 250 |
| Royal Furniture | 4 Furniture | 100 | Days 0–180 | 400 |
| Royal Flour | 12 Flour | 20 | Days 0–240 | 240 |

The maximum combined commitment is 890 coins; the remaining initial authority funds are not implicitly spent elsewhere. Partial whole-unit deliveries consume the finite remaining entitlement. Actual selected-ship cargo must be docked at the specified harbor and manual control enabled. Goods are exported as a recorded physical sink and payment goes authority→company. Insufficient funds, stock, remaining entitlement or an expired/not-yet-open window blocks execution without replenishment. NPCs do not secretly bypass contract eligibility.

## Public Royal Mint and Silver

Silver is the single new canonical good. The forest-role private Silver Mine extracts one complete Silver per funded successful batch, every four days with the listed storage/wage constraints. It begins with no free Silver. The sole authorized Royal Mint is in the different processing role, so mining and monetary use require an actual voyage.

Commissioning the Mint pays **400 coins company→treasury**, once, and takes **four days**. It is public property, has no private factory warehouse and never appears in Company Value. Its delay is explicit and independent of private-facility Ascension timing. A pending Mint cannot issue currency.

An active ready Mint accepts exactly **one whole Silver** from the selected owned ship physically docked at its harbor. That Silver leaves cargo/FIFO ownership and enters the cumulative public reserve. Each authorized exchange creates **80 coins**: **60 to the supplier company, 20 to treasury**. The next exchange waits four days, with a lifetime maximum **24 batches** (24 Silver, 1,920 minted coins). Silver cannot be surrendered twice, pending construction cannot issue, and no timer issues without a real explicit delivery/exchange.

For a hand-checkable exchange, a company with 100 coins and one Silver, plus treasury 400, becomes company 160 and treasury 420, with one more Silver in the public reserve. Regional and all-account cash rise by exactly 80. The supplier's released Silver material basis and issuance receipt are distinct from ordinary trade revenue/material gross margin. The public share is counted once.

Public works may buy up to **20 delivered Planks at 25 coins each**, consuming the physical Planks. Payment comes only from actual treasury cash, including commissioning and public issuance shares. It is a bounded funded procurement, not an automatic household bailout. The separate circulation rules provide a bounded municipal levy/relief review. There is no bank, debt, credit line, Gold, Iron or inflation controller.

## Fleet construction and per-ship execution

The processing harbor shipwright can expand the player fleet to **three completed or pending ships in total**. Each new order costs **500 coins** and consumes **four actual local-market Planks** for the hull. Of the payment, 460 goes to the market and 40 to local households for labor. Insufficient cash/materials or the fleet cap rejects the order without any partial payment, stock removal or ship creation.

An order placed on D completes after daily voyages on **D+4**. The completed ship is empty, docked at the shipwright, has capacity ten whole goods and speed 100, and receives a deterministic unique ID. Pending capital is replaced by the completed asset exactly once. Private-facility Ascensions do not change this schedule.

All owned ships share company cash; each keeps separate cargo, dock/voyage, policy, enablement and priority. Historical routes retain their industrial stage and at most four dockside actions per chain. New v3 pooled runs expose [ordered cargo itineraries](AUTOMATION.md#ordered-cargo-itineraries): exact stop/action/pass state and at most eight operations per ship per day, including skips and departure. Manual and automated commands target the selected `shipId`, validate actual dock/ownership and share the ordinary trade/transfer/departure operations. Stable priority/ordinal-ID execution still runs through one staged world operation. Switching selection or editing a draft is passive. Saving a blocked itinerary retains its next action, budget and all unsold mixed cargo.

The player explicitly chooses ordered market and company-stock actions through two to four towns; NPC merchants keep their complementary two-port policies. Neither system optimizes a strategy. Returning with a local export can fund the importing town, but still needs actual output, destination demand, company liquidity and a viable margin. An empty-return import route can drain local accounts even when food arrives. The [seed-99 witness](AUTOMATION.md#bounded-ordinary-world-witness) measures four real loops and funded Bakery output over 60 days without retuning this economy.

## Reference Company Value and honest finance

```text
Company Value = company cash
              + reference value of all owned cargo and warehouse goods
              + completed ship reference value
              + completed private-facility reference value
              + paid pending ship/facility capital
```

Valuation version 1 uses the following stable coins/good: Grain **6**, Logs **10**, Flour **15**, Planks **20**, Bread **30**, Furniture **80**, Silver **40**. A ship is worth **50 coins per whole capacity unit**; a completed facility uses its authored replacement construction cost. Raw stock × raw marginal reference rate already produces monetary subunits. Wider sums are checked before returning the bounded public value.

Starter gifts have positive reference value but zero historical paid basis. Multiple stores are counted once each. FIFO held basis is not added again. Treasury, households, public Mint, other owners and surrendered Silver are excluded. No depreciation or liquidation simulation exists. A finite market might be unable to buy the holdings at all; reference value is never described as realizable proceeds.

A Merchant starts with 1,800 cash and a 500-coin reference ship: Company Value **2,300**. Ordering a 260-coin Mill leaves cash **1,540** and adds 260 of pending or completed capital, preserving the same total before other operations. Commissioning the public Mint instead reduces private value because the payment acquires no private asset. Shipbuilding's own payment/material flow remains separately auditable.

Material FIFO retains original interval slices: `floor(C*end/Q)-floor(C*start/Q)`. Fractional sale/transfer/production releases exact acquisition basis. Private wages remain period expense: `operating surplus after wages = realized material margin - all wages paid`, including wages for still-held output. Public commissions, Mint receipts and surrendered-metal basis remain separate. Company Value change is not sale revenue, realized margin or full net profit.

## Run identity, local saves and objectives

The current identity is v3 generator/content3, valuation1, current pricing, seed, charter, Ascension, Grand Commission draft or Sandbox, storage2, itinerary1 and circulation1. Retry recreates exactly that initial identity. Current schema5 saves restore the complete world without advancing it; historical modes and schemas are intentionally retired. See [save policy](SAVES.md).

Grand Commission is the sole scored challenge, with four mandatory drafted goals and100 liquid coins by day180. The [Journal](GRAND_COMMISSION.md) distinguishes cumulative accomplishments and reversible conditions. Sandbox has no deadline. Current charters retain their real endowments; public Mint assets never count as company property.

## Evidence and unresolved tuning

The separate `--expanding-economy` report and `--expanding-economy --check` are the reproducible comparison surface. Keep deep multi-seed sweeps opt-in. Compare current idle, one-way imports, complementary commerce, Mill/upstream expansion, external exports, Silver/Mint development and a multi-ship production network. Audits must track exact accounts/flows/FIFO and value alongside actual paid food, wages, population, prosperity and blockages.

In particular, increasing Flour capacity can expose Grain, Logs, payroll or transport as the next limit. Minted money without food can still fail; food without household income can remain on shelves. Public-fund expenditure and exported goods are actual transactions, not evidence by themselves that an economy is sustainable. The [measured experiment analysis](EXPANDING_ECONOMY_EXPERIMENTS.md) records actual productive recovery, additional Flour output, three challenge wins and later household-affordability collapse. These bounded results do not imply permanent growth. Distinguish automated tests, Editor screenshots and standalone-player interaction in the [acceptance record](REPOSITORY-CLEANUP.md).

GitHub Pages remains manual-only. This task neither publishes nor reconfigures it.

## New modern circulation revision

Task2.4 adds [economic rules revision1](ECONOMIC_CIRCULATION_REVIEW.md), independently of unchanged generator/content3 and valuation1. Fresh v3 host setup uses storage2/itinerary1/economy1 and schema5; historical versions are no longer supported. Retry preserves the current identity. This revision adds actual port handling expenses, one bounded municipal review, a Coverage luxury-target floor and finite role-specific starting imports. No recipes, goods, ships or automatic currency sources are added. Harbor displays exact gross and net settlement; Ledger separates wages, fees, FIFO, capital and Mint receipts. The separate `--economic-circulation --check` verifies matched experiments alongside the current productive-economy report.
