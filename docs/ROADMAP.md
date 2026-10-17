# Provisional roadmap

This sequence describes planned work, not completed features or a fixed delivery commitment. Revisit it based on prototype results, especially whether each system creates understandable economic decisions and useful automation.

| Phase | Planned scope | Status |
| --- | --- | --- |
| 0 | Project foundation, small architecture, automated tests, canonical content, and documentation pipeline | Current foundation |
| 1 | Deterministic simulation, markets, finite inventories, ships, basic trading, and trade automation | Planned |
| 2 | Fixed-map scenarios, objectives, time limits, and roguelike runs | Planned |
| 3 | Constrained procedural world generation with economic viability checks | Planned |
| 4 | Modifiers, curses, difficulty levels, and challenge combinations | Planned |
| 5 | Lateral meta progression through starting packages, alternative production capabilities, and new economic problems | Planned |
| 6+ | Advanced competitors, piracy, political systems, optional combat, and other features justified by gameplay testing | Deferred |

Phase 0 includes only logs, planks, a sawmill, one production conversion, validation, a console demo, headless tests, and generated reference output. Its deterministic command is a foundation for Phase 1, not a complete deterministic world simulation.

## Recommended next three implementation tasks

1. **Define and implement a deterministic simulation step.** Decide time units and operation ordering, then test repeatable state transitions using a tiny fixed scenario. Keep random behavior out until a concrete requirement and reproducible seed strategy exist.
2. **Prototype one finite market.** Add stocks, cash balances, an explicit transaction command, and a small documented pricing experiment. Verify that invalid or unaffordable transactions do not partially mutate state, then use headless scenarios to assess depletion and liquidity behavior.
3. **Build the smallest trade automation loop.** Connect two fixed ports with one ship, manual trade commands, and a simple configurable route policy. Compare the decisions it creates with the attention it requires before expanding content.

These are proposed implementation tasks, not settled choices of time model, pricing rule, or automation policy. A local Unity import and player smoke test can validate the presentation boundary before UI work begins.
