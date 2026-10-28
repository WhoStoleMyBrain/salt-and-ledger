# Task 2.7 verification

Baseline: `6a3ead7b8a1120151c27835fc94a7922c6915e04`. Task 2.7 changes route entry and removes the warehouse-existence planning gate. It keeps itinerary1, schema5, the existing atomic executor, quantities, prices, goals and balance.

## Requirement audit

| Requirement | Implementation and evidence |
| --- | --- |
| Complete repeating route at a glance | Selectable 2–4 stop cards show town/order, grouped goods and maxima, next town, current cursor status and missing-storage warning. |
| Several goods without action-card entry | One selected stop exposes seven illustrated commodity rows and Unload/Sell/Load/Buy quantities. Three cells configure Flour/Logs unloading and Bread loading; no Add action or good/action dropdown is needed. |
| Sensible defaults and exact advanced controls | New stops use Unload → Sell → Load → Buy, then ordinal good ID; minimum1, Skip, unrestricted prices/caps and zero reserves. Advanced limits & execution order retains all constraints, duplicate commands, ordering controls and explicit default-order reset. |
| Preserve existing instructions | Reopened/preset/custom stops retain order and ID. Duplicate cells say ordered and defer to separate advanced instructions. Tests cover exact equivalence, advanced values, transient blank/retype and cached-cell aliasing. |
| Plan before industry | Structural validation accepts absent company storage. Assignment, preview, execution and save restoration share that validation. Stop/footer warnings do not disable an otherwise safe Apply. |
| Safe missing-storage execution | Load/Unload use existing MissingCompanyStock Skip/Wait paths. Real later construction permits the same plan to execute. Tests cover both directions/shortage choices, both Ascensions and actual paid stock; no assets or money are supplied implicitly. |
| Clear operation feedback | Live dock/destination/next instruction/free capacity, stop completion count, waiting/partial/skipped labels, cargo and last-outcome/budget disclosure. Current evaluator reason is separate from last committed result. Paused travel elsewhere is off route, not incoming. |
| Safe application and manual control | Manual/dock/partial-visit/daily-budget restrictions remain. Equal reapplication preserves cursor/budget. UI tests and live off-route rejection/return exercise pause/resume. |
| Physical and financial correctness | Ordinary exact trades/transfers/voyages, capacities, mixed cargo, FIFO, rollback and finite accounts unchanged. Full .NET invariants and all three deterministic report checks pass. |
| Exact continuation and daily limits | Real isolated-slot Unity tests plus schema5 .NET cases preserve policy/cursor/budget. Eight absent-warehouse skips cannot acquire another operation through pause, reapply or load. Live mid-voyage save/load retains exact bytes. |
| Passive editing and inspection | Bound-control tests and live resolution/scale/selection/disclosure inspection compare snapshot identity and exact save bytes. Drafts never advance time or move money/goods. |
| Usability, documentation and delivery | Player guide, Automation, Mechanics, design system, roadmap and canonical help updated; generated website/offline help remain aligned. Actual Editor/player evidence and limitations recorded below. |

## Automated checks

- .NET10.0.402 locked restore using the local package cache; Release build: zero warnings/errors. **739/739 tests passed**, zero skipped.
- Grand Commission, Expanding Economy and Economic Circulation `--check` commands passed. Published experiment outputs remain unchanged.
- Content validation, generated reference freshness, **58/58 Python tests**, strict MkDocs build and Unity staging/freshness passed.
- Unity6000.6.6f1 complete suites: **63/63 Edit Mode and70/70 Play Mode passed**, zero skipped. The final focused **10/10 Play Mode** rerun passed after the multiple-cell clear/retype ordering fix. It covers restoration in either order, distinct cached identities, advanced limits, exact continuation and missing-storage construction. Earlier focused run:9/9 passed.
- Existing independent day60 headless Bread witness remains exact: cash1534.6896, Company Value2539.6896, 7 Bakery batches/14 Bread/42 wages and all-account cash11730. This is a regression, not a claim that arbitrary routes profit.

## Actual Unity walkthrough

The live Editor used seed99, Merchant Company, StockOnly, Ascension0, Sandbox. Input used frame-spaced UI Toolkit keyboard/navigation and pointer/dropdown events against the running game. Economic state was observed through read-only saves; no economic APIs, injected inventories or fixture state were used to drive the journey.

1. From Fleet, enter three stops through the commodity grid: Westhaven unload2 Flour/unload2 Logs/load4 Bread; Southhaven sell4 Bread/buy4 Grain/buy2 Logs; Northport sell4 Grain/buy2 Flour. Set purchase reserve100 and per-order cap200 under Advanced. All eight commands are visible in the overview. Drafting leaves exact save bytes unchanged.
2. Apply on day0 at Southhaven **before any warehouse or facility exists**. The missing-Westhaven-warehouse warning remains visible. Build the Westhaven Bakery through Industry for300 coins; the already assigned policy remains byte-for-byte equivalent.
3. Manually sail to Westhaven, advance to day4, and try Resume. It correctly refuses: the retained next stop is Southhaven. Sail back through the ordinary Harbor controls, arrive day7 and enable the unchanged route. Day8 buys4 Grain/2 Logs and departs for Northport. This also exercises safe manual detour recovery without resetting the cursor.
4. Inspect cards, grid, advanced limits and current state at1366×768 and1920×1080, Default/Large. Exact saves remain unchanged. Save/load day8 mid-voyage in a backed-up local slot.
5. Advance ordinary Next day controls through day60. The ship physically moves Grain to processing, carries Flour/Logs to the owned Bakery and sells its Bread at Southhaven. The final policy still equals the one applied before construction.

| Day60 observed result | Amount |
| --- | ---: |
| Company cash | 1541.6896 coins |
| Inventory purchases / gross sales | 208.6664 /292.2 coins |
| Paid capital / wages / handling fees | 300 /36 /5.844 coins |
| Grain bought and sold | 16 each |
| Flour and Logs bought | 8 each |
| Private Bakery output | 6 batches /12 Bread |
| Bread sold | 8 |
| Remaining warehouse / cargo | 4 Bread /2 Flour +2 Logs |
| All-account money | 11730 coins, no Mint issuance or external inflow |

The manual detour and starting cursor differ from the older headless witness, so their day60 batch counts and cash differ. Neither is a profitability guarantee. At day60 the real ship has just reached Westhaven with new inputs; it has not yet performed its next unloading visit.

## Visual and platform evidence

Actual running-game captures are curated under `images/task-2.7/`. They use existing maritime artwork; no new asset or licensing dependency was introduced. At1366×768 Large, the seven-row grid remains readable in the existing vertical scroll with a fixed action footer. The route overview and detailed grid require scrolling between them on smaller displays.

The Windows Mono development build succeeded. Its **Continue** button restored the Editor's day8 mid-voyage save byte-for-byte. After the same ordinary Next day inputs through day60, the complete Editor and player saves were identical: SHA256 `48dc2ece1d0cb5cc4aa7374eadd1502949f8792c2c16a952522443d6070a9c1b`. This includes policy, progress, daily budget, physical stores, cash and FIFO. Actual player keyboard events also cleared both input quantities and retyped them in reverse order without changing the original policy or economic save. Four-stop drafting/reload remained passive.

| Running view | Editor | Windows Mono |
| --- | --- | --- |
| Grid1366×768 Default | [Capture](images/task-2.7/editor-grid-1366-default.png) | [Capture](images/task-2.7/player-grid-1366-default.png) |
| Grid1366×768 Large | [Capture](images/task-2.7/editor-grid-1366-large.png) | [Capture](images/task-2.7/player-grid-1366-large.png) |
| Grid1920×1080 Default | [Capture](images/task-2.7/editor-grid-1920-default.png) | [Capture](images/task-2.7/player-grid-1920-default.png) |
| Grid1920×1080 Large | [Capture](images/task-2.7/editor-grid-1920-large.png) | [Capture](images/task-2.7/player-grid-1920-large.png) |
| Complete route overview | [Three stops,1366 Large](images/task-2.7/editor-planner-1366-large.png) | [Four-stop draft,1366 Large](images/task-2.7/player-four-stop-draft-1366-large.png) |
| Planning before construction | [Valid warning before Apply](images/task-2.7/editor-planning-before-warehouse.png) | [Day60 actual cargo/cursor](images/task-2.7/player-day60-operations.png) |
| Safe off-route control | [Paused with retained next stop](images/task-2.7/editor-paused-off-route.png) | [Three stops,1920 Large](images/task-2.7/player-planner-1920-large.png) |

The player exited normally with no managed gameplay exceptions. It retained the pre-existing native startup warnings about `AMDUnityPlugin` and `NVUnityPlugin` architecture; this task does not claim to resolve those platform warnings. The test player was closed, the Editor stopped, temporary QA overrides/build assets removed, and all three original save slots plus four build-sensitive project files restored byte-for-byte with original modification times. Unrelated pre-existing repository files were left untouched. No Pages publication occurred.

## Limits and remaining UX work

- Automated events and screenshots establish bound-control behavior and rendered layout, not an independent human usability study or native-device input coverage.
- The engine retains only bounded current cursor and last outcome. Cards do not invent a history of every earlier stop or promise future prices, fills, profits or warehouse availability.
- Repeated good/action commands remain an advanced case. They are preserved exactly rather than collapsed into a misleading single quantity.
- Selection/advanced disclosures and scroll position are presentation state, not new save fields. Economic continuation remains exact.
