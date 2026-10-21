# Readability and runtime verification gallery

Task 1.8 replaces the previous partial menu/reference text toggle with four game-wide text scales. Default body text is **17 px**, compared with the previous gameplay's frequent 10–13 px labels; Compact uses **14 px**, Large **21 px**, and Extra Large **25 px**. Caption tokens are 12/15/19/23 px respectively. Layout, wrapping, control heights and scrolling respond to the selected scale. The setting does not transform painted text over unchanged hit targets or alter the simulation.

This page records Editor rendering and interaction evidence. The [Unity verification record](UNITY.md#task-18-seeded-run-and-readability-verification) distinguishes the Editor from the Windows development player and lists test logs and platform limitations. [Playtest procedures](PLAYTEST.md#seeded-worlds-ascension-and-readability) describe reproducible checks; [seeded-run analysis](SEEDED_RUNS.md) covers the economic measurements.

The final **72-screen matrix was recaptured and visually reviewed** after the trade-layout, route-label and docked-ship hit-target fixes. No critical hidden actions or overlapping controls remained in the reviewed matrix. Four additional Compact captures and the construction/input journeys below extend that evidence; all linked images are retained with their original capture filenames.

## Before and after

These baseline captures were taken before the readability changes. The after views use the final Default-scale matrix. Different selected towns and run states are presentation examples, not an economic before/after comparison.

| View | Before | Default after |
| --- | --- | --- |
| Main menu, 1366×768 | [Baseline](images/task18/task18-before-main-1366.png) | [Default](images/task18/task18-matrix-default-1366-main.png) |
| Gameplay, 1366×768 | [Baseline](images/task18/task18-before-gameplay-1366.png) | [Default](images/task18/task18-matrix-default-1366-market.png) |
| Gameplay, 1920×1080 | [Baseline](images/task18/task18-before-gameplay-1920.png) | [Default](images/task18/task18-matrix-default-1920-market.png) |

## Screen matrix

Each link opens an actual Editor Game view capture at its stated resolution, rather than a mockup or a stylesheet rendering. The matrix covers twelve screens at Default, Large and Extra Large, for **72 images**. Compact is included in automated reachability and map-layout checks at both resolutions and in the additional captures below.

Matrix setup uses the running host's public setup/navigation APIs and UI controls to establish repeatable views of the real simulation. Those fixtures certify rendered layout; they do not represent 72 independently completed pointer-only gameplay journeys. The separate interaction sequence below supplies evidence for seed entry, construction, playback and terminal outcomes.

| Screen | Default 1366×768 | Large 1366×768 | XL 1366×768 | Default 1920×1080 | Large 1920×1080 | XL 1920×1080 |
| --- | --- | --- | --- | --- | --- | --- |
| Main menu | [View](images/task18/task18-matrix-default-1366-main.png) | [View](images/task18/task18-matrix-large-1366-main.png) | [View](images/task18/task18-matrix-xl-1366-main.png) | [View](images/task18/task18-matrix-default-1920-main.png) | [View](images/task18/task18-matrix-large-1920-main.png) | [View](images/task18/task18-matrix-xl-1920-main.png) |
| Run setup | [View](images/task18/task18-matrix-default-1366-setup.png) | [View](images/task18/task18-matrix-large-1366-setup.png) | [View](images/task18/task18-matrix-xl-1366-setup.png) | [View](images/task18/task18-matrix-default-1920-setup.png) | [View](images/task18/task18-matrix-large-1920-setup.png) | [View](images/task18/task18-matrix-xl-1920-setup.png) |
| Selected-port market | [View](images/task18/task18-matrix-default-1366-market.png) | [View](images/task18/task18-matrix-large-1366-market.png) | [View](images/task18/task18-matrix-xl-1366-market.png) | [View](images/task18/task18-matrix-default-1920-market.png) | [View](images/task18/task18-matrix-large-1920-market.png) | [View](images/task18/task18-matrix-xl-1920-market.png) |
| Trade overlay | [View](images/task18/task18-matrix-default-1366-trade.png) | [View](images/task18/task18-matrix-large-1366-trade.png) | [View](images/task18/task18-matrix-xl-1366-trade.png) | [View](images/task18/task18-matrix-default-1920-trade.png) | [View](images/task18/task18-matrix-large-1920-trade.png) | [View](images/task18/task18-matrix-xl-1920-trade.png) |
| Factory status and policy | [View](images/task18/task18-matrix-default-1366-factory.png) | [View](images/task18/task18-matrix-large-1366-factory.png) | [View](images/task18/task18-matrix-xl-1366-factory.png) | [View](images/task18/task18-matrix-default-1920-factory.png) | [View](images/task18/task18-matrix-large-1920-factory.png) | [View](images/task18/task18-matrix-xl-1920-factory.png) |
| Warehouse and transfers | [View](images/task18/task18-matrix-default-1366-warehouse.png) | [View](images/task18/task18-matrix-large-1366-warehouse.png) | [View](images/task18/task18-matrix-xl-1366-warehouse.png) | [View](images/task18/task18-matrix-default-1920-warehouse.png) | [View](images/task18/task18-matrix-large-1920-warehouse.png) | [View](images/task18/task18-matrix-xl-1920-warehouse.png) |
| Merchant automation | [View](images/task18/task18-matrix-default-1366-automation.png) | [View](images/task18/task18-matrix-large-1366-automation.png) | [View](images/task18/task18-matrix-xl-1366-automation.png) | [View](images/task18/task18-matrix-default-1920-automation.png) | [View](images/task18/task18-matrix-large-1920-automation.png) | [View](images/task18/task18-matrix-xl-1920-automation.png) |
| Industrial logistics | [View](images/task18/task18-matrix-default-1366-logistics.png) | [View](images/task18/task18-matrix-large-1366-logistics.png) | [View](images/task18/task18-matrix-xl-1366-logistics.png) | [View](images/task18/task18-matrix-default-1920-logistics.png) | [View](images/task18/task18-matrix-large-1920-logistics.png) | [View](images/task18/task18-matrix-xl-1920-logistics.png) |
| Company ledger | [View](images/task18/task18-matrix-default-1366-ledger.png) | [View](images/task18/task18-matrix-large-1366-ledger.png) | [View](images/task18/task18-matrix-xl-1366-ledger.png) | [View](images/task18/task18-matrix-default-1920-ledger.png) | [View](images/task18/task18-matrix-large-1920-ledger.png) | [View](images/task18/task18-matrix-xl-1920-ledger.png) |
| Pause menu | [View](images/task18/task18-matrix-default-1366-pause.png) | [View](images/task18/task18-matrix-large-1366-pause.png) | [View](images/task18/task18-matrix-xl-1366-pause.png) | [View](images/task18/task18-matrix-default-1920-pause.png) | [View](images/task18/task18-matrix-large-1920-pause.png) | [View](images/task18/task18-matrix-xl-1920-pause.png) |
| Encyclopedia | [View](images/task18/task18-matrix-default-1366-encyclopedia.png) | [View](images/task18/task18-matrix-large-1366-encyclopedia.png) | [View](images/task18/task18-matrix-xl-1366-encyclopedia.png) | [View](images/task18/task18-matrix-default-1920-encyclopedia.png) | [View](images/task18/task18-matrix-large-1920-encyclopedia.png) | [View](images/task18/task18-matrix-xl-1920-encyclopedia.png) |
| Run results | [View](images/task18/task18-matrix-default-1366-results.png) | [View](images/task18/task18-matrix-large-1366-results.png) | [View](images/task18/task18-matrix-xl-1366-results.png) | [View](images/task18/task18-matrix-default-1920-results.png) | [View](images/task18/task18-matrix-large-1920-results.png) | [View](images/task18/task18-matrix-xl-1920-results.png) |

Long forms deliberately scroll within their panel. At 1366×768, Large and Extra Large can stack the trade table above the order form; those controls remain reachable by internal scrolling. At Default, the principal stock, quantity, exact total and buy/sell controls fit together without scrolling the trade content. Primary navigation and modal Close controls remain separate from long scrolling bodies.

### Compact views

All four Compact captures were visually inspected, and their native PNG dimensions were verified. The map has separate town labels, docked ship markers and six readable distance/day captions. The market table and primary playback/navigation controls remain visible, while the selected-port panel scrolls independently. Both trade captures show the six commodity rows, quantity field, shortage explanations, buy/sell areas and Close together without overlap. Furniture is deliberately selected with no stock or cargo, so the disabled actions show real availability constraints.

| Screen | Compact 1366×768 | Compact 1920×1080 |
| --- | --- | --- |
| Market and chart | [View](images/task18/task18-compact-market-1366.png) | [View](images/task18/task18-compact-market-1920.png) |
| Trade overlay | [View](images/task18/task18-compact-trade-1366.png) | [View](images/task18/task18-compact-trade-1920.png) |

## Observed fixes and automated coverage

Inspection of the first matrix found trade-table/order-form overlap, map-label collisions and overlapping docked-ship hit targets at larger scales. The fixes reserve real layout height for the trade table, place route labels around authoritative chart geometry, and separate the full ship marker bounds. The economic coordinates, distances and travel durations remain unchanged. The corrected matrix was recaptured and reviewed in full, including the Extra Large seed -42 marker case.

The final official Unity suites passed **80/80 Edit Mode** and **42/42 Play Mode tests**, including trade-section non-overlap, reachable actions, passive scale changes, modal click blocking and **96 map combinations**: two resolutions × four text scales × six Classic/seeded layouts. The map checks include seeded and boundary layouts and actual resolved label/marker geometry. These assertions supplement screenshot review; they do not replace it or certify every possible seed's visual arrangement.

An actual trade-input check typed quantity **3** and moved focus with Tab. The Logs preview displayed exact total **31**, execution average **10.33**, and first/last marginal prices **10/11**. A pointer click at Eastport behind the modal left Northport selected, day 0 and revision 1 unchanged; Escape closed the trade overlay. This verifies numeric editing, focus movement and modal input blocking separately from the screenshot fixtures.

## Seed entry, matching world and retry

The actual input journey used seed **-42**, generator version **1**, Merchant Company and **Ascension 1**. The seed was entered through the runtime keyboard path, with setup controls and preview visible before starting. The preview and map retain the same six authoritative distances.

| Evidence | Capture |
| --- | --- |
| Setup with known seed and all direct-route estimates | [Setup preview](images/task18/task18-default-setup-known-minus42-1366.png) |
| Matching generated town arrangement and route labels | [Started world](images/task18/task18-default-seed-minus42-map-1366.png) |
| Explicit reroll opens a new preview before a separate start | [Reroll preview](images/task18/task18-actual-reroll-preview-1366.png) |

For this seed, Eastport–Northport is **536 units / 6 days**, Eastport–Westhaven **273 / 3**, and Northport–Southhaven **322 / 4** at the unchanged starting speed 100. The full preview exposes all six connections. Retry Same World retains the seed, charter, challenge and ascension while resetting the world; Reroll changes the draft seed only after the explicit request. Host tests additionally compare funds, stock, cargo, policies, facilities, accounting lots, clocks, revision and cleared journals after retry.

These journey captures precede the final map-label layout refinements; use the matrix above to assess the final visual arrangement.

## Actual Ascension 1 bakery timeline

The interaction sequence purchased a Northport bakery on day 0 in the seeded Merchant run and explicitly applied the recommended input policy. Ordering paid **300** immediately, reducing company cash from 600 to 300. Applying the policy bought nothing immediately. Each following Next day action advanced the real world once.

| Day | Observed state | Capture |
| --- | --- | --- |
| 0 | Under construction, two days remaining; empty private warehouse; cash 300; first eligible batch day 4 | [Day 0](images/task18/task18-asc1-bakery-day0.png) |
| 1 | Under construction, one day remaining; no automatic purchase or batch; cash 300 | [Day 1](images/task18/task18-asc1-bakery-day1.png) |
| 2 | Operational since day 2; empty private warehouse; no second construction payment; first eligible batch still day 4 | [Day 2](images/task18/task18-asc1-bakery-day2.png) |
| 3 | Actual procurement leaves one Flour and three Logs; cash 238; scheduled production is not yet due | [Day 3](images/task18/task18-asc1-bakery-day3.png) |
| 4 | First batch produces two Bread, retaining two Logs; cash 238; next eligible day 6 and a real Flour/price wait are shown | [Day 4](images/task18/task18-asc1-bakery-day4.png) |

The completion-day automatic phases do not run for the pending site. Later production still needs actual inputs, permitted prices and capacity; the displayed date is eligibility, not a guarantee of free output. Construction and material expenditure are separate financial measures.

The separate [Ascension 0 construction capture](images/task18/task18-asc0-instant-construction-1366.png) shows the purchased bakery operational immediately on day 0, company cash 300, completion day 0 and first eligible production day 2. Its warehouse is empty and input buying remains off until explicitly applied. The tests also verify the Industrial Consortium's already-built starter exemption at both ascensions.

## Actual terminal outcomes

Two played outcomes on the same seed and ascension expose a legitimate failure and a successful alternative. The production, voyages, trades, objective evaluation and results use the authoritative simulation; no victory flag or result total was injected.

| Observed run | Result | Capture |
| --- | --- | --- |
| Northport bakery plus subsequent trading | Coastal Supply failure on day 60: 16/24 net Bread delivered, cash 735, score 406, material margin 473 and 300 construction expenditure | [Actual defeat](images/task18/task18-actual-seeded-terminal-1366.png) |
| Merchant route after Retry Same World | Coastal Supply victory on day 43: 26/24 net Bread delivered, cash 1,118, score 1,770, material margin 518 and no construction expenditure | [Actual victory](images/task18/task18-actual-seeded-merchant-victory-1366.png) |

The result screens show seed -42, generator version 1 and Ascension 1, alongside the challenge/charter comparison group and real financial accounts. Success and failure freeze further economic mutation. These two journeys are UI observations, not substitutes for the separate 25-seed and 125-seed experiments.

## Evidence boundaries

Screenshots show actual Editor rendering at the requested pixel sizes. Automated geometry checks and runtime input sequences establish specific behavior, while independent human usability, every possible seeded label arrangement, other operating systems and IL2CPP remain outside this evidence. The Windows development player's actual build, launch, input flows and remaining graphics/plugin diagnostics are recorded separately in [Unity integration](UNITY.md#task-18-seeded-run-and-readability-verification). Local text-size persistence is a presentation preference; no run save or meta-progression was added.

## Task 1.9 living-city readability verification

Actual Unity 6000.6.6f1 Editor Game views captured on **2026-10-21** through the official Unity Agent Plugin/CLI. The final matrix contains **76 views**: nine screens × four text scales × two resolutions, plus four scrolled Large/Extra Large trade-order views. All were visually reviewed; the complete Play Mode suite also asserts resolved geometry and reachability. These captures use repeatable host setup and real UI navigation events; they are not 76 independent pointer-only playthroughs. Separate native-input journeys and standalone evidence are documented in [Unity verification](UNITY.md#task-19-living-city-verification).

Default's Overview and Market fit without a permanent primary scrollbar at both resolutions; ordinary trade shows quantity, exact total and Buy/Sell together. Compact keeps the denser 14 px preference. Large/Extra Large preserve 21/25 px text, fixed primary actions and intentional internal scrolling. City details, long setup/results, ledger and advanced industry remain document/form screens with internal scrolling. No paint-only scaling or economic command implements text size.

Visual verification fixed an 11 px Default summary overflow by moving redundant industry counts into details, shortened the pricing label, made the selected city tab distinct from keyboard focus, and replaced an unexplained initial 0% purchasing-power value with awaiting-first-observation wording. Later purchasing-power labels identify the last observed food-reserve benchmark. An initial screenshot-helper error retained the previous Industry tab for some accounts images; every final accounts image below explicitly selects People & money.

### Living-city matrix: 1366×768

| Screen | Compact | Default | Large | Extra Large |
| --- | --- | --- | --- | --- |
| Setup | [View](images/task19/task19-matrix-compact-1366-setup.png) | [View](images/task19/task19-matrix-default-1366-setup.png) | [View](images/task19/task19-matrix-large-1366-setup.png) | [View](images/task19/task19-matrix-xl-1366-setup.png) |
| Town overview | [View](images/task19/task19-matrix-compact-1366-overview.png) | [View](images/task19/task19-matrix-default-1366-overview.png) | [View](images/task19/task19-matrix-large-1366-overview.png) | [View](images/task19/task19-matrix-xl-1366-overview.png) |
| Market tab | [View](images/task19/task19-matrix-compact-1366-market.png) | [View](images/task19/task19-matrix-default-1366-market.png) | [View](images/task19/task19-matrix-large-1366-market.png) | [View](images/task19/task19-matrix-xl-1366-market.png) |
| People and accounts | [View](images/task19/task19-matrix-compact-1366-accounts.png) | [View](images/task19/task19-matrix-default-1366-accounts.png) | [View](images/task19/task19-matrix-large-1366-accounts.png) | [View](images/task19/task19-matrix-xl-1366-accounts.png) |
| Needs and coverage | [View](images/task19/task19-matrix-compact-1366-needs.png) | [View](images/task19/task19-matrix-default-1366-needs.png) | [View](images/task19/task19-matrix-large-1366-needs.png) | [View](images/task19/task19-matrix-xl-1366-needs.png) |
| Industries and capabilities | [View](images/task19/task19-matrix-compact-1366-capabilities.png) | [View](images/task19/task19-matrix-default-1366-capabilities.png) | [View](images/task19/task19-matrix-large-1366-capabilities.png) | [View](images/task19/task19-matrix-xl-1366-capabilities.png) |
| Ordinary trade | [View](images/task19/task19-matrix-compact-1366-trade.png) | [View](images/task19/task19-matrix-default-1366-trade.png) | [View](images/task19/task19-matrix-large-1366-trade.png) | [View](images/task19/task19-matrix-xl-1366-trade.png) |
| Private industry | [View](images/task19/task19-matrix-compact-1366-industry.png) | [View](images/task19/task19-matrix-default-1366-industry.png) | [View](images/task19/task19-matrix-large-1366-industry.png) | [View](images/task19/task19-matrix-xl-1366-industry.png) |
| Results | [View](images/task19/task19-matrix-compact-1366-results.png) | [View](images/task19/task19-matrix-default-1366-results.png) | [View](images/task19/task19-matrix-large-1366-results.png) | [View](images/task19/task19-matrix-xl-1366-results.png) |

### Living-city matrix: 1920×1080

| Screen | Compact | Default | Large | Extra Large |
| --- | --- | --- | --- | --- |
| Setup | [View](images/task19/task19-matrix-compact-1920-setup.png) | [View](images/task19/task19-matrix-default-1920-setup.png) | [View](images/task19/task19-matrix-large-1920-setup.png) | [View](images/task19/task19-matrix-xl-1920-setup.png) |
| Town overview | [View](images/task19/task19-matrix-compact-1920-overview.png) | [View](images/task19/task19-matrix-default-1920-overview.png) | [View](images/task19/task19-matrix-large-1920-overview.png) | [View](images/task19/task19-matrix-xl-1920-overview.png) |
| Market tab | [View](images/task19/task19-matrix-compact-1920-market.png) | [View](images/task19/task19-matrix-default-1920-market.png) | [View](images/task19/task19-matrix-large-1920-market.png) | [View](images/task19/task19-matrix-xl-1920-market.png) |
| People and accounts | [View](images/task19/task19-matrix-compact-1920-accounts.png) | [View](images/task19/task19-matrix-default-1920-accounts.png) | [View](images/task19/task19-matrix-large-1920-accounts.png) | [View](images/task19/task19-matrix-xl-1920-accounts.png) |
| Needs and coverage | [View](images/task19/task19-matrix-compact-1920-needs.png) | [View](images/task19/task19-matrix-default-1920-needs.png) | [View](images/task19/task19-matrix-large-1920-needs.png) | [View](images/task19/task19-matrix-xl-1920-needs.png) |
| Industries and capabilities | [View](images/task19/task19-matrix-compact-1920-capabilities.png) | [View](images/task19/task19-matrix-default-1920-capabilities.png) | [View](images/task19/task19-matrix-large-1920-capabilities.png) | [View](images/task19/task19-matrix-xl-1920-capabilities.png) |
| Ordinary trade | [View](images/task19/task19-matrix-compact-1920-trade.png) | [View](images/task19/task19-matrix-default-1920-trade.png) | [View](images/task19/task19-matrix-large-1920-trade.png) | [View](images/task19/task19-matrix-xl-1920-trade.png) |
| Private industry | [View](images/task19/task19-matrix-compact-1920-industry.png) | [View](images/task19/task19-matrix-default-1920-industry.png) | [View](images/task19/task19-matrix-large-1920-industry.png) | [View](images/task19/task19-matrix-xl-1920-industry.png) |
| Results | [View](images/task19/task19-matrix-compact-1920-results.png) | [View](images/task19/task19-matrix-default-1920-results.png) | [View](images/task19/task19-matrix-large-1920-results.png) | [View](images/task19/task19-matrix-xl-1920-results.png) |

### Larger-text order controls and actual money flows

At 1366×768 the Large/Extra Large order form follows the market table within the trade body. Scrolling reaches the exact quote and action; Close/footer remain fixed. At 1920×1080 the same controls fit side by side.

| Evidence | 1366×768 | 1920×1080 |
| --- | --- | --- |
| Large trade order | [View](images/task19/task19-matrix-large-1366-trade-order.png) | [View](images/task19/task19-matrix-large-1920-trade-order.png) |
| Extra Large trade order | [View](images/task19/task19-matrix-xl-1366-trade-order.png) | [View](images/task19/task19-matrix-xl-1920-trade-order.png) |

The final Windows Mono player's real pointer-driven liquidity sequence is visible in [blocked day-7 sale](images/task19/task19-player-liquidity-blocked.png), [retail-funded day-8 quote](images/task19/task19-player-liquidity-funded.png), [committed sale](images/task19/task19-player-liquidity-sold.png) and [day-8 city accounts](images/task19/task19-player-accounts-day8.png). Market cash 2 cannot pay; households then spend 39 on Bread, enabling a sale for 37 without advancing another day. The [day-28 victory](images/task19/task19-player-victory.png) shows six actual food observations and qualified identity/financial results. [Offline help](images/task19/task19-player-offline-help.png) and [Ledger](images/task19/task19-player-ledger.png) were opened through the player's controls.

Editor [typed seed −42](images/task19/task19-actual-seed-minus42.png), [typed quantity 3/modal blocking](images/task19/task19-actual-trade-input.png) and [actual day-28 victory](images/task19/task19-actual-victory-minus42.png) supplement the matrix. See the dated Unity record for exact assertions, player/backend limits and retained development diagnostics. No image is a mockup or retouched UI.
