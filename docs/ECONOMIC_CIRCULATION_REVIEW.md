# Measured economic circulation

Task 2.4 adds **economic rules revision 1** to newly started modern v3 runs. This is a measured circulation change, not a promise of permanent prosperity. Existing Classic, seeded-v1, Living Cities-v2 and v3 revision-0 runs retain their original rules, exact reports and saves. The baseline was `de3a80041404dca373e1ec77c5b5edf93c442f4c`, with Tasks 2.2 and 2.3 integrated.

## Supplied terminal fixture: observed facts

The [sanitized source diagnostic](experiments/circulation-day140-diagnostic.json) records the supplied Grand Commission at day **140**, revision **535**, seed **−734282136**, Industrial charter, Ascension 1, Coverage. Its status is **Succeeded**. The current codec loaded it, matched the report's full snapshot fingerprint and reproduced the original save bytes without running a simulation command. Original save/report hashes remained unchanged. The raw local save stays private and is not committed.

This is a schema-2 historical run with separate factory storage, not a pooled itinerary run. Its cumulative accounts and six retained food observations do not constitute a complete time series. Quotes below are read-only proposals; terminal mutation remains prohibited.

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

`EconomicRulesVersion=1` requires v3 storage2/itinerary1 and appends `/economy-v1` to run comparison identity. Generator/content remain 3, valuation remains 1. New host setup selects it; Retry and Load preserve the original identity. Schema **5** stores exact fees and fiscal schedules; schemas 1–4 retain their previous interpretation and bytes. Diagnostic treatment overrides are explicitly labelled and cannot be saved as a normal named Combined run.

## Matched experiments and acceptance

Run `dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --economic-circulation`; add `--check` to verify the committed [report](experiments/economic-circulation-results.json). This separate bounded matrix covers seeds −42, 42, 99 and −734282136, StockOnly/Coverage, idle/managed policies and None/FeesOnly/MunicipalOnly/Combined. Checkpoints are days 30/90/140/180/365. Sixteen matched revision-0 pooled/itinerary controls distinguish the total revision effect from the fiscal-only comparison. All new-version treatments share the same price/buffer changes.

Acceptance requires exact financial/physical/FIFO reconciliation, deterministic replay, protected reserves, real isolated payroll recovery and a measurable improvement in some ordinary worlds. It does **not** require every seed to thrive.

All 80 worlds use Merchant charter, Ascension0, Sandbox. The submitted seed reproduces geography and roles, **not** the unknown player command history of the Industrial/A1 terminal Commission. The managed policy buys four real Farm/Mill/Bakery/Lumber facilities, applies the existing stock/cash controls and physically ships Flour from processing to recovery with Logs on the return. Its thresholds are identical across matched arms. It uses no Mint or external contracts. Primary work is **29,200 world-days**, plus bounded deterministic replays and save continuations.

### Observed day-365 changes

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

## Executed verification

| Gate | Actual result |
| --- | --- |
| Locked restore / Release portable build | Passed; netstandard2.1/C#8 retained, zero build warnings/errors |
| Full .NET10 suite | **1040/1040**, zero failures/skips,5m45s |
| Focused core / persistence-generation / experiment cases | 23 /42 /3 passed; included in full suite. Final experiment projection rechecked3/3 after report completion. |
| Canonical validation / shared generated references | Passed;59 reference files, current offline encyclopedia |
| Python suite | **58/58**,37.166s |
| Strict MkDocs / final Unity staging freshness | Passed; no Pages publication |
| Basic/world/market/shipping/automation demos | Passed |
| All historical report checks | Pricing, archipelago, enterprise, industrial logistics, runs, seeded runs, Living Cities, expanding economy, reinvestment and Grand Commission passed unchanged |
| New circulation report | Independent byte check passed,102.367s;80worlds,29,200primarydays,32primary save continuations; observed peak58events/operation and10open FIFO lots |
| Actual Unity6000.6.6f1 Edit / Play suites | **98/98** and **74/74**, zero failures/skips; all4new circulation host tests passed |
| Windows x64 Mono development build | Built and exercised; fresh process continued its actual saved voyage |

The original fixture was revalidated on final code: same report fingerprint and original save bytes, without issuing any command against it. Schema1–4 golden hashes were produced from the preserved prechange simulation assembly and verified against the new codec. The259-seed generation sample retains geography, role assignments, cash and production capabilities. Focused tests prove fractional/split gross-net settlement, NPC/player parity, real payroll recovery, protected poor households, absent-food refusal, no static fiscal oscillation, culture/reversed replay, numeric overflow and rollback after earlier staged transfers. A genuine ordinary day90 levy save continues exactly through98.

### Actual Editor and Windows player journey

The Editor and player used visible UI Toolkit navigation, pointer and keyboard events through the official Unity CLI. No economic mutation API or artificial stock/cash adjustment drove the journey. Observed frame counters advanced; opening views and changing text scale left economic time unchanged.

Both started a fresh **seed42 / Industrial / Ascension0 / Coverage** sandbox, explored the map, selected company local stock in Harbor and bought0.5Grain for8coins. Its subsequent sale preview showed7gross,0.14fee and6.86net, split0.07market/0.07workers. Executing it ended with1198.86cash,0.14fees and−1.14after-expense surplus: the one-coin material loss and handling expense remain separate. Neither transaction spent a day.

After four ordinary Next day clicks, two actual starter Bakery batches had consumed its granted Flour/Logs, produced4Bread and paid12wages. Cash was1186.86. The selected town screen displayed its0.07/0.07receipts,24.5food reserve,20ready payroll reserve, last reviewday4 and next reviewday8. This well-funded town required no levy or relief in the short UI journey; positive recovery transfers were verified by the core tests and longer ordinary-command matrix, not claimed from this screenshot.

Editor local save/load was byte-identical. The player repeated the same commands, saved day4, exited through the UI, then a new process chose **Continue saved voyage**. Editor, player and restarted player day4 save bytes were identical (`258f35a991b929011bc3a3cc8f2ea34edb92d9a3c1d8918aee4aeaf90ca8bc17`). Four more Next day clicks produced identical Editor/player day8 bytes (`079beca6f4b8e332e6f2570c8408986e61e68bdce605c4896e97f1a91916432d`), also matched by loading the actual day4 save headlessly and advancing four ordinary days. Reviews and fees were not replayed on load. The restarted player exited with code0; both logs contain the normal UI exit marker.

Rendered town accounts, exact trade previews and pricing explanations were reviewed at **1366×768 Default** and **1920×1080 Large**. Internal scrolling exposes the complete order and price information while Back/Close remain reachable. The pricing foldout's focused heading needed a narrow contrast correction, verified in the final player. A first hidden-window screenshot was black; it was discarded, the actual window resolution was set explicitly, and all final images below were captured and reviewed after rendering resumed.

| View | Default1366×768 | Large1920×1080 |
| --- | --- | --- |
| Exact Harbor gross/fee/net previews | [Player](images/task24/player-harbor-default.png) | [Player](images/task24/player-harbor-large.png) |
| Town fees, food/payroll reserves and review | [Player](images/task24/player-city-default.png) | [Player](images/task24/player-city-large.png) |
| Furniture reference64, target2 and price bounds | [Player](images/task24/player-prices-default.png) | [Player](images/task24/player-prices-large.png) |
| Material loss, fees and operating surplus | [Editor Ledger](images/task24/editor-ledger-default.png) | Covered by automated layout/navigation and live Large controls |

The player retains the pre-existing optional AMDUnityPlugin/NVUnityPlugin architecture-load warnings and ComputeBuffer cleanup warning. The development-console overlay was closed for readable screenshots; original warnings remain in local logs. No managed gameplay exception was found. Hardware mouse/keyboard input, IL2CPP and non-Windows players were not verified. The short live journey does not substitute for a live365-day playthrough. This task adds no claim of perpetual sustainability or full economic balance.

## Criterion-by-criterion acceptance

The complete original Task2.4 specification was reread before this audit. Each requirement is checked against implementation and the executed evidence above.

| Criterion | Evidence / verdict |
| --- | --- |
| A1: safe fixture diagnosis and fresh reproduction | Read-only codec/fingerprint/byte validation; tagged sanitized source; exact balances, all-good quotes and distinct blockers; no inferred history. Fresh matrix uses ordinary named worlds. Complete. |
| A2: matched arms/seeds/prices/policies/checkpoints | All four seeds, two prices, idle/managed and four fiscal arms plus16old controls; checkpoints30/90/140/180/365 and all requested sectors/flows. Checked report. Complete. |
| A3: measured criteria and adverse cases | Compatibility/conservation first; quantified stock/funds/payroll separation, FeesOnly regressions and surviving operating failures. Complete. |
| B1: bounded real fees, events and affordability | Shared authoritative gross trade plus exact withheld settlement; named receiver events; no upfront seller trap; insufficient gross buyer funds rejects intact. Complete. |
| B2: same company rules, no wealth cap | NPC/player ship/warehouse parity tests and actual matrix receipts; no wealth threshold or privileged company branch. Complete. |
| B3: separate expenses and no duplicate charges | Ledger/Harbor screenshots, fee prefixes, FIFO/loss/split tests and exact save continuation. Complete. |
| C1: one periodic rule with real reserves/caps | Four-day shared readiness review; twice-food levy reserve, payroll-protected available-food relief,12coin/10%/shortfall caps and one direction. Tests and town UI. Complete. |
| C2: real recovery, no missing-input/food rescue | Matched payroll recovery/protected reserve tests, missing-stock refusal and measured long-run failures. Complete. |
| C3: monetary boundaries, bounded work/state and dates | Commit/experiment audits, bounded company-town rows and shadow production, persisted review date, atomic rollback. Complete. |
| D1: luxury-target pathology and honest prices | Version1two-good Furniture floor; exact nearzero/target/highstock/split tests; both actual price views. Abundant real stock can still reach a legitimate floor. Complete. |
| D2: finite role imports and dependencies |259-seed exact band/capability checks; initial Bread on map preview; no recurring grant or recipe change. Complete. |
| D3: explicit identity, defaults and legacy saves | Economic rules1/schema5; exact schemas1–4 golden bytes, historical reports, old Retry/new setup Play tests, restarted player continuation, generated help. Complete. |
| D4: exact arithmetic, previews and geography | Existing shared edge-price engine unchanged; fractional/dynamic fee splitting and retail residual regressions; unchanged259-seed geography/roles. Complete. |
| E: no false balance claim | All measured improvements and losses above;12/16Combined worlds still have zero final all-town food fulfillment. Complete. |
| Verification1: safe tagged source report | Sanitized diagnostic included with this milestone; raw save remains ignored/private and unchanged. Complete. |
| Verification2: A/B invariants, culture/replay and both company types |80world checked matrix, per-operation audit and representative full culture/reverse-order replay. Complete. |
| Verification3: actual recovery and protected poor households | Focused tests fund real batches/retail, retain no-reserve blockage and reject absent-stock relief. Complete. |
| Verification4: edge values, costs, timing and no repeated charges |23core tests,42persistence/generation cases,32matrix continuations and actual cross-process UI continuation. Complete. |
| Verification5: target edges and initial bands | Exact target/price split tests and259seed checks. Complete. |
| Verification6: modern/legacy defaults, reports, freeze and exact UI | Original frozen fixture unchanged; all old goldens; new schema5 terminal/retry tests and visible fee/ledger controls. Complete. |
| Verification7: actual target displays and pipeline | Reviewed Editor/player renders at both requested sizes;1040.NET/58Python/98Edit/74Play; docs/staging passed; bounded29,200primarydays. Complete within Windows Mono scope. |

All DoD clauses are met: auditable NPC/player fees, one bounded municipal mechanism, versioned price correction, finite plausible shelves, safe old/new saves, measured A/B tradeoffs, no fabricated funds/wealth cap/universal recovery claim, and unchanged historical reports and deterministic geography. The remaining limitations are the explicit platform/input scope and continuing economic failures described above. A useful next task is a similarly bounded comparison of productive food logistics and fee burden against MunicipalOnly; additional currency mechanisms would not address the observed missing-material chains by themselves.
