# Salt & Ledger — Illustrated Maritime Ledger visual system (Task 2.5)

**Status:** Required implementation direction for Task 2.5. The accompanying AI-generated `Art-Direction-Concept-Sheet.png` is the **primary aesthetic moodboard**. `04-Visual-Components-Reference.png` reinforces components/icons. `01`–`03` define the **functional B+C layout**. No illustration is a screenshot, pixel-exact UI specification, sprite sheet, font license, or trustworthy gameplay data.

## Design thesis

**A trading chart you can operate; a beautifully bound ledger you can consult.** The ocean/world gets atmosphere and character; management information sits on calm, tactile paper with clear, precise controls. Gold should highlight direction and important affordances, not decorate every number. Information density changes with the task: a market table can be dense and exact; the ordinary map should breathe.

**Design hierarchy:** real economic rules and accessibility > legible task-oriented UX > B+C layout wireframes > this aesthetic moodboard > optional ornament. The result should be recognizable as Salt & Ledger and not a copy of Port Royale or Anno.

## Starting color tokens (verify contrast in Unity screenshots)

| Semantic token | Proposed HEX | Purpose | Guardrail |
|---|---|---|---|
| `sea-900` | `#103B45` | Ocean, map background, dark chrome | Keep thin routes/ship markers distinguishable |
| `sea-700` | `#2F6C73` | Ocean accents/secondary/nav | Avoid text on low-contrast teal |
| `navy-950` | `#0D2631` | HUD header, menu frames | Stable contrast with light numerals |
| `paper-100` | `#F4ECD8` | Readable paper/card highlight | No heavy grain behind small text |
| `paper-200` | `#E9DFC6` | Neutral parchment management background | Separate cards with subtle depth, not heavy borders |
| `ink-900` | `#26343B` | Numbers and body text on parchment | Must remain clear at small text sizes |
| `gold-500` | `#C4A36A` | Heading/selection trim | Don't use gold alone for vital low-contrast numerical text |
| `green-700` | `#396C60` | Valid success, Buy/Apply buttons when appropriate | Include words/icons with color |
| `rust-600` | `#A45139` | Problems, critical trade blocks | Distinguish warning vs destructive actions |
| `border-300` | `#B7AA8F` | Thin section boundaries on parchment | One-pixel rules should not dominate |
| `muted-600` | `#64727A` | Secondary labels on light surface | Re-check actual foreground contrast |

These are **initial theme targets**, not a directive to force exact colors regardless of contrast, monitor or Unity material rendering. Implement a coherent `sl-...` naming scheme using styles supported by this project's Unity UI Toolkit. Keep necessary hover/focus/disabled styles separate from semantic trade outcomes.

## Typography system

- Nautical/historical serif *feeling* for **large titles/town names**, if an already available, permitted font works; fallback to the current readable family rather than downloading an unlicensed one. Do not distribute fonts outside the authorized game assets.
- All prices, exact fractional quantities, table headers, forms, multiline explanations and UI control text: **clear functional text**, consistent tabular/numeric alignment where available.
- Three levels: **page heading**, **group/category label**, **primary metric**. Use typography, spacing, icon and contrast rather than underlining and all-caps everywhere.
- Existing global Compact/Default/Large/Extra Large preference remains authoritative. At Large/XL allow wrapping and meaningful scrolling in detailed views; never compress glyphs to retain decorations.

## Reusable physical components (build once; use repeatedly)

| Component | Visual behavior | Where used |
|---|---|---|
| `SeaHudBar` | restrained dark nautical bar; four high-value facts, time actions, alert count | gameplay map / relevant workspaces |
| `ModeNav` | one selected mode, semantic icon and short visible name; keyboard focus unmistakable | Map/Trade/Industry/Fleet/Economy/Objectives |
| `ParchmentPanel` | calm warm paper surface, thin gold or neutral trim, strong ink text | context deck; focused workspaces |
| `ContextHeader` | town/ship name, one role/route line, small matching illustration/glyph | bottom deck, Harbor, Fleet, Industry |
| `MetricTile` | label, number, unit, comparison; semantic state only when grounded | Bread next need, cash, ship ETA, input availability |
| `ProblemRow` | warning/info glyph, concise cause, clickable remedy/inspection | deck, Fleet, production |
| `GoodsGlyph` | single visual family for 7 currently implemented commodities | Harbor, cargo, recipes, market rows |
| `RecipeLine` | tangible input icons/counts → output icon/count; funded/current status | Industry/selected production |
| `TownMarker` / `ShipMarker` | location accurate, visually distinguishable and keyboard-selectable | generated map |
| `ActionButton` | clear primary/secondary/destructive; active/hover/focus/disabled variants | all actual commands |

Composition: Map view retains most screen area, town/ship deck collapses to a low-height bar. Paper sections support substantial text **only where the task requires** it (trade previews, diagnostics); ordinary context doesn't become a large paper essay.

## Mandatory icon family — small, coherent and factual

**Goods:** Grain (wheat sheaf), Logs (round bark/stack), Flour (sack), Bread (loaf), Planks (straight milled boards, visually different from Logs), Furniture (chair/cabinet), Silver (ingot/ore). Each must be recognizable at the actual HUD scale and remain distinguishable when color perception is reduced. Do not substitute one generic crate icon for everything.

**System:** ship silhouette, warehouse/crate, factory/gear, market/scales, coins, warning, info; additional iconography only if reused. Prefer a single scalable family of authored simple silhouettes or licensed icons with unified stroke/fill weight; valid Unity-compatible UI Toolkit vector or raster sprites are fine. Source artwork should be in clearly named project asset paths with needed `.meta`/import behavior, not hidden as enormous base64 UXML strings.

Icon and number must be semantically consistent. Never show `Bread` with a flour-sack icon, use a green tick to mean a conditional objective is permanent, or show a warning for a town with no food observation yet. Pair icons with labels/tooltips; don't use unlabelled color as the only signal.

## Per-screen art and hierarchy

### Map mode

Rich teal chart/ocean; landmasses have restrained contours/coast highlights and perhaps low-key decorative hatching. Town landmarks/role emblems and ship silhouettes signal identity without overwhelming the generated map. A small gold selection ring or clear outline follows real selection; an alert badge appears only for actionable problems. Sea routes and distance captions become contextual. Map must be fully procedural over authoritative positions, not a pasted flattened island background. Zoom/pan/minimap remain future structural hooks, not new controls.

### Context deck (illustrative paper over dark chart)

A single parchment strip with left-aligned context identity, one or two factual need/cash/production tiles, one most important actionable issue and three appropriately reachable actions. If no context, collapse to a subtle hint. Use compact labels like `Bread: 0.5 / 1.7 due D44` rather than permanent prose. Illustration may be a small consistent town-role tile, not an entire town painting.

### Harbor

Keep the necessary commodity table **dense**. Goods glyph+name; town/ship/company columns; market buy/sell bid/ask unambiguous; numeric columns aligned. Quiet alternating lines or soft separation, not a thick gilded border around each row. Execution pane retains exact bulk cost, average, gross/fee/net and buttons with high contrast. Ship ownership/dock status is obvious. Do not move exact numeric fields behind art or hide the long list beneath mandatory decorations. The Harbor viewport at 1366×768 remains responsive.

### Industry

Display a small representative working-factory tile with name, status, next due opportunity and **real recipe icons**. Keep shared-stock controls and per-factory policies functional; a single town stockpile serves all owned local sites. A compact `Ready now`, `Missing Grain`, `No payroll` indicator should precede detailed policy explanation. Conditional forecasts must not claim guaranteed production.

### Fleet

One small ship silhouette or tile per owned ship, selected-ship location/ETA/hold, highlighted route on map and short current/next operation. Use paper management cards over chart/map context. Do not overlay all NPC and player routes in bright colors at once. Existing Route Planner remains the real 24-action editor and should at least receive coherent header/cards/buttons without a complete redesign.

### Economy and Journal

Economy starts with next essential need, inventory vs demand, split town/household cash, public and private production blockers, followed by explicit detailed accounts. No default wall of tax/levy prose. Journal shows each selected objective, permanent vs current status and readable progress, not a decorative static checklist. Time/cash/score controls remain truthful.

## Asset-sourcing and implementation boundaries

- Concept-sheet and visual component reference are **moodboards**, not runtime assets or accurate recipe/currency diagrams. Do not slice them into images/buttons as if their text and quantities were correct. They may contain mock mechanics not in the game (weather, minimap, unsupported performance charts, extra ship types); do not add those mechanics.
- Do not copy art, fonts or screenshots from other games or stock asset sites without explicit documented rights. Create small original vector/pictogram sources or use existing license-compatible project assets. Keep any imported license notice if required. No runtime external fetch.
- Scope is the **small functional component system and coherent use across Map, deck, Harbor, Fleet, selected-town Economy, plus representative Industry and light Journal/setup treatment**. Full procedural shoreline art, unique painted cities, weather, character portraits, 3D scene art, animation suite and extra systems belong later.
- If an imported font/texture fails or causes unreadable text, fallback to a sober readable token, not a broken decorative look. Required screen states must be screenshot-verified in Editor/player.

## Visual acceptance checks

1. At a glance, can you distinguish a town, a selected ship, a warning, a valid action, each good, and which mode is active?
2. Does the map dominate while work panels stay calm and functional? Does collapsing the deck restore world space?
3. Can you compare precise market bid/ask, good quantities, total order costs and actual fees without scrolling through decorative art first?
4. Does a Mill/Bakery chain show its real inputs/outputs and funding status as symbols and concise numbers, with the underlying calculation still available?
5. Are dark HUD text and parchment numerical tables readable at 1366×768 Default and Large, and 1920×1080 Default and Large, including hover/keyboard focus?
6. Do actual Unity screenshots show the style transformation, not just newly generated reference art? Are original user saves and economic fingerprints unchanged by viewing?
