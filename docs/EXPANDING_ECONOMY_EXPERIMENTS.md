# Expanding Economy experiments

Task 2.0 tests a productive economy through actual commands, including its failures. A challenge victory certifies its stated finite objective; it does **not** certify perpetual prosperity. Reference Company Value is also not liquid cash or household purchasing power.

## Reproduce

```text
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --deep
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --reinvestment --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --reinvestment --deep
```

The normal report is [expanding-economy-results.json](experiments/expanding-economy-results.json). Normal CI checks its exact bytes. Deep output defaults to ignored `.cache/expanding-economy-deep.json`; add `--check` after `--deep` to reproduce an existing local deep report. Runtime and allocation measurements print to the console and are excluded from deterministic JSON. No date, machine path or timing is part of the golden report.

`samples/SaltAndLedger.Demo/ExpandingEconomyExperiments.cs` owns the prescribed policies. These call the same construction, transfer, trade, departure, automation, save and public-economy APIs as the playable world. They never assign stock, cash, population, challenge progress or quote parameters. Every committed v3 operation invokes the authoritative physical, FIFO and monetary audit. The additional observer checks regional/global cash, gathers actual attempts and flows, and stores checkpoints at days 30, 90, 180 and 365. Deep runs add day 1095.

The normal matrix uses seeds **-42, 42 and 99**, both StockOnly/Coverage pricing hypotheses, both Ascension 0/1 timings, and thirteen fixed strategies. It adds the Industrial and Independent charters on seed 42. The deeper matrix uses fifteen signed seeds including both int32 extremes, both pricing hypotheses and ascensions, six representative strategies and a 1,095-day horizon. There is no reroll or per-seed tuning.

All v3 JSON goods/cash are raw **1/10,000** units. Authored price coefficients remain money subunits per goods subunit, numerically coins per whole good. V2 controls explicitly retain their historical whole-unit interpretation. Never compare the raw v2 and v3 integers without their unit scales.

## Fixed policies and controls

| Policy | Actual investment and behavior | Question tested |
| --- | --- | --- |
| `idle` | No player commands; finite NPC economy continues. | What does the untreated v3 world do? |
| `imports` | Bread from food town to recovery; empty return. | Can one-way essentials drain the receiving town? |
| `balanced` | Same Bread leg, with purchased recovery Logs on the return. | What does a complementary export improve, and where is it insufficient? |
| `industry` | Farm at food source, Mill and Bakery at processor; ship Bakery Bread to recovery and bring its Logs back. | Does actual additional Flour/Bread production improve provisioning? |
| `overbuilt` | Four private Mills and Bakery, without a private Farm. | Do additional machines remove missing sources or working-capital constraints? |
| `external` | Physically acquire/deliver the finite royal contracts. | How much regional liquidity can the finite outside account import? |
| `external_control` | Matched voyages and purchases, selling into ordinary local markets. | What changes without external procurement? |
| `monetary` | Mine, public Mint, second ship, physical Silver delivery, Furniture exports and Planks public works; starter runs balanced trade. | Does explicitly issued currency solve food without productive inputs? |
| `monetary_control` | Same mine/second ship/merchant opening, without commissioning or using the Mint/contracts/public works. | What happens when metal has no authorized conversion outlet? |
| `network2` | Farm/Mill/Bakery expansion, second ship carrying Farm Grain to Mill warehouse; first handles Bread/Logs. | What does physical upstream capacity enable, and can the company finance held output? |
| `network3` | Same network, attempts a third ship only with at least 800 coins available. | Does a desired fleet expansion actually become affordable? It is not counted as three ships merely because the policy intended to buy one. |
| `circulation` | Farm/Mill upstream, Bakery and Lumber Camp **at recovery**, physical Flour delivery and Logs export; explicit stock/cash batch controls. | Does productive local employment extend paid welfare without new currency? |
| `fleet3` | Pays for both additional ships immediately, then Mill; independent Bread/Logs, Bread/Flour and Bread/Logs routes serve three destinations. | Do three real routes justify their capital and working-cash burden? |

The demand-managed `circulation` policy enables its Farm while local public Grain is below 24, Mill while Flour is below 12, recovery Bakery while Bread is below 24, and recovery Lumber Camp while Logs are below 12; each also needs company cash of at least 120 coins. The controls toggle real existing facilities, preserving their production deadlines. It buys up to four Flour at the processor (minimum one, marginal maximum 22), delivers it into the recovery Bakery warehouse, and returns with up to eight purchased recovery Logs (minimum one, buy maximum 12, sell minimum 1); reserve is 100 coins. Bakery buys local Logs and sells its real Bread. No output is granted and no wage is paid for a blocked/disabled batch.

The unmodified `industry`, `network2` and `network3` failures remain in the report alongside this more deliberate strategy. This is a policy comparison, not an economic-rule change made to conceal the first result.

## Measured productive recovery and its limit

The seed-42 / StockOnly / Ascension-0 / Merchant `circulation` run provides a useful, finite success case without Mint or external money:

| Day | Company cash | Company Value change | Cumulative private wages | Recovery population | Recovery prosperity | Paid-food window | Recovery household cash |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 30 | 832.6666 | +182.6666 | 148 | 20 | 45 | 66% | 474 |
| 90 | 1,139.8665 | +469.8665 | 352 | 23 | 88 | 100% | 356.4 |
| 180 | 1,195.9665 | +525.9665 | 454 | 28 | 81 | 12% | 0.0015 |
| 365 | 1,213.2997 | +503.2997 | 466 | 18 | 0 | 0% | 0.0015 |

The first recovery affordability failure occurs on **day 160**. Its early missing-food problem is resolved through actual Flour voyages, local paid Bakery batches and retail; its later problem is household income relative to increasingly expensive/population-scaled needs. The remaining food and market money cannot be spent on behalf of households automatically. The company still has cash and productive reference assets when the household account is exhausted. This is meaningful paid recovery over a measured period, not a proof of permanent regional growth.

The original industrial network exposes a different failure. At day 120 it retains unsold Bread aboard its first ship at a cash-poor recovery market, holds substantial Bread/Grain/Flour in private stores, and keeps paying for production until company cash is almost exhausted. A high reference valuation of those holdings cannot satisfy either the cash solvency criterion or actual paid-food fulfillment. Warehouses, transport, local buying power and output sale limits become the next bottlenecks after adding a Mill.

The additional capacity is nevertheless physical and measurable. In the same seed 42 / StockOnly / A0 world, the idle control produces **33 Flour and 80 Bread** across all public facilities through day 365. Farm/Mill/Bakery `industry` produces **65 Flour and 160 Bread** across the whole world, including 16 private Flour and 52 private Bread. Physical upstream transport in `network2` raises totals to **97 Flour and 168 Bread**, including 55 private Flour. Four-Mill `overbuilt` produces just 8 private Flour total; its extra Mills first encounter missing inputs on days 2/2/6, and by day 365 it holds 501.8501 coins of historical material basis while cash is 68.0017. These are added complete batches and paid wages, not extra opening inventory; extra machinery is neither an unlimited source nor guaranteed liquid earnings.

The three-ship `fleet3` policy really commissions both ships and moves all three along independent routes. On seed 42 at day 30, the four towns' food windows are 93–100%, company cash is 500.5335 and reference value has grown 200.5335. By day 90, household affordability and costly held cargo undermine that broad coverage. At day 365, cash is 99.0006, value change is −408.2532 and all towns' recent food fulfillment is zero. More ships remove neither missing wages nor depleted working capital.

The first full matrix also shows **Mint alone is not food recovery**. On seed 42 the monetary strategy eventually reaches all 24 authorized batches: 24 physical Silver reserved, 1,920 coins issued in the exact 1,440 supplier / 480 treasury split. Despite positive company value, its towns' food windows eventually fail. The finite institution never replenishes authorization, buyer budgets, food or input stocks.

Pricing remains a hypothesis comparison, not a declared universal winner. For the same seed 42 / A0 / Merchant opening:

| Strategy / pricing | Day-90 target food | Day-90 household cash | Day-90 company cash | First target affordability failure |
| --- | ---: | ---: | ---: | --- |
| Balanced / StockOnly | 31% | 12.0022 | 2,074.8667 | Day 68 |
| Balanced / Coverage | 100% | 3.7238 | 2,407.1968 | Day 92 |
| Circulation / StockOnly | 100% | 356.4 | 1,139.8665 | Day 160 |
| Circulation / Coverage | 100% | 859.9 | 174.6665 | None through day 365; later food fails from missing supply instead |

Coverage can delay household insolvency while making a fixed private policy less profitable. StockOnly can leave a profitable company facing households with empty wallets. No-funds-failure is also not synonymous with food success: a rich household cannot purchase absent Bread. These paired accounts and physical outcomes matter more than a single wealth ranking.

The seed-42 external-contract trial imports **710 existing coins** from the finite 1,200-coin outside account through actual deliveries; it does not issue coins and leaves 490 outside. Its company operating surplus is 215.0006, versus −112.1 for the no-contract physical-commerce control. Neither outcome guarantees food recovery. The issuer experiment's supplier receipts remain separate from material trading margin and operating surplus after wages.

## Objectives and exact continuation

All three distinct objectives have observed seed-42 / StockOnly / Ascension-0 / Merchant victories, including two managed-policy alternatives. Their thresholds were **not adjusted** to obtain these outcomes:

| Objective | Strategy | Victory day | Cash | Company Value change |
| --- | --- | ---: | ---: | ---: |
| Sustainable Provisioning | `industry` | 36 | 141.6671 | −78.3329 |
| Sustainable Provisioning | `circulation` | 36 | 927.9999 | +237.9999 |
| Industrial Network | `network2` | 14 | 129.3337 | +122.3337 |
| Industrial Network | `circulation` | 28 | 762.6666 | +127.6666 |
| Royal Commission | `monetary` | 24 | 939.0004 | +259.0004 |

The final 60-challenge matrix found the corresponding successful strategies above work for **all three known seeds and both ascensions**. The managed circulation policy is a second tested option for both Sustainable Provisioning and Industrial Network. All eighteen idle challenge controls failed at their deadlines. `industry` still failed the Industrial Network objective in all six worlds, even after producing and selling the requisite goods; it failed the separate cash/value predicates. `network2` failed Sustainable Provisioning for seed 42 under both ascensions, while succeeding in the other four cases. These are differentiated objective outcomes, not equivalent Bread-delivery counters.

Save comparisons occur at days 3, 17 and 61 for two-/three-ship and monetary strategies when reached. Each comparison retains the uninterrupted world, restores a separate one, runs the same next seven days of strategy commands **or until terminal**, and compares every committed event sequence and complete serialized state. It then resumes the report's original saved point. Day-3 monetary saves include a pending second ship and voyages; day-17 saves include multiple simultaneous voyages after issuance. Exact comparisons include fractional inventories, all money boundaries, quote parameters, FIFO intervals, controls, Mint reserve/authorization, finite contracts and challenge progress. The simulation has further targeted hostile/corrupt-save and pending-facility fixtures.

## Three-year sample: the remaining economic limit

The opt-in deep sweep completed **362 sandbox comparisons, 60 challenge cases and six v2 controls**, covering **401,537 primary v3 world-days** in **1,191.515 seconds**. It includes fifteen seeds, both int32 extremes, both pricing modes, both ascensions, and 1,095-day runs. All cash/physical/FIFO audits, representative reverse-configuration and Turkish-culture replay assertions passed. Peak operation size remained 52 observed events; peak retained FIFO lots was 69. There were 577 exact saved-state/event continuation comparisons, each bounded to seven more days or the terminal result.

The six prescribed Merchant policies each have sixty matched worlds (15 seeds × 2 pricing modes × 2 ascensions):

| Policy | Day-90 target food ≥80% and retained population | Day-1095 company cash ≥100 | Day-1095 positive Company Value change |
| --- | ---: | ---: | ---: |
| Idle | 0 / 60 | 60 / 60 | 0 / 60 |
| Balanced commerce | 18 / 60 | 60 / 60 | 60 / 60 |
| Local employment / circulation | 58 / 60 | 47 / 60 | 25 / 60 |
| Two-ship production network | 30 / 60 | 0 / 60 | 36 / 60 |
| Three-ship commerce | 6 / 60 | 39 / 60 | 2 / 60 |
| Monetary pathway | 18 / 60 | 60 / 60 | 60 / 60 |

**Every one of these 360 fixed-policy worlds ultimately has zero recent paid food in all towns, and all towns reach the population floor of 10 by day 1095.** All recovery targets already have zero recent paid food at day 365. All sixty monetary trials consume the finite 24-Silver / 1,920-coin authorization and still fail to sustain food. Added currency is useful capital, not a replacement for viable employers, inputs or logistics.

The managed policy's two day-90 exceptions are seed **−1, StockOnly, both ascensions**: target fulfillment is 70%/69%, with the first affordability failure at day 80. Those worlds remain in the report unchanged. Other seeds' initial populations and liquidity are not secretly retuned to improve the score.

These results establish real finite-period productive recovery and distinct achievable objectives. **Sustained three-year expansion remains unproven.** They do not prove every legal strategy must fail: the fixed monetary policy does not reinvest its receipts into a complete food chain, while the food-network policies do not combine their expansion with the monetary pathway or continuously rebalance all four towns. The separate controlled follow-up below tests that combination using unchanged rules; it preserves these completed original comparisons.

Deep output is the ignored 58,580,975-byte `.cache/expanding-economy-deep.json`, SHA-256 `d7ccc304a1aee0a1f044c9fdcd177874873645cdd6d232a41948402a6b68d078`. The complete deep matrix was generated once; its internal representative replay checks passed. A second full `--deep --check` execution was not performed. The normal matrix was independently replayed in full with `--check`.

## Controlled monetary reinvestment follow-up

The separate [reinvestment report](experiments/expanding-reinvestment-results.json) contains twelve matched Merchant worlds: seeds −42/42/99, both pricing modes, both ascensions, through day 365. Generate or verify it with `--expanding-economy --reinvestment [--check]`; add `--deep` before `--check` for the optional twelve-world, 1,095-day version. This leaves the original normal/deep reports and their strategies unchanged. Generation took 93.401 seconds while the deeper follow-up also ran; the normal artifact is 1,930,744 bytes. Its 48 save checks include day 181, after the monetary ship's change of role, and compare seven further days of exact state and events. A reversed-configuration replay also passes.

The fixed policy first uses the same paid Mine, public Mint, second ship and balanced starter route as `monetary`. From day 12 it buys a recovery-town Bakery when company cash reaches 550 coins, then a Lumber Camp at 470 coins. From day 24 it buys a Farm/Mill pair and attempts the third commission at 1,250 coins; a Planks-blocked commission is retried only while at least 750 coins remain. It then runs actual warehouse Grain→Flour→Bread deliveries. Farm stock above twelve Grain can sell locally. After three ships really complete, a further 1,000-coin threshold permits a second Mill/Bakery pair in the processing town. No threshold depends on seed or observed victory.

Each shipping policy has a real return: the Grain ship buys processing-town Bread for the food-source town, and the Flour ship buys recovery-town Bread for the processing town. After the full 24-Silver authorization, the monetary ship physically carries unused Silver back to its Mine warehouse, then changes to forest Logs→recovery Bakery / recovery Bread→forest commerce. The Mine stays subject to its normal stock ceiling. Production toggles use the same 120-coin cash threshold and explicit stock targets: market Bread 34, market Flour 12, recovery Logs 14, own Farm Grain 16 and own Mill Flour 10. Ships retain a 100-coin reserve; no funds, goods or route state are repaired.

The two processing-town results below are **current six-observation paid-food windows at day 365**, not a claim of uninterrupted perfect welfare throughout the year. Town roles come from each seed's authoritative generation, not a fixed town-name assumption. In seed 42 the processing town is Southhaven (initial population 20); in seed 99 it is Northport (initial population 30). Northport is **not** seed 99's vulnerable recovery town.

| Seed / pricing / ascension | Processing population / food at day 365 | Vulnerable recovery food | Company cash | Company Value change | Private Flour / Bread produced |
| --- | --- | ---: | ---: | ---: | ---: |
| −42 / StockOnly / 0 | 36 / 0% | 0% | 245.4833 | +2,743.6853 | 98 / 232 |
| −42 / StockOnly / 1 | 39 / 0% | 0% | 103 | +2,226.6662 | 118 / 268 |
| 42 / StockOnly / 0 | 36 / 100% | 56% | 356.1313 | +2,419.1313 | 156 / 380 |
| 42 / StockOnly / 1 | 34 / 100% | 54% | 207.2684 | +2,190.2684 | 165 / 384 |
| 99 / StockOnly / 0 | 46 / 100% | 41% | 1,265.9018 | +3,203.9018 | 166 / 398 |
| 99 / StockOnly / 1 | 46 / 100% | 38% | 1,110.2649 | +3,048.2649 | 167 / 402 |
| All six Coverage cases | 0% food in all towns | 0% | 78.0010–445.9595 | Positive reference value | 28–54 / 96–150 |

All twelve worlds really own three ships and issue exactly 1,920 coins from 24 physically delivered Silver; this does not make every strategy solvent or every town healthy. At day 365, seed 42 StockOnly A1 also has 100% food in the food-source town, while seed −42's forest town has partial food (57%/28% under A0/A1). The four processing successes are further evidence of productive circulation, **not** four wins of the separate vulnerable-city recovery objective.

Seed 99 StockOnly A0 shows the trajectory: private wages paid reach 126, 634, 1,768 and 3,848 coins at days 30/90/180/365; processing-town food is respectively 100%, 61%, 81%, 100%, and population 31/34/34/46. At day 365 its households retain 114.2736 coins and its market 228.9059. The second local employer and physical imports produce a real later recovery after earlier affordability failures; Company Value includes the purchased facilities/ships rather than equating cash spent with lost assets.

The optional reinvestment deep run completed all twelve worlds through day 1,095: **13,140 primary days in 106.863 seconds**, peak 51 events/59 open FIFO lots. All twelve ultimately reach zero paid food and population 10 in every town. In seed 99 StockOnly A0, private output stops at 227 Flour/524 Bread with 4,984 wages paid: company cash reaches its roughly 100-coin reserve and the policy disables employers below 120 coins. Idle physical factories and ships still exist. The three NPC companies hold **8,483.6664 coins**, the player 100.0282, markets 3,436.3012, treasury 830 and all households combined only 0.0042. Regional cash remains exactly 12,850 = initial 10,530 + 1,920 issued + 400 imported; the outside authority retains 800. The processing market has only 15.6115 coins, while the other markets retain most remaining market money. Wealth has accumulated in accounts that this fixed policy no longer draws into employment and paid food.

This identifies a working-capital/distribution and policy-reserve failure after a longer productive period, not missing physical capacity or lost money. An illustrative fully private Farm→Mill plus Lumber→Bakery chain pays 19 coins of wages per two Bread before transport/trade spreads, distributed across several towns; a single importer's household income is not automatically that entire amount. High retail prices can outpace local earned income, while low coverage-adjusted sale prices can fail to fund company payroll. Prescribed routes also continue buying inputs from public markets and accumulating NPC trading profits. These are measured constraints and useful next calibration/policy questions, **not a proof that no legal recovery command remains**. Treasury procurement, different stock/price/reserve choices and complementary nonfood exports were not exhaustively optimized. Neither hidden subsidies nor further rules were introduced to make the follow-up pass.

The follow-up deep artifact is `.cache/expanding-reinvestment-deep.json`, SHA-256 `16f747c1645f409b92739a40a7dfb058ef342f84c8a754584b262367612d4341`; it was generated once, with internal reversed replay and save/event checks. A second complete deep freshness run has not been executed. The normal follow-up **independently passed `--check` in 96.839 seconds**, SHA-256 `f9bc58d86ecbdd953fd76853ebe14f18e47efc6178e994cc35babcce63d4d62a`. Final headless verification after these fixtures and the stricter save-lifecycle tests passed **794/794, zero skips, in 119 seconds**; eleven of those are experiment cases. An initial new test incorrectly assumed seed 99's Northport was its vulnerable role; the test was corrected to authoritative `ProcessingPortId`, with no economic changes.

## Evidence status and interpretation

The final normal matrix generated **158 sandbox comparisons, 60 challenge trials and six historical v2 controls**, totaling **62,817 primary v3 world-days in 279.334 seconds** on this development machine. Peak committed operation size was 52 observed events and peak retained open FIFO lots was 58. This timing includes observer aggregation, repeated exact save checks and report serialization; it is not stepping cost alone. The deterministic report is 24,466,723 bytes.

The separate 180-day stepping sample enables actual routes, excludes observer serialization and measures thread allocation. Each v3 world includes the commit audits; these results were obtained while the separate deep sweep was also running, so they are observations rather than isolated benchmark guarantees:

| World | Milliseconds/day | Allocated bytes/day | Final retained FIFO lots |
| --- | ---: | ---: | ---: |
| V2, one owned ship + three NPCs | 0.3169 | 237,670 | 3 |
| V3, one owned ship + three NPCs | 0.5026 | 270,548 | 3 |
| V3, two owned ships + three NPCs | 0.8993 | 343,101 | 4 |
| V3, three owned ships + three NPCs | 0.9316 | 431,467 | 5 |

Ten thousand reads of the already committed snapshot allocated 40 bytes total, including the measurement timer, in every sample. Their measured elapsed times ranged from 0.04 to 5.25 milliseconds, including first-loop JIT effects. Reads return the committed immutable snapshot; daily staging and commit are the allocation-bearing operation. Exact held FIFO lots remain preserved; the world does not retain a growing list of daily events.

The separate normal **`--check` passed** with exact report bytes (504.826 seconds under concurrent deep/build workload), then **passed again in 345.259 seconds** after the final stricter save-lifecycle validation. Its SHA-256 is `f13988d7005ea58d2842bdd5e3572e8a11cfefd42a0d1b4a17ad2fb891af0b21`. Final headless verification passed **794/794 tests**, including eleven experiment-policy cases. The completed deep results are recorded above, in `tasks/TASK-2.0-PROGRESS.md` and in the [acceptance audit](TASK-2.0-VERIFICATION.md). Economic results remain provisional evidence, not a balance guarantee or large-world certification.

The report separately preserves actual company accounts, market and household balances, treasury/external cash, issued/imported currency, physical recipe/extraction/consumption flows, FIFO and wage totals, all owned cargo/sites/pending capital, route stages, production waits, finite entitlements, initial populations and current welfare. Fixed v2 controls and all seven earlier golden reports remain the historical comparison; the v3 experiment must never regenerate them under new rules.

The immediate design question raised by these results is how to sustain productive income and distribute real food to all four roles after the initial capital period. More initial coins, hidden stock repair or automatic Bread income would conceal the measured constraints. Future policy/balance work should instead isolate working-capital allocation, employer output markets, public/private wage funding, route inventory limits and the genuine input/output chain one variable at a time. No further economic systems are implemented by this report.
