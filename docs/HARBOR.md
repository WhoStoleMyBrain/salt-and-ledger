# Harbor and company town stock

New Run starts with Expanding Economy v3 and the Grand Commission. Inspect the full seeded map before drafting; valid seed edits change the preview, while invalid text leaves the last valid map intact. Sandbox remains a separate explicit choice. Loading or retrying preserves the saved run's original rules.

## One harbor work surface

Open Harbor from the town's visible action or by selecting a ship and right-clicking a town. **Inspect** selects Town market or Company warehouse; **Trade with** separately chooses Ship cargo or Company warehouse for the order. Changing what you inspect does not change the physical order destination. A remote ship is visible for information but cannot trade or transfer at this town. Company stock can trade locally while ships are away. NPC cargo remains read-only.

- **Buy / Sell** follows Market → Ship or Ship → Market, or Market → Warehouse or Warehouse → Market for company stock. Ask is the company's buying price; bid is what the town pays the company. Read the exact order amount and gross/fee/net sale preview; expand the calculation for marginal endpoints and display-only average.
- **Load / Unload** follows Warehouse → Ship or Ship → Warehouse at the actual dock. No cash changes and no economic day passes. Material FIFO slices retain their acquisition intervals.
- Ship orders use whole cargo units. Local stock purchases/sales can use fractional quantities. Presets edit a proposal; they do not execute it. Confirming uses the current authoritative snapshot, so a stale proposal must be refreshed.
- Missing inputs, wage funds, storage, market liquidity, price limits, automation and construction each require a different remedy. Buying inputs cannot solve payroll shortage; household wealth is not the same account as market purchasing funds.

Industry remains the place to construct facilities and control their individual production/procurement/sales policies. Each local facility uses the same company stock.

New v3 runs also link the selected owned ship to [Fleet → Route planner](AUTOMATION.md#ordered-cargo-itineraries). Plan ordered market buys/sales and company-stock loads/unloads through two to four actual towns while retaining mixed cargo. Harbor still handles ordinary manual transactions; choose **Manual control** before submitting ship orders. Pausing the route retains its cursor, voyage and cargo. Planner **Inspect harbor** returns to this same transaction surface; remote information never grants transfer permission.

## Read a commodity's local role

Every commodity row keeps its artwork and shows **Produces**, **Uses**, **Makes + uses** or **Trade only**, current scoped stock, local ship cargo, current market bid/ask, and signed production, use and net. The common window is **the next 12 economic days**, from tomorrow through day +12 inclusive. Quantities retain fractional precision. Quotes remain public market prices in either inspection scope.

| Inspect scope | Production | Inputs and needs | Excluded |
| --- | --- | --- | --- |
| Town market | Public facilities | Public recipe inputs, eligible household needs and municipal own-use | Private output and private recipe inputs |
| Company warehouse | Your operational local facilities | Those facilities' recipe inputs | Public production, households and other owners |

The production and input totals assume each scheduled enabled batch succeeds. Disabled and pending sites appear in the selected good's **breakdown** as installed capability but do not contribute to enabled totals. **Blocked** identifies a current input/payroll/storage/output limit; it does not erase the installed recipe. Overdue work gets one next-day opportunity followed by the normal interval, never catch-up output. Ready stock/funds are not reserved: earlier facilities can use shared inputs first.

Household needs hold current population and luxury eligibility fixed and carry the exact saved demand remainder through the window. Need is not a completed purchase. The breakdown separately shows the next current-state stock/affordability preview and **Last actual** consumed, desired, paid and unmet amounts; before an observation it says so. Input usage and household need are additive when both use a good. Municipal own-use remains a separate consumption rule with no retail payment.

**Net = enabled potential output − recipe inputs − consumption needs.** It indicates a conditional local dependency or surplus, not future stock or guaranteed profit. The window excludes market trades, automatic procurement/sales, ship transfers/voyages, contracts and Mint operations. **Stock < need** compares current stock with gross window needs; intervening production or imports may cover the gap. Eligible construction stays in Industry.

## Compare possible destinations

Select a commodity and open **compare towns**. Each town shows current public stock, actual marginal bid/ask, finite market buying power and travel days from the selected ship's current dock when applicable. A row opens that town's Harbor while keeping the commodity selected. Comparison is read-only and never permits remote ship trading.

An ask/bid spread is only an opportunity to investigate. Bulk order totals change along inventory edges, ordinary sales pay handling fees, a buyer may lack the gross payment, and conditions can change during a real voyage. Use the actual trade preview at the destination to determine an executable amount. No future price or profit is promised.

## Shared stock rules, version 2

One company owns one physical store and one FIFO account at each town where it has a private facility. Public market stock, other companies' stocks, other towns and ship holds remain separate. A warehouse adapter validates the named facility's owner and port before resolving this store; industrial shipping uses the same adapter and real voyages.

Capacity is the checked sum of local owned facilities' authored warehouse capacities. Granted and paid pending sites contribute storage immediately, matching the existing permission to stock a construction site. Pending facilities still skip automatic procurement, production and sales, including on their completion day. They first become production-eligible one full interval after completion. No facility means no free company warehouse.

The shared output ceiling for a recipe is the sum of the output limits of operational local facilities with that exact recipe. Installed but disabled facilities still contribute capacity; pending facilities do not contribute output ceilings. Multiple Mills therefore provide additional throughput and output headroom. A recipe without an output ceiling still respects finite aggregate warehouse capacity.

Private phases retain ordinal port/facility order. Every facility reads the stock left by preceding operations. When two facilities need scarce inputs, the first eligible facility can consume them; only successful complete batches pay wages. A downstream producer can consume an earlier batch immediately if it runs later in that phase; otherwise it waits for its next daily attempt. There is no automatic reordering, catch-up production or special input reservation.

Procurement targets and sales reserves refer to shared stock. Policies still perform at most one transaction per configured good per daily phase. They can compete for cash/materials, and an output sales policy can sell material another facility needs. New construction keeps its existing disabled procurement/sales defaults, so the player explicitly chooses those policies.

Facilities retain separate IDs, construction states, controls, deadlines, batches, input/output counters, wages and policy transaction totals. Pooled facility snapshots carry only a stockpile reference and zero local holdings. Production events identify both the real material store and producing facility. Company Value counts shared goods once, completed/pending capital once, and never adds FIFO basis a second time. Wages remain an expense separate from material basis.

## Identity and saves

Current runs use storage2, itinerary1 and economic rules1 with schema5. The codec validates one owned pool per company/town, derived capacity, zero duplicate facility inventory, FIFO agreement and itinerary progress before replacing a session. Retry preserves the original identity. See [save policy](SAVES.md).

## Completion and diagnostic export

The authoritative winning operation freezes the economy immediately. The host acknowledges completion and offers **View results** without requiring another day or a timed animation. The frozen map, Journal and save remain inspectable.

Development builds and the Editor expose **Export Run Report** on diagnostic surfaces. It creates a raw portable save plus a human-readable report under `Application.persistentDataPath/Exports`, displaying the actual paths. Unique safe filenames prevent accidental replacement; size limits bound output. Export neither changes the world nor uses a save slot.

Reports separate markets, households, companies, treasury and finite outside money; list physical stores/FIFO, ships, facilities, input/payroll/output status, prices/targets and objective conditions. Recent host events are explicitly **partial bounded history**, not a complete lifetime transaction ledger. Exporting a difficult economy does not repair its finances or stock. Keep private playtest saves out of published documentation.

## Modern sale handling fees

Economic rules1 Harbor previews show the gross market payment, exact port service fee, net company receipts and local market/worker shares. The company pays2% on successful ordinary sales from either ships or local stock. Buying and private transfers are exempt. A fee is withheld from sale proceeds; no upfront seller cash is needed. Gross market affordability still applies. Fractional orders and split sales retain exact cumulative rounding. [Town and Ledger explanations](ECONOMIC_CIRCULATION_REVIEW.md#commercial-handling) keep these expenses separate from FIFO and wages.
