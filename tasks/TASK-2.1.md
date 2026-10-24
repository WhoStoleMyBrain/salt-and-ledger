# TASK 2.1 — The Grand Commission: Map-First Objective Draft & Journal

**Project:** Salt & Ledger — Unity 6 maritime economic roguelite  
**Milestone:** 2.1, intentionally narrower than Task 2.0  
**Baseline reviewed:** `main` at `a99c59b01c59372b6a3242c68ec59f33bae342f1` (October 23, 2026). Recheck HEAD before editing.  
**Primary goal:** A **complete, playable, scored** v3 roguelite challenge in which the player inspects a revealed generated world, drafts **one of three objectives in each of four rounds**, commits to **all four** objectives, and follows exact progress in a dedicated Objectives/Journal screen until victory or defeat.

This is **not** a task to implement all the recently designed roguelite systems. Make the objective draft and its gameplay consequences genuinely finished, testable and enjoyable to navigate. Keep the work contained enough for a focused Codex implementation pass rather than another 4-hour economy/Unity overhaul.

## 0. Product vision and accepted design decisions

> **The world determines the circumstances. The player chooses how to respond. Success depends on what they do with those choices.**

Salt & Ledger is a deterministic economic strategy game about actual finite assets, specialized towns, ships, production, real payroll, and circulating/explicitly issued money. Players should **read the archipelago first** and then commit to economic choices. Randomness presents a **decision space**; it should not invent hidden constraints after the player has committed.

We have agreed on the eventual core run sequence: reveal the entire world; draft ascending curses; draft one female captain; draft a separate starting charter; draft four mandatory objectives; draft a modest blessing; begin. A future sandbox can continue a scored run in unscored form. **Only the objective-drafting slice of this future flow belongs in Task 2.1.** Existing selectable charters and Ascension 0/1 remain as they are. Do not add placeholder captain/curse/blessing buttons.

Important invariant: **choosing a mission may not change the world generated for a seed.** Mission, charter, and future ability selection must not silently permute towns, alter geography, repair liquidity, change production eligibility, or create stock. The economic challenge has to be solved by ordinary player actions.

## 1. Existing implementation and boundaries (read first)

- `AGENTS.md` and `docs/ARCHITECTURE.md`, `docs/MECHANICS.md`, `docs/EXPANDING_ECONOMY.md`, `docs/EXPANDING_ECONOMY_EXPERIMENTS.md`, `docs/RUNS.md`, `docs/PLAYTEST.md`, `docs/UNITY.md`.
- `RunIdentity.cs`, `RunScenario.cs`, `Challenge.cs`, `ExpandingChallenges.cs`, `RegionalWorldState.cs`, `RegionalWorldSnapshot.cs`, regional accounting and operation events, save capture/restore/codec.
- Unity UI Toolkit: `ArchipelagoRunSetup.cs`, `ArchipelagoRunExperience.cs`, `ArchipelagoLabController.cs`, existing UI helpers, `ArchipelagoMap.cs` and existing ObjectiveText/results/ledger code.
- Canonical content and generated player help; do not duplicate authoritative recipes.

The existing v3 economy already supports the seven private industries (including the Silver Mine), player fleets up to three ships, exact subunit quantities and currency, regional roles, monetary operations, Company Value, and exact saves. V3 has three **hand-authored, non-drafted** challenge kinds plus sandbox. `ExpandingChallenges.Progress` currently produces component percentages, not a generic mission journal. Preserve those behaviors and previously published experiment bytes.

Technical guardrails:

1. The portable simulation stays `netstandard2.1` / C# 8 and independent of Unity and filesystem policy.
2. One authoritative atomic `RegionalWorldState` is the source of truth. No parallel host-only mission progress and no approximate saved replay.
3. V3 = `RunWorldMode.ExpandingEconomy`, generator/content version 3, valuation version 1. **No new economic generator version merely for missions**; add a separately versioned mission draft definition to the run identity instead.
4. Existing Classic/v1/v2/v3 non-drafted modes, deterministic generation, run identities, scores, records, and saves must remain correctly interpretable.
5. No economic retuning, novel currencies, new resource/building types, or new long-horizon artificial subsidy is authorized in this milestone.

## 2. Player-facing definition of done: one full journey

A player can:

1. Select Expanding Economy v3 and **Grand Commission** in run setup; choose existing starting charter, seed, pricing hypothesis and Ascension level.
2. See the generated archipelago **before** drafting: a readable spatial map/positions and actual sea connections, plus the existing truthful descriptions of each town's role, starting industries, permitted buildings and relevant starting conditions. All four towns are revealed, not a fog-of-war roll; this is the exact world the run will use.
3. Enter a distinct four-round drafting screen. Each round offers **three actual distinct missions** suited to a declared category; select exactly one. The offers are deterministic for the world seed and mission-draft version, and remain stable if the player navigates backward. Nothing advances simulation time before starting.
4. Inspect a pre-run summary with all four selected goals, the exact deadline, starting charter, difficulty, and world. Change a choice before final commitment or cancel without changing the seed or economic world.
5. Start with the exact chosen run identity. All four drafted goals must be satisfied together, plus minimum liquidity, before the published deadline. Victory/failure/abandonment use the existing terminal/freeze framework.
6. At any time open a dedicated **Objectives / Journal** screen with one clear line/card per mandatory goal; see `current / target`, capped progress bar, completion/checkmark, exact status reason and deadline/day remaining. Cumulative irrevocable accomplishments are crossed off; conditions which can later become false are shown as **currently met** without falsely permanent strikethrough.
7. Return to the map with only a **compact objective summary** in the main HUD (e.g. `Objectives 2/4 · 72 days left`, or equivalent). The full checklist must not live permanently as one dense top-bar paragraph.
8. Save mid-voyage, mid-construction, or mid-objective; reload exactly without earning duplicate credit. Retry same world reproduces original offers and chosen four goals; reroll explicitly changes seed and the new offer set. Results show each selected objective and exact final progress.

A non-drafted Classic, v1, v2 or v3 run continues using its current setup and outcomes. Sandbox remains unlimited. Do **not** replace the existing three v3 challenges or migrate their reports to new mission semantics.

## 3. Draft generation: deterministic, authored and bounded

Introduce a `Grand Commission` challenge option in v3 only. A draft is a **frozen choice over a generated world**, not a second world generator.

- Exactly **four rounds**, **one selection per round**, **three visible offers per round**.
- Use four category pools, one pool per round, each with at least **four** actually supported authored candidates. Pick three **distinct** candidates from that category deterministically using a **separate, stable salted RNG stream** derived from `(signed world seed, mission draft version, round ID)`. Do not consume the geography/v2-role RNG; avoid unseeded runtime entropy, strategy-dependent rerolls, or rejection loops to find a winning world.
- The same seed and mission-draft version produce the same ordered offers, regardless of charter, ascension, screen visits, culture, pricing view, or the previous selected cards. Map/port role parameterization **must** use the exact generated v3 role IDs. The same seed with different selected goals yields precisely the same initial geographic/economic world apart from its challenge/identity state.
- Stable machine IDs for all templates; player-facing prose may evolve but cannot silently change completed-run semantics. Declare a **mission-draft version 1** and reject incompatible/future versions explicitly. Avoid relying on UI labels or collection indices as authority.
- Keep a selected mission resolved to explicit typed data: template ID, goal ID, commodity/port/role parameters, exact target and evaluation rule. Seeded parameters are computed from authoritative v3 world configuration, not from arbitrary displayed names.
- All three visible offers must be locally legal/structurally achievable. You do **not** need to prove economic victory for every combination, nor secretly increase funds/deadlines or reorder towns based on the offered goals. If a candidate is not legal for every v3 role permutation, author an alternative with a guaranteed valid role-relative destination rather than rolling a different world.
- Do not generate meaningless three-of-three options that are always the identical slate. The minimum four-candidate-per-category pool gives seed-dependent variation; log representative golden offer vectors across several seeds, including negative seeds and int32 limits.
- Keep the draft definition **small and strongly typed** (templates, resolved conditions, progress). **No arbitrary scripting language, general quest editor, runtime reflection DSL, plugin framework or remote content service.**

### Four categories and concrete pilot candidate pool

The following are **design targets, not immutable balance thresholds**. Implement at least four real templates in each round. You may adjust modest numerical thresholds for measured feasibility, but document authored changes and never tune one problematic seed secretly. Prefer composing a small collection of reusable condition evaluators instead of sixteen hardcoded per-template engines.

**Round 1 — Navigation / Fleet** (physical ship movement matters):

1. **All Harbors:** Visit all four towns with at least one player-owned ship (the charter's initially docked port counts as visited).
2. **Second Command:** Own at least two completed player ships and have the second acquired ship visit two distinct towns through actual docking/arrival (shipyard's initial dock may count once; a ship that never leaves the shipyard cannot satisfy this).
3. **Regional Survey:** Visit the generated recovery, processing and furniture towns (distinct roles and therefore distinct actual towns).
4. **Open the Routes:** Visit at least three distinct towns and complete a real net-positive sale at a town other than the initial port.

**Round 2 — Production / Industry**:

1. **A New Industry:** Own one **operational** private Mill and one **operational** private Bakery in **different towns**; achieve at least one actual funded completed batch in each corresponding selected facility. The generator guarantees eligible distinct towns. Show the two building/production requirements distinctly in the journal. (A future captain's exceptional permit could offer another path for other industry missions, but no captain system yet.)
2. **Timber Chain:** Own an operational private Lumber Camp and Sawmill and produce a documented positive threshold of private Planks.
3. **Artisan Guild:** Own an operational private Sawmill and Furniture Workshop and produce a documented positive quantity of private Furniture.
4. **Breadworks:** Own two operational private Bakeries in different towns and privately produce a documented quantity of Bread.

**Round 3 — Markets / Commerce**:

1. **Bread for the Coast:** Sell a **net** quantity of Bread to the actual generated recovery town.
2. **Planks for the Forest:** Sell a **net** quantity of Planks to the actual generated furniture town.
3. **Flour for the Bakers:** Sell a **net** quantity of Flour to the generated food-source town.
4. **Royal Exporter:** Earn a documented amount from physically fulfilled **finite external export contracts**, not imaginary sale value or local resale.

Net sales are **player-company completed sales at the exact target town minus that company's purchases of the same good from that town**. Buybacks can reduce progress; buying and reselling the same local stock must never manufacture credit. Include both ship and legitimate private warehouse market trades, if both existing paths can execute them. Neither NPC trades nor hypothetical quotes award player credit.

**Round 4 — Civic / Capital Development**:

1. **Royal Institution:** Public Royal Mint commissioned by this company **and** at least one completed physical Silver issuance (not merely delivering/selling Silver).
2. **A City Back in Trade:** Six recent actual paid-food opportunities at a defined fulfillment threshold and a specified minimum prosperity in the generated recovery town. This is a **rolling/current-condition** requirement, not an irreversible achievement.
3. **Prosperous Enterprise:** Company Value growth reaches a defined threshold **and** liquid cash meets a defined threshold simultaneously. This is a current-condition requirement, not a completed one-off grant.
4. **Public Works:** Deliver a funded positive quantity of Planks to the royal public-works buyer plus a modest sustained paid-food condition. Count actual treasury-funded procurement, not an imaginary contract completion.

All four picked cards become compulsory. There is **no alternative between cards after drafting**. Individual cards may have transparent conjunctive subconditions; the UI must show them. Use **day 180** and **at least 100 displayed company coins** as the initial common Grand Commission victory requirements; if feasibility evidence supports a better single baseline, document the change and test it without per-seed adjustments. Ensure the minimum-cash final requirement is visible throughout gameplay.

## 4. Typed objective state, correctness and scoring

Build a small reusable engine for **goal progress**, not a parallel economic simulation:

- Each goal has stable ID, short title, explanation, target, current exact raw value, display-unit formatter, percent (0..100), status and a **progress semantic**.
- Separate **irrevocable cumulative** conditions (actual private production ever completed, visit history, actual Mint issuance) from **reversible** ones (current ownership/operational availability if it can change; company cash/value; recent food coverage/prosperity; signed net delivery which can decline after buybacks).
- If a card contains subconditions, its displayed completion is the conjunction and its percentage should explain the remaining blocker (e.g. limiting/minimum subcondition, not a misleading average that reports complete at 99%). Cumulative subconditions can be checked off independently.
- Distinguish `4/4 goals currently satisfied` from `2 permanently finished`. Never cross off net-trade credit as irreversible if a buyback can undo it. Show progress drops when they genuinely occur.
- Derive production quantities, real funded payroll, Mint/public results, facility operational states, Company Value and cash from existing authoritative snapshots/accounting wherever possible. Avoid redundant counters if a trusted aggregate already exists.
- **New historical state is needed** for ship-port visit sets and any per-good/per-target signed trade history not already retained. Store it as bounded authoritative run-specific state and update it only from actually committed vessel/market operations. A newly commissioned ship's starting dock counts as an initially docked port but must not count as a completed inter-town voyage. Track visits per ship if the mission distinguishes ships; always validate ownership. Use bounded fixed port/ship IDs rather than unbounded history.
- Make all state transitions atomic with the existing day, voyage arrival, construction, manual/automated trade and challenge phases; failed or rolled-back operations must not advance mission progress. No Unity-only events grant economic achievements.
- `GrandCommission` should be a distinct authored run variant / challenge with a resolved plan. Do not alter the meaning of `ExpandingChallenges.Progress()` for existing `SustainableProvisioning`, `IndustrialNetwork`, or `RoyalCommission` runs; reuse applicable computations or generalize behind a compatibility-preserving adapter.
- Evaluate victory after authoritative committed operations as current challenges do. **ALL four goals and 100 displayed coins** must hold before or on day 180; otherwise defeat at deadline. Existing terminal freeze, explicit abandonment, exact day-boundary victory precedence and sandbox behavior remain.
- For the new drafted challenge, score objective completion as a bounded **0–500 partial-progress component across the four selected cards** (e.g. capped mean; expose the exact documented formula). Award the existing victory/time/solvency components separately. Even if several tasks are complete, partial score cannot grant victory. Keep old scoring formulas and historical comparison groups unchanged.

## 5. Identity, reroll, save/load and trusted restoration

The drafted mission plan is **part of the authoritative run identity**, not a host preference. The identity's comparison-group string must distinguish different objective selections even when seed, charter, ascension and pricing match. Record the mission-draft version, exact chosen goal/template IDs and resolved parameters (or a stable canonical digest with reconstructible selected data). Show/copy the meaningful run identity to the player without flooding ordinary UI.

- Preserve the current v3 world/economy generator version. Separate random streams for mission offers and future player-choice systems.
- `Retry Same World` must start the same world **and the same previously selected commission**; only explicit reroll or player edits before commitment alter it. Existing classic/v1/v2/v3 non-drafted retry semantics are unchanged.
- Save the complete resolved draft and bounded historical objective state (including visit sets and target-specific signed balances); load validates the plan against the versioned known offer catalogue and rechecks historical claims/fingerprints wherever possible. There must be no silent migration into another set of objectives, no forged arbitrary goal counters, and no duplicate credit caused by loading.
- A saved terminal victory remains completed and frozen. A saved active run remains active with the exact previous step/deadline. Verify uninterrupted-versus-save/reload continuations over a genuine inter-port voyage and a transaction with a net-delivery objective.
- The current world-save schema is versioned. **Do not reject previously valid schema-v1 saves of existing runs.** If the drafted state requires a new schema version, read old saves through the old path and use the new schema only where justified; explicitly reject unsupported future schema and mismatched content/rules. Never drop old fields or simply ignore corrupted new ones.
- The pre-game unfinished draft does not need persistent save slots. A back/cancel button may discard uncommitted selection. After starting, the committed draft is durable.
- No new cloud save, meta-progression, achievement database, player profile or network service.

## 6. Unity UI: a purposeful slice, not another dense dashboard

### Setup and map-first reveal

Reuse existing setup logic for `ExpandingEconomyGeneration`, preview geography, initial roles/conditions and charter selection. Introduce the `Grand Commission` path **without replacing** existing v3 scored challenges or v3 sandbox.

The current setup lists coordinates/routes and city capabilities. Add a **compact visual geographical preview** derived from the same source coordinates/distances, so a player can genuinely see whether two key industries are far apart before drafting. Reuse existing map widgets/logic if practical; do not build a second simulated map. Retain accessible textual route details. All generated towns/capabilities are public before picking an objective. Respect Compact, Default, Large, Extra Large and 1366x768 / 1920x1080.

The draft screen should show the current round/category, three legible cards with tangible conditions and targets, current selections (small summary), Back/Cancel/Next, and a final `Review & Begin` stage. A selection must visibly highlight which of the three is selected. Backtracking may edit earlier picks while preserving deterministic offers. Make it impossible to start with missing slots or mismatched seed/plan.

### Dedicated Objectives / Journal view

Add `Objectives` as a real secondary screen accessible while the game is active, paused, and inspectable on terminal results. Render each selected goal with:

- Exact named target(s) / relevant town and good, actual current/target numbers **in display units** (not raw 10,000-subunit integers).
- A visually clear progress bar (0–100), checked or crossed-off cumulative requirements, and separately labeled currently met/not met requirements.
- Explicit blocker text when a prerequisite is missing, a market sale was reversed, a facility is not operational, or a rolling welfare target has fallen.
- Days elapsed / remaining; show `Day 180` deadline and final minimum cash. Negative remaining days must not appear after terminal defeat.
- A compact `n/4 goals currently met · D days left` element in gameplay HUD (and a keyboard-accessible route to the full journal). Remove the existing excessive v3 challenge paragraph from the permanently visible top area for drafted runs. Don't make the gameplay/map view depend on repeatedly scrolling the entire page.

Use sensible progressive disclosure: long explanation can scroll inside Journal/details, but the overview and navigation should not require scrolling, nor smaller fonts than the current readability baseline. Other legacy run modes continue to display coherent objective summaries. Do not undertake a general overhaul of trade, industry, fleet, ledger or encyclopedia screens in this milestone.

### Results

Show Grand Commission victory/defeat with its four actual resolved goals, progress or completion state at terminal day, score components, and seed/charter/difficulty/mission draft identity. Existing result buttons (retry, setup, reroll, menu, save results) remain. Do not implement **Continue in Sandbox** in Task 2.1; that is a separate terminal-state transition and scoring-contract decision.

## 7. Verification — enough to substantiate a real feature, not a 2.0-size sweep

Deliver targeted high-signal tests, then full existing suites if environment allows. This project already has extensive Task 2.0 deterministic reports; do **not** create another 400,000-day sweep or recalculate all huge experiment artifacts unless a detected economic regression demands it.

### Simulation / identity / save tests

1. Fixed-seed golden candidate slates for 42, −42, 99 and both int32 extremes; same offers under different cultures, charter, pricing and ascension, with explicit draft version.
2. Three distinct offers per round; at least four possible authored templates in each category; correct choices and no duplicate/invalid/tampered selections.
3. Same v3 seed produces exactly the same map, economic roles and starting accounts across differing drafted objectives. Drafting/previewing never advances the world.
4. Structural feasibility in a bounded sample of deterministic world seeds: roles/industries/ports needed by each offered template exist, accessible through legal real actions, regardless of random role permutations.
5. Cumulative production only from real private funded batches; role-relative ownership/location checks, including **A New Industry** in two distinct towns with a batch in each.
6. Exact target-market net supply: purchase and resale cancel; any later buyback can make the goal unfulfilled; unsold cargo, quoted previews, other ports and NPC deliveries do not count.
7. Correct individual-ship visitation after actual voyage arrivals, initial dock, commissioned ship and save/reload; no remote ghost visits or credited navigation on rejected departure.
8. Welfare and liquidity criteria can go from satisfied to unsatisfied; permanent production or visit conditions stay completed. Component bars/counters are clamped but real underlying signed credits are preserved.
9. Two or more **distinct** actual Grand Commission victories on representative fixed seeds/charters (not direct state edits); at least one deliberate failure/idle control. Report commands, paid inputs and success day. This is feasibility evidence, not a universal winning guarantee.
10. Restore and Retry reproduce complete identity and mission state; save before arrival and after nonzero progress; tampering/version mismatch is rejected without replacing the live world. Existing v3/legacy save fixtures remain valid.
11. All terminal states reject economic mutation, with exact-day success before deadline failure; abandoning records true partial progress without accidentally awarding victory.
12. Historical v3 challenge/sandbox and Classic/v1/v2 paths retain their old definitions and behavior. Do not modify old deterministic report contents as a side effect of the new option.

### Unity and documentation tests

- Edit/Play Mode cases for map preview, four-round selection, backtracking, disabled Begin on incomplete draft, main-HUD summary, Journal detail, time remaining, pause/resume, terminal results and exact save/Continue flow.
- If an actual Unity Editor/Windows Mono player is available, complete **one end-to-end Grand Commission** with real UI input and one save/reload, including visible objective checkmarks and a true result. Distinguish automated tests, real Editor interaction and standalone hardware input accurately; **do not claim observed UI if not actually run**.
- At minimum review Default at 1366×768 and Large at 1920×1080 for the map preview, draft screen, Journal and compact HUD. Avoid an exhaustive 76-screenshot matrix for this focused milestone.
- Preserve canonical C# content, generated help and MkDocs freshness. Add `docs/GRAND_COMMISSION.md`, update `docs/RUNS.md`, `docs/ROADMAP.md`, `docs/PLAYTEST.md`, `docs/UNITY.md`, appropriate authored player help, and `AGENTS.md` only where changed contracts require it.
- Prefer a compact golden offer test and a short reproducible headless witness, not a new 25MB report. `dotnet test`, Python tests, build, Unity staging and strict docs build must pass where toolchain is available. Re-run historical golden report checks or verify hashes when applicable. Preserve old report bytes. Never claim unavailable checks were run.
- Do not publish GitHub Pages or alter publishing workflows.

## 8. Recommended implementation order and Codex workflow

1. **Inspect:** Read this complete task and `AGENTS.md`, assess actual repo HEAD and nearby systems. Briefly record touched contracts and identify the minimal extension points for `RunIdentity`, challenge evaluation, operation-level historical events, save codec and UI.
2. **Agree contracts in code:** Immutable mission templates / resolved plans; seed and versioning; typed objective metrics; progress semantics; limited historical state. Establish these before parallel edits to Unity and save handling.
3. **Simulation vertical slice:** Deterministic offer generator → resolved challenge → real counters/conditions → victory/scoring → save/load/compatibility. Write targeted tests immediately, including A New Industry, net market supply and port visits.
4. **Unity vertical slice:** Existing generated-world setup → visual map preview → 4-round card draft → review/begin → compact HUD → Journal → terminal results, without redesigning unrelated economic views.
5. **Representative play and documentation:** At least two real headless winning scripts, tests, available Unity UI checks, truthful instructions, remaining UX limitations and CI-equivalent validations.

Use a short `tasks/TASK-2.1-PROGRESS.md` with checkpoints and incomplete items. Subagents are optional: delegate clearly separated code/test or UI/docs work **after** shared immutable contracts are fixed. Don't spend an enormous fraction of the task on huge optional sweeps, exhaustive screenshots, agent ceremony or broad unrelated refactors.

When context gets compacted, reread the task and progress log. End with a **criterion-by-criterion acceptance table**, links/paths, checks actually executed, reproducible sample playthrough results, known limitations and all unverified behavior. Keep commits reviewable. Don't silently change fixed historical v3 balance or reports to make the new objective win.

## 9. Explicit exclusions — preserve scope

**Do not implement in Task 2.1:**

- Female captains, special captain rules, a captain roster or a character-and-charter combination system.
- Random starting-charter drafts, minor/major/greater curse tiers, blessings, additional Ascension levels, curse stacking, persistent character unlocks or permanent progression.
- Runtime mission generation after day 0, branching optional objectives, a full quest scripting engine or an unlimited custom challenge editor.
- Post-victory sandbox continuation, unlimited fleets, new production recipes/goods, banks/debt, town acquisition, combat, piracy, politics or extra cities.
- Retuning monetary issuance, long-term population/price curves, NPC income concentration or full multi-year sustainability. The Task 2.0 long-run circulation issue remains visible but is **not** this milestone's mandate.
- A wholesale Unity dashboard redesign. Confine the new navigation to setup/draft/Journal/HUD/result integration.

Later design steps can add **female captains with rule-changing abilities**, **independent meaningful charter drafting**, **tiered mandatory curse drafts**, **one blessing draft**, and **scored-result → optional unscored sandbox continuation**. The code added here should not forbid those layers, but it must not pretend to implement them.

## 10. Definition of Done — verify all items

- [ ] The v3 Grand Commission is the primary/default new scored v3 path without removing the three old authored challenges or sandbox. Full starting archipelago and important town capabilities are revealed **before** the objective draft; preview agrees with real run.
- [ ] Exactly four seeded, category-constrained rounds offer three distinct valid choices each, with a genuine pool of at least four per round; choosing/backtracking does not advance the world or change offers.
- [ ] The chosen four objectives are frozen into an explicit, versioned, replayable identity that also distinguishes score comparison groups.
- [ ] The four categories implement actual varied goals, including **A New Industry**, real ship visits, source/target signed net sales, and public economic or welfare objectives.
- [ ] All four selected goals **and** minimum company cash are required by the common deadline; success, failure and abandonment use real terminal rules.
- [ ] Correct permanent-versus-current progress semantics and honest numerical counters, checkmarks, progress bars and blocker text.
- [ ] New needed history (visits and target sales) is bounded, authoritative and updates only on actual committed actions, with full rollback.
- [ ] Dedicated accessible Objectives/Journal view, compact gameplay objective summary and comprehensible end results.
- [ ] Existing v3 challenges, historical modes, economic rules, reports, and valid existing saves are preserved.
- [ ] New drafted run survives exact save/load/Continue and Retry; corrupt/tampered drafts fail atomically without changing the active session.
- [ ] Tests show deterministic offers, wrong-world rejection, net reversal, no fake visits, production correctness, rolling goals and terminal freeze.
- [ ] At least two differentiated real-command Grand Commission victories and an idle/failure control are reproduced without grants or direct state edits.
- [ ] Full headless/Python/docs and relevant Unity checks are run where available; actual Editor/player limitations are honestly reported.
- [ ] New mechanics/flow are documented, a practical manual playtest route is recorded, and no GitHub Pages publication is performed.

**Acceptance standard:** The feature is finished only when players can genuinely draft, track, save, complete and score their own four-objective economic run. A random-looking card picker with host-only counters does not satisfy this task.
