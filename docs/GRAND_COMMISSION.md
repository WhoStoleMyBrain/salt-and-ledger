# The Grand Commission

The Grand Commission is the default scored choice for **Expanding Economy v3**. Reveal the seeded map first, then choose one objective from up to three feasible offers in each of four rounds. All four chosen cards become mandatory. Complete them **together by the configured deadline (day 720 by default)**, with **at least 100 liquid company coins**. A high partial score cannot substitute for victory.

Sandbox is the unlimited alternative. Both use current circulation rules and [shared company town stock](HARBOR.md).

## Read the map, then commit

1. **New Run** defaults to v3 Grand Commission. Select the seed, existing starting charter, pricing view and Ascension 0 or 1, then **Explore full map**. Inspect all generated towns, actual sea connections, roles, stock/accounts, public industries and permitted private facilities. The preview is the world that will be started; exact distances remain available in a secondary foldout.
2. Enter the four category rounds: Navigation / Fleet, Production / Industry, Markets / Commerce, and Civic / Capital Development. Each offers up to three distinct feasible cards from its candidate pool. An unsupported custom configuration with no feasible card in a category fails before starting.
3. Select one card per round. Backtracking changes a choice without changing offers or advancing a day. Review the full four-card commission, world identity, charter, difficulty, deadline and cash floor before Begin.
4. During play, open **Objectives / Journal** for exact conditions and blockers. The map HUD keeps a compact currently-met count and days remaining. Journal inspection pauses playback; returning preserves the relevant parent screen.
5. Save explicitly to continue the existing run. Retry Same World starts the original commission and storage rules fresh; a reroll returns to setup for a new seed and a new reviewed draft. Victory freezes immediately and presents an acknowledgment with **View results**, **Inspect objectives** and **Inspect frozen harbor**. Results retain the four cards and their final numerical progress.

Construction, trade, payroll and shipping remain ordinary [v3 operations](EXPANDING_ECONOMY.md). A map or card selection never creates materials, wages, money, visits or objective credit. The incomplete pregame selection is not a persistent save slot.

## Draft-version 2 objective catalogue

Targets below are authored for every seed, not adjusted after testing an individual world. Names of role-relative destinations resolve to the actual generated towns before selection.

| Round | Card | Required result | Progress character |
| --- | --- | --- | --- |
| Navigation | All Harbors | Visit all generated towns with owned ships; the initial dock counts | Permanent visits |
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

A New Industry evaluates the same identified Mill and Bakery for operational state and actual funded batches. Choose actual eligible Mill and Bakery towns from the map. Default specialization prevents these directly dependent industries from sharing a town. Each must have actual recipe inputs, output space and company-funded payroll. Pending construction or an unsuccessful attempt grants no batch credit. The Journal identifies the relevant facilities and shows each site and batch separately.

In a new pooled run, same-town facilities share their company's stock and material FIFO, but retain separate production counters and payroll. Default specialization requires Flour shipping between Mill and Bakery towns; shared local storage does not bypass construction eligibility. Harbor's paid local-stock trades count as ordinary town market transactions, while Load/Unload transfers confer no production or sale credit.

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

Every atomic economic operation reevaluates the conjunction of all four cards and the 100-coin company floor. Success on the exact configured deadline takes precedence over deadline defeat. Failure at the deadline, success and confirmed abandonment freeze further economic mutation. Partial score cannot grant victory, and a terminal run does not continue automatically in sandbox.

## Deterministic identity and bounded history

Generator4 and content3 retain valuation1, storage2, itinerary1 and economic rules1. Mission draft2 resolves the generated geography and capability distribution, filters structurally unavailable objectives, then uses four independent salted deterministic category streams. The configuration fingerprint and resolved card parameters belong to the plan signature and complete run identity. Charter, pricing, difficulty, culture and screen visits do not change offers for the same seed and configuration.

All Harbors targets the actual generated town count. Named destinations resolve from actual generated capabilities. Fleet cards require a sufficient configured fleet limit; industry cards require legal construction towns; supply and horizon checks remove unsupported templates. These checks establish structural opportunity, not profitability or guaranteed victory.

`CommissionHistory` belongs to the atomic regional snapshot. Its bounds derive from generated town count, configured fleet limit and canonical goods. Initial docks and committed final arrivals count; intermediate sea-path passage grants no visit or sale. The second ship must leave its yard to acquire another visit. Market trades, automation, voyages and construction retain the existing atomic commit and rollback boundary.

## Saves and compatibility

Current schema7 retains the exact draft, bounded history, all economic state and itinerary progress. Unsupported historical saves fail safely. See [save policy](SAVES.md).

## Verification

```text
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --grand-commission --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --world-generation --check
```

The commission witness resolves current generated production sites and uses ordinary construction, paid purchases, funded batches, physical voyages/transfers, finite exports and Silver issuance. It checks exact mid-voyage and terminal save restoration. Procedural integration tests cover a separate paid Mill-to-Bakery shipping journey and objective progress.

See [Task3.1 verification](TASK-3.1-VERIFICATION.md) for actual executed results and remaining Unity checks. Earlier four-town seed-specific balances, names and completion days remain historical evidence in Git; they are not generator4 predictions. The longer provisional horizon does not increase objective quantities or finite external budgets.
