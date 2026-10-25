# Task 2.3 verification

This record distinguishes deterministic simulation checks from actual rendered Unity interaction. Task 2.3 adds ordered player itineraries to new pooled v3 runs without changing the economy, world generation, NPC policies or historical report fixtures.

## Authoritative contracts

The [automation guide](AUTOMATION.md) documents the four commands, bounded stops/actions, exact order quantities, skip/wait, mixed cargo, eight-operation budget and safe replacement. Itinerary format 1 is independently identified; schema 4 saves retain the entire route and next unfinished command. Existing schema 1–3 runs keep their original rules and route controls.

## Headless evidence

- Final full Release .NET suite: **972/972 passed**, zero skips, 2m38s, including the additional malformed-save coherence checks.
- Focused economic tests: **25/25 passed**. They include three independent player ships, contention, exact marginal prices, reserves, full mixed holds, skip/wait, repeat-click/edit budget protection, manual equivalence, stale decisions and rollback after an exceptional later production phase.
- Combined historical/new save tests: **121/121 passed**. Final hardened itinerary save tests passed **59/59**, including real paid FIFO after two completed actions, mid-voyage, terminal state, paused manual travel and recorded shortage decisions consistent with their command policy.
- All 17 restore/build/demo/report checks passed, including pricing, archipelago, enterprise, industrial logistics, runs, seeded runs, living cities, expanding economy, reinvestment and Grand Commission. Historical report bytes remain unchanged. No deep sweep ran.
- Content validation, 59-file generated-reference freshness, **58/58 Python tests**, strict MkDocs and Unity staging/freshness passed. The first Python attempt had sandbox temporary-directory errors; rerunning with normal temporary access passed after adding the new help topic to the expected catalogue.

### Ordinary seed-99 witness

The unchanged Merchant / StockOnly / Ascension 0 world pays 300 coins for a Westhaven Bakery, sails there by day 3, then enables the example route. Only normal day advances occur afterward. A is Westhaven, B Southhaven and C Northport. Actual returns to A occur on days **17, 31, 45 and 59**.

| Day-60 measure | Observed result |
| --- | ---: |
| Funded Bakery batches / Bread produced | 7 / 14 |
| Private wages | 42 coins |
| Company cash | 1548.5336 coins |
| Reference Company Value | 2553.5336 coins |
| Inventory purchases / sales revenue | 209.6664 / 300.2 coins |
| Released / held material basis | 128.3332 / 81.3332 coins |
| Realized material margin / surplus after wages | 171.8668 / 129.8668 coins |
| Grain bought and sold | 16 units each |
| Flour and Logs bought and unloaded | 8 units each |
| Bread loaded / sold | 12 / 8 units |
| Final owned goods | 6 Bread, 1 Flour, 1 Logs |
| All-account money | 11730 coins, unchanged |

No Mint issuance, external inflow, free supply or manual cargo correction occurs. Every operation reconciles physical flows, accounts and FIFO. Reversed configuration order reproduces the same fingerprint. This bounded observation demonstrates the requested route; it does not establish indefinite economic sustainability or universal seed viability. Reference value and retained output are not realized profit.

## Actual Unity verification

Unity **6000.6.6f1** compiled the final host without errors or warnings. Full Edit Mode passed **98/98**. Final full Play Mode passed **69/70**, including **all seven itinerary tests**. The remaining failure is the existing `SeededRunExperienceTests.SetupValidatesAndPreviewsTheExactWorldWithoutChangingTheCurrentEconomy` clipboard assertion: copying seed -42 reads back an empty system clipboard. A direct temporary clipboard round-trip also failed in the stopped, non-batch Editor, including after focusing it. The test remains unchanged and failing; this is an explicit environment verification limitation, not a green full-suite claim.

The first Play run exposed invalid new-draft IDs and child-label change events overwriting numeric drafts. Both were fixed, then the full suite reran. Independent review also found that a rejected off-route enable could leave the manual checkbox displaying the attempted state; it now refreshes from the snapshot and retains the rejection reason. The seventh itinerary test covers this rejection.

### Editor journey

The running Editor used ordinary setup, Industry, map, Fleet and planner controls. It bought the Westhaven Bakery for300 coins, sailed there by day3, drafted the example and exercised add/remove/reorder, keyboard quantity/reserve input and Skip/Wait choices. All draft edits preserved day3/revision5/cash1500. Its remote Southhaven Grain preview showed exactly30 coins for4 units and was explicitly hypothetical. Apply and enable were separate visible actions.

After enablement, only Next day advanced the route. Actual returns on days17 and31 were inspected and captured; day17 carried2Flour and2Logs. On day32 the ship departed with4Bread from the paid Bakery. Continued through day60, the Editor matched every economic figure in the headless table above, at revision64. The company's factory had7 funded batches, consumed7Flour/7Logs and paid42 wages. No manual cargo move occurred after enabling.

The day5 voyage was saved and loaded through the local-slot screen with **identical full serialized bytes** before any further step. A separate ordinary seed42 Independent run bought1Grain and1Logs, then waited on an absent Silver sale as its third action. Saving/loading and pressing Run dockside again retained identical bytes, day0/revision3/cash1483, mixed cargo and next action3. Earlier purchases were not repeated.

Both1366×768 Default and1920×1080 Large were reviewed, including real keyboard text/dropdown input, internal scrolling, persistent footer controls, action disclosure, Harbor/Industry links and NavigationCancel back to Fleet. The live Editor console reported no new errors. Input was delivered through official Unity CLI UI Toolkit navigation, key and pointer events to the visible controls; scripts did not call economic mutation APIs.

### Windows Mono player

The accepted source built successfully as a Windows x64 Mono development player. Process1 repeated setup, construction and route application, saved on day5 and exited through the UI with code0. A fresh process used **Continue saved voyage**, restoring the exact pre-exit state, then completed the same route through day60 and exited with code0.

Full save bytes matched the Editor at **days5,17,31,32 and60**. The day60 full-save SHA256 is `be29592ae9ec0c05d316338d4aa3b3ace0c85a5ee1cdb8983cf9e87cd969d39e`. Both required display sizes/scales were rendered and reviewed in the player too. The player produced the same seven batches, wages, cash, value and physical holdings; no manual cargo correction or economic shortcut was used.

Raw player startup logs retain the pre-existing optional AMDUnityPlugin/NVUnityPlugin architecture-load diagnostics. The development-console overlay was closed for unobstructed screenshots; those warnings were not erased from the logs. Shutdown logs contain the existing ComputeBuffer disposal/native graphics cleanup warning. Both normal exit markers and exit0 were recorded, with no managed gameplay exception found. Physical hardware input, IL2CPP and non-Windows players remain unverified.

The three original local save slots were backed up before testing and restored afterward; all three original SHA256 values match. Build-only settings and temporary QA resources were restored/removed. The Editor ended stopped with background execution false and auto-tick disabled. Private saves, raw logs, generated staging and executable builds remain ignored. No Pages publication or settings change occurred.

### Reviewed screenshots

- [Editor: passive three-town draft, Default](images/task23/editor-draft-default.png)
- [Editor: exact hypothetical quote and editable limits, Default](images/task23/editor-remote-quote-details-default.png)
- [Editor: first return with Flour and Logs, Default](images/task23/editor-first-return-default.png)
- [Editor: second return and exact next work, Large](images/task23/editor-second-return-large.png)
- [Editor: actual Bakery batches, wages and inputs, Large](images/task23/editor-paid-bakery-finances-large.png)
- [Editor: waiting third action after two paid orders, Default](images/task23/editor-waiting-third-default.png)
- [Windows player: second physical return, Default](images/task23/player-second-return-clear-default.png)
- [Windows player: day60 Bread voyage, Large](images/task23/player-day60-large.png)

## Criterion-by-criterion acceptance

The authoritative Task2.3 specification was reread before this audit. “Verified” identifies the exercised implementation, not every seed or platform.

| Criterion | Result and evidence |
| --- | --- |
| A1 immutable bounded policy and legacy compatibility | Verified:2–4 unique connected stops,1–8 commands each/24 total; bounded copied collections, canonical references and v3/pooled/player/version checks. Historical reports unchanged. |
| A2 four commands, quantities and constraints | Verified: whole minimum/maximum, relevant-only price/funds/reserve rules, Skip/Wait; shared exact trade calculator and actual docked FIFO transfers. |
| A3 authoritative progress and departure | Verified: immutable policy, cursor/pass, enabled state, decision/reason and saved daily budget; departure only after completed/skipped instructions. Exact continuation tests and live saves. |
| A4 mixed cargo and no duplicate work | Verified: good-specific finite hold actions, retained leftovers, full-hold tests; two paid actions followed by a repeated wait preserve cash/cargo exactly. |
| A5 atomicity, stale protection and bounded daily work | Verified:8 operations including skips/departure, repeated-click/edit budget guards, staged reevaluation, stale decisions and exceptional later-production rollback. |
| A6 passive editing and safe application | Verified: actual passive add/remove/reorder/field edits; explicit Apply, pause/dock/partial-stop/day-budget guards; equivalent application preserves progress, old/new policies are exclusive. |
| A7 historical NPC behavior | Verified: unchanged three NPC routes and finite shared trade engine; deterministic witness and legacy reports. |
| B1 Fleet planner and example | Verified: ordinary visible Fleet route creation, ordered cards, all fields and role-resolved passive example. New custom draft works without using the preset. |
| B2 accurate state, quotes and blockage | Verified: actual dock/voyage/cargo/cursor, per-action stocks/accounts/reserves and exact bulk total; remote label and visible absent-Silver wait. |
| B3 manual control and navigation | Verified: pause preserves state; rejected enable resets checkbox and explains why; Play regression and actual Harbor/Industry/Back navigation. |
| B4 compact accessible presentation | Verified through UI Toolkit input and actual captures at both specified sizes/scales; one expanded action and internal scrolling, inherited text scale, stable footer. Native hardware input is unverified. |
| C1 versioned complete policy saves and old formats | Verified: identity itinerary1/schema4, no reroll on route change; omitted old fields, schema1/2 tests and pinned original schema3 bytes. |
| C2 accounting and ownership | Verified: ordinary trade/transfer/FIFO paths, per-operation cash/physical/material audit, exact manual equivalence and matching live money/value/outcomes. |
| C3 exact continuation, invalid state and freeze | Verified:59 focused save cases, resealed malformed state rejection and terminal freeze; actual mid-stop reload and mid-voyage fresh-process Continue. |
| C4 bounded deterministic operation | Verified: finite policy/actions/history, no random draws or hidden supply, unchanged actor order; independent three-ship and reverse-configuration tests. |
| D primary real A→B→C integration | Verified headless, Editor and Windows player: multiple full loops, four traded/transported goods, actual shared-stock supply and paid Bakery production. |
| D single-stop precision | Verified: independent Grain/Logs orders, exact marginal totals/caps/reserves, capacity and skip-then-depart cases. |
| D insufficient funds/stock | Verified: distinct Skip/Wait outcomes, finite cash, preserved next action, no duplicate debit or phantom transfer. |
| D mixed goods and location | Verified: mixed Bread/Flour/Logs tests, actual mixed return, capacity shortage and remote-unload rejection. |
| D hardening and regressions | Verified: three owned ships with one blocked, exact saves, rollback, corruption, old two-port presets and all golden reports. |
| D actual ordinary UI journey | Verified: visible setup/application, two observed return arrivals and day60 completion, keyboard/back/edits/block reason, both sizes and Windows player. |
| D scope and verification gates | .NET972, Python58, Edit98, seven new Play tests, content/docs/prep/reports/build/player pass. **Full Play remains69/70 because this environment cannot round-trip the system clipboard.** No deep sweeps, retuning or NPC redesign. |

The functional Definition of Done is demonstrated: one owned ship applies a real three-port/four-good plan through visible UI and completes repeated physical cycles without manual loading, with exact authoritative amounts, money, FIFO, location, time and save continuation. Old modes and saves remain supported. The outstanding verification limitation is the unrelated clipboard-dependent full Play assertion; repeat that check on a desktop with working clipboard access before claiming an entirely green Unity suite.

For local repetition use the [playtest steps](PLAYTEST.md#task-23-ordered-cargo-itinerary-acceptance). Useful next work is human route-planning usability feedback and targeted strategies on other seeds, while retaining these bounded witnesses and historical fixtures. Broader economic retuning and optimizer/NPC systems remain outside this milestone.
