# Cargo itineraries and automation

Player routes use ordered cargo itineraries. NPC merchants retain their proven two-port market policies. Controlled economic experiments also retain industrial two-port policies; these are research controls, not alternate player editors.

## Factory, warehouse and ship responsibilities

**Factories produce, warehouses trade locally, ships transport.** Industry's overview opens an individual factory with Enabled, Operating rate and Priority. **Apply changes** commits those settings together; Cancel restores the applied settings. Full-rate Normal is the default. Lower rates admit fewer complete scheduled batches, preserving exact material quantities and full wages. High priority takes shared inputs before Normal and Low; ties use stable factory IDs. A blocked admitted batch retries without accumulating missed batches. See [exact production scheduling](MECHANICS.md#production-construction-and-storage).

Industry's Warehouse tab and Harbor's Warehouse automation button open the same company/town Buy/Sell rules. All seven goods can be configured, including goods only needed for logistics. Each Buy row has enabled, desired stock and maximum marginal price; each Sell row has enabled, retained stock and minimum marginal price. Zero price means unrestricted. Buy target must not exceed Sell reserve when both are enabled. The common cash reserve protects purchases, not payroll.

Warehouse V2 applies a valid completed edit or switch immediately. Invalid values remain highlighted while the previous applied policy continues; fix the error or reload the applied rules. There is no separate warehouse Apply step. Switching Buy/Sell or editing another commodity preserves the other settings. The reserve popup edits the same buy cash reserve from either tab, not a new constraint on sales. These configuration changes execute no trades or economic days and do not write a local save slot; save the run separately.

Buying runs before private production and selling afterward, once per enabled good in stable order. Proposals use current stock, exact marginal prices, finite market/company funds and actual capacity. They are not future reservations. Warehouse rules remain active when factories are disabled and may stock real paid construction sites. They never create a warehouse, goods or money. Ship loading and unloading stay separate physical operations at the actual dock.

The warehouse shows conditional private output/input needs over12 days, current stock and rule blockers. Rate-adjusted totals count admitted whole batches; installed full-rate capacity remains in breakdowns. A factory's missing-input action opens the ordinary Market ↔ Store trade pair with the relevant good and quantity, then returns to that factory. Warehouse automation is a separate action. Buying an ingredient does not advance production or fix an unrelated payroll/storage blocker. Local factory trading forms are retired.

## Ordered cargo itineraries

Immutable `CargoItineraryPolicy` version 1 applies to player ships in v3 pooled-stock runs. It has **2–4 unique port stops**, **1–8 ordered commands per stop**, and **at most 24 commands total**. The last stop connects back to the first through a traversable sea path. Each leg may cross intermediate towns; transit neither executes their orders nor grants dock visits. Every port, connection, commodity and ship ownership is validated before application. Company storage need not exist yet: a future Load/Unload is a valid plan, with a missing-warehouse warning. Existing NPC merchants keep their original two-port policies and finite budgets.

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

Opening the planner pauses playback. Drafting, selecting ships and inspecting fields spend no time or money and do not require an empty hold. **Review route → Apply route** preserves cargo and leaves automation paused. Replacing a policy requires manual control, an actual dock included in the new plan, no partially executed old stop, and no itinerary operations already performed on the current day. A stop is replaceable before its first action or after all its commands; after work today, advance a day before replacing it. Reapplying identical policy content preserves progress. Only one market, industrial or cargo-itinerary policy can be active on a ship.

**Manual control / pause route** disables future route actions without changing the itinerary, voyage or cargo. Ordinary manual permissions then apply. To resume, the ship must be at the exact next stop or on its matching incoming voyage; manual travel elsewhere does not reset the cursor. A partially completed waiting stop must satisfy its remaining orders before replacement. Inspect the specific stock, funds, reserve or price blocker instead of assuming that pausing alone advances or cancels it.

### Plan and inspect in Unity

From **Fleet**, choose the owned ship and open **Route planner**; the ship inspector, Harbor and Industry also link to that ship's planner. Route V2 separates plotting the Course, editing stop Orders, Review and the Applied route. The live status shows actual dock/voyage, next stop/action, cargo capacity and the authoritative current reason. **Route tools & details → Cargo, last outcome & daily budget** expands the held commodities and used daily budget.

Plot two to four unique towns on the route chart, using deterministic paths through the actual available connections. Reorder or remove stop cards before **Set stop orders**. Select a stop, then a commodity/action cell in its **Sell / Buy / Unload / Load** grid. The cell opens an inline quantity editor; **Save order** changes the draft, while Cancel retains the previous instruction. For example, save Flour 2 and Logs 2 under Unload, then Bread 4 under Load: three goods at one stop. Each instruction still counts toward the eight-command stop and 24-command route limits. Explicit Remove deletes an instruction rather than treating invalid text as a command.

New stops use **Unload → Sell → Load → Buy**, then stable commodity-ID order, freeing cargo space before acquiring goods. This execution order is separate from the visual column order. Applied routes and presets retain their exact order. The inline editor exposes minimum shipments, Skip/Wait, marginal prices, cash/warehouse reserves and spending caps through advanced settings. The ordered-instruction list exposes the actual sequence; moving an instruction fixes that stop's custom order and later new grid instructions append. Repeated instructions for the same good/action remain separate and individually editable rather than being silently merged into one cell.

Maximum/minimum fields accept whole units; coin and warehouse reserves accept up to four decimal places. Invalid fields remain in the inline editor and prevent saving that edit. **Review route** checks the whole draft before **Apply route** assigns it. **Start route** and **Pause route** control execution separately. **Reload saved route** discards draft edits in favor of the ship's actual applied policy; it does not load a local run slot. **Route tools & details → Inspect harbor** opens ordinary trading and returns to the planner on Back. The current-hold summary shows actual cargo/free capacity, not predicted cargo after proposed orders or reserved arrival stock.

Missing company storage is an ordinary **Skip/Wait** blocker when the instruction reaches execution. A Skip advances without moving goods; a Wait retains the instruction. Build eligible industry through Industry to create real local company storage; the already assigned route can then use it without being recreated. The planner never creates storage, buildings or cargo. Other current blockers, such as absent cargo or a full hold, may take precedence. Pending paid facilities already provide storage under the existing construction rules; production still waits for completion.

**Draft Grain–Flour–Bread loop** resolves actual eligible Bakery, Grain and Flour locations in the generated world. It drafts only an unapplied itinerary; inspect its resolved stop orders and import dependencies before applying it. It creates no facilities, cargo or wages. Default specialization requires Flour and fuel producers to remain outside Bakery towns. Public markets can hold imported goods even where those goods cannot be produced locally.

### Exact continuation

Schema6 saves preserve complete typed plans, exact stop/action/cycle, last reason and daily budget. Loading never resets completed work. Retry recreates the original initial world. Historical player route editors and old save schemas are retired.

### Headless evidence

The procedural journey and navigation tests run ordinary paid trades, construction, production, transfers and multi-lane voyages, including exact save continuation and accounting audits. Run `dotnet test SaltAndLedger.sln -c Release`, and use `--world-generation --check` for representative world reports. Earlier seed-99 four-town numerical observations remain in Git history; they are not current generator4 predictions. See [Task3.1 verification](TASK-3.1-VERIFICATION.md) for checks actually performed and remaining Unity interaction checks.
