# Task 2.1 verification

Baseline: `a99c59b01c59372b6a3242c68ec59f33bae342f1`. Audit date: October 24, 2026. The authoritative requirements remain `tasks/TASK-2.1.md`; this record reports evidence against its fourteen Definition of Done items.

**All fourteen criteria have implementation and verification evidence for this milestone.** The full headless/Python/Unity suites passed, the requested eight primary views were reviewed, and actual Editor and Windows Mono UI journeys reached day-18 victory. The standalone journey includes a clean process exit and exact fresh-process Continue. The disclosed hardware/backend and economic limits below remain outside these observed results.

## Definition of Done audit

| # | Authoritative criterion | Status and concrete evidence |
| --- | --- | --- |
| 1 | The v3 Grand Commission is the primary/default new scored v3 path without removing the three old authored challenges or sandbox. Full starting archipelago and important town capabilities are revealed **before** the objective draft; preview agrees with real run. | **Verified.** `RunScenario.GetChallenges(v3)` prepends Grand Commission and retains the three original definitions and sandbox. Tests compare exact initial ports, accounts, ships and public economy across selected plans. Actual Editor/player setup reveals the seeded map and capabilities before drafting. |
| 2 | Exactly four seeded, category-constrained rounds offer three distinct valid choices each, with a genuine pool of at least four per round; choosing/backtracking does not advance the world or change offers. | **Verified.** Draft tests check four pools of four, three distinct offers, invalid choices, golden vectors, culture independence, passive state and 67 legal seeds. Unity tests cover selection/backtracking and incomplete-draft protection. Both live journeys selected four actual offered cards without advancing day zero. |
| 3 | The chosen four objectives are frozen into an explicit, versioned, replayable identity that also distinguishes score comparison groups. | **Verified.** Immutable `GrandCommissionPlan` records seed, draft version 1, four resolved goals and canonical SHA-256 signature. `RunIdentity` extends only drafted comparison groups. Draft/save tests cover different plans, wrong seeds, unknown versions, altered parameters, exact Retry and restored identities. |
| 4 | The four categories implement actual varied goals, including **A New Industry**, real ship visits, source/target signed net sales, and public economic or welfare objectives. | **Verified in real simulation operations.** Progress, additional-template, save and witness tests exercise funded distinct-town Mill/Bakery production, all four navigation evaluators, timber/artisan/Breadworks industry, signed local supply, finite exports, physical Mint issuance, reference value, paid food and funded public works. No goal credit comes from UI counters. |
| 5 | All four selected goals **and** minimum company cash are required by the common deadline; success, failure and abandonment use real terminal rules. | **Verified.** `ChallengeRules` requires all four current goals and 100 coins by day 180. `GrandCommissionAtomicTests` performs genuine economic preparation and the final harbor arrival on day 180, winning before deadline failure; its day-179 save continues identically. Idle failure, abandonment and frozen terminal mutations are covered. |
| 6 | Correct permanent-versus-current progress semantics and honest numerical counters, checkmarks, progress bars and blocker text. | **Verified.** Typed progress preserves signed values, clamps bars and uses the limiting subcondition. Real buybacks, value expenditure and food-window decline reduce current progress while visits/output persist. Actual Journal screenshots show funded batch checkmarks, permanent visits/Mint issuance and current net-supply/facility conditions with exact numbers. |
| 7 | New needed history (visits and target sales) is bounded, authoritative and updates only on actual committed actions, with full rollback. | **Verified.** Snapshot history is bounded to three owned ships with four first-visit dates each and 28 town/good purchase/sale balances. Ship and warehouse market paths, arrivals and commissioned docks update staged state. Late revision-overflow tests preserve the exact original snapshot after an otherwise valid trade and after a staged arrival/daily phases. |
| 8 | Dedicated accessible Objectives/Journal view, compact gameplay objective summary and comprehensible end results. | **Verified.** Dedicated Journal, compact HUD, exact deadline/cash floor and four-goal results passed automated and live checks. Eight primary layout views cover both requested resolution/scales. The results footer was corrected to two rows and visually rechecked before the accepted player build. |
| 9 | Existing v3 challenges, historical modes, economic rules, reports, and valid existing saves are preserved. | **Verified.** All 845 headless tests passed. Four schema-1 save byte hashes captured from the pre-Task-2.1 Release assembly still match exactly and restore/continue. All nine published experiment JSON files are byte-identical to HEAD; hashes are recorded below. Canonical content/schema, v3 generation, original v3 challenge definitions and pricing calculators have no changes. |
| 10 | New drafted run survives exact save/load/Continue and Retry; corrupt/tampered drafts fail atomically without changing the active session. | **Verified.** Strict schema-2 tests cover progress, voyages, pending construction, fleet, tampering and exact Retry. Actual Editor day1 reload, terminal reload and Retry are byte-identical; standalone fresh-process Continue and final victory exactly match Editor checkpoints. Invalid host loads preserve the active session in Play Mode tests. |
| 11 | Tests show deterministic offers, wrong-world rejection, net reversal, no fake visits, production correctness, rolling goals and terminal freeze. | **Verified.** The final suite includes 51 Grand Commission cases across draft, progress, save, atomic, additional-template and winning-witness test files. Coverage includes impossible/future/NPC visit claims, payroll/production consistency, target-specific reversals and current welfare falling after actual delivered food runs out. |
| 12 | At least two differentiated real-command Grand Commission victories and an idle/failure control are reproduced without grants or direct state edits. | **Verified.** `--grand-commission --check` executes the two ordinary-command witnesses below and an idle day-180 defeat. The winning cases are also checked by the full test suite, including exact cash, wages, score and save continuation. |
| 13 | Full headless/Python/docs and relevant Unity checks are run where available; actual Editor/player limitations are honestly reported. | **Verified with disclosed limits.** 845 headless, 58 Python, 94 Edit and 58 Play tests passed. Three targeted Play cases passed again after the footer fix. Build, staging, content/docs freshness and strict MkDocs passed. Editor and accepted-source Windows Mono completed real UI victories; physical hardware, IL2CPP and other platforms are unverified. |
| 14 | New mechanics/flow are documented, a practical manual playtest route is recorded, and no GitHub Pages publication is performed. | **Verified.** Grand Commission mechanics/catalogue, run flow, shared offline help, architecture, roadmap, AGENTS, practical playtest route and this acceptance record are updated. Generated reference is fresh and strict docs pass. No publishing workflow was changed and GitHub Pages was not published. |

## Actual checks

| Check | Observed result |
| --- | --- |
| `dotnet test tests/SaltAndLedger.Simulation.Tests/SaltAndLedger.Simulation.Tests.csproj -c Release --no-build --no-restore` | **845 passed, 0 failed, 0 skipped**, 2m 2s; final run after the five additional-template tests were compiled. |
| Focused `GrandCommissionSaveTests` and `GrandCommissionAtomicTests` | **28/28 passed**, including old save bytes, exact continuation, resealed corruption, exceptional rollback and day-180 success precedence. |
| Focused `GrandCommissionAdditionalTemplatesTests` | **5/5 passed**, using actual voyages, trades, paid production, physical Planks delivery and rolling public-works welfare. |
| Draft/progress focused tests | **16/16 passed**, recorded in the coordinating progress log and included in the final full suite. |
| Release solution build | Passed with zero warnings/errors, recorded in the progress log. |
| Legacy production, world, market, pricing `--check`, shipping and automation demos | All six passed on the final Release build. Larger unchanged economic reports were preserved by byte/hash audit rather than rerunning long sweeps. |
| `--grand-commission --check` | Two genuine victories and one idle failure; exact mid-voyage and terminal round trips passed. |
| Python unit tests | **58/58 passed**, recorded in the coordinating progress log. |
| Content validation, generated docs/freshness, strict MkDocs, Unity staging | Passed, including final validation after the acceptance record and gallery were added. |
| Historical experiment artifacts | Nine files compared byte-for-byte with `git show HEAD:<path>` and SHA-256; all identical. No large historical economic sweep was rerun for this audit. |
| New Unity Play Mode tests | **3/3 passed**, 4.74s; final footer-fix rerun **3/3**, 3.85s. Full suites separately passed **94/94 Edit**, 13.77s, and **58/58 Play**, 67.78s. |
| Live Editor and Windows Mono player | Both completed genuine day18/revision43 victories with exact same full state. Standalone process2 used Continue after process1 saved and exited through the UI. |

## Reproducible economic outcomes

Run from the repository root:

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --grand-commission --check
```

Amounts below are displayed coins, not raw monetary subunits. `GrandCommissionWitness` uses ordinary paid construction, warehouse/ship orders, funded batches, transfers, voyages, finite exports and Silver-backed Mint operations. It neither edits state nor creates supplemental grants.

| Seed / charter | Four mandatory goals | Outcome | Cash | Paid inventory | Paid wages | Private capital | Public commission | Score |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 99 / Merchant | All Harbors; A New Industry; Flour for the Bakers; Royal Institution | Victory, day 18 | 648.6668 | 261.3331 | 24 | 880 | 400 | 2,164 |
| 42 / Independent | All Harbors; Timber Chain; Royal Exporter; Royal Institution | Victory, day 13 | 295.6668 | 302.9998 | 42 | 820 | 400 | 2,129 |
| 99 / Merchant, idle | Same four goals as the first row | Defeat, day 180; 0/4 currently met | Not tabulated | 0 | 0 | 0 | 0 | No victory award |

The separate boundary test uses seed 42 / Merchant with All Harbors, Timber Chain, Royal Exporter and Royal Institution. It pays for the industries and Mint, produces and physically transports goods, fulfills an export and issuance, then deliberately schedules the fourth harbor arrival for day 180. It wins on that arrival with the cash floor satisfied. Saving on day 179 and continuing produces the exact same frozen terminal save.

The additional Artisan Guild test actually produces four private Planks, carries them by ship to the owned Workshop and pays for two Furniture batches. Breadworks rejects two funded Bakeries in one town until a second town has an operational owned Bakery. Open the Routes loses its current sale condition on buyback while retaining its three permanent visits. Public Works retains its paid Planks accomplishment when later food shortages make its rolling welfare condition false.

## Historical report byte audit

These hashes are for the unchanged working files under `docs/experiments/`; each also matched its exact HEAD blob. This verifies artifact preservation, not a claim that every large report generator was rerun.

| File | SHA-256 |
| --- | --- |
| `archipelago-results.json` | `c74b761229c7a43d95d049b0211e6f73d3e29c0948824ef033aa16df941e32ae` |
| `enterprise-results.json` | `9bc50a42974527b02f6b2686f22fd57d03a607d50a29e8a66d5a50c333855685` |
| `expanding-economy-results.json` | `f13988d7005ea58d2842bdd5e3572e8a11cfefd42a0d1b4a17ad2fb891af0b21` |
| `expanding-reinvestment-results.json` | `f9bc58d86ecbdd953fd76853ebe14f18e47efc6178e994cc35babcce63d4d62a` |
| `industrial-logistics-results.json` | `1f75edd14231a700ffaa796bff8d61df0840b93fafeb98747623cdf1bfadf9c3` |
| `living-city-results.json` | `831d08ec72c13e6eec1fbd24b6b9b76b909f8284f4ce43d62b77c1f7f733fef0` |
| `pricing-results.json` | `47a3242f76c0f8e69b689d0f9acc87be09620204a12b7c394088dac1a8a31429` |
| `run-results.json` | `df4eab4c605d5ff3133d6c4d8e8cf22fd38911a81cfbd07745881bc8c457ed9e` |
| `seeded-run-results.json` | `5a44d60f3877613d316e01aec85c9f570b39de417d4e63cf43ec9eae1998d506` |

Four additional golden hashes in `GrandCommissionSaveTests` were captured from the pre-Task-2.1 Release assembly after eleven ordinary automated days in Classic, seeded-v1, Living Cities-v2 and Expanding Economy-v3. The new codec produces the identical schema-1 bytes for those states and continues their restored worlds identically. Grand Commission alone selects schema 2.

## Actual Editor and Windows Mono journeys

Unity 6000.6.6f1 ran the actual ArchipelagoLab scene. Official Unity CLI scripts sent frame-spaced UI Toolkit keyboard/pointer events to visible controls. Economic operations came exclusively through gameplay controls; reflection read snapshots/save bytes and adjusted presentation/capture only. Frames were observed advancing. This is automated live input, not physical mouse/keyboard hardware certification.

Both journeys drafted seed99, Merchant Company, StockOnly pricing, Ascension0: All Harbors / A New Industry / Flour for the Bakers / Royal Institution. Ordinary paid construction and warehouse inputs produced funded Mill/Bakery batches in Northport/Westhaven on day2. Actual voyages visited all four ports. Silver was mined, transferred aboard at Eastport and surrendered at the active public Mint in Northport; finite Furniture export funded part of the enterprise. The last four-Flour sale at Southhaven triggered **day18/revision43 victory, cash648.6668, score2164**.

The Editor saved and loaded day1 mid-voyage with two days remaining; full serialized bytes matched. Terminal save/reload matched the frozen victory, and Retry matched the exact original day0 bytes. Slot1 was empty before verification; existing slots2/3 were preserved. The visible Windows x64 Mono development player saved the same day1 state and exited through the game's confirmation. A fresh process used **Continue saved voyage** and restored those exact bytes, then reached the same full victory snapshot as the Editor. Checkpoint comparisons below include identity, all economic state, fractional residuals, FIFO and commission history rather than only displayed cash.

The accepted-source build succeeded through the Editor build helper. Its simulation DLL matches the staged DLL exactly (SHA-256 `9f2f7bb5796fb386f2df9f118ea46030f0c5a79dc2e7b8fc11e26a19a8f2b403`). Its optional runtime Pipeline QA setting was enabled for this development build and restored afterward. Build-generated settings/empty Pipeline directories were reverted; existing user Learn/Recovery edits remain untouched. The player was reviewed at 1366×768 Default; the two-resolution/scale gallery is Editor evidence. Recorded optional AMDUnityPlugin/NVUnityPlugin load diagnostics remain; the development-console overlay was dismissed after recording them to inspect gameplay. No managed gameplay exception appeared. Both process shutdowns also reported Unity graphics/ComputeBuffer cleanup diagnostics; these are disclosed rather than claimed fixed.

Player terminal reload and Retry also matched the Editor's terminal/day0 bytes exactly. The second process then exited through the game's confirmed Exit control with recorded code0. The first process's game-exit marker and disappearance were observed; its numerical exit code was not retained. The terminal verification save remains in slot1, with pre-existing slots2/3 intact.

### Reviewed primary gallery

| View | Default, 1366×768 | Large, 1920×1080 |
| --- | --- | --- |
| Map reveal | [Actual map](images/grand-commission/default-map.png) | [Actual map](images/grand-commission/large-map.png) |
| Draft | [Actual draft](images/grand-commission/default-draft.png) | [Actual draft](images/grand-commission/large-draft.png) |
| Journal | [Actual Journal](images/grand-commission/default-journal.png) | [Actual Journal](images/grand-commission/large-journal.png) |
| Compact HUD | [Actual HUD](images/grand-commission/default-hud.png) | [Actual HUD](images/grand-commission/large-hud.png) |

Navigation remains fixed; long Journal cards/details scroll internally. Completed views: [corrected Editor results](images/grand-commission/victory-results.png), [player results](images/grand-commission/player-victory-results.png), [player final market/Mint conditions](images/grand-commission/player-victory-journal-lower.png), [player score](images/grand-commission/player-victory-score.png).

### Exact Editor/player checkpoint comparisons

| Checkpoint | Day / revision | SHA-256 of identical full save bytes |
| --- | --- | --- |
| day0 | 0 / 0 | `89d6d2d638596f161db2cd07b3e36b08ccb797ea11b279abd82cdeb33ac2b247` |
| mid-voyage-before-save | 1 / 10 | `8a068384496e709613882b5f5b29689bd309670c904c817b2b520c7f6ab02161` |
| mid-voyage-after-load | 1 / 10 | `8a068384496e709613882b5f5b29689bd309670c904c817b2b520c7f6ab02161` |
| production | 2 / 15 | `d26319d33fbd08cc9e38570a4a739cae69e299e368a8efd9cbb5c2c5e96d5a5a` |
| day4 | 4 / 21 | `20d0c0c1632fec52b58cc47a3096ad733a28d5b258e8c8a2300a8f8401ca7411` |
| day12 | 12 / 32 | `80edf14aa40cf381d5edda3397ace2003effe7ab2f2e927c4df436eaa1999308` |
| day18-before-sale | 18 / 42 | `d2bd0a5b3dffb1bfb63cdb98d911432eb4760af618c2f33620f0ebc7631e6fe4` |
| victory | 18 / 43 | `47cf4fbea9d4bf6cea228291a64297e778d188cca0f548b847c0cf33d97a2110` |

Local `.cache/` interaction traces, build/test results and full checkpoints remain ignored; reproducible tests/witnesses and the compact reviewed gallery are committed review artifacts.

## Limits of the evidence

- The save schema validates known plans, exact fingerprints, bounded legal history, initial/acquired docks, plausible travel timing, current location, company/local-market/public-flow reconciliation, funded production and terminal predicates. First-visit records are deliberately bounded; they cannot authenticate every historical voyage against a coordinated rewrite of otherwise plausible saved state. Local fingerprints detect damage and incompatibility, not authorship or cryptographic trust.
- The successful commissions demonstrate feasible strategies for their exact choices, seeds, charters, pricing and difficulty. They do not prove all combinations winnable, human usability, or permanent economic sustainability. No economic parameters were retuned and the known v3 long-horizon circulation limitations remain outside this milestone.
- The new full suite includes 51 Grand Commission cases. Compact/Extra Large rely on inherited scaling and existing coverage; the focused live gallery covers the two combinations explicitly required for this milestone. Physical hardware input, IL2CPP, non-Windows players and every possible draft combination remain unverified.
