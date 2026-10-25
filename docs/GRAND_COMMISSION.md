# The Grand Commission

The Grand Commission is the default scored choice for **Expanding Economy v3**. Reveal the seeded map first, then choose one objective from three offers in each of four rounds. All four chosen cards become mandatory. Complete them **together by day 180**, with **at least 100 liquid company coins**. A high partial score cannot substitute for victory.

The existing Sustainable Provisioning, Industrial Network, Royal Commission and unlimited sandbox remain available with their original objectives. Classic, seeded-v1 and Living Cities-v2 do not use this draft. Task 2.2 keeps these mission rules, world generation and monetary parameters unchanged while adding independently versioned [shared company town stock and Harbor](HARBOR.md) for new v3 runs.

## Read the map, then commit

1. **New Run** defaults to v3 Grand Commission. Select the seed, existing starting charter, pricing view and Ascension 0 or 1, then **Explore full map**. Inspect all four towns, actual sea connections, roles, stock/accounts, public industries and permitted private facilities. The preview is the world that will be started; exact distances remain available in a secondary foldout.
2. Enter the four category rounds: Navigation / Fleet, Production / Industry, Markets / Commerce, and Civic / Capital Development. Each offers three distinct cards from its four-candidate pool.
3. Select one card per round. Backtracking changes a choice without changing offers or advancing a day. Review the full four-card commission, world identity, charter, difficulty, deadline and cash floor before Begin.
4. During play, open **Objectives / Journal** for exact conditions and blockers. The map HUD keeps a compact currently-met count and days remaining. Journal inspection pauses playback; returning preserves the relevant parent screen.
5. Save explicitly to continue the existing run. Retry Same World starts the original commission and storage rules fresh; a reroll returns to setup for a new seed and a new reviewed draft. Victory freezes immediately and presents an acknowledgment with **View results**, **Inspect objectives** and **Inspect frozen harbor**. Results retain the four cards and their final numerical progress.

Construction, trade, payroll and shipping remain ordinary [v3 operations](EXPANDING_ECONOMY.md). A map or card selection never creates materials, wages, money, visits or objective credit. The incomplete pregame selection is not a persistent save slot.

## Draft-version 1 objective catalogue

Targets below are authored for every seed, not adjusted after testing an individual world. Names of role-relative destinations resolve to the actual generated towns before selection.

| Round | Card | Required result | Progress character |
| --- | --- | --- | --- |
| Navigation | All Harbors | Visit all four towns with owned ships; the initial dock counts | Permanent visits |
| Navigation | Second Command | Own two completed ships; the second acquired ship must visit two towns | Current fleet; permanent second-ship visits |
| Navigation | Regional Survey | Visit recovery, processing and furniture-role towns | Permanent visits |
| Navigation | Open the Routes | Visit three towns and retain a positive net sale of one good away from the initial town | Permanent visits; reversible net sale |
| Industry | A New Industry | Operational Mill and Bakery in **different towns**, each with at least one funded completed batch in that exact facility | Current sites; permanent batch evidence |
| Industry | Timber Chain | Operational Lumber Camp and Sawmill; four privately produced Planks | Current sites; permanent output |
| Industry | Artisan Guild | Operational Sawmill and Furniture Workshop; two privately produced Furniture | Current sites; permanent output |
| Industry | Breadworks | Operational Bakeries in two towns; four privately produced Bread | Current sites; permanent output |
| Commerce | Bread for the Coast | Four net Bread sold to the recovery town | Reversible signed net supply |
| Commerce | Planks for the Forest | Four net Planks sold to the furniture town | Reversible signed net supply |
| Commerce | Flour for the Bakers | Four net Flour sold to the food-source town | Reversible signed net supply |
| Commerce | Royal Exporter | 100 coins received from fulfilled finite external cargo contracts | Permanent actual receipts |
| Civic | Royal Institution | This company commissions the public Mint, then completes at least one physical Silver issuance | Permanent public development |
| Civic | A City Back in Trade | Six recent paid-food opportunities at recovery, at least 80% baseline-protected fulfillment, current prosperity at least 50 | Rolling/current welfare |
| Civic | Prosperous Enterprise | Reference Company Value growth of 100 coins and liquid cash of 100 coins simultaneously | Current assets and liquidity |
| Civic | Public Works | Two treasury-funded delivered Planks, plus six recent recovery food opportunities with at least 60% baseline-protected fulfillment | Permanent procurement; rolling welfare |

The Open the Routes sale must have a positive signed quantity for at least one good at one remote town. The exact minimum is one physical subunit (0.0001 good); ship orders still require whole units, while legitimate local warehouses can trade fractional goods. A mere quoted price, unsold cargo or move into owned storage does not qualify.

### Production is more than possession

A New Industry evaluates the same identified Mill and Bakery for operational state and actual funded batches. A starter Bakery and a new Mill in the processing town do **not** satisfy its different-town requirement. The Mill is eligible at processing; build the other Bakery at food source or recovery. Each must have actual recipe inputs, output space and company-funded payroll. Pending construction or an unsuccessful attempt grants no batch credit. The Journal identifies the relevant facilities and shows each site and batch separately.

In a new pooled run, same-town facilities share their company's stock and material FIFO, but retain separate production counters and payroll. This helps a local Mill feed a Bakery; it does not waive A New Industry's distinct-town requirement. Harbor's paid local-stock trades count as ordinary town market transactions, while Load/Unload transfers confer no production or sale credit.

Timber, artisan and Breadworks output comes from authoritative company private-production totals. Public production, purchased output and warehouse transfers do not become private production. Reference Company Value includes real owned capital and goods under existing valuation rules; it is neither liquidation proceeds nor realized profit.

### Net sales can reverse

For each exact target town and good:

```text
net supply = completed player-company local sales - completed player-company local purchases
```

Ship trades and private warehouse market trades share this rule, including automation. Buying four Flour and reselling those four at the same town earns zero. Importing four Flour and selling them to the named town earns four; buying one back reduces it to three and makes a four-Flour card unmet again. Negative raw balances remain visible, while the progress bar stops at zero. NPC trade, previews, rejected orders, other destinations and public procurement do not count as these local market sales.

### Food must actually be paid for

The welfare conditions inspect the recovery town's last six scheduled food opportunities. Fulfillment uses actual purchased Bread over the sum of the greater current-population and initial-population needs for those observations. Shrinking population cannot erase the baseline. The displayed percentage rounds down; exact integer quantities determine it. Six observations with low or zero consumption are not six completed meals.

Delivering Bread does not itself fulfill food: households must afford and buy it at a due opportunity. Missing stock or household funds can later reduce the rolling window, and prosperity can fall. Public/NPC contributions help regional welfare without claiming personal player attribution. Public Works still separately requires actual treasury-funded procurement. Mint ownership remains public.

## Progress, victory and score

Every condition exposes a raw value, exact target, display unit, semantic and reason. Cumulative visit, funded production and public-delivery facts can be checked off permanently. Current ownership, signed market supply, cash/value and welfare are labeled **currently met**; a mixed card is not permanently crossed off merely because its current requirements hold.

For each condition, `percent = min(100, floor(max(0, current) * 100 / target))`. A card takes the **minimum** of its condition percentages and is met only when all its conditions are met. This makes the remaining blocker visible. The compact count means **goals currently met**, not permanently finished goals.

Grand Commission objective score is `floor(500 * sum(four card percentages) / 400)`, bounded to 0–500. Existing components remain separate: 1,000 victory points, up to 600 remaining-time points, and up to 100 solvency points. Compare results only within the same complete identity, including the chosen commission. These are bounded prototype scores, not a ranking of wealth across different worlds or starting endowments.

Every atomic economic operation reevaluates the conjunction of all four cards and the 100-coin company floor. Success on day 180 takes precedence over deadline defeat. Failure at the deadline, success and confirmed abandonment freeze further economic mutation. Partial score cannot grant victory, and a terminal run does not continue automatically in sandbox.

## Deterministic identity and bounded history

The economic generator and content stay version 3; reference valuation stays version 1. **Mission draft version 1** is independent. For each round, begin an unsigned 32-bit state with `uint(seed) XOR 2166136261`, then FNV-1a mix the characters of `salt-and-ledger:grand-commission:1:<round-index>`. A bounded Fisher–Yates shuffle of the four authored candidates uses `state = state * 1664525 + 1013904223` modulo 2³² and multiplication-high bounded draws. Take the first three in order. Round indices 0–3 are fixed in category order.

This consumes no geography or economic-role random draws. Offers do not depend on charter, pricing, difficulty, prior selections, culture or screen visits. Templates resolve role destinations from unchanged v3 generation. Unknown draft versions and choices not offered in their round are rejected. All required facility roles exist in every v3 role permutation; that structural legality does not guarantee a winning strategy for every four-card combination.

The selected plan stores stable IDs and typed rule, target, unit, semantic, port/role/good/recipe parameters. Its SHA-256 signature covers the seed, draft version and ordered canonical parameters; display prose is excluded. The signature extends only drafted comparison groups. Independently, pooled storage appends `/storage-v2` to the complete run comparison group without changing that draft signature. Retry preserves the exact plan, storage rules and initial world.

`CommissionHistory` belongs to the atomic regional snapshot, with at most three owned ships, four first-visit dates per ship and 28 town/good purchase/sale balances. Initial docking, genuine arrivals and a commissioned ship's initial shipyard dock count. The second ship cannot fulfill two visits by remaining in the yard. Staged local trades, automation, voyages and construction share the ordinary commit/rollback boundary. Existing private production and public finance totals remain the source for other conditions; there is no second UI-owned quest ledger.

## Saves and compatibility

New pooled v3 runs with itinerary format 1 use world-save **schema 4**, including Grand Commission runs. Prior pooled runs retain **schema 3** and their original route controls. Older drafted runs retain **schema 2** and separate facility stores; other legacy runs retain schema 1. Absent `CompanyStorageRules` and `ItineraryVersion` fields retain their original rules, never automatic consolidation or route conversion. Restore reconstructs the offered catalogue and compares the saved plan's seed/version, choices, signature and every canonical resolved parameter. It validates bounded visit dates and ownership, market totals, public-flow adjustments, private production/payroll, terminal predicates, exact storage/FIFO ownership and any itinerary cursor. Loading neither advances a day nor grants credit again. A bad load leaves the active host session intact. See [ordered itineraries](AUTOMATION.md) for route format and continuation rules.

Fingerprints detect accidental damage and mismatches; they are not cryptographic authentication of a player-controlled save. Bounded first-visit records cannot prove an entire historical voyage sequence against a coordinated rewrite of otherwise plausible world state. The save format intentionally does not introduce a replay log or arbitrary quest counters. Valid older schema-1/2 saves remain supported byte-for-byte under their original rules. Development-only diagnostic exports retain the exact raw save and label recent activity as partial; they do not add history or change objective progress.

## Verification and limits

`GrandCommissionDraftTests` fixes ordered offers for seeds 42, -42, 99 and both signed-int32 extremes, checks three distinct offers from four templates, invalid choices/version/identity rejection, independent cultures and unchanged economic state across choices. A bounded 67-seed sample checks role targets, available recipes, distinct-town Mill/Bakery eligibility and connected routes.

`GrandCommissionProgressTests` uses ordinary operations for funded distinct-town industry, Ascension timing, signed net reversal, fractional warehouse trades, NPC exclusion, commissioned-ship arrivals, paid-food recovery followed by decline, and reversible Company Value versus permanent visits. The draft/progress targeted run passed 16 tests. Local-save tests and the reproducible witness add continuation and terminal evidence. Actual Editor/player interactions and visual limitations are recorded separately in [Unity integration](UNITY.md) and the [playtest guide](PLAYTEST.md); passing a headless test is not visual verification.

The [Task 2.2 acceptance record](TASK-2.2-VERIFICATION.md) covers pooled-storage integration, Harbor, acknowledgment, schema-3 continuation and diagnostic exports. Historical Task 2.1 captures and report outcomes below remain identified separately.

### Reproduce two actual winning commissions

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --grand-commission --check
```

The compact `GrandCommissionWitness` executes ordinary construction, paid warehouse/ship purchases, funded production, physical transfers/voyages, sales, contracts and Mint operations. It does not edit world state, add grants or change economic parameters. This preserved report uses legacy facility storage and checks exact mid-voyage and terminal schema-2 round trips as well as the outcomes below. Separate pooled tests cover new schema-3 runs without rewriting this historical report.

| Seed / charter | Mandatory selection | Result | Final company coins | Inventory purchase coins | Private wage coins | Private capital / public commission coins | Score |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| 99 / Merchant | All Harbors; A New Industry; Flour for the Bakers; Royal Institution | Victory day 18 | 648.6668 | 261.3331 | 24 | 880 / 400 | 2,164 |
| 42 / Independent | All Harbors; Timber Chain; Royal Exporter; Royal Institution | Victory day 13 | 295.6668 | 302.9998 | 42 | 820 / 400 | 2,129 |
| 99 / Merchant, idle control | Same commission as the first trial | Defeat day 180, 0/4 currently met | See witness output | 0 | 0 | 0 / 0 | Partial score only |

The first run builds the Mill/Bakery in distinct towns, ships Flour to the food-source destination and transports extracted Silver to the public Mint. The second operates a timber chain and physically fulfills a finite export contract before its Silver issuance. These selected plans fit different seed offer sets. Their paid inputs, wages and construction compete with shipment liquidity; final cash is not a profit comparison between differently endowed charters. Known strategies finish well before day 180, leaving room to learn rather than proving all offered combinations easy or economically sustainable.

### Follow the seed-99 witness through ordinary controls

Use Expanding Economy, seed **99**, Merchant Company, StockOnly pricing and Ascension 0. In the revealed map, **Southhaven** is food source and your initial dock, **Northport** is processing and the royal harbor, **Eastport** is forest/Silver, and **Westhaven** is recovery. Draft **All Harbors → A New Industry → Flour for the Bakers → Royal Institution**, review, and begin paused. Leave ship automation disabled and advance individual days so you can stop production at the prescribed batch counts.

1. Through Industry, build a Mill at Northport and buy **two Grain** into its private warehouse. Build a Bakery at Westhaven and buy **one Flour and one Logs** into that warehouse. Build a Silver Mine at Eastport. Through Company, commission the public Mint. These orders use actual company money; private warehouse purchases are local market trades, not remote transfers from your ship.
2. At Southhaven, buy **four Bread** aboard Ledger, then sail to Westhaven. Step days until arrival. After each advance inspect the three facilities: disable the Mill and Bakery after their first funded batch, and the Mine after its first Silver batch. This preserves their actual output and avoids additional wages. An operational site with completed batch evidence remains valid when production is disabled.
3. Save after the first voyage day, then Load/Continue the same slot. Inspect Journal and the voyage before another advance: nothing should gain an extra visit or batch during loading. At Westhaven, sell all four Bread. This is useful paid commerce, but it does not substitute for the drafted Flour destination.
4. Sail to Eastport. Load **one Silver** from the owned Mine through a physical warehouse-to-ship transfer, then buy **one Furniture** from the local market. Sail to Northport. Through Company, fulfill **one Furniture** against the finite royal Furniture contract, then exchange the physically carried Silver at the completed Mint. Inspect the Journal's public-institution completion.
5. At Northport buy **four Flour** aboard Ledger, sail to Southhaven, and sell all four there. The visit history now covers all towns, the distinct-town factories have their own funded batches, the food-source market has four net imported Flour, and the public Mint has an actual issuance. With the same step sequence the headless witness wins on day 18 with **648.6668 coins**. Results and Journal should show all four met; terminal save/load retains that frozen result.

These are reproducible player actions, not a claim that every UI platform has been exercised. Extra days, enabled production, different purchases or a different pricing/difficulty setting can change cash and completion day. Use the [actual Unity evidence](UNITY.md) to distinguish observed Editor/player journeys from this headless reference route.

This focused milestone does not retune the known long-horizon v3 circulation limitations, add captains/charter drafts/blessings, increase fleet size or add future-system placeholder controls. The objective thresholds are global provisional design choices. Representative successful runs demonstrate feasible strategies, not universal balance or enjoyable human play.
