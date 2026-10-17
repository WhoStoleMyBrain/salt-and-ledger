# Implemented architecture

Phase 0 separates content, simulation, host presentation, and reference generation using a small set of concrete modules. Phase 0.5 connects the same simulation to a minimal Unity host. Neither introduces services or extension points for future systems.

```text
data/content.json + data/content.schema.json
    |
    +-- Python schema + semantic validation
    |       +-- Markdown --> docs/reference/ --> MkDocs
    |       +-- structured reference --> generated/encyclopedia.json
    |
    +-- C# strict loading + semantic validation
            +-- immutable ContentCatalog
                    +-- Inventory + Production.TryRunBatch
                            +-- console demo / headless tests
                            +-- Unity Bootstrap smoke component
```

## Modules and dependencies

| Module | Responsibility | Dependencies |
| --- | --- | --- |
| `src/SaltAndLedger.Simulation` | Content loading, immutable definitions, inventory, one-batch production | `netstandard2.1`, C# 8, Newtonsoft.Json 13.0.3 |
| `samples/SaltAndLedger.Demo` | Read a content file and demonstrate successful and rejected production | `net10.0`, simulation project |
| `tests/SaltAndLedger.Simulation.Tests` | Headless tests of loading, content rules, inventory, and production | `net10.0`, simulation project, test dependencies |
| `unity` | Bootstrap smoke scene/component, integration tests, and a player build entry point | Unity 6000.6.6f1, staged simulation DLL, Unity Newtonsoft package 3.2.2 |
| `data` | Canonical content and Draft 2020-12 JSON Schema | No host or presentation dependency |
| `tools` | Validate authoring data; generate reference artifacts; prepare Unity runtime assets | Python, dependencies in `requirements.txt`; .NET 10 for Unity preparation |
| `tests/python` | Schema, semantic validator, and generation tests | Python test runner and pipeline dependencies |
| `docs` and `mkdocs.yml` | Authored specifications and external reference site | MkDocs |
| `.github/workflows` | Headless build, test, validation, freshness, site, and Unity staging checks | .NET 10 SDK and Python; no Unity Editor |

The simulation does not reference `UnityEngine`, access an Editor API, or decide where a host stores its content. The console host supplies JSON from a file; the Unity host supplies the same JSON from a `TextAsset`. The existing Unity project and its template packages remain in place; the integration adds no economic rules.

## Toolchain and Unity dependency boundary

`global.json` selects stable .NET 10 SDKs starting at 10.0.100 with `latestFeature` roll-forward within 10.0. This lets developers receive SDK servicing updates without adopting a later major runtime. The simulation's `netstandard2.1` and C# 8 targets remain unchanged. All three standalone projects have NuGet lock files, and CI restores with `--locked-mode`.

`python tools/prepare_unity.py` validates canonical content, builds the existing simulation project in Release, and stages only `SaltAndLedger.Simulation.dll` and `data/content.json` under `unity/Assets/Generated/`, with stable generated metadata. The content becomes `Resources/CanonicalContent.json`. No simulation source is copied or compiled a second way. Preparation must be rerun after relevant changes; `--check` compares source, configuration, and output hashes without rebuilding. These local artifacts and their manifest are ignored by Git.

Unity's versioned package manifest and lock file supply `com.unity.nuget.newtonsoft-json` 3.2.2. The NuGet build dependency remains Newtonsoft.Json 13.0.3. The inspected Unity package contains upstream 13.0.2, with the same strong-named assembly identity (`Newtonsoft.Json`, version `13.0.0.0`, public key token `30ad4fe6b2a6aeed`). Matching identity permits dependency resolution but does not establish behavior on every backend. Integration tests exercise the actual Unity package; [Unity integration](UNITY.md) records results and remaining platform checks. Do not stage the NuGet Newtonsoft DLL, demo, or headless test assemblies into Unity.

The Bootstrap component loads the staged content, invokes the existing production command for three successful batches and one rejected batch, and reports the resulting inventory. Unity tests and player verification cover the host boundary; standalone tests remain the main simulation regression suite. Runtime content distribution beyond this small `Resources` asset is deferred.

## Content contract

`data/content.json` declares `schemaVersion: 1` and definitions for goods, buildings, and recipes. IDs are stable, globally unique across entity kinds, and independent of display names. Recipes reference an existing building and existing goods. Definitions are data, not executable behaviors.

Python applies the JSON Schema and semantic checks. The C# loader implements the same accepted content contract without a runtime JSON Schema engine. Schema constraints alone are insufficient: reference integrity, global uniqueness, and strict JSON integer-token representation also require validation. For example, JSON Schema can consider `1.0` mathematically integral, but this content format rejects it and `1e0`; use `1`. Duplicate object keys and non-JSON numeric constants such as `NaN` or `Infinity` are rejected.

Content versioning is explicit, with only version 1 supported. There is no migration framework. If the contract changes, update schema, both validators, fixtures, generated output, and the relevant specifications together.

Version 1 also enforces the following restrictions:

- IDs match `[a-z][a-z0-9_]*` in full; names and descriptions are nonblank strings.
- Goods, buildings, recipes, recipe inputs, and recipe outputs are nonempty arrays.
- Quantities are integer JSON tokens from 1 through 2,147,483,647. Inventory state can additionally contain zero.
- A good appears at most once on each side of a recipe. A good may appear on both sides; no extra economic assumption forbids that relationship.
- Unknown properties are rejected at every object level. References must target the correct entity kind, not merely an existing ID.

The format accepts standard JSON only: comments, trailing commas, duplicate keys, and nonstandard constants are invalid.

## C# API and state ownership

The public types live in `SaltAndLedger.Simulation`:

- `ContentLoader.Load(string json)` returns an immutable `ContentCatalog` after validation.
- `ContentCatalog` exposes read-only lists `Goods`, `Buildings`, and `Recipes`, with `GetGood(id)`, `GetBuilding(id)`, and `GetRecipe(id)` lookup methods.
- Definitions expose `Id`, `Name`, and `Description`; recipes also expose `BuildingId`, `Inputs`, and `Outputs`. Each recipe quantity has `GoodId` and `Quantity`.
- `Inventory` is created for a catalog, optionally with initial quantities. `GetQuantity(id)` reads stock; `Snapshot()` returns a detached read-only view of all stock.
- `Production.TryRunBatch(inventory, buildingId, recipeId)` performs one explicit production transition. It returns `true` on success and `false` when inputs are insufficient. Invalid IDs, an incompatible building, and arithmetic overflow are rejected without mutation.

Inventory owns mutable quantities; the catalog provides immutable definitions. The production command computes and checks its result before applying it. The [maintained mechanics specification](MECHANICS.md) defines the behavior that generated content cannot explain.

```csharp
using System.Collections.Generic;
using SaltAndLedger.Simulation;

// The host supplies JSON text from data/content.json.
ContentCatalog content = ContentLoader.Load(json);
var inventory = new Inventory(content, new Dictionary<string, int>
{
    ["logs"] = 2,
    ["planks"] = 0
});

bool produced = Production.TryRunBatch(inventory, "sawmill", "saw_planks");
// produced == true; logs == 0; planks == 1.
```

## Determinism and headless execution

For a given validated catalog, initial inventory, and sequence of commands, the implemented production logic produces the same state and outcomes. It uses integer arithmetic, no random values, no wall clock, and no background scheduling. Checked arithmetic prevents overflow from silently changing stock.

This is determinism for the implemented command, not yet a world simulation or a promise of save/replay compatibility. Time stepping, event ordering, seed management, save formats, concurrency, and replay compatibility must be specified when those systems are introduced. The current API assumes sequential calls; concurrent mutation is not a supported execution model.

The separation makes future headless experiment batches possible. No balance runner, performance target, or verified thousands-of-runs benchmark is claimed in Phase 0.

## Documentation pipeline

The generator validates the source before deriving output. It owns all of `docs/reference/` and only `generated/encyclopedia.json` under `generated/`. Authored pages stay outside the generated directory.

| Output | Contents |
| --- | --- |
| `docs/reference/index.md` | Index of generated entities |
| `docs/reference/goods/{id}.md` | Good definition and consuming/producing recipe links |
| `docs/reference/buildings/{id}.md` | Building definition and recipe links |
| `docs/reference/recipes/{id}.md` | Building, inputs, outputs, and links to related definitions |
| `generated/encyclopedia.json` | Sorted reference entities with IDs, source facts, wiki paths, and relationship IDs |

The structured output declares `formatVersion: 1` and `sourceSchemaVersion: 1`. Each entity has a `wikiPath` relative to `docs/`; goods add `consumedByRecipeIds` and `producedByRecipeIds`, buildings add `recipeIds`, and recipes retain `buildingId`, `inputs`, and `outputs`. Entity arrays sort by stable ID; recipe quantities sort by good ID. This is a reference artifact, not a saved game or a replacement runtime content format.

`python tools/generate_docs.py` writes the expected artifacts and removes stale files within its owned reference directory. `python tools/generate_docs.py --check` is read-only and fails for missing, changed, or unexpected files in that directory or a stale encyclopedia artifact. Both generation and validation accept `--source` for an alternate content file, defaulting to `data/content.json`; generation still uses the standard repository output paths.

Generated pages cannot define production time, prices, or behavior absent from the data. [Mechanics](MECHANICS.md) explicitly maintains those rules. Tests and freshness checks keep generated facts synchronized, while review keeps authored specifications aligned with behavior.

## Boundaries and limitations

No market pricing, ships, trade routes, world generation, objectives, modifiers, progression, random events, competing merchants, piracy, combat, multiplayer, automation policy, or elaborate UI exists here. Keep those in the provisional [roadmap](ROADMAP.md), not in unused foundation code.

Headless compilation and tests exercise the portable library and pipeline. Unity import, Editor behavior, Mono player execution, and IL2CPP player execution are separate verification points; follow [Unity integration](UNITY.md) and report which checks actually ran.
