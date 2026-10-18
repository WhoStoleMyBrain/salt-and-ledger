# Maintained mechanics specification

This page specifies implemented behavior through Task 1.1 that cannot be derived from content definitions alone. Update it with behavior changes. Generated reference pages describe only the canonical definitions and their relationships.

## Inventory

An inventory belongs to a validated content catalog. It contains one quantity for each known good; unspecified initial quantities default to zero. Quantities are nonnegative signed 32-bit integers, from 0 through 2,147,483,647 inclusive. Unknown goods and negative initial quantities are rejected.

These limits define an exact, bounded sample state representation. They do not decide future cargo units, storage capacity, market depth, money representation, or production balance. Reads do not mutate stock. A snapshot is detached and cannot be used to modify the inventory.

## One production command

`Production.TryRunBatch(inventory, buildingId, recipeId)` attempts exactly one complete batch:

1. Resolve the building and recipe, and require the recipe to belong to that building.
2. Check that all required inputs are available.
3. Calculate every resulting input and output stock using checked integer arithmetic.
4. Apply the complete change only when all checks succeed.

Success returns `true`. Insufficient inputs return `false`. Unknown identifiers, a wrong building/recipe association, and output overflow are errors. Every rejected or failed command leaves the inventory unchanged; there is no partial consumption or partial output.

A good may occur on both sides of a recipe. Input availability is checked against the starting inventory, so a command cannot use its own future output to meet an input requirement. The calculation then subtracts inputs before adding outputs and checks the final balance for overflow. A batch that consumes and restores the same quantity can therefore succeed at the maximum stock value.

The `buildingId` selects a canonical definition and validates recipe compatibility. This low-level command still requires no world or building instance and advances no clock. The world scheduler below calls this same command; it does not implement a second production algorithm. No inventory reservation, queue, or concurrent execution contract exists.

## Example

The canonical recipe `saw_planks` belongs to `sawmill`, consumes 2 `logs`, and produces 1 `planks` per successful command.

| Before | Command outcome | After |
| --- | --- | --- |
| 2 logs, 0 planks | Successful batch | 0 logs, 1 plank |
| 1 log, 0 planks | Rejected: insufficient logs | 1 log, 0 planks |
| 2 logs, 2,147,483,647 planks | Rejected: output overflow | 2 logs, 2,147,483,647 planks |

The console demo starts at 6 logs and 0 planks. Three commands succeed; a fourth returns `false`. It ends at 0 logs and 3 planks.

The ratio is demonstration data. It does not imply physical conservation units, an economic price relationship, or a balanced production chain.

## World state and time

`WorldState` owns one fixed location, one shared inventory, a collection of building instances, and a nonnegative signed 64-bit `Tick`. The default tick is 0. A successful `Step` advances it by exactly 1 with checked arithmetic; a step starting at 9,223,372,036,854,775,807 fails without mutation. An empty world or a world with no enabled buildings still advances when stepped.

A tick is one abstract economic step. It has no conversion to hours, days, real seconds, or Unity frames. The host explicitly calls `Step`; the simulation creates no thread, timer, background loop, or random values.

A `BuildingInstance` has its own stable `Id`, a canonical `BuildingId`, a canonical `RecipeId`, an integer `Priority` (default 0), and `ProductionEnabled` (default `true`). Several instances can reference the same building definition and recipe. World construction validates all references and recipe/building associations, even for disabled instances. Runtime location and instance IDs match `[a-z][a-z0-9_]*` in full using ASCII characters; instance IDs must be unique within the world. These are runtime identities, separate from the globally unique canonical definition IDs. No new content definition is created for an instance or the location.

The host supplies initial stock; unspecified goods start at zero. It may also supply a nonnegative initial tick. State is copied into the world. Callers cannot change its inventory or building flags through retained constructor inputs or snapshots. Building instances are immutable values; a configuration change replaces the relevant value inside the next world snapshot. There is no construction, demolition, runtime priority change, or recipe-switching command.

## Tick order and production controls

`Step(commands = null)` performs these phases in order:

1. Enumerate and validate all submitted `SetProductionEnabledCommand` values against the current world. A null command sequence means no commands; a null element, invalid identifier, or unknown building instance rejects the step.
2. Check that advancing the tick will not overflow, and prepare private working state.
3. Apply commands in their declared sequence. `SetProductionEnabledCommand(instanceId, enabled)` affects production in this same step. If commands target the same instance more than once, the last command wins.
4. Select enabled instances. Sort production by ascending `Priority`, then by instance `Id` using ordinal comparison. Negative priorities are valid and run before larger priorities.
5. Attempt exactly one complete batch per eligible instance using `Production.TryRunBatch`. Each attempt sees the working inventory left by earlier attempts in this step, including outputs that can become later instances' inputs in the same tick. There are no stock reservations. Record success or resource shortage.
6. Construct the complete step result and next snapshot, then commit that snapshot once. Its tick is the starting tick plus 1.

The one-batch-per-enabled-instance rule is a temporary prototype scheduling rule, not a final production rate or capacity balance. Buildings are not retried later in the same tick if an earlier attempt had insufficient inputs. Disabled buildings produce no attempt record. Configuration commands do not produce separate events.

Building collection insertion order, dictionary enumeration order, content collection order, object creation order, and frame timing do not determine who runs first. Command sequence order is deliberately meaningful. Fixed priority and ID ordering can repeatedly favor one building when stock is scarce; there is no fairness rotation or automatic priority adjustment.

### Resource contention

With equal priorities, enabled instances `sawmill_a` and `sawmill_b`, and 2 logs / 0 planks:

| Attempt order | Instance | Outcome | Shared inventory afterward |
| --- | --- | --- | --- |
| 1 | `sawmill_a` | Produced | 0 logs, 1 plank |
| 2 | `sawmill_b` | Resource shortage | 0 logs, 1 plank |

The shortage is a normal completed attempt. The step commits and advances the clock. On the next tick, with no new stock, both enabled instances report shortage. Disabling `sawmill_a` at the start of a tick with 2 available logs instead lets `sawmill_b` produce in that tick.

## Atomic steps and errors

Command validation failures, command enumeration failures, invalid configuration, and arithmetic overflow are exceptional conditions. They throw and return no `StepResult`. A failed `Step` leaves the previous tick, inventory, and every production flag unchanged, including any configuration commands or successful production attempts staged before the error. The next valid call retries that same tick. For example, if the first mill can add a plank but the second would overflow plank stock, neither batch commits and the tick does not advance.

| Invalid input or operation | Error contract |
| --- | --- |
| Null required argument or runtime ID | `ArgumentNullException` |
| Malformed runtime ID, duplicate instance, null collection member, or recipe/building mismatch | `ArgumentException` |
| Unknown canonical reference, initial-stock good, or command target | `KeyNotFoundException` |
| Negative initial quantity or tick | `ArgumentOutOfRangeException` |
| Stock or tick overflow | `OverflowException` |
| Calling `Step` recursively on the same world | `InvalidOperationException` |

Runtime ID syntax is checked when constructing the corresponding instance or command. World construction resolves canonical references; `Step` resolves command targets. An exception thrown by a caller's command enumerable propagates without committing world state.

The world stages a private inventory and building state for each step, then replaces one immutable snapshot only after constructing the complete result. This small copying approach is intentional for atomicity and clarity; it is not a generalized transaction system or a performance claim. The API requires one caller making sequential calls; concurrent or reentrant mutation is unsupported. External side effects in a caller-provided command enumerable are outside the world's state contract.

## Step results and snapshots

A completed `StepResult` contains:

- `Tick`: the tick at which commands and production ran. The first default-world result has `Tick == 0`.
- `After`: the immutable world snapshot after the step; the first result has `After.Tick == 1`. It includes location ID, inventory, and building instance state.
- `ProductionAttempts`: attempts in actual production order. Each has building instance, building definition, and recipe identifiers, an outcome of `Produced` or `ResourceShortage`, and its inventory changes.

Each inventory change identifies the good and its `Before`, `After`, and signed `Delta` quantities for that attempt, ordered by ordinal good ID. Only changed balances are included. A shortage has no changes; a successful recipe with no net stock change still has a `Produced` outcome. A step may have no attempt records and still advance its tick.

Snapshots, attempt lists, and change lists are read-only and detached from future state changes. Inventory entries and buildings have stable ordering in snapshots. Keeping an earlier result does not expose or mutate live world state. The world retains only its current snapshot, not an ever-growing event history; a host that wants diagnostic history must choose what to retain.

## Determinism and remaining scope

The same validated content, initial world state, and per-tick ordered commands produce the same states and structured outcomes. Reversing equivalent input building collections does not change results. Callers advance several ticks by calling `Step` repeatedly; no batch-step helper is provided.

There is no production duration, cooldown, rate per minute, capacity cost, money, wage, energy, price, transport, consumption, or configurable automation policy. Production repeats only because the host advances the world. Ordering for future trades, arrivals, and consumption remains unresolved and must be specified when those operations are implemented. Deterministic step results are useful for comparison; they are not a save format, durable replay protocol, event bus, or persistent event-sourcing system.
