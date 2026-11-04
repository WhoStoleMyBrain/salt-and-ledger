# Approved UX redesign verification

Task 3.0 implements the approved Figma design in the existing Unity 6 UI Toolkit host. Figma was inspected read-only. The authoritative specification is `tasks/TASK-3.0.md`; the actual starting commit was `976e2e535ab323504b40b065a608696930c4b1de`.

The integrated implementation passed **759 .NET, 64 Unity Edit Mode and 89 Unity Play Mode tests**. Actual Editor journeys cover all three HUD states and trading pairs, the eight Bakery states, all five Warehouse V2 states, physical multi-good route execution, missing-storage Wait/Skip and exact disk save/load. The Windows Mono player continued that exact Editor save, completed the voyage, performed paid and cash-free trades, and restored its own save exactly. Automated suites, scripted interaction with actual running Unity, and screenshot comparison are separate evidence; this is not a human mouse-and-keyboard usability study.

## Design authority

- Main gameplay/Trading/Industry: Figma page `0:1`, all three HUD states, Trading A/B/C, Industry overview/factory and eight Bakery states.
- Fleet: eight latest ROUTE V2 frames on page `109:1056`; three older frames only clarify behavior.
- Warehouse: `193:1057`, `193:1360`, `215:1340`, `215:1797`, `216:1513`.
- Rejected Trading D, the speculative five-input factory and archived warehouse Review/Apply are excluded.
- Figma was not modified. Individual source symbols and their provenance are documented in [asset provenance](design/task-3.0/ASSETS.md). Frame screenshots are design evidence, never runtime sprites.

## Acceptance matrix

| Requirement | Implementation | Verification |
| --- | --- | --- |
| 1. Selected-town HUD | Right inspector with public sites, population/prosperity, household needs and real actions | Passed bound-signal and town navigation tests. Actual town/shortage context reviewed in [Bakery state 1](design/task-3.0/evidence/editor-bakery-01.png), [HUD evidence](design/task-3.0/HUD-TRADING-EVIDENCE.md) and the Windows Northport arrival. |
| 2. Selected-ship HUD | Actual dock/voyage, cargo, remaining capacity, automation and next instruction | Passed selected-second-ship, NPC read-only, voyage and wait/cursor tests. Actual [ship HUD](design/task-3.0/evidence/editor-hud-ship.png) and [six-day voyage](design/task-3.0/evidence/editor-route-sailing.png) reviewed. |
| 3. No-selection HUD | Full chart, hidden contextual inspector and visible activity | Passed initial state, chooser, clear selection and exact snapshot/save passivity checks. Actual [unselected chart](design/task-3.0/evidence/editor-hud-none.png) has no stale inspector. |
| 4. Trading A | Exact paid Market ↔ Ship arrows | Passed purchases/sales, finite quotes/fees, stale same-day rejection, ownership/manual/docking guards and selected-second-ship execution. Actual [Editor journey](design/task-3.0/HUD-TRADING-EVIDENCE.md) bought 1 Grain for 16 coins and sold it for 13.72 net without advancing time. |
| 5. Trading B | Exact paid Market ↔ Warehouse arrows; fractional local goods | Passed fractional purchase/sale, remote/automated-ship independence and target consistency tests. Actual Editor paid sale and purchase of 2 Flour verified in the [Bakery journey](#actual-editor-bakery-journey); the same restored warehouse inventory was inspected in the player. |
| 6. Trading C | Actual-dock Warehouse ↔ Ship transfers without payment | Passed both transfer directions, whole-cargo restrictions, unchanged cash/FIFO basis and exact save/load. Actual [Editor load/unload](design/task-3.0/HUD-TRADING-EVIDENCE.md) moved 1 Flour each way, preserving cash and restoring stock. |
| 7. Industry overview | Actual public/private sites and eligible paid construction | Passed public inspection, multiple-site construction, construction timing and starter-grant tests. Actual Editor [overview](design/task-3.0/evidence/editor-bakery-02.png) shows one owned Bakery, two public sites and real build choices; [1366 Large factory](design/task-3.0/evidence/editor-factory-1366-large.png) reviewed. Windows overview/factory inspection preserved exact save state. |
| 8. Factory management | Recipe, input/payroll/storage readiness, enabled/rate/priority draft and atomic Apply/Cancel | Passed settings, invalid rate, passive Cancel, real funded batches and wage tests. Actual Editor shortage, paid resupply, unapplied drafts and final 50%/High settings reviewed in states 3–8 below. |
| 9. Eight Bakery states | Town → overview → shortage → paid warehouse input → return → rate draft → priority draft → applied settings | Automated complete-batch/wage scenario passed. All eight actual Editor states executed and [captured](#actual-editor-bakery-journey) at 1920×1080 Default, with exact cash/stock/revision evidence. Player/other-scale coverage remains separate. |
| 10. ROUTE V2 planning | Actual chart, town selection, stop reordering/add/remove and ship selection | All **14 itinerary scenarios passed**, including unapplied-draft reopening, course-chart interactions, surface contrast and inline visibility. Actual chart hover, add/remove/reorder and [applied route](#actual-editor-route-and-persistence-journey) exercised without draft-time economic changes. |
| 11. Multi-commodity inline orders | Seven-good grid, explicit Save order draft, ordered execution and editable exact limits | Passed multi-good disposal-first configuration, inline cancellation/validation, duplicate-order preservation and instruction identity changes. Actual two-good orders configured and executed. The form now reveals automatically and focuses quantity; existing Logs and new Bread edits were verified in the Windows player at both required layouts, including visible Save/Cancel and exact passive cancellation. |
| 12. Route review/save/pause/execute | Authoritative assignment, separate Start/Pause, real voyages and live cursor/budget | Passed paused application, partial-stop replacement rejection, paid actions followed by Wait, off-route resume rejection and daily-budget persistence. Actual [route journey](#actual-editor-route-and-persistence-journey) bought Grain/Logs, sailed, sold, returned, paused/resumed and preserved the operation budget. |
| 13. Future infrastructure | Missing storage warns without bypassing structural validation | `FutureWarehouseWaitSurvivesLoadThenConstructionEnablesSameRoute` and unfunded-preset tests passed. Actual Westhaven missing-storage [Wait](design/task-3.0/evidence/editor-route-missing-warehouse-wait.png) preserved exact state on repeated execution; [Skip then departure](design/task-3.0/evidence/editor-route-skip-departure.png) used two daily operations and created no goods/storage. |
| 14. Warehouse Buy/Sell states | Seven goods, actual public/private stocks, target/reserve, price limits and switches; valid edits apply automatically | Passed warehouse policy, exact paid procurement/production/sales, pooled-storage and independent valid-edit tests. Both actual [Warehouse tabs](design/task-3.0/WAREHOUSE-EVIDENCE.md) exercised; later production and automatic sales retained 2 Bread. |
| 15. Both cash reserve states | One real buy-only company-cash reserve edited from either tab | Passed shared reserve behavior and Escape priority in `ReservePopupUsesSameBuyOnlyReserveOnBothTabsAndCancelClosesItFirst`. Both actual [reserve popups](design/task-3.0/WAREHOUSE-EVIDENCE.md) show the same 100-coin Buy reserve and its scope. |
| 16. Invalid warehouse feedback | Visible invalid draft retains last policy; valid edits to other goods still apply | Passed invalid target/rate tests and opposite-rule repair. Actual [Bread Buy 10 / Sell Keep 2 conflict](design/task-3.0/evidence/editor-warehouse-05.png) preserved exact serialized state; repairing Buy to 2 then pausing retained its configured limit. |
| 17. No economic/schema changes | Simulation owner, pricing/accounting, canonical economic content, reports and save serialization unchanged | Diff audit confirms no changes to simulation/session/save implementations or canonical content/schema. **759 .NET tests** and three deterministic reports passed. |
| 18. Actual authoritative values | Snapshot/previews/economic signals and actual map geometry replace prototype values | Passed bound commodity flows, public/private separation, actual town quote comparison, paid totals/fees, production readiness and objective progress tests. Actual Editor trade totals, physical production, wages, cargo, travel days and economic balances are recorded below. |
| 19. Navigation/passivity/focus | Inset chart blocking, nested returns, secondary screens, terminal dismissal and fresh-run cleanup | Passed snapshot/save-byte inspection checks, planner/Harbor/Industry return cases, NPC Details single-Escape return, fresh-run modal cleanup and terminal-result cleanup. Terminal cleanup regression uses genuine abandonment; no fabricated victory fixture. |
| 20. 1920×1080 / 1366×768 and supported scales | Inherited Compact/Default/Large/Extra Large tokens and internal scrolling | Readability/bounds tests passed. Actual Editor switched through all four scales at both sizes with exact state unchanged; [curated screenshots](design/task-3.0/HUD-TRADING-EVIDENCE.md) show scrolling limits. Actual Windows route, inline editor, Industry and Warehouse were checked at 1920 Default and 1366 Large. |
| 21. Complete save/load journeys | Existing schema 6 and three-slot host API retained | Passed exact paid-lot transfer save/load, mid-voyage slot load, factory/warehouse continuation and itinerary cursor/daily-budget preservation. Editor Save/Continue restored exact D8/R28 and Save → advance → Load restored D29/R65. Windows Continue matched that exact save, then its own Save → advance → Load restored D34/R74 byte for byte. |
| 22. Documentation | Player guide, Harbor, automation, design system, canonical help, provenance and this record updated | Generated help/content checks, **58 Python tests** and strict MkDocs passed after help edits. Final documentation freshness/build verification is recorded below. |

## Checks executed

- Offline locked .NET restore passed using repository package cache; NuGet audit disabled for the offline invocation.
- Release build passed, zero warnings/errors.
- .NET test suite: **759 passed, 0 failed, 0 skipped**, 4m05s.
- Grand Commission, Expanding Economy and Economic Circulation `--check`: all matched.
- Content validation passed; generated-doc freshness passed (59 files).
- Python tests: **58 passed**; strict MkDocs build passed.
- Unity staging and freshness checks passed after the help updates.
- Static C#8/netstandard2.1 host compilation against installed Unity assemblies passed, zero warnings/errors. This is not a Unity interaction test.
- Unity 6000.6.6f1 actual Editor import/recompile passed; startup reached the main menu and advanced real frames without gameplay errors.
- Actual Unity Edit Mode: **64 passed, 0 failed, 0 skipped**, 85.09s (`.cache/task30/editmode-first.json`).
- First Play Mode run: **73 passed / 79 total**, 6 failed, 134.09s. These failures exposed layout reachability, settling and an obsolete selector; they were corrected without removing the intended viewport assertions.
- Second Play Mode run: **81 passed / 82 total**, 1 failed, 138.58s. The remaining viewport issue was corrected.
- Third Play Mode run: **85 passed, 0 failed, 0 skipped**, 162.01s (`.cache/task30/playmode-third.json`). This includes all 11 approved-trading/navigation regressions, four new Industry/Warehouse scenarios, six Industry usability tests, three expanding-industry tests and ten itinerary scenarios, alongside the retained suites.
- Subsequent complete Play Mode run: **87 passed, 0 failed, 0 skipped**, 162.98s (`.cache/task30/playmode-final.json`), including both additional route regressions.
- Focused route surface regression: **1/1 passed**, 5.47s. The first expanded 89-test run exposed two further failures: a Large inline-form viewport defect and an obsolete ship-ID selector. The form now reveals after layout with advanced controls collapsed; ship names remain friendly while commands retain stable IDs. Focused inline and fleet checks passed (2.6s and 0.68s).
- **Final complete Play Mode: 89 passed, 0 failed, 0 skipped**, 163.67s (`.cache/task30/playmode-final-green.json`). No runtime source changed afterward.
- Windows Mono development build succeeded with no build diagnostics (`.cache/task30-live-build-result.json`). The actual player passed the continuation, inspection, voyage, trading and persistence journey below. Existing native graphics startup warnings are disclosed under limitations.

## Actual Editor Bakery journey

The following are unedited screenshots of the running Unity Editor game view at **1920×1080, Default text size**. Each was viewed alongside its corresponding approved Figma state. They show one real Sandbox session at Northport; Figma's Southhaven, day, cash and sample inventories are not copied into the simulation. Only these screenshots are curated here, not raw saves.

| State | Actual Editor screenshot | Figma reference | Observed behavior / comparison |
| --- | --- | --- | --- |
| 1. Town selected | [View state 1](design/task-3.0/evidence/editor-bakery-01.png) | `86:741` | Persistent navy map/rail, parchment town inspector, public-production symbols and visible Bakery Flour-shortage notice. Real town data replaces the prototype's tutorial overlay. |
| 2. Industry overview | [View state 2](design/task-3.0/evidence/editor-bakery-02.png) | `86:879` | Operating sites and build choices occupy separate columns. Owned Bakery and public Mill/Sawmill have real recipes, readiness and actions; site cards remain individually inspectable. |
| 3. Flour shortage | [View state 3](design/task-3.0/evidence/editor-bakery-03.png) | `86:1182` | Flour `0 / 1` is marked NOT ENOUGH; Logs `2 / 1` is READY. Open Warehouse leads to the paid local-stock workflow. Actual applied rate is 100%, rather than the prototype's 75%. |
| 4. Paid warehouse purchase | [View state 4](design/task-3.0/evidence/editor-bakery-04.png) | `86:1430` | Market/Store pair, selected Flour, company stock 0 and order quantity 2. The actual row arrow executes the authoritative order; exact totals remain in tooltips/details instead of the prototype's mock purchase block. |
| 5. Inputs restored | [View state 5](design/task-3.0/evidence/editor-bakery-05.png) | `86:1887` | Returning to the same Bakery shows Flour `2 / 1`, Logs `2 / 1` and readiness from current inputs, cash and space. The company cash display reflects the real payment. |
| 6. Rate draft | [View state 6](design/task-3.0/evidence/editor-bakery-06.png) | `86:2135` | The 50% preset updates the draft and explicitly labels it NOT APPLIED; the applied line remains 100%/Normal until Apply. |
| 7. Priority draft | [View state 7](design/task-3.0/evidence/editor-bakery-07.png) | `86:2383` | High is selected alongside the 50% draft. The applied settings and economic state remain unchanged. |
| 8. Settings applied | [View state 8](design/task-3.0/evidence/editor-bakery-08.png) | `86:2631` | Applied settings show Enabled/50%/High, the 12-day capability changes to +6 Bread and the next rate-admitted attempt is D4. The action is reached by scrolling, unlike Figma's fixed-height prototype. |

The shortage was created through a real sale of the existing 2 Flour: gross **22.6666 coins**, fee **0.4533**, net **22.2133**. Company cash changed from **1200** to **1222.2133** and warehouse Flour from **2** to **0**. Buying 2 Flour back cost **26.6666 coins**, leaving **1195.5467** and warehouse Flour **2**. Applying 50%/High changed revision **2 → 3** without changing that cash or stock. All eight states remained on **day 0**. The local evidence log is `.cache/task30-factory-journey-editor-evidence.jsonl`.

These eight day-0 states demonstrate paid resupply and configuration; completed batches and factory wages remained zero during that initial sequence. Subsequent explicit Next day interactions exercised the same Bakery and its real warehouse policy: by day 4 it had completed one batch and paid **6 coins in wages**, and by day 8 it had completed two batches and paid **12**. The [Warehouse evidence](design/task-3.0/WAREHOUSE-EVIDENCE.md) records the actual purchases, sales and retained Bread. At the later exact day-29 save/load, the Bakery had completed **7 batches** and paid **42 coins in wages**, with rate 50% and priority High retained. The visual comparison confirms the intended chart/inset/inspector composition, symbols and workflow, while retaining standard Unity fields, actual site cards and scrollable controls rather than claiming pixel-identical reproduction.

## Actual Editor route and persistence journey

The actual Editor route used the same seed-99 session, with ordinary UI events and physically transported goods. These unmodified screenshots were compared with Figma ROUTE V2 `144:2980` (planning/chart) and `144:3136` (under way). They retain the chart, sailing-order context, current-leg/activity feedback and pause controls. Actual seeded coordinates, distances and cargo replace fictional prototype geography and forecasts.

| Actual capture | Resolution / text size | Observed state |
| --- | --- | --- |
| [Applied plan](design/task-3.0/evidence/editor-route-applied-1920-default.png) | 1920×1080 / Default | Northport Buy 1 Grain and 1 Logs; Southhaven Sell 1 Grain and 1 Logs. Two stops, four instructions, manual pause and an actual 12-sailing-day loop. |
| [Applied plan, larger text](design/task-3.0/evidence/editor-route-applied-1366-large.png) | 1366×768 / Large | Same real route and game state with larger readable labels; lower context actions use the panel scroller. |
| [Physical voyage](design/task-3.0/evidence/editor-route-sailing.png) | 1920×1080 / Default | Departed Northport with Grain 1 and Logs 1, hold 2/8, six days to Southhaven. Cargo orders wait for actual arrival. |
| [Missing warehouse: Wait](design/task-3.0/evidence/editor-route-missing-warehouse-wait.png) | 1920×1080 / Default | Three-stop route reached Westhaven; Load 1 Flour waits because company storage is absent. The stop and activity strip explain the blocker. |
| [Missing warehouse: Skip](design/task-3.0/evidence/editor-route-skip-departure.png) | 1920×1080 / Default | After changing only the shortage behavior through the route editor, the unavailable load skipped and the empty ship departed for Northport, five days away. |

Chart hover, stop addition/removal/reordering, multi-good editing, advanced settings, canceled editing and review preserved the exact serialized economic state at **day 8, revision 27**. Applying the two-town route changed revision to 28 without running it. Starting then bought the two whole cargo units for **38 coins** and departed, using **three operations that day**. Pausing and resuming preserved cargo, the six-day voyage and that budget. After actual arrival on day 14, the two sales received **8.82 net coins** and the ship physically departed back to Northport.

This witness lost **29.18 coins** on the two instantaneous cargo purchases/sales; it is deliberately not a claim of profitable routing. Voyage-day factory wages and other world activity are excluded from that dockside comparison. All-account money remained **11130 coins** at every recorded stage.

After completing that loop, the player applied the third stop with Load 1 Flour at Westhaven, which had no company warehouse. Actual execution reached the missing-storage **Wait** on day 29. Repeating Run dockside preserved the complete serialized state at **revision 60**, including cash and cargo. Pausing and changing that instruction to Skip was legal at the untouched stop. Restarting consumed **two operations**—the skip and departure—without payment, cargo creation or storage construction. The ship then paused on its five-day return voyage.

Actual disk persistence was exercised twice: Save/Continue across an Editor recompile restored exact **D8/R28**; later Save → advance → Load restored exact **D29/R65**. The latter included company cash **1210.5537**, the paused voyage, two operations already used that day, factory rate/priority and the warehouse rules. The exact saved text matched after restoration. Original user slots are handled by the lead's backup/restoration process; neither slot screenshots nor raw saves are published here. Local evidence is `.cache/task30-route-journey-evidence.json` and `.cache/task30-factory-journey-editor-evidence.jsonl`.

The initial inline-order controls were functionally exercised, but screenshot review revealed that their expanded form opened below the viewport. Those screenshots are excluded from acceptance evidence. The corrected form scrolls into view after layout, focuses quantity and starts advanced controls collapsed. The final automated regression and actual Windows captures below verify this correction. Earlier Editor route captures also precede the final friendly ship-name label; the Windows captures show the final source.

## Actual Windows Mono continuation

The final development build ran as a real Windows Mono player. UI Toolkit pointer, navigation-submit and keyboard events operated its visible controls through the Unity CLI; read-only reflection observed snapshots and exact serialized text. No direct simulation command, invented stock, replayed economy or edited save supplied the witness. Resolution and text preference changes were presentation-only. The player was set to 60 rendering frames per second for this test process; this did not advance economic time or change project settings.

1. **Continue:** the main-menu button restored the Editor's exact **D29/R65** save, including the paused Westhaven → Northport voyage, five remaining days and `OperationsDay=29 / OperationsUsed=2`. Company cash was **1210.5537 coins**. Selecting the ship and opening screens preserved the exact save text.
2. **Inspect:** route, existing Logs/new Bread inline edits, Industry, Bakery and Warehouse Buy/Sell were inspected at **1920×1080 Default** and **1366×768 Large**. Both inline forms automatically exposed quantity, Save and Cancel, with quantity focused. Cancel and screen/scale changes preserved the entire serialized economic state. The ship picker displayed **Ledger**, not an internal identifier.
3. **Physical arrival:** five explicit Next day actions completed the real return voyage on **D34/R70**. The paused itinerary did not execute orders or reset its stored cursor/budget. Factory and town processes ran normally on those days.
4. **Trading A:** bought **1 Grain for 20 coins**, then sold it for **18 gross − 0.36 fee = 17.64 net**. Cargo returned to its previous state; neither trade advanced the day or changed paused-route progress.
5. **Trading C:** loaded **1 actual Bread** from the company warehouse, then unloaded it. Cash and company accounting stayed unchanged; warehouse stock and ship cargo returned exactly. Trading B subsequently displayed that same restored warehouse inventory.
6. **Save/load:** slot 3 Save → one explicit day → Load restored **D34/R74** byte for byte, including **1207.8517 coins**, rate **50%**, priority **High**, **8 completed Bakery batches**, **48 wages**, the **100-coin buy reserve** and warehouse Bread **2**, Flour **4**, Logs **4**. Its SHA-256 was `a9e3c27cc01533b3c89feb070dc0349a47157448472f02dc31225dac649fde56`.

These are unedited captures of the running final player. They are compared with the same Figma source states as the Editor evidence; the player watermark identifies their origin. The route/inline and factory captures were repeated after the save/load journey, so their real state is D34 rather than the earlier D29 inspection. No screenshot is used as runtime art.

| Actual Windows capture | Comparison / observation |
| --- | --- |
| [Town after arrival](design/task-3.0/evidence/player-arrival-hud-1920-default.png) | Selected-town HUD `5:2`; actual Northport public sites and household needs after the real voyage. |
| [Route, 1920 Default](design/task-3.0/evidence/player-route-1920-default.png) / [1366 Large](design/task-3.0/evidence/player-route-1366-large.png) | ROUTE V2 chart and sailing order; three real stops, five instructions, current dock, 14 sailing days and a paused route. |
| [Existing Logs, 1920 Default](design/task-3.0/evidence/player-inline-logs-1920-default.png) / [1366 Large](design/task-3.0/evidence/player-inline-logs-1366-large.png) | ROUTE V2 inline edit; quantity is focused and Save/Cancel are fully visible without helper scrolling to the form. |
| [New Bread, 1920 Default](design/task-3.0/evidence/player-inline-bread-1920-default.png) / [1366 Large](design/task-3.0/evidence/player-inline-bread-1366-large.png) | The same reusable form creates a new instruction; cancel leaves the applied route untouched. |
| [Bakery, 1366 Large](design/task-3.0/evidence/player-factory-1366-large.png) | Actual recipe, inputs and retained 50% rate; large-text body scrolls rather than shrinking text. |
| [Warehouse Buy, 1366 Large](design/task-3.0/evidence/player-warehouse-buy-1366-large.png) | WAREHOUSE V2 `193:1057`; real Buy reserve, two enabled buy rules, selected Bread rule and current stock/quote context. |
| [Paid Grain purchase](design/task-3.0/evidence/player-trading-a-1920-default.png) | Trading A `33:2`; actual post-purchase cargo 1, current market and company cash. |
| [Bread loaded without payment](design/task-3.0/evidence/player-trading-c-1920-default.png) | Trading C `34:306`; actual warehouse/cargo flow with no settlement. |
| [Restored local warehouse](design/task-3.0/evidence/player-trading-b-1920-default.png) | Trading B `34:36`; paid local-stock interface after the no-payment roundtrip. |

The local evidence is `.cache/task30-player-continuation-evidence.jsonl`; it records the comparisons and exact-save passivity assertions. Raw save evidence remains ignored and unpublished. Initial player captures included Unity's development-console overlay for the pre-existing native plugin warnings below. The overlay was dismissed before the curated captures, without deleting the log or changing game state.

## Visual evidence coverage

The [HUD/trading evidence](design/task-3.0/HUD-TRADING-EVIDENCE.md) covers all three HUD states, Trading A/B/C, direct paid/free transfers, and small-viewport text scaling. The [Warehouse evidence](design/task-3.0/WAREHOUSE-EVIDENCE.md) compares all five named current V2 states, invalid-policy repair and actual later automatic procurement/sales. The eight Bakery images and five route images above are actual running Editor captures; the Windows section shows the final player. Earlier `.cache/task30-live-editor-first-*` images are diagnostic captures used to find layout defects; they are not final acceptance evidence. Cached Figma screenshots are design references, not running Unity.

Reference coverage:

- HUD town `5:2`, ship `20:2`, no selection `20:199`; Trading A `33:2`, B `34:36`, C `34:306`; Industry overview `50:566` and factory `51:697`: actual Editor evidence linked above.
- Bakery workflow `86:741`, `86:879`, `86:1182`, `86:1430`, `86:1887`, `86:2135`, `86:2383`, `86:2631`: all eight Editor states at 1920×1080 Default, plus actual 1366 Large factory evidence.
- Latest route states `144:2980`, `144:3136`, `145:3164`, `145:3337`, `170:3226`, `170:3549`, `171:3288`, `174:3319`: inspected as design authority; actual chart/applied/voyage/Wait/Skip and final Windows inline-order captures are linked above.
- Warehouse `193:1057`, `193:1360`, `215:1340`, `215:1797`, `216:1513`: all five actual Editor states compared and linked, plus 1366 Large evidence.

Actual Editor inspection covered **both 1920×1080 and 1366×768, each at Compact, Default, Large and Extra Large**. Exact SaveText remained unchanged for every presentation-only size/scale change. The curated image subset concentrates on representative Default/Large states and the narrow Extra Large density limit; it does not imply a separate capture of every screen at every scale. Windows Mono coverage used 1920 Default and 1366 Large; its Compact/Extra Large combinations were not separately repeated outside the Editor.

## Adaptations and remaining UX debts

- Actual seeded geography, stock, prices, finances and recipes replace Figma's sample data. The existing typography and inherited layout scales are retained rather than hardcoding prototype pixel dimensions.
- Industry uses individual actual site cards, standard ON/OFF controls and numeric zero for an unrestricted price. Larger text sizes use scrolling. The warehouse reserve is honestly one buy-only reserve on both tabs; autosave applies a policy to the current simulation, while saving a run to disk remains explicit.
- At 1366×768, Large initially shows two full trading rows and Extra Large shows one; the remaining goods require scrolling. Factory Apply also requires scrolling in the captured 1920 Default layout. These are density/reachability tradeoffs rather than all-controls-at-once reproduction of the prototype.
- The route hover information uses a fixed-position panel. Cargo displays the current physical hold, not an invented arrival forecast, and route legs use a uniform gold treatment. Save order edits a draft; Review/Apply assigns it; Start/Pause controls execution separately.
- The bottom fleet attention card is cached with the economic snapshot. Selecting another owned ship can leave that correctly named card pointing at the previous ship until the next economic commit. This does not change the selected ship's orders or cargo.
- The Windows player logs the existing `AMDUnityPlugin` and `NVUnityPlugin` x64/unknown-architecture startup warnings and a D3D12 info-queue query warning. The AMD/NVIDIA warnings were already documented in Tasks 2.6–2.8; they remain unresolved platform issues. No managed gameplay exception appeared in this player run, and the actual build/continuation/trade/save checks passed. The temporary development connector is not a production dependency.
- No unsupported future feature, rejected Trading D, speculative five-input factory or archived warehouse Review/Apply option is exposed. The complete eight-state Bakery and five-state Warehouse mutation sequences were exercised in the Editor; Windows verification continued that real configured run rather than repeating every edit from a fresh world. No other platform or non-development player build was tested.

## Final audit and cleanup

The full authoritative task was reread for final acceptance, and the 22 implementation/verification rows above were checked individually. An independent read-only source audit found no additional blocking defect and confirmed no simulation, canonical content, session, schema or report changes. The limitations above distinguish tested behavior from visual adaptations and checks not repeated on every platform.

Both task-owned verification processes exited. All three original local save slots and four Editor/build-modified project files were restored from the fresh pre-task backups, with exact SHA-256 and modification-time checks. The temporary build connector configuration and empty generated connector folders were removed. The pre-existing IET settings change, README-PACKAGE and Recovery assets remain untouched. Raw saves and local diagnostic logs are not published.

After evidence curation, content validation, all 59 generated reference freshness checks, Unity staging freshness, strict MkDocs and `git diff --check` passed. The 12 Windows screenshots are byte-identical copies of their actual player captures, with PNG dimensions verified. There are no remaining blocking implementation or verification steps for this milestone; the explicit platform, scaling and visual limitations above remain.

No GitHub Pages publication or configuration changes are part of this task.
