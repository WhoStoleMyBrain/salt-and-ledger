# Current economic experiments

Current productive-economy reports use circulation1, storage2 and itinerary1, with Task2.8 factory/warehouse automation and report format3. They execute ordinary world commands without repairing cash, stock, welfare or policy outcomes. Every commit audits physical flows, FIFO and both money boundaries. A Grand Commission victory certifies its four goals, not perpetual regional prosperity.

## Reproduce

```text
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --expanding-economy --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --economic-circulation --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --grand-commission --check
```

The [normal report](experiments/expanding-economy-results.json) has 29 comparisons: seeds 42/99, thirteen prescribed policies through 180 days, two additional charter cases and one Coverage/Ascension1 network case. Reversed setup and Turkish-culture replay must match exactly. Normal CI checks this bounded report and the separate 80-world [circulation study](ECONOMIC_CIRCULATION_REVIEW.md). No machine timing/path or timestamp enters report bytes.

Policies cover idle, one-way imports, complementary trade, industry/overbuilding, finite external exports and their control, physical Silver/Mint and its control, local circulation, two/three-ship networks and a three-route fleet. Existing market/industrial ship policies remain matched research controls rather than current player editors. Local rules now belong to one town warehouse: when a good is bought and sold, the sell reserve retains at least the buy target. Warehouse trading remains active when the prescribed controller disables a factory. Factories retain100% rate and Normal priority; prices, recipes, wages and initial accounts are unchanged. Company pools report their own policies/trading counters, while factories report production controls, phase and batches. Schema6 save checks restore exact state and compare seven further days of command events.

## Measured current outcomes

Seed42, Merchant, StockOnly, Ascension0 at day 180:

| Policy | Company cash | Reference value change | Paid food at recovery town |
| --- | ---: | ---: | ---: |
| Idle |1800|0|0%|
| Balanced trade |1887.7907|297.7907|0%|
| Industry |4.6406|−77.2075|7%|
| Two-ship network |5.9004|792.8884|1%|
| Local circulation |1056.8835|392.8835|25%|
| Monetary development |1872.7911|1502.7911|0%|

Industry produced54 Bread and19 Flour; the two-ship network produced54 of each. Compared with the pre-Task2.8 baseline, both produce more Bread and hold more reference value, but Industry cash falls from7.7464 to4.6406 and the network's recovery food window falls from4% to1%. Real investment and output coexist with low liquidity and missed food. Monetary development increases measured company value without solving transport or food inputs. These are prescribed-policy counterexamples, not an optimized strategy search or grounds to add a bailout.

The optional [reinvestment report](experiments/expanding-reinvestment-results.json) runs three seeds, both pricing choices and Ascensions through365 days:12 cases and4380 primary world-days. Reproduce with `--expanding-economy --reinvestment --check`. Current StockOnly/Ascension0 observations are:

| Seed | Company cash | Private Flour / Bread produced | Processing population, initial → final | Processing recovery food window |
| --- | ---: | ---: | ---: | ---: |
| −42 |1195.1965|167 /346|30 →34|50%|
| 42 |1110.4645|125 /260|20 →26|57%|
| 99 |811.7116|158 /326|30 →34|57%|

Seeds42/99 previously produced47/49 Flour and104 Bread each, with zero processing food-window fulfillment and populations16/21 at year end. Current cumulative processing food consumption rises from48 to85 and57 to114.286 physical Bread respectively. This is partial recovery: cumulative food affordability shortfalls are17.4617 and35.2644 Bread, and both latest purchases remain below need. Each case still has three real ships,1920 coins issued against24 physically surrendered Silver, and four exact save-continuation checks. Greater production does not remove finite household budgets.

Coverage remains mixed. At seed−42/Ascension0, regional paid Bread rises227 →254 and population99 →112, but material surplus after wages falls20.3096 →−298.981 coins and Company Value falls4411.5745 →3901.0811. At seed99/Ascension0, private Bread rises116 →122 while regional paid Bread falls286 →278 and population110 →106. None of the three Coverage/Ascension0 processing towns has a positive final recovery food window. Surplus after wages excludes service fees; neither that measure nor reference Company Value is liquid cash or a sustainability guarantee.

Across the normal29-case report,14 cases retain identical economic checkpoints: idle, imports, balanced, external/control and monetary/control for both seeds. The15 cases with factory trading change, as do all12 reinvestment cases. Save fingerprints also change because schema6 and automation state are explicit; those representation changes alone are not economic differences. The [circulation review](ECONOMIC_CIRCULATION_REVIEW.md#task28-warehouse-policy-comparison) reports gains and regressions across its matched arms. [Task2.8 verification](TASK-2.8-VERIFICATION.md) records execution and UI evidence separately.

`--deep` remains optional: fifteen seeds/both pricing/both Ascensions at 1095 days for the main matrix, or longer reinvestment. Deep output belongs in ignored `.cache/`; it is not ordinary CI. Runtime/allocations print separately and are not a large-world performance certification.

## Complete runs versus economic controls

`--grand-commission --check` executes seed99 and42 manual wins, a seed99 itinerary win with enabled mid-voyage save restoration, and an idle deadline failure. The manual witnesses retain their economic results exactly: day18/cash622.6936 and day13/cash285.8935. Tests verify funded production, physical trade, all four goals, solvency and terminal freeze. [Task2.8 verification](TASK-2.8-VERIFICATION.md) distinguishes headless operations from actual Unity interaction.
