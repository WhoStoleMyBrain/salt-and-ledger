# Your first company

Begin at the main menu. **New Run** opens world, seed, ascension, challenge and company-charter selection; **Sandbox** offers the same economic systems without a victory deadline. The encyclopedia is available before you commit to a run.

## Choose a world and text size

Settings offers Compact, Default, Large and Extra Large game-wide text. Default body text is 17 px; the selected preference is remembered on this device. It changes layout and hit targets without spending a day or changing the economy.

Choose **Fixed / Classic Archipelago** for the original authored map, **Seeded Archipelago v1** for new geography with the historical economy, or **Living Cities v2** for generated specializations and finite resident finances. Seeded modes accept a signed 32-bit integer. Inspect coordinates, connections and voyage times; Living Cities additionally shows production roles, private eligibility, population, prosperity and household funds. **Copy seed** records the seed; **Randomize seed** changes only the preview. Record the full mode/version, charter, challenge, ascension and living pricing choice to reproduce a run.

Ascension 0 preserves instant construction. Ascension 1 spends the full cost immediately but new private sites require two complete economic days, then a full production interval before the first eligible batch. The Industrial Consortium's inherited bakery is already built. See [seeded runs and Ascension](SEEDED_RUNS.md).

## Choose an approach

Review the objective, deadline, solvency condition, ship, company cash, and starter assets together. A liquid merchant company, an industrial company with a factory, and independent traders with logistical flexibility offer different opportunities. Select one package; their benefits do not accumulate.

The setup screen supplies the current numbers. The [run guide](RUNS.md) lists the packages, goals, and reproducible strategies. [Charters](reference/mechanics/help_charters.md) explains starter grants and tradeoffs. [Challenges](reference/mechanics/help_challenges.md) explains the difference between net market delivery, genuine regional consumption, and productive enterprise performance.

## Make a first trade

1. Select your ship and its docked town. Open Trade through the visible action or the town context interaction.
2. Select a commodity and quantity. Compare the **exact total**, current marginal prices, average execution price, cargo room, and cash.
3. Buy only what the market has and your company can afford. The completed trade changes cash and cargo immediately without advancing the day.
4. Inspect another connected town and choose a voyage deliberately. Remote quotes describe the current market, not a guaranteed price after arrival.
5. Advance time or resume playback. After the ship actually arrives, inspect a fresh proposal and sell.

Opening trade pauses the economy. Closing it leaves you in control of resuming. Disable the ship's automation before issuing manual orders.

If an order cannot execute, read its reason: stock, capacity, company funds, destination liquidity, location, or a policy restriction. A lower selling limit cannot provide missing town cash. In Living Cities, households can replenish market cash by actually purchasing stocked food, or companies can inject cash through real local purchases. Check household funds and available goods before expecting recovery; there is no guaranteed payment day. Learn more about [trade](reference/mechanics/help_trading.md), [prices](reference/mechanics/help_prices.md), and [liquidity](reference/mechanics/help_liquidity.md).

## Build a supply chain

Use the generated [production-chain table](reference/chains.md) to find a recipe, its materials, and its building. Select a town and inspect its private-industry options. Construction competes with cargo purchases for liquid cash and starts with empty private storage. In Living Cities, generated capabilities are binding: a forest-furniture town cannot build a Sawmill just because you can pay. Its real Plank inputs must arrive through trade.

Use the recommended industry preset to prepare a draft. Review its input targets, price limits and cash reserve, then apply the policy deliberately to authorize future purchases. The status and schedule show construction progress, missing inputs and blocked operation; detailed controls remain available. A successful batch consumes the factory's private inputs and creates its private output; public market stock is separate. Living factories must also fund their displayed wage from company cash. An unpaid batch waits without using inputs, while a pending site cannot operate early.

For factory-to-market transport:

1. Retain finished output instead of selling all of it locally.
2. Assign the player ship to an industrial route using the owned source warehouse.
3. Select the good, source threshold, shipment quantity, warehouse reserve, destination, and selling floor.
4. Choose the return behavior and apply the policy.
5. Enable automation and advance economic days. Inspect its stage and wait reason when progress stops.

The ship must physically dock to load. It keeps goods that the destination cannot buy. Procurement, production, loading, voyages, and sales all use ordinary finite economic rules. See [factory ownership](reference/mechanics/help_factories.md), [procurement](reference/mechanics/help_procurement.md), and [industrial routes](reference/mechanics/help_industrial_routes.md).

## Read a living town

Select a town and use **Overview**, **Market** or **Controls** in the sidebar. The fixed **Trade**, **Industry** and **city economy** actions open their respective views. Compare market cash with household cash: only the market pays for your sold cargo. Residents can have money but no Bread, or face stocked Bread they cannot afford. The food summary separates both causes and shows actual consumed goods; a requested meal is not a completed purchase.

**City economy** groups details into **People & money**, **Needs & coverage** and **Industry & trade**. Inspect population/recent direction, smoothed prosperity, luxury eligibility, actual wage income, public industries and eligible private construction. Population affects future food quantity; prosperity and a protected food reserve gate infrequent Furniture purchases. Logging plus furniture making does not imply local Plank production: check missing Sawmill capability and actual inputs before investing. **City economy help** opens the shared offline explanation; **Back** returns to the map.

Use the core summary for immediate decisions and the secondary panels to diagnose constraints. Long ledger/reference/configuration screens still scroll internally. All four text sizes remain available; ordinary trade controls keep their readable size. Actual resolution and pointer coverage is recorded in [Unity verification](UNITY.md).

The recovery challenge asks for sustained paid food access, retained population and prosperity, plus your own solvency by day 90. Keep the target supplied and watch whether residents and employers can fund their next actions. [Living Cities](LIVING_CITIES.md) includes a real example in which household purchases let an illiquid market buy one more cargo unit, followed by another honest liquidity wait.

## Read your accounts

Company cash is available liquidity. Purchase expenditure, sale revenue, construction payments, and held stock answer different questions. The financial panel and ledger separate them.

Goods retain historical acquisition or input-material cost through manufacture and transfers. A completed sale releases that cost into cost of goods sold; its attributed gross margin is revenue less that cost. Unsold goods are shown with their remaining basis, not an invented sale value. Granted starter assets remain identifiable.

Living wages are separate paid expenses. Operating surplus after wages subtracts all wages paid from material gross margin, including wages for output still held; it is not full profit. [Historical cost](reference/mechanics/help_cost_basis.md), [financial reports](reference/mechanics/help_finances.md) and [Living Cities](LIVING_CITIES.md) explain the distinction and remaining unmodeled costs.

## Finish, inspect, and try again

The objective summary shows the current goal, progress, time remaining, and outstanding conditions. A challenge stops automatically when it succeeds or reaches its failure condition. The whole triggering economic operation completes before results appear; playback does not continue behind the results screen.

Review the outcome and score breakdown alongside actual cash, costs, deliveries, production, and remaining goods. Compare only the same challenge, charter, ascension and exact world identity. **Retry Same World** restores all starting conditions, including seed and difficulty. **New World / Reroll** explicitly previews a new seed before you begin; **Change Setup** retains the current selections for editing.

Escape closes the top trade or industry modal first. In ordinary gameplay it opens pause; in secondary screens it returns to the parent. Restart, return to menu, abandonment, and exit protect active progress with confirmation. Runs are in memory only and do not survive closing the application.

## Use the encyclopedia

Open it from the main menu, setup, or pause. Select a category, search names or descriptions, and follow related entries. Recipe inputs, outputs, extraction/transformation behavior, buildings, sources, and uses all come from canonical content. Back returns to the previous entry or parent screen.

The reference works offline inside the game. This website adds development documentation and experiment evidence under a separate Development menu; it is not required to learn the controls.
