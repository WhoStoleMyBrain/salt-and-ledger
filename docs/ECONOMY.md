# Economy: goals and open questions

## Accepted modeling goals

The economy should support meaningful decisions through interacting finite systems. Markets have finite inventory, production, consumption, and liquidity. Player actions must affect availability and opportunities. Rules and information should let players reason about those effects.

Trading and industrial production should each offer viable specializations with opportunity costs. Production and trade should eventually support configurable automation so the player can move from individual transactions to higher-level planning. Content breadth is justified only when it adds decisions.

The simulation must support deterministic execution and eventually many headless runs for balance investigation. Determinism makes experiments reproducible; it does not by itself establish economic validity, good balance, or player enjoyment.

## Implemented sample

The canonical content contains two goods, `logs` and `planks`, a `sawmill`, and recipe `saw_planks`. One successful explicit production command consumes 2 logs and produces 1 plank atomically. Inventory quantities are bounded nonnegative integers.

This is a data-loading and production-consistency example. The conversion is not a balancing claim, a continuous production rate, or a model of physical units. There is no clock, price, cash, consumption schedule, labor, storage capacity, transport, or market. See [Mechanics](MECHANICS.md) for the command contract.

## Unresolved modeling choices

| Question | What a prototype should establish |
| --- | --- |
| Time and update order | Whether discrete steps or another model make production, consumption, and transactions understandable and repeatable |
| Price formation | How availability, demand, player impact, and transaction size create decisions without an opaque formula |
| Liquidity and money flows | Which entities hold cash, how payments move, and what sources or sinks keep scenarios viable |
| Consumption | How demand evolves, what unmet demand means, and whether players can anticipate it |
| Production cadence and costs | How time, labor, operating expenses, and capacity interact without adding repetitive management |
| Storage and transport | Which constraints create meaningful tradeoffs and how to present them clearly |
| Market information | What is observable, how stale information is communicated, and whether discovery improves decisions |
| Specialization | Whether trading and production each have useful advantages and vulnerabilities across scenarios |
| Automation policies | Which configurable rules reduce attention costs while preserving strategic choice |
| Recovery and failure | When a bad decision can be recovered from, and when it should end a run |

None of these questions is resolved by the sample ratio or by the choice of integer inventory arithmetic. Future money and price representation also remains open.

## Hypotheses to test

- Scarcity coupled with finite cash may create useful tradeoffs between immediate profit and maintaining a network's supply.
- A small set of interacting goods may provide enough strategic depth before adding more production chains.
- Clear reserve and trade limits may automate repetitive transactions while leaving route selection and specialization to the player.
- Multiple viable objective solutions may support fair challenge without tailoring every scenario to the player's build.

Test these with explicit scenarios, recorded assumptions, and comparable runs. Track measures such as stockouts, transaction failures, stranded cash, recoverability, and required player attention once the relevant systems exist. These measures are proposed experiment aids, not implemented instrumentation or accepted balance targets.

## Deferred

Procedural economy generation, economic viability scoring, advanced competitors, political systems, piracy, and combat belong to later milestones. They should not require placeholder types or unused parameters in the current production sample.
