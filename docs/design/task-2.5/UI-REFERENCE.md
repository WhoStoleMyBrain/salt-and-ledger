# Salt & Ledger — Task 2.5 visual reference and interaction intent

These reference assets are **original UI concepts**, not executable game state, a replacement for the specification, or pixel-exact design assets. All displayed cash, inventories, production readiness, ship names/ETA and needs are intentionally **illustrative**. Unity UI must bind to authoritative snapshots and exact read-only previews. The **Illustrated Maritime Ledger art direction is required**: the original [Art Direction Concept Sheet](<mockups/Art-Direction-Concept-Sheet (2).png>) is the primary aesthetic moodboard, supplemented by the [Visual Components Reference](<mockups/04-Visual-Components-Reference (1).png>) for colors, icon silhouettes and panel behaviors. The image *pixels* are still not production sprites; create an actual reusable, readable Unity UI Toolkit component/asset family following [ART-DIRECTION.md](ART-DIRECTION.md). The local reference filenames retain their supplied download suffixes.

## Historical baseline screenshots

- Baseline HUD: https://github.com/WhoStoleMyBrain/salt-and-ledger/blob/3feb6b5c1fa88fbc3c176ffdc89f5de9a02aaf92/docs/images/harbor/editor-hud-default-1366.png
- Baseline Harbor: https://github.com/WhoStoleMyBrain/salt-and-ledger/blob/3feb6b5c1fa88fbc3c176ffdc89f5de9a02aaf92/docs/images/harbor/editor-harbor-default-1366.png
- Baseline Route Planner: https://github.com/WhoStoleMyBrain/salt-and-ledger/blob/3feb6b5c1fa88fbc3c176ffdc89f5de9a02aaf92/docs/images/task23/editor-draft-default.png
- Baseline town economy: https://github.com/WhoStoleMyBrain/salt-and-ledger/blob/3feb6b5c1fa88fbc3c176ffdc89f5de9a02aaf92/docs/images/task24/player-city-default.png

## Target 01 — Map + Context Deck

Reference: [Map + Context Deck](<mockups/01-Map-Context-Deck (1).png>).

- Global time/company/goal bar and a thin mode strip, **not a third huge status panel**.
- Map is the primary spatial workspace. Islands and ships are visually identifiable; distances and all ship route labels are not continuously shown.
- Selected town opens a **compact bottom command deck**: name/role, an actionable issue, next essential-food comparison, cash, production status and relevant primary actions. Unselected state collapses; selected ship shows a distinct cargo/route-oriented deck.
- For real state, do not always show Mill/food/cash indicators: choose the top two/three meaningful facts, suppress noise, and replace unavailable statistics with honest `Not modeled`/`Not yet observed`.
- A compact alert count opens a navigable list; alerts are contextual, not every idle/zero-value field.

## Target 02 — Fleet mode

Reference: [Fleet mode](<mockups/02-Fleet-Mode (2).png>).

- Side ship list, **same world map** highlighting just the selected itinerary, and a right-side ship inspector.
- Real dock, voyage, ETA, cargo capacity and selected/blocked action. Clicking the route controls enters the **existing** multi-stop planner; don't reinvent its commands.
- Preserve ship context across Map, Fleet, Harbor, Route Planner and back. Own/independent ships must never become conflated.
- The mockup shows one hypothetical 3-stop loop; valid routes and statuses come from `CargoItineraryPolicy` and `CargoItineraryEvaluator`.

## Target 03 — Focused town economy

Reference: [Town economy](<mockups/03-Town-Economy (1).png>).

- Facts/status first: next paid household Bread opportunity, current physical stock versus quantity needed, actual municipal cash, one production bottleneck and how to act.
- Secondary charts/bars show *current comparative quantities* rather than unsourced historical prices or future guarantees.
- Money accounts remain distinguishable, with genuine detail linked to City Economy and Ledger.
- Next-need and next-production calculations must be derived read-only from live snapshots/canonical rules. If a downstream facility shares a stockpile, current input readiness is conditional, not a promise of future output.

## Interactive HTML

The [interactive prototype](<mockups/SaltAndLedger_UI_Prototype (2).html>) offers a local visual comparison of the three layouts. You can switch between Map, Fleet and Economy and select illustrative towns on the Map. Trade/Industry/Objectives icons deliberately **do not model or execute gameplay**; in the **Unity implementation** every mode must be real, with actual actions or an existing functional screen. Do not port this HTML into Unity; it demonstrates spacing and hierarchy only.

## Required art direction and supplementary reference

- [Art Direction Concept Sheet](<mockups/Art-Direction-Concept-Sheet (2).png>) — **primary moodboard** for richly illustrated nautical map, warm parchment management panels, aged-gold accents, small resource/building/ship art, and a coherent trading-world identity.
- [Visual Components Reference](<mockups/04-Visual-Components-Reference (1).png>) — **supplementary generated style board** with approximate deep-sea / paper / gold / green / rust palette, goods icons, collapsible panels and more concise workspaces. Text, figures and simulated mechanics visible in this board are NOT authoritative and must not be implemented as factual UI.
- [ART-DIRECTION.md](ART-DIRECTION.md) — **normative, source-friendly guide** for tokens, components, implementation limits, assets, readability and what does/doesn't need full illustration.

**Hierarchy:** real game data and accessible functional controls > existing 01–03 structural wireframes > these two art-direction references. `01`–`03` still define the map/deck/workspace composition even if they use the older dark surface. The new illustrated references define the **colors/material/identity**. Implement neither as a flattened static screenshot, and do not treat generated image text as correct game data. Game assets must be created or licensed appropriately, not copied from Port Royale, Anno or unrelated games.

## What the reviewer will inspect

- Can a player locate a food/industry/route issue from the map **without reading a wall of text**?
- Can they act through the correct Harbor/Industry/Fleet control and return to the same object?
- Does the primary HUD visibly breathe at 1366×768, while detailed trading remains precise?
- Can a user in Large text still find close/back and principal actions?
- Are preview/demand labels honest, source-backed and distinct from guaranteed forecasts?
- Does the real interface now exhibit **consistent illustrated maritime styling**, readable resource icons and calm parchment surfaces across the core user journey, not just the moodboard?
- Are dense Harbor and Planner panels still easy to read and operate despite visual ornament?
- Has Codex captured **actual running Unity screenshots**, separately from all these concepts, at the required text scales?

## Implemented direction and deliberate differences

The implemented shell follows the B+C structure: a compact dark global bar and six real navigation destinations, a larger live map, a collapsible parchment town/ship deck, a same-map Fleet workspace, and a focused town Economy view. Trade and Industry enter the existing functional overlays; Objectives enters the typed Journal. The old comprehensive inspector is reached through Details / log. The [design system](../../UI_DESIGN_SYSTEM.md) records the exact component, signal, selection and return contracts.

The reusable visual family uses dark navy/teal framing, light parchment cards, dark ink, gold trim, sea-green calm/selection and rust for grounded problems. Goods use seven distinct silhouettes with their real labels. Ship, workshop, scales, coins, warning and information glyphs share the same drawing language. Harbor and Industry include a modest workshop/coastal illustration; Fleet includes a ship vignette. These are original Painter2D source drawings. The [production asset inventory](PRODUCTION-ASSETS.md) records their source, import behavior and provenance.

| Reference intent | Implemented interpretation | Reason and remaining polish |
| --- | --- | --- |
| Rich painted sea and unique island scenes | Layered procedural coast, chart marks, role landmarks and selectable ship silhouettes over the actual map transform | Preserves seeded positions, resizing and real picking without a large static texture. More painterly town-specific artwork remains future polish. |
| Warm textured paper and ornate maritime headings | Calm flat parchment tokens, fine borders, readable inherited runtime font and clear size/weight hierarchy | Keeps precise prices and four text scales legible; no unlicensed font or distracting paper grain. A licensed heading font is optional later work. |
| Three illustrative deck facts and dramatic warnings | At most three real current facts plus one prioritized reason; benign first observations/manual states use calm styling | Reference values and implied urgency are not game rules. Alerts require a concrete current cause and destination. |
| Stylized commodity images and factory art | One original vector icon family, canonical recipe symbols and small reusable woodcut-like vignettes | Recognizable at practical control sizes, source-controlled and package-free. Illustration never claims a particular facility exists. |
| Example Fleet loop, charts and speculative controls | Selected ship's actual itinerary or voyage, current instructions and current-stock/need comparisons | No fictional route, future price guarantee, retained-history invention, minimap or unsupported feature is introduced. |

The first actual Unity Editor review at 1366×768 Default confirmed the live map, selected-town deck, Harbor, Fleet and Economy render with the new kit. Compared with the historical HUD, the permanent city prose column is absent and geography occupies more of the screen; Harbor retains its precise stock/price table on a calm paper surface. That review also found weak ship-label contrast, insufficient cash/value separation and overly urgent calm-state strips. Those findings led to explicit dark ship labels, a visible cash/value separator and warning-only rust treatment. They are concrete iteration findings, **not final acceptance of every scale, focus state or player journey**.

### Actual Editor and Windows player visual review

Current rendered evidence and limitations are recorded in [cleanup verification](../../REPOSITORY-CLEANUP.md). Historical screenshot matrices were removed; concept sheets remain inspiration only.
