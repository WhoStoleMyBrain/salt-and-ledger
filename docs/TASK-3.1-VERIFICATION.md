# Task 3.1 verification

This change replaces the four-town generator with configurable generator4 and schema7 persistence. The implementation branch starts from fetched GitHub `main` commit `a1f68550207e85ebff78e52f32010cb4e250250f`.

See [world generation](WORLD_GENERATION.md) for configuration, constraints and reproduction commands. The generator uses deterministic separated integer placement, a distance-valid minimum spanning tree with selected shortcuts, and bounded recipe-constrained economic assignment. Dijkstra navigation sums per-lane physical travel days.

## Representative worlds

The [default report](experiments/world-generation-results.json) runs30 ordinary days per seed and checks reversed configuration, save round trips and seven further identical days. All entries have connected routes, renewable potential sources, zero direct local recipe conflicts and zero institution-input conflicts.

| Seed | Towns | Lanes | Longest fastest voyage at speed100 | Production input dependencies needing transport | NPC merchants |
| ---: | ---: | ---: | ---: | ---: | ---: |
| −42 | 8 | 10 | 12 days | 12 | 4 |
| 42 | 8 | 10 | 13 days | 14 | 4 |
| 99 | 8 | 10 | 12 days | 12 | 4 |
| −2147483648 | 12 | 14 | 14 days | 17 | 6 |
| 2147483647 | 9 | 11 | 13 days | 17 | 5 |

Three custom examples each completed365 ordinary days plus exact continuation: [sparse-eight](experiments/world-generation-sparse-eight.json) has8 towns/7 lanes; [twelve-town commerce](experiments/world-generation-twelve-town.json) has12 towns/19 lanes; [narrow specialization](experiments/world-generation-narrow.json) has10 towns/12 lanes with exactly one public and one eligible private town per industry. They retain finite money and actual shortages.

Real commission witnesses use paid construction, purchases, funded production, physical shipping, contracts and Mint issuance. Seed99 Merchant wins on day53 with508.3035 coins; seed42 Independent wins on day58 with290.7952 coins. Seed99's itinerary variant wins on day49 with500.9535 coins. The idle control fails at720 days. These different policies/endowments are not profitability comparisons. The final-arrival deadline regression also proves success takes precedence on the exact deadline.

## Automated checks

- Locked .NET10 restore and Release build, zero warnings/errors.
- Complete .NET regression suite: **823 passed, zero failures, zero skipped** in the final run.
- 66 Python tests, canonical content/configuration validation, generated reference freshness and strict MkDocs build.
- Actual portable Unity host checks and all65 Unity C# syntax checks.
- Unity DLL/content/configuration/encyclopedia staging plus read-only freshness check.
- Grand Commission CLI checks, five-seed default report and allthree365-day custom configuration checks.
- Current expanding-economy, reinvestment and circulation report generation/checks:29/12/80 trials, totaling38800 primary world-days plus reversed/culture replays and save continuation. The reports use compact format4 and real accounts/stock, with no subsidy or outcome repair.

Generation tests cover514 geography seeds,131 constrained economic seeds, each example over20 seeds,2/64-town boundaries, every canonical direct dependency and a changed future recipe, separate public/private bounds, unique names/IDs, enumeration/culture stability, narrow configurations and actionable invalid bounds. Integration tests cover paid production/shipping/objective progress, configurable fleet/institutions, exact continuation, daily atomicity, FIFO, money/goods ownership, route automation and impossible-save rejection.

The final review added institution guards for Silver at the Mint and hull material at shipyards, strict integer/case configuration parsing, safe large-margin arithmetic, save travel-time lower bounds, and animation bounded by committed elapsed days. A command-only revision or departure cannot invent visual travel.

## Unity boundary

Unity Editor and a graphical Unity player are unavailable in this cloud environment. No Editor/Play Mode interaction or screenshot-based layout verification is claimed.

A headless harness compiled the actual `ArchipelagoLabSession`, `EconomicText`, `VoyageVisualInterval` and `LocalWorldGenerationConfiguration` against the real simulation with warnings treated as errors. It exercised eight-, ten- and twelve-town worlds, multi-lane travel (two/two/four legs taking six/six/eleven days), custom configuration loading, invalid-file isolation, exact save continuation and Retry configuration preservation. Roslyn parsed all 65 Unity C# files without syntax errors. These are host and syntax checks, not Unity type-binding or visual playtests.

Local Unity verification remains required:

1. Run `python tools/prepare_unity.py`, open the pinned Unity project, and run Edit Mode/Play Mode tests.
2. At 1366×768 and 1920×1080, create multiple default seeds and the eight-/twelve-town examples. Check preview/start agreement, distinct selectable town markers, names and public/private capability lists. Reroll between different town counts and confirm obsolete markers/routes disappear.
3. Sail a selected ship over several lanes. Verify segment animation, final ETA, cargo, manual arrival trading, and no intermediate dock visit or itinerary orders.
4. Build an eligible industry, supply paid inputs, produce output and transport it. Check default direct producer/consumer exclusion in Industry and valid imports in Harbor.
5. Apply/pause/resume an automated route, save during travel, Load and Retry. Check exact cursor/cargo/remaining duration and original configuration after changing the external configuration file.
6. Inspect generated Mint/Silver/shipyard locations, fleet limits including pending orders, feasible objective cards, actual All Harbors count and configured deadline. Complete a Commission witness and inspect its frozen result.
7. Set an invalid file configuration and verify a visible setup error without fallback or replacement of an active saved session.

The existing finite economy can decline and merchants can be unprofitable. The generator guarantees structural constraints and reachable potential supply, not economic success. Custom content needs canonical economic specifications, and bounded candidate searches may reject a difficult seed/configuration with diagnostic reasons.
