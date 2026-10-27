# Repository consolidation verification

Verified 27 October 2026, starting from `1fb0e8f`. The supported game is Expanding Economy v3, circulation1, pooled storage2 and itinerary1, with Grand Commission or explicit Sandbox. No economy rebalance is part of this cleanup. Only current schema5 saves remain supported; see the [pre-release save policy](SAVES.md).

## Removal and retention audit

| Area | Removed or simplified | Retained and why |
| --- | --- | --- |
| Simulation | World/Trading/Maritime mutable owners, old runners and step results | One atomic RegionalWorldState and shared exact calculations |
| Scenarios | Classic/seeded-v1/LivingCities-v2 player modes, old Archipelago/Enterprise/LivingCity factories, fixed challenges | Current charter endowments directly in ExpandingEconomyScenario; unchanged IDs in WorldIds; Grand Commission/Sandbox |
| Generation | Classic authored geography and retired scenario wrappers | Pure SeededArchipelago geography and LivingCityGeneration role draws, because v3 composes them |
| Automation | Old player market/industrial editors and redundant Logistics screen | Player itinerary planner; NPC market evaluator/detached maritime projection; industrial policies for controlled economic research |
| Saves | Schemas1–4, obsolete optional defaults and legacy facility-store restoration | Strict schema5 identity/account/FIFO/physical/history/cursor validation before host replacement |
| Unity | Bootstrap, SampleScene, TradingLab, smoke host, prototype build commands, Welcome template | ArchipelagoLab as sole build scene; shared panel/theme/text helpers relocated with GUIDs intact |
| Tests | Retired-owner/mode/scenario/schema-only suites and obsolete UI paths | Atomicity, exact pricing, conservation, payroll/FIFO, pools, fleet/itineraries, generation, objectives, current saves and host journeys |
| Reports/docs | Retired runners, seven obsolete report JSONs, old task/evidence documents and330 screenshots | Current v3/circulation/reinvestment evidence, sanitized day140 diagnosis, current guides/help, eight curated captures and useful art references |

PackageManager removed nine unused authoring packages: 2D Animation, Aseprite, PSD Importer, Sprite Shape, Tilemap Editor, Tilemap Extras, 2D Tooling, Timeline and Visual Scripting. Manifest and lock agree. Compilation, both Unity suites and the Windows build/player verified their absence. URP, Input System, Newtonsoft.Json, Test Framework, Pipeline, sprite support, engine modules and existing user Editor/IDE tooling remain. Modules were not removed merely because current scripts do not mention them.

Existing user-generated runtime artwork and `art/backup/generated-maritime/` remain intact. The [asset inventory](design/task-2.5/PRODUCTION-ASSETS.md) now accurately describes PNGs and fallback geometry. Unrelated recovery files, package README and local learn-framework settings were excluded from cleanup commits.

## Executed checks

| Check | Actual result |
| --- | --- |
| Locked restore / Release solution build | Passed using cached packages with network vulnerability audit disabled; zero build warnings/errors. Portable simulation remains netstandard2.1/C#8; demo/tests use .NET10 |
| Full headless suite | **715 passed**, zero failed/skipped,6m10s |
| Final Unity Edit Mode | **63 passed**, zero failed/skipped,262.82s |
| Unity Play Mode | **62 passed**, zero failed/skipped,135.64s |
| Final Python suite | **58 passed**,8.136s |
| Content/reference | Six base goods/buildings/recipes plus validated v3 extension;58 generated reference files checked; canonical content/IDs unchanged |
| Unity staging | DLL/content/format2 encyclopedia freshness passed |
| Grand Commission check | Two manual wins, itinerary/save-continuation win and idle deadline failure passed |
| Expanding economy check |29 comparisons/5,220 primary days; current-rule report regenerated and independently byte checked |
| Optional reinvestment check |12 comparisons/4,380 days; regenerated and independently byte checked |
| Circulation check |64 modern treatments+16 diagnostic controls/29,200 days; independently byte checked |
| Windows application | Unity6000.6.6f1 Windows64 Mono development build succeeded; fresh process rendered, continued an exact Editor save and won |

An initial circulation report mismatch was confined to corrupted arrow labels in account-transfer keys. Normalizing those old labels made the complete parsed reports equal. Correct UTF-8 labels were regenerated and independently checked; economic values/events did not change.

CI validates the current solution, content/help/docs, staging, commissions and bounded expanding report, with a separate circulation job. Retired report commands are gone; deep sweeps and the full reinvestment report remain opt-in. Local equivalents were executed; a remote Actions run was not triggered. Pages workflow/settings were neither changed nor published.

## Deterministic behavior and actual gameplay

Ordinary current-rule seed99 Merchant and seed42 Independent wins were captured before removal. Replaying after consolidation produced **identical complete baseline JSON bytes**, SHA256 `07F00894317DB530A6010F9C0771E2B913220EA8A379C1BA215BEF84E3989A1C`. Seed99 still wins on day18 with622.6936 coins/score2162; seed42 on day13 with285.8935/score2128. No generation, pricing or accounting formulas were retuned.

The actual Editor journey used visible UI Toolkit controls to preview seed99, draft All Harbors/A New Industry/Flour for the Bakers/Royal Institution, pay for Northport Mill/Westhaven Bakery/Eastport Silver Mine and real inputs, commission the public Mint, and advance funded production. It assigned and enabled a Bread Buy/Sell itinerary, executed the purchase/departure, and saved/loaded on day1 **byte-identically**, preserving two travel days, cargo and cursor. The route was then paused during travel, its Bread sold manually on arrival, and the commission continued. The headless itinerary witness separately verifies the automatic destination sale.

The ship physically loaded Silver, bought/delivered Furniture to the finite contract, surrendered Silver at the active Mint, bought four Flour and sailed to Southhaven. The final Flour sale committed victory immediately: **day18/revision45, four goals,622.6936 coins**. A fresh Windows process used Continue to restore the Editor day18 handoff byte-for-byte, then performed the same winning sale. Extra UI policy controls explain the revision difference from the headless route; economics agree.

No grants, edited saves or direct economic mutation APIs were used for these live journeys. The host save API supplied read-only comparison checkpoints. Both runtimes exercised Map, Harbor, Industry, Fleet, Economy and Objectives. At **1366×768 and1920×1080, Default and Large**, rendered captures showed actual bound values, readable parchment and commodity icons. Navigation/scaling/inspection left serialized economics byte-identical. Industry and Journal retain intentional internal scrolling.

Windows also saved and loaded the terminal result byte-identically. The test player exited normally with code0. All three original user slots were restored and matched their pre-test SHA256 hashes; temporary Pipeline build settings, incidental project settings and Editor background behavior were restored.

## Curated running-game captures

These are unedited Unity captures, not moodboards or reconstructed UI. The existing development-console overlay was closed for player captures; its messages are disclosed below.

| Capture | Context |
| --- | --- |
| [Map-first preview](images/current/editor-map-preview-1366.png) | Editor1366×768 Default, before drafting |
| [Map and command deck](images/current/editor-map-1366-large.png) | Editor1366×768 Large, day18 before final sale |
| [Harbor](images/current/editor-harbor-1920.png) | Editor1920×1080 Default, actual cargo/public stock and fee/net proposal |
| [Industry](images/current/editor-industry-1920.png) | Editor1920×1080 Default, constructed Mill/local stock |
| [Assigned itinerary](images/current/editor-itinerary-1366.png) | Editor1366×768 Default, enabled voyage and destination order |
| [Windows map](images/current/player-map-1920-large.png) | Windows1920×1080 Large, frozen completed commission |
| [Windows Journal](images/current/player-objectives-1920.png) | Windows1920×1080 Default, authoritative goals |
| [Windows victory](images/current/player-victory-1920.png) | Windows1920×1080 Default, four goals/exact cash |

## Measured economy, retained dependencies and limitations

Production remains distinct from sustainable welfare. At seed42/day180, industry and two-ship-network policies end with7.7464 and1.0854 coins and recovery food fulfillment1% and4%; the circulation policy reaches25% with950.4168 coins. Year-long current-rule reinvestment at seeds42/99 produces104 Bread but ends with zero paid-food fulfillment at processing towns and declining population; seed−42 retains58%. Tests and analysis record those counterexamples instead of preserving stale universal-recovery claims. The60day [Bread-loop witness](AUTOMATION.md#bounded-ordinary-world-witness) still completes four loops/seven funded batches, with exact wages, FIFO and commercial fees.

- Shared maritime merchant projections/evaluators, industrial research policies, pure v1/v2 generation and small unscaled/fixed-price/separate-store invariant fixtures remain intentionally. They are not selectable historical modes; removing them would rewrite current algorithms or discard useful isolated invariants.
- A few fixed-challenge fields and numeric enum identities remain in schema5's shape to preserve current fingerprints. Retired challenge behavior is gone. Economy0/treatment worlds remain unsaveable diagnostic controls.
- Interaction used UI Toolkit pointer, keyboard and navigation events through Pipeline. Native physical input and operating-system focus behavior were not tested. Compact/Extra Large retain existing UI-test coverage; no new exhaustive manual matrix is claimed.
- Windows retains pre-existing optional AMD/NV plugin architecture messages, a D3D12 debug-interface message and stripped unused URP postprocessing-shader warnings. The pre-cleanup asset-task player log contains the same messages. No Salt & Ledger managed exception was observed; rendering and operations succeeded. These remain platform cleanup debt.
- One Editor save-confirmation status request timed out. Inspection confirmed the save had completed; loading resumed from the still-open save screen. The subsequent hidden-map click was rejected without an economic operation.
- No Web build, other platform, native-input endurance test or new deep balance sweep was performed. No future gameplay framework or art overhaul was added.

## Completion criteria

| Requested finish line | Evidence |
| --- | --- |
|1. Build simulation and Unity application | Release solution and actual Windows64 Mono build succeeded |
|2. Run remaining relevant tests |715 headless,63 Edit,62 Play and58 Python passed; current report checks passed |
|3. Complete modern trading/production/itinerary/save/objectives run | Full Editor journey, headless automatic-route win, exact fresh Windows Continue and matching victory |
|4. Verify content, documentation and CI | Content/reference/staging checks, strict documentation build and current CI commands executed locally; remote execution not claimed |
|5. Explain removals and retention | Audit above; README, architecture, guides, roadmap, AGENTS and shared help updated |
|6. Identify remaining legacy code | Explicit shared NPC/research/generation/fixture/schema-shape dependencies above |
