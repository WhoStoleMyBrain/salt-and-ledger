# Configurable procedural worlds

Generator **4** is the single supported player-world generator. The default creates **8–12 towns**, all seven current goods and industries, a connected sparse network, finite stocks/accounts and ordinary physical NPC merchants. Grand Commission defaults to **720 days**; Sandbox remains unlimited. No additional days increase objective quantities, external entitlements or Mint lifetime issuance.

## Configuration and reproducibility

`data/world-generation.default.json` is the canonical distribution configuration. `data/world-generation.schema.json` describes its structure. Both C# and Python validate bounds, known goods/industries, recipe inputs, mandatory Bread, impossible pairwise availability, institution references and incompatible geographical settings. Generation additionally validates actual placement, public/private assignment and path connectivity. Errors name the failed constraint and bounded search; settings are never silently relaxed.

Canonical recipes, building identities and goods remain in `data/content.json`. Existing generation economics (prices, capacities, intervals, wages and construction costs) are separate specifications in `GenerationEconomics.cs`; the distribution JSON does not redefine them. New canonical recipes automatically contribute direct dependency conflicts; a new commodity or industry also needs its ordinary economic specification.

| Setting | Default | Meaning |
| --- | --- | --- |
| `townCount` | 8–12 | Inclusive seeded count; supported range 2–64, subject to all other constraints |
| `geography.width/height/margin` | 1000 / 700 / 60 | Integer coordinate rectangle and inset |
| `minimumTownSpacing` | 110 | Minimum Euclidean separation |
| `minimumLaneDistance/maximumLaneDistance` | 110 / 450 | Inclusive direct-lane distance limits; never relaxed for connectivity |
| `extraLaneCount` | 3 | Exact extra links beyond the connected spanning tree |
| `candidateCount/placementAttempts` | 12 / 1500 | Bounded geography candidates and placement attempts per candidate |
| `industries[].publicTowns` | 1–2 | Towns with existing public facilities, independently drawn for each industry |
| `industries[].privateTowns` | 2–4 | Towns where that industry can be constructed |
| `enforceProductionSeparation` | true | Direct recipe producer/consumer exclusion across combined local capability |
| `requireSupplyChains` | true | Renewable potential upstream sources, including essential Bread |
| `population/prosperity` | 20–60 / 40–80 | Initial inclusive demographic ranges |
| `marketCash` | 500–1600 coins | Finite market endowment per town |
| `householdCashPerResident` | 20–42 coins | Finite initial household account, multiplied by initial population |
| `startingInventories` | Per-good ranges | Whole goods, granted once; Silver starts at zero |
| `npc.tradersPerTownPermille` | 500 | Merchants = ceiling(towns × density / 1000), bounded by explicit count settings |
| `npc.minimumCount/maximumCount` | 2 / 24 | Merchant population limits |
| `npc.startingCash/cargoCapacity/speed` | 300–500 coins / 3–6 goods / 100 | Independent finite merchant starting conditions |
| `institutions.*PortIndex` | null | Optional zero-based ordinal town indices for start, Mint, Silver source and shipyard |
| `fleetLimit` | 3 | Completed plus pending player ships; configurable 1–32 |
| `deadlineDays` | 720 | Grand Commission horizon; existing finite contract windows are capped to it |
| `economicCandidateCount` | 16 | Bounded capability-count/assignment attempts |

The public and private ranges are separate; neither implies that every public site permits private construction. The combined capability is checked before either subset is installed. Enabled industries have at least one potential site. Arrays of goods and industries and inventory keys are normalized ordinally before fingerprinting; culture and input enumeration order do not affect identity.

Current households require Bread enabled. Disabling supply validation permits deliberately finite upstream chains, but does not grant missing inputs. Disabling production separation permits local chains explicitly. Unknown content or unsupported economics fail with an actionable error. Exact seed-specific packing and availability can still exhaust bounded search; that reports failure rather than claiming mathematical impossibility.

## Geography and economic assignment

Placement uses a deterministic integer candidate stream and rejection sampling against minimum spacing, a simple blue-noise-style method suitable for a maximum of 64 ports. Lane distances use integer ceiling-square-root. Kruskal's minimum spanning tree uses only distance-valid edges. Additional links favor actual distance savings in the current graph. Among bounded valid layouts, the generator prefers larger summed nearest-town separation, then shorter network diameter, with stable ties. This controls overlapping clusters and excessive detours without optimizing an economic strategy.

Economic assignment draws public/private counts and geographically correlated suitability around seeded anchors. A bounded backtracking search considers the most constrained industries first and places only mutually compatible capabilities. Public and private subsets are then selected from the validated union. The first valid bounded economic candidate is used. Population, prosperity, cash and inventory draws use a separate stream. There is no runtime entropy, wall clock, hidden restocking or rescue transfer.

Towns have stable IDs `town_001` onward and distinct generated names. Names never select economic roles. Helpers such as the Bakery, Mill, Furniture and recovery destination resolve actual capabilities for current objectives and presets; they do not identify four exclusive town slots.

## Production specialization

For two distinct canonical recipes A and B, exclude co-location if an output of A is an input of B, or vice versa. This covers existing public factories, every permitted private blueprint, starter grants and future construction. The Mint is placed away from Silver capability, retaining physical delivery. The shipyard is likewise placed away from local production of its canonical Planks hull material; explicit shipyard indices constrain the same capability assignment.

For current content the direct pairs are:

- Farm / Mill: Grain.
- Mill / Bakery: Flour.
- Lumber Camp / Sawmill: Logs.
- Lumber Camp / Bakery: Logs.
- Sawmill / Furniture Workshop: Planks.

The rule is computed from recipes, not these names. It does not prohibit indirect chain membership: Farm and Bakery can coexist if their other capabilities permit it. Public inventories and private stock can hold imported goods regardless of local production eligibility. Structural feasibility means a connected potential source exists; it promises neither sufficient throughput nor profitable prices, wages, liquidity or a winning plan.

## Navigation, merchants and institutions

`SeaNavigation` minimizes the sum of `ceiling(lane distance / ship speed)` over the connected graph. Ties use total distance then ordinal path. Departure stores original/final endpoints and the summed duration. Every whole day decrements that voyage once. Intermediate transit does not dock, trade, complete route orders or count as a Commission visit. Unity renders the same path segment durations.

NPC policies resolve real public producers and industrial/household consumers; private-only configurations can use potential producer locations. They distribute routes across goods and short physical paths, with deterministic ties. Each has a separate finite company account and ordinary cargo, buying, selling and voyage rules. They can encounter shortages or lose money.

The Mint, Silver source and shipyard use generated valid locations or explicit configured indices. Industrial starter grants use eligible Bakery towns; an incompatible explicit starting location fails clearly. All fleet limits count pending orders. Objectives resolve actual towns/counts and filter unavailable templates before the deterministic draft.

## File-based runs and reports

```text
python tools/validate_data.py
python tools/validate_data.py --world-config data/world-generation-examples/narrow-specialization.json
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --world-generation --check
dotnet run --project samples/SaltAndLedger.Demo -c Release -- data/content.json --world-generation --config data/world-generation-examples/sparse-eight.json --seed 42 --days 365 --check report.json
```

The headless report includes configuration/fingerprint, actual towns and lanes, public/private capability counts, transport dependencies, local conflicts, renewable sources, central/leaf towns, travel diameter, population/accounts, rejected candidate constraints and final state checksum. `--check` verifies reversed enumeration, exact save round trips and seven further days of identical continuation. Without `--seed`, it runs −42, 42, 99 and both int32 boundaries. `--days` defaults to 30 and is bounded to 0–10,000. Failures return a nonzero exit status with the failed constraints.

Examples:

- `sparse-eight.json`: eight towns, a spanning tree without extra loops, fleet limit five, 540-day horizon.
- `twelve-town-commerce.json`: twelve towns, eight extra links, more merchants, fleet limit six, 1,080-day horizon.
- `narrow-specialization.json`: ten towns, exactly one public and one private town per industry, 330 maximum lane distance and reduced market liquidity.

Unity stages the same default JSON with `prepare_unity.py`. An optional `world-generation.json` under `Application.persistentDataPath` supplies a file-based custom run. Invalid configuration disables setup with its error; it does not fall back silently. Preview, draft and Start retain that configuration. Existing saves embed their own complete JSON; editing the file does not reinterpret a loaded run. No Custom Run editor is included in this milestone.

## Identity and verification

Run identity retains generator4, content3, valuation1, full configuration/fingerprint, seed, charter, pricing, Ascension, draft2, storage2, itinerary1 and economic rules1. Save schema7 rejects old saves safely and reconstructs the exact configured world before restoring mutable state. Retry starts its original configuration; Load resumes exact stocks, accounts, voyages and schedules.

See [Task3.1 verification](TASK-3.1-VERIFICATION.md) for actual results, test scope and the remaining Unity Editor/player checks. Bounded generation and simulation evidence do not establish long-run balance or universal profitability.
