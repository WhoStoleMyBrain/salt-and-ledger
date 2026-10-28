using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Explicit v3 opt-in, using the shared atomic regional owner and canonical scoped content.</summary>
    public static class ExpandingEconomyScenario
    {
        public static RegionalWorldState Create(ContentCatalog content, RunIdentity identity,
            bool reverseConfiguration = false, CirculationConfiguration? circulation = null)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (identity.WorldMode != RunWorldMode.ExpandingEconomy)
                throw new ArgumentException("An Expanding Economy identity is required.", nameof(identity));
            if (circulation != null && identity.EconomicRulesVersion != circulation.Version)
                throw new ArgumentException("Diagnostic circulation must match the run's economic rules version.", nameof(circulation));
            if (identity.EconomicRulesVersion == 1 && circulation == null)
                circulation = new CirculationConfiguration();
            var generation = ExpandingEconomyGeneration.Generate(identity.Seed!.Value, identity.GeneratorVersion,
                identity.LivingPricingMode, identity.EconomicRulesVersion);
            var charter = GetCharter(generation, identity.CharterId);
            var food = generation.FoodSourcePortId;
            var processor = generation.ProcessingPortId;
            var ships = new[]
            {
                new RegionalShipConfiguration(new ShipConfiguration(WorldIds.TimberShipId, Q(5), food),
                    "timber_company", "Timber Merchant", 0, 100, priority: 0,
                    policy: new TradeRoutePolicy(food, processor, Leg("logs", 5, 3, 18, 12, 90),
                        allowEmptyReturn: true), automationEnabled: true, minimumOriginStock: Q(10)),
                new RegionalShipConfiguration(new ShipConfiguration(WorldIds.PlankShipId, Q(3), processor),
                    "plank_company", "Plank Merchant", 0, 100, priority: 1,
                    policy: new TradeRoutePolicy(processor, generation.FurniturePortId, Leg("planks", 3, 2, 26, 22, 78),
                        allowEmptyReturn: true), automationEnabled: true, minimumOriginStock: Q(4)),
                new RegionalShipConfiguration(new ShipConfiguration(WorldIds.GrainShipId, Q(6), food),
                    "grain_company", "Grain & Flour Merchant", 0, 100, priority: 2,
                    policy: new TradeRoutePolicy(food, processor, Leg("grain", 6, 4, 14, 10, 84),
                        Leg("flour", 4, 1, 24, 12, 96), cashReserve: M(20), allowEmptyReturn: true), automationEnabled: true),
                new RegionalShipConfiguration(new ShipConfiguration(WorldIds.PlayerShipId,
                    charter.CargoCapacity, charter.StartingPortId, charter.Cargo), WorldIds.PlayerOwnerId,
                    "Ledger", 0, RunScenario.StartingShipSpeed, priority: 10, isPlayer: true,
                    policy: MerchantPolicy(generation), automationEnabled: false)
            };
            var companies = new[] { new CompanyConfiguration("timber_company", M(300)),
                new CompanyConfiguration("plank_company", M(400)), new CompanyConfiguration("grain_company", M(500)),
                new CompanyConfiguration(WorldIds.PlayerOwnerId, charter.Cash) };
            return new RegionalWorldState(content.ForExpandingEconomy(),
                reverseConfiguration ? generation.Ports.Reverse() : generation.Ports,
                reverseConfiguration ? generation.Geography.Routes.Reverse() : generation.Geography.Routes,
                reverseConfiguration ? ships.Reverse() : ships,
                companies: reverseConfiguration ? companies.Reverse() : companies,
                industryBlueprints: reverseConfiguration ? generation.Blueprints.Reverse() : generation.Blueprints,
                challenge: identity.Commission == null ? null :
                    GrandCommission.Definition.Configuration,
                initialFacilities: charter.Facilities, initialGrantProvenance: charter.Id,
                difficulty: identity.Difficulty, runIdentity: identity,
                economy: ExpandingEconomyConfiguration.CreateDefault(generation.MintPortId, generation.SilverPortId),
                circulation: circulation);
        }

        public static StartingCharter GetCharter(ExpandingEconomyGeneration generation, string charterId)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (charterId != RunScenario.MerchantCharter && charterId != RunScenario.IndustrialCharter &&
                charterId != RunScenario.IndependentCharter) throw new ArgumentException("Unknown starting charter: " + charterId, nameof(charterId));
            var industrial = charterId == RunScenario.IndustrialCharter;
            var portId = industrial ? generation.ProcessingPortId : generation.FoodSourcePortId;
            var capacity = industrial ? 8 : charterId == RunScenario.MerchantCharter ? 10 : 12;
            var name = industrial ? "Industrial Consortium" : charterId == RunScenario.MerchantCharter ? "Merchant Company" : "Independent Traders";
            var cargo = charterId == RunScenario.IndependentCharter
                ? new Dictionary<string, int> { ["grain"] = Q(4), ["logs"] = Q(2) } : new Dictionary<string, int>();
            var facilities = industrial ? new[] { new PrivateFacilityConfiguration(RunScenario.StarterBakery,
                WorldIds.PlayerOwnerId, portId, WorldIds.BakeryBlueprint,
                new Dictionary<string, int> { ["flour"] = Q(2), ["logs"] = Q(2) }, provenance: charterId) }
                : Array.Empty<PrivateFacilityConfiguration>();
            var cash = charterId == RunScenario.MerchantCharter ? 1800 : industrial ? 1200 : 1500;
            var description = industrial ? "An operational bakery and real inputs at the processing harbor, with capital to expand the chain."
                : charterId == RunScenario.MerchantCharter ? "Liquid investment capital and an empty hold for commerce, production and fleet choices."
                : "A larger hold and granted raw cargo support a flexible industrial opening.";
            var tradeoff = industrial ? "Less liquid capital and a smaller hold; the bakery needs materials, wages and customers."
                : charterId == RunScenario.MerchantCharter ? "No granted industry or materials; each investment competes with shipment cash."
                : "Less cash than the merchant; the starter goods occupy half the hold.";
            var endowment = cash + " coins; " + capacity + "-unit ship at " + LivingCityGeneration.PortName(portId) +
                (industrial ? "; granted operational bakery with 2 Flour and 2 Logs." :
                charterId == RunScenario.IndependentCharter ? "; granted 4 Grain and 2 Logs." : "; no facility or cargo.") +
                " Grants have zero paid basis and disclosed positive reference value.";
            return new StartingCharter(charterId, name, description, tradeoff, endowment,
                M(cash), Q(capacity), portId,
                cargo, facilities);
        }

        public static TradeRoutePolicy MerchantPolicy(ExpandingEconomyGeneration generation, bool balanced = false) =>
            new TradeRoutePolicy(generation.FoodSourcePortId, generation.RecoveryPortId,
                Leg("bread", 10, 4, 34, 26, 300), balanced ? Leg("logs", 8, 2, 18, 1, 144) : null,
                cashReserve: M(100), allowEmptyReturn: true);

        public static WarehousePolicy BakeryWarehousePolicy(bool sellOutput = false) => new WarehousePolicy(M(80),
            new[] { new WarehouseCommodityRule("flour", true, Q(4), 28, false, 0, 0),
                new WarehouseCommodityRule("logs", true, Q(4), 24, false, 0, 0),
                new WarehouseCommodityRule("bread", false, 0, 0, sellOutput, 0, 20) });

        public static WarehousePolicy MillWarehousePolicy(bool sellOutput = true) => new WarehousePolicy(M(80),
            new[] { new WarehouseCommodityRule("grain", true, Q(8), 24, false, 0, 0),
                new WarehouseCommodityRule("flour", false, 0, 0, sellOutput, 0, 6) });

        public static IndustrialRoutePolicy IndustrialPolicy(ExpandingEconomyGeneration generation,
            string facilityId = RunScenario.StarterBakery, string? sourcePortId = null) =>
            new IndustrialRoutePolicy(sourcePortId ?? generation.ProcessingPortId, generation.RecoveryPortId,
                new IndustrialRouteLeg("bread", Q(8), Q(2), IndustrialEndpointKind.Warehouse,
                    sourceFacilityId: facilityId, minimumSourceStock: Q(4), minimumSellPrice: 26),
                cashReserve: M(80), allowEmptyReturn: true);

        private static RouteLeg Leg(string good, int max, int min, long buy, long sell, long cap) =>
            new RouteLeg(good, Q(max), Q(min), buy, sell, M(cap));
        private static int Q(int quantity) => EconomicUnits.FromWhole(quantity);
        private static long M(long coins) => EconomicUnits.Coins(coins);
    }
}
