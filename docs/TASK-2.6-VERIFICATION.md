# Task 2.6 verification

Task 2.6 adds read-only economic visibility and simplifies the existing Unity interface. No economic rules, balance, generated world, objective, identity, automation or persistence fields change. The starting repository revision was `423b58d`.

## Requirement audit

| Requirement | Implementation and evidence |
| --- | --- |
| Stock, prices and local role for every good | All seven Harbor rows bind stock, current market bid/ask, installed produces/uses role and signed Make/Use/Net. Existing commodity artwork is reused. |
| One consistent normalized timeframe | `EconomicSignals.CommodityFlow` uses days D+1 through D+12. Due/overdue work gets one next opportunity, then its normal interval assuming success. Tests cover deadlines, overdue work and beyond-window capability. |
| Fractional household and input needs | Gross canonical recipe quantities and scheduled household demand share the same window. Demand uses the canonical residual calculation at current population/eligibility. Tests cover fractional carry, luxury eligibility and input plus consumption totals. |
| Conditional capability versus actual behavior | Main totals include enabled operational sites; breakdown includes disabled/pending capability, inputs, wages, readiness and last actual outcomes. Household desired/consumed/payment and stock/funds misses remain separate. A ready batch is not a stock/payroll reservation or a forecast. |
| Separate public and company ownership | Scope selection binds public facilities/households or the selected company's local facilities/pool. Tests cover other owners, other towns, multiple facilities and shared stock counted once. Trade, procurement, sales and ship transfers never enter passive totals. |
| Discoverable opportunities | Selected-good comparison lists all towns' current stock, actual bids/asks, finite market buying power and travel time from the docked ship. Town selection retains the good and the input-blocking Harbor modal. No profit or future-price claim. |
| Eligible construction in Industry | Eligible types appear above construction timing details. Market rows contain commodity roles and needs, not a construction list. |
| Clear manual physical flows | Market → Ship, Ship → Market, Market → Warehouse, Warehouse → Market, Load and Unload retain the existing authoritative preview/command path. Tests check all six, fractional local trades, whole cargo, fees, cash, physical stock and FIFO held basis. |
| Concise primary surfaces with complete details | Compact Journal numerical conditions retain Current/Cumulative semantics and expandable blockers; Fleet activity and Economy calculations move to disclosures. Setup/Industry copy is shortened. Exact marginal prices, average, fee split and net receipts remain available. |
| Passive navigation | New Play scenarios compare snapshot identity and exact schema5 save bytes across scope, commodity, town, modal/Back, Journal disclosure and existing views. No simulation mutation APIs were added to the new binding. |
| Existing functionality and visual system | Existing controls, guards, policies and route editor remain. Inherited layout/text tokens and current parchment/navy/teal materials/icons are reused; no new art, resource or dependency. |
| Readability and usability walkthrough | Actual Editor and Windows Mono interaction/captures at1366x768 and1920x1080, Default/Large; details and limitations below. |

## Automated verification

- .NET SDK 10.0.402: locked restore using the local package cache; Release build with zero warnings/errors; **731/731 tests passed**, zero skipped. This includes 16 new commodity-flow cases and the existing 23 economic-signal cases.
- Grand Commission check passed: seed 99 manual and itinerary wins at day 18, seed 42 manual win at day 13, and the expected idle deadline failure.
- Expanding Economy check passed: 29 comparisons / 5,220 primary world-days. Circulation check passed: 64 rules1 A/B worlds plus 16 rules0 controls / 29,200 primary days.
- Every published experiment file retained its original SHA256. These are regression checks, not a new economic treatment or balance claim.
- Data validation, generated documentation freshness, **58 Python tests**, strict MkDocs build and Unity staging/freshness passed.
- Unity 6000.6.6f1: **63/63 Edit Mode** and **66/66 Play Mode** tests passed, zero skipped. The final Play pass includes resolved-text contrast checks (at least 4.5:1) for comparison rows and Journal percentages against both empty and filled bars. Windows Mono development build succeeded. After the final municipal own-use wording correction, the four focused scenarios passed again (4/4).

The first Unity Edit pass was interrupted by a verification-command timeout logged during a long-running test (62/63); the clean rerun passed without concurrent Editor commands. The first Play pass was 60/66: all four new scenarios passed, while six old assertions expected renamed labels or financial details in their previous location. The corrected assertions retain their numerical/permission coverage, including exact marginal endpoints, averages and fee splits in disclosure.

## Actual running-game evidence

The Editor walkthrough used seed 42, Industrial Consortium and a four-card Grand Commission drafted through the real controls. It bought/sold one Grain through ship cargo, bought 1.5/sold 0.5 Grain through the warehouse, then loaded/unloaded one Grain without payment. All six orders left the day unchanged (revision 0 to 6, cash 1200 to 1180.58). Four Next Day commands reached day 4: the Bakery had consumed 2 Flour + 2 Logs, produced 4 Bread and paid 12 wages; company cash was 1168.58.

Inspection switched public/private scope, selected Bread, opened its breakdown and four-town comparison, selected another town, closed with NavigationCancel, and visited Industry/Fleet/Economy/Journal/Map. Both requested resolutions and Default/Large were exercised. The full inspection retained exact save bytes; frame counts advanced throughout. Save and Load through slot 3 also preserved the exact state.

The first captures revealed low-contrast nested comparison values and Journal percentages. The final styles use parchment/ink comparison rows and light percentages over darker bars; the new resolved-color assertions protect these fixes. Screenshots are actual running-game captures, not mockups or altered artwork. Raw saves and logs remain in ignored `.cache/`.

A fresh Windows Mono process continued the exact Editor save, byte-for-byte. Two ordinary warehouse purchases supplied 1 Flour and 1 Logs without advancing the day; two Next Day commands then produced 2 additional Bread and paid 6 wages. The day 6 company had 1128.58 cash and the Bakery had 3 completed batches / 6 Bread / 18 wages. The player inspection/scale/navigation walkthrough again preserved exact save bytes. A final fresh Windows sandbox verified municipal Planks availability as no-payment own-use, with inspection again preserving exact save bytes.

| Actual running UI | 1366x768 | 1920x1080 |
| --- | --- | --- |
| Editor Harbor | [Default](images/task-2.6/editor-harbor-1366-default.png), [Large](images/task-2.6/editor-harbor-1366-large.png) | [Default](images/task-2.6/editor-harbor-1920-default.png), [Large](images/task-2.6/editor-harbor-1920-large.png) |
| Windows Harbor | [Default](images/task-2.6/player-harbor-1366-default.png), [Large](images/task-2.6/player-harbor-1366-large.png) | [Default](images/task-2.6/player-harbor-1920-default.png), [Large](images/task-2.6/player-harbor-1920-large.png) |
| Journal | [Editor Large](images/task-2.6/editor-objectives-1366-large.png), [Windows Large](images/task-2.6/player-objectives-1366-large.png) | [Windows Default](images/task-2.6/player-objectives-1920-default.png) |

Additional evidence: [company warehouse](images/task-2.6/editor-warehouse-1920-default.png), [Bread breakdown](images/task-2.6/editor-bread-breakdown.png), [four-town comparison](images/task-2.6/editor-bread-comparison.png), [Economy](images/task-2.6/editor-economy.png), [Windows warehouse after paid resupply](images/task-2.6/player-warehouse-1920-default.png), and the final [municipal Planks own-use breakdown](images/task-2.6/player-municipal-own-use.png).

Interaction used actual UI Toolkit pointer and keyboard/navigation events through Unity's local QA connection, not direct economic mutation APIs. This is not a human physical-input playtest. The Windows log contained the previously recorded AMDUnityPlugin/NVUnityPlugin architecture-load messages and a D3D12 debug-interface warning; no managed game exceptions occurred. The development-console overlay was closed for the layout-matrix captures, with the original log retained; the final municipal screenshot retains that known startup overlay. These platform/plugin warnings were not changed by this UI task.

All three original user save slots were restored byte-for-byte with their original timestamps. Temporary QA build settings/assets were restored; the Editor was left stopped. No user save, generated runtime staging or QA log is included in the commits.

## Scope and limitations

Twelve-day totals hold current population/eligibility fixed and assume scheduled attempts succeed. They do not simulate competition, future procurement, prices, affordability, voyages or population changes. Current stock below window need is a coverage warning, not proof that a shortage will occur. The public scope describes public production even when private goods may later be sold into that market. Cross-town quotes require real transport and an executable destination preview.

Large text may use the existing vertically stacked Harbor layout and scroll. Detailed recipes, financial arithmetic and Journal blockers remain deliberate disclosures. Factory/ship automation and the multi-stop editor were not redesigned. No GitHub Pages publication or web build was performed.
