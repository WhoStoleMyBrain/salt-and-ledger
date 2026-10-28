# Player guide

## Start a company

Choose **New Run** from the main menu. The current game uses Expanding Economy v3 with finite household and market accounts, local shared company stock and ordered cargo itineraries. Grand Commission is the scored experience; Sandbox is an explicit unlimited alternative.

Choose a signed 32-bit seed, charter, StockOnly or Coverage pricing and Ascension0 or1. Explore the full economic map before drafting. Invalid seed text retains the last valid preview. Town roles determine real industries, imports, money and permitted construction; the map is not a promise that every strategy will remain profitable.

The Merchant Company starts with 1800 coins, a 10-unit ship and no industry. Industrial Consortium starts with 1200 coins, an 8-unit ship and an operational Bakery supplied with 2 Flour and 2 Logs. Independent Traders start with 1500 coins, a 12-unit ship,4 Grain and 2 Logs. Grants have zero paid basis but real reference value. Ascension1 makes new facilities complete after two days; starter facilities are already operational.

In Grand Commission, select one of three offered cards in each of four category rounds. All four chosen goals are mandatory by day 180, alongside 100 liquid company coins. Read numerical requirements in [Objectives](GRAND_COMMISSION.md), especially distinct-town production, signed net sales and current versus cumulative conditions.

## Work from the chart

The compact HUD shows day, company cash, reference value and objective progress. Map, Trade, Industry, Fleet, Economy and Objectives switch workspaces. Select a town or ship for the collapsible bottom command deck. Escape respects modal/confirmation priority; map inspection and navigation do not advance time. Pause, encyclopedia, results and Journal stop playback. Text size is under Settings and changes layout without changing the run.

Use [Harbor](HARBOR.md) to inspect **Town market** or **Company warehouse**. Each good shows stock, its local role, current market bid/ask, and signed **production / use / net over the next 12 days**. These are scheduled possibilities, not guaranteed future stock: a blocked factory still needs inputs, wages and space. Open the selected good's **breakdown** for installed versus enabled production, current blockers, household needs and the last actual consumption.

Open **compare towns** for that good's current market stock, bids, asks, buying power and travel time from the selected docked ship. Select a row to inspect that harbor without losing the good selection. Price differences identify candidates for a voyage; exact bulk pricing, sale fees, finite buyer cash and changes while sailing decide the result.

Choose **Ship cargo** or **Company warehouse** as the paid trade destination independently of the inspection scope:

| Action | Physical flow | Payment |
| --- | --- | --- |
| Buy cargo | Market → Ship | Company pays market |
| Sell cargo | Ship → Market | Market pays company; sale fee withheld |
| Buy warehouse stock | Market → Warehouse | Company pays market |
| Sell warehouse stock | Warehouse → Market | Market pays company; sale fee withheld |
| Load / Unload | Warehouse ↔ Ship | None |

Ship actions require the real local dock and Manual control. Warehouse trades can be managed remotely; they never move goods between towns. Read the exact order preview, including gross sale amount, fee and net receipts, before committing. Expand calculation details when needed.

Industry constructs real facilities and manages inputs, output limits, payroll and policies. Same-town facilities share company stock; public stock stays separate. Construction costs are capital, wages are expenses, and held material basis is not realized profit. Read current input/payroll/storage blockers before enabling procurement or sales.

Fleet selects each owned ship independently. In the [route planner](AUTOMATION.md), select one of 2–4 stop cards and enter several goods' quantities directly in its **Unload / Sell / Load / Buy** grid. Blank or zero removes an instruction. New stops dispose of cargo before acquiring more; **Advanced limits & execution order** retains minimums, Skip/Wait, prices, reserves, spending caps and exact custom ordering. The cards summarize the whole repeating route while one stop is edited.

Plan before building: Load/Unload at a town without company storage shows a warning but remains valid. At execution it skips or waits as configured; building eligible industry later lets the same route use that real warehouse. Nothing is constructed or supplied by a route. Applying and enabling are separate actions. Pausing retains cargo, voyage and the next unfinished instruction; the live planner shows the current blocker, free capacity and last committed outcome. Repeated execution or save/load cannot reset the eight-operation daily budget. The player can commission up to three ships using actual local Planks, company cash and labor.

Economy puts next Bread need, stock/funds shortfalls and production readiness first. Expand **Food affordability & demand** or a facility's **Inputs, payroll & last outcome** for the underlying figures; **Accounts & pricing** opens the full city accounts. Households can be wealthy while Bread is absent; a market can lack payroll while its households have money. The public Royal Mint requires commissioning and physically transported Silver; finite external contracts are not unlimited buyers.

For a quick inspection: **Map → town → Harbor → select a good → breakdown / compare towns → Industry → Fleet or Economy → Map**. Keep eligible construction in Industry. A warehouse scope describes only your local industries; the market scope describes public industry and household/municipal needs. Neither counts trades or ship transfers as passive output. Journal rows keep exact current/target values and **Current / Cumulative** markers; **Details & blockers** explains the conditions and identifies the actual facilities. Inspection and disclosure controls never spend a day.

## Finish or continue

Save explicitly before leaving; [Continue](SAVES.md) restores the exact saved state. Retry starts fresh with the same complete identity. Victory, deadline failure or confirmed abandonment freezes economic commands. The acknowledgment offers results or frozen inspection. Scores compare only matching identities, not unrelated seeds or starting packages.

See [mechanics](MECHANICS.md), [expanding economy](EXPANDING_ECONOMY.md), [town finances](LIVING_CITIES.md) and the shared in-game encyclopedia for exact rules. The current economy has measured long-horizon shortages; a successful commission is not proof of sustainable welfare everywhere.
