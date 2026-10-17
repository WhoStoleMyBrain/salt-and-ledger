# Salt & Ledger

Salt & Ledger is a planned single-player maritime trading and economic strategy game for Unity 6. Its central activity is intelligent optimization and automation of trading networks within a simulated economy.

The current repository is a minimal foundation: a Unity-independent C# simulation, validated sample content, a console demonstration, headless tests, and generated reference documentation. No Unity game or encyclopedia UI has been implemented.

## Read the project

- [Game design](GAME_DESIGN.md): non-negotiable principles and intended player experience.
- [Architecture](ARCHITECTURE.md): implemented modules, contracts, and dependencies.
- [Mechanics](MECHANICS.md): maintained specification for the sample production command.
- [Content reference](reference/index.md): generated goods, building, and recipe definitions.
- [Economy](ECONOMY.md): accepted modeling goals and unresolved choices.
- [Roadmap](ROADMAP.md): provisional development sequence.
- [Decisions](DECISIONS.md): accepted foundation decisions and their tradeoffs.
- [Ideas](IDEAS.md): experiments and intentionally deferred concepts.
- [Unity integration](UNITY.md): local import and verification instructions.

## Canonical definitions and maintained rules

`data/content.json` is the source for both runtime content and generated reference pages. `data/content.schema.json` describes its structure; semantic validators check relationships and additional constraints. The generator also emits `generated/encyclopedia.json` for a future in-game reference consumer.

Generated reference pages describe only facts present in the data. Command semantics, error behavior, and the absence of time or market behavior are specified in [Mechanics](MECHANICS.md). Long-term design direction is not a claim that those mechanics exist today.
