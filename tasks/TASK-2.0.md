# TASK 2.0 — The Expanding Economy

**Salt & Ledger — one integrated, ambitious implementation assignment**  
**Status:** Approved design direction, implementation specification (not implemented by this document).  
**Repository:** `WhoStoleMyBrain/salt-and-ledger`  
**Baseline reviewed:** `main` at `64fc25e20a1377db84f5e1bddd11d41822c8f6b7` (Task 1.9 complete). Check the current HEAD before editing.  
**Intended in-repository path:** `tasks/TASK-2.0.md`  
**Scope relationship:** This **combines and supersedes** the previously discussed next Task 1.10 (productive growth, economy recovery, industry usability) and Task 2.0 (player fleets, saves, diversified objectives) into **one milestone**. No separate 1.10 delivery is expected.

---

## 0. Mission and player-facing definition of success

Make Salt & Ledger's **Living Cities** evolve into a genuine **expanding maritime economy**. The player must be able to diagnose a shortage, invest in the appropriate location, expand existing production chains, ship goods on complementary routes, strengthen local purchasing power, and grow a company whose value includes factories, inventories and ships. Regional money must circulate through real transactions and be able to **expand through explicitly accounted mechanisms**: limited trade with an external authority and an optional, uniquely located **Royal Mint**, commissioned but not owned by the player. This is not an endless-cash sandbox, an automatic population stipend, or an economy in which the only way to succeed is to extract the last coins from a starving town.

The complete playable experience should support a credible journey such as:

1. Start a seeded world and see where industries **may** be constructed and where they cannot.
2. Notice that a public Mill limits Flour and therefore regional Bread production; fund a private Mill in an eligible town, then identify the next bottleneck rather than making Flour appear for free.
3. Build a complementary production/commerce network with existing Farms, Lumber Camps, Mills, Sawmills, Bakeries and Furniture Workshops. **Every existing production type must be buildable by the player somewhere in every new-version world**, not necessarily in the same town. Multiple sites of one type must be possible where eligible.
4. Acquire a second and possibly third player ship, configure each independently, move actual input and output stock, and supply imports while exporting something that returns purchasing power.
5. Observe **proportional**, potentially fractional paid consumption: 15 residents demand 50% more of a given essential than 10 under the same parameters. Ships continue moving whole cargo units.
6. Watch cash, household income, wages, stock and Company Value change independently and correctly. Buying an asset does not automatically make the player's economic position look like a loss.
7. Use finite external export contracts or commission the central Royal Mint and transport actual Silver to it. New currency enters **specific accounts** under recorded, bounded rules; it is never silently generated at a day boundary.
8. Save while a ship is in transit, a facility/ship is being constructed, some orders are blocked, and a challenge is in progress. Reload, continue, and obtain precisely the same economic outcome as uninterrupted execution.
9. Complete an appropriately difficult new objective involving sustained welfare, functioning industry, Company Value and/or monetary development; inspect transparent financial and economic results.

**Player agency and intelligible consequences matter more than guaranteeing indefinite prosperity.** A poorly designed import route, unprofitable industry, excessive hoarding, dead-end logistics, unaffordable essentials, inadequate wages or an unproductive Mint must still be allowed to fail. The player must be able to **understand why**, and multiple deliberately tested strategies must show real recovery is possible. Favor real mechanics over making benchmark numbers green.

This is a **real implementation**, not only a design document, a headless prototype, a pile of mocks or a future-feature scaffold. Bring the combined systems into ArchipelagoLab, exercise them in a Windows development player when tooling permits, and produce verifiable experiments, tests, reference material and acceptance evidence. Do not claim real Unity/player verification if only headless tests were performed.

## 1. Read the repository and respect established boundaries

Before substantial changes, read `AGENTS.md`, `docs/GAME_DESIGN.md`, `docs/ARCHITECTURE.md`, `docs/MECHANICS.md`, `docs/LIVING_CITIES.md`, `docs/ECONOMY.md`, `docs/DECISIONS.md`, `docs/ROADMAP.md`, `docs/RUNS.md`, `docs/PLAYTEST.md`, `docs/UNITY.md`, the latest Task 1.9 experiments, and the relevant simulation/Unity code. Inspect existing tests and reports, particularly `LivingCityGeneration`, `LivingCityScenario`, `RegionalWorldState`, snapshots, trading/pricing, historical FIFO, automation stages, construction, run identity, and ArchipelagoLab's single-player-ship assumptions. Read `tasks/` conventions if present; this task spec may be supplied separately rather than checked into the repository.

Keep the proven structural principles:

- One authoritative, deterministic, **atomic** regional owner; no separate Unity economy, no unseeded runtime random events, no dependence on wall-clock time for economic state.
- Portable simulation in `src/SaltAndLedger.Simulation` targeting **`netstandard2.1` and C# 8**; no Unity, filesystem policy or UI implementation in the simulation library. Demo and tests use .NET 10. The Unity host uses the staged simulation DLL and canonical content.
- Stable IDs and immutable canonical definitions; Python and C# validation agree. Prices, recipes, goods and help have explicit, validated sources, not duplicated UI constants.
- Checked arithmetic; exact transfer and pricing semantics; authoritative snapshots/previews; no stale decisions or partial commit on an exception. Expected shortages are outcomes, not reasons to corrupt the world.
- Company cash is authoritative once, not also counted as independent ship cash. Separate market, household, company, public authority and (if modeled) external account ownership. Goods are never teleported by remote management.
- FIFO historical acquisition/material basis remains separate from sale revenue, actual wage expenditure, capital and holdings; no fictional paid basis for starter grants. Preserve existing accounting terms and never call an arbitrary value change realized profit.
- Existing manual/automatic routes use shared real transactions; per-ship evaluation is bounded and stateless over the committed snapshot. **Automation is configurable execution of player decisions, not a magical optimal trader.**
- Keep the older single-location world, two-port MaritimeWorldState, TradingLab, Classic, seeded geography v1, and Living Cities generation v2 as executable regression baselines. Historical examples and reports must not quietly adopt new rules.
- Preserve Ascension 0 instant and Ascension 1 delayed player facility construction in old rulesets. Preserve exact retry/reroll semantics, terminal freeze, and current player settings. Do not hand-edit generated encyclopedia/reference artifacts; regenerate them.

### 1.1 A versioned compatibility boundary is required

This changes fundamental economy contracts. Implement an opt-in **Expanding Economy / Living Cities economic ruleset v3** (name can be refined), selected from the normal run setup. It retains the existing four-town seeded geography and complementary economic roles but adds the new quantity, monetary, industry and player-development rules. **Do not silently reinterpret v2 seeds or existing v2 run identities as v3.**

The version distinction must travel with run identity, reports, save files, comparison groups and displayed setup/result metadata. V2 deterministic signatures and old reports are regression fixtures. If new canonical goods/buildings would alter historical reports, introduce the **smallest explicit versioned catalog/extension or scoped content selection** necessary, keeping the data canonical and validators/docs in sync. Do not let a broad global catalog expansion silently alter historical quantities, scenario behavior or archived report bytes. Explain the chosen boundary in architecture documentation.

Do not duplicate a complete v3 simulation as an unrelated second economic engine. Share transaction, pricing, inventory, batch, route, ownership and accounting code through **concrete, minimal** refactors with explicit compatibility adapters. Avoid speculative plugin architectures or generalized future-economy frameworks.

## 2. Foundational quantitative contracts: divisible physical goods, exact money

### 2.1 Fractional goods, not binary floating point

In v3, population demand is proportional, **not `ceil(population / populationPerUnit)`**. For existing Bread configuration, each four-day need equals `population / 20` Bread (unless explicitly changed in authored v3 rules). E.g. 10 residents need 0.50, 15 need 0.75, 20 need 1, 21 need 1.05 and 30 need 1.50. The same proportional logic should support Furniture/other explicitly population-scaled needs and preserve their own intervals and eligibility rules. Full, partial, stock-short and funds-short fulfillment must be measured in actual fractional quantities. Population change must not introduce huge integer demand cliffs.

Use **fixed-point integer subunits or exact rationals with retained remainders**, not `float`/`double` in authoritative balances, goods, prices, accounting, routes or saves. A default candidate is **10,000 physical subunits per cargo unit**, subject to proving numeric limits and avoiding loss of precision with configured population denominators. If exact authored fractions are finer than the selected quantum, carry the exact residual deterministically rather than discarding it at every due date. Make unit scale/rounding specified, checked and invariant across transactions and persistence.

One coherent physical quantity model underlies markets, warehouses, private/public production, consumption, Silver stocks, FIFO, transfers and ship cargo. Do not implement disconnected town floats and ship integer inventories with conversion losses:

- A ship buy/sell/transfer order is constrained to **whole cargo-unit multiples**, with unit-count capacity enforced from actual cargo quantities. Players cannot load a fractional cargo unit just to bypass capacity or pricing.
- Resident purchases/consumption may move fractional commodity quantities; after a town consumes 0.75 from 2.00 Bread, its stock is exactly 1.25.
- Canonical production recipes continue executing **complete batches** in declared recipe units, applied to the scaled quantity system. An extraction only creates declared output. No fractional input/product is fabricated because a household wanted it.
- Market stock may contain 2.75 Bread; a ship can acquire two whole units and leave 0.75. Physical ownership totals and FIFO basis remain exact.
- Historical worlds may continue to expose their old whole-integer API. V3's new quantity contract must not change those calls or published results.

### 2.2 Money, exact charges and bulk pricing

V3 should also use a **documented exact monetary subunit**, with `long` per-account storage and wider checked intermediate arithmetic. A coherent candidate is 10,000 money subunits per displayed coin, compatible with meaningful price for 1/10,000 of a good. You may select a better scale and refactor strategy if tested, but **never** silently round a paid fraction into a free unit or charge repeated rounding penalties on split orders.

Preserve the proven stock-sensitive pricing properties and make them true for divisible quantities:

1. The initial quote, maximum executable quantity, exact total and resulting state must be identical between preview and execution.
2. The total payment for an uninterrupted order equals that of sequentially split orders operating along the same unchanged pricing curve, **including fractional boundaries**.
3. Same-edge ask is never below bid, and a buy/sell round-trip across an unchanged local model must not be profitable. Cross-port transport margins are allowed.
4. Charges paid and received are exactly equal; credits and debits never disagree by a subunit. A failed trade changes nothing.
5. Bounded algorithms with exact integer/rational arithmetic are required: **do not iterate one subunit at a time or every distinct quoted price** for large orders.
6. Correctly account for bid/ask direction, existing stock, partial retail purchases, money and quantity overflows, and current compatibility semantics of `UnitPrice` vs bulk total.
7. Show human-friendly quantities and prices in Unity without formatting affecting economics.

Prefer a mathematically defined edge-cost/prefix-potential convention with explicit rounding so that split consistency follows algebraically. Keep the old whole-unit pricing path and its golden tests as a baseline. Include property-based or wide bounded randomized tests for monotonicity, reversal, splitting, numerical extremes and cultures. Document the actual formula, bounds, scaling and any changed `UnitPrice` meaning.

### 2.3 Accurate household welfare with fractional consumption

A food observation records exact desired quantity, paid-consumed quantity, unavailable stock amount, unaffordable amount, payment and initial-population-equivalent need. Windowed fulfillment, prosperity, luxury demand, household coverage and recovery objectives must use those quantities with stable, documented rounding **only when producing a final display/score percentage**. A declining population must not game the recovery objective by shrinking its denominator. Keep the existing bounded history and slow population/prosperity changes rather than adding per-person simulations. Retain essential-before-luxury purchasing and a real, quantified essential reserve. Do not change the four-day Bread cadence merely to hide a stock/monetary problem; test cadence separately if later needed.

## 3. Expandable, geographically specialized production

### 3.1 All existing industry kinds available to the player

V3 must provide legal **player-owned** construction blueprints for each currently modeled real production building:

- Farm (Grain extraction)
- Lumber Camp (Logs extraction)
- Mill (Grain to Flour)
- Sawmill (Logs to Planks)
- Bakery (Flour and Logs to Bread)
- Furniture Workshop (Planks to Furniture)

Each must be constructible **in at least one reachable town in every generated v3 world**, including unusual signed int32 seeds. Not every town is eligible for every industry. Retain the deliberate dependency: a town with a Lumber Camp and Furniture Workshop can be unable to construct a local Sawmill and must import actual Planks. Likewise, use local suitability to preserve useful food-chain dependencies. No single town needs to be fully self-sufficient. Eligible roles/towns and physical source chains must be previewable and enforced in the authoritative construction command, not just in Unity.

Existing v2 blueprints and town capabilities remain unchanged. The v3 generator is pure, versioned, bounded, based on the **same v1 geography for the same seed**, and must not reroll or secretly rescue a seed after observing strategy outcomes. Prove the per-seed role/eligibility invariants structurally; support the proof with goldens and broad seed samples.

### 3.2 Multiple facilities of the same type

Remove the **one owned facility per blueprint per town** prohibition for v3. Each facility has unique stable ID, separately owned storage, production schedule, wages, priority and policies. Creating a second Mill does not overwrite or merge the first. The limiting factors should be funds, inputs, wages, storage, real market demand and optional explicit town-site capacity—**not an arbitrary unexplained one-Mill limit**. Do not create a speculative upgrade tree. Keep current starter-factory ownership and constructed-facility accounting honest.

Use existing Ascension 0/1 timing rules for newly purchased player industry in v3, and preserve granted starter operational status. Private extraction pays configured real wages just as private conversion does; failed/unfunded batches do not pay and do not create resources. Test physical quantities, employer cash, household wages, FIFO/material accounting, output limits, multiple-priority ordering, partial stock, and exceptional rollback.

### 3.3 Verify renewable food capacity and actual bottlenecks

Task 1.9's v2 map has a single public Mill producing at most **one Flour per two days**, while four towns together request **five to eight Bread every four days** at initial populations. A Flour produces two Bread, so maximum renewable food under existing public capacity is roughly **one Bread/day**, before production blockage or logistics. This is a demonstrated structural limitation, not merely poor NPC route configuration.

V3 must let the player **increase actual Flour capacity** and construct complementary upstream facilities. Do not simply double Flour stocks on load or change production output invisibly. Report the next bottleneck when Mill capacity increases—Grain, Logs, wages, output limits, transport, demand or liquidity. Demonstrate both a real expanding food chain and an overbuilt facility that fails for economically legible reasons.

## 4. Monetary architecture: circulation *and* controlled expansion

### 4.1 Explicit accounts, issuers and boundaries

Retain finite separate cash balances for company owners, local market accounts and aggregate households. Add only the new **concrete accountable parties** genuinely required for the v3 mechanisms: a regional/royal public treasury and an explicitly funded **external trading authority** (or equivalent single, well-defined external payer). Decide and document exactly which accounts lie **inside the region's reported money supply** and which lie outside its boundary. Avoid counting the same coins as treasury, ship cash, company cash and household cash simultaneously.

A sound example identity is:

`regional_cash_now = regional_cash_initial + minted_currency + external_net_inflow - currency_retired`

and, if the external authority has an explicitly modeled finite cash account:

`all_account_cash_now = all_account_cash_initial + minted_currency - currency_retired`

All values are exact and audited after every commit. External trade transfers existing money **from a real finite external account** into regional accounts, increasing *regional* liquidity but not global cash. The Mint is the **sole initial source of genuinely new coins in v3**, apart from any separately and explicitly authorized issuance defined by this task. Do not count an external transfer as minted currency or count the same issuance twice. No daily cash reset, unbounded treasury, population stipend, or hidden NPC credit line. Any future money retirement must be explicit; absent a justified design, do not add one now.

Use structured committed events for mint authorization/issuance, Silver reserve transfers, external purchases, treasury expenditures and account delta. Ensure failure of a later stage rolls back earlier cash moves, physical changes, voyage progress and deadlines in the same atomic operation.

### 4.2 Fix circulation through actual economic participation

A town importing Bread must not **inevitably** lose all market and household liquidity when the player builds a sensible, complementary economic network. At the same time, an import-dependent town with no employer, exports, investment or public spending can quite reasonably become insolvent. There must be no rule that magically refunds Bread imports.

Make the player able to:

- Supply genuine imports and purchase/export local output back out, returning money to the supplying town's market.
- Start a viable eligible local employer, paying actual wages that allow residents to buy essentials.
- Clear output stock/production ceilings through market exports and procurement, restarting productive work.
- Invest in capacity and logistics with measurable opportunity costs.
- Access finite public/external purchases that inject regional liquidity **only in exchange for actual delivered goods/services**.

Do not promise that everyone's cash increases. Measure **market liquidity, household purchasing power, paid consumption, production activity and Company Value** separately. In a closed transfer, one account's increased cash must come from another. Real welfare/productive assets can rise without minting; minting adds liquidity, not food or automatic wealth.

### 4.3 Bounded early-game external commerce

Introduce a small, legible external market/royal trade office rather than an infinite offshore buyer. It offers **deterministic, limited contracts** for some existing meaningful commodities (e.g. Planks, Furniture, possibly food). Contract terms define item, quantity, price, availability window, total budget, and completion/delivery conditions. Prefer simple authored/versioned contracts or a bounded deterministic schedule over a giant procedural quest generator.

Critical requirements:

- The external payer's funding is finite and visible/auditable. Its purchasing power cannot reset for free when a contract expires. Any periodic newly authorized funding must have an explicit outside-source account and be recorded, not hidden income.
- The goods must be owned and physically delivered to an actual valid harbor, trade-office stock or shipping endpoint. A shipment cannot teleport from a distant factory or private warehouse.
- The external authority takes possession of the goods and records a physical sink/export or warehouse; its cash really goes to a market or company as declared. A fulfilled contract cannot be replayed for free or double credited.
- Prices and quantities are bounded; player and NPC access should follow clearly authored eligibility rather than NPC cheating. An open contract cannot purchase beyond its remaining funded amount.
- The flow should offer a useful **early/midgame alternative** to extracting the last cash from other towns and should make productive exporting worthwhile. Completing it must not be an automatic win or an inexhaustible income source.

Choose simple provisional terms only after checking the real v3 flow. Explain the economic boundary: external trades move money *into the region* from the limited external account, **not** newly printed coins.

### 4.4 The Royal Mint: unique, central, public, commissioned by the player

Add a **Royal Mint** as a special regional institution, not an ordinary player-owned factory. Exactly **one designated central town** is eligible to host it, deterministically chosen under v3 roles/geography (e.g. the processing/administrative town, never an arbitrary hardcoded city name). It may start unavailable and be **commissioned or funded by the player**. It is not automatically included in the player's Company Value and does not grant private factory stock, a cash account for the player, or unrestricted control of currency policy. The commissioning payment transfers to a real public account; it is not silently destroyed. Its activation delay, costs, constraints and state are authoritative and explained in UI. Apply relevant construction timing rules explicitly; do not accidentally inherit the wrong private-facility timing or create early issuance during construction.

Introduce **Silver** as the one new monetary commodity for this milestone, with a physically modeled **Silver Mine** (canonical extraction building and recipe) and real eligible location(s), wages, finite stock/capacity and transportation. Do **not** add Gold, Iron, full mining tech trees or complex precious-metal exchange mechanics yet. Silver mining must be available somewhere in every v3 world, ideally **not colocated with the Mint**, making a real supply route strategically useful. Existing goods, production and logistics continue to matter.

A mint batch must require **actual physically delivered Silver** held at the authorized Mint endpoint. Preserve metal provenance/quantity via explicit movement into a monetary reserve/vault (or an explicitly audited physical conversion sink). A mint batch authorizes a fixed positive currency issuance amount and may pay a share to the supplied Silver owner and a share to the public treasury. **Every recipient, new coin and consumed/reserved unit must be auditable.** The shares and provisional exchange ratio are design parameters, not free grants. A portion arriving in the treasury must be spendable only through declared funded activities (e.g. procurement of actual goods or wages for actual public work), not a periodic unstated bailout.

Constrain issuance through the Silver input, ownership/permission, actual Mint completion and production interval/capacity, maximum batch size and budget/policy limits. Do not permit repeated issuance using the same metal or a silver-buy/mint/resell exploit that yields riskless, unbounded cash under unchanged state. Ordinary investment returns from a productive, finite Silver Mine are allowed. **Do not let a player purchase the Mint as a private facility or treat mint issuance as company trading profit without recognizing the Silver surrendered and the issuance source.**

The Mint should be a **strategic high-level optional route**, not a compulsory rescue for all challenges and not the source of automatic population-indexed income. Model no bank loans, credit, interest, inflation-targeting central bank AI, royal taxes, coin denominations or monetary politics. A concise exact monetary regime is better than pseudo-realism.

### 4.5 Avoid automatic money creation/destruction from basic vs luxury consumption

**Do not implement** the preliminary idea that consuming Bread automatically creates money or purchasing Furniture automatically destroys it. Both essential and luxury purchases transfer existing money from households to a market; goods are physically consumed. A luxury can be economically discretionary and alter prospects/prosperity but is **not a money sink** unless an explicit account or retirement operation is later designed. No daily per-capita cash grants. Increased population means increased real needs and productive opportunities, not an automatic right to freshly minted currency.

Document the difference between (a) newly extracted/processed **goods**, (b) **Company Value**, (c) regional **money supply**, (d) distribution/velocity of money, and (e) living-standard outcomes. Show a high-monetary-liquidity world with insufficient food still failing, and a food-rich but household-poor town unable to consume everything until real income or a funded intervention occurs.

## 5. Player fleet expansion and coherent industrial logistics

### 5.1 Purchasable ships and real shipbuilding costs

Support the starting player ship plus **up to two additional purchasable owned ships** in the integrated milestone. The exact cap is deliberately bounded for UI, performance and tests; do not implement arbitrary unlimited fleets. Each ship has a distinct stable ID, dock/route/voyage state, cargo, capacity, speed/upgrade-free type, automation policy and stage. All player ships share **one** company cash account. NPC merchants retain the same finite execution rules and their existing fleet configuration unless a scenario explicitly declares otherwise.

Prefer a **shipwright/shipbuilding service at eligible harbor(s)** rather than a fake instant cash-to-ship creation. The commission must have an explicit quoted payment from player to an actual market/public account, a stated lead time, and a real finite physical requirement (e.g. market Planks consumed for the hull) plus funded labor if it is included. This is a bounded service, **not a whole ship-construction tech tree**. No second ship spawns at sea, without payment, before completion, or with fabricated cargo. An unfunded/unsupplied order fails or waits without duplicating resources. Ship commission state and completion participate in atomic rollbacks and saves. Define a deterministic time relative to the same authoritative day. Existing Ascension construction timing must not accidentally change.

When commissioning creates a ship asset, its ownership and reference value appear in Company Value; a paid pending order appears as **construction in progress**, not a second completed ship. A commissioned public Royal Mint does *not* appear as privately owned ship/industrial capital.

### 5.2 Fully ship-targeted commands and policies

ArchipelagoLab currently assumes the single fixed player ship in several places. Refactor the minimal simulation/host APIs so **selected `shipId`** is authoritative for manual trades, docked loading/unloading, sailing, route editing, route enablement and previews. Validate owner, actual dock, capacity, compatibility of existing cargo and snapshot revision. Remote management can issue permitted local industry/market controls but cannot teleport goods into a ship.

Each ship independently performs its existing bounded dockside action chain, and multiple ships run in stable priority/ID order through **one atomic daily world operation**. Preserve the policy's stage and carried cargo on waits, disablement and save/resume. Explicitly prevent a route edit or ship switch from silently changing another ship's policy. No time/space duplication from concurrent docked ships. Old single-player-ship fixtures must continue to behave identically.

The UI must provide a legible fleet selector and statuses (docked/sailing, destination, arrival estimate, cargo, route stage, pause reason). The player should be able to maintain complementary routes—e.g. importing Bread and exporting locally produced resources—without repetitive manual actions. **Do not introduce a fully general graph-routing language or an optimizing AI fleet manager**; use the existing understandable leg/policy concepts, extended only as required for multiple owned ships.

## 6. Company Value: cash plus owned productive capital

Introduce a prominent **`Company Value`** metric for v3, computed deterministically from authoritative ownership, with an explicitly disclosed **reference valuation** rather than claiming to predict liquidation proceeds.

`Company Value = liquid company cash + owned goods reference value + owned completed-ship reference value + owned completed-facility reference value + paid construction in progress`

Implement as a checked exact integer amount in v3 monetary subunits; use a wider intermediate as needed. Every economic asset is counted **once**:

- Goods in all owned ships and private warehouses, including held starter grants, are physically distinct stores. FIFO paid basis is **not** a second asset to add on top of those goods.
- Owned completed facilities use stable authored/versioned reference values, normally aligned with the cost of replacing that facility in a comparable state. Owned granted facilities have the **same value** as equivalent purchased facilities although their historical paid basis remains zero.
- Each owned ship has an explicit reference asset value, defined for its type/capacity; starter ships count as assets, regardless of no historical player expenditure.
- Pending player-funded facilities/ships count only the **paid capital amount** or explicitly documented stage value until completion; prevent double counting when the completed asset replaces the pending order.
- The publicly commissioned Mint, public markets, household cash, other companies and the royal treasury are **not owned by the player** and must not appear in Company Value. Silver surrendered to the Mint reserve is no longer a company asset.
- Use a stable goods reference-price schedule, **not** current local spot bid multiplied by all owned goods. Current-market liquidation could fail for lack of buyers, move the quote, or require voyages; do not mislabel reference value as realizable proceeds.

Show **Cash**, **Company Value**, and an accurate **Operating Surplus After Wages** (or better clearly defined operating measure) side-by-side, with an explanatory breakdown and **change versus starting Company Value**. Capital spending can reduce Cash and increase productive assets without reducing headline Company Value by the full expense. A newly constructed asset may depreciate or change reference value only under an explicitly modeled rule; do **not** add depreciation in this milestone. Distinguish reference asset value from historical FIFO cost, actual wages, realized material margin, sale revenue, held basis and unpaid obligations. Preserve v2 scoring/calculations and other historical uses of cash.

Where objectives need solvency, retain explicit **cash** requirements. New objectives can use Company Value if that promotes meaningful capital allocation, but avoid a pure-value exploit where a player wins by repeatedly passing stock between stores or purchasing an overpriced building and instantly claiming fabricated realized profit. Exact comparisons include charter/start asset differences; do not rank unlike world/charter/mint/pricing configurations as if equivalent.

## 7. Durable save and resume: exact continuation, not a replay approximation

Add supported **local, offline Save / Load / Continue** to the run UI, for Classic, seeded-v1, Living Cities v2 and v3, or explicitly justify any narrower supported scope. This milestone should aim to support all active run modes rather than v3 alone. Persistence is **host-owned**; the portable simulation can expose concrete serializable authoritative state and validated restoration, but may not choose file paths, call Unity APIs or read/write files itself.

The versioned save must contain everything necessary to continue precisely, including:

- Exact complete run identity: world mode, seed, generator/ruleset versions, pricing, charter, challenge, ascension and content/valuation version identifiers.
- Day, revision, immutable configuration fingerprint and challenge progress/status, including terminal success/failure/abandonment.
- All market cash/inventories/quotes or committed pricing parameters, household balances, bounded food histories, prosperity/population/demand remainders, all treasury/external accounts, Mint state/metal reserve, monetary issuance/outside-flow counters and commission schedules.
- Every company account and every ship (owned/NPC), position/remaining voyage time, cargo, policy, enablement and route stage.
- All private facilities, production schedules, construction/ship orders, automation policies, warehouses, historical FIFO open acquisition lots, paid basis totals, wages and cumulative accounting aggregates.
- Deterministic IDs/counters and any other authoritative information whose loss would change the next operation. UI transient focus, frame interpolation and preferences need not be authoritative economic state.

Use a **concrete, bounded, schema-versioned** save DTO and validated restore; no arbitrary runtime type-name deserialization. Unsupported versions, mismatched content/ruleset identity, corrupt/truncated files, duplicate IDs, negative or overflowing accounts, inconsistent physical stock/FIFO or impossible voyage/state should be rejected clearly **without discarding the current valid session**. Do not partially restore a saved world. On-disk writing should use a temporary file and atomic replace/rename where available, preserving the previous save on failure. Include safe slot names/paths, local-only storage, at least one explicit save slot and a useful list/continue flow; a modest finite slot limit is welcome. No cloud saves, user accounts, cross-device sync or speculative migration framework.

**The proof is continuation equivalence:** run a deterministic sequence uninterrupted; separately save after various difficult states, load into a fresh host, and execute the same remaining commands. The final economic snapshot, committed operation events/fingerprints, money/silver reconciliations, FIFO totals, challenge outcome and Company Value must agree. Include cases with fractional goods, fractional cost basis, multiple ships with different route stages, pending player/Mint/ship construction, expired/partly fulfilled external contracts, committed currency issuance and blocked payroll. Save/load must not itself advance a day, mint, pay wages, reevaluate a contract, reroll, or create a second copy of owned cargo.

Make active-run replacement/Exit confirmation mention **unsaved progress** appropriately; after an explicit successful save it should not misleadingly claim that progress is necessarily lost. A terminal saved run must remain inspectable but terminal-frozen. Retry Same World must still create a fresh initial world rather than load an old save. Preserve persisted text-size settings independently of run files.

## 8. New challenges and differentiated goals

V3 needs **at least three materially different, fully playable and measured new objectives** that use the newly implemented systems rather than three Bread-delivery aliases. Select transparent thresholds/deadlines from controlled strategy trials, then document the selected values. Suggested archetypes (names can change):

1. **Sustainable Recovery / Regional Provisioning:** a target or multiple towns repeatedly **pay for and consume** enough essentials over a sustained window, retain population/prosperity criteria, and show a continued source of relevant wages/trading capital rather than passing with a one-time stock dump.
2. **Industrial Network / Commercial Expansion:** build actual complementary facilities, process and deliver some upstream/downstream goods (e.g. Flour/Planks and Furniture), and achieve a **Company Value** target with a separate modest company-cash solvency floor and a measured real operating or delivery outcome. No reward merely for shuffling the same goods.
3. **Royal Commission / Monetary Development:** commission/activate the public Mint, transport real Silver, complete a bounded actual issuance and/or external contract, and demonstrate a regional-economic benefit with finite monetary accounting. Minting alone should not win without the specified economic outcome.

Each should support legitimate alternative strategies when reasonable, a meaningful failure case, a clearly shown objective/deadline and the usual terminal freeze, repeatable retry, qualified score grouping and honest results. Preserve the old Coastal Supply / Regional Relief / Enterprise Ambition / original City Recovery exactly for legacy modes. Avoid a universal unqualified ranking of assets across fundamentally different charters or starting economies.

The sandbox remains available without a forced terminal goal. Do not make the Royal Mint compulsory in every v3 scenario.

## 9. Unity UI: make the enlarged economy readable and playable

ArchipelagoLab remains the actual game surface. Preserve its main menu, world setup, encyclopedia, trade modal, industry panel, ledger, results, pause/Escape/modal priorities, focus handling, map interpolation and four existing text scales. Use the **authoritative simulation** for all prices, stock/cash limits, construction dates, ownership, eligibility, route waits, valuation and challenge outcomes. Opening/closing a panel, switching ships, editing a draft or inspecting a quote is passive: it never advances time or mutates the economy until an explicit action is applied.

### 9.1 Compact, actionable company and town views

Primary gameplay at **1366×768 Default** should not require scrolling a giant dashboard to reach its main actions. Do not shrink text to fit. Apply tabs, concise overviews, progressive disclosure and contextual details:

- **Company summary:** Cash, Company Value (with composition and change from initial), wages/operating surplus, fleet capacity, current important waits, challenge progression and money introduced into the region.
- **Town overview:** population, prosperity, essential desired/affordable/purchased quantities, next needs, market vs household cash, relevant jobs/wages, leading stock shortages/affordability problems, and **buildable vs disallowed** industries.
- **Industry/production chain:** a meaningful explanation of the *next* limiting condition: missing input, employer payroll, output/storage cap, construction delay, destination lack of funds, shipment in transit, applied policy price/reserve limit. Explicitly distinguish **last attempted failure** from **current predicted eligibility**. Make draft policy vs applied policy evident, with useful existing suggested presets.
- **Fleet:** selected ship, cargo/hold, state, arrival, per-ship manual actions and route stage. Multiple ships must be independently discoverable and editable without confusing their command target.
- **Royal/external economy:** a concise contract list (good/amount/payment/remaining budget/deadline), Mint location/commission cost/activation state, required Silver, next issuance possibility, treasury and issuance counters. Clearly say public Mint is not owned by the company.
- **Company valuation:** a transparent drill-down with inventories, ships, facilities, work in progress, excluded public property and reference vs liquidation distinction.
- **Save/Continue:** explicit controls and useful incompatibility/corruption errors, never secretly replacing the current run before a valid new one is available.

Example states to render from real snapshots, not hardcoded text:

> Bakery cannot attempt next batch: Flour 0.00 / 1.00; payroll funded; awaiting Flour delivery in 3 days.
>
> Town has 7.25 Bread but households cannot afford the next 1.05; market money and household money are distinct.
>
> Second ship: returning with Logs, arrival in 2 days; no unauthorized loading of another ship's stock.
>
> Royal Mint: commissioned and active, Silver reserve 0.00; no issuance authorized until metal is physically delivered.

At 1366×768 and 1920×1080, review Compact, Default, Large and Extra Large. Longer encyclopedia and accounting histories may scroll internally, but avoid a permanently scroll-dependent primary trade/industry/fleet workflow. Retain modal input interception, keyboard Cancel/Escape priority, focus recovery, visible disabled reasons, and integer ship-order controls alongside fractional town-goods displays.

### 9.2 Encyclopedia and player explanations

Add synchronized, generated or authored-but-shared help explaining exact fractional consumption, monetary subunits and price totals, employer funding, external trade vs minted currency, public ownership of the Mint, Silver source/transport, regional vs total money, Company Value, per-ship orders, save/retry semantics and new objectives. Reference the same canonical names, recipes and values. Do not hand-author a different recipe in Unity than the generated website describes.

## 10. Headless economic experiments: test actual behavior, not only invariants

Add a separate deterministic `--expanding-economy` demo/report and `--expanding-economy --check` freshness mode. An opt-in deeper multi-seed mode is encouraged; **do not run the exhaustive sweep in every CI job**. Preserve all prior report bytes and run previous modes with their existing `--check` options.

### 10.1 Required comparison dimensions

Use several known seeds including **-42, 42, 99**, both pricing hypotheses where supported, both ascensions and selected charters. Additional seeds and longer periods are strongly encouraged, but the normal CI experiment must remain bounded. Use fixed documented strategies and identical initial v3 worlds per comparison. At minimum contrast:

- **V2 historical control:** preexisting long-run loss of population under its prescribed policies; old v2 must be unchanged.
- **V3 idle/no expansion:** money and production evolve only by declared baseline policies; genuine failure remains possible.
- **Merchant-only imports:** sells Bread to a target without deliberate complementary export route; documents which accounts are drained.
- **Balanced commerce:** imports essentials **and** buys/exports locally produced goods to return cash, without Mint; separate stock blockage, policy and actual external demand effects.
- **Industry capacity expansion:** constructs a private Mill and at least one compatible upstream site, executes all procurement and actual paid batches, distributes resulting goods through physical voyages, and measures next bottleneck.
- **Monetary pathway:** physically mines and transports Silver, commissions the central Mint, issues finite money, exposes treasury distribution and public expenditure; compare with matched no-Mint control.
- **External export contracts:** actual paid goods shipped/fulfilled, bounded by payer funds; compare cash distribution with an otherwise identical world lacking the contract.
- **Multi-ship production network:** at least two owned ships operating simultaneous legitimate policies with no duplication, plus a save/reload continuation at an inconvenient intermediate point.

This is not a requirement that every strategy wins every seed. Report **genuine outcomes**, counterexamples and why a strategy failed. Do not boost a seed, alter a deadline or top up an account after seeing the outcome. If initial rules produce unavoidable structural collapse, fix the identified systemic mechanism explicitly in v3, preserve the V2 control, and rerun the full comparison.

### 10.2 Audit dimensions in every comparative run

At each committed operation or through exhaustive audit observers, reconcile:

- All actual company, market, household, regional treasury and external account balances.
- Regional currency balance vs minted issuance, external net flow and any explicitly retired currency; all-account money conservation except authorized Mint issuance.
- Exact physical goods flow by extraction, recipes, production, transfers, consumption, trade/export and Mint silver reserve; no unrecorded goods creation, disappearance or duplication.
- Household fractional desired, affordable, purchased and unmet quantities (stock vs funds); changes to population, prosperity, income and enabled luxuries.
- Public/private production attempts, successful payroll, input/output ceilings, blocked batches and extraction.
- Player/NPC trade expenditures/revenues, material FIFO lots/basis released/held, wages, capital spending and realized operating measure.
- Reference Company Value by cash, goods, completed ships/sites and construction in progress, including starter grants, no double counts.
- Fleet cargo, voyages, per-ship policies, construction schedules, contract entitlements, Mint issuance batches, deadline/score and save/load continuity.

Output specific, reproducible findings: e.g. first essential-affordability failure day, first public payroll shortage, first source shortage, last successful local purchase, regional stock and money at days 30/90/180/365, traded Silver, minted amounts, public share spent, changes in Company Value, active ships, current vs initial population, and regional paid food fulfillment. Keep monetary and physical measures comparable; beware confusing **company profit** with **stock still held** or **new minted money**.

### 10.3 Named controlled counterexamples

Create focused minimal fixtures, with small hand-checkable amounts, that separate:

1. Household-rich but market-poor employer cannot pay wages until a real transfer funds it.
2. Market-rich but household-poor town has food on its shelf but cannot sell enough without actual household income.
3. Effective complementary commerce stabilizes purchasing power for a measured period despite no local money creation.
4. More Mills without sufficient Grain/Logs do not create unlimited Bread; overbuilding can lower company liquidity without improving welfare.
5. Silver delivery cannot issue twice, cannot mint while pending, cannot mint with insufficient metal or beyond authorized capacity.
6. Royal Mint pays an exactly reconciled supplier/treasury split; treasury use requires a real funded expense.
7. External buyer with zero/insufficient remaining budget cannot buy goods, even when the player has them.
8. A town with newly created coins but zero viable food inputs can still fail essential fulfillment.
9. Fractional purchase is affordable only up to its exact available budget; 0.75 physical goods and price do not become 0 or 1.
10. Trade-order splitting/reversals, FIFO partial slices and save/reload preserve exact economic results.
11. A blocked shipbuilding order does not create a ship or consume payment/stock improperly.
12. A second ship cannot execute against the first ship's dock/cargo/policy, and ownership is validated for player vs NPC.

If a fixture shows the economic model cannot support the intended kind of recovery, diagnose and adjust with one named explicit rule at a time, not a hidden bailout or results-specific fudge.

## 11. Performance, deterministic correctness and compatibility

### 11.1 Hard invariants and test requirements

The following need focused C# tests, targeted integration tests and repeated experiment assertions:

- **Atomicity:** overflow or illegal state at any stage rolls back earlier same-operation wages, consumption, issuing coins, silver transfers, ship actions, public contracts, construction completion, challenge updates and time/revision. Expected funding/stock shortages remain explained ordinary outcomes.
- **Cash:** every transfer is zero-sum between accounts; only an explicit Mint issuance increases global cash. Regional cash changes by exactly recorded minted and external net flows (plus any documented modeled boundary crossings); never by mere population growth.
- **Physical goods:** every mine/farm/logging batch is an authorized source; every transformation preserves the exact recipe accounting; every export/consumption/Mint conversion is an authorized physical sink or reserve. No stock is restored to make a challenge succeed.
- **Exact fractional math:** bounded quantities, deterministic proportion calculations, positive prices and appropriate nonnegative funds; rounding and carry documented; split orders identical to bulk along the same curve; no easy same-model reversal arbitrage. Use wide integer intermediates and checked public bounds.
- **Economically honest funds:** a market cannot pay more than it holds, households cannot spend unowned cash, Silver cannot be minted twice, company capital cannot be spent twice, and an external authority cannot fulfil more contracts than its actual remaining budget.
- **Ownership:** cargo and warehouse stores are separate, transfers require ownership and actual dock, private and public assets never alias, all ship IDs and facility IDs unique, selected-player-ship command cannot affect NPCs.
- **Construction:** one payment for each asset/project; Ascension 0/1 private site timing retained; no pending-site automatic output; new ship and public Mint activation have explicit verified schedules.
- **Accounting:** paid FIFO lots remain exact under fractional handling, physical moves and transformations; grants' paid basis stays zero; cost basis, Company Value, material gross margin, operating surplus, mint issuance and cash are separate measurements.
- **Determinism:** repeat, reverse-configuration, different culture, save/reload and reorder-independent defined phases must match exact state and report bytes; no unseeded RNG or presentation timer affects simulation.
- **Identity:** exact Retry Same World and separately confirmed Reroll; old v1/v2 world signatures unchanged; unlike rulesets never share an unqualified comparison group. Ended challenges freeze further economic mutation.
- **Docs/content:** Python and C# acceptance agree, generated reference is current, offline encyclopedia/website derive from canonical names, CI catches stale reports.

Include numeric-boundary tests, especially scaling `int` inventories into fixed-point quantities, converting price units, summing all accounts and reporting Company Value; **do not hide overflow with unchecked casts or silent clamps**. Failure cases should preserve exact pre-attempt snapshot fingerprints.

### 11.2 Performance

Measure baseline and extended simulation stepping plus snapshot/allocation/FIFO lot growth with representative four-town worlds and 1/2/3 owned ships. New detailed accounting should not create unbounded per-day histories inside the world. Keep bounded recent events in the host and bounded welfare observation windows; preserve the exact open FIFO lots that still represent real held goods. If introducing long-lived contractual history, retain cumulative counters + bounded relevant outstanding obligations rather than every old event forever. Report measured runtime and memory characteristics; do not invent a large-world scalability certification.

### 11.3 Validation and Unity evidence

Run applicable commands from `AGENTS.md`, including locked restore, Release solution build, **all** .NET tests, all legacy demo/report freshness checks, Python validation/tests, reference regeneration/check, strict MkDocs, Unity preparation/freshness, the new expanding-economy experiment/freshness, and any v3 deep sample separately. Keep CI job bounded.

For Unity 6000.6.6f1, where the Editor is available, use official Unity Agent CLI/UI skills, run **full Edit Mode and Play Mode** suites, then actual live scene controls and visual inspection at both target sizes and four text scales. Verify in practice:

- Full setup / starting charter / v3 seed / construction preview and industry eligibility.
- Actual fractional stock and household consumption while whole ship orders remain enforced.
- Constructing an eligible Mill, observing real batch/payroll/stock and using its output.
- Commissioning an additional ship, selecting it, configuring a route distinct from the starter ship, simultaneous voyages and blocked waits.
- A real external contract completion and Mint commission, Silver delivery and issued cash visible in the regional and Company Value panels.
- Save, leave, restart/reload and complete a real challenge; exact state remains stable after retry/reroll.
- Legacy Classic/v1/v2 scenarios and existing UI paths remain usable.

Build an actual **Windows x64 Mono development player** from accepted code and exercise at least one complete v3 outcome, a save/reload journey and a real economic issuance interaction when the toolchain supports it. Include the observed steps, exit/log markers and screenshot evidence. Do not infer runtime input correctness from a build-success marker or from synthetic mutation of the economic snapshot. If a specific test is only performed in the Editor or cannot be run, disclose it. **IL2CPP and non-Windows shipping targets remain unverified unless genuinely built and tested**. Do not remove unrelated packages or disable warnings/licensing checks just to make the log quiet.

## 12. Deliverables, phased work plan, and agent coordination

This is **one committed milestone** with internal gates, not an excuse to deliver only the first foundational subtask. Plan the architecture before broad parallel edits; prefer small concrete types, shared authoritative accounting and pragmatic refactoring. If working with subagents, delegate to well-defined files/contracts with a single owner for cross-cutting simulation changes. Do not let parallel agents invent different unit scales, money boundaries, run versions or save DTO formats.

Suggested execution order (adapt when code evidence requires):

**Gate A — Baseline and contracts**  
Check current HEAD/CI, archive known v2 report hashes, inspect required source and test boundaries; write `tasks/TASK-2.0-PROGRESS.md` with fixed-point unit contract, v3 identity/version/catalog strategy, ledger account boundaries, public Mint ownership, external contract funding, value reference tables, shipbuilding payment flow and save serialization shape. Document decisions before concurrent feature work. The authoritative spec remains this file.

**Gate B — V3 exact quantity and economic core**  
Introduce v3 mode, scaled quantities/money and complete fraction-aware pricing, consumption, FIFO and atomic reconciliation. Prove with hand-computable cases and wide math tests while keeping all historical demos byte-identical. No Unity duplication.

**Gate C — Productive specialization and monetary actors**  
Make existing facilities player-buildable somewhere and allow multiple owned instances; add Silver/source and unique Mint; establish bounded outside trade and exact currency issuance/distribution. Verify real production/circulation and hostile liquidity cases. Compare before/after against v2 controls.

**Gate D — Ships and value**  
Add bounded purchasable ships, actual shipwright materials/wages/payment, independent player-ship targeting/policies; add reference Company Value and honest financial summaries. Demonstrate an operating two-ship network with actual production and funding dependencies.

**Gate E — Complete persistence and runs**  
Implement versioned atomic save/load/continue for all supported modes, exact continuation tests including Mint/fleet/FIFO state, and at least three new v3 objectives with measured thresholds. Preserve all old results and terminal behavior.

**Gate F — Unity, experiments, playthrough and final verification**  
Build integrated UI; run normal/deep experiments and update deterministic reports; revise shared help and documents; run full headless and Unity test matrices; launch the intended development player and exercise a real v3 victory and save/resume. Audit the complete checklist and document remaining issues honestly.

Maintain a concise but useful progress log containing completed gates, decisions and reasons, focused test results, current regression counts, known failing examples, Unity Editor/player evidence, and outstanding Definition-of-Done items. After any context compaction, reread this entire spec and progress log. If a stage exposes a conflicting requirement, resolve by adhering to the player-facing economic principles and explicitly documenting a narrow, tested choice; do not simply omit the user-requested integrated capability. Use subagents where they help, but integrate against one shared simulation contract.

Required committed/reviewable artifacts, as applicable:

- Source in simulation, host and Unity; typed tests and deterministic sample/experiment runner(s).
- Canonical/v3 content and schema/validator updates, authored shared player help, generated reference and encyclopedia, Unity staged assets prepared from source.
- A new `docs/EXPANDING_ECONOMY.md` covering all v3 mechanics, money/capital examples, issued-vs-imported currency, Silver and Mint operation, route/fleet behavior, proportional needs, valuation and save compatibility.
- New `docs/experiments/expanding-economy-results.json` and reproducible freshness command; opt-in deep evidence outside normal CI.
- Updated `docs/ARCHITECTURE.md`, `docs/MECHANICS.md`, `docs/GAME_DESIGN.md`, `docs/ECONOMY.md`, `docs/RUNS.md`, `docs/ROADMAP.md`, `docs/DECISIONS.md`, `docs/PLAYTEST.md`, `docs/UNITY.md` and `AGENTS.md` only as actually justified by behavior. Keep former milestones' evidence historically accurate.
- `tasks/TASK-2.0-PROGRESS.md` recording design decisions, implementation stages, final test/report evidence and explicit limitations.
- An end-user functional walkthrough, at least one new real world played through to a result, and actual screenshots/verification where accessible. Do not imply publication: the Pages publishing workflow is **manual-only** and must not be triggered or changed without explicit authorization.

## 13. Definition of Done — audit every line before claiming completion

### Versioning and exact economy

- [ ] V3 is selectable, identifiable, deterministic; Classic, seeded-v1 and Living Cities v2 historical rules and reports remain unchanged.
- [ ] The same seed has exactly the old v1 geographic positions/routes while v3 economic roles and currency rules are versioned, reproducible and auditable.
- [ ] V3 monetary and goods subunit contracts are exact and documented; no authoritative binary floating point, price drift or quantity leakage.
- [ ] Population 10/15/20/21/30 produces proportional scheduled demand, actual paid fractional consumption, correct stock/funds shortfalls and stable welfare scoring.
- [ ] Bulk, split and reversed transactions use exact shared pricing; zero/free rounding exploits, overflow and preview mismatches are tested.
- [ ] FIFO historical lots and cost aggregates remain correct for partial fractional sales, production, transfers and zero-basis grants.

### Production and monetary development

- [ ] All six preexisting production building types have real player construction blueprints somewhere on every v3 seed; ineligible locations remain authoritative rejections.
- [ ] Multiple independently scheduled owned facilities of one kind can coexist where eligible; real payroll and stock constraints remain.
- [ ] A genuine Mill expansion enables more potential Flour/Bread output and tests reveal remaining upstream/logistics constraints; no magically replenished food.
- [ ] Silver is a canonical physical good; an actual Silver Mine provides real wages/stock and a valid transport path to the sole eligible Mint town.
- [ ] The Royal Mint can be commissioned, has real timing/public ownership and bounded Silver-backed issuance, and never appears as a private player asset.
- [ ] Every minted coin reaches an identified account exactly once, and each surrendered Silver quantity is tracked in a reserve or explicit sink.
- [ ] Public treasury payments are funded transactions; early external contracts have finite payer budgets and actual delivered goods. External cash is not mislabeled issuance.
- [ ] Every commit reconciles global/regional money, all owners, goods, wage records, contract flows and Mint; rejected/exceptional actions preserve state.
- [ ] Productive multi-day recovery is demonstrated without automatic Bread-money creation, furniture-money destruction, population stipends or hidden bailouts; bad strategies may fail.

### Companies, shipping and persistence

- [ ] The player can commission two additional ships with real cost, materials, delivery and ownership; no phantom cargo or free ships.
- [ ] Manual and automated commands operate on the chosen owned `shipId`; simultaneously running policies preserve bounded stages, stock, funds, travel and priority order.
- [ ] Company Value includes cash, **all** player-owned goods, ships, facilities and paid pending capital exactly once, using declared stable reference prices—not fabricated liquidation proceeds.
- [ ] Starter gifts have positive reference value where appropriate but zero historical paid cost; all financial measures are correctly distinguished in UI and results.
- [ ] Local save/load/continue preserves authoritative state for all supported modes; mismatched/corrupt versions fail safely without replacing the current run.
- [ ] Save/reload yields exact continuation equivalence in the presence of fractional goods, multiple ships, pending construction, Mint, external contracts, household observations and FIFO lots.

### Game, evidence and integration

- [ ] At least three distinct v3 challenges are supported end to end, with tested real wins and honest losses/idle failures; cash and Company Value requirements have intentional, documented meanings.
- [ ] A player can see what blocked food, production, employment, trade, shipbuilding, Mint and external procurement without inspecting source or duplicate UI calculations.
- [ ] Default main gameplay does not depend on permanent giant scrolling; primary actions are readable at 1366×768, and all four scales/both target resolutions receive actual Editor visual review where possible.
- [ ] Full headless test suites, legacy report freshness, new normal experiment/freshness, validation, encyclopedia/doc generation, strict MkDocs and Unity preparation/freshness pass.
- [ ] Broad v3 seed tests and bounded deeper policy comparisons identify real circulating-money/production outcomes, not merely account invariants; report contains numeric evidence and counterexamples.
- [ ] Full Unity Edit/Play suites and actual in-Editor economic interactions are run where supported; real Windows Mono player build and player interaction are distinguished from Editor/headless evidence.
- [ ] End-to-end v3 example includes new production, multi-ship commerce, monetary activity, Company Value and save/reload before a terminal result.
- [ ] Documentation and progress log distinguish **implemented**, **verified in tests**, **observed in Unity**, **measured but provisional economics**, and **unverified platforms/features**.
- [ ] GitHub Pages is not published or reconfigured without the user's authorization.

## 14. Explicit exclusions and decisions deliberately left for measured tuning

**Do not expand into:** individual household agents, personal jobs or health simulations; loans, banking, debt, credit creation, interest or speculative financial markets; Gold and Iron chains; piracy, combat, politics, city takeover, democracy/tax systems; a hidden global income stipend; unconstrained minting or external buyers; a general route-language optimizer; unlimited fleet management; cloud saves/accounts/multiplayer; permanent meta unlocks; broad new Ascension tiers/curses; arbitrary random disasters; or a wholesale Unity visual restyling unrelated to readability and the approved new panels.

**The following parameters are intentionally provisional.** Choose them transparently, document them as design hypotheses, benchmark them and avoid tuning them secretly per seed: v3 industry eligibility details and site capacity, new buildings' construction/wage/production intervals and stock ceilings; exact fixed-point scales where better than the candidates; Silver mine rate and starting geology; Mint activation cost/delay, Silver-to-currency conversion, supplier/treasury shares and batch frequency; finite external contract schedule/budget/price; shipwright price/materials/lead time and ship reference values; reference commodity prices; v3 starting accounts; new challenge thresholds/deadlines; and any additional bounded public procurement to let treasury funds circulate. Decisions must satisfy the contracts above, and be backed by small numeric fixtures and comparable seeded runs. Do not confuse the freedom to calibrate numbers with permission to omit or fundamentally change the systems requested.

An explicit economic deadlock is permissible and potentially interesting; a hard structural ceiling that the player cannot address through any legal production, commerce, investment or monetary action should be investigated. Do not produce a perfectly healthy idle world by secretly subsidizing it, and do not make deliberate failure impossible.

**Final response to the project owner:** provide a concise change inventory; versions/compatibility decisions; the exact reference units and issuance boundary; links/paths to task progress, new report, docs and playtest evidence; tests and commands actually executed with counts; real observed economic outcomes; any failing cases and all unverified Unity/player/platform points. **Do not mark this milestone complete solely because Codex finished writing code or because CI is green.**
