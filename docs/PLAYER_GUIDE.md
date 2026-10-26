# Your first company

Begin at the main menu. **New Run** defaults to **Expanding Economy v3 / The Grand Commission**. Review the seed, company charter and ascension, inspect the full map, then choose four objectives before starting. **Sandbox** offers the economic systems without a victory deadline. The encyclopedia is available before you commit to a run.

## Choose a world and text size

Settings offers Compact, Default, Large and Extra Large game-wide text. Default body text is 17 px; the selected preference is remembered on this device. It changes layout and hit targets without spending a day or changing the economy.

Use **Explore full map** to inspect every town before drafting. Select towns to compare market stock, roles, building opportunities, cash and needs; route distances are available in a secondary foldout. A valid seed changes the preview; invalid text keeps the last valid map and prevents starting with that text. **Copy seed** records the seed; **Randomize seed** changes the preview, not an active world.

**Advanced / Legacy world rules** retains Fixed / Classic, Seeded Archipelago v1 and Living Cities v2. Seeded modes accept a signed 32-bit integer. Record mode/version, charter, challenge, ascension and pricing choice when comparing runs. New v3 runs use shared company town stock; loading or retrying an older run preserves its separate facility warehouses.

Ascension 0 preserves instant construction. Ascension 1 spends the full cost immediately but new private sites require two complete economic days, then a full production interval before the first eligible batch. The Industrial Consortium's inherited bakery is already built. See [seeded runs and Ascension](SEEDED_RUNS.md).

## Choose an approach

Review the objective, deadline, solvency condition, ship, company cash, and starter assets together. A liquid merchant company, an industrial company with a factory, and independent traders with logistical flexibility offer different opportunities. Select one package; their benefits do not accumulate.

For the Grand Commission, choose **one of three cards in each of four rounds**, then review all four mandatory goals. Victory requires all four together and at least 100 company coins by day 180. **Objectives / Journal** pauses playback and shows numerical conditions, reasons, permanent accomplishments and conditions that can reverse. A destination sale can lose credit if you buy those goods back. Read [The Grand Commission](GRAND_COMMISSION.md) for exact objectives.

The setup screen supplies the current numbers. The [run guide](RUNS.md) lists historical packages and strategies. [Charters](reference/mechanics/help_charters.md) explains starter grants and tradeoffs. [Challenges](reference/mechanics/help_challenges.md) explains the difference between net market delivery, genuine regional consumption, and productive enterprise performance.

## Use the command deck

The map starts with a quiet, collapsed command deck. Select a town or ship to reveal its actual context, up to three facts, a current blocker when present, and the relevant actions. **Collapse** gives the chart more room; reopening retains the selection. Town names and ship markers are selectable by mouse or keyboard. **Routes** exposes distance captions; Fleet highlights the selected ship's configured route without moving it.

The mode strip provides six working destinations:

| Mode | What to do there |
| --- | --- |
| **Map** | Select a town or ship, inspect the bottom deck, and choose Harbor, Industry or Economy. **Details / log** opens the full operational inspector. |
| **Trade** | Open Harbor for the selected context. With no selection, choose a town first. Exact paid trades and docked Load/Unload remain in Harbor. |
| **Industry** | Construct or manage your facilities at the selected town, including their individual policies and actual local stock. With no selection, choose a town first. |
| **Fleet** | Choose an owned ship, see its cargo, dock or ETA, route and exact next instruction, then **Inspect / edit route**. |
| **Economy** | Compare the selected town's next food need, separate money accounts and current production opportunities. **Full city accounts / pricing** opens the detailed City Economy. |
| **Objectives** | Open the real Journal, with exact goals and permanent versus reversible conditions. Historical challenges retain their own objective view. |

Mode changes and management views pause playback. Selection, inspection, route overlays and collapsing the deck do not change the day, cash, stock or route progress. Returning from Harbor, Industry, Fleet or Economy does not automatically resume playback or enable automation. Use **Play**, **Pause**, **Next day** and the speed selector deliberately. The pause menu's explicit **Resume** restores playback only if it was running when that menu opened.

**Back** returns to the view that opened the detail or planner, keeping the relevant town and owned ship. Escape closes an active overlay or confirmation first, returns Fleet/Economy or a context chooser to Map, and opens pause from ordinary Map play. The map's **Details / log** retains the full **Overview / Market / Controls** inspector, including historical route settings, recent activity and run identity. Company development remains available from pause or **Fleet → Company / shipwright**; Ledger is also reachable from Economy and pause.

The dark chart and parchment panels share goods and status symbols, always paired with words or tooltips. HUD cash and reference value use at most one decimal; a positive amount below 0.1 appears as `<0.1`. Hover for the exact amount. Harbor execution previews and the Ledger retain exact monetary precision; display rounding never changes affordability or scoring.

## Make a first trade

1. Select your owned ship or its docked town. Open **Harbor** from the command deck, **Trade**, or by right-clicking the town.
2. Select a commodity, choose **Selected ship hold** for the paid trade target, and set a whole quantity. Compare the **exact total**, current **Buy / ask** and **Sell / bid**, average execution price, cargo room, and cash.
3. Buy only what the market has and your company can afford. The completed trade changes cash and cargo immediately without advancing the day.
4. Inspect another connected town and choose a voyage deliberately. Remote quotes describe the current market, not a guaranteed price after arrival.
5. Advance time or resume playback. After the ship actually arrives, inspect a fresh proposal and sell.

Opening Harbor pauses the economy. Closing it leaves you in control of resuming. Disable the selected ship's automation before issuing manual cargo orders. NPC holds are read-only, and inspecting a remote ship never permits remote loading or trading.

The same Harbor shows **Company local stock** when you own industry in that town. Select it for paid market purchases/sales, including fractional quantities, even while your ship is elsewhere. **Load** moves local goods aboard the docked ship; **Unload** moves cargo into local stock. These transfers move no cash and preserve historical cost. They still require whole cargo units, space at the destination and the actual matching dock. Quantity presets only edit the proposal; the action button commits it. An older saved run shows a selector for its actual separate facility stores.

If an order cannot execute, read its reason: stock, capacity, company funds, destination liquidity, location, or a policy restriction. A lower selling limit cannot provide missing town cash. In Living Cities, households can replenish market cash by actually purchasing stocked food, or companies can inject cash through real local purchases. Check household funds and available goods before expecting recovery; there is no guaranteed payment day. Learn more about [trade](reference/mechanics/help_trading.md), [prices](reference/mechanics/help_prices.md), and [liquidity](reference/mechanics/help_liquidity.md).

## Build a supply chain

Use the generated [production-chain table](reference/chains.md) to find a recipe, its materials, and its building. Select a town and open **Industry** from the deck or mode strip. Construction competes with cargo purchases for liquid cash and grants no free materials. Generated capabilities are binding: a forest-furniture town cannot build a Sawmill just because you can pay. Its real Plank inputs must arrive through trade.

In a new v3 run, all your facilities in one town use **one company stockpile**. Grain bought or unloaded there can feed a local Mill; its Flour can feed a local Bakery with Logs. No factory-to-factory transfer is needed. Public market goods, other companies' stores and other towns remain separate. The sum of your real local facility capacities limits storage; paid pending sites add space immediately but cannot produce early. Multiple operational buildings of a recipe add throughput and output headroom. Stable facility order determines whether a downstream batch runs that day or waits for the next opportunity.

Use the recommended industry preset to prepare a draft. Review its input targets, price limits and cash reserve, then apply the policy deliberately to authorize future purchases. Policies remain separate per facility but read the shared stock: selling all Flour may deprive your Bakery of inputs. The status and schedule show construction progress, missing inputs and blocked operation; detailed controls remain available. A successful batch consumes actual owned inputs and creates actual output. Living factories must also fund their displayed wage from company cash. An unpaid batch waits without using inputs, while a pending site cannot operate early. [Harbor and town stock](HARBOR.md) explains the complete storage contract and legacy behavior.

For a new v3 cargo itinerary:

1. Retain finished output instead of selling all of it locally.
2. Select an owned ship, open **Fleet**, then **Inspect / edit route**.
3. Draft two to four actual town stops with ordered Buy, Sell, Load and Unload actions. Set whole quantities, minimums, price limits, cash or warehouse reserves and Skip/Wait behavior.
4. Review the current quote and blocker, then apply the draft from a permitted manual-control dock and stop boundary. Apply and enable are separate actions.
5. Enable automation and advance economic days. Fleet shows the exact next work and reason when the ship waits; inspection never repeats completed orders.

The ship must physically dock to load. It keeps goods that the destination cannot buy. Procurement, production, loading, voyages, and sales all use ordinary finite economic rules. Older runs retain their two-port policies under **Details / log → Controls**, including the industrial-shipping workflow. See [cargo itineraries](reference/mechanics/help_cargo_itineraries.md), [factory ownership](reference/mechanics/help_factories.md), [procurement](reference/mechanics/help_procurement.md), and [historical industrial routes](reference/mechanics/help_industrial_routes.md).

## Read a living town

Select a town to see its concise deck, then open **Economy** for food, money and production facts. Compare market cash with household cash: only the market pays for your sold cargo. Residents can have money but no Bread, or face stocked Bread they cannot afford. Harbor rows distinguish public supply from your own recipe inputs; a shortage label is a planning signal, not an automatic order.

The next Bread tile compares current public stock with the exact next scheduled request at current population, including carried fractional demand. **Awaiting first need** means there is no completed meal observation yet. Current-stock shortfall and current affordability are separate: the exact retail preview uses today's stock, quotes and household funds. These facts do not guarantee future availability, payment or price. Classic/v1 households are **Not modeled**, rather than a zero-food crisis.

Production cards show the real recipe, current inputs, employer cash and wage, storage/output limits, next eligible date and last committed outcome. **Ready from current state** means one complete batch could use the current materials and funding at its scheduled opportunity. It does not reserve those materials or predict that earlier traders, procurement or another pooled facility will leave them available. A previous successful batch is not proof of current readiness. Pending or disabled facilities remain blocked, and public market stock cannot silently supply a private batch.

**Full city accounts / pricing** groups detail into **People & money**, **Needs & coverage** and **Industry & trade**. Inspect population/recent direction, smoothed prosperity, luxury eligibility, actual wage income, public industries and eligible private construction. Population affects later food quantity; prosperity and a protected food reserve gate infrequent Furniture purchases. Logging plus furniture making does not imply local Plank production: check missing Sawmill capability and actual inputs before investing. **City economy help** opens the shared offline explanation; **Back** returns to the selected town's previous workspace.

Use the core summary for immediate decisions and the secondary panels to diagnose constraints. Long ledger/reference/configuration screens still scroll internally. All four text sizes remain available; ordinary trade controls keep their readable size. Actual resolution and pointer coverage is recorded in [Unity verification](UNITY.md).

The recovery challenge asks for sustained paid food access, retained population and prosperity, plus your own solvency by day 90. Keep the target supplied and watch whether residents and employers can fund their next actions. [Living Cities](LIVING_CITIES.md) includes a real example in which household purchases let an illiquid market buy one more cargo unit, followed by another honest liquidity wait.

## Read your accounts

Company cash is available liquidity. **Company Value** also includes owned goods, ships, operational sites and paid pending capital once; it is a reference valuation, not available cash or liquidation proceeds. Purchase expenditure, sale revenue, construction payments, and held stock answer different questions. The financial panel and ledger separate them.

Goods retain historical acquisition or input-material cost through manufacture and transfers. A completed sale releases that cost into cost of goods sold; its attributed gross margin is revenue less that cost. Unsold goods are shown with their remaining basis, not an invented sale value. Granted starter assets remain identifiable.

Living wages are separate paid expenses. Operating surplus after wages subtracts all wages paid from material gross margin, including wages for output still held; it is not full profit. [Historical cost](reference/mechanics/help_cost_basis.md), [financial reports](reference/mechanics/help_finances.md) and [Living Cities](LIVING_CITIES.md) explain the distinction and remaining unmodeled costs.

## Finish, inspect, and try again

The compact objective summary shows goals met and remaining time; the Journal carries the full conditions. A challenge freezes automatically when it succeeds or reaches its failure condition. The whole triggering economic operation commits first. Victory then displays an acknowledgment with **View results**; dismissing it lets you inspect the frozen map or Journal without spending another day.

The results screen also offers **Inspect frozen map**, including after loading an older completed save. Harbor, Economy and Industry remain available for inspection; economic actions stay disabled. **Menu** returns to results.

Review the outcome and score breakdown alongside actual cash, costs, deliveries, production, and remaining goods. Compare only the same challenge, charter, ascension and exact world identity, including chosen objectives and storage rules. **Retry Same World** restores all starting conditions and the original draft. **New World / Reroll** explicitly previews a new seed before you begin; **Change Setup** returns to editable setup.

Escape closes the top Harbor or Industry modal first. Fleet/Economy return to Map; detailed screens return to their parent, and ordinary Map opens pause. Restart, return to menu, abandonment, and exit protect unsaved progress with confirmation.

Use **Save / Load** from pause or Company to store a run explicitly in one of three local slots. **Continue** loads the latest compatible slot, including exact voyages, stocks, construction and terminal state; there is no automatic or cloud save. An invalid load leaves the active run intact. Retry starts fresh and does not replace saved slots. Text-size preferences are independent of run saves.

The Editor and development builds offer **Export Run Report** on Save/Load, results and Ledger. It creates an exact save plus a readable diagnostic report under the displayed `Exports` paths, without changing the world or a save slot. Recent activity in the report is explicitly partial. The [Task 2.2 verification record](TASK-2.2-VERIFICATION.md) separates actual checks from local repetition instructions.

## Use the encyclopedia

Open it from the main menu, setup, or pause. Select a category, search names or descriptions, and follow related entries. Recipe inputs, outputs, extraction/transformation behavior, buildings, sources, and uses all come from canonical content. Back returns to the previous entry or parent screen.

The reference works offline inside the game. This website adds development documentation and experiment evidence under a separate Development menu; it is not required to learn the controls.
