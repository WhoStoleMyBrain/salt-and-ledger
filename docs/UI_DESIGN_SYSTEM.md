# Illustrated Maritime Ledger UI

The playable archipelago uses a nautical chart, a compact contextual deck and focused management views. Task 2.6 adds commodity-level economic visibility and progressive disclosure to the Task 2.5 maritime kit. Dark sea surfaces hold navigation and geography; calm parchment surfaces hold decisions and exact numbers. Illustration identifies goods, ships and production rather than supplying fictional game data.

This document describes the implemented component and interaction contracts. The [verification record](REPOSITORY-CLEANUP.md) separates actual Editor/player captures and operation tests from unsupported native-device checks; the concept sheets remain inspiration only.

## Surface hierarchy and player flow

| Surface | First information | Working destination |
| --- | --- | --- |
| Global bar | Day/deadline, spendable company cash, reference Company Value, goal cue, notices, time controls, menu | Real Journal, current notices, explicit Play/Pause/Next day and pause menu |
| Map | Real seeded positions, town names/roles, distinct owned/NPC ships and contextual routes | Select a town or ship; the bottom deck opens without a world command |
| Town deck | Name/short role, three compact facts, one prioritized current reason | Harbor, Industry and Economy for that town |
| Ship deck | Dock/voyage, hold occupancy, manual/automation state and current instruction | Docked Harbor, owned Route Planner, Fleet or inspection details |
| Trade | Scoped commodity stock, local role, conditional 12-day production/use/net and public bid/ask | Selected-good breakdown and cross-town comparison; exact paid Buy/Sell or docked no-cash Load/Unload |
| Industry | Actual facility/recipe, construction, current readiness, stock and policy | Existing construction, warehouse and per-facility controls |
| Fleet | Owned ship list, the same map, selected ship's actual route and next instruction | Existing planner, docked Harbor and Company/shipwright; committed cursor/budget in disclosure |
| Economy | Next Bread opportunity and shortfalls, separate accounts, population/prosperity and current batch readiness | Food and production breakdowns, full City Economy/pricing, Ledger, Harbor and private-facility management |
| Objectives | Exact current/target values, Current/Cumulative markers and authoritative completion bars | One Details & blockers foldout per card; original goals/results navigation |

An unselected new run has a quiet collapsed deck. A deliberate selection opens it; Collapse preserves that context and Expand restores it. Map is the return point from Fleet/Economy. Trade and Industry are existing overlays over the originating workspace, so closing them restores that context. Without a selection, their mode buttons open a real town chooser.

The representative loop is **Map → town → Harbor → Industry → Fleet/Economy → Map**. Select the processing town, compare its public or owned input shortage and next opportunity, then use Harbor to make a legitimate transaction. Return to inspect the newly committed stock. Industry keeps actual recipes and shared-stock policies; buying an input does not promise production if payroll, capacity or earlier facilities can still block it. Fleet follows the selected owned ship into the existing planner and back. Economy distinguishes a physical Bread shortfall from affordability at current quotes, then offers detailed accounting one level deeper.

Harbor's **Inspect** scope and **Trade with** destination are independent. Town market includes only public production and household/municipal needs; Company warehouse includes only the selected company's local industrial stock and recipes. The selected good's breakdown and comparison are keyboard-accessible foldouts, with hover text as a supplement. Compare towns retains the good selection and uses actual public stock, marginal quotes, buyer cash and current-dock travel durations. A new town selection changes inspection context, never the ship's physical location.

Use the same physical nouns throughout: **Town market**, **Company warehouse**, **Ship cargo**. Paid actions follow Market → Ship, Ship → Market, Market → Warehouse and Warehouse → Market; no-cash actions use Load: Warehouse → Ship and Unload: Ship → Warehouse. Full exact arithmetic, actual financial counters, route state and production blockers remain available in named disclosures. Setup keeps starting assets, cash floor and deadline visible; the Journal keeps condition values and reversible/cumulative meaning visible while moving repeated explanation and full facility identifiers into Details.

The operational inspector remains available through **Details / log**, with activity, identity, pricing, Ledger, Company and help destinations. Player automation uses the Fleet itinerary planner; retired two-port player editors are removed. See [Harbor](HARBOR.md), [automation](AUTOMATION.md) and the [player guide](PLAYER_GUIDE.md).

Task 2.7's Route Planner keeps the full repeating route visible as compact selectable stop cards. A single selected-stop grid pairs existing commodity art with Unload/Sell/Load/Buy quantity fields. Advanced limits and exact command order are a disclosure, not the primary entry method. New stops use disposal-first ordering; reopened, preset and custom-ordered stops retain exact instructions, including duplicates. A duplicate cell links the user to its ordered instructions rather than pretending it is one combined order. Future warehouse warnings are separate from structural validation and do not prohibit Apply. Current cursor, cargo/free capacity and last committed outcome are distinct from passive draft summaries.

## Ownership, input and return contracts

`ArchipelagoLabSession` remains the sole host-facing selection and command owner. Its selected port, inspected ship and owned command ship retain their existing meanings. `PresentationContext` stores only the active command mode, whether a deliberate selection exists and deck collapse. Existing screen/overlay parent state handles returns. No economic quantity, route cursor or save field lives in this presentation context.

- Mode navigation and management overlays pause playback. Returning from those views does not resume playback or enable an automation policy. Explicit time controls remain the only source of requested economic days.
- Map selection, the Routes overlay, hover, focus, deck collapse and pure projections do not issue world commands. A visual ship interpolation never changes the authoritative dock or voyage.
- Harbor retains the selected ship and town. NPC inspection is read-only; a remote hold is information, not permission to trade or transfer. Company stock uses the actual local store and can trade remotely under the existing rules.
- Fleet lists owned ships separately. It keeps the selected owned ship through Map, Harbor and planner returns. NPC markers remain inspectable; their automation is not editable.
- Escape/NavigationCancel closes the active Harbor/Industry overlay first, then follows the existing secondary-screen return path. Fleet/Economy/choosers return to Map; ordinary Map opens pause. Confirmation keeps its existing priority and destructive-progress safeguards.
- UI Toolkit buttons own pointer and keyboard picking. Decorative glyphs and child text ignore picking. Modal opening records return focus, disables the underlying map/deck/navigation, and focuses Close. Closing restores focus without an economic operation.
- Stable fleet/market rows update bound values. Economy cards refresh on committed snapshot or selected-town change, rather than rebuilding every frame. Internal detail scrolling is separate from the full-screen layout.

The supported host uses current v3 households and pooled company warehouses. Only current schema6 saves load; obsolete formats fail without replacing the active session. Terminal worlds remain frozen while their Map, Journal and results can be inspected.

## Exact facts and display precision

`EconomicSignals` is a pure portable projection over the committed `RegionalWorldSnapshot` and canonical content. It does not construct a world, advance a day, reserve material, consume random draws or replace a snapshot.

| Projection | Source and meaning | Explicit limit |
| --- | --- | --- |
| `NextEssentialNeed` | Actual schedule; current population; persisted demand remainder; canonical `PreviewDemand`; current public stock; shared retail proposal | Current quantities/quotes/funds only. Stock and affordability shortfalls are separate. Before any food observation, say Awaiting first need. |
| `PublicProduction` | Canonical recipe, actual public stock and employer cash, controls, schedule, output/numeric limits and shared batch readiness | One conditional batch; not a projection of future imports or procurement |
| `PrivateProduction` | Same canonical production rules, actual company material owner, company payroll, construction, capacity and output ceilings | Shared stock can be consumed by earlier facilities; no reservation or guaranteed completion |
| `CommodityFlow` | Scoped installed recipes, actual next eligible days/intervals and canonical demand over D+1 through D+12; current stock; separate last actual consumption | Enabled scheduled capacity and need, not future inventory. Population/eligibility fixed; fractional demand carry retained. No trade or transport counted. |
| Fleet instruction | Existing `CargoItineraryEvaluator` / `RegionalAutomation`, immutable policy and committed progress | Current reason is separate from last committed outcome; no route optimization or predicted prices |

Next eligible date, current readiness and last committed outcome answer different questions and remain separate fields. **Ready from current state** means a complete canonical batch can use today's stock/funds at the stated opportunity if intervening operations leave them available. Pending construction and disabled facilities do not appear as immediately productive. A numeric receiver limit is not mislabeled as household poverty.

Commodity rows use one fixed **12-day** window. Positive output and negative input/consumption needs are gross scheduled quantities; conditional net is their difference. Installed role remains visible when a site is disabled, pending or blocked. Only enabled operational sites contribute to the main production/input totals; blocked enabled sites retain their conditional capacity and show the blocker. Need totals carry the saved fractional remainder through canonical demand calculations. The latest actual consumption is displayed separately, with explicit absence before the first observation. Neither these signals nor a cross-town ask/bid spread forecast completed purchases, future prices or profit. Current stock below gross window need is a dependency cue, not a predicted shortage after intervening production/imports.

The HUD formats coins with invariant `0.#` precision: at most one decimal, with values strictly between zero and 0.1 shown as **<0.1**. Its tooltip preserves the exact cash/reference-value amounts. Harbor execution previews and Ledger retain existing exact precision up to four decimal places. The simulation still receives integer subunits; display formatting never supplies affordability, score, price or command amounts. Reference Company Value is explicitly distinct from spendable cash, realized earnings and liquidation proceeds.

## Shared visual tokens

`IllustratedMaritime.uss` owns the palette under the root `sl-theme` class. The original `ArchipelagoLab.uss` remains the source of inherited text/layout size tokens. New surfaces use semantic classes; existing Harbor, Industry and run cards receive shared adapters rather than independent color copies.

| Token | HEX | Use |
| --- | --- | --- |
| `--sl-sea-900` | `#103B45` | Chart and primary action surfaces |
| `--sl-sea-700` | `#2F6C73` | Ocean accent and hover/active navigation |
| `--sl-navy-950` | `#0D2631` | Compact HUD and dark framing |
| `--sl-paper-100` | `#F4ECD8` | Quiet cards, fields and highlighted paper |
| `--sl-paper-200` | `#E9DFC6` | Parchment workspaces |
| `--sl-paper-300` | `#DDD0AE` | Table headers and paper separation |
| `--sl-ink-900` | `#26343B` | Paper text and precise numbers |
| `--sl-gold-500` | `#C4A36A` | Restrained trim and art accents |
| `--sl-gold-ink` | `#775527` | Readable small category labels on paper |
| `--sl-green-700` | `#396C60` | Primary/selected/ready treatment |
| `--sl-rust-600` | `#A45139` | Warning trim, artwork and danger fill |
| `--sl-rust-ink` | `#93452F` | Readable small warning text on paper |
| `--sl-border-300` | `#B7AA8F` | Quiet paper boundaries |
| `--sl-muted-600` | `#536067` | Secondary paper labels |
| `--sl-paper-selection` | `#D2E0D2` | Selected goods and calm status rows |
| `--sl-paper-warning` | `#F1DFC4` | Grounded warning rows |
| `--sl-focus-light` | `#FFE6A6` | Focus against dark chrome |

Calculated sRGB solid-swatch contrast is **9.67:1** for ink/parchment, **4.89:1** muted/parchment, **5.09:1** dark gold/parchment, **5.05:1** warning ink/parchment, **4.54:1** green/parchment, **5.11:1** cream/primary green and **13.32:1** cream/navy. Actual screenshot review remains necessary: those ratios do not establish layout, icon recognition or rendered focus quality. The darker muted/warning text is an intentional adjustment to the reference palette.

Shared spacing tokens are 4/8/12/18px. Gold is trim, not vital pale numerical text. Calm/manual/ready states do not receive rust styling; `sl-warning` is attached only when a current fact warrants it. Warnings retain a reason or symbol as well as color.

## Typography and reusable components

The existing readable runtime font remains in use, with no new font file or license dependency. Heading hierarchy comes from weight, size and spacing. This deliberately favors legibility over reproducing the moodboard's decorative serif lettering.

| Scale | Body | Caption | Emphasis | Page heading | Minimum standard control |
| --- | ---: | ---: | ---: | ---: | ---: |
| Compact | 14px | 12px | 16px | 24px | 32px |
| Default | 17px | 15px | 20px | 28px | 37px |
| Large | 21px | 19px | 24px | 34px | 43px |
| Extra Large | 25px | 23px | 29px | 40px | 49px |

These are actual inherited glyph/layout sizes, not a transform. Larger layouts wrap the deck/actions and retain internal scrolling in detailed views. Text preference alone persists in PlayerPrefs, independently of run saves.

| Component/classes | Contract |
| --- | --- |
| `sl-hud`, `sl-mode-nav` | Dark compact bar and one clear active mode; actual navigation buttons |
| `sl-paper`, `sl-card` | Warm matte surface with ink text and restrained boundaries |
| `sl-header`, `sl-heading`, `sl-section-title`, `sl-group-label` | Shared context/page/group hierarchy |
| `sl-metric`, `sl-primary-value` | Label, actual quantity/unit and optional grounded comparison |
| `sl-problem`, `sl-warning`, `sl-calm` | Concise current reason; calm by default, warning only when appropriate |
| `sl-goods-row`, `sl-recipe`, `sl-recipe-good` | Recognizable good symbol plus text; exact canonical input/output identity |
| `sl-primary`, `sl-secondary`, `sl-danger`, `sl-selected` | Consistent command and selection treatments |
| `MaritimeGlyph`, `sl-glyph-small`, `sl-glyph`, `sl-glyph-large` | Generated commodity images and source-native shared symbols in 24/30/48px defaults, 28/34/56px for larger text layouts |
| `MaritimeIllustration`, `sl-illustration-small`, `sl-illustration-banner` | Restrained decorative harbor/workshop/ship tile; never a building census or economic state |

Buttons retain distinct hover, pressed, keyboard focus and disabled states. Dark chrome uses a light focus border; paper actions use an ink border. Fields have an explicit focused border, readable input text and matching popup/toggle styling. Semantic state never depends solely on color. The shared style adapters preserve the existing Harbor table, exact gross/fee/net previews, Industry forms and route-editor layout rather than replacing their controls.

## Artwork and asset ownership

The seven goods are **Logs** (round bark logs), **Planks** (milled boards), **Grain** (sheaf), **Flour** (sack), **Bread** (scored loaf), **Furniture** (chair) and **Silver** (ingot). Ship, workshop, balance scales, coins, warehouse, warning and information symbols share their palette and outline language. Map landmarks add food-source, forest, processing and coastal identity.

Runtime materials and illustrations include user-supplied generated raster artwork, with original Painter2D geometry retained for shared symbols and fallback drawing. Stable resource names and metadata wire the assets; no moodboard screenshot is used as a runtime panel. No external font or runtime network fetch is required. Workshop/harbor and ship images are reusable vignettes, not a building census or a new ship type. See the [production asset inventory and provenance](design/task-2.5/PRODUCTION-ASSETS.md).

The [visual reference guide](design/task-2.5/UI-REFERENCE.md) distinguishes functional wireframes, illustrated inspiration and actual running-game evidence. The map remains live geography rather than a pasted background.

## Map viewport contract

`MapViewport` is a UI-only, aspect-preserving transform. `Fit(worldBounds, viewportBounds, padding)` accepts actual geographic bounds and viewport-local bounds; padding is left/top/right/bottom. It rejects invalid non-finite inputs, expands a degenerate world dimension to one, and proportionally limits opposing padding to 80% of the corresponding viewport dimension. The result exposes world/content bounds, centers and one scale.

`WorldToViewport` and `ViewportToWorld` are inverse operations over that same transform. `ArchipelagoMap` obtains bounds from the real ports and refreshes the transform when layout/marker dimensions change. Port markers, ship interpolation, route lines/captions, art anchors and setup preview use that same map implementation. Docked marker groups use presentation berth offsets converted through the inverse; they do not change the actual port position or sailing distance. UI Toolkit buttons perform picking, so there is no competing custom hit-test coordinate system.

Crowded town labels and ship buttons use bounded screen-space callouts. Ships are placed in stable player-first/ordinal order; a displaced rectangle retains a leader and dot at its true chart anchor. Route captions avoid those displayed rectangles. Callout placement never changes the interpolated world point, seeded geography, voyage progress or save data. This keeps the early seed-99 P1/N3 crossing individually selectable at Large text without inventing a different voyage.

Default fit-all is the only camera mode. The exposed centers/scale/inverse are the extension point for later camera work; this milestone has no zoom, panning or minimap controls and stores no camera data in a save.

The unselected chart does not display six permanent distance captions. Selecting a town shows its connected routes; selecting a ship shows its actual configured route or voyage. A modern manually controlled ship with no assigned itinerary does not expose a compatibility policy as a fictitious route. **Routes** explicitly reveals the connections. Durations still come from authoritative `SeaRoute.DurationDays` for the selected ship's speed. Ordered itinerary overlays follow the actual stop sequence.

## Implementation and verification boundaries

Industry uses one existing parchment modal with sibling **Factories / Warehouse** tabs. Factory controls are enabled state, operating rate and priority; recipe art, available inputs, full wage and blocker stay alongside them. Warehouse Buy/Sell tabs use the shared seven commodity symbols, stock and signed12-day flows, compact editable thresholds and marginal prices. Both panes preserve draft values until Apply or explicit reload. Harbor opens the same warehouse view; manual stock and physical transfer controls remain available. Automatic proposals and production signals come from simulation snapshots, never UI pricing or schedule arithmetic.

The main source boundaries are `ArchipelagoCommandDeck.cs` (shell/context/navigation), `ArchipelagoWorkspaces.cs` (Fleet/Economy), `ArchipelagoCommodityIntelligence.cs` (Harbor flow/comparison binding), `ArchipelagoMaritimeKit.cs` (shared visual components), `EconomicSignals.cs` / `CommodityFlowSignals.cs` (pure current facts and conditional schedules), and `ArchipelagoMap.cs` / `MapViewport.cs` (chart projection). Existing Harbor/Industry/Route Planner partials remain the operation destinations.

Focused tests cover projections, viewport transforms, bound shell values and inspection/navigation without mutation. The [Task 2.6 verification record](TASK-2.6-VERIFICATION.md) contains the completed full suites, ordinary production/trade/route witnesses, exact save continuation and actual 1366×768 / 1920×1080 Default/Large captures. It separately records the unavailable native-device input check and remaining density/polish debts.

Deferred work remains deliberately small and explicit: polished bespoke town paintings, a licensed serif-heading option, larger-map camera controls and deeper economic forecasting are future proposals. No placeholder captain, curse, blessing or other future-mode control is added to this UI.
