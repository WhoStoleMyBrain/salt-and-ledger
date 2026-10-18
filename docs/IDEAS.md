# Experiments and deferred ideas

This document is a backlog of possibilities. It does not change the accepted [design principles](GAME_DESIGN.md), imply that a feature exists, or commit the roadmap to a particular implementation.

## Hypotheses requiring gameplay experiments

| Hypothesis | Useful first experiment |
| --- | --- |
| Few interacting goods can create substantial depth | Compare decisions in a small fixed economy before introducing more chains |
| Automation can reduce attention while preserving planning | Compare manual trading with a minimal configurable route policy |
| Trading and industry can each be viable specializations | Run the same fixed scenario with different initial capabilities and inspect advantages and failure modes |
| Optional opportunities can favor a strategy without invalidating others | Offer a specialized opportunity alongside broadly achievable mandatory objectives |
| Alternative starting capabilities can support replayability without stacking power | Compare sidegrade starting packages against the same challenge set |

These gameplay hypotheses have not been established by the current prototypes. The separate [pricing experiments](PRICING_EXPERIMENTS.md) measure fixed/linear/scarcity behavior and accounting invariants; they do not establish strategic depth, enjoyable automation, or viable specializations. Record relevant gameplay evidence and revise a proposal before promoting it to an accepted decision.

## Intentionally deferred concepts

- **City development and deeper production chains:** add only when they produce meaningful constraints or opportunities.
- **Fixed-map roguelike scenarios:** objectives, time limits, and run structure are planned for Phase 2; failure and recovery rules remain open.
- **Constrained procedural worlds:** defer until economic viability can be defined and checked on fixed scenarios.
- **Modifiers, curses, and difficulty combinations:** must preserve understandable rules and fair challenges.
- **Lateral meta progression:** alternative starting packages, company charters, recipes, building specializations, challenges, adversaries, world configurations, and optional difficulty modifiers. Unlock criteria remain undecided; permanent stacking numerical advantages conflict with the accepted direction.
- **Advanced competitors, piracy, politics, and optional combat:** later possibilities justified by gameplay testing, not requirements for the initial economy.
- **Production switching and optimization policies:** need concrete production and automation problems first.
- **Random events:** need warning, preparation, or alternative solutions where required by fair challenge principles; do not arbitrarily invalidate specialization or secretly tailor all challenges to the build.
- **In-game encyclopedia UI:** consume generated structured reference data when a Unity presentation layer exists.

## Outside the current scope

The current prototype implements finite local trading, production, experimental inventory-sensitive prices, and manual cargo transport between two handcrafted ports with one ship. It contains no general demand simulation, automatic replenishment, additional ships or traders, automated trade routes, procedural generation, roguelike progression, curses, modifiers, production switching, optimization policies, random events, competing merchants, piracy, combat, multiplayer, or elaborate Unity UI. Multiplayer is outside the accepted single-player direction and has no planned milestone.
