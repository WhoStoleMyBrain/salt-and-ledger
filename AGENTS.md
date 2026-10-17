# Repository guidance

Salt & Ledger is a Unity 6 maritime economic strategy project. This repository implements the Phase 0 foundation and a minimal Phase 0.5 Unity integration. Read `docs/ARCHITECTURE.md` and `docs/GAME_DESIGN.md` before changing its boundaries or behavior.

- Keep `src/SaltAndLedger.Simulation` independent of Unity, presentation, file-system policy, and documentation generation. Preserve `netstandard2.1` and C# 8 compatibility.
- Prefer small concrete types and explicit state transitions. Do not add speculative service layers, plugin systems, or interfaces for deferred features.
- Keep content in `data/content.json`; keep its structural contract in `data/content.schema.json`. Stable IDs are globally unique and must not depend on display names. Keep Python and C# semantic validation aligned.
- Use finite nonnegative integer inventory quantities and checked arithmetic. A failed production command must leave inventory unchanged. Preserve deterministic behavior; do not introduce wall-clock time or unseeded randomness into the simulation.
- Document non-data-derived behavior in `docs/MECHANICS.md`. Label design proposals as hypotheses or deferred ideas rather than accepted rules.
- Do not hand-edit `docs/reference/` or `generated/encyclopedia.json`. Regenerate and commit those files with content changes.
- The demo and headless tests use .NET 10; `global.json` selects a stable 10.0 SDK. Keep NuGet lock files and Unity's package manifest/lock synchronized with intentional dependency changes.
- Run `python tools/prepare_unity.py` after simulation or content changes. It rebuilds and stages the simulation DLL and canonical JSON under ignored `unity/Assets/Generated/`; never copy simulation source or another Newtonsoft assembly into Unity. Unity's pinned UPM package supplies JSON support.
- C#: PascalCase for public types/members, camelCase for local variables/parameters, four-space indentation. Python: snake_case, four-space indentation, standard-library solutions when sufficient. Follow adjacent code.
- Add focused tests for meaningful behavior changes and failure cases. Do not scaffold later roadmap features as part of foundation maintenance.
- Do not claim Unity integration was tested unless the Editor and relevant player build were actually exercised. Use `docs/UNITY.md` for local verification.

Run from the repository root as applicable:

```text
dotnet restore SaltAndLedger.sln --locked-mode
dotnet build src/SaltAndLedger.Simulation/SaltAndLedger.Simulation.csproj -c Release
dotnet test tests/SaltAndLedger.Simulation.Tests/SaltAndLedger.Simulation.Tests.csproj -c Release
dotnet run --project samples/SaltAndLedger.Demo -- data/content.json
python -m pip install -r requirements.txt
python tools/validate_data.py
python tools/generate_docs.py
python tools/generate_docs.py --check
python -m unittest discover -s tests/python -v
python -m mkdocs build --strict
python tools/prepare_unity.py
python tools/prepare_unity.py --check
```

Report what changed, which checks actually ran, and any unverified integration points.
