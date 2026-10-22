# Game design

## Accepted direction

Salt & Ledger is a single-player maritime trading and economic strategy game intended for Unity 6, inspired by Port Royale 2, Patrician 2, Antimatter Dimensions, and economic roguelikes. The central gameplay is intelligent optimization and automation of trading networks operating within a simulated economy.

Historical modes include six goods; opt-in v3 adds Silver and a public monetary institution. The archipelago has interconnected production chains, player-owned factories and complete economic runs. Classic and seeded-v1 retain three historical challenges; v1 changes geography only. Task 1.9's opt-in Living Cities v2 also varies complementary economic roles and introduces finite households, wages, paid consumption, population/prosperity and one sustained city-recovery challenge. All modes retain three replacement charters, physical industrial shipping, material FIFO accounting and Ascension 0/1. City ownership, broader civic development and persistent lateral progression remain future directions.

The principles below are constraints for future design. Particular mechanisms that satisfy them remain open until prototypes provide evidence.

## Meaningful economic decisions

- Markets have finite inventory, production, consumption, and liquidity.
- Economic depth should emerge from interactions between systems.
- More goods, buildings, or mechanics do not automatically mean more depth.
- Player actions should meaningfully affect the simulated economy.
- Information and understandable rules should enable better decisions.

A system earns its complexity by creating decisions with consequences. Additional content alone is not evidence of depth. Tasks 1.1–1.4 establish finite markets, exact stock-sensitive prices, shipping, and configurable automation. Task 1.5 adds four geographic towns, scheduled extraction/processing, physical consumption, and independently funded merchants. Task 1.6 adds actual capital investment and private ownership alongside the public industries. Task 1.7 connects those systems to explicit starting choices and endings. Logs compete between sawmills and bakeries, while planks support consumption or furniture manufacturing.

Player activity changes the same supply and prices NPCs use, while different routes consume different days. Construction uses company liquidity that could otherwise buy a shipment. The [enterprise experiment](ENTERPRISE.md#reproduce-the-experiment) compares those uses of capital, actual purchase costs, sales revenue, and held goods. That historical report retains unpaid consumption and no wages. [Living Cities](LIVING_CITIES.md) separately models finite paid retail and employer-funded wages; travel costs remain absent. These measurements establish concrete tradeoffs and feasible strategies, not settled balance, long-run monetary circulation or enjoyment.

## Automation over micromanagement

- Manual activities should become automatable as the player progresses.
- Repetitive optimization requiring constant player attention is undesirable.
- Production and trade automation should support configurable policies.
- Automation should create opportunities for higher-level planning.

Automation replaces repetitive execution while leaving strategic choices with the player. The [route policy](AUTOMATION.md) executes configured commodities, quantities, marginal limits, reserves, and expenditure caps without discovering an optimal strategy or overriding finite constraints. ArchipelagoLab exposes both directions between directly connected ports, including optional empty return. Bounded same-day actions remove arbitrary transaction-day delays while real voyages still take their configured duration.

Private industry policies maintain chosen local input stocks and optionally sell surplus output, respecting marginal limits, cash reserves, storage, and market liquidity. Industrial routes now load owned stock, transport it, sell or unload it, and optionally carry useful inputs back. Explicit directional stages support the same commodity on both legs; incompatible cargo pauses intact. Warehouse reserves, shipment size, destination, and sale limits remain player decisions. One configuration can operate across many days without repeated transfers or recipe switching.

TradingLab preserves the older comparison. The [industrial experiment](AUTOMATION.md#reproducible-industrial-shipping-comparison) verifies identical manual and automatic economic transitions while reducing prescribed inputs. It also retains cargo under impossible prices or absent liquidity. Unlock structure, broader route graphs, and policy usability remain unresolved; working controls and measured input counts are not evidence of enjoyable gameplay.

## Strategic specialization

- Different approaches, including trading, industrial production, and eventually piracy or other economic activities, should be viable.
- Specialization creates strengths, weaknesses, and opportunity costs.
- No single approach should be universally optimal.

Viability does not require identical mechanics or guaranteed success. The supply charter has reproducible merchant and industrial-shipping solutions with different starting assets, completion times, and capital requirements. All nine challenge/charter combinations have a tested active solution; idle play fails in each. A workshop investment in the historical enterprise experiment generates real sales but leaves less cash after ninety days than retaining the starting capital; a factory is not universally superior. These examples require observed playtesting before stronger balance claims. Piracy and other advanced activities are deferred.

## Lateral meta progression

Permanent progression, if added, should expand the available choices rather than grant stacking numerical advantages. Task 1.7 implements the choices first without persistence or unlock requirements: Merchant Company, Industrial Consortium, and Independent Traders are available from the start. Select exactly one package before a run; its assets replace the others, and an active company cannot switch package.

The merchant emphasizes liquidity, the consortium exchanges liquidity and hold capacity for a bakery and inputs, and the independents exchange liquidity for a larger hold and raw cargo. [Run setup](RUNS.md#select-a-starting-company) records actual endowments, starting quote estimates, and the unvalued ship capabilities. Cash alone is not starting wealth, and the packages are not claimed to be perfectly normalized competitors.

Potential systems include:

- Further starting packages and company charters.
- Alternative production recipes and building specializations.
- New economic challenges and mechanics.
- New adversaries and world configurations.
- New optional difficulty modifiers.

Further packages, capabilities, persistent unlocks and modifiers are design directions. Task 1.8 implements exactly one selectable modifier: Ascension 1 delays newly purchased player facilities by two complete days. Ascension 0 remains the baseline. Difficulty is chosen for each run; no unlock, stacking curse, challenge-point budget or permanent income/production bonus exists. The consortium's granted bakery is already operational at either difficulty, so this narrow industrial penalty affects charters and strategies differently.

## Fair challenges

- Random events should not arbitrarily invalidate a player specialization.
- Optional economic opportunities may favor particular strategies.
- Major mandatory objectives should have multiple potential solutions, reasonable warning, or meaningful ways to prepare.
- Player decisions can have negative consequences; the game should not guarantee success.
- Avoid secretly adjusting all challenges to match the player's build.

Fairness requires understandable exposure to risk and meaningful agency. The game need not remove consequences or adapt every challenge to the player's chosen strategy. The Coastal Supply Charter uses the existing world clock and a signed net-supply objective derived from completed transactions. Buying back and reselling the destination's goods cannot manufacture objective credit. Regional Relief measures actual scheduled consumption across towns, explicitly including public/NPC contributions. Enterprise Ambition requires private output and attributed material gross margin. All have a defined deadline, solvency condition, and terminal outcome.

The [run guide](RUNS.md) documents the capped score and fixed-world strategies. New run identities group results by challenge, charter, ascension and exact world, including the seeded generator version and seed. Granted goods have no paid acquisition expenditure, so their proceeds and different initial facilities affect results. A universal normalized leaderboard is not claimed. [Seeded experiments](SEEDED_RUNS.md) separate valid, navigable maps from demonstrated strategic viability; no map receives secret funds, stock or deadline adjustments. Thresholds remain provisional, with no permanent rewards.

## A complete run and understandable consequences

The player journey is main menu, setup, strategy, terminal outcome, and review. Setup should communicate the objective, deadline, starting assets, and tradeoffs before committing. The pause menu provides real navigation and deliberate abandonment/restart/exit, with confirmation before discarding an active run. A completed challenge freezes its authoritative economy, while results and reference remain inspectable. Retry restores the selected starting state; sandbox remains unlimited.

Historical FIFO attribution makes transactions and production explainable without inventing omitted costs. Actual purchase expenditure follows goods through split transfers and manufacturing; sales release the corresponding material basis. Held stock, liquid cash, construction, revenue, and realized material gross margin stay distinct. Living production separately expenses real wages; operating surplus after wages includes wages for output still held and is not full matched profit. Exact totals establish modeled accounting correctness, not a complete depreciation, freight, overhead or resale-value model.

## Transparency and documentation

- Game content should be defined in structured, validated data wherever practical.
- The simulation and documentation should share canonical definitions.
- Players should be able to understand mechanics, production recipes, and modifiers.
- In-game documentation and external wiki content should remain synchronized with game data.

Phase 0 puts definitions in a canonical JSON file and produces both Markdown and structured encyclopedia reference data. Task 1.7 stages that generated reference for the offline in-game encyclopedia. Goods, buildings, recipes, and related-entry links derive from canonical definitions; authored player mechanics feed both the game reference and external help. Behavior not derivable from content remains specified in [Mechanics](MECHANICS.md). Neither interface maintains hand-copied recipe facts or asks a runtime model to invent rules.

## Design status

The principles above are accepted. The [economy questions](ECONOMY.md) and [experiment ideas](IDEAS.md) remain unresolved, and the [roadmap](ROADMAP.md) is provisional. Legacy ticks and fixed reports remain unchanged. [Commerce and industry](ENTERPRISE.md), [automation](AUTOMATION.md), [runs](RUNS.md) and [seeded worlds](SEEDED_RUNS.md) describe current behavior. V2 economic roles, aggregate household finance and the narrow recovery objective are implemented experiments; final balance, further ascensions, city ownership and persistent progression remain unselected. Readability is a current requirement: four layout-aware text scales should keep trading, policies and accounting usable without changing simulation state; actual verification belongs in [Unity integration](UNITY.md).

## Expanding Economy: productive agency and monetary development

Task 2.0 explicitly selects v3 rather than changing the v2 control. The player can expand every existing production kind somewhere, build several eligible sites, and commission up to two additional independently controlled ships. Proportional paid needs remove integer population cliffs. Finite external deliveries and the unique publicly owned Silver-backed Mint add accountable monetary pathways. Company Value exposes owned productive assets without relabeling acquisition or capital spending as realized profit. Local saves preserve difficult economic states instead of replaying an approximate history.

The [v3 rules](EXPANDING_ECONOMY.md) aim to make shortages addressable through concrete investment, employment, exports and physical logistics. They do not guarantee that any policy, seed or idle world succeeds. A Mill can expose a Grain shortage; a new ship can tie up needed cash; minted coins cannot replace absent food. The public Mint is optional high-level infrastructure, not a privately owned passive-income multiplier. All provisional prices, authorization bounds and objectives require measured comparisons and actual play. No debt, taxation, unrestricted fleet, hidden cash refill or additional ascension is included.
