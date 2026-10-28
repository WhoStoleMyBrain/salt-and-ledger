# Task 2.8 — Factory and Warehouse Automation verification

Baseline `3d8732ca86353c41fd5bb73678ac818e95c00920`. Task2.8 separates private production controls from town-wide automatic local trades. Canonical recipes, prices, population, objectives, ship capacities and route execution are unchanged. Save schema6 explicitly replaces schema5; obsolete loads preserve the active session and do not migrate files.

## Requirement audit

| Requirement | Implementation and evidence | Status |
| --- | --- | --- |
| Three primary factory settings | Enabled, integer0–100 rate with25/50/75/100 presets, Low/Normal/High priority; one atomic snapshot-bound Apply | Passed engine, Unity and live Editor/player checks |
| Whole-batch scheduling | Bounded production credit admits complete opportunities; skipped slots advance, admitted shortages retry without accumulating credit; controls preserve phase/deadline | Engine scenario tests passed |
| Scarce shared inputs | Stable port/priority/ordinal-ID order reads actual pooled stock; full wages only on success | Engine priority/rate scenarios and ordinary funded witness passed |
| One warehouse policy | Actual company/town pool owns policy and trading counters; factory policy API rejects pooled use | Engine and persistence tests passed |
| Buy/Sell all commodities | Separate tabs, seven artwork rows, stock targets/reserves, marginal limits and company cash reserve; logistics-only goods allowed | Engine/Unity tests and actual warehouse purchases/sales passed |
| No circular trades | Both-enabled BuyTarget<=SellReserve; at most one exact ordinary transaction per good per phase | Threshold/price/partial/cash/capacity scenarios passed |
| Correct daily ownership | Buying before private production, selling afterward; enabled/rate-independent and actual paid pending storage permitted | Pooled-storage scenarios passed |
| Accessible controls and blockers | Harbor direct entry, sibling Industry tabs, authoritative current previews, conditional12-day private flows and input-rule explanations | Full Unity suites passed; live direct entry and layout verified |
| Existing operations/accounting | Manual stock, real dock transfers, itineraries, finite prices/liquidity, FIFO, full wages and atomic rollback retained | Full .NET759/759 passed |
| Exact saves and safe failure | Schema6 rate/priority/admission and explicit bounded warehouse fields; unsupported schema rejected before replacement | Save/corruption tests, Editor round trip and exact player Continue passed |
| Diagnostics and experiments | Warehouse counters/policies separated from factory batch/wage data; format3 reports disclose intentional consolidated policies | Per-command audits and all current report freshness checks passed |
| Documentation/help | Mechanics, player guide, Harbor, automation, saves, current experiment analysis and generated shared help updated | Final Python58/58, generated reference, strict docs and Unity staging freshness checks passed |
| Real multi-factory interaction | Paid Flour voyage, Lumber Camp, three competing Bakeries; reduce rate/change priority using existing eligibility | Headless two-arm witness, actual Editor rate change and player priority change passed |

## Actual checks

- Locked .NET restore and Release solution build: passed, zero warnings/errors.
- Full .NET: **759 passed,0 failed,0 skipped**. The first run identified two outdated numerical reinvestment expectations; exact new results replace them, including remaining food affordability shortfalls.
- Focused engine:15 passed; related regression subset99 passed; save plus ordinary witness23 passed. These overlap the full suite and are not additional totals.
- Final Python:58 passed. Canonical content validated. Generated59 reference files and freshness check passed. Strict MkDocs including this evidence page passed; Unity staging freshness passed with final shared help.
- Final format3 generation and byte-exact `--check` passed for29 expanding cases/5220 days,80 circulation/control worlds/29200 days and12 existing reinvestment cases/4380 days. Every committed operation audited money, physical goods and FIFO. Grand Commission `--check` passed the unchanged seed99/42 manual wins, seed99 itinerary win and idle terminal failure.
- Unity6000.6.6f1 / CLI1.0.0-beta.14: compilation passed. Full Edit Mode **64 passed,0 failed,0 skipped** (234 seconds); full Play Mode **71 passed,0 failed,0 skipped** (152.46 seconds). Initial focused Play test5/6 exposed a test input exceeding the representable integer stock bound before reaching capacity validation; the corrected scenario passed in the full suite. Counts include updated disabled/pending signals and primary-control visibility assertions.
- Windows Mono development build succeeded; rebuilt with the final generated help before live player verification.

## Funded scarcity witness

Seed99, Merchant Company, StockOnly, Ascension0, Sandbox. Ordinary commands physically buy10 Flour at Northport and return it to Southhaven by day12. Construction pays1120 coins for one Lumber Camp and three Bakeries; real ship-to-warehouse transfer preserves the paid material basis. Camp priority is High; warehouse rules buy Flour toward6 with100 cash reserve and sell surplus Bread.

By day14, the Camp supplies2 Logs and the Bakeries have produced1/1/0 batches. Real Flour remains; Logs are the scarce input. Reducing the first Bakery to50% produces1/2/1 batches by day16. The separate priority arm produces2/1/1. Both complete2 Camp batches and4 Bakery batches:8 Bread,36 coins of full wages,522.5652 remaining cash. Exact schema6 continuation agrees after each subsequent day. No test grants, market replenishment or account repair are used.

## Measured economic effects

Reports intentionally replace conflicting facility rules with one warehouse policy: same-good buy targets are retained before surplus sales, and disabling production no longer disables town trading. This changes some prescribed research strategies, not canonical economic parameters.

Fourteen of29 expanding cases retain unchanged economic checkpoints; all40 idle circulation/control cases remain unchanged. In32 managed circulation arms, paid Bread improves18/worsens8/ties6; player surplus improves24/worsens8. The unchanged idle cases isolate the automation change from an economy retune.

StockOnly reinvestment seeds42/99 improve processing-town recent food fulfillment from0% to57%, with private Bread260/326 versus104 previously. They still record food affordability shortfalls17.4617/35.2644 physical Bread units. Coverage counterexamples retain operating losses and shortages. More private output does not guarantee universal or permanent welfare improvement. See [current productive results](EXPANDING_ECONOMY_EXPERIMENTS.md) and [circulation policy comparison](ECONOMIC_CIRCULATION_REVIEW.md#task28-warehouse-policy-comparison).

## Live economic walkthrough

The Editor journey completed the paid Flour round trip and actual construction, warehouse configuration and rate adjustment through visible UI controls. Day16 matched the headless rate arm exactly:8 Bread,36 full wages,522.5652 cash and Bakery batches1/2/1. A local slot3 save/load preserved exact bytes at day16/revision27. All factory/warehouse tab, selection and Default/Large scale inspection across1366×768/1920×1080 preserved the same saved state.

The first screenshots exposed controls below the fold. The final layout places Enabled/Rate/Priority and preset/Apply rows first, keeps concise current readiness, recipe, actual input stock and full wage nearby, and moves extended timing/accounting into existing disclosure. At1366×768 Large, these core controls and facts now fit before scrolling. Priority labels and checkbox width were corrected from actual captures; active tabs have green/gold treatment.

A scene-opening request and a first post-reload script request exceeded the Pipeline's5-second main-thread timeout. The same target Editor remained reachable, the intended scene was confirmed open/ready, and bounded script timeout was increased for subsequent QA. No Editor replacement or raw economic-state injection was used. UI verification uses frame-spaced UI Toolkit keyboard/navigation and dropdown pointer events in a real running scene, not direct simulation calls.

The Windows Mono player's **Continue** restored that exact Editor day16 save: SHA256 `51d92297c11075a7a5a92435a14b68154f2c00593a861fe9dba94dfb0ee6237c`. Harbor opened the shared Warehouse rules directly. Using visible controls, the player increased the Flour target to8, set the first two Bakeries to Low priority and left the third Normal and Camp High. Day17 bought2 Flour for44.6666 actual coins. Day18 gave the scarce Logs to the third Bakery and then the ordinal first Low-priority Bakery: cumulative batches2/2/2, Camp3,12 total Bread,54 full wages,463.8186 cash. Six Flour remained in the warehouse, with no Logs or Bread held after the configured sale. This is scarce-input allocation and paid circulation, not a claim that this production strategy is profitable.

## Screenshots and scope of verification

Actual running-game captures use the existing maritime artwork. No new assets or licensing dependencies were introduced. Screenshots show bound UI and rendering; economic/save assertions establish transactions and continuation. Automated input consists of frame-spaced UI Toolkit keyboard/navigation and pointer/dropdown events; native hardware input and an independent human usability study are not claimed.

At1366×768 Large, the factory's primary controls and current facts fit before scrolling. The warehouse uses one scroll view for all seven goods with fixed Buy/Sell navigation and Apply. Fewer rows fit at Large; targets/limits remain readable rather than being squeezed into tiny controls. Production/input totals are explicitly conditional and rate-adjusted. Prices and trading proposals remain current-snapshot facts, never future guarantees.

The player saved/loaded day18/revision32 exactly, exited normally, then restored the same bytes through **Continue** in a second fresh process: SHA256 `b62aae8df8682e88427326494de733158c114391cf6392f4abe16fb06e51bab3`. Both final Editor and player inspection matrices preserved their exact respective saved bytes. The 24 native-resolution screenshots below are unmodified captures, not mockups.

| View and size | Editor | Windows Mono |
| --- | --- | --- |
| Factory, 1366×768 Default | [Capture](images/task-2.8/editor-factory-1366-default.png) | [Capture](images/task-2.8/player-factory-1366-default.png) |
| Factory, 1366×768 Large | [Capture](images/task-2.8/editor-factory-1366-large.png) | [Capture](images/task-2.8/player-factory-1366-large.png) |
| Factory, 1920×1080 Default | [Capture](images/task-2.8/editor-factory-1920-default.png) | [Capture](images/task-2.8/player-factory-1920-default.png) |
| Factory, 1920×1080 Large | [Capture](images/task-2.8/editor-factory-1920-large.png) | [Capture](images/task-2.8/player-factory-1920-large.png) |
| Warehouse Buy, 1366×768 Default | [Capture](images/task-2.8/editor-warehouse-buy-1366-default.png) | [Capture](images/task-2.8/player-warehouse-buy-1366-default.png) |
| Warehouse Buy, 1366×768 Large | [Capture](images/task-2.8/editor-warehouse-buy-1366-large.png) | [Capture](images/task-2.8/player-warehouse-buy-1366-large.png) |
| Warehouse Buy, 1920×1080 Default | [Capture](images/task-2.8/editor-warehouse-buy-1920-default.png) | [Capture](images/task-2.8/player-warehouse-buy-1920-default.png) |
| Warehouse Buy, 1920×1080 Large | [Capture](images/task-2.8/editor-warehouse-buy-1920-large.png) | [Capture](images/task-2.8/player-warehouse-buy-1920-large.png) |
| Warehouse Sell, 1366×768 Default | [Capture](images/task-2.8/editor-warehouse-sell-1366-default.png) | [Capture](images/task-2.8/player-warehouse-sell-1366-default.png) |
| Warehouse Sell, 1366×768 Large | [Capture](images/task-2.8/editor-warehouse-sell-1366-large.png) | [Capture](images/task-2.8/player-warehouse-sell-1366-large.png) |
| Warehouse Sell, 1920×1080 Default | [Capture](images/task-2.8/editor-warehouse-sell-1920-default.png) | [Capture](images/task-2.8/player-warehouse-sell-1920-default.png) |
| Warehouse Sell, 1920×1080 Large | [Capture](images/task-2.8/editor-warehouse-sell-1920-large.png) | [Capture](images/task-2.8/player-warehouse-sell-1920-large.png) |

Both Windows processes exited with code0 and no managed gameplay exceptions. The existing native `AMDUnityPlugin`/`NVUnityPlugin` architecture warnings still occur; the development console was dismissed for final captures, not removed from logs. This task does not fix those platform warnings.

The QA players were closed and the Editor left stopped. Temporary QA tick/build overrides and generated Pipeline settings were removed. All three original user save slots and four build-sensitive project files were restored with matching SHA256 hashes and original modification timestamps. Original older slots are preserved even though schema6 now rejects them. Unrelated pre-existing repository changes were excluded. No raw private saves or generated binaries are published, and no Pages publication occurred.

## Remaining limits

- No required Task2.8 functionality remains knowingly incomplete. Schema5 and older saves are deliberately unsupported, with graceful active-session preservation covered by tests.
- Default/Large have actual Editor/player evidence at both resolutions. Compact/Extra Large retain existing layout-token behavior and automated readability coverage; a new independent native-device usability pass for those sizes was not performed.
- Large warehouse rows require vertical scrolling, and duplicate facilities still use stable IDs to distinguish dropdown choices. These are remaining presentation debts, not extra gameplay systems.
- Rate-adjusted output is conditional capability, not guaranteed supply. Warehouse proposals are independent current-state previews sharing real money/capacity; previous rules and intervening actors can change actual fills.
- Legacy per-factory trading survives only in isolated nonpooled research fixtures. Supported pooled worlds reject that API and have one warehouse policy. No competing player automation model remains.
