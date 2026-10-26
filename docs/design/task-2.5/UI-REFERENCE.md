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

The final-source review inspected the complete **24-frame Editor matrix** and the rebuilt Windows Mono player's **24-frame matrix**: town/deck, Economy, Fleet, Harbor table, Harbor order and Industry at **1366×768 and 1920×1080, Default and Large**. Representative Fleet frames at **Compact and Extra Large** were also inspected in both hosts, plus the player's real day-16 itinerary view. The rebuilt player matrix shows a fresh-process continuation of the actual day-4 save, not a constructed display state. These are rendered gameplay, not concepts or staged HTML. The [verification record](../../TASK-2.5-VERIFICATION.md) owns the committed capture links, completed tests, fingerprints and acceptance limitations.

The reviewed player frames show the intended material split consistently: restrained navy/teal navigation and chart, parchment deck/management surfaces, readable ink, calm green readiness and rust only for a real missing-input reason. All seven Harbor goods retain distinct silhouettes and text; the workshop and ship vignettes render in real Harbor/Industry/Fleet. Ship labels now have dark ink, and cash/reference value has a visible separator. The deck stays non-scrolling at Default and wraps actions at Large. Economy distinguishes public stock, private stock, household funds and conditional batch readiness. Active mode/focus borders remain conspicuous on the dark navigation.

At 1920×1080, Harbor shows all seven goods alongside exact order previews and Buy/Sell. The corrected **1366×768 Default** Editor frame shows both actions and the complete v3 fee/net split in its initial viewport: horizontal field labels reclaim vertical space without reducing text/control sizes. The rebuilt player's day-4 Grain preview demonstrates a different legitimate state: Buy is available, while Sell is disabled with an explicit local-stock shortage because production consumed the Grain. It does not invent a fee-bearing sale for unavailable stock. At 1366×768 Large the table and order form stack inside the detail scroll, preserving the header/Close and footer; the separate table/order captures show that reflow without shrinking the glyphs. Large's later Sell/transfer detail still requires scrolling. Industry keeps recipe symbols and current readiness above the detailed policy/warehouse controls, which need internal scrolling at the smaller resolution. This is an explicit density tradeoff, not a claim that every command fits in the first frame.

Four additional final-player Harbor captures select the town's actual **2 Bread** stock without executing a trade. A one-Bread sale preview shows **39.5 gross, 0.79 service fee, 38.71 net**, with **0.395** to each local recipient and an enabled Sell button. All amounts and both paid actions fit the initial 1366×768 Default viewport and both 1920×1080 variants; the 1366×768 Large capture shows the internally scrolled order section. These frames verify fee contrast and reachability in the rebuilt player as well as the Editor, while retaining the Grain-shortage frames as honest blocked-state evidence.

The earlier selected-town **1366×768 Large** frame had overlapping P1/N3 markers. Both final matrices confirm separate ship buttons and offset town callouts with leaders to their real positions; previously overlapping NPC markers are also separately visible. The Harbor first-viewport fit is corrected as described above. Fleet's repeated no-itinerary sentence is replaced with a distinct instruction to use the planner. The final player instead shows its genuinely applied three-stop itinerary, current voyage, carried Bread and next actual Sell instruction; by day 16 it shows mixed Flour/Logs cargo and the next Westhaven Unload instruction. No blocking visual issue was found in either required Default/Large matrix. Remaining polish includes the verbose Industry readiness header, the density of enlarged Fleet cards and tight long voyage labels at Extra Large. Compact/Extra Large retain the visible route, selected ship and pinned actions in the reviewed representative frames.

Native Windows mouse/keyboard injection was unavailable through the computer-use pipe in this environment. Actual UI Toolkit pointer/navigation/key events and bound-control operations were exercised in the running host and player; those checks and captures establish their stated paths, not native-device acceptance. Reference PNGs and HTML remain inspiration only; none substitutes for genuine trades/production, fingerprints or the explicitly recorded input checks.
