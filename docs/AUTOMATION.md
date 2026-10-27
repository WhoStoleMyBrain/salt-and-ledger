# Cargo itineraries and automation

Player routes use ordered cargo itineraries. NPC merchants retain their proven two-port market policies. Controlled economic experiments also retain industrial two-port policies; these are research controls, not alternate player editors.

## Ordered cargo itineraries

Task 2.3 adds immutable `CargoItineraryPolicy` version 1 for player ships in new v3 pooled-stock runs. It has **2–4 unique port stops**, **1–8 ordered commands per stop**, and **at most 24 commands total**. The last stop connects back to the first through a real sea route. Every port, connection, commodity and owned company-stock endpoint is validated before application. Existing NPC merchants keep their original two-port policies and finite budgets.

| Instruction | Physical and financial effect | Applicable limits |
| --- | --- | --- |
| Buy from public market | Market stock moves into the docked hold; company pays the market | Maximum marginal ask, company cash reserve, per-order expenditure cap |
| Sell to public market | Named cargo moves into the local market; finite market cash pays the company | Minimum marginal bid |
| Load from company town stock | Local owned stock moves into the docked hold; no payment | Quantity retained in that town's company stock |
| Unload to company town stock | Named cargo moves into the local owned store; no payment | Actual remaining company storage capacity |

Every instruction has a positive **maximum whole units per visit**, a positive **minimum whole quantity** (default one), and **Skip for this visit** or **Wait at this stop**. The maximum is an order cap, not a cargo target. Execution chooses the largest quantity satisfying current stock, space, exact prices, real funds and applicable limits. It executes that quantity once and advances even if less than the maximum. If less than the minimum is possible, Skip advances without a transaction; Wait retains that exact instruction without spending. Earlier completed orders are never repeated because a later one waits.

Only the named good is affected. Bread, Flour and Logs may remain aboard together within the combined hold capacity; unmentioned or unsold goods stay aboard. A full hold produces a specific capacity blocker. A partial sale is a completed instruction and can leave some of that good aboard for later explicitly configured work. Skipping a bad sale can therefore constrain later purchases. Automation never empties incompatible cargo, tops up stock or loans money.

Price limits constrain the worst marginal unit, not the first quote or execution average. A price limit of zero is unrestricted; a blank UI spending cap is unrestricted within the real account bounds. Purchase reserve and spending cap apply independently to each purchase. A warehouse reserve applies only to loading and can retain fractional local stock, while the transferred amount remains whole. Current remote quotes and source-stock previews are informational: they reserve nothing and can change before arrival. All paid actions and transfers reuse the ordinary preview, transaction and FIFO paths.

### Clock, cursor and safe edits

Each ship stores its exact stop index, next action index, pass/cycle, last committed decision and blocker, last quantity, enabled flag and daily operation budget. At most **eight operations per ship per economic day** include successful commands, intentional skips and departure. Eight commands at one stop can leave departure for the next day. Repeated **Run dockside actions**, save/load or pausing cannot reset the budget. A wait stops the chain; an all-blocked route cannot spin. Historical routes keep their original four-action chain limit.

Departure requires every stop instruction to have executed or intentionally skipped. It spends no day and starts the ordinary integer-duration voyage; only subsequent day advancement permits arrival. During travel the stored stop names the destination and the next action is zero. The displayed pass advances when leaving the last listed stop, so applying at a middle stop can start with a partial pass; it is not proof of a completed round trip. Ships retain the existing priority/ordinal-ID execution order, sharing company cash once. A later exception rolls back all earlier staged ship actions, cursor/budget changes, cash and FIFO together.

Opening the planner pauses playback. Drafting, selecting ships and inspecting fields spend no time or money and do not require an empty hold. **Apply draft** preserves cargo and leaves automation paused. Replacing a policy requires manual control, an actual dock included in the new plan, no partially executed old stop, and no itinerary operations already performed on the current day. A stop is replaceable before its first action or after all its commands; after work today, advance a day before replacing it. Reapplying identical policy content preserves progress. Only one market, industrial or cargo-itinerary policy can be active on a ship.

**Manual control · route actions paused** disables future route actions without changing the itinerary, voyage or cargo. Ordinary manual permissions then apply. To resume, the ship must be at the exact next stop or on its matching incoming voyage; manual travel elsewhere does not reset the cursor. A partially completed waiting stop must satisfy its remaining orders before replacement. Inspect the specific stock, funds, reserve or price blocker instead of assuming that pausing alone advances or cancels it.

### Plan and inspect in Unity

From **Company → Fleet**, choose the ship's **Route planner**; Harbor and Industry also link to that ship's planner. The live header shows actual dock/voyage, next stop/action, cargo capacity and the authoritative current reason. **Cargo and committed route state** expands the held commodities and used daily budget.

Each stop is a compact card. Choose its town, add/remove/reorder stops, and expand an action to choose its instruction, good, maximum, minimum, skip/wait and relevant financial/storage limits. Only that action's applicable fields are shown. Maximum/minimum fields accept whole units; coin and warehouse reserves accept up to four decimal places. Invalid fields remain a draft and prevent application. **Reload applied route** discards draft edits in favor of the actual policy. **Inspect harbor** and **Open current harbor** return to the existing Harbor trading surface; **Back** restores the previous screen or overlay.

**Draft Grain–Flour–Bread loop** resolves the current world's economic roles and creates this unapplied example:

| Stop | Ordered instructions |
| --- | --- |
| A — vulnerable coast with an owned Bakery | Unload up to 2 Flour; unload up to 2 Logs; load up to 4 Bread |
| B — agricultural food source | Sell up to 4 Bread; buy up to 4 Grain; buy up to 2 Logs |
| C — processing town | Sell up to 4 Grain; buy up to 2 Flour; sail back to A |

All example orders have minimum one and Skip on shortage. Purchases keep 100 company coins and cap each order at 200 coins. You must actually build the eligible Bakery and fund inputs and wages; the preset creates nothing. Starting empty at A is valid: its first absent-input/output orders skip, and later arrivals deliver real inputs. The ordinary ship phase precedes factory production, so newly supplied inputs can fund a batch later that day, while newly produced Bread becomes available for a subsequent visit or explicit dockside action.

### Exact continuation

Schema5 saves preserve complete typed plans, exact stop/action/cycle, last reason and daily budget. Loading never resets completed work. Retry recreates the original initial world. Historical player route editors and old save schemas are retired.

### Bounded ordinary-world witness

The focused headless test uses **seed 99, Merchant Company, StockOnly, Ascension 0, Sandbox, pooled storage and itinerary version 1**. It pays 300 coins for a Westhaven Bakery, sails from Southhaven to Westhaven and enables the unmodified example on day 3. After that, it submits only whole-day advances through day 60. The public processing industry and all three ordinary NPC routes remain active; there are no supplied test goods, subsidies or manual cargo moves.

The ship physically returns through Westhaven → Southhaven → Northport → Westhaven on days **17, 31, 45 and 59**. Seven funded Bakery batches produce **14 Bread** and pay **42 coins in wages**. The route buys/sells 16 Grain, buys 8 Flour and 8 Logs, unloads 8 of each input, loads 12 Bread and sells 8 Bread. Final owned stock is 6 Bread, 1 Flour and 1 Log.

| Day-60 measure | Coins |
| --- | ---: |
| Company cash | 1,534.6896 |
| Reference Company Value | 2,539.6896 |
| Inventory purchases / gross sale revenue | 209.6664 / 292.2 |
| Released material basis / held basis | 128.3332 / 81.3332 |
| Realized material gross margin | 163.8668 |
| Operating surplus after wages | 121.8668 |
| Commercial service fees (separate expense) | 5.844 |

Construction is separate capital expenditure. Reference Company Value is not realized income. All-account money remains **11,730 coins**, with zero Mint issuance and zero external inflow. Each operation reconciles physical production/consumption and all owners' cash, FIFO basis and quantities. Reversing equivalent configuration order reproduces identical output and final state. This is a bounded operational witness, not a claim of permanent profitability or an actual Unity interaction check.

```text
dotnet test tests/SaltAndLedger.Simulation.Tests/SaltAndLedger.Simulation.Tests.csproj -c Release --filter FullyQualifiedName~CargoItineraryTests --logger "console;verbosity=detailed"
```

The focused suite covers scarce stock/cash, finite market liquidity, mixed/full holds, reserves, exact marginal caps, skip/wait, partial-order completion, repeated-call daily limits, three independent ships, manual economic equivalence, stale decisions and exceptional rollback. The current results above were recorded in the full consolidation test run with circulation rules1. Actual Editor/player interaction and full integration checks are recorded in [current verification](REPOSITORY-CLEANUP.md).
