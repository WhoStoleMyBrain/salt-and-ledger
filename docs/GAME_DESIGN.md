# Game design

## Accepted direction

Salt & Ledger is a single-player maritime trading and economic strategy game intended for Unity 6, inspired by Port Royale 2, Patrician 2, Antimatter Dimensions, and economic roguelikes. The central gameplay is intelligent optimization and automation of trading networks operating within a simulated economy.

City development, production chains, procedural worlds, roguelike scenarios, customizable difficulty, and lateral meta progression are long-term directions. Phase 0 establishes the smallest working technical foundation; it does not implement those systems.

The principles below are constraints for future design. Particular mechanisms that satisfy them remain open until prototypes provide evidence.

## Meaningful economic decisions

- Markets have finite inventory, production, consumption, and liquidity.
- Economic depth should emerge from interactions between systems.
- More goods, buildings, or mechanics do not automatically mean more depth.
- Player actions should meaningfully affect the simulated economy.
- Information and understandable rules should enable better decisions.

A system earns its complexity by creating decisions with consequences. Additional content alone is not evidence of depth. Phase 0 demonstrates finite inventory and an explicit conversion; markets, money, production schedules, and consumption remain unimplemented.

## Automation over micromanagement

- Manual activities should become automatable as the player progresses.
- Repetitive optimization requiring constant player attention is undesirable.
- Production and trade automation should support configurable policies.
- Automation should create opportunities for higher-level planning.

Automation is part of the intended strategy experience. Its unlock structure, policy language, and user interface are unresolved. Phase 0 contains no automation policies.

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

These are design directions, not features implemented in Phase 0. Their exact form and unlock conditions are hypotheses to test.

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

The principles above are accepted. The [economy questions](ECONOMY.md) and [experiment ideas](IDEAS.md) remain unresolved. The [roadmap](ROADMAP.md) is provisional and should change when prototypes show a better sequence. No price formula, tick duration, map-generation algorithm, or progression system has been selected by this foundation task.
