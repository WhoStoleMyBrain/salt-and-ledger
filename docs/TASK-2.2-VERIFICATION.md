# Task 2.2 verification

Baseline: `eafb45228480a51cea8427dced821751b537563a`. Audit date: October 25, 2026. The full requirements are in `tasks/TASK-2.2.md`. [Harbor contracts](HARBOR.md) document the implementation. All eleven Definition of Done criteria below were checked against code, tests and the recorded live journeys.

## Definition of Done audit

| Criterion | Evidence |
| --- | --- |
| New Run defaults to visible v3 | New setup explicitly selects Expanding Economy, Grand Commission and pooled storage. Advanced / Legacy retains old modes; Sandbox remains selectable. `HarborExperienceTests` verifies defaults, passive selection, exact legacy Retry and fresh pooled Change Setup. |
| Usable accurate preview map | The dedicated pre-draft screen uses the existing `ArchipelagoMap` and generated world. Automated inspection checks all towns, starting stocks and buildable capabilities without changing active state; invalid seed text retains the last valid preview. Visual acceptance recorded below. |
| Unified paid trading and no-cash loading | Harbor contains market, selected ship and local stock with shared authoritative previews. Tests exercise fractional paid warehouse orders, whole docked transfers, remote denial and NPC read-only inspection. Live acceptance recorded below. |
| Two industries share stock and produce in sequence | Focused simulation and Play Mode tests perform funded Mill–Bakery production. The Farm/voyage/Mill/Bakery test also checks physical transport and FIFO material costing. No synthetic production counters or supplemental grants. |
| One inventory/FIFO owner per pooled town | Explicit storage rules v2; pool capacity derives from actual sites. Tests cover stable scarce-input ordering, only successful payroll, finite capacity, combined output ceilings, pending sites, policy contention, route adaptation, rollback and valuation once. Facilities retain individual production/labor/control records. |
| Clear prices and blockers | Buy ask and sell bid, exact order totals, display-only average, paying account, fractional/whole quantity rules and immediate production/transfer remedies use authoritative previews. Stock indicators distinguish physical availability from affordability. |
| Proper completed-run acknowledgment | Winning operations freeze immediately. Completion offers results, Journal, frozen map and saving without another day. Actual winning journey recorded below. |
| Exact old saves/tests/reports | Untouched schema-1 and schema-2 golden bytes and continuation pass. The supplied completed save roundtrips exactly with legacy separate warehouses. All historical report generators pass checks and their committed bytes remain unchanged. |
| Exact new save roundtrips | Schema 3 tests combine multiple owned ships, active voyage, pending facility and partial FIFO slices, then compare continuation. Strict corrupt-store/identity/FIFO cases reject. Terminal success/failure/abandonment remains frozen. |
| Diagnostic export works | Read-only paired raw save and readable JSON report, unique filenames, bounded output/history and exact state fingerprint. All four actual Unity Edit Mode export cases pass, including parse/reload, non-overwrite, unchanged slots and frozen terminal export. |
| Actual Unity verification recorded | Editor 6000.6.6f1 passed full suites and a genuine day22 UI victory, with all four required resolution/scale combinations inspected. Windows x64 Mono continued the actual day1 save and reproduced the victory byte-for-byte; terminal load, export, retry and normal Exit passed. Captures and limitations follow. |

## Executed automated checks

| Check | Actual result |
| --- | --- |
| Locked restore, Release simulation and demo build | Passed; simulation build zero warnings/errors. |
| Full .NET suite | 887 passed, zero failed/skipped, 3m55s. Subsequent focused pending-policy/continuation/report checks: 9 passed after one additional test and strengthened continuation coverage. |
| Focused persistence suite | 81 passed, including legacy bytes, new schema and strict restoration. |
| Focused pooled economics/report/winning-save suite | 21 passed; ordinary economic commands produced a genuine pooled Grand Commission victory. |
| Demonstrations | Production, world, market, shipping and automation all passed. |
| Bounded report checks | Pricing, archipelago, enterprise, industrial logistics, runs, seeded runs, living cities, expanding economy, reinvestment and Grand Commission all passed. No deep sweep or report regeneration. |
| Content, Python and documentation | Validation, generated freshness, 58 Python tests and strict MkDocs passed. Restricted temporary-directory access initially prevented Python tests; approved normal-access rerun passed. |
| Unity staging | Preparation and freshness check passed. Unity compilation caught and resolved an invalid preview-member reference. |
| Actual Unity Edit Mode | 98 passed, zero failed/skipped, 184.34s. A preceding attempt was contaminated by a concurrent focus-command timeout log; rerun without interfering commands passed. |
| Actual Unity Play Mode | Final full suite **63/63 passed**, 90.6s. Earlier runs exposed outdated labels and a real stale Settings dropdown; corrected before acceptance. Final two-line price-header change then passed **5/5** focused Harbor tests, 2.74s. |
| Windows x64 Mono development player | Final-source build passed. Continued the Editor's mid-voyage save, completed the same economic journey, exported, saved/loaded the frozen victory, retried the exact original world and exited through the UI with process exit code 0. |

## Legacy playtest diagnostic

The existing private slot-3 fixture was read and copied only to ignored local verification storage. Its SHA-256 is `5bb9e1b135adf2e63e7d55243e84731254f4555ff6a581fd4a68690651ab0a47`. Loading and saving reproduces the original bytes: schema 2, legacy warehouses, day140/revision535 succeeded, seed -734282136, Industrial Consortium, Ascension1, Coverage.

The report confirms 11,450 all-account coins, 8 Bread and 4 Grain in Southhaven's Sawmill store, 20.75 public Bread at Eastport and none at the other towns. Southhaven and Westhaven market cash remain 9.3394 and 9.6449 coins. There are six separate facility stores and four ship stores. Export does not merge, replenish, retune or rescue them. Raw fixture and report remain private and uncommitted.

## Live journey and reviewed captures

Official Unity CLI scripts sent frame-spaced Toolkit pointer/keyboard events to the actual ArchipelagoLab controls. Reflection read snapshots/save bytes and adjusted presentation/capture; it did not issue economic commands or modify economic state. Frame advancement was observed. This is automated live interaction, not physical input-hardware certification.

The Editor drafted seed99 / Merchant / StockOnly / Ascension0 with storage v2: All Harbors, A New Industry, Flour for the Bakers and Royal Institution. Paid construction created a Northport Mill and Bakery, a Westhaven Bakery and an Eastport Silver Mine. Remote Harbor purchases put two Grain and one Logs into Northport's one store. The Mill produced Flour on day2; the Bakery consumed that Flour and Logs on day3 and produced two Bread. Each batch paid six coins. Westhaven's independently funded Bakery satisfied the commission's distinct-town requirement.

Actual voyages traded Bread, collected mined Silver, delivered a finite Furniture export, commissioned the public Mint, waited its four-day construction interval, surrendered physical Silver, and sold four Flour at Southhaven. The final sale produced **day22/revision45 victory with 283.6668 coins**. The host showed completion before Results; the frozen Journal and export remained available.

| Measured final result | Displayed coins or physical goods |
| --- | ---: |
| Company cash | 283.6668 |
| Reference Company Value / change from start | 2243.6668 / -56.3332 |
| Paid inventory purchases / sale revenue | 275.3331 / 356.9999 |
| Realized material margin / all private wages | 176.0001 / 78 |
| Operating surplus after wages | 98.0001 |
| Private construction / public Mint commission | 1180 / 400 |
| Mint supplier receipts / total issued currency | 60 / 80 |
| External net inflow / all-account money | 100 / 11810 |
| Held goods | 4 Bread and 4 Silver |

The shared chain needed genuine inputs; it stopped when those inputs ran out. The enabled Silver Mine continued paying wages for held output. These outcomes are evidence of finite accountable operations, not a claim of indefinite profitability or balanced town economies.

The day1 mid-voyage Save/Load reproduced exact full bytes, including two travel days remaining: SHA-256 `2919f1ab0bdd820b57482096824511e331350d72b967479ca4f6cb1709ec9822`. Day9 load/unload/reload changed revisions21→24 while day9 and company cash579.6667 stayed fixed; FIFO slices moved between the actual docked ship and town store. Remote Harbor showed cargo inaccessible while preserving local company market trading.

The Results export produced parseable companion files and displayed both precise paths. Before export, after export and the raw exported save were byte-identical: SHA-256 `660ad5dbd9a0ef94d2695cbe9ce0ae31f2a9d6e103aca5ae6cf1aa91f03260d5`. The report marks recent activity as partial. Its snapshot fingerprint and the save envelope's checksum intentionally hash different representations; export validates the loaded snapshot against the current snapshot.

The Windows Mono player continued that exact day1 save. All ten compared full-save checkpoints match the Editor byte-for-byte: shared production on day3; before/load/unload/reload Silver on day9; departure day9; arrival day12; pre-sale day22; victory; and after export. Player export also reproduces the same raw victory bytes. Loading the terminal save returns the frozen day22/revision45 result. Retry reproduces the original day0/revision0 save, including seed, draft and pooled identity: SHA-256 `82569abfb210dc89026987ac27bd73a00596ce0304b0fc029df7e8a57dc9b871`.

All three original user slots were restored after live testing and their SHA-256 hashes checked against the original backups. The supplied completed slot3 remains unchanged. The player exited through its confirmation with code 0; the Editor was left stopped. Temporary build settings and runtime QA configuration were restored; unrelated pre-existing Learn/recovery files were preserved.

### Screenshot evidence

These are unmodified captures of the actual Editor Game view or Windows player, inspected at their native dimensions. Default and Large use the same inherited scale system as Compact and Extra Large; the existing readability suite covers all four scale settings. Narrow Harbor layouts deliberately scroll inside the modal while keeping close/status controls available. No fonts were reduced to force a fit.

| Actual Editor view | 1366×768 | 1920×1080 |
| --- | --- | --- |
| Harbor, Default | [Capture](images/harbor/editor-harbor-default-1366.png) | [Capture](images/harbor/editor-harbor-default-1920.png) |
| Harbor, Large | [Capture](images/harbor/editor-harbor-large-1366.png) | [Capture](images/harbor/editor-harbor-large-1920.png) |
| Map/HUD, Default | [Capture](images/harbor/editor-hud-default-1366.png) | [Capture](images/harbor/editor-hud-default-1920.png) |
| Map/HUD, Large | [Capture](images/harbor/editor-hud-large-1366.png) | [Capture](images/harbor/editor-hud-large-1920.png) |

The full preview map was inspected at [1366×768 Default](images/harbor/editor-full-map-default-1366.png) and [1920×1080 Large](images/harbor/editor-full-map-large-1920.png), including pointer selection of Eastport. Editor [completion acknowledgment](images/harbor/editor-completion.png) and [frozen Journal](images/harbor/editor-victory-journal.png) show the genuine winning run.

Windows Mono captures show the [remote Harbor and shared Bread output](images/harbor/player-harbor.png), [physical docked Silver transfers](images/harbor/player-docked-transfers.png), [completion acknowledgment](images/harbor/player-completion.png) and [Results with diagnostic export](images/harbor/player-results.png). Raw user saves, full export paths and private verification files are not part of the published gallery.

## Evidence boundaries

Only recorded checks above are claimed. Native physical input hardware, IL2CPP and other platforms are outside this milestone's verification. The Windows player logged optional AMD/NVIDIA plugin architecture failures and a D3D12 info-queue warning at startup. Shutdown logged an unreleased ComputeBuffer/D3D12 resource and Unity memory diagnostics despite exit code 0. No managed gameplay exception occurred in the recorded journey; these graphics/runtime warnings remain unresolved and prevent a warning-free player claim. Earlier Editor console entries also contain resolved compilation and CLI compatibility/timeout errors; final successful suites and live play are reported separately.

No economics were retuned, no future route/captain systems were added, and GitHub Pages was not published or reconfigured. The next scoped milestone remains multi-stop route automation; graphics-plugin/shutdown warning investigation can be handled separately without changing these economic contracts.
