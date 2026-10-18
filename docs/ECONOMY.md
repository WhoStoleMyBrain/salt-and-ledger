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

Prices are explicit fixed experimental bid/ask quotes in runtime configuration. The trader buys at ask and sells at bid, with `Ask >= Bid > 0`; a same-market round trip cannot increase trader cash. Quotes never respond to supply, demand, stock, production, or time. They are an accounting baseline, not dynamic pricing or final balance. Currency naming, scale, initial balances, and demo prices remain experimental.

Controls run first, trades in declared order second, and production third. Sold logs can feed production immediately; newly produced planks can be traded next tick. This ordering is provisional. Insufficient stock or cash rejects one complete trade and allows later commands to continue. Invalid requests and arithmetic overflow roll back the whole tick, including earlier trades, cash, controls, and production.

There is no automatic stock or cash replenishment, external income, consumption schedule, labor, storage capacity, or transport. Sawmills stop when available logs are exhausted; finite liquidity can prevent trades even when stock exists. Tick-level copying makes failure handling simple for this small prototype; larger-world cost and throughput have not been benchmarked.

## Unresolved modeling choices

| Question | What a prototype should establish |
| --- | --- |
| Meaning of time and additional phases | How abstract ticks map to economic time; whether trade-before-production remains useful when arrivals and consumption exist |
| Price formation | How availability, demand, player impact, and transaction size create decisions without an opaque formula |
| Liquidity and money flows | Whether a closed two-account baseline produces useful constraints; what future sources or sinks could keep scenarios viable |
| Consumption | How demand evolves, what unmet demand means, and whether players can anticipate it |
| Production cadence, allocation, and costs | Whether one batch per tick and fixed priority need replacement; how labor, operating expenses, and capacity add decisions |
| Storage and transport | Which constraints create meaningful tradeoffs and how to present them clearly |
| Market information | What is observable, how stale information is communicated, and whether discovery improves decisions |
| Specialization | Whether trading and production each have useful advantages and vulnerabilities across scenarios |
| Automation policies | Which configurable rules reduce attention costs while preserving strategic choice |
| Recovery and failure | When a bad decision can be recovered from, and when it should end a run |

The discrete step, fixed quotes, integer money, and trade-before-production order are implemented prototype rules. They do not resolve the remaining questions above, establish balanced production rates or prices, or promise fairness under scarcity.

## Task 1.2: accounting baseline

The `--market` demo uses logs bid/ask 2/3 and planks bid/ask 4/5, one sawmill, and four explicit ticks. It shows successful purchases/sales, inventory and funding rejections, same-tick production using sold logs, and repeatability. Starting combined cash is 27 and remains 27; all six combined initial logs become three combined planks through production. The final market holds 1 plank and 25 cash; the trader holds 2 planks and 2 cash. The [mechanics example](MECHANICS.md#executable-market-example) is checked by a headless test at every tick.

Tests cover all four stock/funds rejection outcomes, exact affordability, goods/cash conservation, numeric boundaries, dependent command ordering, complete exceptional rollback, culture-independent outcomes, and immutable state ownership. The scenario intentionally has no invented replenishment or income to rescue exhausted resources.

## Proposed Task 1.2b: Dynamic Market Pricing Experiments

Compare explicit stock-sensitive pricing hypotheses against this fixed-price baseline using small reproducible scenarios. Define quote timing, transaction-size effects, bounds, rounding, and bid/ask spread before implementing a candidate. Test conservation and whole-tick rollback unchanged, and measure stockouts, rejected trades, cash concentration, and round-trip opportunities. A useful experiment should explain how decisions change, not merely show prices moving.

No dynamic formula is selected or implemented. Do not assume automatic replenishment, consumption, extra goods, ships, or multiple ports are necessary for the first comparison; each would introduce additional assumptions requiring separate evidence.

## Hypotheses to test

- Scarcity coupled with finite cash may create useful tradeoffs between immediate profit and maintaining a network's supply.
- A small set of interacting goods may provide enough strategic depth before adding more production chains.
- Clear reserve and trade limits may automate repetitive transactions while leaving route selection and specialization to the player.
- Multiple viable objective solutions may support fair challenge without tailoring every scenario to the player's build.

Test these with explicit scenarios, recorded assumptions, and comparable runs. Track measures such as stockouts, transaction failures, stranded cash, recoverability, and required player attention once the relevant systems exist. These measures are proposed experiment aids, not implemented instrumentation or accepted balance targets.

## Deferred

Procedural economy generation, economic viability scoring, advanced competitors, political systems, piracy, and combat belong to later milestones. They should not require placeholder types or unused parameters in the current production sample.
