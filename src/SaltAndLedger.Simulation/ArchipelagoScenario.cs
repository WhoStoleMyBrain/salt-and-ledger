using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Shared authored experiment configuration; economic operations remain in the world.</summary>
    public static class ArchipelagoScenario
    {
        public const string PlayerShipId = "player_ship";
        public const string TimberShipId = "timber_merchant";
        public const string PlankShipId = "plank_merchant";
        public const string North = "northport";
        public const string South = "southhaven";
        public const string East = "eastport";
        public const string West = "westhaven";

        public static RegionalWorldState Create(ContentCatalog content, bool reverseConfiguration = false)
        {
            var northCamp = new BuildingInstance("north_lumber_camp", "lumber_camp", "harvest_logs");
            var westCamp = new BuildingInstance("west_lumber_camp", "lumber_camp", "harvest_logs");
            var sawmill = new BuildingInstance("south_sawmill", "sawmill", "saw_planks");
            var ports = new[]
            {
                Port(North, "Northport", 340, 120, "Lumber exporter; plank demand", 2500, 14, 0,
                    new[] { Linear("logs", 10, 12, 6, 40), Linear("planks", 28, 5, 12, 60) },
                    new[] { new ScheduledFacilityConfiguration(northCamp, 3, 18) },
                    new[] { new ConsumptionConfiguration("north_construction", "planks", 1, 5) }),
                Port(South, "Southhaven", 570, 550, "Sawmill; imported logs become planks", 4000, 4, 5,
                    new[] { Linear("logs", 20, 8, 12, 60), Linear("planks", 14, 6, 8, 50) },
                    new[] { new ScheduledFacilityConfiguration(sawmill, 2, 12) },
                    Array.Empty<ConsumptionConfiguration>()),
                Port(East, "Eastport", 850, 250, "Commercial settlement; plank demand", 4000, 0, 2,
                    new[] { Linear("logs", 16, 6, 6, 50), Linear("planks", 30, 5, 10, 60) },
                    Array.Empty<ScheduledFacilityConfiguration>(),
                    new[] { new ConsumptionConfiguration("east_construction", "planks", 1, 4) }),
                Port(West, "Westhaven", 120, 400, "Secondary lumber exporter; plank demand", 2200, 12, 1,
                    new[] { Linear("logs", 9, 10, 5, 40), Linear("planks", 26, 4, 12, 60) },
                    new[] { new ScheduledFacilityConfiguration(westCamp, 4, 16) },
                    new[] { new ConsumptionConfiguration("west_construction", "planks", 1, 6) })
            };
            var routes = new[]
            {
                new SeaRoute(North, West, 200),
                new SeaRoute(North, South, 300),
                new SeaRoute(North, East, 400),
                new SeaRoute(South, East, 300),
                new SeaRoute(South, West, 400),
                new SeaRoute(East, West, 600)
            };
            var ships = new[]
            {
                new RegionalShipConfiguration(new ShipConfiguration(TimberShipId, 5, North),
                    "timber_company", "Timber Merchant", 300, 100, priority: 0,
                    policy: new TradeRoutePolicy(North, South,
                        new RouteLeg("logs", 5, 3, 14, 10, 70), allowEmptyReturn: true),
                    automationEnabled: true, minimumOriginStock: 10),
                new RegionalShipConfiguration(new ShipConfiguration(PlankShipId, 3, South),
                    "plank_company", "Plank Merchant", 400, 100, priority: 1,
                    policy: new TradeRoutePolicy(South, East,
                        new RouteLeg("planks", 3, 2, 24, 20, 72), allowEmptyReturn: true),
                    automationEnabled: true, minimumOriginStock: 5),
                new RegionalShipConfiguration(new ShipConfiguration(PlayerShipId, 8, North),
                    "player", "Ledger", 200, 100, priority: 10, isPlayer: true,
                    policy: PlayerPolicy(), automationEnabled: false)
            };
            return new RegionalWorldState(content,
                reverseConfiguration ? ports.Reverse() : ports,
                reverseConfiguration ? routes.Reverse() : routes,
                reverseConfiguration ? ships.Reverse() : ships);
        }

        public static TradeRoutePolicy PlayerPolicy(bool restrictive = false) =>
            new TradeRoutePolicy(North, South,
                new RouteLeg("logs", 6, 2, restrictive ? 9 : 14, 14, restrictive ? 36 : 100),
                new RouteLeg("planks", 3, 1, 24, 18, 72), cashReserve: 20, allowEmptyReturn: true);

        private static MarketQuote Linear(string good, long reference, int target, long sensitivity, long ceiling) =>
            new MarketQuote(good, new InventoryPricing(InventoryPricingModel.Linear,
                reference, target, sensitivity, 1, ceiling, 1));

        private static RegionalPortConfiguration Port(string id, string name, int x, int y, string role,
            long cash, int logs, int planks, IEnumerable<MarketQuote> quotes,
            ScheduledFacilityConfiguration[] facilities, ConsumptionConfiguration[] consumption) =>
            new RegionalPortConfiguration(new PortConfiguration(id, quotes, cash,
                facilities.Select(facility => facility.Instance),
                new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks }),
                name, x, y, facilities, consumption, role);
    }
}
