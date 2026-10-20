using System;
using System.Collections.Generic;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    public enum TradingLabScenario
    {
        Standard,
        Restrictive,
        NoDestinationCash
    }

    /// <summary>Handcrafted host configuration matching the headless shipping fixture.</summary>
    public static class TradingLabScenarios
    {
        public const string North = "northport";
        public const string South = "southhaven";
        public const long InitialTraderCash = 100;

        public static MaritimeWorldState CreateWorld(ContentCatalog content, TradingLabScenario scenario)
        {
            if (!Enum.IsDefined(typeof(TradingLabScenario), scenario))
                throw new ArgumentOutOfRangeException(nameof(scenario));
            var ports = new[]
            {
                new PortConfiguration(North, new[]
                {
                    Linear("logs", 10, 12, 6, 50), Linear("planks", 24, 6, 12, 60)
                }, 1000, Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["logs"] = 18 }),
                new PortConfiguration(South, new[]
                {
                    Linear("logs", 20, 6, 12, 60), Linear("planks", 12, 6, 6, 50)
                }, scenario == TradingLabScenario.NoDestinationCash ? 0 : 1000,
                    new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") },
                    new Dictionary<string, int> { ["logs"] = 2 })
            };
            return new MaritimeWorldState(content, ports, "trader", InitialTraderCash,
                new ShipConfiguration("ship", 6, North), travelTicks: 2);
        }

        public static TradeRoutePolicy CreatePolicy(bool restrictive = false)
        {
            return new TradeRoutePolicy(North, South,
                new RouteLeg("logs", 6, 2, restrictive ? 8 : 10, 19),
                new RouteLeg("planks", 2, 2, 18, 31));
        }

        private static MarketQuote Linear(string goodId, long referencePrice, int target,
            long sensitivity, long maximum)
        {
            return new MarketQuote(goodId, new InventoryPricing(InventoryPricingModel.Linear,
                referencePrice, target, sensitivity, 1, maximum, 1));
        }
    }
}
