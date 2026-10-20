using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Authored six-good sandbox and optional charter, shared by Unity and experiments.</summary>
    public static class EnterpriseScenario
    {
        public const string PlayerShipId = ArchipelagoScenario.PlayerShipId;
        public const string PlayerOwnerId = "player";
        public const string North = ArchipelagoScenario.North;
        public const string South = ArchipelagoScenario.South;
        public const string East = ArchipelagoScenario.East;
        public const string West = ArchipelagoScenario.West;
        public const string GrainShipId = "grain_merchant";
        public const string SawmillBlueprint = "private_sawmill";
        public const string BakeryBlueprint = "private_bakery";
        public const string WorkshopBlueprint = "private_workshop";
        public const long InitialPlayerCash = 600;
        public const int CharterBread = 24;
        public const int CharterDeadline = 60;
        public const long CharterMinimumCash = 100;

        public static RegionalWorldState Create(ContentCatalog content, bool challenge = false,
            bool reverseConfiguration = false) =>
            CreateCore(content, challenge ? new ChallengeConfiguration("coastal_supply_charter",
                "The Coastal Supply Charter", PlayerOwnerId, East, "bread", CharterBread,
                CharterDeadline, CharterMinimumCash) : null, null, reverseConfiguration);

        internal static RegionalWorldState CreateRun(ContentCatalog content, ChallengeConfiguration? challenge,
            StartingCharter charter) => CreateCore(content, challenge, charter, false);

        private static RegionalWorldState CreateCore(ContentCatalog content, ChallengeConfiguration? challenge,
            StartingCharter? charter, bool reverseConfiguration)
        {
            var ports = new[]
            {
                Port(North, "Northport", 340, 120, "Grain and timber exporter; public bakery", 5000,
                    Stock(14, 0, 16, 3, 8, 0),
                    Quotes(10, 28, 6, 20, 20, 60),
                    new[] { Facility("north_lumber_camp", "lumber_camp", "harvest_logs", 3, 22),
                        Facility("north_farm", "farm", "harvest_grain", 3, 32),
                        Facility("north_bakery", "bakery", "bake_bread", 3, 18) },
                    new[] { Demand("north_construction", "planks", 1, 5),
                        Demand("north_households", "furniture", 1, 12) }),
                Port(South, "Southhaven", 570, 550, "Sawmill; affordable planks; bread demand", 6000,
                    Stock(4, 5, 0, 0, 2, 0),
                    Quotes(20, 14, 12, 25, 36, 58),
                    new[] { Facility("south_sawmill", "sawmill", "saw_planks", 2, 16) },
                    new[] { Demand("south_food", "bread", 2, 5),
                        Demand("south_households", "furniture", 1, 12) }),
                Port(East, "Eastport", 850, 250, "Furniture workshop; underserved bread market", 8000,
                    Stock(0, 4, 0, 0, 0, 0),
                    Quotes(16, 30, 14, 28, 44, 68),
                    new[] { Facility("east_workshop", "furniture_workshop", "make_furniture", 5, 5) },
                    new[] { Demand("east_food", "bread", 2, 4),
                        Demand("east_construction", "planks", 1, 8),
                        Demand("east_households", "furniture", 1, 8) }),
                Port(West, "Westhaven", 120, 400, "Mill and timber; private food industry opportunity", 5000,
                    Stock(16, 1, 10, 8, 0, 0),
                    Quotes(9, 26, 11, 15, 34, 62),
                    new[] { Facility("west_lumber_camp", "lumber_camp", "harvest_logs", 3, 24),
                        Facility("west_mill", "mill", "mill_flour", 2, 16) },
                    new[] { Demand("west_construction", "planks", 1, 6),
                        Demand("west_food", "bread", 2, 6) })
            };
            var routes = new[]
            {
                new SeaRoute(North, West, 200), new SeaRoute(North, South, 300),
                new SeaRoute(North, East, 400), new SeaRoute(South, East, 300),
                new SeaRoute(South, West, 400), new SeaRoute(East, West, 600)
            };
            var ships = new[]
            {
                new RegionalShipConfiguration(new ShipConfiguration(ArchipelagoScenario.TimberShipId, 5, North),
                    "timber_company", "Timber Merchant", 0, 100, priority: 0,
                    policy: new TradeRoutePolicy(North, South,
                        new RouteLeg("logs", 5, 3, 14, 10, 70), allowEmptyReturn: true),
                    automationEnabled: true, minimumOriginStock: 10),
                new RegionalShipConfiguration(new ShipConfiguration(ArchipelagoScenario.PlankShipId, 3, South),
                    "plank_company", "Plank Merchant", 0, 100, priority: 1,
                    policy: new TradeRoutePolicy(South, East,
                        new RouteLeg("planks", 3, 2, 24, 20, 72), allowEmptyReturn: true),
                    automationEnabled: true, minimumOriginStock: 5),
                new RegionalShipConfiguration(new ShipConfiguration(GrainShipId, 6, North),
                    "grain_company", "Grain & Flour Merchant", 0, 100, priority: 2,
                    policy: new TradeRoutePolicy(North, West,
                        new RouteLeg("grain", 6, 4, 12, 4, 72),
                        new RouteLeg("flour", 3, 1, 22, 4, 66), cashReserve: 20, allowEmptyReturn: true),
                    automationEnabled: true),
                new RegionalShipConfiguration(new ShipConfiguration(PlayerShipId, charter?.CargoCapacity ?? 10,
                    charter?.StartingPortId ?? North, charter?.Cargo),
                    PlayerOwnerId, "Ledger", 0, 100, priority: 10, isPlayer: true,
                    policy: MerchantPolicy(), automationEnabled: false)
            };
            var companies = new[]
            {
                new CompanyConfiguration("timber_company", 300),
                new CompanyConfiguration("plank_company", 400),
                new CompanyConfiguration("grain_company", 500),
                new CompanyConfiguration(PlayerOwnerId, charter?.Cash ?? InitialPlayerCash)
            };
            var blueprints = new[]
            {
                new IndustryBlueprint(SawmillBlueprint, "sawmill", "saw_planks", 280, 2, 40,
                    new[] { North, West, South }, 20),
                new IndustryBlueprint(BakeryBlueprint, "bakery", "bake_bread", 300, 2, 40,
                    new[] { North, West, East }, 30),
                new IndustryBlueprint(WorkshopBlueprint, "furniture_workshop", "make_furniture", 360, 3, 40,
                    new[] { South, East }, 15)
            };
            return new RegionalWorldState(content,
                reverseConfiguration ? ports.Reverse() : ports,
                reverseConfiguration ? routes.Reverse() : routes,
                reverseConfiguration ? ships.Reverse() : ships,
                companies: reverseConfiguration ? companies.Reverse() : companies,
                industryBlueprints: reverseConfiguration ? blueprints.Reverse() : blueprints,
                challenge: challenge, initialFacilities: charter?.Facilities,
                initialGrantProvenance: charter?.Id ?? "initial_inventory");
        }

        public static TradeRoutePolicy MerchantPolicy() =>
            new TradeRoutePolicy(North, East, new RouteLeg("bread", 10, 4, 32, 24, 280),
                cashReserve: 100, allowEmptyReturn: true);

        public static IndustryPolicy BakeryPolicy(bool sellOutput = false) =>
            new IndustryPolicy(true, sellOutput, 100,
                new[] { new InputStockRule("flour", 3, 24), new InputStockRule("logs", 3, 18) },
                new[] { new OutputSaleRule("bread", 0, 20) });

        public static IndustryPolicy WorkshopPolicy() =>
            new IndustryPolicy(true, true, 100,
                new[] { new InputStockRule("planks", 4, 24) },
                new[] { new OutputSaleRule("furniture", 0, 40) });

        public static IndustryPolicy DefaultIndustryPolicy(string blueprintId)
        {
            switch (blueprintId)
            {
                case BakeryBlueprint: return BakeryPolicy(true);
                case WorkshopBlueprint: return WorkshopPolicy();
                case SawmillBlueprint:
                    return new IndustryPolicy(true, true, 100,
                        new[] { new InputStockRule("logs", 4, 14) },
                        new[] { new OutputSaleRule("planks", 0, 18) });
                default: throw new KeyNotFoundException("Unknown enterprise blueprint: " + blueprintId);
            }
        }

        private static Dictionary<string, int> Stock(int logs, int planks, int grain, int flour, int bread, int furniture) =>
            new Dictionary<string, int>
            {
                ["logs"] = logs, ["planks"] = planks, ["grain"] = grain,
                ["flour"] = flour, ["bread"] = bread, ["furniture"] = furniture
            };

        private static MarketQuote[] Quotes(long logs, long planks, long grain, long flour, long bread, long furniture) =>
            new[]
            {
                Linear("logs", logs, 12, 6, 60), Linear("planks", planks, 6, 8, 70),
                Linear("grain", grain, 12, 4, 30), Linear("flour", flour, 6, 6, 50),
                Linear("bread", bread, 8, 12, 80), Linear("furniture", furniture, 4, 16, 120)
            };

        private static MarketQuote Linear(string good, long reference, int target, long sensitivity, long ceiling) =>
            new MarketQuote(good, new InventoryPricing(InventoryPricingModel.Linear,
                reference, target, sensitivity, 1, ceiling, 1));

        private static ScheduledFacilityConfiguration Facility(string id, string building, string recipe,
            int interval, int limit) =>
            new ScheduledFacilityConfiguration(new BuildingInstance(id, building, recipe), interval, limit);

        private static ConsumptionConfiguration Demand(string id, string good, int quantity, int interval) =>
            new ConsumptionConfiguration(id, good, quantity, interval);

        private static RegionalPortConfiguration Port(string id, string name, int x, int y, string role,
            long cash, Dictionary<string, int> stock, MarketQuote[] quotes,
            ScheduledFacilityConfiguration[] facilities, ConsumptionConfiguration[] demand) =>
            new RegionalPortConfiguration(new PortConfiguration(id, quotes, cash,
                facilities.Select(f => f.Instance), stock), name, x, y, facilities, demand, role);
    }
}
