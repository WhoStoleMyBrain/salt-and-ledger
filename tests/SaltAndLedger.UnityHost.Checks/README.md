# Portable Unity host checks

This console check compiles the actual Unity-independent session, display-text, voyage-interval and
configuration-file classes against the real simulation. It uses no simulated/mock
world state and adds no NuGet packages beyond the simulation's locked dependency.

From the repository root:

```text
dotnet restore SaltAndLedger.sln --locked-mode
dotnet build SaltAndLedger.sln -c Release --no-restore
dotnet run --project tests/SaltAndLedger.UnityHost.Checks -c Release --no-build
```

An optional sole argument supplies the repository path when running elsewhere.
The check parses every Unity C# file using the selected .NET SDK's Roslyn compiler,
then exercises real 8-, 10-, and 12-town JSON configurations, multi-lane voyages,
exact mid-voyage save/load and continuation, Retry, and invalid-file isolation.
It also checks that departure and same-day construction never invent visual travel,
and that catch-up and arrival animation remain between committed voyage progress.
Temporary configuration files are removed afterward. Any failure exits nonzero.

This does not type-check the Unity-dependent UI or replace Editor/EditMode/PlayMode
and Windows Mono verification. See `docs/UNITY.md` for the remaining checks.
