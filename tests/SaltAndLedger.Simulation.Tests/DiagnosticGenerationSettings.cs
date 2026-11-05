using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.Simulation.Tests;

internal static class DiagnosticGenerationSettings
{
    // Shared-pool arithmetic fixtures deliberately colocate supply chains to isolate FIFO,
    // scheduling and rollback. Generated player-world tests keep production separation on.
    internal static WorldGenerationConfiguration AllowLocalSupplyChains { get; } = Create();

    private static WorldGenerationConfiguration Create()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["enforceProductionSeparation"] = false;
        return WorldGenerationConfiguration.Parse(json.ToString());
    }
}
