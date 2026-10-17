# Local Unity 6 integration check

The repository has no Unity project. The simulation targets `netstandard2.1` with C# 8 to fit Unity 6's .NET Standard API profile, but standalone build and test results do **not** establish Unity Editor or player compatibility. These steps are a local verification procedure; they are not a claim that it has been run.

Unity 6 documents .NET Standard 2.1 support and API compatibility settings in its [managed plug-in compatibility guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html).

## Prepare the assembly and host

1. Build from the repository root:

   ```powershell
   dotnet build src/SaltAndLedger.Simulation/SaltAndLedger.Simulation.csproj -c Release
   ```

2. Open a separate local Unity 6 test project. Under **Edit > Project Settings > Player > Other Settings > Configuration**, set **Api Compatibility Level** to **.NET Standard 2.1** for the target platform. The exact grouping may differ between Unity 6 releases.

3. Install Unity's official package **`com.unity.nuget.newtonsoft-json`**, with **3.2.2** as the intended package version for this integration check. If Package Manager does not list it, use **Add package by name** with that package name and version. The standalone library pins Newtonsoft.Json 13.0.3; the Unity package supplies the assembly used by Unity. The official [3.2 package manual](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html) identifies its documented upstream version as 13.0.2, so do not assume an exact dependency match. Check the installed package and verify compatibility using the tests below; see also the [package changelog](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/changelog/CHANGELOG.html).

4. Copy only `src/SaltAndLedger.Simulation/bin/Release/netstandard2.1/SaltAndLedger.Simulation.dll` into the Unity project's `Assets/Plugins/SaltAndLedger/`. Do not also copy a standalone `Newtonsoft.Json.dll`; Unity's package supplies that dependency. The optional `.pdb` can be copied for debugging. Do not import the `net8.0` demo or test assemblies.

5. Copy the repository's `data/content.json` into `Assets/Content/content.json` as a TextAsset. This is a verification copy; the repository data remains canonical. Refresh the copy when testing content changes.

## Editor smoke test

Create `Assets/SaltAndLedgerSmokeTest.cs`, attach it to an empty GameObject, and assign the imported JSON TextAsset to `Content` in the Inspector:

```csharp
using System.Collections.Generic;
using SaltAndLedger.Simulation;
using UnityEngine;

public sealed class SaltAndLedgerSmokeTest : MonoBehaviour
{
    public TextAsset Content;

    private void Start()
    {
        var catalog = ContentLoader.Load(Content.text);
        var inventory = new Inventory(catalog, new Dictionary<string, int>
        {
            ["logs"] = 2,
            ["planks"] = 0
        });

        bool produced = Production.TryRunBatch(inventory, "sawmill", "saw_planks");
        Debug.Assert(produced);
        Debug.Assert(inventory.GetQuantity("logs") == 0);
        Debug.Assert(inventory.GetQuantity("planks") == 1);
        Debug.Assert(!Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Debug.Assert(inventory.GetQuantity("logs") == 0);
        Debug.Assert(inventory.GetQuantity("planks") == 1);
        Debug.Log("Salt & Ledger smoke test passed: 0 logs, 1 plank.");
    }
}
```

Enter Play mode and confirm that the Editor compiles without dependency errors, all assertions pass, and the completion message appears. Also verify that malformed content is rejected by the loader when deliberately supplied in a separate test asset.

## Player verification and limitations

Build and run a development player with this scene on the intended target platform and inspect the player log. Test the actual scripting backend and stripping configuration intended for the game. Where supported and relevant, exercise Mono and IL2CPP separately: successful Editor execution does not verify IL2CPP ahead-of-time compilation or managed stripping behavior.

Record the Unity version, platform, backend, API compatibility level, Newtonsoft package version, stripping level, and result. If reflection or stripping issues emerge, diagnose them in that concrete configuration before adding preservation rules; this repository does not include an untested blanket `link.xml`.

The smoke test validates assembly import, dependency resolution, content loading, and the sample command. It does not verify a production content-distribution pipeline, save games, an encyclopedia UI, performance, or all supported player platforms.
