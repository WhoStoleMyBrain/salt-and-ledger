# Approved Maritime Command UI

The playable archipelago follows the approved [Salt & Ledger Figma UX redesign](https://www.figma.com/design/YhPqWIwZSE2JW7fAttB8Ac/Salt---Ledger---UX-Redesign): a persistent left navigation rail, live nautical chart, right context inspector and bottom activity strip. Harbor, Industry, Route V2 and Warehouse V2 are working views over the same simulation. Dark sea surfaces hold navigation and geography; parchment surfaces hold decisions and exact numbers. Symbols identify actual goods and production rather than supplying fictional game data.

This document describes component and interaction contracts. Figma defines the Task 3.0 layout and workflows; the original painterly Mediterranean artwork in `Resources/Maritime` defines the game's visual identity. The [asset record](design/task-3.0/ASSETS.md) identifies retained secondary Figma exports. Design screenshots and asset contact sheets do not constitute evidence of a running Unity game. Historical verification records describe their own implementation versions.

## Surface hierarchy and player flow

| Surface | First information | Working destination |
| --- | --- | --- |
| Global bar | Day/deadline, spendable company cash, reference Company Value, goal cue and time controls | Real Journal, explicit Play/Pause/Next day and pause menu |
| Navigation rail | One active Map, Harbor, Industry, Fleet, Economy or Goals destination | Switch workspace while keeping the relevant town and owned ship |
| Map | Real seeded positions, town names/roles, distinct owned/NPC ships and contextual routes | Select a town or ship; Clear selection restores the unselected chart |
| Town inspector | Name/role, public production, population, prosperity, household needs and current blockers | Harbor, Industry and Economy for that town |
| Ship inspector | Dock/voyage, actual cargo/capacity, automation and next instruction | Docked Harbor, owned Route Planner, Fleet or inspection details |
| Activity strip | Current food constraint, actual fleet state and commission cue | Relevant workspace, current notices and operational log |
| Harbor | Paired actual inventories, commodity symbols, current marginal quotes and conditional 12-day flows | Immediate directional orders; selected-good exact calculations, breakdown and town comparison |
| Industry | Public/private facility overview, eligible construction and selected factory recipe/readiness | Build, manage Enabled/rate/priority, buy missing inputs and open the shared warehouse rules |
| Fleet | Owned ship list, the same map, selected ship's actual route and next instruction | Existing planner, docked Harbor and Company/shipwright; committed cursor/budget in disclosure |
| Economy | Next Bread opportunity and shortfalls, separate accounts, population/prosperity and current batch readiness | Food and production breakdowns, full City Economy/pricing, Ledger, Harbor and private-facility management |
| Objectives | Exact current/target values, Current/Cumulative markers and authoritative completion bars | One Details & blockers foldout per card; original goals/results navigation |

An unselected new run hides the right inspector. Selecting a town or ship reveals the relevant context; Clear selection hides it again without changing the world. Map is the return point from Fleet/Economy. Harbor and Industry occupy the chart work area and return to their originating context. Their navigation entries offer a town chooser when there is no selection.

The representative loop is **Map → town → Harbor → Industry → Fleet/Economy → Map**. Select the processing town, compare its public or owned input shortage and next opportunity, then use Harbor to make a legitimate transaction. Return to inspect the newly committed stock. Industry keeps actual recipes and shared-stock policies; buying an input does not promise production if payroll, capacity or earlier facilities can still block it. Fleet follows the selected owned ship into the existing planner and back. Economy distinguishes a physical Bread shortfall from affordability at current quotes, then offers detailed accounting one level deeper.

Harbor has three physical pairs: **Market ↔ Ship**, **Market ↔ Store** and **Store ↔ Ship**, where Store is the company warehouse. Each row has actual left/right stock, one commodity button and directional order buttons. The global quantity field and shortcuts change the proposed amount; an arrow executes that row's order immediately. Paid arrows show the current first marginal bid/ask, while their tooltips retain the exact bulk payment or gross/fee/net proceeds. Selecting the commodity opens the same information as accessible details, together with the conditional economic breakdown and cross-town comparison.

The details' information scope remains independent of the order pair. Town market includes public facilities and household/municipal needs; Company warehouse includes the selected company's local industrial recipes. Changing this scope never redirects an order. Compare towns retains the good selection and uses actual public stock, marginal quotes, buyer cash and current-dock travel durations. A new inspected town never changes a ship's physical location.

Use the same physical nouns throughout: **Town market**, **Company warehouse**, **Ship cargo**. Paid actions follow Market → Ship, Ship → Market, Market → Warehouse and Warehouse → Market; no-cash actions use Load: Warehouse → Ship and Unload: Ship → Warehouse. Full exact arithmetic, actual financial counters, route state and production blockers remain available in named disclosures. Setup keeps starting assets, cash floor and deadline visible; the Journal keeps condition values and reversible/cumulative meaning visible while moving repeated explanation and full facility identifiers into Details.

The activity strip's **View log** opens operational details, with activity, identity, pricing, Ledger, Company and help destinations. Player automation uses the Fleet itinerary planner; retired two-port player editors are removed. See [Harbor](HARBOR.md), [automation](AUTOMATION.md) and the [player guide](PLAYER_GUIDE.md).

Route V2 has Course, Orders, Review and Applied states. Plot two to four actual towns, reorder or remove stops, then use a selected stop's **Sell / Buy / Unload / Load** grid. A commodity/action cell opens one inline editor; **Save order** updates only the draft and Cancel keeps the previous draft instruction. The ordered list preserves and exposes exact execution order, including duplicates. New orders retain the established disposal-first defaults. **Review route → Apply route** assigns the validated plan and leaves execution paused; Start route is separate. Current hold and free space are actual state, never a predicted arrival manifest. Future warehouse warnings remain distinct from structural validation and do not prohibit application.

## Ownership, input and return contracts

`ArchipelagoLabSession` remains the sole host-facing selection and command owner. Its selected port, inspected ship and owned command ship retain their existing meanings. `PresentationContext` tracks presentation mode and deliberate selection. Screen/overlay parent state handles returns. No economic quantity, route cursor or save field lives in this presentation context.

- Mode navigation and management overlays pause playback. Returning from those views does not resume playback or enable an automation policy. Explicit time controls remain the only source of requested economic days.
- Map selection, Clear selection, Fit map, the Routes overlay, hover, focus and pure projections do not issue world commands. A visual ship interpolation never changes the authoritative dock or voyage.
- Harbor retains the selected ship and town. NPC inspection is read-only; a remote hold is information, not permission to trade or transfer. Company stock uses the actual local store and can trade remotely under the existing rules.
- Fleet lists owned ships separately. It keeps the selected owned ship through Map, Harbor and planner returns. NPC markers remain inspectable; their automation is not editable.
- Escape/NavigationCancel closes the active Harbor/Industry overlay first, then follows the existing secondary-screen return path. Fleet/Economy/choosers return to Map; ordinary Map opens pause. Confirmation keeps its existing priority and destructive-progress safeguards.
- UI Toolkit buttons own pointer and keyboard picking. Decorative glyphs and child text ignore picking. Opening a management view records return focus, blocks the background map and time controls, and focuses Close. The navigation rail remains available for deliberate workspace changes. Closing restores valid originating focus without an economic operation; a missing source falls back to the active navigation entry.
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

`IllustratedMaritime.uss` supplies the shared semantic theme; `ArchipelagoApprovedUX.uss` applies the approved palette and shell under `sl-theme`/`ux-shell`. `ArchipelagoLab.uss` remains the source of inherited text/layout size tokens. Scoped Industry and Route styles reuse this language without changing economic state.

| Token | HEX | Use |
| --- | --- | --- |
| `--sl-sea-900` | `#183C44` | Navigation and maritime framing |
| `--sl-sea-700` | `#2F6C73` | Ocean accent and hover/active navigation |
| `--sl-navy-950` | `#122632` | Compact HUD and dark framing |
| `--sl-paper-100` | `#F7F3E8` | Quiet cards, fields and highlighted paper |
| `--sl-paper-200` | `#EAE5D8` | Parchment workspaces |
| `--sl-paper-300` | `#E1DCCF` | Table headers and paper separation |
| `--sl-ink-900` | `#253640` | Paper text and precise numbers |
| `--sl-gold-500` | `#C4A36A` | Restrained trim and art accents |
| `--sl-gold-ink` | `#775527` | Readable small category labels on paper |
| `--sl-green-700` | `#386E59` | Primary/selected/ready treatment |
| `--sl-rust-600` | `#A45139` | Warning trim, artwork and danger fill |
| `--sl-rust-ink` | `#93452F` | Readable small warning text on paper |
| `--sl-border-300` | `#C6C9BB` | Quiet paper boundaries |
| `--sl-muted-600` | `#53646A` | Readable secondary paper labels |
| `--sl-paper-selection` | `#D2E0D2` | Selected goods and calm status rows |
| `--sl-paper-warning` | `#F1DFC4` | Grounded warning rows |
| `--sl-focus-light` | `#FFE6A6` | Focus against dark chrome |

Secondary ink is darkened from the reference's pale muted text so small paper labels remain readable. Verify contrast against each actual backing surface, including selections and warnings; no earlier palette's numerical contrast claim establishes the new rendered result. Actual Editor/player screenshot review remains necessary for clipping, icon recognition, disabled controls and keyboard focus.

Shared spacing tokens are 4/8/12/18px. Gold is trim, not vital pale numerical text. Calm/manual/ready states do not receive rust styling; `sl-warning` is attached only when a current fact warrants it. Warnings retain a reason or symbol as well as color.

## Typography and reusable components

The existing readable runtime font remains in use, with no new font file or license dependency. Heading hierarchy comes from weight, size and spacing, following the approved functional typography.

| Scale | Body | Caption | Emphasis | Page heading | Minimum standard control |
| --- | ---: | ---: | ---: | ---: | ---: |
| Compact | 14px | 12px | 16px | 24px | 32px |
| Default | 17px | 15px | 20px | 28px | 37px |
| Large | 21px | 19px | 24px | 34px | 43px |
| Extra Large | 25px | 23px | 29px | 40px | 49px |

These are inherited glyph/layout sizes, not a transform. Larger layouts wrap inspector actions and retain internal scrolling in detailed views. Text preference alone persists in PlayerPrefs, independently of run saves.

| Component/classes | Contract |
| --- | --- |
| `sl-hud`, `sl-mode-nav` | Dark compact bar and one clear active mode; actual navigation buttons |
| `sl-paper`, `sl-card` | Warm matte surface with ink text and restrained boundaries |
| `sl-header`, `sl-heading`, `sl-section-title`, `sl-group-label` | Shared context/page/group hierarchy |
| `sl-metric`, `sl-primary-value` | Label, actual quantity/unit and optional grounded comparison |
| `sl-problem`, `sl-warning`, `sl-calm` | Concise current reason; calm by default, warning only when appropriate |
| `sl-goods-row`, `sl-recipe`, `sl-recipe-good` | Recognizable good symbol plus text; exact canonical input/output identity |
| `sl-primary`, `sl-secondary`, `sl-danger`, `sl-selected` | Consistent command and selection treatments |
| `MaritimeGlyph`, `MaritimeArtwork` | Original seven commodity illustrations, ship and Bakery art, fitted without distortion and paired with labels; authored vectors and unmatched industry symbols supply fallbacks |
| `ux-inspector`, `ux-activity`, `ux-trade-row` | Selected context, current attention cards and paired physical inventory rows |
| `industry-v3-*`, `route-v3-*` | Scoped factory/warehouse and route workflows using the shared maritime palette and text scale |
| `MaritimeIllustration`, `sl-illustration-small`, `sl-illustration-banner` | Restrained decorative harbor/workshop/ship tile; never a building census or economic state |

Buttons retain hover, pressed, keyboard focus and disabled states. Dark chrome uses a light focus border; paper actions use an ink border. Fields have an explicit focused border, readable input text and matching popup/toggle styling. Semantic state never depends solely on color. Redesigned controls still bind the existing exact gross/fee/net previews, production commands and ordered itinerary engine.

## Artwork and asset ownership

The seven goods are **Logs** (round bark logs), **Planks** (milled boards), **Grain** (sheaf), **Flour** (sack), **Bread** (scored loaf), **Furniture** (chair) and **Silver** (ingot). Ship, workshop, balance scales, coins, warehouse, warning and information symbols share their palette and outline language. Map landmarks add food-source, forest, processing and coastal identity.

The shared glyph renderer prefers original `Maritime` artwork, then existing project-authored vectors. `ApprovedMaritimeAssets` supplies only unmatched compact industry symbols (Lumber Camp, Sawmill, Furniture Workshop and the generic natural source; Bakery falls back there only if its original texture is missing). Its commodity exports remain available but are not the default rendering path. All sources and import settings are preserved; no runtime URL fetching occurs. See the [Figma export provenance](design/task-3.0/ASSETS.md) and [restored artwork verification](TASK-3.0a-VERIFICATION.md).

Map islands reuse bounded harbor/forest illustrations without moving labels or hit targets. Harbor and Bakery headings use small original illustrations; the Bakery image appears only for that facility type. Narrow layouts omit these decorative headings to protect control space. Fleet retains its scrollable ship illustration. Ship markers reuse the original ship glyph beside their compact P/N labels; arrows and status retain authored utility symbols; navigation labels remain unchanged. Decorative elements ignore pointer picking and never represent a census or forecast.

Existing user-supplied illustrations and source-native fallback symbols retain their [previous asset provenance](design/task-2.5/PRODUCTION-ASSETS.md). No mockup screenshot is a runtime panel. The map remains live geography rather than a pasted background. Decorative vignettes are not a building census or new ship type.

## Map viewport contract

`MapViewport` is a UI-only, aspect-preserving transform. `Fit(worldBounds, viewportBounds, padding)` accepts actual geographic bounds and viewport-local bounds; padding is left/top/right/bottom. It rejects invalid non-finite inputs, expands a degenerate world dimension to one, and proportionally limits opposing padding to 80% of the corresponding viewport dimension. The result exposes world/content bounds, centers and one scale.

`WorldToViewport` and `ViewportToWorld` are inverse operations over that same transform. `ArchipelagoMap` obtains bounds from the real ports and refreshes the transform when layout/marker dimensions change. Port markers, ship interpolation, route lines/captions, art anchors and setup preview use that same map implementation. Docked marker groups use presentation berth offsets converted through the inverse; they do not change the actual port position or sailing distance. UI Toolkit buttons perform picking, so there is no competing custom hit-test coordinate system.

Crowded town labels and ship buttons use bounded screen-space callouts. Ships are placed in stable player-first/ordinal order; a displaced rectangle retains a leader and dot at its true chart anchor. Route captions avoid those displayed rectangles. Callout placement never changes the interpolated world point, seeded geography, voyage progress or save data. This keeps the early seed-99 P1/N3 crossing individually selectable at Large text without inventing a different voyage.

Default fit-all is the only camera mode. The exposed centers/scale/inverse are the extension point for later camera work; this milestone has no zoom, panning or minimap controls and stores no camera data in a save.

The unselected chart does not display six permanent distance captions. Selecting a town shows its connected routes; selecting a ship shows its actual configured route or voyage. A modern manually controlled ship with no assigned itinerary does not expose a compatibility policy as a fictitious route. **Routes** explicitly reveals the connections. Durations still come from authoritative `SeaRoute.DurationDays` for the selected ship's speed. Ordered itinerary overlays follow the actual stop sequence.

## Implementation and verification boundaries

Industry's overview distinguishes public facilities, owned sites and actual eligible construction. Selecting a private facility opens its recipe, input stock, wage, lifecycle and current blocker beside Enabled, rate and priority. **Apply changes** commits those factory settings together; Cancel restores the applied settings. A missing-input action opens **Market ↔ Store** with the relevant good and quantity, then returns to that factory. Purchasing ingredients does not run a batch or solve an unrelated wage/storage blocker.

Warehouse V2's Buy/Sell tabs expose one shared company/town policy. Completing a valid field edit or toggling a valid rule applies it immediately; invalid input stays highlighted and leaves the previous policy active. Editing another commodity or changing tabs never silently erases the opposite side's settings. The cash-reserve popup edits the same purchase reserve from either tab: it does not invent a sale reserve or protect production wages. Configuration autosave is not a local run-slot write, immediate trade or day advance. Save the run separately. Automatic proposals and production signals come from snapshots, never UI pricing or schedule arithmetic.

The source boundaries are `ArchipelagoApprovedUX.cs` (approved shell), `ArchipelagoApprovedTrading.cs` (paired orders), `IndustryApprovedUX.cs` / `ArchipelagoWarehouseAutomation.cs` (factory detail and valid-policy autosave), and `RouteApprovedUX.cs` (Course/Orders/Review). They compose `ArchipelagoCommandDeck.cs`, `ArchipelagoWorkspaces.cs`, the existing itinerary draft/evaluator, and `ArchipelagoCommodityIntelligence.cs`. `EconomicSignals.cs` / `CommodityFlowSignals.cs` remain pure projections; `ArchipelagoMap.cs` / `MapViewport.cs` remain the sole chart projection. Simulation, save schema and economic execution stay unchanged.

Focused tests exercise bound arrow totals and all six physical flows, stale proposals, actual docks, manual/NPC restrictions, second-ship selection, input-shortage purchases, factory settings, warehouse autosave/validation, route review/execution and exact save continuation. Navigation and text-scale tests compare pre/post snapshots and serialized state. Required visual checks use actual Unity at 1366×768 and 1920×1080 with supported text scales; automated source compilation or Figma renders alone do not verify those interactions.

The rejected Trading D design, speculative five-input factory and archived Warehouse Review/Apply designs are not part of the current interface. Camera zoom/pan, bespoke town paintings and economic forecasting remain outside this milestone. No placeholder future-mode control is added.
