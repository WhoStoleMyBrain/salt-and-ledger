# Game design

## Accepted direction

Salt & Ledger is a single-player maritime trading and economic strategy game intended for Unity 6, inspired by Port Royale 2, Patrician 2, Antimatter Dimensions, and economic roguelikes. The central gameplay is intelligent optimization and automation of trading networks operating within a simulated economy.

City development, broader production chains, procedural worlds, roguelike scenarios, customizable difficulty, and lateral meta progression are long-term directions. The current handcrafted archipelago does not implement those systems.

The principles below are constraints for future design. Particular mechanisms that satisfy them remain open until prototypes provide evidence.

## Meaningful economic decisions

- Markets have finite inventory, production, consumption, and liquidity.
- Economic depth should emerge from interactions between systems.
- More goods, buildings, or mechanics do not automatically mean more depth.
- Player actions should meaningfully affect the simulated economy.
- Information and understandable rules should enable better decisions.

A system earns its complexity by creating decisions with consequences. Additional content alone is not evidence of depth. Tasks 1.1–1.4 establish finite markets, exact stock-sensitive prices, shipping, and configurable automation. Task 1.5 adds four geographic towns, scheduled extraction/processing, physical consumption, and independently funded merchants. Player activity changes the same supply and prices NPCs use, while different routes consume different days. Market ownership of production, payment-free consumption, and the absence of travel costs are explicit prototype assumptions. The experiments do not settle balance or demonstrate enjoyable decisions; final calibration, household finances, and factory ownership remain unresolved.

## Automation over micromanagement

- Manual activities should become automatable as the player progresses.
- Repetitive optimization requiring constant player attention is undesirable.
- Production and trade automation should support configurable policies.
- Automation should create opportunities for higher-level planning.

Automation replaces repetitive execution while leaving strategic choices with the player. The [route policy](AUTOMATION.md) executes configured commodities, quantities, marginal limits, reserves, and expenditure caps without discovering an optimal strategy or overriding finite constraints. ArchipelagoLab exposes both directions between directly connected ports, including optional empty return. Bounded same-day actions remove arbitrary transaction-day delays while real voyages still take their configured duration. TradingLab preserves the older comparison. Unlock structure, broader route graphs, and policy usability remain unresolved; working controls and measured input counts are not evidence of enjoyable gameplay.

## Strategic specialization

- Different approaches, including trading, industrial production, and eventually piracy or other economic activities, should be viable.
- Specialization creates strengths, weaknesses, and opportunity costs.
- No single approach should be universally optimal.

Viability does not require identical mechanics or guaranteed success. The relevant tradeoffs must be established through gameplay experiments; piracy and other advanced activities are deferred.

## Lateral meta progression

Permanent progression should expand the available choices rather than grant stacking numerical advantages.

Potential systems include:

- Alternative starting packages and company charters.
- Alternative production recipes and building specializations.
- New economic challenges and mechanics.
- New adversaries and world configurations.
- New optional difficulty modifiers.

These are design directions, not implemented features. Their exact form and unlock conditions are hypotheses to test.

## Fair challenges

- Random events should not arbitrarily invalidate a player specialization.
- Optional economic opportunities may favor particular strategies.
- Major mandatory objectives should have multiple potential solutions, reasonable warning, or meaningful ways to prepare.
- Player decisions can have negative consequences; the game should not guarantee success.
- Avoid secretly adjusting all challenges to match the player's build.

Fairness requires understandable exposure to risk and meaningful agency. The game need not remove consequences or adapt every challenge to the player's chosen strategy.

## Transparency and documentation

- Game content should be defined in structured, validated data wherever practical.
- The simulation and documentation should share canonical definitions.
- Players should be able to understand mechanics, production recipes, and modifiers.
- In-game documentation and external wiki content should remain synchronized with game data.

Phase 0 puts definitions in a canonical JSON file and produces both Markdown and structured encyclopedia reference data. Behavior not derivable from those definitions is maintained in [Mechanics](MECHANICS.md). A future in-game UI should consume the generated reference rather than maintain a separate copy.

## Design status

The principles above are accepted. The [economy questions](ECONOMY.md) and [experiment ideas](IDEAS.md) remain unresolved, and the [roadmap](ROADMAP.md) is provisional. Legacy abstract ticks remain unchanged; the new regional sandbox provisionally maps one tick to one day and separates instant transactions from advancement. [Living archipelago](LIVING_ARCHIPELAGO.md) records the implemented flows, map, merchant competition, and measured strategy effects. [Pricing experiments](PRICING_EXPERIMENTS.md) and [Manual shipping comparison](SHIPPING_DEMO.md) retain historical comparisons. Final balance, procedural maps, and progression systems remain unselected.
