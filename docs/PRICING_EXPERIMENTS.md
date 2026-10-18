# Task 1.2b: pricing experiments

**Provisional recommendation: use the linear model for the next controlled gameplay experiment.** It makes inventory and order size matter with a single constant slope before rounding and bounds. The scarcity alternative creates much stronger pressure near depletion, but its threshold and ceiling need more explanation. Neither model is established as balanced or enjoyable.

## Reproduction and evidence

Run from the repository root with the .NET 10 SDK:

```text
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --pricing
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json --pricing --check
```

The first command prints a comparison table and regenerates [the machine-readable report](experiments/pricing-results.json). The second compares it without writing. An optional final path selects another JSON output or comparison file. CI runs the check command. The original production, `--world`, and fixed `--market` demos remain separate regression scenarios.

The report contains 165 observations and 1,883 invariant checks per run. The runner repeats the entire experiment and repeats it under `tr-TR`; all three serialized reports must match. It records parameters, stock, quotes, first and last marginal execution prices, quoted and executed totals, outcomes, cash changes, and price impacts. Null prices mean no stock-feasible marginal quote or no calculated execution price; zero executed amount means no payment. Exceptions are explicit rows with unchanged state. A failed assertion stops the command before replacing the report. The successful artifact has no invariant violations.

Every price and preview comes from the simulation API used by execution. The runner does not implement another pricing formula. The tables below summarize actual report rows, whose freshness is checked against executable code. The JSON contains no timestamps, timing measurements, machine paths, or random inputs. This report concerns controlled scenarios, not a saved-world format or an economic forecast.

## Compared hypotheses and parameters

All three models use the same content, initial stock, cash, and ordered commands in each scenario. Cash uses indivisible integer units. Parameters are experimental runtime configuration, not canonical good definitions.

| Good | Reference price `P` | Target stock `T` | Sensitivity `k` | Minimum | Maximum | Spread `s` |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Logs | 100 | 100 | 80 | 5 | 400 | 2 |
| Planks | 200 | 50 | 160 | 10 | 800 | 4 |

- **A / Fixed:** bid/ask are `P-s` / `P+s`: logs 98/102, planks 196/204. Other parameters do not affect fixed execution. Existing fixed-mode semantics remain available; these comparison prices differ from the original `--market` demonstration's 2/3 and 4/5 quotes.
- **B / Linear:** the exact reference value for the stock transition between `n-1` and `n` is `P + k*(T-n)/T`.
- **C / Scarcity:** add `3*k*max(K-n, 0)/K` to that reference value, where `K = max(1, floor(T/4))`. This is a continuous piecewise-linear hypothesis with a steeper slope below the threshold, rather than another linear model with a different sensitivity.

For each dynamic transition, floor the complete exact reference value once, add `s` for a purchase or subtract `s` for a sale, then clamp to the configured positive minimum and maximum. Buying at market stock `S` traverses boundaries `S, S-1, ...`; selling traverses `S+1, S+2, ...`. Sum the marginal prices instead of multiplying the first quote by quantity. See [Mechanics](MECHANICS.md) for the full arithmetic, preview, rejection, and rollback contracts.

Target stock sets scale. Sensitivity sets the price change over that scale; the minimum and maximum protect bounds, and the spread prices reversals. Six parameters per good already require explanation. The scarcity model adds fixed rules for the quarter-target threshold and three-times sensitivity; they are explicit hypotheses, not hidden adjustments. With targets below 8, `K` is 1 and there are no legal positive boundaries below it, so the scarcity surcharge disappears. Such tiny scales cannot distinguish these two models.

## Scenarios

Unless overridden, the market and trader each start with 100,000 cash, the trader has 100 logs, the market has no planks, and no building is present. Each named case starts independently except the explicitly continued sequences below. There is no replenishment or external income.

| Scenario | Reproducible setup and operations |
| --- | --- |
| A: abundant stock | Market logs 200: buy 1 or 100 independently. Market logs 2,000: buy 100. |
| B: moderate scarcity | Market logs 60: buy 5, buy 25, or sell 25 independently. |
| C: severe scarcity | Logs 10: buy 1, 5, or all 10. After depletion, attempt another purchase. Repeat buy 10 with only 2,000 trader cash. Probe a last unit at stock 1 and threshold-adjacent stocks 26, 25, and 24. |
| D: production | Start at 12 logs and 45 planks, with one enabled sawmill. Advance four ticks, then disable the mill for one idle tick. Observe both goods after every tick. |
| E: cycles and splitting | Start at 30 logs. Successively buy/sell 1, 3, and 7; then buy 4, buy 3, sell 2, sell 5. Independently compare buy/sell 7 with 3 then 4. Probe one-unit reversals at stocks 1 and 2,000. Production is absent. |
| F: finite liquidity | At stock 60, buy or sell 5 with exactly the required payer cash, then repeat with one less. Prices and other starting balances are identical within each pair. |
| G: numeric boundaries | Buy all `int.MaxValue` units or sell that quantity into an empty market, with sufficient finite cash and destination stock room. Transfer one unit at `long.MaxValue`; provoke total, cash-credit, and stock-credit overflow. |

In the maximum-price cases, `P`, `k`, minimum, and maximum are `long.MaxValue`, target is 100, and spread is 0. Both fixed sides are `long.MaxValue`. Other extreme-quantity cases use the standard parameters above. The buyer/seller starts with `long.MaxValue` cash where required, the recipient with zero, except the intentional cash-credit failure whose recipient starts with one. Total overflow requests two units. Stock overflow sells one into an already full market. All exceptional attempts retain the identical snapshot and tick.

## Scarcity and order size

These are successful purchases with sufficient cash. The marginal column is the first through last unit actually priced; the total is the sum over every crossed boundary.

| Starting logs / quantity | Model | First → last marginal price | Total paid |
| --- | --- | ---: | ---: |
| 200 / buy 100 | Fixed | 102 → 102 | 10,200 |
| 200 / buy 100 | Linear | 22 → 101 | 6,120 |
| 200 / buy 100 | Scarcity | 22 → 101 | 6,120 |
| 60 / buy 25 | Fixed | 102 → 102 | 2,550 |
| 60 / buy 25 | Linear | 134 → 153 | 3,580 |
| 60 / buy 25 | Scarcity | 134 → 153 | 3,580 |
| 10 / buy 10 | Fixed | 102 → 102 | 1,020 |
| 10 / buy 10 | Linear | 174 → 181 | 1,772 |
| 10 / buy 10 | Scarcity | 318 → 400 | 3,632 |

At 200 logs, a one-unit dynamic purchase costs 22, while 100 units average 61.2. Multiplying the displayed 22 by 100 would substantially undercharge. At 60 logs, five units cost 676 and 25 cost 3,580, with respective exact averages 135.2 and 143.2. At 2,000 logs, both dynamic models hit the floor: buying 100 costs 500 and creates no marginal price movement. Thus the bounds deliberately hide inventory differences at extremes.

Both dynamic models distinguish small and large orders in ordinary conditions. Scarcity distinguishes order size much more strongly near depletion: buying five of ten costs 1,692, while all ten cost 3,632. Linear costs are 876 and 1,772. This makes the nonlinear candidate more sensitive, but sensitivity alone is not evidence of a better player decision.

The threshold has a visible discrete effect:

| Market logs before buying one | Linear ask | Scarcity ask |
| --- | ---: | ---: |
| 26 | 161 | 161 |
| 25 | 162 | 162 |
| 24 | 162 | 172 |

The exact curve has no value discontinuity at 25, but its slope changes sharply. Integer prices expose that kink as a 10-unit jump from stock 25 to 24, compared with no rounded linear change. The scarcity cap then flattens the final part of the curve: its last-unit ask is 400, compared with linear 181. A future display must explain the threshold and bounded region; otherwise these changes may look arbitrary.

## Production, cash, and boundaries

Four sawmill batches move market stock from 12 logs / 45 planks to 4 logs / 49 planks. No cash moves.

| Model | Log ask before → after | Plank ask before → after |
| --- | ---: | ---: |
| Fixed | 102 → 102 | 204 → 204 |
| Linear | 172 → 178 | 220 → 207 |
| Scarcity | 297 → 380 | 220 → 207 |

The idle fifth tick changes neither quotes nor inventory. Production draws logs from the market and returns planks there, preserving the existing prototype ownership assumption. These price movements describe that stock change; they do not simulate demand, consumption, industrial profit, or operating costs.

At stock 10 with 2,000 trader cash, buying all stock succeeds for fixed and linear pricing but fails for scarcity pricing, leaving its ten logs untouched. Price pressure can therefore preserve stock through an affordability barrier, though this is not evidence of equilibrium. In scenario F, exactly 510 cash funds the fixed five-log purchase and 676 funds either dynamic purchase; one less rejects. Exact market cash of 490 funds the fixed sale and 636 funds either dynamic sale; one less rejects. Attractive displayed prices cannot create liquidity.

Buying all 2,147,483,647 logs completes with totals 219,043,331,994 (fixed), 10,737,437,639 (linear), and 10,737,440,507 (scarcity). Reverse-direction bulk sales also complete. These exercise the bounded summation path at maximum quantity; the report does not assert a hardware-independent latency guarantee. A valid one-unit `long.MaxValue` transfer completes. An unrepresentable total or destination balance throws and leaves the entire tick unchanged. The runner compares preview and execution for each normal or exceptional attempt.

At zero stock there is no stock-feasible buying quote; at maximum stock there is no stock-feasible selling quote. These marginal quotes do not guarantee trader stock, account funding, or recipient balance room. Fixed rejection attempts retain their historical requested price/total; dynamic stock rejections have no execution price. That distinction is explicit in the JSON.

## Reversal, splitting, and rounding observations

At stock 30, completed buy/sell round trips of 1, 3, and 7 lose 4, 12, and 28 cash respectively in every model. The longer sequence buy 4, buy 3, sell 2, sell 5 also returns stock to 30 while losing 28. No goods or combined cash are created. These are closed cycles without production or other external stock changes.

Buying seven at stock 30 costs 714 fixed, 1,120 linear, or 1,130 scarcity. Splitting into three then four costs exactly 306 + 408, 475 + 645, or 475 + 655. Equivalent sale splits also match. Flooring each boundary before summation, and reversing the same boundary at bid, makes rounding independent of order grouping.

Clamping can compress the effective spread to zero. At stock 2,000, either dynamic one-unit reversal pays and receives 5. The scarcity last-unit reversal pays and receives 400. These cycles break even; they do not profit. A strictly positive realized spread at every bound is therefore **not** an implementation contract. The focused headless tests additionally cover exhaustive small states and more closed cycles; the report's few trajectories alone would not establish universal exploit resistance.

## Interpretation and next experiment

Linear pricing gives the clearest explanation: removing stock raises the reference value at a constant configured rate, with explicit rounding and bounds. It distinguishes ordinary small and large trades, responds to production, and retains readable totals. Scarcity highlights severe shortage more strongly, but the quarter-target kink, target-scale limitation, cap, and rapidly increasing cash requirement make its tuning and explanation harder. Fixed pricing remains a useful accounting control.

Retain both dynamic modes, and provisionally start subsequent controlled player-decision experiments with linear pricing. Compare a small set of target, sensitivity, spread, and bound choices against identical stock budgets and explicit decisions. Record which choices become possible or blocked, including cash distribution and recoverability. Keep the scarcity hypothesis for comparison rather than promoting its stronger movement to a final rule.

No result in this report establishes fun, balanced production margins, sustainable money flows, or demand. A closed market with a single trader and no replenishment can simply exhaust stock or concentrate cash. The report's recommendation was to define measurable economic choices and test whether these quotes help people reason about them. Task 1.3a provides a separate [manual shipping comparison](SHIPPING_DEMO.md) using the same linear calculation between two finite markets. Task 1.3b adds a [configured automation comparison](AUTOMATION.md) reusing exact marginal pricing and finite accounts. Both leave this report and its measurements unchanged. Consumption and automatic strategy discovery remain deferred.
