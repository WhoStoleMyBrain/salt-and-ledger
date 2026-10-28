# Architecture

Salt & Ledger has one supported player world: Expanding Economy v3 with circulation1, pooled company stock2 and cargo itinerary1. `RunScenario` creates Grand Commission or Sandbox runs from immutable `RunIdentity`. There is one Unity scene and host: ArchipelagoLab. Prototype owners, scenes, modes and fixed challenges have been removed.

## Dependency boundaries

`SaltAndLedger.Simulation` targets C#8 / netstandard2.1 and depends only on Newtonsoft.Json. It owns content validation, pure world generation, exact calculations, regional state and portable save serialization. It knows nothing about Unity, paths, screens or documentation generation. The demo and xUnit project target .NET10. Unity consumes its staged DLL and canonical content; Unity's pinned UPM Newtonsoft package is the sole JSON assembly.

Canonical stable IDs live in `data/content.json`; structural validation uses `data/content.schema.json`. Python and C# semantic checks agree. `WorldIds` centralizes unchanged runtime town/actor/blueprint IDs. `ExpandingEconomyScenario` owns the current endowments and configurations. The base content scope and `ForExpandingEconomy()` extension remain explicit to keep validation and small recipe fixtures simple; player worlds always select the seven-good scope.

## One atomic owner

`RegionalWorldState` owns ports, companies, physical stores, ships, policies, construction, households, public development, accounting, objective history, day and revision. Snapshots are immutable. Instant commands change revision but never the day; no-op/rejected commands retain snapshot identity. Snapshot-bound previews reject stale execution. Working copies stage all changes, including FIFO, route cursor/budget and earlier daily attempts, before a single commit. Exceptional failure rolls everything back.

Company cash is counted once; ship cash is a compatibility projection used by the shared merchant evaluator. Each company/town pool is its sole private material/FIFO owner. Facilities reference that pool but keep individual schedules, lifecycle, enabled/rate/priority controls, production credit, batches and wages. The pool owns its one local Buy/Sell policy and trading counters. Cargo, public markets, other companies and other towns never alias the pool.

Daily order is stable actor priority/ID; town warehouse buying; due fiscal review; public production by port/priority/ID; private production by port/priority/ordinal ID; town warehouse selling; consumption by port/kind/rule; welfare/population and next-day quotes; voyages; construction completion; objective evaluation. All-account cash equals initial cash plus explicit Mint issuance. Regional cash additionally includes net inflow from the finite outside authority.

## Reused algorithms and bounded state

`SeededArchipelago` is the pure integer geography stream; `LivingCityGeneration` supplies domain-separated role draws. Their names describe algorithm versions, not alternative player modes. V3 composes them without changing positions, distances or role signatures.

NPCs still use immutable two-port `TradeRoutePolicy` and stateless `RoutePolicyEvaluator`, via a detached `MaritimeWorldSnapshot`. There is no second mutable maritime world. `IndustrialRoutePolicy` remains for controlled current economic research. These algorithms use ordinary regional trades/transfers/voyages and are not exposed as alternate player route editors. Player automation uses bounded ordered `CargoItineraryPolicy` instructions, with authoritative progress and an eight-operation daily limit.

Small custom test worlds retain unscaled quantities, fixed prices and separate-store configuration where they isolate exact arithmetic or rollback. They are not selectable modes or durable saves. Diagnostic circulation treatments, including economy0 controls, are likewise research configurations and cannot be saved as current runs.

## Identity, objectives and saves

Identity includes seed, generator/content3, valuation1, pricing, charter, Ascension, commission draft/signature, storage2, itinerary1 and economic rules1. Retry recreates that identity; loading restores exact state. The typed Grand Commission evaluator derives current conditions and bounded cumulative visits/net-sale history from committed state. All four goals plus solvency are required; terminal runs freeze.

`WorldSaveCodec` accepts only schema6 and current rules. Strict bounded JSON, checksums, configuration/content fingerprints, ownership, lots, quantities, accounts, routes and history are validated before returning a reconstructed owner. The host installs it only after success. A few zero-valued fixed-challenge fields remain in the explicit save shape; they do not enable retired challenges. See [save policy](SAVES.md).

## Unity and documentation

`ArchipelagoLabSession` owns presentation selection, playback pacing, bounded observation and safe local slots. `ArchipelagoLabController` and its focused UI partials consume snapshots, previews and command results. The map transform, text scales, navigation, drafts and economic signals are read-only presentation. Opening transactional/reference screens pauses playback; explicit commands alone mutate the economy. There is no UI-side pricing, production or travel model.

`tools/generate_docs.py` validates and generates `docs/reference/` and `generated/encyclopedia.json` from canonical content and shared player help. Unity uses that same format2 encyclopedia offline. CI checks freshness, links and the strict MkDocs build. Current experiment reports contain deterministic values, no machine paths or timestamps. Pages remains manual-only.
