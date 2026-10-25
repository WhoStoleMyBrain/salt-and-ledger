# Harbor and company town stock

New Run starts with Expanding Economy v3 and the Grand Commission. Inspect the full seeded map before drafting; valid seed edits change the preview, while invalid text leaves the last valid map intact. Advanced / Legacy retains Classic, seeded-v1 and Living Cities-v2. Sandbox remains a separate explicit choice. Loading or retrying preserves the saved run's original rules.

## One harbor work surface

Open Harbor from the town's visible action or by selecting a ship and right-clicking a town. Read public market stock, the selected ship's hold, and company stock together. A remote ship is visible for information but cannot trade or transfer at this town. Company stock can trade locally while ships are away. NPC cargo remains read-only.

- **Buy / Sell** exchanges goods with the public market and transfers actual company/market cash. Ask is the company's buying price; bid is what the town pays the company. The preview gives the exact whole-order amount and a display-only average.
- **Load / Unload** moves owned goods between the actual docked ship and local company stock. No cash changes and no economic day passes. Material FIFO slices retain their acquisition intervals.
- Ship orders use whole cargo units. Local stock purchases/sales can use fractional quantities. Presets edit a proposal; they do not execute it. Confirming uses the current authoritative snapshot, so a stale proposal must be refreshed.
- Missing inputs, wage funds, storage, market liquidity, price limits, automation and construction each require a different remedy. Buying inputs cannot solve payroll shortage; household wealth is not the same account as market purchasing funds.

Industry remains the place to construct facilities and control their individual production/procurement/sales policies. In pooled runs, each local facility uses the same company stock. In legacy runs, the warehouse selector shows actual separate facility stores; loading an old save never merges them.

New v3 runs also link the selected owned ship to [Fleet → Route planner](AUTOMATION.md#ordered-cargo-itineraries). Plan ordered market buys/sales and company-stock loads/unloads through two to four actual towns while retaining mixed cargo. Harbor still handles ordinary manual transactions; choose **Manual control** before submitting ship orders. Pausing the route retains its cursor, voyage and cargo. Planner **Inspect harbor** returns to this same transaction surface; remote information never grants transfer permission.

## Shared stock rules, version 2

One company owns one physical store and one FIFO account at each town where it has a private facility. Public market stock, other companies' stocks, other towns and ship holds remain separate. A warehouse adapter validates the named facility's owner and port before resolving this store; industrial shipping uses the same adapter and real voyages.

Capacity is the checked sum of local owned facilities' authored warehouse capacities. Granted and paid pending sites contribute storage immediately, matching the existing permission to stock a construction site. Pending facilities still skip automatic procurement, production and sales, including on their completion day. They first become production-eligible one full interval after completion. No facility means no free company warehouse.

The shared output ceiling for a recipe is the sum of the output limits of operational local facilities with that exact recipe. Installed but disabled facilities still contribute capacity; pending facilities do not contribute output ceilings. Multiple Mills therefore provide additional throughput and output headroom. A recipe without an output ceiling still respects finite aggregate warehouse capacity.

Private phases retain ordinal port/facility order. Every facility reads the stock left by preceding operations. When two facilities need scarce inputs, the first eligible facility can consume them; only successful complete batches pay wages. A downstream producer can consume an earlier batch immediately if it runs later in that phase; otherwise it waits for its next daily attempt. There is no automatic reordering, catch-up production or special input reservation.

Procurement targets and sales reserves refer to shared stock. Policies still perform at most one transaction per configured good per daily phase. They can compete for cash/materials, and an output sales policy can sell material another facility needs. New construction keeps its existing disabled procurement/sales defaults, so the player explicitly chooses those policies.

Facilities retain separate IDs, construction states, controls, deadlines, batches, input/output counters, wages and policy transaction totals. Pooled facility snapshots carry only a stockpile reference and zero local holdings. Production events identify both the real material store and producing facility. Company Value counts shared goods once, completed/pending capital once, and never adds FIFO basis a second time. Wages remain an expense separate from material basis.

## Compatibility and saves

`CompanyStorageRules` is independent of geography, economic generator/content and valuation versions. `Legacy = 1` retains separate warehouses; `Pooled = 2` is selected explicitly for new v3 host runs. Existing constructor defaults remain legacy for historical APIs and reports. Pooled identity comparison groups append `/storage-v2`; the seed, economic generation and objective draft do not change.

Old pooled runs with itinerary version zero retain save schema 3. New v3 pooled host starts explicitly choose itinerary version 1 and schema 4, preserving complete route instructions and exact next action/budget alongside company stock. Original schema-1/2 identities omit the storage field and retain their previous format, fingerprints and behavior. There is no consolidation or route migration. The codec validates one owned pool per actual company/town, derived finite capacity, zero duplicate facility inventory, exact FIFO/physical agreement and any versioned itinerary before replacing a host session. Retry starts a fresh copy of the same storage, itinerary identity and objective draft.

## Completion and diagnostic export

The authoritative winning operation freezes the economy immediately. The host acknowledges completion and offers **View results** without requiring another day or a timed animation. The frozen map, Journal and save remain inspectable.

Development builds and the Editor expose **Export Run Report** on diagnostic surfaces. It creates a raw portable save plus a human-readable report under `Application.persistentDataPath/Exports`, displaying the actual paths. Unique safe filenames prevent accidental replacement; size limits bound output. Export neither changes the world nor uses a save slot.

Reports separate markets, households, companies, treasury and finite outside money; list physical stores/FIFO, ships, facilities, input/payroll/output status, prices/targets and objective conditions. Recent host events are explicitly **partial bounded history**, not a complete lifetime transaction ledger. Exporting a difficult economy does not repair its finances or stock. Keep private playtest saves out of published documentation.

The [Task 2.2 verification record](TASK-2.2-VERIFICATION.md) contains the criterion audit, actual test results, Editor and Windows Mono winning journeys, save/export checks, screenshots and remaining graphics/runtime warnings.

## Modern sale handling fees

Economic rules1 Harbor previews show the gross market payment, exact port service fee, net company receipts and local market/worker shares. The company pays2% on successful ordinary sales from either ships or local stock. Buying and private transfers are exempt. A fee is withheld from sale proceeds; no upfront seller cash is needed. Gross market affordability still applies. Fractional orders and split sales retain exact cumulative rounding. Older runs show their original gross/net-equivalent settlement. [Town and Ledger explanations](ECONOMIC_CIRCULATION_REVIEW.md#commercial-handling) keep these expenses separate from FIFO and wages.
