# Current economic experiments

Current productive-economy reports use circulation1, storage2 and itinerary1. They execute ordinary world commands without repairing cash, stock, welfare or policy outcomes. Every commit audits physical flows, FIFO and both money boundaries. A Grand Commission victory certifies its four goals, not perpetual regional prosperity.

## Reproduce

```text
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --economic-circulation --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --grand-commission --check
```

The [normal report](experiments/expanding-economy-results.json) has 29 comparisons: seeds 42/99, thirteen prescribed policies through 180 days, two additional charter cases and one Coverage/Ascension1 network case. Reversed setup and Turkish-culture replay must match exactly. Normal CI checks this bounded report and the separate 80-world [circulation study](ECONOMIC_CIRCULATION_REVIEW.md). No machine timing/path or timestamp enters report bytes.

Policies cover idle, one-way imports, complementary trade, industry/overbuilding, finite external exports and their control, physical Silver/Mint and its control, local circulation, two/three-ship networks and a three-route fleet. Existing market/industrial policies are retained as matched research controls rather than offered as current player editors. Company pools are reported once alongside facilities' individual counters. Save checks restore exact state and compare seven further days of command events.

## Measured current outcomes

Seed42, Merchant, StockOnly, Ascension0 at day 180:

| Policy | Company cash | Reference value change | Paid food at recovery town |
| --- | ---: | ---: | ---: |
| Idle |1800|0|0%|
| Balanced trade |1887.7907|297.7907|0%|
| Industry |7.7464|−462.1454|1%|
| Two-ship network |1.0854|457.0864|4%|
| Local circulation |950.4168|286.4168|25%|
| Monetary development |1872.7911|1502.7911|0%|

Industry produced40 Bread and 17 Flour; the two-ship network produced46 of each. Real investment and output therefore coexist with low liquidity and missed food. Monetary development increases measured company value without solving transport or food inputs. These are prescribed-policy counterexamples, not an optimized strategy search or grounds to add a bailout.

The optional [reinvestment report](experiments/expanding-reinvestment-results.json) runs three seeds, both pricing choices and Ascensions through 365 days. Reproduce with `--expanding-economy --reinvestment --check`. At StockOnly/Ascension0, seeds 42/99 produce104 Bread and 47/49 Flour but finish with **zero** paid-food fulfillment at their processing towns; those populations fall below their initial values. Cash ends at 102.7219/102.5280 coins. Seed−42 differs: its processing town retains58% fulfillment. The former historical claim of universal year-end food is intentionally withdrawn rather than preserved by weakening accounting or retuning the economy.

`--deep` remains optional: fifteen seeds/both pricing/both Ascensions at 1095 days for the main matrix, or longer reinvestment. Deep output belongs in ignored `.cache/`; it is not ordinary CI. Runtime/allocations print separately and are not a large-world performance certification.

## Complete runs versus economic controls

`--grand-commission --check` executes seed99 and 42 manual wins, a seed99 itinerary win with enabled mid-voyage save restoration, and an idle deadline failure. The manual witnesses retain their pre-cleanup economic results exactly: day 18/cash622.6936 and day 13/cash285.8935. Tests verify funded production, physical trade, all four goals, solvency and terminal freeze. [Current verification](REPOSITORY-CLEANUP.md) distinguishes these headless operations from actual Unity interaction.
