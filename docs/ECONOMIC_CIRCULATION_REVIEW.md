# Measured economic circulation

Task 2.4 adds **economic rules revision 1** to newly started modern v3 runs. This is a measured circulation change, not a promise of permanent prosperity. The current supported game uses this revision. Older playable modes and save schemas are retired; explicit revision0 experiment controls remain diagnostic only. The baseline was `de3a80041404dca373e1ec77c5b5edf93c442f4c`, with Tasks 2.2 and 2.3 integrated.

## Supplied terminal fixture: observed facts

The [sanitized source diagnostic](experiments/circulation-day140-diagnostic.json) records the supplied Grand Commission at day **140**, revision **535**, seed **−734282136**, Industrial charter, Ascension 1, Coverage. Its status is **Succeeded**. At the time of that study, the then-current codec loaded it, matched the report's full snapshot fingerprint and reproduced the original save bytes without running a simulation command. Original save/report hashes remained unchanged. The raw local save stays private and is not committed.

This was a schema2 historical run with separate factory storage. The current schema6-only codec intentionally rejects that old raw save; the sanitized observations below remain useful economic rationale. Its cumulative accounts and six retained food observations do not constitute a complete time series. Quotes below are read-only proposals; terminal mutation remains prohibited.

| Town / role | Market coins | Household coins | Public Bread | Population / prosperity |
| --- | ---: | ---: | ---: | ---: |
| Eastport / food source | 986.7246 | 2454.7889 | 20.75 | 40 / 100 |
| Northport / forest furniture | 284.3664 | 1469.5 | 0 | 38 / 27 |
| Southhaven / processing | 9.3394 | 1223.3754 | 0 | 19 / 31 |
| Westhaven / vulnerable coast | 9.6449 | 505.325 | 0 | 20 / 26 |

Regional money is exactly **10,250 coins**: markets 1290.0753 + households 5652.9893 + NPC companies 3197.9348 + player 109.0006 + public treasury 0. The finite external authority holds 1200, giving **11,450 all-account coins**. Mint issuance and external net inflow are both zero. Money was not destroyed.

NPCs started with 1200 coins combined. Grain, Plank and Timber companies now hold 1387.0001, 811.8985 and 999.0362 respectively, with realized material margins 893.0001, 411.8985 and 712.7029. Their accounts contain real purchases and sales, no wages or construction spending. Remaining held basis (6 and 13.6667 for Grain and Timber) explains why cash growth and realized margin differ. Historical merchants had no service expenditure path. Their regional cash share grew from 11.7% to 31.2%; this fact does not justify confiscating a balance.

The player's 1200 starting cash became 109.0006 after 1383.8714 purchases, 2162.872 sales, 510 wages and 1360 construction. Realized material margin is 1076.5817. Private stores contain eight Bread, which is not public retail stock. Neither held output nor distant output feeds households without actual sale and transport.

Southhaven's latest public Sawmill outcome is **PayrollShortage**: its 9.3394 market coins cannot pay a 10-coin batch despite 9.646 Logs. Its Mill reports **InputShortage**, with zero Grain. Northport, Southhaven and Westhaven record zero Bread consumed at all six retained opportunities on days 120–140. Their cumulative missing-stock amounts are 54.65, 24.9 and 32.25 Bread; cumulative affordability shortfall is zero. Eastport consumed all 60.25 desired Bread. This distinguishes physical shortage and employer liquidity from household insolvency. A recorded latest outcome describes an earlier attempt; it is not a fresh readiness preview.

Eastport/Northport/Southhaven/Westhaven households received 1684/392/1074/216 production wages and spent 129.2111/602.5/370.6246/260.675 at retail. These are cumulative transfers, not injected allowances. The selected goals—Regional Survey, Artisan Guild, Bread for the Coast and Prosperous Enterprise—do not require every town to maintain food welfare, so their victory is consistent with these shortages.

### Exact Furniture quote diagnosis

| Town | Stock | Coverage target | Reference coins | Buy one total | Sell one total |
| --- | ---: | ---: | ---: | ---: | ---: |
| Eastport | 2.875 | 0.5 | 64 | 19.4992 | 1.4165 |
| Northport | 8 | 0.0001 | 52 | 1 | 1 |
| Southhaven | 1.75 | 0.0001 | 64 | 1 | 1 |
| Westhaven | 1 | 0.0001 | 68 | 1.0228 | 1 |

These are exact one-whole-good edge sums, not marginal price times quantity. The actual Northport private warehouse has sufficient stock/funds for both one-Furniture proposals, each quoted at one coin. Southhaven's owned warehouse can buy at one coin; its sale is rejected for missing owned Furniture. No trade was executed against this frozen save.

Coverage previously initialized a zero luxury target, skipped ineligible demand, then clamped it with `max(1, target)`. In v3, one means one **subunit**, or 0.0001 goods. Low prosperity/luxury ineligibility or exhausted hypothetical purchasing budget can therefore produce an extreme ratio between real stock and target. Other goods retain their town-specific reference curves; the source diagnostic includes every good's stock, target and current bid/ask.

## Revision 1 contracts

### Commercial handling

Successful ordinary company **sales** pay a **2% port handling fee** from their gross proceeds. Player and NPC ship/warehouse sales follow the same calculation. Buying, own-stock transfers, household retail, Mint exchange and finite public/external contracts are exempt. The town must still afford the gross purchase. Withholding the fee avoids a zero-cash seller deadlock; rejection or preview moves nothing.

For each company/town pair, cumulative fee equals `floor(cumulative gross local sales / 50)` in monetary subunits. An operation pays the difference between old and new cumulative fees. Market receipts similarly use the difference of `floor(cumulative fee / 2)`; households receive the remainder for local handling work. Prefix arithmetic preserves exact totals across order splitting, fractional warehouse quantities and different owned stores. It never tests or caps accumulated wealth.

Gross revenue and FIFO material basis remain unchanged. `FeesPaid` is a separate expense; `OperatingSurplusAfterExpenses` is realized material margin minus all wages and service fees. It excludes construction, public commissioning and Mint receipts. Account transfers and company events identify payer, receiver, port, good, day and revision. Household service income is distinct from production wages. World state retains only bounded cumulative rows; host history remains capped.

### One four-day municipal review

After ship actions and private procurement, before public production, every town reviews its accounts on days 4, 8, 12, and so on. A shadow inventory follows public facility order and reserves one enabled material/output-ready batch per facility due before the next review. A separate figure identifies ready payroll due now. Disabled, missing-input and output-limited work creates no claim.

The next full essential food budget uses current exact quotes, population and fractional demand residual. When ready payroll due now exceeds municipal cash, households may pay a levy **only above twice that food budget**. Otherwise relief may cover the household cost shortfall for **available next-opportunity food**, after protecting upcoming ready payroll. Each payment is bounded by the actual shortfall, **12 coins**, and **10% of unreserved payer cash**. Only one direction can execute per review. The two-food-budget levy and one-opportunity relief thresholds prevent a static reverse-transfer cycle. Missing food produces no relief; money alone does not supply inputs or unblock full stores.

Last reason, reserves, both transfer totals and next review day are saved even when no transfer occurs. Loading neither reviews again nor steps the day. All changes participate in the same atomic world commit and rollback as production, trade and voyages. Regional and all-account cash boundaries remain unchanged; only the existing physical Silver-backed Mint issues currency.

### Prices, finite starting imports and compatibility

Revision-1 Coverage uses a luxury price-target floor of half the authored target, at least one whole good. Current Furniture has an authored four-good target, hence a **two-good floor**. Funded eligible Coverage demand can raise it. Town reference prices and real stock still differ; sufficiently abundant inventory may legitimately reach the minimum price. The floor does not create household demand, stock or money. StockOnly and historical Coverage retain their original formulas.

Each new town starts with at least four initial essential opportunities of Bread (16 days at the unchanged four-day cadence), preserving larger existing stocks. Additional finite imported basics are two Flour at the food source, two Logs and two Grain at processing, two Planks at forest furniture, and one Flour plus one Plank at the vulnerable coast. No RNG draws, cash, recipes, recurring deliveries or paid company basis are added. Roles remain complementary and trade-dependent.

`EconomicRulesVersion=1` requires v3 storage2/itinerary1 and appends `/economy-v1` to run comparison identity. Generator/content remain3, valuation remains1. Retry and Load preserve the supported identity. Schema **6** stores exact fees, fiscal schedules and factory/warehouse automation; schemas1–5 are intentionally unsupported. Diagnostic treatment overrides are explicitly labelled and cannot be saved as a normal named Combined run.

## Matched experiments and acceptance

Run `dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --economic-circulation`; add `--check` to verify the committed [report](experiments/economic-circulation-results.json). This separate bounded matrix covers seeds −42, 42, 99 and −734282136, StockOnly/Coverage, idle/managed policies and None/FeesOnly/MunicipalOnly/Combined. Checkpoints are days 30/90/140/180/365. Sixteen matched revision-0 pooled/itinerary controls distinguish the total revision effect from the fiscal-only comparison. All new-version treatments share the same price/buffer changes.

Acceptance requires exact financial/physical/FIFO reconciliation, deterministic replay, protected reserves, real isolated payroll recovery and a measurable improvement in some ordinary worlds. It does **not** require every seed to thrive.

All 80 worlds use Merchant charter, Ascension0, Sandbox. The submitted seed reproduces geography and roles, **not** the unknown player command history of the Industrial/A1 terminal Commission. The managed policy buys four real Farm/Mill/Bakery/Lumber facilities, applies the existing stock/cash controls and physically ships Flour from processing to recovery with Logs on the return. Its thresholds are identical across matched arms. It uses no Mint or external contracts. Primary work is **29,200 world-days**, plus bounded deterministic replays and save continuations.

### Pre-Task2.8 baseline observations

The numerical observations in this section are the pre-Task2.8 baseline, retained in Git at `3d8732ca86353c41fd5bb73678ac818e95c00920`. They explain the original circulation assessment; they are not the results of the current warehouse-policy report. The [Task2.8 comparison below](#task28-warehouse-policy-comparison) describes the updated controls and observed changes.

Each row below compares against its matching revision1 None control, so finite imports and the price-target correction are held constant. Bread changes sum across16 worlds; a cash ranking is not a complete welfare ranking.

| Intervention | Paid Bread improves / worsens / equal | Aggregate paid Bread change | Tradeoff |
| --- | --- | ---: | --- |
| Fees only | 4 / 4 / 8 | +2.9171 | Some transport timing/provision worsens; −42 StockOnly managed loses6.3479 Bread,42 StockOnly managed loses5.6365. |
| Municipal only | 7 / 0 / 9 | +62.5784 | NPC cash increases in9/16 worlds because funded markets reopen real commerce. |
| Combined | 8 / 0 / 8 | +72.9141 | Player cash falls in6/8 managed worlds, rises in2; NPC cash falls in11/16 and rises in5. |

Combined versus untouched revision0 improves Bread in14/16 worlds, ties2, and adds161.9613 paid Bread. That includes the additional finite starting food and changed Furniture target, so it cannot be credited entirely to fiscal circulation or productive growth. Starting imports have zero paid company basis and are not earnings.

Concrete counterexamples matter:

- **−42 StockOnly managed:** Combined consumes205 Bread versus185.1892 under None (+19.8108). Player after-expense surplus improves16.3001 coins, but cash is19.3802 lower. Final town food-window percentages are10/7/0/0 and populations18/49/10/10. More cumulative food did not produce universal durable recovery.
- **Submitted seed, StockOnly managed:** Combined adds14.7389 paid Bread,10.6463 player cash and48.6787 after-expense surplus. Cumulative affordability shortfall falls40.0371 Bread while missing-stock shortfall rises23.4982. Additional purchasing power exposes a distinct physical bottleneck.
- **42 Coverage managed remains a failure:** Combined ends with80.0004 player cash and−511.1777 operating surplus after wages and fees. It consumes142 Bread versus140 under None. Player wages are310; service fees are39.4317 player and79.6622 NPC. Southhaven receives50.4982 in cumulative levy and no relief. These transfers do not fix the strategy's operating losses or missing food chain.

At day365, **12 of16 Combined worlds have zero food fulfillment in all four retained food windows**. No intervention establishes indefinite sustainability. Coverage and StockOnly remain measured alternatives rather than a universal ordering. The report records each town's stocks, reference prices, targets, clamps, exact current quotes, population, wages, needs, payroll/input/output-limit outcomes, blocked trades and account movements at each requested checkpoint. This supports locating the actual cause instead of diagnosing all shortages as money scarcity.

Combining mechanisms is not always the strongest food intervention: on the submitted seed with StockOnly managed policy, MunicipalOnly consumes213 Bread versus207 under Combined. The fee has an economic cost even when its recipients benefit.

Every committed operation audits all-account/regional cash, goods including pooled storage, FIFO basis/quantities, company cash including fee expense and municipal/service transfer totals. All worlds retain their initial global/regional totals because this policy uses no issuance or external inflow. Representative Turkish-culture and reversed-configuration runs reproduce exact state/events. All Combined runs save at days30 and140 and compare round-trip bytes plus seven further ordinary days. Diagnostic None/FeesOnly/MunicipalOnly configurations are deliberately not normal durable named saves.

## Task2.8 warehouse-policy comparison

Current report format3 keeps the same80-world matrix, seeds, horizons, fiscal arms and physical ship routes. It moves local trading into one policy per company/town. A good configured for both buying and selling retains at least its buy target; warehouse trading continues when a factory is disabled. Factories retain100% rate and Normal priority. No price, recipe, household, fiscal or initial-account rule is retuned. Pool policies, trading counters and blockers are recorded separately from factory rate, priority, admission credit and output.

Against the pre-Task2.8 baseline, all32 revision1 idle runs and all8 revision0 idle controls retain identical economic checkpoints. All32 managed revision1 runs and8 managed revision0 controls change. Among the32 managed revision1 runs, paid Bread increases in18, decreases in8 and ties in6; final population increases in19, decreases in4 and ties in9. Player surplus after wages and fees improves in24 cases and worsens in8. Reference Company Value improves in18 and worsens in14. These are combined effects of consolidated thresholds, single warehouse execution and independent trading; the comparison does not isolate one of those changes as the sole cause.

Selected **Combined** managed runs at day365, before → after Task2.8:

| Seed / pricing | Paid Bread | Player cash | Player surplus after wages and fees |
| --- | ---: | ---: | ---: |
| 42 / StockOnly |154 →154|860.4967 →966.9634|196.6634 →283.7967|
| 99 / Coverage |148 →142|105.2034 →80.0015|−571.6855 →−521.5765|
| −42 / StockOnly |205 →210.1877|92.6204 →80.0001|−523.992 →−540.8851|

Seed42 StockOnly improves company finances without changing cumulative food: stock shortfall remains219.0824 Bread and affordability shortfall47.0176. Seed99 Coverage has less paid Bread and a larger stock shortfall,320.7 →328.6, despite a smaller operating loss. Seed−42 StockOnly buys more food but loses more company surplus; food affordability shortfall rises53.0135 →54.7623. Its NPC cash rises4157.1813 →4316.1377 coins while municipal cash falls2747.4811 →2509.0302 and household cash rises3692.7172 →3784.832. Local trading redistribution does not establish a universal welfare gain.

Operational blockers remain visible. Most Combined managed cases record203–298 player cash-reserve waits over the year; seed42 StockOnly instead records232 destination-liquidity waits. NPC shipments repeatedly retain cargo because destination markets lack funds or current marginal bids fall below configured limits. Lower interface overhead does not remove finite supply, liquidity, wages or physical transport requirements.

All initial, checkpoint and final snapshots independently reconcile unique company/market/household/treasury/external balances to initial all-account cash plus issuance, and each company's purchases to FIFO held basis plus released/surrendered basis. These circulation policies still use no Mint or external inflow. Per-operation audits and representative replay/save checks remain part of report generation. Schema6 and richer diagnostic fields change fingerprints independently of economic outcomes. [Task2.8 verification](TASK-2.8-VERIFICATION.md) records executed checks, the real factory scarcity example and Unity evidence.

## Verification boundary

The matched report and sanitized historical diagnosis preserve the rationale and measured economic outcomes; the pre-Task2.8 numerical baseline is labelled separately from the current report. See [Task2.8 verification](TASK-2.8-VERIFICATION.md) for current checks, Editor/player evidence and limitations. The old raw day140 save is unsupported by the schema6-only codec and was not mutated or converted.
