# Economy: goals and open questions

## Accepted modeling goals

The economy should support meaningful decisions through interacting finite systems. Markets have finite inventory, production, consumption, and liquidity. Player actions must affect availability and opportunities. Rules and information should let players reason about those effects.

Trading and industrial production should each offer viable specializations with opportunity costs. Implemented route and private-production policies support the move from individual transactions to higher-level planning; their effect on player attention still needs observed playtesting. Content breadth is justified only when it adds decisions.

The simulation must support deterministic execution and eventually many headless runs for balance investigation. Determinism makes experiments reproducible; it does not by itself establish economic validity, good balance, or player enjoyment.

## Legacy world prototypes

The original fixture uses `logs`, `planks`, `sawmill`, and `saw_planks`. One successful production command consumes 2 logs and produces 1 plank atomically. Task 1.5 additionally defines `lumber_camp` and input-free `harvest_logs`; older fixtures do not acquire extraction or consumption automatically. Inventory quantities remain bounded nonnegative integers.

Task 1.1 places two independently identified sawmill instances at one fixed location with shared configurable stock. Each enabled mill attempts one batch per abstract integer tick. A host advances the clock explicitly. Commands enable or disable production before that tick's attempts; production runs by ascending priority and ordinal instance ID. A shortage is an expected outcome. Exceptional failures roll back the entire tick, including commands and earlier batches. See [Mechanics](MECHANICS.md) for the exact contract.

This models finite resources and reproducible competition, not production balance. The conversion has no physical units, and a tick has no hours-or-days interpretation. One batch per enabled building per tick is a temporary scheduling choice. Earlier buildings can repeatedly take scarce inputs before later buildings; fixed ordering deliberately makes this consequence visible without asserting that it is fair. A different allocation policy needs its own evidence and rules.

Task 1.2 adds one finite local market and one trader. The existing location stock is the market inventory; production consumes and returns goods there, while the trader owns separate stock. This public-market production ownership is a prototype assumption, not a decision about player-owned factories. Both accounts have finite nonnegative `long` cash balances, with one integer unit as the smallest currency unit. Trades conserve goods and combined cash; production transforms goods without charging operating costs.

Task 1.2's fixed experimental bid/ask quotes remain available unchanged in runtime configuration. The trader buys at ask and sells at bid, with `Ask >= Bid > 0`; those quotes never respond to stock, production, or time. Task 1.2b additionally provides linear and piecewise scarcity curves that derive prices from market stock. All three modes share finite accounts, exact transaction accounting, and the existing world schedule. Currency naming, scale, initial balances, and pricing parameters remain experimental.

Controls run first, trades in declared order second, and production third. Sold logs can feed production immediately; newly produced planks can be traded next tick. This ordering is provisional. Insufficient stock or cash rejects one complete trade and allows later commands to continue. Invalid requests and arithmetic overflow roll back the whole tick, including earlier trades, cash, controls, and production.

Task 1.3a adds exactly two handcrafted local markets and one manually controlled ship under a single clock and trader account. Cargo capacity counts every item as one unit; the ship is the trader's only goods inventory. Trading requires docking at the intended port. Production and travel run on successful abstract ticks, so local inventory and quotes can change while cargo is underway. There are no per-port trader warehouses or hidden transfers.

These legacy fixtures have no extraction, cash replenishment, external income, consumption schedule, labor, or travel operating cost/risk. Sawmills stop when available logs are exhausted; finite liquidity can prevent trades even when stock exists. Snapshot copying makes failure handling simple for these small worlds; larger-world cost and throughput have not been benchmarked.

## Unresolved modeling choices

| Question | What a prototype should establish |
| --- | --- |
| Meaning of time and additional phases | Whether regional days and the explicit ship/procurement/production/sales/consumption/arrival phases create understandable decisions; legacy ticks remain abstract |
| Price formation | Whether the tested stock-sensitive curves create understandable decisions; how reference values, depth, and production should be calibrated before considering demand |
| Liquidity and money flows | Whether finite market, household and company accounts circulate enough actual money; identify deadlocks before proposing any explicit outside source/sink |
| Consumption | Whether paid food/luxury demand, municipal own-use and separate stock/affordability failures create understandable pressure |
| Production cadence, allocation, and costs | Whether current regional intervals, input competition, construction costs, and fixed ordering create useful tradeoffs; whether modeled production wages and still-unmodeled expenses create useful decisions |
| Storage and transport | Whether finite private storage, item-count cargo capacity, and geographic voyage durations create understandable tradeoffs; whether additional constraints earn their complexity |
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

Read-only previews expose the exact proposed total and affordability/stock outcome using execution's own calculation. Current marginal quotes expose the next stock-feasible purchase and sale, omitting the impossible side at zero or maximum stock. No hidden price state or stale price cache is maintained. Task 1.4 presents first/last marginal prices, exact totals, and pricing parameters in TradingLab; whether players understand them remains a playtest question.

The recorded severe-scarcity case buys all 10 remaining logs for 1,020 under fixed pricing, 1,772 under linear pricing, and 3,632 under scarcity pricing. With trader cash of 2,000 the scarcity purchase is rejected while the other two succeed. This illustrates a real difference in liquidity pressure under the report's stated parameters; it is not a general balance target. **Use linear pricing as the provisional candidate for the next calibration task**, because it exposes stock/quantity pressure with one constant slope. Retain the scarcity alternative to test whether its stronger late-purchase penalties justify the knee and ceiling behavior. The [report](PRICING_EXPERIMENTS.md) supports that recommendation with the full comparison.

Price depth still needs calibration against the existing 2-logs-to-1-plank conversion and finite account balances. Keep the fixed baseline and both hypotheses available while comparing player-visible tradeoffs. The transport prototype below adds explicitly requested logistical assumptions; it does not justify adding replenishment, consumption, or extra goods merely to make a pricing curve appear active.

## Task 1.3a: manual maritime trading

The [shipping comparison](SHIPPING_DEMO.md) uses linear pricing provisionally in both ports, with abundant Northport logs, a scarce Southhaven market, and a Southhaven sawmill. One trader starts with 100 cash; each public market starts with 1,000. One ship holds at most 6 items and takes two ticks each way. Two equivalent manual decisions transport 2 or 6 logs, sell them at Southhaven, and return with 2 planks. Each ends at tick 6, back at Northport with no cargo.

The smaller expedition buys goods for 51 and sells them for 120, realizing gross profit **69**. The full-log expedition buys for 89 and sells for 208, realizing gross profit **119**. Both return legs contribute 29. The full load earns more total log profit but less per log: 15 versus 20. Those exact outcomes are executable scenario assertions, not independently maintained balance calculations. The documented parameters and per-tick output explain every cost, marginal price, stock change, and finite cash balance.

Equal final empty cargo makes realized sale revenue minus purchase cost comparable to the trader's cash change. The markets finish in different states: the full log delivery supports two extra public-market planks. Those unsold market goods do not belong to the trader and are not counted as its profit. A comparison with unsold ship cargo would need an explicit valuation or matched liquidation, not just a comparison of cash balances.

Local price differences allow transport profit without violating the same-market, same-edge no-profitable-cycle contract. The ship moves goods between different finite inventories at different configured prices, and production can change those inventories between transactions. Buying reduces source stock, selling deepens destination stock, and finite cargo/cash limit how much of an apparent opportunity can be taken. A visible remote quote is perfect **current** information; it is not a guaranteed arrival-time price. Idle time alone never changes a quote.

This establishes a working manual trading loop, not a balanced economy or an optimal strategy. The map is handcrafted; only one trader and ship exist. Capacity uses equal item counts without weight, production belongs to the market, information is immediately visible, travel has no costs or risk, and there is no automatic replenishment, consumption, or competitor flow. These assumptions make the comparison inspectable and remain provisional. Task 1.3b tests explicit route policies against these same constraints without expanding the economic model.

## Task 1.3b: configured route execution

The [automation comparison](AUTOMATION.md#measured-scenarios) retains the manual markets and exact transaction/movement rules. A player chooses commodities, loading targets/minimums, worst acceptable marginal buy/sell prices, an optional cash reserve, and whether an empty return is acceptable. The stateless policy issues existing commands and explains why it acts or waits; it does not choose a globally optimal strategy or predict arrival-time prices.

With an outbound marginal buy limit of 10, the policy buys six logs for 54 and reproduces the full manual expedition's **119 gross profit in six ticks**. Lowering only that limit to 8 selects two logs for 16 and reproduces **69 in six ticks**. Both return with two planks and finish with empty cargo. Equivalent manual/automatic states match at every tick; the policy earns no special execution privileges. Four useful manual action batches become one route configuration, while both still require six host advances. This prescribed-input count is not a user study or proof of enjoyable automation.

In the blocked variant, Southhaven starts with zero cash. The ship buys six logs for 54, arrives, and retains its cargo through three `InsufficientMarketFunds` waits. The bounded observation ends at tick 5 with trader cash 46 and all six logs unsold. Its -54 cash change is not reported as realized expedition profit or loss: the accounting interval remains open. There is no sale revenue, rejected transaction, forced departure, price override, or market repair.

Finite liquidity and depletion can therefore stop an otherwise valid route. Full and restrictive expeditions also face higher Northport asks afterward; their next loading evaluation waits at the chosen limit. Automation makes this consequence repeatable and observable, not economically sustainable. Future experiments should distinguish temporary waits, limits the player wishes to change, and conditions with no recovery under the existing closed model. Price calibration, initial stock/liquidity, attention cost, and recoverability remain hypotheses, not reasons to add hidden replenishment.

## Task 1.4: playable economic inspection

The [TradingLab playtest](PLAYTEST.md) presents the same finite two-port world with manual trade, explicit travel/time, and policy controls. Standard and restrictive starting comparisons retain the six-tick 119/69 gross-profit outcomes. The zero-cash destination retains unsold cargo and explains its inability to fund a sale. These are existing fixture variants, not additional goods, economic rules, or balance changes.

The player sees each market's current stock, liquidity, and marginal quotes alongside complete trade previews. A purchase can cross several inventory edges, and a remote quote can change before arrival when production changes stock. The display keeps cash changes separate from realized gross profit while cargo remains unsold. It does not value cargo at a hypothetical liquidation price or treat public-market output as trader wealth.

Policy-limit recovery is deliberately distinct from economic recovery. Raising an overly restrictive buy ceiling changes what the next policy evaluation permits; it creates no cash or goods and consumes no tick. An illiquid destination remains unable to pay after a sale-limit edit. Reset constructs a new experiment world and is labeled accordingly. A bounded stop after unchanged ticks limits observation; it does not prove that all possible manual strategies are deadlocked.

The Task 1.4 recommendation was to observe whether people can predict bulk cost, diagnose price versus liquidity waits, and reconfigure deliberately. Task 1.5 now adds explicitly requested economic flows and geographic competition; that earlier human-understanding question remains unanswered by deterministic tests.

## Task 1.5: a living regional experiment

[Living archipelago](LIVING_ARCHIPELAGO.md) records the complete shared configuration and measured sixty-day comparisons. Northport and Westhaven create logs through canonical extraction. Southhaven converts logs on a two-day schedule. Three towns consume planks on explicit cadences without modeled payment. Two independent merchants purchase, carry, and sell goods under finite cash/capacity and price constraints. The player competes in the same inventories; no private NPC markets or hidden subsidies exist.

The day scale applies only to the new regional owner. Dockside trades and sailing orders are instantaneous revisioned commits. Daily advancement runs bounded merchant/player policies, production, physical consumption, and travel under one atomic clock. Source/transformation/sink records replace a false global goods-conservation assumption, while cash remains exactly conserved. The starting cash total is 13,600 in all strategy comparisons.

With no player intervention, the Timber Merchant completes nine shipments and the Plank Merchant seven over sixty days. Extraction creates 44 logs; processing uses 48 logs to make 24 planks; towns consume 16 planks and record 21 unmet units. Westhaven reaches its log ceiling, while Northport and Westhaven lack plank deliveries. This shows recurring production and shipping alongside distribution failures, not a fully balanced network.

The aggressive player route finishes with cash 552 and three unsold planks, and reduces completed NPC shipments from 16 to 5 by competing for actual stock. Switching to Westhaven finishes with cash 672 and three logs still aboard, enables more extraction, and reduces unmet demand to 10. Manual West exports realize 45 with empty final cargo; extra logs lower Southhaven's bids and can make further sales unattractive to merchants with a sale floor. Restrictive automation spends no money because its buy limit never permits a useful shipment after scheduled competition.

Closed empty-cargo intervals realize 412 and 485 for the aggressive/switching strategies, respectively. Their remaining cargo positions stay open; final cash changes are not a valuation of the held goods. The report separately records purchase costs, sale revenue, physical deliveries, account balances, sailing days, and input counts. These comparisons are outcomes for one configured world, not a ranking of universally optimal strategies.

The new demand is only physical consumption: it models neither households paying shops nor wages replenishing buyers. Market cash can drain over a sufficiently long run, and thresholds/price floors can strand cargo even while another town has demand. The baseline's sixty-day activity and a 600-day finite-accounting regression establish neither perpetual sustainability nor appropriate economic scale. Next investigations should measure liquidity distribution over longer horizons and observe whether players understand source, price, and delivery constraints before introducing a monetary system.

## Task 1.6: capital, ownership, and supply

The [enterprise experiment](ENTERPRISE.md#reproduce-the-experiment) adds four canonical goods and connected production, one grain/flour merchant, paid private construction, finite factory stock, and local procurement/sales. Companies now own cash independently of ships. Construction transfers cash to the local market; private production retains ownership until actual sale. Physical ship transfers move no cash. Combined initial cash 25,800 is conserved per operation across all accounts.

Ninety-day outcomes reveal different capital choices: merchant cash ends 1,029, a workshop 356 after 360 capital, local food production 705 after 300 capital, and manufacturing/shipping 1,637 after 300 capital. The report retains unsold goods, actual purchases, production, and sale revenue separately. These are cash comparisons under authored assumptions, not precise investment profitability rankings or evidence that manufacturing is always superior.

The optional charter requires 24 net bread supplied to Eastport by day 60 with at least 100 cash. The merchant and hybrid strategies succeed on days 29 and 46 respectively; waiting fails. Net sales subtract destination buybacks, so recirculating the same market goods does not generate supply credit. This objective measures additional market supply rather than household consumption. Consumption remains a physical sink without modeled payment.

Task 1.6 deferred full historical cost basis; Task 1.7 implements the attribution described below across private manufacturing and transfers. Exact proposed execution averages are supported independently of that accounting question. Finite liquidity, public/private competition, factory capacity, delayed production, and travel remain genuine constraints without hidden replenishment.

## Task 1.7: complete runs and attributed results

The [run experiment](experiments/run-results.json) crosses three fixed challenges with three replacement starting charters. Each active example succeeds, and every matched idle case fails at day 60. This demonstrates feasible strategies on the current authored economy; it is not proof that all policies work, that every package is equally difficult, or that the choices are balanced.

### Starting endowments and opportunity costs

The three packages all use the same map and configured ship speed. They differ in cash, hold size, location, and granted assets:

| Charter | Liquid cash | Ship and location | Granted assets | Partial replacement estimate, excluding ship |
| --- | ---: | --- | --- | ---: |
| Merchant Company | 600 | 10-unit hold, Northport | None | 600 |
| Industrial Consortium | 300 | 8-unit hold, Westhaven | Bakery; 2 Flour and 2 Logs in its warehouse | 645 |
| Independent Traders | 480 | 12-unit hold, Northport | 4 Grain and 2 Logs aboard | 523 |

The industrial estimate is 300 cash + the bakery's 300 construction quote + initial Westhaven replacement purchases of 2 Flour for 29 and 2 Logs for 16. The independent estimate is 480 cash + initial Northport replacement purchases of 4 Grain for 23 and 2 Logs for 20. These goods totals were obtained from successful authoritative trade previews. They are finite initial asking-price replacement estimates, not transactions performed during initialization, historical paid basis, liquidation values, or guaranteed future quotes.

Ship prices and resale are unmodeled, so the table deliberately excludes ship valuation and cannot establish equal total wealth. The industrial package exchanges liquidity and hold capacity for an existing production foothold; its factory needs real replenishment after starter inputs run out. The merchant package can immediately fund flexible shipments or decide where to invest. The independent package offers a larger hold and useful starting goods, but less cash and initially occupied cargo space. These are lateral opportunities with real constraints rather than stacking income bonuses.

### Measured challenge outcomes

All three goals use day 60 and at least 100 company cash as their deadline/solvency conditions. Coastal Supply requires net 24 Bread into Eastport; Regional Relief requires 24 actual Bread consumed by scheduled demand across the region; Enterprise Ambition requires private production of 12 Bread and 200 realized gross margin.

| Challenge | Charter / demonstrated strategy | Completion day | Cash | Attributed gross margin | Score |
| --- | --- | ---: | ---: | ---: | ---: |
| Coastal Supply | Merchant / merchant shipping | 29 | 1,014 | 414 | 1,910 |
| Coastal Supply | Industrial / factory shipping | 39 | 1,027 | 814 | 1,810 |
| Coastal Supply | Independent / merchant shipping | 29 | 918 | 438 | 1,901 |
| Regional Relief | Merchant / merchant shipping | 48 | 1,114 | 514 | 1,720 |
| Regional Relief | Industrial / factory shipping | 56 | 1,210 | 1,022 | 1,640 |
| Regional Relief | Independent / merchant shipping | 48 | 1,018 | 538 | 1,720 |
| Enterprise Ambition | Merchant / constructed bakery | 15 | 475 | 286 | 1,997 |
| Enterprise Ambition | Industrial / starter bakery | 15 | 515 | 337 | 2,001 |
| Enterprise Ambition | Independent / constructed bakery | 21 | 423 | 310 | 1,932 |

The merchant and industrial Coastal examples reach the same net-delivery goal through materially different physical chains. The industrial case still holds 87 material cost in remaining inventory at completion. Its 814 gross margin is therefore not `final cash - initial cash`; paid inventory remains tied up, and some starting materials were grants. The ambition cases likewise include remaining basis and, where a bakery was purchased, 300 capital expenditure. Detailed held goods, costs, construction, events, and player/automation inputs remain in the report.

Idle Regional Relief satisfies only two Bread of scheduled consumption, so ambient NPC/public activity cannot win its 24-unit goal. Idle industrial ownership can manufacture four Bread from grants, but has no realized sales margin and cannot win Enterprise Ambition. Neither demand requests, unsold output, transfers, nor repeated target-market buying/reselling manufacture the required outcome.

The exact score is documented in [Mechanics](MECHANICS.md#starting-charters-outcomes-and-scoring): at most 500 capped objective points, 1,000 victory points, up to 600 time points on success, and up to 100 solvency points. For the merchant Coastal result, `500 + 1000 + (60 - 29) * 10 + 100 = 1910`. The score is bounded and compares within the challenge/charter group. Different starter grants and capabilities make a universal cross-package profit or score ranking misleading.

### What historical accounting establishes

FIFO receipt queues retain actual paid or material cost through partial trades, physical transfers, and production. Released cost and still-held basis reconcile to purchases; construction stays separate. The [accounting contract](MECHANICS.md#historical-inventory-costs-and-committed-events) defines exact remainder allocation and auditable committed events. Starter grants retain their origin rather than receiving a fabricated current-price basis.

This supports attributed gross margin and an honest held-cost display. That historical measurement has no wages, fuel, transport fees, labor-productivity costs, depreciation, taxes or realizable ship/factory valuation. Living Cities separately records actual wages without changing material FIFO; operating surplus after wages is not full profit. A sale of a granted good can have positive gross margin against zero paid basis without proving a superior return on total starting wealth. Keep endowments, physical holdings, liquidity, capital, and margin visible together.

Automation connects private warehouses to real shipping and ordinary markets, with measured manual-equivalent transitions and finite blocked cases in the [automation experiments](AUTOMATION.md). This reduces prescribed repetitive commands without improving prices, speed, or resource access. Actual player comprehension and enjoyable strategic choice still require observed playtesting.

## Task 1.8: measured geography and construction pressure

The [seeded run experiment](SEEDED_RUNS.md#reproduce-the-experiment) holds economic configuration fixed while permuting four bounded geographic slots and deriving six integer route distances. Ascension 1 delays new player facilities by two complete days; starter grants remain operational. The normal report covers 25 signed seeds plus Classic, all three challenges/charters, both ascensions, and idle, merchant, local-factory and factory-shipping policies: 1,872 finite trials. Each operation reconciles combined/company cash, physical extraction/transformation/consumption, trade transfers and FIFO basis. These are actual simulation runs, not an approximate profitability model.

All 450 seeded challenge/charter/ascension combinations in the normal sample have a winning benchmark strategy. Idle wins none. That result is empirical sample coverage, not a guarantee of victory for every seed or player policy; the generator separately proves bounded, connected geography. No stock, liquidity, production, demand, recipe, objective or charter value was adjusted per seed or across the milestone.

An executed 125-seed deep batch covers 9,072 total trials including Classic and finds a benchmark victory for all 2,250 seeded combinations. It reproduces the normal sample's strategy outcome pattern and completion-day ranges. Of its 375 Ascension 1 industrial Regional Relief trials, 130 succeed exactly on day 60; none do so at Ascension 0. This is measurable deadline exposure without a claim that every untested seed or arbitrary strategy is feasible.

### Geography changes the fastest strategy

For Merchant Company and Coastal Supply at Ascension 0, seed **99** favors the tested merchant route in completion time: day 28 versus day 39 for paid private industry and shipping. Seed **-42** reverses that relationship: merchant day 43 versus industrial day 28. Identical goals and starting assets therefore produce a real route/investment timing tradeoff.

Cash and material margin answer different questions. On seed -42 the merchant finishes with 1,118 cash and 518 realized material margin; industry finishes with 926 cash and 697 margin after paying 300 construction capital. Its held material basis remains attached to unsold inventory. On seed 99 the comparable figures are 984 cash/384 margin versus 861 cash/769 margin. Neither the factory nor cargo receives an invented liquidation value. [Full measurements](SEEDED_RUNS.md#measured-difficulty-and-efficiency) retain the distinction.

### Local profitability is not objective completion

The local Westhaven bakery earns positive material margin and completes Enterprise Ambition, but loses Coastal Supply because its sales occur in the wrong town. It also loses Regional Relief with **22/24** consumed Bread: Westhaven's six-day demand interval permits only 20 Bread consumed by day 60, and two initial units are consumed elsewhere in these scripts. Producing more into the same town cannot create another scheduled demand opportunity. Physical shipping is the missing action, not a fabricated manufacturing subsidy or a global recipe buff.

Procurement targets, marginal limits, company reserves, private stock, voyage duration and finite destination liquidity all remain operative. The experiment records blocked policy decisions and private procurement/sales reasons separately from actual rejected trades. A wait can prevent a purchase before any transaction is submitted; counting only rejected orders would hide that pressure. Held output and paid inputs remain on the balance explanation rather than being labeled realized profit.

### Slow Foundations is a narrow modifier

Purchased two-day-interval bakeries ordered on day 0 complete on day 2 and first produce no earlier than day 4, compared with day 2 on Ascension 0. In the normal seeded sample, industrial Coastal victories range from days 28–44 at Ascension 0 and 28–46 at Ascension 1. Industrial Regional Relief ranges move from 52–56 to 52–60; some Ascension 1 runs finish exactly on their deadline. Local-factory Enterprise Ambition finishes on day 12 normally and days 12–14 under Ascension 1.

Industrial Consortium's inherited bakery retains its historical schedule and grants at both difficulties. Pure merchants also receive no penalty when they build nothing. All sampled merchant Coastal/Relief trials still win, so enabling a suitable default trading route remains easy. The modifier is not a claim of globally harder challenges, fairer starting packages or uniformly lower profit. Seed -42's purchased-factory run actually finishes with higher cash/margin under Ascension 1 despite finishing two days later, because its market timing changes.

The calibration proposal remains conservative: retain existing prices, recipes, capital and deadlines, then test one explicit pressure at a time in a future tuning task. Compare local-factory and shipping decisions under the same charter/seed/ascension and report both liquidity and held basis. Observe whether people understand the consequences before changing several economic parameters at once. Human play difficulty, enjoyment and longer-horizon sustainability remain unresolved.

## Task 1.9: finite circulation and economic roles

[Living Cities](LIVING_CITIES.md) adds an explicitly separate v2 experiment: town household accounts pay exact local retail asks; successful employers fund actual wages; population scales need; sustained meals and purchasing power affect prosperity and durable demand. Municipal Planks remain an own-use sink. A permutation of authored production/capability packages creates real import dependencies, while Classic and geography-only v1 retain their historical rules and report bytes.

Every committed cash total includes companies, markets and households once. Private labor is expensed separately from material FIFO: `OperatingSurplusAfterWages` subtracts all wages paid from realized material margin, including wages for held output. This is an exact supported cash-cost measure, not full matched profit. No future wage income, credit, tax, depreciation, freight cost or subsidy is inferred.

The normal experiment executes 36 one-year comparisons, two five-year idle controls, 18 recovery trials and nine controlled mechanisms. Real merchant and private-production strategies win all 12 active recovery cases; idle loses all six. In seed 42 at Ascension 0, StockOnly/Coverage merchant cash ends at 814/1,171 with 91/105 regional Bread consumed; constructed industry ends at 95/74 with 131/135 consumed. Greater player liquidity and greater regional welfare are distinct outcomes. The one coverage-target hypothesis changes behavior without resolving long-run finite-money pressure.

The optional 25-seed deep sweep executes 300 three-year comparisons. All towns reach the population floor of 10 under those prescribed strategies, and 100 of 300 worlds have no market Bread left. Five- and ten-year seed-42 idle balances and stocks match: a processing market holds five Logs but only six cash against a ten-cash wage, while households still hold money. These are distribution/payroll stalls, not broken conservation, and do not prove that every possible manual recovery path fails. No hidden balancing rescue was applied.

Controlled examples show both a closed wage→retail cycle (4,380 each over 365 days) and a hungry, wealthy household sector unable to fund its market-owned employer's missing one cash. They also demonstrate real retail recapitalization of a market, partial food purchases, durable demand switching off after deprivation despite remaining wealth, and population increasing desired food. The [detailed evidence](LIVING_CITIES.md#what-the-experiments-actually-show) distinguishes these mechanisms and records performance, exact strategy assumptions and limits.

The next economic decision should address one measured failure at a time: whether players can recognize and deliberately redirect stranded money and goods, and whether a narrowly defined policy or production change sustains useful circulation without erasing finite risk. Current results support a coherent, inspectable prototype, not full balance or a promise of universal solvency.

## Hypotheses to test

- Scarcity coupled with finite cash may create useful tradeoffs between immediate profit and maintaining a network's supply.
- A small set of interacting goods may provide enough strategic depth before adding more production chains.
- Clear reserve and trade limits may automate repetitive transactions while leaving route selection and specialization to the player.
- Multiple viable objective solutions may support fair challenge without tailoring every scenario to the player's build.

Test these with explicit scenarios, recorded assumptions, and comparable runs. Current reports already record stockouts, affordability failures, account balances, waits and recovery outcomes. Required human attention and enjoyment still need observed playtesting; neither instrumentation nor deterministic victories establish accepted balance targets.

## Deferred

Task 1.9 implements constrained v2 role generation and explicit structural checks. Universal economic viability scoring, advanced competitors, city ownership, politics, piracy and combat remain future work. They should not require placeholder types or unused parameters in the current production sample.

## V3: distinguish productive capacity, circulation and issuance

[Expanding Economy](EXPANDING_ECONOMY.md) addresses v2's unexpandable Flour ceiling through legal private Mills and upstream extraction. All existing building types have a place in the role matrix, and multiple same-kind sites can coexist. Production capacity is potential, not free output: materials, wages, storage and actual distribution remain finite constraints. Fractional population-proportional retail makes paid food volume comparable across nearby population values.

Three financial questions are deliberately separate. **Circulation** transfers existing coins between markets, households and companies. **External contracts** import existing cash from a visible finite outside account in exchange for physically exported goods. **Mint issuance** creates bounded coins only against physically transported and surrendered Silver, splitting them between supplier and public treasury. Population and consumption trigger no automatic issuance. Public works spend only treasury funds on actual finite deliveries.

Company Value measures cash and owned goods/capital at declared stable references. It can rise through productive goods or capital without increasing regional money; it can also fall while a company earns sale revenue. It is neither guaranteed liquidation proceeds nor material FIFO basis. Mint receipts, wages, construction and held output must remain visible separately from realized material margin.

Required comparison questions include: does complementary export trade fund a food-importing town for a measured period; which input or payroll constraint follows an extra Mill; what does a second ship actually deliver; how much finite outside budget remains; where do minted coins circulate; and can households with those coins obtain actual food? Keep v2 deterioration, v3 idle, overbuilding, unsupported employers and food-rich/household-poor counterexamples. A green account invariant establishes conservation, not sustainable welfare or enjoyable strategy. New observed outcomes belong to the separate v3 report; no economic balance claim follows from authored parameters alone.

## Measured circulation follow-up

[Task2.4 circulation review](ECONOMIC_CIRCULATION_REVIEW.md) diagnoses the actual terminal day140 fixture and separates wealth concentration, municipal payroll constraints, missing physical food and price-target pathology. New versioned rules introduce bounded real transfers, not account equalization. Matched ordinary-command results retain counterexamples and distinguish policy-only changes from new starting imports/price targets. Fiscal liquidity cannot replace production inputs or physical food transport.
