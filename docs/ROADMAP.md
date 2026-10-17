# Provisional roadmap

This sequence describes planned work, not completed features or a fixed delivery commitment. Revisit it based on prototype results, especially whether each system creates understandable economic decisions and useful automation.

| Phase | Planned scope | Status |
| --- | --- | --- |
| 0 | Project foundation, small architecture, automated tests, canonical content, and documentation pipeline | Implemented |
| 0.5 | .NET 10 toolchain and minimal Unity integration using the existing simulation | Implemented; verification scope in [Unity integration](UNITY.md) |
| 1 | Deterministic simulation, markets, finite inventories, ships, basic trading, and trade automation | Planned |
| 2 | Fixed-map scenarios, objectives, time limits, and roguelike runs | Planned |
| 3 | Constrained procedural world generation with economic viability checks | Planned |
| 4 | Modifiers, curses, difficulty levels, and challenge combinations | Planned |
| 5 | Lateral meta progression through starting packages, alternative production capabilities, and new economic problems | Planned |
| 6+ | Advanced competitors, piracy, political systems, optional combat, and other features justified by gameplay testing | Deferred |

Phase 0 includes only logs, planks, a sawmill, one production conversion, validation, a console demo, headless tests, and generated reference output. Phase 0.5 exposes the same behavior through a Unity component and integration tests. Its deterministic command is a foundation for Phase 1, not a complete deterministic world simulation.

## Recommended next three implementation tasks

1. **Verify the intended player backend and stripping configuration.** Run the Bootstrap smoke scene in an IL2CPP player if that is the intended shipping backend, and record the platform and stripping level. Diagnose concrete compatibility failures before adding preservation rules.
2. **Define and implement a deterministic simulation step.** Decide time units and operation ordering, then test repeatable state transitions using a tiny fixed scenario. Keep random behavior out until a concrete requirement and reproducible seed strategy exist.
3. **Prototype one finite market.** Add stocks, cash balances, an explicit transaction command, and a small documented pricing experiment. Verify that invalid or unaffordable transactions do not partially mutate state, then use headless scenarios to assess depletion and liquidity behavior.

These are proposed tasks, not settled choices of time model or pricing rule. A minimal trade automation loop with two fixed ports and one ship remains later Phase 1 work, after the deterministic step and finite-market behavior have been evaluated.
