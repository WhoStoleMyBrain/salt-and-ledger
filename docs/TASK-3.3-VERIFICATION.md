# Task3.3 verification

Autonomous companies use persistent plans and ordinary paid regional operations. The [system description](NPC_COMPANIES.md) records decision rules, caps, ownership and limits. The [headless witness](experiments/npc-companies-results.json) runs seed42 for720 days with normal endowments and a zero-cash control.

The funded witness records one paid additional hull,86 input-consuming private production batches,108 docked transfers,525 whole goods sold from ships and3 from company warehouses. The Flour company constructs two real Mills, pays486 coins of payroll and distributes actual production. All-account money remains constant; no Mint issuance or outside funding occurs. Mid-voyage, pending-site and pending-hull saves round-trip and resume exact state. The zero-cash control buys no hulls or sites and produces no goods.

The integration suite also exercises seeds−42 and99, reversed configuration, Turkish culture, multiple initial ships, foreign asset rejection, Royal privilege rejection and720-day12/24-town continuations. Every regional commit retains existing physical, FIFO, financial and cash invariants. Performance measurements in the test output are observational and are not saved as deterministic report values.

## Unity verification boundary

Unity Editor/Play Mode and Windows Mono interaction are not available in this cloud environment. No screenshots or Editor results are claimed. Portable host compilation/checks and staging are recorded with the final check results below.

Remaining local smoke tests:

1. Open ArchipelagoLab in Unity, start seed42 Sandbox and inspect each profile before advancing. Confirm selecting NPCs cannot enable player trade, factory-control, transfer, Royal or route-edit commands against their assets.
2. Advance through day40. Observe a new NPC hull at the real shipyard, its voyage to its business harbor, NPC construction activity and private production. Inspect every vessel of a multi-ship company and confirm one owner identity and consistent read-only asset lists.
3. Inspect NPC Mills and warehouses in Industry/Economy. Confirm real public market stocks/prices change after paid procurement/distribution and NPC rows have no edit controls.
4. Save/load during a voyage, factory foundations, hull construction and input-blocked production. Compare companies, ownership, stages and next-day outcomes.
5. At1366×768 and1920×1080, exercise Compact/Default/Large/Extra Large. Confirm map selections, ship picker, inspector scrolling and Industry rows remain readable with multiple NPC ships and sites; verify ordinary player routes, menus, Grand Commission and Escape behavior.
6. Build and run the Windows Mono player following [Unity guidance](UNITY.md), then repeat the ownership and save smoke tests.

## Executed checks

Passed:

- .NET10 locked restore and Release solution build, zero warnings/errors.
- 15 targeted NPC integration tests, including paid hull parity, pending fleet limits, insufficient materials/cash, hostile resealed saves and late FIFO-sequence rollback of earlier staged investments.
- Seed42 autonomous-company720-day witness and unaffordable control, including committed report freshness.
- Grand Commission ordinary/manual and itinerary witnesses, with exact save/load and deadline control.
- Normal expanding-economy report freshness,29 comparisons and5220 primary world-days.
- 68 Python tests, canonical content validation,60 generated reference freshness checks and strict MkDocs build.
- Unity staging and staging freshness; portable host8/10/12-town persistence checks and syntax parsing of66 Unity C# files. The new Play Mode company inspection test is authored but not executed in Unity.

- Full solution regression:894 passed,0 failed,0 skipped. The final15-test NPC run also covers two commercial shipbuilding cases added after that full run started.
- Economic circulation report freshness:80 worlds,29200 primary world-days, including reverse/culture replay and exact save continuations.
- World-generation check:five seeds, generated plans and ordinary continuation; refreshed the committed report.
- Optional reinvestment report regenerated and freshness-checked for12 comparisons and4380 primary world-days.
- `git diff --check`.

Observed long-horizon integration timings under concurrent verification were20.5 seconds for12 towns and46.7 seconds for24 towns, each covering720 days including save/continuation checks. These are cloud observations, not frame-time guarantees. The ordinary8-town commit benchmark in the expanding report measured approximately0.8–1.3ms/day in representative cases; observer serialization is excluded from that specific benchmark.
