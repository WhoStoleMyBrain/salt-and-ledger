using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;

/// <summary>One handcrafted fixture shared by the manual and automated comparisons.</summary>
internal static class ShippingScenario
{
    internal const string North = "northport";
    internal const string South = "southhaven";
    internal const int Capacity = 6;

    internal static MaritimeWorldState Create(ContentCatalog content, bool reversePorts = false,
        long southCash = 1000)
    {
        var ports = new[]
        {
            new PortConfiguration(North, new[]
            {
                Linear("logs", 10, 12, 6, 50), Linear("planks", 24, 6, 12, 60)
            }, 1000, Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["logs"] = 18 }),
            new PortConfiguration(South, new[]
            {
                Linear("logs", 20, 6, 12, 60), Linear("planks", 12, 6, 6, 50)
            }, southCash, new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") },
                new Dictionary<string, int> { ["logs"] = 2 })
        };
        return new MaritimeWorldState(content, reversePorts ? ports.Reverse() : ports,
            "trader", 100, new ShipConfiguration("ship", Capacity, North), travelTicks: 2);
    }

    private static MarketQuote Linear(string goodId, long referencePrice, int target,
        long sensitivity, long maximum) =>
        new MarketQuote(goodId, new InventoryPricing(InventoryPricingModel.Linear,
            referencePrice, target, sensitivity, 1, maximum, 1));
}
