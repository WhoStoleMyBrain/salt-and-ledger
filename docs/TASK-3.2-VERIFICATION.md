# Task 3.2 verification

This milestone adds the authoritative [contracts and projects](CONTRACTS_PROJECTS.md) board, real ship deliveries, finite reserved funding, production and paid-consumption evidence, permanent public facilities and standalone company depots. Save schema8 rejects older pre-release saves.

## Headless verification

Verified with .NET SDK 10.0.403 and locked dependencies. The complete Release suite passed **881/881 .NET tests**, with no skips, failures or build warnings. All **67 Python tests** passed.

| Check | Result |
| --- | --- |
| `dotnet restore SaltAndLedger.sln --locked-mode` | Passed |
| Release solution build, then `dotnet test ... --no-build` | Passed; 881 tests |
| Demo `--grand-commission --check` | Passed; manual and itinerary victories, ordinary idle failure |
| Demo `--expanding-economy --check` | Passed; regenerated report reproduced exactly |
| Demo `--economic-circulation --check` | Passed; regenerated report reproduced exactly |
| Demo `--world-generation --check` | Passed; five seeds and exact continuation |
| Portable Unity host checks | Passed for 8-, 10-, 12-town runs; all 66 Unity C# files parsed |
| `python tools/validate_data.py` | Passed |
| `python tools/generate_docs.py --check` | Passed; 60 shared reference files |
| `python -m unittest discover -s tests/python -v` | Passed; 67 tests |
| `python -m mkdocs build --strict` | Passed |
| `python tools/prepare_unity.py` and `--check` | Passed |
| `git diff --check` | Passed |

The economy reports expose free external cash and accepted-contract reservations separately. No graphical Unity testing is included in these results.

Focused scenarios cover deterministic offer generation across six seeds and custom configurations, finite acceptance escrow, exclusive and ordinary competing commitments, limits/declines/expiry, recurring windows, actual named-factory production, paid household recovery with player attribution, split-invariant fee privileges and expiry, permanent public construction, standalone warehouse trading/transfers/automation, atomic rejection, resealed invalid-save rejection and exact continuation. Industrial evidence on the inclusive deadline succeeds; otherwise-identical production one day late fails without the bonus.

## Complete gameplay witness

`OpportunityWitnessTests.CompetingCargoTenderFundsPermanentPublicIndustryWithExactContinuationAtEveryStep` runs seed 42 with explicitly configured finite starting inventories, ordinary commands and no state editing:

1. Two exclusive Planks supply offers at `town_003` and `town_004` each require9goods, three windows of 3 goods. Their combined 18 goods exceed the existing ship's 10-good hold. Each reserves 222 coins (9×23 plus 15 completion bonus).
2. The player accepts the commercial-privilege offer for `town_003`; its alternative becomes Withdrawn. A real 9-Plank purchase at `town_002` is transported by ordinary voyages. The company delivers 3 Planks in each 22-day window, finishing on day 45 for 222 coins total and a local fee privilege through day 69.
3. The player invests 220 coins and 6 actual Planks in a public Lumber Camp at `town_002`, split into two contributions. No public facility exists until full funding; the permanent facility is installed on day 48.
4. Its canonical `harvest_logs` batch first runs on day 50 after its normal interval. Actual public Logs increase 17→19, and the town pays the genuine 6-coin production wage. The project creates no private factory or free inventory.
5. A parallel world restored from a save performs every command and day; save bytes agree after every step. Actual reloads occur during voyages and after the partial project. All-account cash, physical ownership and FIFO accounting reconcile throughout.

The existing Grand Commission witnesses also complete successfully, including the itinerary-controlled journey on day 49; the idle control fails normally at day 720. The updated economy reports retain the original trading/production outcomes while recording the new identity, accounts and reservations. They audit 5,220 expanding-economy world-days and 29,200 circulation world-days, with replay/culture/save continuation checks.

## Unity verification boundary

Unity Editor is not installed in the cloud execution environment. Portable host checks parse Unity C# syntax and exercise the actual Unity-independent session classes; they do not type-check Unity Toolkit assemblies or establish successful graphical interaction. No Unity screenshots or human interaction tests are claimed.

Run `python tools/prepare_unity.py`, open `unity/` using its pinned Editor version, and use ArchipelagoLab. At both **1366×768** and **1920×1080**, check Compact, Default, Large and Extra Large text:

1. Start a Sandbox and open Contracts & Projects from Company, Economy and a town. Check the town filter, available/active/project/history views, selected ship cargo, travel and reward terms. Verify panel scrolling and Back/Escape navigation.
2. Accept one explicit tender and verify that the named alternative becomes withdrawn. Check the active limit and a normal overlapping pair that can both be accepted. Deliver partial cargo at the real dock and confirm no action is enabled for a remote ship.
3. Configure Buy/Load → Deliver contract itinerary orders. Run a recurring window, save while waiting for the next window, load, and verify that the route cursor and daily budget do not repeat a payment. Check configured Skip/Wait for active shortages and automatic Skip for a resolved commitment.
4. Complete a Trading Depot using partial money/material contributions. With no factory in that town, open Market ↔ Store, Store ↔ Ship and Warehouse automation; execute a paid trade and a physical transfer.
5. Complete a public industry project. Confirm the new public facility appears in Industry and Economy, first waits its canonical interval, and then consumes actual inputs and pays wages. Verify an incompatible local producer/consumer pair cannot be commissioned even after infrastructure access.
6. Complete a commercial-privilege contract, inspect actual gross/fee/net sale previews at its town, advance beyond expiry, and confirm the ordinary rate returns. Inspect resolved history and exact continuation after a save.
7. Keep a Grand Commission playable through the ordinary victory/results flow. Build and smoke-test the Windows Mono player using `docs/UNITY.md`.
