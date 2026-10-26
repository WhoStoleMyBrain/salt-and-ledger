# Task 2.5 implementation progress

Authoritative scope: `TASK-2.5.md`, including revised §2.5 and all 21 acceptance criteria. Baseline confirmed: `3feb6b5c1fa88fbc3c176ffdc89f5de9a02aaf92`. This is a presentation milestone; economic operations, content, generation, save schemas 1–5 and historical reports remain unchanged.

## Shared contracts and integrated plan

- **Lead integrator:** main agent owns the shell, navigation, integration, documentation, CI, actual Editor/player verification and final acceptance. Other workstreams have explicit nonoverlapping files below. Only the lead drives Unity.
- **Selection:** `ArchipelagoLabSession` remains the owner of selected town/ship and selected owned command ship. A small `PresentationContext` tracks only active mode, whether a deliberate selection exists, deck collapse and return destination. It never stores economic quantities. New runs start with a collapsed deck even though the session has a valid default port. Selection updates always use the session; NPC selection never replaces the owned command ship.
- **Navigation:** Map/Fleet/Economy are composed workspaces over the same gameplay shell; Trade and Industry enter existing overlays; Objectives enters the existing Journal. Existing detailed City Economy/Ledger/Company/Route Planner screens remain the execution and detail destinations. Preserve selected ship/town and parent mode across Back/Escape/modal close. Management navigation pauses playback and never automatically resumes it; explicit Play/Next day remains authoritative. Confirmation and overlay cancellation retain priority. Keep old controls reachable through details rather than deleting working capabilities.
- **Signals:** new pure `EconomicSignals` in the portable simulation assembly returns immutable structured next-essential-need and production-opportunity facts over an existing committed snapshot and content. Reuse `PreviewDemand`, `PreviewConsumption`, `PrivateProductionRules`/existing production previews and the itinerary evaluator. Do not create worlds, simulate days, reserve goods, mutate residuals or introduce another formula. Separate current material/payroll readiness from due date, last outcome, and conditional future opportunity. Public and private stores stay distinct; pooled competition is explicit. Historical absent household systems return Not modeled. No new snapshot fields or serialization changes.
- **Map:** one UI-only `MapViewport` transform fits actual world bounds with marker-aware clamped padding and inverse. All ports, route lines/captions, interpolated ships and setup preview use it through `ArchipelagoMap`. UI Toolkit buttons own picking. Default is fit-all; no zoom/pan/minimap controls. Distances are contextual/explicit-overlay; selected ship itinerary shows only its real ordered legs. No economic distance/geography changes.
- **Visual kit:** shared `sl-` tokens/components, deep sea `#103B45`, navy `#0D2631`, teal `#2F6C73`, paper `#E9DFC6`/`#F4ECD8`, ink `#26343B`, gold `#C4A36A`, sea green `#396C60`, rust `#A45139`, border `#B7AA8F`; adjust only for measured contrast. Body remains existing readable runtime font and inherited 14/17/21/25px text scales. Titles use hierarchy rather than an unlicensed font. `MaritimeGlyph` is a reusable authored icon element (good IDs plus ship/industry/market/cash/attention/info/warehouse) with small/normal/large classes and textual labels/tooltips. Original project-authored vector/Painter2D sources are reproducible and avoid an additional package/font. A representative illustrated tile and procedural shore/landmark details are required; concept-sheet pixels never ship as game art.
- **Precision:** HUD shows at most one decimal coin with exact tooltip; nonzero values below 0.1 use `<0.1`. Detailed Harbor and Ledger retain existing exact four-decimal amounts. Signals keep exact integer subunits; formatting never feeds commands.
- **Refresh:** snapshot/selection-driven updates, stable visual element identities, no rebuilding whole views per frame. Only map ship interpolation animates frame-to-frame. Preserve focus and scroll where reasonable.

## Implementation sequence and ownership

1. Read task/reference/source contracts; establish this document. **Complete initial contract review.** References are present with downloaded suffixes `(1)`/`(2)`; preserve originals and document actual links.
2. Independent signals workstream: `EconomicSignals.cs`, focused .NET signal tests; additive read-only APIs only, shared rule extraction only after lead review.
3. Independent map workstream: `MapViewport.cs`, `ArchipelagoMap.cs`, focused Unity Edit tests. Coordinate glyph integration through the contract above; no other controller edits.
4. Independent visual-kit workstream: new `IllustratedMaritime.uss`, `MaritimeGlyph.cs`, representative illustration/component files and source/provenance documentation; no shell/controller/UXML edits. Prepare kit while shell is built; apply polish only after functional integration.
5. Lead: compact HUD/navigation/deck, Fleet/Economy views, overlay return/selection, apply shared kit and genuine recipe/goods bindings. Integrate each subsystem before full journeys.
6. Lead: targeted Unity tests and real ordinary-command journeys A–D; actual Editor and Windows Mono at 1366×768 and 1920×1080 Default/Large, supplementary Compact/XL; save bytes and inspection fingerprints.
7. Lead: player guide, design system, roadmap and generated help; bounded economic-circulation CI check; full CI-equivalent tests and unchanged historical report checks. Reread full task and audit all 21 DoD items in `docs/TASK-2.5-VERIFICATION.md` before declaring completion.

## Completed and remaining

- Read full Task 2.5 and both normative reference documents; inspected all five supplied concept images. Actual HEAD matches baseline.
- Reviewed existing session/snapshot, modal, screen, city, map, production/retail and itinerary ownership. Existing Harbor and planner will be reused.
- All 21 requirements have now been implemented and audited against source, tests and actual Editor/Windows player evidence in `docs/TASK-2.5-VERIFICATION.md`. Native OS device injection remains unavailable; actual Toolkit key/pointer/navigation events and rendered players were exercised. No economic/save behavior was changed.

## Existing local changes and preservation

Before implementation: user-supplied untracked `README-PACKAGE.md` and `docs/design/`; modified tutorial-framework settings; untracked Unity `_Recovery` scene and meta. Preserve these unrelated Editor artifacts. Private user save slots must be backed up afresh before actual save testing; never overwrite them with an older task's backup.

## Tests actually executed

- Pure `EconomicSignalsTests`: **26/26 passed** on final projection code, including numeric-limit unavailability, exact residuals, public/private actual-batch equivalence, pooled contention and unchanged saved bytes.
- Existing circulation/living-economy/pooled-storage focused regressions: **52/52 passed** after mechanical public readiness extraction.
- Portable Release build and Unity staging passed, zero warnings/errors. First staging attempt could not contact NuGet inside the sandbox; the approved normal-access rerun restored the locked dependency and succeeded.
- Unity CLI 1.0.0-beta.14 confirmed current. Actual Editor 6000.6.6f1 is available and was stopped before verification. First integrated recompile requested; no Editor/rendering/test pass claimed yet.

## Milestone 1: working source integration

- Added pure `EconomicSignals` with next essential opportunity and canonical batch/current-store facts. Numeric receiver overflow is explicit unavailable affordability/payment, not a zero-price or poverty result. `RegionalCirculation` delegates its unchanged public readiness logic to one shared helper; no economic formula or persisted state was added.
- Added `PresentationContext`, compact mode shell/deck, real Fleet and town Economy workspaces, chooser when Trade/Industry has no selection, actionable notice destinations, and exact existing Harbor/Industry/Journal/planner adapters. Moved the old detailed inspector into a deliberate Details/log destination; kept its controls.
- Added shared `MapViewport`, contextual routes and selected itinerary overlays with procedural chart/coast/landmark treatment. New map Edit tests are written; not yet executed.
- Original `MaritimeGlyph` family and `MaritimeIllustration` source-native vector kit now exist, with shared `IllustratedMaritime.uss`. Integrated symbols into Harbor, navigation and recipes and representative Harbor/Industry/Fleet vignettes. Visual polish remains unverified until actual captures.
- Files modified so far include the new command deck/workspaces/kit partials and USS; existing controller, run experience, city, commission, trade/industry/Harbor/development adapters; map; projection tests. Agents are adding focused scene tests, provenance and current guide/help/CI maintenance.
- Explicit navigation decision: closing Journal now leaves playback paused, matching the required management-view contract. No route enablement changes occur on inspection.

Next: compile integrated Unity host, fix binding/layout issues, exercise the functioning shell before refining contrast and spacing. Then run focused Edit/Play tests and genuine input journeys before full regression gates.

## Known issues / next concrete steps

- No implementation blocker remains. Native Windows computer-use service failed with missing pipe/OS error 2; physical-device feel is explicitly unverified. Human playtesting of modal focus, route return and Large/XL density is the next concrete step.
- Exact production readiness, shared-stock competition and inspection purity passed focused and full tests. The new UI does not promise future execution or price.
- Original reference filename suffixes are preserved and documentation links resolved. Final screenshots are genuine runtime captures, separate from the moodboards.

## Milestone 2: real Editor rendering and focused map verification

- Actual Editor 6000.6.6f1 rendered menu, seed-99 sandbox Map, selected town/deck, Harbor, Economy and Fleet at 1366×768 Default. First-pass captures are private ignored `.cache/task25-editor-first-*.png`; they are provisional, not final visual acceptance.
- Live review found misleading role labels (matching words in the explanatory paragraph) and a compatibility route shown for an unassigned modern ship. Both fixed; shared role helper and explicit modern route filtering have regression cases. Calm readiness strips now use green; rust requires an actual warning. HUD cash/value has a visible separator, and notice details scroll internally.
- Integrated compile initially caught a duplicate helper; removed the duplicate. A malformed test GUID initially caused zero discovery; corrected it rather than accepting an empty pass. Actual focused `MapViewportTests`: **15/15 passed**, 5.73 seconds.
- Full Python suite: **58/58 passed**, 15.423 seconds. Generated 59 reference files and freshness check passed. Initial strict MkDocs failed on the not-yet-written UI design-system page; final strict build remains pending that document.
- Player guide/help/roadmap and separate bounded circulation CI job updated; 30 help IDs retained. Generated reference and offline encyclopedia regenerated from source help.
- Full headless gates are running independently. No player/save-continuation claim yet; new shell Play tests are now running.

## Milestone 3: genuine economic journey and regression gates

- First new shell Play suite: **8/8 passed** in 18.07 seconds, including paid Harbor procurement, actual pooled Mill/Bakery batches, second ship, NPC restrictions, itinerary wait reason, legacy modes, Journal and exact schema-5 reload. Subsequent review added two focus/notice tests and a planner–Harbor return case; final full suite pending.
- Actual Editor seed-99 Merchant/StockOnly/A0 Grand Commission: drafted All Harbors / A New Industry / Flour for Bakers / Royal Institution. Constructed and funded Northport Mill+Bakery, Westhaven Bakery and Eastport Silver Mine; real Bread trade, Silver cargo transfer, Furniture external contract, Mint commission/exchange and Flour delivery produced **victory on day 22, revision 43, cash 258.3003 coins**. No economy API/state setters used for this journey: visible UI Toolkit navigation/field/pointer events only.
- Mid-voyage day-1/revision-11 schema-5 Save/Load and full read-only inspection matrix retained identical bytes: SHA-256 `29cff5c4190b07fe07277a70a5ad6a4e610f56bb3fe0ff6f8a04554c64ed8a45`. All three user slots were freshly backed up before writes; restoration remains required at cleanup.
- Actual Editor Map/deck, Fleet, Economy and Harbor captures made at 1366×768 and 1920×1080, Default and Large. Precise Harbor tables/order sections use internal scrolling at Large. Coast clipping spotted in short viewports and fixed by presentation-only scale clamps; final-source captures pending. Early-voyage markers can approach town labels at Large; collapse/Fleet gives more room, without falsifying voyages.
- Review fixes: modal focus resolves recreated Economy cards or a stable mode fallback; Notices Escape closes only its list; Planner→Harbor→close returns to the same draft/ship. Industry recipe symbols moved beside readiness rather than hidden in accounting details.
- Full standalone gates **passed**: locked restore (approved network rerun), Release solution build 0 warnings/errors, **1,066/1,066 .NET tests**, five demos, all **11 report freshness commands**. SHA-256 comparison confirms all eleven experiment JSON files unchanged. Circulation check exercised 80 worlds / 29,200 days with all monetary/physical/FIFO invariants.
- Strict MkDocs and Unity staging freshness now pass. Python **58/58** and 59 generated reference files remain current. New `UI_DESIGN_SYSTEM.md` and the 21-item verification draft exist; final acceptance pending actual Windows player/full Unity suites.

Next: complete full Unity Edit/Play suites, build Windows Mono with final source, run live continuation/itinerary and native input, capture final four-size/scale matrix, restore user slots/editor settings, finalize every acceptance row and documentation freshness.

## Milestone 4: final interaction review and compatibility

- Full Edit suite first passed **113/113**. Full Play suite first reported **79/84**, with five old UI expectations (removed dock counts, initially expanded deck, direct detailed City Economy entry, automatic Journal playback resume). Updated those tests to follow the new required navigation while retaining actual-value and all-scale reachability assertions. All ten new command-deck tests passed in that run.
- A final source map review found early-voyage marker/label overlap at 1366×768 Large. Added bounded town-label displacement and leader lines; real port/ship coordinates, islands and voyage interpolation are unchanged. New seed-99/day-1 collision test raises the Edit total to 114. Final full suites are running against the current imported assemblies; an intermediate status-poll HTTP 400 was recovered and is not treated as a completed new-code pass.
- Loaded the archived day-140 schema-2 terminal save through a backed-up slot, inspected Results/Journal, and verified exact original bytes: SHA-256 `5bb9e1b135adf2e63e7d55243e84731254f4555ff6a581fd4a68690651ab0a47`. Added a Results→Inspect frozen map return path so loaded terminal saves also expose the shell; a terminal-state regression checks disabled time controls and exact bytes.
- Post-polish Editor captures cover Map/deck, Fleet, Economy, Harbor table and precise order sections, and visible Industry recipe symbols at all four required resolution/scale combinations. Latest label adjustment still needs refreshed map captures. Inspection remained byte-identical at day 1/revision 11.
- Native Windows Computer Use attempted normal discovery, retry and fresh-session recovery; all failed with missing native pipe / OS error 2. Native OS mouse/keyboard remains **unverified**. Actual Unity UI Toolkit navigation/key/pointer events and runtime captures are supported; do not describe these as native OS input.
- Static review found no economic mutation/formula duplication; 107 meta files have valid unique GUIDs, and 520 authored name definitions have no conflicting literal names. Repeated Harbor cells/map children remain intentionally scoped.

## Milestone 5: Windows player continuation and final visual corrections

- Full current-import Unity gates passed **114/114 Edit** (73.45s) and **84/84 Play** (132.33s). An earlier 82/84 Play run exposed Default Harbor's Sell action 22px below its initial viewport; compacting the illustration/card spacing fixed that without reducing font size. These counts precede the final two visual regressions below and will be superseded by the final gate.
- Windows Mono development build succeeded. Actual player process 1 continued the exact day-1 schema-5 bytes, rendered all four required resolution/scale combinations, then used real Harbor sale and Route Planner controls. Applied/enabled BreadLoop, advanced to day 4/revision 17, loaded two actual privately produced Bread and departed Westhaven for Southhaven. Cash was **573.9201 coins**. Its route save/load preserved exact bytes, cargo, cursor and daily budget. The player exited normally with code 0; a fresh-process continuation against the final build remains next.
- The first hidden Windows launch yielded black captures; a display-mode reset restored rendering and all 24 matrix captures were replaced. The log contains the existing optional AMD/NVIDIA native-plugin architecture warnings. Do not describe those as economic/application exceptions or hide their existence.
- Final visual review found ship markers P1/N3 overlapping at 1366 Large and the extra v3 fee preview pushing Sell below the initial Default Harbor viewport. Added bounded ship-callout displacement/leader lines with unchanged true voyage anchors, plus a seed-99/day-1 collision regression (17 map cases). Harbor now uses horizontal labels and tighter card spacing at Default/narrow only; normal text/control sizes and Large reflow remain unchanged. A dedicated v3 initial-viewport regression is being added before the final recompile/gates.
- Archived schema-2 day-140 Results → frozen Map → Economy → Harbor → Journal inspection succeeded, with disabled trade/time commands and exact original saved bytes. The new explicit Results inspection entry is covered by a terminal-state Play regression. Actual day-22 results diagnostic export also completed without changing the terminal snapshot.
- All eleven experiment reports remain unchanged from the initial checkout. Ten match raw HEAD bytes; the pre-existing circulation diagnostic uses CRLF locally versus LF in HEAD, with identical normalized Git content and unchanged original local hash.

Next: finish the final v3 layout test and Unity gates, rebuild, continue the day-4 route in a fresh Windows process, replace final affected captures, review them, restore all original slots/build-generated settings, and finalize every acceptance row with linked evidence and the native-input limitation.

## Final gate results

- Final source imported successfully. A test-runner HTTP 400 immediately after domain reload was an infrastructure interruption; inspected the stopped/ready Editor and restarted only after confirming no test run was active.
- **115/115 Edit Mode tests passed**, 143.74s, zero skipped/inconclusive; includes 17 map projection/route/callout cases.
- **85/85 Play Mode tests passed**, 125.79s, zero skipped/inconclusive; includes 11 command-deck integration cases and the modern v3 fee/Buy/Sell initial-viewport regression with unchanged 17px text and exact saved bytes.
- Final content validation and generated-reference freshness passed; tracked diff whitespace check passed. Final-source Editor captures and Windows rebuild/continuation are now in progress.

## Final acceptance and handoff

- Reread the complete original `TASK-2.5.md` after final integration and checked every Definition-of-Done item individually. `docs/TASK-2.5-VERIFICATION.md` now records all 21 dispositions, concrete operations, exact hashes, screenshot links, provenance and unsupported checks. No requested implementation item was replaced with mock data.
- Final Windows Mono build succeeded. Three actual test player processes exited normally with code 0. A fresh final-build process continued the day-4 route byte-for-byte, then advanced twelve ordinary days to **day 16/revision 29, cash 551.0435 coins**, carrying **2 Flour + 2 Logs** one day from Westhaven. Actual itinerary sales/purchases, mixed cargo, cycle/cursor/budget and wages/fees are documented. No economic parameters changed.
- Final actual Editor and Windows captures cover 1366×768 and 1920×1080 Default/Large for Map/deck, Harbor table/orders, Fleet, Economy and Industry; Compact/XL Fleet supplements were also inspected. Independent visual review found no blocking issue in the required matrix. A separate passive player Bread preview shows **39.5 gross, 0.79 fee, 38.71 net**, with **0.395** per recipient, enabled Sell and unchanged day-4 saved bytes.
- **66 genuine runtime PNGs** and a dimension/producer/SHA-256 manifest are in `docs/images/task25/` (about 17.2 MB of documentation evidence, never runtime textures). Concept images remain separate. Exact final inspection hashes match the recorded day-1 Editor and day-4 player saves; the old schema-2 terminal fixture remains exact.
- Final content validation, 59-file generated-help freshness, strict MkDocs and Unity staging freshness passed. Full .NET **1066/1066**, Python **58/58**, Unity Edit **115/115**, Unity Play **85/85**, five demos and eleven report checks all passed. Historical report bytes remain unchanged from the initial checkout; no experiment regeneration or Pages publication occurred.
- All three original user save slots were restored and their hashes verified. Removed only verified empty build-generated Pipeline folders; restored the four originally clean build/settings assets, including Unity services configuration. Preserved the user's pre-existing tutorial settings, recovery scene and supplied references. Editor is ready/stopped; its run-in-background preference was restored false. Temporary runtime instrumentation setting is false again.
- Remaining limitations: native OS mouse/keyboard service unavailable (missing pipe/OS error 2); actual Toolkit events were exercised instead and are labeled honestly. Existing optional AMD/NVIDIA plugin load and D3D12 info-queue warnings remain. Large/XL Industry/planner/Fleet detail density needs human comfort review; the required Default/Large controls are reachable. Source-native art deliberately stays simpler than the painted moodboard.

Next concrete steps are human playtesting of physical-device modal focus, the stability of route callouts during playback and Large/XL detail comfort. No Task 2.6 systems, extra art production pipeline, economy rebalance or save migration is started.
