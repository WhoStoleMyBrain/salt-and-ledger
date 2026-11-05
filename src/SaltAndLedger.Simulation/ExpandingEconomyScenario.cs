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
                identity.LivingPricingMode, identity.EconomicRulesVersion, identity.GenerationConfiguration, content);
            var charter = GetCharter(generation, identity.CharterId);
            var scopedContent = content.ForExpandingEconomy();
            var ships = new List<RegionalShipConfiguration>();
            var companies = new List<CompanyConfiguration>();
            AddMerchants(generation, scopedContent, ships, companies);
            ships.Add(new RegionalShipConfiguration(new ShipConfiguration(WorldIds.PlayerShipId,
                charter.CargoCapacity, charter.StartingPortId, charter.Cargo), WorldIds.PlayerOwnerId,
                "Ledger", 0, RunScenario.StartingShipSpeed, priority: 1000, isPlayer: true,
                policy: charter.StartingPortId == generation.FoodSourcePortId || charter.StartingPortId == generation.RecoveryPortId
                    ? MerchantPolicy(generation) : null));
            companies.Add(new CompanyConfiguration(WorldIds.PlayerOwnerId, charter.Cash));
            return new RegionalWorldState(content.ForExpandingEconomy(),
                reverseConfiguration ? generation.Ports.Reverse() : generation.Ports,
                reverseConfiguration ? generation.Geography.Routes.Reverse() : generation.Geography.Routes,
                reverseConfiguration ? ships.AsEnumerable().Reverse() : ships,
                companies: reverseConfiguration ? companies.AsEnumerable().Reverse() : companies,
                industryBlueprints: reverseConfiguration ? generation.Blueprints.Reverse() : generation.Blueprints,
                challenge: identity.Commission == null ? null :
                    GrandCommission.Create(generation.RecoveryPortId, identity.GenerationConfiguration.DeadlineDays),
                initialFacilities: charter.Facilities, initialGrantProvenance: charter.Id,
                difficulty: identity.Difficulty, runIdentity: identity,
                economy: ExpandingEconomyConfiguration.CreateDefault(generation.MintPortId, generation.SilverPortId,
                    generation.ShipyardPortId, generation.Configuration.FleetLimit, generation.Configuration.DeadlineDays,
                    generation.Configuration.EnabledGoods),
                circulation: circulation);
        }

        public static StartingCharter GetCharter(ExpandingEconomyGeneration generation, string charterId)
        {
            if (generation == null) throw new ArgumentNullException(nameof(generation));
            if (charterId != RunScenario.MerchantCharter && charterId != RunScenario.IndustrialCharter &&
                charterId != RunScenario.IndependentCharter) throw new ArgumentException("Unknown starting charter: " + charterId, nameof(charterId));
            var industrial = charterId == RunScenario.IndustrialCharter;
            var bakeryPorts = generation.EligiblePorts("bake_bread");
            if (industrial && bakeryPorts.Count == 0)
                throw new ArgumentException("The Industrial Consortium requires private Bakery eligibility. Enable bake_bread or select another charter.", nameof(generation));
            if (industrial && generation.Configuration.Institutions.StartingPortIndex.HasValue &&
                !bakeryPorts.Contains(generation.StartPortId, StringComparer.Ordinal))
                throw new ArgumentException("The configured starting town cannot host the Industrial Consortium's Bakery. Choose a Bakery-eligible starting town or another charter.", nameof(generation));
            var portId = industrial && !generation.Configuration.Institutions.StartingPortIndex.HasValue
                ? bakeryPorts[0] : generation.StartPortId;
            var capacity = industrial ? 8 : charterId == RunScenario.MerchantCharter ? 10 : 12;
            var name = industrial ? "Industrial Consortium" : charterId == RunScenario.MerchantCharter ? "Merchant Company" : "Independent Traders";
            var cargo = charterId == RunScenario.IndependentCharter
                ? new Dictionary<string, int> { ["grain"] = Q(4), ["logs"] = Q(2) }
                    .Where(pair => generation.Configuration.EnabledGoods.Contains(pair.Key, StringComparer.Ordinal))
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, int>();
            var facilities = industrial ? new[] { new PrivateFacilityConfiguration(RunScenario.StarterBakery,
                WorldIds.PlayerOwnerId, portId, generation.Blueprints.Single(blueprint => blueprint.RecipeId == "bake_bread").Id,
                new Dictionary<string, int> { ["flour"] = Q(2), ["logs"] = Q(2) }, provenance: charterId) }
                : Array.Empty<PrivateFacilityConfiguration>();
            var cash = charterId == RunScenario.MerchantCharter ? 1800 : industrial ? 1200 : 1500;
            var description = industrial ? "An operational bakery and real inputs at an eligible harbor, with capital to expand the chain."
                : charterId == RunScenario.MerchantCharter ? "Liquid investment capital and an empty hold for commerce, production and fleet choices."
                : "A larger hold and enabled raw cargo support a flexible industrial opening.";
            var tradeoff = industrial ? "Less liquid capital and a smaller hold; the bakery needs materials, wages and customers."
                : charterId == RunScenario.MerchantCharter ? "No granted industry or materials; each investment competes with shipment cash."
                : "Less cash than the merchant; granted goods occupy hold space.";
            var endowment = cash + " coins; " + capacity + "-unit ship at " + generation.Ports.Single(port => port.Id == portId).Name +
                (industrial ? "; granted operational bakery with 2 Flour and 2 Logs." :
                charterId == RunScenario.IndependentCharter ? "; granted " +
                    (cargo.Count == 0 ? "no enabled raw cargo" : string.Join(" and ", cargo.Select(pair =>
                        (pair.Value / 10000).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + pair.Key))) + "." : "; no facility or cargo.") +
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
            string facilityId = RunScenario.StarterBakery, string? sourcePortId = null)
        {
            var source = sourcePortId ?? GetCharter(generation, RunScenario.IndustrialCharter).StartingPortId;
            var destination = generation.RecoveryPortId != source ? generation.RecoveryPortId :
                generation.Ports.First(port => port.Id != source).Id;
            return new IndustrialRoutePolicy(source, destination,
                new IndustrialRouteLeg("bread", Q(8), Q(2), IndustrialEndpointKind.Warehouse,
                    sourceFacilityId: facilityId, minimumSourceStock: Q(4), minimumSellPrice: 26),
                cashReserve: M(80), allowEmptyReturn: true);
        }

        /// <summary>Finite merchants connect actual producers with local industrial or household demand.</summary>
        private static void AddMerchants(ExpandingEconomyGeneration generation, ContentCatalog content,
            List<RegionalShipConfiguration> ships, List<CompanyConfiguration> companies)
        {
            var settings = generation.Configuration.Npc;
            var count = Math.Max(settings.MinimumCount, Math.Min(settings.MaximumCount,
                checked((generation.Ports.Count * settings.TradersPerTownPermille + 999) / 1000)));
            if (count == 0) return;
            var candidates = new List<MerchantConnection>();
            var travelDays = new Dictionary<string, int>(StringComparer.Ordinal);
            int Duration(string origin, string destination)
            {
                var key = string.CompareOrdinal(origin, destination) < 0 ? origin + "/" + destination : destination + "/" + origin;
                if (!travelDays.TryGetValue(key, out var days))
                {
                    days = SeaNavigation.FindPath(generation.Geography.Routes, origin, destination, settings.Speed).DurationDays;
                    travelDays.Add(key, days);
                }
                return days;
            }
            foreach (var origin in generation.Ports)
            {
                var outputs = origin.Facilities.SelectMany(facility => content.GetRecipe(facility.Instance.RecipeId).Outputs)
                    .Select(output => output.GoodId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
                foreach (var good in outputs)
                    foreach (var destination in generation.Ports.Where(port => port.Id != origin.Id))
                    {
                        var industrialDemand = destination.Facilities.Any(facility =>
                            content.GetRecipe(facility.Instance.RecipeId).Inputs.Any(input => input.GoodId == good));
                        var householdDemand = destination.Consumption.Any(need => need.GoodId == good);
                        if (industrialDemand || householdDemand)
                            candidates.Add(new MerchantConnection(origin.Id, destination.Id, good,
                                Duration(origin.Id, destination.Id)));
                    }
            }
            // Private-only supply worlds still offer honest finite trade from potential production sites.
            if (candidates.Count == 0)
                foreach (var blueprint in generation.Blueprints.OrderBy(value => value.Id, StringComparer.Ordinal))
                    foreach (var origin in blueprint.EligiblePortIds)
                        foreach (var output in content.GetRecipe(blueprint.RecipeId).Outputs)
                            foreach (var destination in generation.Ports.Where(port => port.Id != origin &&
                                (port.Consumption.Any(need => need.GoodId == output.GoodId) ||
                                generation.Blueprints.Any(other => other.EligiblePortIds.Contains(port.Id) &&
                                    content.GetRecipe(other.RecipeId).Inputs.Any(input => input.GoodId == output.GoodId)))))
                                candidates.Add(new MerchantConnection(origin, destination.Id, output.GoodId,
                                    Duration(origin, destination.Id)));
            if (candidates.Count == 0)
                throw new ArgumentException("NPC trader count is positive but no enabled producer-to-consumer route exists. Enable complementary industries or set NPC minimum/count density to zero.", nameof(generation));
            // Round-robin goods prevents every merchant selecting the same short food route.
            var groups = candidates.GroupBy(candidate => candidate.GoodId).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.OrderBy(candidate => candidate.DurationDays)
                    .ThenBy(candidate => StableDraw(generation.Seed, candidate.Origin + "/" + candidate.Destination + "/" + candidate.GoodId))
                    .ThenBy(candidate => candidate.Origin, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Destination, StringComparer.Ordinal).ToArray()).ToArray();
            for (var index = 0; index < count; index++)
            {
                var group = groups[index % groups.Length];
                var route = group[(index / groups.Length) % group.Length];
                var capacity = settings.CargoCapacity.Minimum + (int)(StableDraw(generation.Seed, "merchant-capacity:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)) %
                    (uint)(settings.CargoCapacity.Maximum - settings.CargoCapacity.Minimum + 1));
                var cash = settings.StartingCash.Minimum + (int)(StableDraw(generation.Seed, "merchant-cash:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)) %
                    (uint)(settings.StartingCash.Maximum - settings.StartingCash.Minimum + 1));
                var suffix = (index + 1).ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
                var owner = "merchant_company_" + suffix;
                var returnRoute = candidates.Where(candidate => candidate.Origin == route.Destination &&
                    candidate.Destination == route.Origin && candidate.GoodId != route.GoodId)
                    .OrderBy(candidate => candidate.GoodId, StringComparer.Ordinal).FirstOrDefault();
                RouteLeg MerchantLeg(string good, string source)
                {
                    var quote = generation.Ports.Single(port => port.Id == source).Market.Quotes[good];
                    var reference = quote.Pricing?.ReferencePrice ?? quote.Ask;
                    return new RouteLeg(good, Q(capacity), Q(1), checked(reference * 2),
                        Math.Max(1, reference / 2));
                }
                var policy = new TradeRoutePolicy(route.Origin, route.Destination, MerchantLeg(route.GoodId, route.Origin),
                    returnRoute == null ? null : MerchantLeg(returnRoute.GoodId, returnRoute.Origin), allowEmptyReturn: true);
                ships.Add(new RegionalShipConfiguration(new ShipConfiguration("merchant_" + suffix, Q(capacity), route.Origin),
                    owner, content.GetGood(route.GoodId).Name + " Merchant " + suffix, 0, settings.Speed,
                    priority: index, policy: policy, automationEnabled: true));
                companies.Add(new CompanyConfiguration(owner, M(cash)));
            }
        }

        private sealed class MerchantConnection
        {
            internal string Origin { get; }
            internal string Destination { get; }
            internal string GoodId { get; }
            internal int DurationDays { get; }
            internal MerchantConnection(string origin, string destination, string goodId, int durationDays)
            { Origin = origin; Destination = destination; GoodId = goodId; DurationDays = durationDays; }
        }

        private static uint StableDraw(int seed, string purpose)
        {
            unchecked
            {
                var value = (uint)seed ^ 2166136261u;
                foreach (var character in purpose) value = (value ^ character) * 16777619u;
                value = (value ^ (value >> 16)) * 0x85EBCA6Bu;
                value = (value ^ (value >> 13)) * 0xC2B2AE35u;
                return value ^ (value >> 16);
            }
        }

        private static RouteLeg Leg(string good, int max, int min, long buy, long sell, long cap) =>
            new RouteLeg(good, Q(max), Q(min), buy, sell, M(cap));
        private static int Q(int quantity) => EconomicUnits.FromWhole(quantity);
        private static long M(long coins) => EconomicUnits.Coins(coins);
    }
}
