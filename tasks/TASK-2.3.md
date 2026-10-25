# Salt & Ledger — Task 2.3: Ordered Multi-stop Cargo Itineraries

**Status:** proposed sequential Codex milestone; **depends on Task 2.2 being merged and verified**. Read the latest `AGENTS.md`, its real merged source/tests, `docs/AUTOMATION.md`, `docs/EXPANDING_ECONOMY.md`, `docs/GRAND_COMMISSION.md`, and `docs/ARCHITECTURE.md`. Do not assume Task 2.2 landed merely because this prompt exists.

## Mission

Replace the fragmented player experience of 'market route' versus 'industry shipping route' with **one explicitly configured, deterministic, bounded itinerary** for new v3 pooled-warehouse runs. A single owned ship must be able to execute **several ordered buy/sell/load/unload instructions at each of multiple ports**, carry **different goods simultaneously**, loop, honor real finances, and explain blockages. The player designs the trade; automation executes their instructions. This is not a strategy optimizer or an NPC AI redesign.

**Core acceptance itinerary** (on an ordinary viable seeded v3 world):

- **Port A (player Bakery):** Unload arriving Flour and Logs to the shared company town stockpile; load available finished Bread from that stockpile.
- **Port B (agricultural town):** Sell carried Bread if there is demand and market cash; buy Grain and Logs up to individually configured limits.
- **Port C (processing town):** Sell Grain; buy Flour; return to Port A with Flour and Logs intact. Repeat without manual cargo moves.

The Bakery automatically consumes exactly what is present at its town stockpile, pays its real wages when funded, and produces real Bread. There is no requirement that source goods magically arrive or that every order succeeds. An undersupplied route must behave predictably, not duplicate an order indefinitely or loop instantly.

## A. Typed itinerary contract — bounded and inspectable

1. An immutable policy per player-owned ship with **2–4 ordered stops** (one stop per unique port in a cycle initially, repeat the first at loop boundary) and **1–8 commands per stop**, max **24 total commands**. Select each stop by stable port ID. Validate actual routes and known goods. Do not accept empty or unbounded action sequences. For v3 new pooled-storage rules only; old `TradeRoutePolicy` and `IndustrialRoutePolicy` must continue operating in legacy worlds/saves without changed historical report bytes.
2. Supported commands: `BuyFromMarket`, `SellToMarket`, `LoadFromCompanyTownStock`, `UnloadToCompanyTownStock`. Each has explicit good, max **whole cargo units**, optional minimum actionable quantity, destination/source, money/price/cash reserve or warehouse reserve as applicable, and policy on shortage: **skip for this visit** or **wait at stop**. No partial physical teleport; transport requires actual arrival. Market actions transfer real cash at exact bulk price; own-stock transfers do not. Trade prices and previews share the existing engine, including stock-sensitive marginal curves.
3. Model state in the authoritative run snapshot: `itineraryId/version`, ordered stop index, action index, current cycle, next action, last committed decision/block reason and enabled flag, with state transitions that are deterministic across save/load. Do not use a background coroutine to mutate economics. No departure without finishing or intentionally skipping stop actions.
4. Ship hold can contain **mixed commodities** up to its actual finite capacity. At the next stop, actions apply only to the relevant good; leftover cargo remains aboard unless explicitly sold/unloaded. A full hold must block or skip buy/load according to policy with an informative reason. Never discard incompatible goods as an implicit route reset. Stop progression cannot duplicate a previously completed action when an intervening action waits.
5. Enforce the existing atomic commit/revision/stale-preview boundary. Keep a finite per-day limit such as 8 executable dockside operations. If a stop has more instructions, retain the exact next command for the next day instead of silently truncating. A route with all actions blocked must not spin; a wait remains a wait. Shipping uses real integer voyage time; no same-day teleports.
6. Meaningful edit contract: the player may draft/edit while paused without changing world state; Apply requires valid selected owned ship and an explicitly documented safe transition (e.g. docked, no in-progress partial stop; preserve cargo). Do not require an empty hold merely to view or adjust a draft, but never silently wipe an underway route or cargo. Old policy selection and new itinerary selection must be mutually exclusive and clearly labeled.
7. Existing NPC merchants can retain simple historical routes. They must still use the same finite cash and price engine; do not expand their AI as part of this milestone.

## B. Usable route planning UI

1. **Fleet → Route planner** with one visual card per stop and a list of ordered actions; affordances to add/remove/reorder stops/actions, choose town, commodity, market versus company stock, quantity, price threshold, reserve, and skip/wait. Offer a short **example preset** reproducing the A→B→C Grain–Flour–Bread loop after resolving actual role towns; preview its plan but don't auto-apply.
2. Show actual ship dock/voyage, next stop and action, current cargo, source stock, market budget, cash reserve, predicted trade total **at current quotes**, and reason if blocked. Live quotes are informational when remote and may change before arrival. Show readable concise messages: 'Waiting at Southhaven for 2 Flour in your warehouse', 'Market has only 7 coins; can afford 0 Planks', etc.
3. Provide a clearly named **manual control** toggle/pause for route actions. Manual orders while enabled must follow established permissions; turning off automation must not change the route/position or destroy cargo. Link from town/harbor UI to that ship's itinerary and vice versa.
4. Keep the 2.2 harbor's transaction UX consistent; don't rebuild the trading UI again. All quantity entry, accessibility, keyboard navigation and 1366×768 layout must be tested. Avoid a page-long form of dozens of fields for every stop; use compact stop summaries with per-action disclosure.

## C. Persistence, accounting and versioning

1. Store entire itinerary and its **exact current stop/action/cycle state** in new saves. Existing schema-1/2 and old pooled-storage saves must still load. Upgrade the relevant codec schema only for new policy-bearing identities, or use versioned optional fields with strict validation and unchanged old serialized bytes. New identity/comparison fingerprints include the policy format where rules change, but merely assigning a different valid route must not reroll the world.
2. Apply existing material FIFO lots through mixed cargo, independent action chains and shared company town stock. Confirm purchase basis releases exactly once on sale/production; Company Value counts owned stock once, even during transfers. Cash transfers reconcile exactly; NPC/player authority parity is preserved.
3. After saving mid-voyage **and mid-stop following two completed actions**, loading and continuing must execute the same next action as uninterrupted execution. Validate impossible stop index, mismatched port, negative quantities, missing referenced stockpile, invalid policy and corrupt state before mutating the active host session. Terminal saves remain frozen.
4. No runaway simulation in a malformed policy, no unbounded event log, no variable amount of random draws on trade evaluation, no silent money or good creation. Deterministic order among multiple player ships and the three existing NPCs remains stable.

## D. Acceptance and tests

- **Primary integration test:** configure A→B→C route for one ship, with actual Bakery, Mill/other source where appropriate. Demonstrate multiple market goods and multiple company-stock loads/unloads, real paid production, repeated two full loops, all physical goods accounted, and no manual loading after enabling the route.
- **Single-stop precision:** Buy Grain and Logs at the same town; hold limits, reserves and exact marginal prices apply separately; a skipped unavailable second good does not block planned departure.
- **Insufficient funds/stock:** both `skip` and `wait` produce documented, different, deterministic behavior; no unbounded loop, repeated cash debit or phantom transfer.
- **Mixed cargo:** carrying Bread, Flour and Logs together doesn't pause solely due to goods being different; overflow blocks appropriately; wrong destination cannot unload remotely.
- **Hardening:** three simultaneous player ships using independent policies, one blocked, others progressing; mid-stop save/reload exact comparison; exceptional rollback; corrupt save rejection; old 2-port route presets and all historical golden reports preserved.
- **Actual UI:** from the game's ordinary setup configure and watch a 3-stop loop without scripting economic mutations; verify visible stop/status, 1366×768 Default and 1920×1080 Large, keyboard/back/route edits, blocked action diagnosis, and Windows development player if available.
- **Scope gates:** pass targeted/full .NET tests, content/Python/MkDocs/Unity prep, appropriate Unity Edit/Play tests and player screenshots; document truthful verification and remaining limits. No multi-year deep sweep as part of this milestone; route policies can be measured in small deterministic 30–90-day runs.

**Definition of Done:** A player can describe a real 3-port, 3+ good loop, apply it through visible UI, see exact present/next work, and have one ship carry it through at least two successive real cycles **without clicking manual load/unload**. All amounts, money, FIFO costs, locations, time and save state remain authoritative and exact. Old runs/saves remain supported. Do not defer the A→B→C case to documentation or pretend an industrial two-port preset satisfies it.
