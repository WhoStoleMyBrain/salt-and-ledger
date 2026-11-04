# Task 3.0a — original maritime artwork

This is an art-direction correction to Task 3.0 at `c1615c9`. Its HUD, right inspector, Trading A/B/C, Industry, Route V2 and Warehouse V2 layouts and commands remain in place. No simulation, economic signals, canonical content, save fields, asset pixels or import settings changed.

## Rendering hierarchy

| Surface | Source now used | Reason |
| --- | --- | --- |
| Commodity rows, recipes, cargo, warehouse and route order cells | Original `Maritime/Goods` | Shared `MaritimeGlyph` now calls `MaritimeArtwork.DrawGlyph` first; Planks and Silver retain their separate UV regions. |
| Ship glyphs (including map markers); Bakery tiles and recipe center | Original `Maritime/Illustrations/ship` and `bakery` | Identity art fitted to existing bounds. |
| Map islands | Original harbor/forest-harbor illustrations | Reuses the existing decoration scale at actual port coordinates; labels, routes, markers and hit targets are unchanged. |
| Harbor heading; Bakery detail heading; Fleet pane | Original harbor, Bakery and ship illustrations | Small contextual decoration; Bakery art is hidden for other factories and warehouse automation. |
| Farm, Mill, warehouse, market, money, warnings and other utility glyphs | Existing project-authored Painter2D paths | Clear utility symbols; no suitable original facility painting for Farm/Mill. |
| Lumber Camp, Sawmill, Furniture Workshop, natural extraction input | Retained `ApprovedMaritime` industry symbols | No matching original illustration or specific authored vector. Bakery can also use its imported fallback if the original texture is unavailable. |

All `ApprovedMaritime` assets remain intact. Their goods are no longer the default. Narrow layouts intentionally hide decorative modal-heading illustrations to preserve room for controls. Map markers retain compact labeled hit targets with the original ship glyph. We did not restore old coast embellishments, change the approved layout or add paintings for other industries. Mixed compact industry styles remain a small visual debt; inventing new art is outside this correction.

Assets are the owner's existing generated artwork and project-authored paths; no new assets or licenses were introduced. See [original provenance](design/task-2.5/PRODUCTION-ASSETS.md) and [retained Figma export provenance](design/task-3.0/ASSETS.md). The repository records no separate third-party license for the supplied generated raster art. Moodboards are not runtime textures or verification evidence.

## Verification

Executed on 2026-11-04 with .NET 10 and Unity 6000.6.6f1:

| Check | Actual result |
| --- | --- |
| `dotnet test SaltAndLedger.sln -c Release --no-restore` | 759 passed; 0 failed/skipped, 4m46s; rebuilt simulation/tests. |
| Unity PlayMode assembly | 89 passed; 0 failed/skipped, 219.84s. Existing trading, real production, route, warehouse, save and passive-navigation tests retained. Illustration context/picking assertions extend existing journeys. |
| Unity EditMode assembly | 64 passed; 0 failed/skipped, 171.63s. |
| `tools/generate_docs.py --check` | 59 reference files current. |
| `tools/prepare_unity.py --check` | Simulation/content/help staging current. |
| `python -m mkdocs build --strict` | Passed. |
| Windows Mono build | Succeeded using the existing ArchipelagoLab build helper. |

The actual Editor and Windows player walkthrough uses Sandbox, seed 99, Industrial Consortium, Stock-sensitive pricing and Ascension 0. Through UI events, selling the starter two Flour receives **22.2133** coins net with **0.4533** fee; buying them back through the Bakery shortage link costs **26.6666** coins. The Bakery returns from blocked to ready with two Flour and two Logs, at day 0/revision 2/cash **1195.5467**. This matches the prior Task 3.0 journey. No direct economic API or state injection drives this evidence.

Map town/ship selection, Fleet, all three Harbor pairs, Bakery and warehouse navigation at **1920?1080 Default** and **1366?768 Large** preserve exact serialized save text. The resulting SHA-256 in both runtimes is `ae43841af5d47503dd39bc306239c07a50acd91108066daf72249cdeed5e4002`. Drafting the existing three-stop Grain?Flour?Bread preset and inspecting its order grid also leaves that text unchanged. These checks include keyboard-submit controls, real text entry and mouse dropdown selection. Frames advance; paused economic days do not.

## Actual before/after captures

The paired Editor images use the same seed and paid-sale/shortage workflow, at 1920?1080 Default. Before images are the committed Task 3.0 runtime captures, not Figma mockups. After images are unedited captures of this implementation.

| Screen | Task 3.0 before | Task 3.0a after |
| --- | --- | --- |
| Town/map | [Before](design/task-3.0/evidence/editor-bakery-01.png) | [After](design/task-3.0a/evidence/editor-map.png) |
| Harbor/input purchase | [Before](design/task-3.0/evidence/editor-bakery-04.png) | [After](design/task-3.0a/evidence/editor-harbor.png) |
| Bakery/input shortage | [Before](design/task-3.0/evidence/editor-bakery-03.png) | [After](design/task-3.0a/evidence/editor-bakery.png) |

Windows Mono captures: [Fleet, 1920 Default](design/task-3.0a/evidence/player-fleet.png); [map](design/task-3.0a/evidence/player-map-large.png), [Harbor](design/task-3.0a/evidence/player-harbor-large.png), [Bakery](design/task-3.0a/evidence/player-bakery-large.png), [warehouse](design/task-3.0a/evidence/player-warehouse-large.png) and [route orders](design/task-3.0a/evidence/player-route-large.png), all 1366?768 Large.

## Acceptance audit

| Criterion | Evidence / result |
| --- | --- |
| 1. Task 3.0 UX intact | Same HUD/workspaces and command handlers; all 89 PlayMode tests pass; live paid purchase, pair switching, route draft and warehouse navigation work. |
| 2. Original art direction | Painterly town islands, goods, Bakery and Fleet ship are visible in the actual captures. |
| 3. Original assets used live | Shared original-first renderer, unchanged source PNGs/UV crops; before/after evidence above. |
| 4. Readable compact utilities | Existing labels, arrows and vector utilities retained; original goods remain distinguishable at both checked sizes; narrow heading decoration is hidden. |
| 5. No economic/schema change | No changes under simulation, data or persistence; 759 portable tests pass; exact passive fingerprints agree across Editor/player. |
| 6. Tests pass | 759 .NET + 64 EditMode + 89 PlayMode; documentation and staging checks pass. |
| 7. Before/after screenshots | Three paired runtime comparisons plus six additional Windows captures linked above. |

Remaining limitations: Large text at 1366?768 still requires scrolling in Harbor, factory settings and route tables, as in Task 3.0. This correction preserves that layout. Compact/Extra Large are covered by existing automated layout tests, but were not given a new manual screenshot pass. No long economic sweep was needed for this presentation-only change.

The first hidden Windows launch produced black screenshots despite valid input/state checks; those captures are not evidence. Unity's normal `build run` launcher resolved it. The desktop-control helper was unavailable (native pipe missing), so interaction used Unity's supported runtime CLI. Initial Editor readiness polling encountered import-time/protocol errors with the older Pipeline package; explicit supported commands subsequently worked. No game-managed exception or missing-art/USS error occurred in the verified walkthrough. Existing native graphics-plugin architecture notices are unchanged. Only task-owned sessions and temporary QA settings were used; local save slots were preserved.
