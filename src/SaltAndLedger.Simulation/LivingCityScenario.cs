using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Opt-in finite household economy; all actions use the existing authoritative regional world.</summary>
    public static class LivingCityScenario
    {
        public static RegionalWorldState Create(ContentCatalog content, RunIdentity identity,
            bool reverseConfiguration = false)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (identity.WorldMode != RunWorldMode.LivingCities)
                throw new ArgumentException("A Living Cities identity is required.", nameof(identity));
            var generation = LivingCityGeneration.Generate(identity.Seed!.Value, identity.GeneratorVersion,
                identity.LivingPricingMode);
            var charter = GetCharter(generation, identity.CharterId);
            var food = generation.FoodSourcePortId;
            var processor = generation.ProcessingPortId;
            var furniture = generation.FurniturePortId;
            var ships = new[]
            {
                new RegionalShipConfiguration(new ShipConfiguration(ArchipelagoScenario.TimberShipId, 5, food),
                    "timber_company", "Timber Merchant", 0, 100, priority: 0,
                    policy: new TradeRoutePolicy(food, processor, new RouteLeg("logs", 5, 3, 18, 12, 90),
                        allowEmptyReturn: true), automationEnabled: true, minimumOriginStock: 10),
                new RegionalShipConfiguration(new ShipConfiguration(ArchipelagoScenario.PlankShipId, 3, processor),
                    "plank_company", "Plank Merchant", 0, 100, priority: 1,
                    policy: new TradeRoutePolicy(processor, furniture, new RouteLeg("planks", 3, 2, 26, 22, 78),
                        allowEmptyReturn: true), automationEnabled: true, minimumOriginStock: 4),
                new RegionalShipConfiguration(new ShipConfiguration(EnterpriseScenario.GrainShipId, 6, food),
                    "grain_company", "Grain & Flour Merchant", 0, 100, priority: 2,
                    policy: new TradeRoutePolicy(food, processor, new RouteLeg("grain", 6, 4, 14, 10, 84),
                        new RouteLeg("flour", 4, 1, 24, 12, 96), cashReserve: 20, allowEmptyReturn: true),
                    automationEnabled: true),
                new RegionalShipConfiguration(new ShipConfiguration(EnterpriseScenario.PlayerShipId,
                    charter.CargoCapacity, charter.StartingPortId, charter.Cargo), EnterpriseScenario.PlayerOwnerId,
                    "Ledger", 0, RunScenario.StartingShipSpeed, priority: 10, isPlayer: true,
                    policy: MerchantPolicy(generation), automationEnabled: false)
            };
            var companies = new[] { new CompanyConfiguration("timber_company", 300),
                new CompanyConfiguration("plank_company", 400), new CompanyConfiguration("grain_company", 500),
                new CompanyConfiguration(EnterpriseScenario.PlayerOwnerId, charter.Cash) };
            var challenge = identity.ChallengeId == null ? null : identity.ChallengeId == RunScenario.RecoveryChallenge
                ? RecoveryConfiguration(generation.RecoveryPortId) : RunScenario.GetChallenge(identity.ChallengeId).Configuration;
            return new RegionalWorldState(content,
                reverseConfiguration ? generation.Ports.Reverse() : generation.Ports,
                reverseConfiguration ? generation.Geography.Routes.Reverse() : generation.Geography.Routes,
                reverseConfiguration ? ships.Reverse() : ships,
                companies: reverseConfiguration ? companies.Reverse() : companies,
                industryBlueprints: reverseConfiguration ? generation.Blueprints.Reverse() : generation.Blueprints,
                challenge: challenge, initialFacilities: charter.Facilities, initialGrantProvenance: charter.Id,
                difficulty: identity.Difficulty, runIdentity: identity);
        }

        /// <summary>Only locations adapt to generated capabilities; charter quantities, cash and provenance are preserved.</summary>
        public static StartingCharter GetCharter(LivingCityGeneration generation, string charterId)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            var historical = RunScenario.GetCharter(charterId);
            var industrial = charterId == RunScenario.IndustrialCharter;
            var portId = industrial ? generation.ProcessingPortId : generation.FoodSourcePortId;
            var port = LivingCityGeneration.PortName(portId);
            var facilities = historical.Facilities.Select(facility => new PrivateFacilityConfiguration(
                facility.Id, facility.OwnerId, portId, facility.BlueprintId, facility.InitialInventory,
                facility.Policy, facility.Enabled, facility.Provenance)).ToArray();
            var description = industrial ? "A bakery at " + port + " and starter inputs give production an immediate foothold."
                : historical.Description;
            var endowment = industrial ? "300 cash; 8-unit ship and granted operational bakery at " + port +
                "; 2 Flour and 2 Logs. Grants have zero paid basis; completed batches pay real wages."
                : charterId == RunScenario.MerchantCharter ? "600 cash; 10-unit ship at " + port + "; no facility or cargo."
                : "480 cash; 12-unit ship at " + port + "; granted 4 Grain and 2 Logs with zero paid basis.";
            return new StartingCharter(historical.Id, historical.Name, description, historical.Tradeoff, endowment,
                historical.Cash, historical.CargoCapacity, portId,
                historical.Cargo.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal), facilities);
        }

        public static ChallengeConfiguration RecoveryConfiguration(string portId) => new ChallengeConfiguration(
            RunScenario.RecoveryChallenge, "A City Back on Its Feet", EnterpriseScenario.PlayerOwnerId,
            portId, "bread", 90, 90, 100, ChallengeKind.CityRecovery,
            recoveryMinimumProsperity: 55, recoveryWindowSize: 6);

        public static TradeRoutePolicy MerchantPolicy(LivingCityGeneration generation)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            return new TradeRoutePolicy(generation.FoodSourcePortId, generation.RecoveryPortId,
                new RouteLeg("bread", 10, 4, 34, 26, 300), cashReserve: 100, allowEmptyReturn: true);
        }

        public static IndustryPolicy BakeryPolicy(bool sellOutput = false) => new IndustryPolicy(true, sellOutput, 80,
            new[] { new InputStockRule("flour", 4, 28), new InputStockRule("logs", 4, 24) },
            new[] { new OutputSaleRule("bread", 0, 20) });

        public static IndustrialRoutePolicy IndustrialPolicy(LivingCityGeneration generation,
            string facilityId = RunScenario.StarterBakery, string? sourcePortId = null)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            return new IndustrialRoutePolicy(sourcePortId ?? generation.ProcessingPortId, generation.RecoveryPortId,
                new IndustrialRouteLeg("bread", 8, 2, IndustrialEndpointKind.Warehouse,
                    sourceFacilityId: facilityId, minimumSourceStock: 4, minimumSellPrice: 26),
                cashReserve: 80, allowEmptyReturn: true);
        }
    }
}
