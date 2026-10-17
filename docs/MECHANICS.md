# Maintained mechanics specification

This page specifies Phase 0 behavior that cannot be derived from content definitions alone. Update it with behavior changes. Generated reference pages describe only the canonical definitions and their relationships.

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

The `buildingId` selects a definition and validates recipe compatibility. The sample does not construct, own, staff, or locate building instances. No inventory reservation, queue, or concurrent execution contract exists.

## Example

The canonical recipe `saw_planks` belongs to `sawmill`, consumes 2 `logs`, and produces 1 `planks` per successful command.

| Before | Command outcome | After |
| --- | --- | --- |
| 2 logs, 0 planks | Successful batch | 0 logs, 1 plank |
| 1 log, 0 planks | Rejected: insufficient logs | 1 log, 0 planks |
| 2 logs, 2,147,483,647 planks | Rejected: output overflow | 2 logs, 2,147,483,647 planks |

The console demo starts at 6 logs and 0 planks. Three commands succeed; a fourth returns `false`. It ends at 0 logs and 3 planks.

The ratio is demonstration data. It does not imply physical conservation units, an economic price relationship, or a balanced production chain.

## Time, determinism, and scope

A production command is instantaneous in this sample: no simulation time advances because there is no simulation clock. There is no duration, cooldown, rate per minute, capacity cost, money, wage, energy, price, transport, or automatic repetition. A caller chooses when to issue each command.

The same content, initial stock, and ordered command sequence produce the same results. The implementation uses no randomness or wall-clock inputs. Determinism currently covers only this explicit state transition; future time stepping and scheduling require their own specification.
