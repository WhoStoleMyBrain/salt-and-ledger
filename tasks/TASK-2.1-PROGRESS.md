# Task 2.1 — Grand Commission progress

Authoritative specification: `tasks/TASK-2.1.md` (read completely). Baseline checked: `a99c59b01c59372b6a3242c68ec59f33bae342f1`. Preserve pre-existing Unity Learn settings change and `_Recovery` assets. No Pages publication or economic retuning.

## Integrated contracts (inspection milestone)

- Keep generator/content 3 and valuation 1. A separate mission draft version 1 uses a domain-separated seed stream per category; four pools of four templates yield three distinct ordered offers. Role targets come from unchanged v3 generation.
- `GrandCommissionDraft.Generate(seed, version = 1)` exposes four `Rounds` (category, ordered `Offers`). `Resolve(templateIds)` requires exactly one offered ID per round and returns immutable `GrandCommissionPlan` (`Seed`, `Version`, `Goals`, canonical `Signature`). Resolved goals have stable typed template/evaluation IDs and explicit role, port, good and exact target parameters. Reconstruct and compare the whole plan on restore.
- `RunIdentity.Commission` is optional and required only for `grand_commission`; constructor accepts it as the final optional argument. Its signature extends only drafted comparison groups. Existing JSON omits the absent field so old configuration fingerprints and report bytes remain stable.
- `GrandCommission.Progress(snapshot)` returns four typed goal progress records with exact conditions, clamped minimum-condition percentage, cumulative/current semantics and explicit blockers. Score is `floor(500 * sum(card percent) / 400)`. All four plus 100 coins by day 180 win; terminal guards stay authoritative. Existing v3 progress formulas stay unchanged.
- New `CommissionHistory` is optional immutable snapshot state. It records at most three owned ships in acquisition order with first-visit day per fixed port, and exact purchased/sold subunits for at most four ports × seven goods. Initial docking, real arrivals and commissioned docks count. Successful player ship and warehouse market trades count; previews, rejected actions and NPCs do not. Working copies stage history with all other state. Production/public outcomes are derived from existing trusted aggregates.
- History public API: `Ships` ordered `CommissionShipVisits` (`ShipId`, `AcquisitionOrder`, `Visits` port→first-day), `Markets` ordered `CommissionMarketBalance` (`PortId`, `GoodId`, `PurchasedQuantity`, `SoldQuantity`, signed `NetQuantity`). `RegionalWorldSnapshot.CommissionHistory` exposes it. No UI-owned progress.
- Save schema 1 remains the unchanged path for old runs; drafted runs use schema 2 with explicit optional identity/history DTOs. Validate known offer/resolved plan, bounded legal history, aggregate reconciliation, docking/time plausibility and terminal predicates; load never steps or replaces the active session on failure.
- UI owns only an unfinished selection. Setup reveals exact spatial map/capabilities first, then four rounds, review, explicit Begin. Dedicated Journal pauses playback, shows exact subconditions and semantics, and returns to its parent. Drafted HUD is compact. No unrelated panel redesign.

## Plan and ownership

1. Simulation contracts and atomic history/identity/challenge integration (lead).
2. Authored templates, deterministic offers and typed evaluation with focused tests (mission agent).
3. Strict schema-2 capture/restore and schema-1 compatibility tests (save agent).
4. UI Toolkit setup/map/draft/Journal/HUD/results and targeted Unity tests (UI agent).
5. Lead integrates subsystems, two genuine winning witnesses plus idle failure, full headless/Python/docs/staging, actual Editor/player journey and eight focused screenshot views where supported.
6. Update authored/generated help and docs; reread full spec and audit each of its 14 DoD items individually.

## Evidence and remaining work

- Core milestone: sixteen templates, four salted deterministic pools, typed evaluation, optional identity/signature, atomic history hooks and schema-2 restore are implemented. Full solution Release build passed with zero warnings/errors. Initial mission tests passed **15/15** (actual focused run); persistence tests are next. UI first implementation is on disk but not yet compiled in Unity or visually verified.
- Reviewed authoritative spec, repository guidance, v3 identity/challenge/world/save extension points, regional architecture, economy/mechanics/run docs and current setup/map host.
- Remaining: integrated save and atomic tests, two real-command wins plus idle control, full regressions, actual Unity interaction/screenshots, generated documentation, and final 14-item acceptance audit.
- Known baseline limitation: long-horizon v3 circulation is intentionally outside this milestone. Existing optional graphics-plugin diagnostics are not a reason to alter packages.
- Next: reproduce compact winning witnesses, run save tests, stage the DLL and compile the UI; review actual layouts and complete the live journey.

## Core decisions and files

- New simulation: `CommissionHistory.cs`, `GrandCommissionDraft.cs`, `GrandCommissionProgress.cs`; extended `RunIdentity`, `RunScenario`, `Challenge`, regional snapshot/world/development and v3 scenario. Save owner extends explicit codec/DTO/capture/restore and adds commission validation. `AcquisitionOrder` is zero-based (initial 0, second 1, third 2); only first-visit dates are retained, not an unbounded voyage journal.
- Mission draft version 1 uses per-category FNV domain initialization then a fixed three-draw LCG Fisher–Yates selection. Resolved parameter digest is SHA-256, independent of prose/culture. Thresholds: industry one funded batch per distinct-town Mill/Bakery, four Planks, two Furniture or four Bread; commerce four net goods or 100 export coins; civic one Mint issuance, six meals at 80% plus prosperity 50, value growth 100 plus cash 100, or two funded public Planks plus six meals at 60%. These are authored global targets, without economic retuning.
- Existing `ExpandingChallenges.Definitions` and progress formulas are untouched; `GetChallenges(v3)` prepends Grand Commission. Old identity/snapshot JSON omits absent commission data. Schema 1 remains the old path, with real old-binary save byte comparisons planned.
- UI files add exact map reveal, four rounds/review, dedicated Journal, compact HUD/results. Root owns all actual Unity processes and final verification.

## Integrated evidence milestone

- Two genuine ordinary-command witnesses now succeed (`samples/SaltAndLedger.Demo/GrandCommissionWitness.cs`, `--grand-commission --check`): seed 99 Merchant, All Harbors/New Industry/Flour for Bakers/Royal Institution, day 18, cash 648.6668, paid inputs 261.3331, wages 24, private capital 880, public commission 400, score 2164; seed 42 Independent, All Harbors/Timber Chain/Royal Exporter/Royal Institution, day 13, cash 295.6668, paid inputs 302.9998, wages 42, private capital 820, public commission 400, score 2129. Both pass exact mid-voyage and terminal save round trips. Idle seed99 fails on day180 with zero goals met. No grants or direct state edits.
- Mission tests **16/16 passed**, including actual paid-food recovery followed by decline. Save/atomic tests **28/28 passed**, including four old-binary schema1 byte hashes, real net reversals, commissioned ship history, resealed tampering, late exceptional rollback and real day180 arrival victory after day179 save/reload.
- First full headless suite: **839 passed / 1 failed / 840**; sole failure was historical expected v3 catalog count3. Updated it to four with explicit preservation of the old three; full rerun in progress.
- Unity6000.6.6f1 is available and initial compilation passed. First three new Play tests failed at an old HUD branch calling `ExpandingChallenges.Progress` for GC. Fixed explicit dispatch in `ArchipelagoTradeOverlay.RefreshChallenge`; rerun pending. No live UI journey or screenshot acceptance claimed yet.
- Staging initially hit sandbox NuGet network restriction; approved staging rerun passed. Current DLL/help staged. Docs generation/freshness, content validation, **58/58 Python tests**, strict MkDocs passed. Shared help and generated reference updated by documentation agent.
- Remaining: final full suite, corrected Unity tests, actual Editor/player journey and focused screenshots, manual playtest/Unity acceptance records, report hash audit, final spec/DoD audit.

## Verification milestone

- Full Release headless suite now **845/845 passed, zero skipped** (2m2s); includes five additional real-operation template tests. All nine published experiment JSON files match HEAD bytes/SHA-256. No economic tuning or publishing changes.
- Corrected new Unity Play Mode tests **3/3 passed** (4.74s). Full Editor suites and Windows player verification remain next.
- Actual live Editor frames advance. Seed99 Merchant was drafted through real UI Toolkit keyboard/pointer events, with the exact four witness goals. Reviewed actual PNG captures for map, draft, Journal and compact HUD at Default1366×768 and Large1920×1080; navigation remains fixed and details scroll internally.
- Live Editor day0 purchases funded the distinct-town Mill/Bakery, Silver Mine and public Mint. Day1 voyage was saved through previously empty local slot1 and loaded through the actual UI; existing slots2/3 preserved. Economic completion and exact serialized comparison still in progress. Input is automated UI Toolkit events via official Unity CLI, not physical hardware input.
- Next: finish Editor victory, inspect completed Journal/results, run full Unity suites, build/exercise Windows Mono player, finish documentation and fourteen-item acceptance audit.

## Live Editor completion

- Actual UI seed99 Merchant won on **day18/revision43**, cash **648.6668**. Ordinary construction, paid inputs/payroll, four real port visits, carried Silver issuance and final four-Flour sale produced all four required goals. No economic API or state editing was used by the UI driver.
- Serialized day1 pre-save/post-load bytes match exactly. Terminal save/reload bytes match exactly and remain frozen; actual Retry returns bytes identical to the original day0/commission. Slot1 was empty before verification; existing slots2/3 remain unchanged.
- Captured and reviewed actual live production Journal, final checkmarks and victory. One minor result-footer edge overflow is being corrected before the final player build. Full Edit Mode now **94/94 passed** (13.77s); full Play Mode running.

## Accepted-source player build

- Full Play Mode **58/58 passed** (67.78s). Results-only footer now wraps4+3; actual Editor terminal Continue and corrected screenshot reviewed. Targeted Grand Commission Play Mode rerun **3/3 passed** (3.85s). Full solution Release build remains zero warnings/errors.
- Locked restore initially failed due sandbox NuGet socket permissions; approved network rerun passed. Python **58/58** and content/generated freshness passed again; staging read-only freshness passed.
- Windows x64 Mono development build succeeded through the official Editor CLI. Temporary runtime Pipeline build setting was restored, and two build-generated project settings changes were restored to their pre-build contents. Existing Learn/Recovery edits remain untouched.
- Visible standalone process started at1366×768, frames advance; actual UI drafted the same seed99 commission. Player ordinary construction/transport journey and clean process Continue are in progress. Next: finish player, curate evidence, final docs/build/spec audit.

## Final acceptance milestone

- Reread the complete authoritative specification and audited **all fourteen DoD criteria individually** in `docs/TASK-2.1-VERIFICATION.md`. All implemented; no deferred required component. Authored rules remain unchanged economic v3, with separate mission version1 and schema2 only for drafted runs.
- Visible Windows Mono process1 saved day1 and exited via UI. Fresh process2 used Continue, restored exact bytes and reached **day18/revision43 victory, cash648.6668, score2164** via ordinary controls. Eight intermediate checkpoints match Editor full save bytes; terminal reload and exact day0 Retry also match. Process2 then exited via UI with code0; process1 exit marker/disappearance observed but numerical code not retained.
- Reviewed standalone final checkmarks and score, and retained12 compact evidence PNGs in `docs/images/grand-commission/`. Build simulation DLL matches staged SHA-256. Slot1 contains the verification victory; pre-existing slots2/3 preserved. Editor background override restored; generated build settings/assets cleaned without touching user Learn/Recovery edits.
- Final evidence:845/845 headless;58/58 Python;94/94 Edit;58/58 Play;3/3 targeted footer rerun; locked restore and Release build pass; content/generated/staged freshness pass; strict MkDocs pass; both winning headless witnesses plus idle failure reproduced. Nine historical report bytes unchanged. No Pages publication/workflow edits.
- Remaining limitations: physical hardware input, IL2CPP/non-Windows and all possible draft combinations are unverified; focused screenshots cover Default1366×768 and Large1920×1080. Existing optional AMD/NVIDIA plugin and native shutdown cleanup diagnostics persist. Bounded save history detects invalid claims but does not authenticate coordinated plausible rewrites. Long-horizon v3 economic limitations remain explicitly out of scope.
- Final review: all six legacy production/world/market/pricing-check/shipping/automation demos passed on the final Release build. Generated docs/staging freshness and strict MkDocs passed again after the acceptance record/gallery were added; diff whitespace check passes. No further feature implementation is outstanding. Changes are grouped for simulation/persistence, Unity flow, and documentation/evidence commits.
