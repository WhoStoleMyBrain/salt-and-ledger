# Autonomous companies

NPC companies are persistent owners in the same regional economy as the player. Each has a disclosed finite starting endowment, an initial fleet, a seeded commodity specialization and a business plan tied to actual generated towns and eligible recipes. Merchant, Industrial and Diversified profiles express investment preferences. They never receive recurring grants, free replacement vessels, credit or guaranteed profit.

The default configuration starts roughly one company per two towns, with 800–1200 coins and one 3–6-unit ship each. Strategy weights cycle Merchant / Industrial / Diversified while seeded commodity, site and route selection preserve geographic diversity. Company count may be zero. All settings live in the existing canonical [generation configuration](WORLD_GENERATION.md); changing them changes run identity and its fingerprint.

## Ordinary daily operations

Company planning runs in ordinal company-ID order after the daily household reset and before the existing actor phase. Fleet assignment uses committed/staged current assets. Each company reviews investments every16 days, with at most one investment per review. Default caps are three ships and two industries per company, including paid pending assets. The player fleet cap remains separate. The configured maximum possible fleet must fit the256-ship save bound.

Industrial and Diversified companies first establish an eligible industry for their chosen output. Diversified companies prioritize a second vessel before adding another factory. Merchants prioritize shipping capacity, then may build a supporting industry at their route's origin when eligible. A further factory requires eight completed batches per existing site. Industry purchases retain the configured80-coin operating reserve plus four batch payrolls; hull purchases retain the operating reserve. Unaffordable decisions retain capital. Profiles do not inspect player plans, future markets or economic forecasts.

Factory construction uses the ordinary paid command, transfers its full price to the local market and creates actual company storage. Ascension1 now applies the same two-day foundation delay to every company. Private production follows canonical inputs, wages, rate, capacity and schedules. Warehouses buy up to three recipe batches of inputs within real storage and cash limits, and sell output above two whole units retained for shipping. Rules use ordinary exact marginal pricing, FIFO and commercial handling fees. Warehouse shortages never create goods or borrowing.

Market shipping reuses the existing regional market-route evaluator. Industrial shipping physically loads warehouse output, sails to the selected customer/input harbor, sells it and may bring a canonical input back to its own warehouse. Other required inputs use paid local procurement. No remote transfer is permitted. New hulls consume the same local Planks, market payment and household labor as a player's order, take the same four days, and arrive empty at the real shipyard. They sail to the company's business harbor before beginning work. Every ship draws on its company's one cash account.

The immutable plan and review state belong to the company snapshot. Daily changes, construction, policy assignment, trades, FIFO and company cooldowns commit atomically with the rest of the day. [Schema9 saves](SAVES.md) preserve exact plans, review deadlines, paid hull sequences, voyages, construction, production and warehouse policies. Load validates original company identity and specialization, owner-specific pending/completed hulls, capital and asset caps before replacing a session. NPCs receive no player Grand Commission credit, Royal Mint authorization, export contracts or project privileges.

## Inspection and limits

Select an NPC ship on the map or ship picker to inspect its company identity, specialization, owned fleet, industries, warehouses and pending hulls. Industry lists show other companies' local sites as read-only rows. Ordinary activity identifies companies commissioning vessels, constructing factories and producing goods. Player command surfaces retain their owned-asset filters. Future investment decisions are not displayed.

Plans are deliberately persistent. Companies do not adapt to competitors, change commodities after a downturn, liquidate assets or receive rescues. An industrial company may wait on missing inputs; a merchant may retain cargo when a town cannot pay. The two-harbor industrial route carries one input on its return leg; additional inputs rely on paid local warehouses. This milestone does not promise sustainable employment or profitable investment in every seed.

Run the reproducible witness:

```text
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --npc-companies --check
```

The committed [720-day results](experiments/npc-companies-results.json) include seed42 normal endowments and a zero-cash control. They record real ship orders, input-consuming private batches, docked transfers, ship and warehouse sales, fees, wages, final public stocks/prices, conservation and exact save continuation. See [verification](TASK-3.3-VERIFICATION.md) for executed checks and remaining Unity checks.
