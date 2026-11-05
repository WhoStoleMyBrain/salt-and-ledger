using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.Simulation.Tests;

internal static class CommissionTestSettings
{
    // Finite authored endowments isolate welfare/credit rules from seed-specific NPC competition.
    internal static WorldGenerationConfiguration FiniteFood()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["npc"]!["minimumCount"] = 0;
        json["npc"]!["maximumCount"] = 0;
        json["npc"]!["tradersPerTownPermille"] = 0;
        json["population"] = new JObject { ["min"] = 21, ["max"] = 21 };
        json["prosperity"] = new JObject { ["min"] = 60, ["max"] = 60 };
        json["startingInventories"]!["bread"] = new JObject { ["min"] = 12, ["max"] = 12 };
        json["startingInventories"]!["planks"] = new JObject { ["min"] = 8, ["max"] = 8 };
        json["startingInventories"]!["flour"] = new JObject { ["min"] = 16, ["max"] = 16 };
        return WorldGenerationConfiguration.Parse(json.ToString());
    }
}
