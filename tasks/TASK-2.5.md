# TASK 2.5 — Map-First Command Deck, Economic Intelligence & Illustrated Maritime UI

**Project:** Salt & Ledger  
**Milestone type:** Substantial but bounded Unity UI/UX **and reusable art-direction system** vertical slice  
**Authoritative scope:** Presentation, UI navigation, read-only projections, map-view architecture, a cohesive reusable illustration/icon/component language, and verification. **No economic rebalance or new roguelite mechanic.**  
**Reviewed baseline:** `3feb6b5c1fa88fbc3c176ffdc89f5de9a02aaf92` on `WhoStoleMyBrain/salt-and-ledger` (October 25, 2026). Inspect the actual checkout HEAD and adapt to intervening changes.  
**Design inputs:** `docs/design/task-2.5/ART-DIRECTION.md` (authoritative visual system), `docs/design/task-2.5/UI-REFERENCE.md` (functional wireframes and reference hierarchy), all accompanying PNG/HTML mockups; genuine screenshots committed under `docs/images/harbor/`, `docs/images/task23/`, and `docs/images/task24/`.

## 0. Mission and outcome

Make Salt & Ledger's *primary play experience* feel like a trading strategy game rather than a simulation inspector. Replace the permanent text-heavy city sidebar with a **map-first command surface**, a **small contextual bottom deck**, and **purposeful management modes**. Preserve full access to existing economic controls and precise information, but show it at the moment the player needs it.

A player should be able to look at the map, spot a relevant problem, select the town or ship, understand the immediate cause, open the right action, and return to the same context **without rereading the economic model or walking through unrelated panels**.

Target the agreed hybrid of earlier **Concept B (bottom command deck)** and **Concept C (dedicated work modes)**. At the same time, move consciously toward the **Illustrated Maritime Ledger** aesthetic: richly colored blue-green chart/ocean, restrained gold detailing, **warm parchment management surfaces**, legible ink-colored text, recognizable illustrated commodity/ship/industry symbols, and coherent maritime framing. This is now a **required visual direction**, not optional inspiration. See `ART-DIRECTION.md` for concrete tokens, production scope, and priorities.

**Reference hierarchy when images disagree:** authoritative economic interaction and real data > accessibility/legibility > functional B+C wireframes (`01`–`03`) for layout > `Art-Direction-Concept-Sheet.png` and `04-Visual-Components-Reference.png` for *visual language*. AI moodboards depict fanciful towns, interfaces, quantities, fonts and mechanics; none are source-of-truth data, game screenshots, licensable sprite atlases, or literal pixel templates. Build actual reusable assets/components in the project; do **not** paste the moodboard over gameplay or assume it contains usable individual icons.

Implement in Unity UI Toolkit using existing authoritative operations, controls and generated reference data. Retain readable text sizes and genuine usability at 1366×768; do not shrink numerical text to fit extra ornamentation. The outcome should look visibly more intentional and aesthetically coherent than today's technical HUD while remaining fast to understand.

**Design maxims**:

1. *The world determines the circumstances. The player chooses how to respond.* Preserve visible seeded geography and economic capabilities.
2. *Complexity comes from economic decisions, not operating the UI.*
3. *Show what matters now; expose detail when relevant.*
4. *A single current fact must have a single authoritative calculation.* Never duplicate domain formulas in the host.
5. *Important problems are discoverable, but information density should follow the task.* A trading table can be dense; an ordinary map need not be.
6. *Presentation doesn't mutate economic state.* Selection, previews, hover, scrolling, mode switches and forecast calculations must be pure.
7. *Illustration should encode identity or meaning before serving as decoration.* Richer map; calmer numbers/tables. Artistic details may not obscure selection, warnings, prices or input fields.
8. *One design system, not individual decorated screens.* Shared panel, icon, typography, button, alert and spacing styles should be reusable by future captain/charter/curse screens.

### Explicit exclusions (protect scope and Codex budget)

Do **not** implement new captains/waifus, charter draft, blessings, curse tiers, objectives, commodities, town ownership, unlimited fleets, economic tuning, taxes/fees, larger world generation, a minimap, free pan/zoom interactions, route optimizer, advanced long-horizon predictions, historical data-retention refactor, browser build, multiplayer or new save schema. Do not rewrite the C# economic simulation, its pricing or the working Harbor/Cargo Itinerary engine. In particular, do not redesign all Harbor trading interactions and the 24-action route editor within this task. Keep them genuinely functional and reachable with consistent navigation. Do not spend hours generating a full art-production pipeline or dozens of bespoke town paintings before the core input/navigation works. **This task DOES require a bounded, cohesive visual kit**: shared themed surfaces, type hierarchy, interaction states, seven recognizable commodity symbols and a small set of maritime/production/status symbols, visibly applied to the playable vertical slice. A reusable elegant approximation is preferable to unfinished photorealistic art.

## 1. First inspect and establish UI contracts

Read `AGENTS.md`, `README.md`, `docs/GAME_DESIGN.md`, `docs/ARCHITECTURE.md`, `docs/HARBOR.md`, `docs/AUTOMATION.md`, `docs/GRAND_COMMISSION.md`, `docs/PLAYER_GUIDE.md`, `docs/UNITY.md`, `docs/TASK-2.2-VERIFICATION.md`, `docs/TASK-2.3-VERIFICATION.md` and `docs/ECONOMIC_CIRCULATION_REVIEW.md` as relevant. Inspect source before choosing which VisualElements to move/reuse:

- `unity/Assets/SaltAndLedger/Runtime/ArchipelagoLab/ArchipelagoLab.uxml`, `.uss`, `ArchipelagoRunExperience.uss`.
- `ArchipelagoLabController.cs`, `ArchipelagoRunExperience.cs`, `ArchipelagoCityEconomy.cs`, `ArchipelagoMap.cs`, `ArchipelagoCommission.cs`, `ArchipelagoHarbor.cs`, `ArchipelagoIndustryOverlay.cs`, `ArchipelagoCompanyPanels.cs`, `ArchipelagoRoutePlanner.cs`, `ArchipelagoLabSession.cs`, `ArchipelagoTextScale.cs`.
- `src/SaltAndLedger.Simulation/RegionalWorldSnapshot.cs`, `CompanyStockpile.cs`, `CargoItineraryEvaluator.cs`, `LivingCityPricing.cs`, `PrivateProductionRules.cs`, `RegionalCirculation.cs`.

Create a concise `tasks/TASK-2.5-PROGRESS.md` documenting final scope, UI ownership, screens and navigation, read-only projection APIs, legacy behavior, **visual asset ownership/licensing and the shared style/component contract**, and tests. Define shared UI contracts before parallelizing. Avoid adding one more monolithic controller: use small dedicated partials or presentation classes where appropriate. Retain the existing authoritative `ArchipelagoLabSession`/`RegionalWorldSnapshot`/`ArchipelagoMap` instead of making another quasi-authoritative UI model.

## 2. Target visual hierarchy and primary HUD

The **gameplay** screen at a typical 1366×768 default scale must have these functional layers:

### 2.1 Compact global bar

Display only:

- Current day (and challenge deadline/remaining time if applicable), with a clear pause/play/next-day control.
- **Spendable company cash**; optionally compact **reference Company Value** (label it explicitly; not cash/profit).
- Compact Grand Commission progress (`2/4` goals, or corresponding legacy challenge cue) leading to the existing Journal/Goals screen.
- A compact count of *actionable* notices, if any, leading to a list.
- Menu/pause. Keep save/load, Ledger and specialized Company controls available through appropriate destinations, rather than forcing all into the global bar.

**No raw 4-decimal balances at default scale.** Choose and document a display-only rounding convention suitable for HUD (e.g., whole/one decimal with a precise tooltip when rounding would conceal a nonzero quantity). Do not round authoritative values used for affordability or scoring. In Harbor execution previews and Ledger retain exact displayed monetary precision and executable totals.

Remove redundant status paragraphs, map-control instructions and the permanently expanded company/ascension explanation from the gameplay HUD. Ascension/version details belong in setup, run info or a details view. Use short noun/number labels and familiar iconography, with text equivalents.

### 2.2 Consistent mode strip and routes to real actions

Provide visible navigation for **Map, Trade, Industry, Fleet, Economy, Objectives**. These must not be dead tabs or merely decorative underlines. Each maps to an actual working view or existing functional workflow:

- **Map**: primary map + bottom contextual deck.
- **Trade**: open existing Harbor trading in the context of the selected town/owned ship; preserve market and company-stock purchases, whole ship trades, prices and paid transaction previews. If no relevant context is selected, show a functional town/ship chooser, not an inert placeholder.
- **Industry**: show/select the player's real facilities in the selected town, existing construction and policies, and a clear navigation into the existing Industry view. Do not duplicate its commands in a second implementation.
- **Fleet**: a dedicated operational workspace with owned ship selection, mapped itinerary/next-stop context, cargo/ETA/blockers, and genuine entry into the existing Route Planner. Its status must come from the authoritative ship and itinerary snapshot/evaluator.
- **Economy**: task-focused selected-town status with read-only next-need/production signals and links to existing full City Economy, Ledger and pricing details.
- **Objectives**: retain the real Journal (and legacy-challenge behavior) with exact permanent/current predicates; keep selected context when returning.

An economical implementation may compose existing detailed screens rather than reproduce all of them in a new shell. **No duplicated trade/warehouse/route execution logic.** Selection and nav must be visually clear, with one active focus/mode at a time.

### 2.3 Main map canvas

Give the world map noticeably more space than the current left-map/right-always-open-sidebar split. On Map mode:

- The actual selected town/ship remains clearly marked; town and ship markers remain clickable and reachable by keyboard.
- Town labels should prioritize town name and, if applicable, *one* significant status icon; do not repeat `0 player · 0 NPC` on every marker. Show actual ships through their markers rather than repeating zero counts in prose.
- Ship statuses should visually distinguish player from NPC and active/docked/blocked where grounded.
- **Six distance captions must not permanently compete with the map.** Make their display contextual (e.g. selected route/port or an explicit Routes overlay), with accurate travel days from the authoritative route/speed calculation.
- No large world/role descriptions printed on the canvas. Port details belong in the command deck or an expanded view.
- Evolve the current schematic map into an **illustrated, legible nautical chart**. Retain truthful seeded coordinates and hit targets; add modest visual layering (landmass fill/shoreline outline, subtle coast/wave/chart detailing), distinct town-role icon/landmark treatment, and readable ship silhouettes/markers. Do not need four fully painted unique city-scene sprites. Do not use a single large screenshot/illustration as the map: geography and selection must remain data-driven and dynamic.
- Respect readability and rendering cost: treat artwork as visual dressing, separate from navigation geometry. Do not mask ports, ships, routes, alerts, or focus outlines behind textures or map noise.

### 2.4 Bottom contextual command deck

**Default state:** minimal/collapsed when nothing meaningful is selected (or a one-line action hint). It must be easy to reopen; expanding/collapsing and selection must not advance days.

**With a town selected:** expose at most these immediately:

- Town name and *short* role (`Processing`, `Food source`, `Forest`, `Coastal`).
- At most **three concise and relevant indicators**, chosen from: next Bread need/stock, municipal liquidity, public/owned production blocker, near-term event. Each contains a number/label and interpretable status where appropriate. Do not show idle “0/0 consumption” as starvation; show `Awaiting first need` or equivalent.
- One prioritized actionable reason (`Mill needs 2 Grain`, `Food stock short for next need`, `Sale blocked: market cash insufficient`) grounded in real snapshots/previews. If nothing actionable, an appropriate calm state; avoid fabricating urgency.
- Primary contextual actions: **Harbor**, **Industry**, **Town details/Economy** when available; hide/disable accurately for modes without corresponding actions. More specialized actions are one level deeper.

**With an owned ship selected:** name, location/voyage ETA, cargo occupancy, current automation status and next instruction or block reason; primary actions such as **Open Harbor** (when docked), **Route Planner**, **Ship details/Fleet**. NPC ship is inspection-only. Never offer an invalid remote cargo action.

At 1366×768 Default, the expanded deck must be compact, responsive, **not internally scroll by default**, and must not cover the selected markers or erase the usability of the map. Retain sensible layout at Large and Extra Large via wrapping, controlled secondary scrolling or collapse; **never shrink the existing text scale** to make everything fit.

The mockup `01-Map-Context-Deck.png` illustrates a hierarchy, not an exact requirement for its three fictional indicators. The implementation must show actual data.

### 2.5 REQUIRED aesthetic system: Illustrated Maritime Ledger

**This section is a meaningful expansion over the earlier UX-only spec.** The structural wireframes (`01`–`03`) still define **where and when** information appears. The two illustrated reference sheets now define **how it looks**. `docs/design/task-2.5/ART-DIRECTION.md` is the detailed normative implementation guide.

**Visual composition contract**:

- **World/map:** dark deep-sea teal with subtle nautical chart texture, richer but restrained shorelines/islands, small distinct maritime ship and port markers; chosen town and selected route should stand out. Default map must still be data-driven, not a static painting.
- **Focused interaction surfaces:** warm parchment/paper-inspired panels with high-contrast dark-ink text, matte appearance, restrained old-gold rules/trims, sea-green selected/success states, rust-color warnings. A dark teal header/footer may frame a parchment work area; don't turn every panel bright beige or decorate every table cell.
- **HUD:** dark transparent/opaque teal strip with compact, legible gold/cream accents. Not a massive permanently expanded parchment banner; map must retain space.
- **Contextual bottom deck:** parchment-facing when expanded; short actual-value badges, one actionable alert, prominent stable Harbor / Industry / Details buttons. Collapsed state should be small and quiet.
- **Dense Harbor trading table:** priority is **precise side-by-side quantities and financial execution**. Keep rows clean, alternate subtly where helpful, use meaningful small good icons and short status text. Never hide bid/ask, capacity, affordability or exact transaction total for ornament.
- **Fleet / Industry / Economy / Journal:** a coherent family of parchment cards, dark-ink numerical sections and navy/teal action bars. Fleet has legible ship silhouettes and highlighted route; Industry can visualize actual `inputs → product` recipe; Economy presents **small fact/alert tiles first**, fiscal prose in expandable details; Journal uses readable permanent/current state, not just color.

**Required reusable design tokens/components** (do not hard-code visually divergent per-screen styling):

1. A documented palette with roles (deep sea/navy background, lighter ocean teal, parchment base, parchment highlight, readable ink, antique gold, sea-green success/selection, rust/orange warning, muted/disabled border). Use token names and actual final RGB/HEX values documented once; use the swatches in `ART-DIRECTION.md` as *starting values*, and adjust empirically for contrast.
2. Semantic typography: nautical/serif-like headings only where licensed and legible; **normal readable sans or similarly crisp text for body, values, tables, control labels**. Existing TextScale Compact/Default/Large/XL continues to govern real glyph size. No new restricted/commercial font dependency and no font files from moodboards.
3. Shared UI Toolkit styling of parchment panels, section headers, goods/status rows, contextual action cards, primary/secondary/danger buttons, compact stats, disabled/hover/focus/selected states and low-key inline explanation. Required keyboard and pointer state contrast; never rely on color alone.
4. **A small consistent icon family**, applied rather than merely documented: recognizable distinct symbols for all current v3 physical goods (**Logs, Planks, Grain, Flour, Bread, Furniture, Silver**) plus reusable Ship/Industry/Market/Cash/Attention/Info indicators. Styles and size classes should match. Allow modest practical icon reuse from a single authored source family; don't introduce twelve incompatible stock-icon styles. Include short text labels/tooltips for accessibility. Existing game/project icon/vector APIs are fine; if a chosen vector pipeline does not import reliably in Unity, use deterministic compatible raster fallbacks. Keep production sources and generation process reproducible.
5. **One restrained illustration example** visibly integrated in a genuine UI journey: the selected-town/Harbor header or a representative Bakery/ship tile, plus recognizable coast/landmark treatment on the map. Original generated moodboards are **reference art only**, not production UI backgrounds. Strong silhouettes and consistent material/colors matter more than painterly detail.

**Bounded application surfaces in this milestone**: the **Map/Deck**, **Harbor**, **Fleet**, and selected-town **Economy** must visibly share the style; **Industry** must include its recipe/status styling; the Grand Commission **Journal** and setup/map-reveal should receive a consistent lightweight header/panel treatment so navigation is not jarring. Do not commission every ancient legacy page, encyclopedia entry, all results/screens, town-specific buildings, or high-detail ship art. Preserve controls and data first. Use shared tokens to make later expansion inexpensive.

**Asset provenance and performance**: create only assets authored for this project or clearly license-compatible materials. Don't fetch/copy UI imagery from Port Royale/Anno or random web searches; don't commit prohibited or unlicensed fonts. Keep textures/icon atlases reasonably sized, package exact sources as appropriate, and don't turn dozens of large full-screen PNGs into runtime overlays. No runtime network asset fetch. Make the same symbols legible against deep-teal and parchment backgrounds and at Large text scale. Don't reduce apparent complexity by reducing usable contrast or hiding executable price information.

**Do not overbuild:** If art complexity threatens the milestone, finish a coherent style library and a **polished representative map → Harbor → Industry → Fleet/Economy loop**, rather than dozens of standalone decorative icons. The requirement is a visibly changed *working interface*, not complete commercial-game artwork.

## 3. Focused management presentation

### 3.1 Fleet mode: a real cross-context workspace

Approximate composition: owned fleet list (one compact row per ship), central **same authoritative map** with the selected route highlighted, and a selected ship inspector. The other existing ship-picker/dropdown route must continue to work. Display at a glance:

- Ship, location/ETA or dock, capacity and actual hold occupancy.
- Automation status (manual/active/waiting); current stop index, *next instruction* and real last/current reason when itinerary is applied.
- Highlight that ship's exact configured itinerary (ordered ports). Show route only when requested/selected; do not render every route in bright colors simultaneously.
- Read-only next action / `Wait` / `Skip` reason using the existing `CargoItineraryEvaluator` / `RegionalAutomation` result, not a guess.
- Working **Inspect/Edit Route** action which reaches the current planner with the correct selected ship. Keep manual control, apply/enable, stale-state guards, multiple goods, fees, whole cargo and FIFO behavior unchanged.

Preserve individual ship choice when going back to Map, Harbor, or Planner, including a paused/blocked ship. If a ship has no itinerary, say so without rendering a fictitious next stop.

### 3.2 Economy mode: facts before accounting explanation

Show selected town: concise population/prosperity trend, next essential need, market cash versus household cash, and a public/owned production signal, with clear subordinate access to the complete City Economy/Finance explanations. Use basic comparative bars/numbers where useful, and label when a metric is not applicable (Classic/v1). Don't recite the fiscal model in the first panel.

**Required two modest read-only projection vertical slices:**

1. **Next essential need**: next due day, exact prospective quantity at current population and persisted fractional demand remainder, current market stock, potential stock shortfall; separately assess purchasing power if the canonical retail preview exposes it. Never turn a current-stock comparison into an unqualified future promise. If no scheduled need or no modeled households, say `Not modeled`/`No need` instead of zero-demand scare text.
2. **Next production opportunity**: next eligible attempt day for a selected or prioritized public/private facility, current input sufficiency per recipe, storage/output constraints, enable/construction/payroll state, and resulting **potential output for one funded batch**. Use existing production previews plus recipe data. Private facilities in pooled stock must respect other facilities' access and cannot assume inputs will remain unused. Call the result `Ready from current state` / `Blocked now` / `Would produce if inputs remain` rather than predicting guaranteed future execution. Distinguish `last committed outcome` from `current readiness`.

**Do not build a 30-day predictive simulator in this task.** Current market bids/asks and exact transaction previews are facts; future prices depend on subsequent operations and must not be shown as guaranteed. Preserve the existing price overview and optionally link to it.

### 3.3 Trade and Industry: preserve working depth

Harbor already offers a useful side-by-side market/ship/local-stock table and exact total, average, gross/fee/net preview; preserve that functional dense table. Improve the **entry and return journey**, not the price engine. Town and ship selection should carry into Harbor, and closing it should return to the correct mode/context. Industry controls must continue to work against shared town stock: one Mill can create Flour consumed by one Bakery in the same company town pool. Production, FIFO, cost basis, constraints, and industrial/itinerary routes must not change.

### 3.4 Goal and alert discoverability

The persistent goal cue should open the actual objective Journal. Don't replace typed progress with invented GUI counters. A small alert surface (max ~3 prominently shown at once, more in detailed list) should provide **specific target + cause + destination**; clicking it selects the related town/ship/facility or opens the relevant functional screen. Suppress noisy, unactionable notices: e.g. `0/0` before the first food observation, or every unbuilt industry in every town. If a warning isn't provably actionable, show it only in an expanded diagnostic view.

## 4. UI-owned selection, navigation, preview lifetime and input

### 4.1 One contextual selection model

Retain the existing authoritative Session selection, with a lightweight UI-owned `PresentationContext` (or equivalent) tracking selected town, selected ship, active mode, optional facility/commodity, and last return destination. It is a **presentation state**, not an economic account or alternative simulated world.

Switching among Map/Fleet/Economy/Trade/Industry/Journal should preserve relevant selection. Opening Harbor from Northport must not mysteriously reselect a different ship or town. Opening Route Planner from Ledger must open Ledger even if other ships exist. Returning to previous view must preserve scroll selection when reasonable without rebuilding the economy.

Mouse/right-click, keyboard focus/Enter/Escape/Back, stacked modals, gamepad navigation when already supported and text-size scaling must remain coherent. Don't accidentally allow world time to continue behind management views. Document clear playback semantics: read-only views and menu transitions do not advance time; the existing Pause/Play/Next day remains the sole time control. Closing an inspected view must not silently enable a previously paused route or execute a trade. Confirmation dialogs must retain their existing safeguards.

### 4.2 Exact, read-only display projections

Put derived UI signals behind one small, easily unit-tested, **pure** projection API over committed snapshots and canonical content, preferably in the Unity-independent simulation project **only when it can be implemented without changing authoritative state**. Alternatively a host-only pure service may call existing snapshot previews. Decide once, document it, and prevent duplicated food/production calculations.

No calls that mutate `RegionalWorldState`, consume RNG draws, run a new economic day, simulate future transactions or alter a saved state. Projection code may be cached by `(snapshot revision, relevant selection)` for performance. Avoid constructing a fresh world as a side effect of opening UI. It must return structured fields and reason codes/concise authored labels, not opaque concatenated accounting paragraphs. Test identical outputs for repeated calls and no before/after fingerprint differences.

Fallback behavior in Classic, seeded v1, Living Cities v2, old v3 separate storage and all current pooled/itinerary/circulation combinations is mandatory. No null-reference regressions when City/Economy/Commission/Itinerary is unavailable.

## 5. Future-proof map viewport without premature new map features

Refactor `ArchipelagoMap` (or introduce a small `MapViewport` collaborator) so **world coordinates are clearly separate from UI-screen coordinates**:

- One testable world-to-viewport projection with fit-to-world bounds, transform/inverse when applicable and clamped padding for labels and marker interaction.
- Port markers, NPC/player ship markers, hit targets, selected-route overlay and any preview-map variant use **the same coordinate transform**.
- Layout changes, DPI/text scales and map resizing must not reposition islands inconsistently relative to ships or lose clickable hit areas.
- Clear hook/structure for future camera center, zoom factor, pan and a minimap viewport rectangle. The state should be **UI-only**, not part of the seeded geography or economic save.
- Default fit-all four-town layout remains the sole required user-facing camera mode in this milestone. Do not implement a working minimap or force zoom/panning onto a four-town world. Do not introduce divergent preview-map and gameplay-map projection code.
- Route lines/labels can be filtered by selected ship, selected town or explicit overlay. Future larger maps should not force six labels onto all worlds.

Test known world-space positions and viewports, selection/picking at different aspect ratios, and correct relative location of sailing ships when interpolation is enabled. If inverse hit test is not needed because UI Toolkit buttons own picking, document that and avoid unused complexity.

## 6. UX presentation standards and accessibility

- Use **one unified Illustrated Maritime Ledger component family** from the art-direction guide: accurate icons, strongly readable ink-on-parchment management surfaces, restrained antiqued-gold trim and dark teal chart/HUD. Icons identify goods/ships/status, never substitute for necessary text. Don't copy art or UI assets from Port Royale/Anno.
- Warning states must combine icon/word and color; colors alone are insufficient. Do not use the same red for every benign stock difference.
- Distinguish `market cash`, `household cash`, `company cash`, and `Company Value` everywhere. Company Value remains reference valuation, not liquidation proceeds.
- Distinguish market supply relative to upcoming household needs from private production-input scarcity, which is a separate account/store.
- Quantities and balances should show compact HUD precision while detailed previews remain exact. Use consistent units (`Bread`, `coins`, `days`), no mysterious `0/0 paid` on a not-yet-observed need.
- Avoid decorative permanent paragraphs such as `Trades spend no time; economic state is paused` across the main map. Put long explanations in help/tooltips and first-use descriptions where appropriate. **Decorative textures and typography must not displace the actual next action or create contrast problems**; if a foreground control is hard to read over patterned parchment, simplify the texture, not the text size.
- A meaningful player action (Harbor trade, Industry, Fleet route, Town Economy, Journal) must be reachable directly from a selected town/ship, without more than one additional relevant decision screen. Avoid nested modal dead ends and inaccessible close buttons.
- At 1366×768 Default, the map + primary actions should not require the entire game page to scroll. Long detailed tables can scroll **within their dedicated views** while navigation/close remains pinned. At Large/XL, reflow and let detail scroll, but preserve readable font sizes and keyboard focus.
- Apply the same component tokens/design hierarchy to main menu, setup/map reveal, Journal and results **where straightforward**, especially shared titles, header/footer, selected cards and actions; **do not fully redesign every historical page**. Avoid visually jarring transitions from map/deck into Harbor/Industry/Planner. The goal is a coherent core visual language, not exhaustive repainting.

## 7. Integration paths and scenarios to demonstrate

### 7.1 User journeys (must actually work)

**Journey A — selecting and resolving a production problem:** start a new named v3 seed-99 Merchant/StockOnly/A0 Grand Commission. Select the processing town, inspect its own/public current input shortfall and next eligible production opportunity; from the deck open Harbor, obtain real Grain/other legitimate input through the existing operation, return and see the status update. This must not claim a guaranteed Mill production if wages/capacity/shared-stock ordering could block it.

**Journey B — ship and itinerary:** select an owned ship and switch to Fleet. See actual whereabouts, chosen itinerary steps, next instruction and block reason if currently waiting. Open Route Planner, modify/apply using existing proper guards, return to Fleet and then Map with the same ship selected. Verify that showing route overlays/distance changes never moves cargo, changes company money or advances the day.

**Journey C — economy and food:** pick a living city with an upcoming essential need. Show the due day, requested quantity at current population and fractional remainder, available stock and *current* affordability indicators without inventing future prices. Inspect exact City Economy to see cash split and detailed financial behavior, then return to the selected city on the map.

**Journey D — continued and legacy saves:** continue a real mid-voyage schema-5 v3 run, inspect the new shell and resume with exactly preserved authoritative bytes. If an archived separate-warehouse save is available (e.g. previous user-provided day-140 fixture), open it using its legacy semantics and inspect without pooled assumptions. Frozen terminal saves remain frozen and can show results/Journals. Historical Classic/v1/v2 setups and UI should remain usable.

### 7.2 Verification gates

- Compile standalone Release C# 8 / `netstandard2.1` simulation and existing Unity host without unexpected dependencies.
- Existing full .NET/Python/content/generated-docs and report freshness checks pass. Preserve historical report bytes and save bytes; do not regenerate experiments merely because the UI changed.
- Add focused pure projection/unit tests: fractional next need and next due; actual food shortage vs first observation; funded/unfunded production; private pooled inventory vs public market; route wait cause; legacy no-city/null cases; non-mutation; overflow boundaries/rounding.
- Add Unity Edit/Play tests for: initial map with no selection/collapsed deck, selected town, selected player ship, NPC ship inspection, mode transitions and back, Harbor/Industry/Fleet/Journal wiring, independent map overlay, modal and pause lifecycle, route plan selection, saved-run reload and zero-state-change from inspection.
- Test at least **1366×768 Default, 1366×768 Large, 1920×1080 Default/Large**, plus existing Compact/Extra Large where feasible. Inspect genuine Editor screenshots; include real Windows Mono player tests and native interaction if available; disclose unavailable checks.
- A fixed human-readable visual review comparing the old HUD screenshot with the new Map, Fleet and Economy states. Record explicit reasons for divergences from mockups. No staged/static screenshot may be passed off as the actual running UI.
- Verify no duplicate `VisualElement` name collisions, no out-of-bounds controls, scroll capture trapping the player, text overlapping tabs/markers, or loss of keyboard focus after model updates. Avoid regenerating all visual elements every frame if it resets focus or scroll; maintain responsive viewport and bounded allocations.
- Smoke through ordinary economic operations: dock trade; pooled Mill→Bakery real production; several itinerary actions; paid food; save/continue; Grand Commission victory; no formula changes or free transfers. Reuse existing genuine command witnesses where possible rather than building a new balancing sweep.
- **Small documentation/CI maintenance:** update `docs/PLAYER_GUIDE.md` to describe the actual mode navigation, deck and current-vs-future projections; add `docs/UI_DESIGN_SYSTEM.md` with reusable hierarchy, map viewport contract and player task flows; update `docs/ROADMAP.md` to include Tasks 2.2–2.5 and future captain/curse/deeper forecasting directions. Add `--economic-circulation --check` to appropriate CI validation if it fits the time budget; otherwise create a separate bounded CI job/report validation and document the choice. Do not publish/reconfigure Pages.

## 8. Implementation order and scope control

1. Map actual existing controls and important player journeys; document UI selection/mode/return contracts in progress file.
2. Establish pure read-only `EconomicSignals` and future-proof `MapViewport` abstractions with focused tests. They must not depend on a half-built new shell.
3. Make the compact main bar, functional mode navigation and map-first deck **work with the existing style** before deep theming; preserve original controls through adapters where necessary. Ensure Gameplay is functional before expanding dedicated views.
4. Establish a small **Illustrated Maritime Ledger** UI Toolkit design-token/component/asset layer (color, hierarchy, cards, buttons, states, commodity/ship/status icons). Apply it consistently to the main map/deck; make the map visually more chart-like without changing selection or geography.
5. Wire and visually style Fleet and selected-town Economy as functional views, plus focused Harbor and Industry usage; reuse City Economy, Ledger, Journal and Route Planner. Use compact headers/icons/fact tiles in place of permanent prose. Do not broadly rewrite advanced route editing.
6. Polish integrated map → town → Harbor/Industry and ship → Fleet journeys; fix navigation, Back/pause/collapse/selection/resizing, legibility and keyboard focus. Update help/docs and capture genuine Unity before/after references.
7. Execute targeted C# and full CI-equivalent suites; rerun affected Unity Edit/Play and Windows Mono end-to-end journey when tools permit. Record counts and blockers; audit every criterion below.

**Budget discipline:** functional navigation, exact read-only projections, and input/persistence regressions take priority; the bounded reusable icon/panel system and one coherent polished path are mandatory too. If an elaborate illustrated ocean texture or one-off town painting would consume the remaining budget, defer that single art flourish. Never declare the theme done if only a static concept image or one screen changed.

Delegate isolated UI presentation, projection tests and documentation only after shared contracts are stable. Integrate and review frequently. If the work grows too large, preserve the **fully functioning new Map shell + Fleet route workspace + next-food/next-batch projections** ahead of cosmetic refinements; don't substitute placeholder tabs, mock data, or unsupported victory claims. Finish with a coherent vertical slice.

## 9. Definition of Done (explicit audit required)

1. Real default gameplay screen is map-first, with compact global bar and no permanently expanded old city sidebar.
2. Selecting a town gives a concise non-scrolling bottom deck (default resolution), real priority information and working Harbor/Industry/Economy actions.
3. Selecting an owned or NPC ship gives correct location/cargo/route status and ownership-limited controls; manual/automated route state is not altered by inspection.
4. Functional Map/Trade/Industry/Fleet/Economy/Objectives navigation exists, with consistent highlighted mode and selection, and correct Back/Escape/close semantics.
5. Dedicated Fleet view shows real ships, current route/stop/action/status and an accurate selected-route overlay, opening the existing planner for the correct ship.
6. Dedicated Economy view shows real selected-town cash/needs/production data, actionable near-term projections, and leads to full detailed accounting.
7. Next household essential need projection uses exact schedules, population and fractional demand residuals; no `0/0` false crisis, unauthored future price guarantees, or phantom food.
8. Next facility production projection is sourced from actual recipe/stock/enabled/capacity/payroll/readiness with clear current-state versus future caveats; pooled competition is not ignored or guaranteed away.
9. Conditional map distance/route labeling reduces clutter while preserving accurate route inspection and all required marker picking.
10. A tested, shared map world/viewport projection supports resizing and provides an extensible future zoom/pan/minimap foundation; no actual zoom/minimap is necessary.
11. Existing Harbor, facility/stock, itinerary, route application, objectives, pause, results, retry and save/export remain functional and economically identical.
12. Opening, switching or closing any read-only UI surface causes no economic world mutation; exact snapshots/fingerprints and tests demonstrate this.
13. Old supported modes and saves work without schema change, forced migration, reinterpreted storage, or visual crashes; new schema-5 continues exactly.
14. 1366×768 Default and Large and 1920×1080 Default and Large are inspected with actual Unity captures; labels and actions remain legible/reachable, including keyboard and modal navigation.
15. Documentation and in-game help reflect the new UI; old player guide references are not left as contradictory primary instructions; maintenance CI check is explicitly addressed.
16. `tasks/TASK-2.5-PROGRESS.md` plus `docs/TASK-2.5-VERIFICATION.md` record actual executed checks, screenshots, economic invariants, limitations and every criterion separately. No unverified Editor/player evidence is claimed.
17. **The new Illustrated Maritime Ledger visual direction is visibly present in the real Unity gameplay**: deep-sea map/nav paired with restrained, legible parchment management surfaces, gold/teal/rust semantic accents and an intentional typography hierarchy; not solely in reference images.
18. Reusable, documented **design tokens and UI Toolkit components** drive the map/deck, Harbor, Fleet, town Economy and Industry; states include focus/hover/selected/disabled and both normal/alerted status. No duplicated one-off styling required for every new screen.
19. At least seven **distinct recognizable existing-good icons** (Logs, Planks, Grain, Flour, Bread, Furniture, Silver) and a concise cohesive ship/industry/market/warning/icon family appear in functional UI, with text equivalents; asset provenance/import behavior is recorded.
20. The map receives a modest attractive chart-style treatment (shore/land/ship/town symbols or equivalent) using the **real geographic layout**, maintaining route overlays, selection accuracy and readability. At least one genuine representative town/ship/industry tile has coherent illustration treatment; no concept-sheet screenshot is used as a static map skin.
21. After visual styling, real Unity captures at the required resolutions/scales verify **readable parchment text, icon meaning, actionable status, precise Harbor prices, visible focus and reachable actions**, with a qualitative comparison to both the current baseline and the illustrated concept. The visual reference guide describes deviations and remaining art polish honestly.

## 10. Handoff

Return a concise technical summary: UI design chosen and any deviations from reference, relevant implementation files, new projection/view contracts, adopted theme tokens/icon inventory and art provenance, genuine Unity screenshots from the final player, test outcomes including any failure/unsupported platform, old-save integrity, documentation updates, and exact next usability questions for the human playtest. **Do not begin Task 2.6, implement captains/curses, or launch a browser build.**
