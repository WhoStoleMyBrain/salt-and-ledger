# Economy: goals and open questions

## Accepted modeling goals

The economy should support meaningful decisions through interacting finite systems. Markets have finite inventory, production, consumption, and liquidity. Player actions must affect availability and opportunities. Rules and information should let players reason about those effects.

Trading and industrial production should each offer viable specializations with opportunity costs. Production and trade should eventually support configurable automation so the player can move from individual transactions to higher-level planning. Content breadth is justified only when it adds decisions.

The simulation must support deterministic execution and eventually many headless runs for balance investigation. Determinism makes experiments reproducible; it does not by itself establish economic validity, good balance, or player enjoyment.

## Implemented world prototype

The canonical content contains two goods, `logs` and `planks`, a `sawmill`, and recipe `saw_planks`. One successful explicit production command consumes 2 logs and produces 1 plank atomically. Inventory quantities are bounded nonnegative integers.

Task 1.1 places two independently identified sawmill instances at one fixed location with shared configurable stock. Each enabled mill attempts one batch per abstract integer tick. A host advances the clock explicitly. Commands enable or disable production before that tick's attempts; production runs by ascending priority and ordinal instance ID. A shortage is an expected outcome. Exceptional failures roll back the entire tick, including commands and earlier batches. See [Mechanics](MECHANICS.md) for the exact contract.

This models finite resources and reproducible competition, not production balance. The conversion has no physical units, and a tick has no hours-or-days interpretation. One batch per enabled building per tick is a temporary scheduling choice. Earlier buildings can repeatedly take scarce inputs before later buildings; fixed ordering deliberately makes this consequence visible without asserting that it is fair. A different allocation policy needs its own evidence and rules.

Task 1.2 adds one finite local market and one trader. The existing location stock is the market inventory; production consumes and returns goods there, while the trader owns separate stock. This public-market production ownership is a prototype assumption, not a decision about player-owned factories. Both accounts have finite nonnegative `long` cash balances, with one integer unit as the smallest currency unit. Trades conserve goods and combined cash; production transforms goods without charging operating costs.

Task 1.2's fixed experimental bid/ask quotes remain available unchanged in runtime configuration. The trader buys at ask and sells at bid, with `Ask >= Bid > 0`; those quotes never respond to stock, production, or time. Task 1.2b additionally provides linear and piecewise scarcity curves that derive prices from market stock. All three modes share finite accounts, exact transaction accounting, and the existing world schedule. Currency naming, scale, initial balances, and pricing parameters remain experimental.

Controls run first, trades in declared order second, and production third. Sold logs can feed production immediately; newly produced planks can be traded next tick. This ordering is provisional. Insufficient stock or cash rejects one complete trade and allows later commands to continue. Invalid requests and arithmetic overflow roll back the whole tick, including earlier trades, cash, controls, and production.

There is no automatic stock or cash replenishment, external income, consumption schedule, labor, storage capacity, or transport. Sawmills stop when available logs are exhausted; finite liquidity can prevent trades even when stock exists. Tick-level copying makes failure handling simple for this small prototype; larger-world cost and throughput have not been benchmarked.

## Unresolved modeling choices

| Question | What a prototype should establish |
| --- | --- |
| Meaning of time and additional phases | How abstract ticks map to economic time; whether trade-before-production remains useful when arrivals and consumption exist |
| Price formation | Whether the tested stock-sensitive curves create understandable decisions; how reference values, depth, and production should be calibrated before considering demand |
| Liquidity and money flows | Whether a closed two-account baseline produces useful constraints; what future sources or sinks could keep scenarios viable |
| Consumption | How demand evolves, what unmet demand means, and whether players can anticipate it |
| Production cadence, allocation, and costs | Whether one batch per tick and fixed priority need replacement; how labor, operating expenses, and capacity add decisions |
| Storage and transport | Which constraints create meaningful tradeoffs and how to present them clearly |
| Market information | What is observable, how stale information is communicated, and whether discovery improves decisions |
| Specialization | Whether trading and production each have useful advantages and vulnerabilities across scenarios |
| Automation policies | Which configurable rules reduce attention costs while preserving strategic choice |
| Recovery and failure | When a bad decision can be recovered from, and when it should end a run |

The discrete step, integer money, trade-before-production order, and selectable fixed/stock-sensitive prices are implemented prototype rules. They do not resolve the remaining questions above, establish balanced production rates or prices, or promise fairness under scarcity.

## Task 1.2: accounting baseline

The `--market` demo uses logs bid/ask 2/3 and planks bid/ask 4/5, one sawmill, and four explicit ticks. It shows successful purchases/sales, inventory and funding rejections, same-tick production using sold logs, and repeatability. Starting combined cash is 27 and remains 27; all six combined initial logs become three combined planks through production. The final market holds 1 plank and 25 cash; the trader holds 2 planks and 2 cash. The [mechanics example](MECHANICS.md#executable-market-example) is checked by a headless test at every tick.

Tests cover all four stock/funds rejection outcomes, exact affordability, goods/cash conservation, numeric boundaries, dependent command ordering, complete exceptional rollback, culture-independent outcomes, and immutable state ownership. The scenario intentionally has no invented replenishment or income to rescue exhausted resources.

## Task 1.2b: dynamic pricing experiments

The headless `--pricing` runner compares three modes under equivalent initial conditions using only logs, planks, and the existing sawmill. Both dynamic modes are selectable in the actual world, not a parallel economic simulation. [Pricing experiments](PRICING_EXPERIMENTS.md) records exact parameters, reproducible scenarios, generated measurements, observed weaknesses, and the provisional recommendation.

| Model | Hypothesis | Limitation to examine |
| --- | --- | --- |
| Fixed baseline | Stable prices isolate accounting and finite-liquidity behavior | No inventory or transaction-size pressure |
| Linear inventory response | One slope around a target is easy to explain; larger purchases progressively cross more expensive stock transitions | Floors/ceilings flatten differences; reference/target/sensitivity still require calibration |
| Piecewise scarcity response | A stronger slope below one quarter of target makes severe shortage more consequential | A visible change in slope can make late purchases abruptly expensive after integer rounding; more behavior must be explained |

Each dynamic good has an immutable reference price, target stock, sensitivity, lower/upper bounds, and per-side spread. The scarcity knee is derived from the target and adds a fixed extra slope; it does not introduce a demand account or price clock. For targets below 8 its knee has no executable lower edge, so the scarcity model coincides with linear pricing. Target stock is a scale parameter, not a promise to restore inventory. [Mechanics](MECHANICS.md#inventory-sensitive-pricing-hypotheses) specifies exact transition boundaries, rational calculations, mathematical floor, and final-side clamps.

A bulk order sums prices across the inventory transitions it traverses. Reversing those same transitions pays no more than the original cost, and splitting an uninterrupted order preserves its exact total. These are permanent correctness requirements for the prototype. The formulas, knee, multiplier, spread, and numeric scenario choices are revisable hypotheses. A no-arbitrage test does not establish that the prices are balanced or that the resulting decisions are enjoyable.

Market-owned production removes logs and adds planks, so its completed snapshot can show more expensive logs and cheaper planks. Bounds or rounding can make small changes invisible. Quotes depend only on inventory/configuration, so idle ticks do not move prices. There is no external demand, replenishment, consumption, labor cost, or NPC flow to replenish depleted goods or redistribute cash. Finite cash may reject a seemingly attractive trade even when the price curve is well defined.

Read-only previews expose the exact proposed total and affordability/stock outcome using execution's own calculation. Current marginal quotes expose the next stock-feasible purchase and sale, omitting the impossible side at zero or maximum stock. No hidden price state or stale price cache is maintained. The eventual UI still needs to explain the first/last marginal prices, total, target, and bounds clearly; no trading UI is implemented here.

The recorded severe-scarcity case buys all 10 remaining logs for 1,020 under fixed pricing, 1,772 under linear pricing, and 3,632 under scarcity pricing. With trader cash of 2,000 the scarcity purchase is rejected while the other two succeed. This illustrates a real difference in liquidity pressure under the report's stated parameters; it is not a general balance target. **Use linear pricing as the provisional candidate for the next calibration task**, because it exposes stock/quantity pressure with one constant slope. Retain the scarcity alternative to test whether its stronger late-purchase penalties justify the knee and ceiling behavior. The [report](PRICING_EXPERIMENTS.md) supports that recommendation with the full comparison.

The next experiment should calibrate price depth against the existing 2-logs-to-1-plank conversion and finite account balances. Keep the fixed baseline and both hypotheses available while comparing player-visible tradeoffs. Do not add replenishment, consumption, extra goods, ships, or multiple ports merely to make a pricing curve appear active; each introduces separate assumptions.

## Hypotheses to test

- Scarcity coupled with finite cash may create useful tradeoffs between immediate profit and maintaining a network's supply.
- A small set of interacting goods may provide enough strategic depth before adding more production chains.
- Clear reserve and trade limits may automate repetitive transactions while leaving route selection and specialization to the player.
- Multiple viable objective solutions may support fair challenge without tailoring every scenario to the player's build.

Test these with explicit scenarios, recorded assumptions, and comparable runs. Track measures such as stockouts, transaction failures, stranded cash, recoverability, and required player attention once the relevant systems exist. These measures are proposed experiment aids, not implemented instrumentation or accepted balance targets.

## Deferred

Procedural economy generation, economic viability scoring, advanced competitors, political systems, piracy, and combat belong to later milestones. They should not require placeholder types or unused parameters in the current production sample.
