# Economy: goals and open questions

## Accepted modeling goals

The economy should support meaningful decisions through interacting finite systems. Markets have finite inventory, production, consumption, and liquidity. Player actions must affect availability and opportunities. Rules and information should let players reason about those effects.

Trading and industrial production should each offer viable specializations with opportunity costs. Production and trade should eventually support configurable automation so the player can move from individual transactions to higher-level planning. Content breadth is justified only when it adds decisions.

The simulation must support deterministic execution and eventually many headless runs for balance investigation. Determinism makes experiments reproducible; it does not by itself establish economic validity, good balance, or player enjoyment.

## Implemented world prototype

The canonical content contains two goods, `logs` and `planks`, a `sawmill`, and recipe `saw_planks`. One successful explicit production command consumes 2 logs and produces 1 plank atomically. Inventory quantities are bounded nonnegative integers.

Task 1.1 places two independently identified sawmill instances at one fixed location with shared configurable stock. Each enabled mill attempts one batch per abstract integer tick. A host advances the clock explicitly. Commands enable or disable production before that tick's attempts; production runs by ascending priority and ordinal instance ID. A shortage is an expected outcome. Exceptional failures roll back the entire tick, including commands and earlier batches. See [Mechanics](MECHANICS.md) for the exact contract.

This models finite resources and reproducible competition, not production balance. The conversion has no physical units, and a tick has no hours-or-days interpretation. One batch per enabled building per tick is a temporary scheduling choice. Earlier buildings can repeatedly take scarce inputs before later buildings; fixed ordering deliberately makes this consequence visible without asserting that it is fair. A different allocation policy needs its own evidence and rules.

There is no price, cash, consumption schedule, labor, storage capacity, transport, or market. Without replenishment, sawmills stop producing after the initial logs are exhausted. Tick-level copying makes failure handling simple for this small prototype; larger-world cost and throughput have not been benchmarked.

## Unresolved modeling choices

| Question | What a prototype should establish |
| --- | --- |
| Meaning of time and additional phases | How abstract ticks map to economic time, and where future trade, arrivals, and consumption occur relative to production |
| Price formation | How availability, demand, player impact, and transaction size create decisions without an opaque formula |
| Liquidity and money flows | Which entities hold cash, how payments move, and what sources or sinks keep scenarios viable |
| Consumption | How demand evolves, what unmet demand means, and whether players can anticipate it |
| Production cadence, allocation, and costs | Whether one batch per tick and fixed priority need replacement; how labor, operating expenses, and capacity add decisions |
| Storage and transport | Which constraints create meaningful tradeoffs and how to present them clearly |
| Market information | What is observable, how stale information is communicated, and whether discovery improves decisions |
| Specialization | Whether trading and production each have useful advantages and vulnerabilities across scenarios |
| Automation policies | Which configurable rules reduce attention costs while preserving strategic choice |
| Recovery and failure | When a bad decision can be recovered from, and when it should end a run |

The discrete step and production order are implemented rules for this prototype. They do not resolve the remaining questions above, establish a balanced production rate, or promise fairness under scarcity. Future money and price representation also remains open.

## Proposed Task 1.2: finite markets and trading

Use a fixed headless scenario with one local market and one trader, finite inventories, and finite cash balances. Retain logs and planks. Start with an explicit fixed price per good as a test baseline; do not treat it as the final price model. Choose and document integer money units, quantity limits, transaction order, and whether trades occur before or after production before adding the behavior.

Implement explicit buy/sell commands that move goods and cash atomically. Insufficient stock, insufficient cash, invalid identifiers or amounts, and overflow must leave every participant unchanged. Define which conditions are expected rejections and which are errors, including how they interact with the existing tick atomicity contract. Stable command ordering must resolve competing requests reproducibly. Provide structured trade outcomes and headless scenarios demonstrating depletion, liquidity limits, and repeatability.

Test conservation of transferred goods and cash, exact affordability boundaries, repeated trades, reversed equivalent setup collections, and complete rollback on exceptional failure. No ships, travel, procedural generation, extra goods, dynamic price formula, or automation policies are needed to establish this contract. This is a proposed next task, not an implemented market or accepted final economic model.

## Hypotheses to test

- Scarcity coupled with finite cash may create useful tradeoffs between immediate profit and maintaining a network's supply.
- A small set of interacting goods may provide enough strategic depth before adding more production chains.
- Clear reserve and trade limits may automate repetitive transactions while leaving route selection and specialization to the player.
- Multiple viable objective solutions may support fair challenge without tailoring every scenario to the player's build.

Test these with explicit scenarios, recorded assumptions, and comparable runs. Track measures such as stockouts, transaction failures, stranded cash, recoverability, and required player attention once the relevant systems exist. These measures are proposed experiment aids, not implemented instrumentation or accepted balance targets.

## Deferred

Procedural economy generation, economic viability scoring, advanced competitors, political systems, piracy, and combat belong to later milestones. They should not require placeholder types or unused parameters in the current production sample.
