using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>The sole player-world generator. Distribution is constrained against canonical recipe dependencies.</summary>
    public sealed class ExpandingEconomyGeneration
    {
        public const int CurrentVersion = 4;
        public const string FarmBlueprint = "private_farm";
        public const string LumberBlueprint = "private_lumber_camp";
        public const string MillBlueprint = "private_mill";
        public const string SilverMineBlueprint = "private_silver_mine";
        public int Seed { get; }
        public int GeneratorVersion => CurrentVersion;
        public int EconomicRulesVersion { get; }
        public bool ShouldSerializeEconomicRulesVersion() => EconomicRulesVersion != 0;
        public LivingPricingMode PricingMode { get; }
        public WorldGenerationConfiguration Configuration { get; }
        public ArchipelagoGeography Geography { get; }
        public LivingCityGeneration SourceRoles { get; }
        public string FoodSourcePortId { get; }
        public string ProcessingPortId { get; }
        public string FurniturePortId { get; }
        public string RecoveryPortId { get; }
        public string StartPortId { get; }
        public string SilverPortId { get; }
        public string MintPortId { get; }
        public string ShipyardPortId { get; }
        public IReadOnlyList<RegionalPortConfiguration> Ports { get; }
        public IReadOnlyList<IndustryBlueprint> Blueprints { get; }
        public WorldGenerationQuality Quality { get; }
        public string Signature { get; }

        private ExpandingEconomyGeneration(int seed, LivingPricingMode pricingMode, int economicRulesVersion,
            WorldGenerationConfiguration configuration, ContentCatalog content)
        {
            Seed = seed; PricingMode = pricingMode; EconomicRulesVersion = economicRulesVersion; Configuration = configuration;
            Geography = SeededArchipelago.Generate(seed, configuration: configuration, content: content);
            var ids = Geography.Positions.Keys.ToArray();
            var assignment = AssignIndustries(seed, configuration, content, Geography, out var attempted, out var failures);
            var blueprints = new List<IndustryBlueprint>();
            var publicFacilities = ids.ToDictionary(id => id, id => new List<ScheduledFacilityConfiguration>(), StringComparer.Ordinal);
            foreach (var item in assignment.OrderBy(x => x.Distribution.IndustryId, StringComparer.Ordinal))
            {
                var recipes = content.Recipes.Where(x => x.BuildingId == item.Distribution.IndustryId).OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
                var economics = GenerationEconomics.Industry(item.Distribution.IndustryId);
                foreach (var recipe in recipes)
                {
                    var blueprintId = (recipe.BuildingId == "furniture_workshop" ? WorldIds.WorkshopBlueprint : "private_" + recipe.BuildingId) + (recipes.Length == 1 ? "" : "_" + recipe.Id);
                    if (item.PrivatePorts.Count != 0)
                        blueprints.Add(new IndustryBlueprint(blueprintId, recipe.BuildingId, recipe.Id, M(economics.Cost),
                            economics.Interval, Q(economics.Capacity), item.PrivatePorts, Q(economics.OutputLimit), M(economics.Wages)));
                    foreach (var id in item.PublicPorts)
                        publicFacilities[id].Add(new ScheduledFacilityConfiguration(new BuildingInstance(id + "_" + recipe.Id,
                            recipe.BuildingId, recipe.Id), economics.Interval, Q(economics.OutputLimit), wagePerBatch: M(economics.PublicWages)));
                }
            }
            Blueprints = Array.AsReadOnly(blueprints.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray());
            string[] Produces(string good) => assignment.Where(x => content.Recipes.Any(r => r.BuildingId == x.Distribution.IndustryId &&
                r.Outputs.Any(o => o.GoodId == good))).SelectMany(x => x.AllPorts).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var silver = Produces(ExpandingEconomyConfiguration.MintInputGoodId);
            SilverPortId = configuration.Institutions.SilverPortIndex.HasValue ? ids[configuration.Institutions.SilverPortIndex.Value] :
                silver.FirstOrDefault() ?? ids[0];
            MintPortId = configuration.Institutions.MintPortIndex.HasValue ? ids[configuration.Institutions.MintPortIndex.Value] :
                ids.Where(id => id != SilverPortId && !silver.Contains(id)).OrderByDescending(id => Distance(Geography.Positions[id], Geography.Positions[SilverPortId]))
                    .ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault() ?? throw new WorldGenerationException("The Mint needs a town without local Silver production. Reduce Silver Mine availability or choose more towns.");
            if (MintPortId == SilverPortId || (configuration.EnforceProductionSeparation && silver.Contains(MintPortId)))
                throw new WorldGenerationException("Configured Mint town can produce Silver locally; choose a different mintPortIndex or Silver allocation.");
            if (silver.Length != 0 && !silver.Contains(SilverPortId))
                throw new WorldGenerationException("Configured silverPortIndex is not a Silver-producing town.");
            var shipMaterialSources = Produces(ExpandingEconomyConfiguration.ShipMaterialGoodId);
            ShipyardPortId = configuration.Institutions.ShipyardPortIndex.HasValue ? ids[configuration.Institutions.ShipyardPortIndex.Value] :
                !configuration.EnforceProductionSeparation || !shipMaterialSources.Contains(MintPortId) ? MintPortId :
                ids.Where(id => !shipMaterialSources.Contains(id)).OrderBy(id => Distance(Geography.Positions[id], Geography.Positions[MintPortId]))
                    .ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault() ?? throw new WorldGenerationException("The shipyard needs a town without local production of its hull material. Reduce material-industry availability or choose more towns.");
            if (configuration.EnforceProductionSeparation && shipMaterialSources.Contains(ShipyardPortId))
                throw new WorldGenerationException("Configured shipyard town can produce its hull material locally; choose a different shipyardPortIndex or industry allocation.");
            FoodSourcePortId = FirstEligibleOrPublic("bake_bread", publicFacilities) ?? ids[0];
            ProcessingPortId = FirstEligibleOrPublic("mill_flour", publicFacilities) ?? ids.First(id => id != FoodSourcePortId);
            FurniturePortId = FirstEligibleOrPublic("make_furniture", publicFacilities) ?? ids.First(id => id != FoodSourcePortId);
            RecoveryPortId = ids.Where(id => id != FoodSourcePortId && !Produces("bread").Contains(id))
                .OrderByDescending(id => Distance(Geography.Positions[id], Geography.Positions[FoodSourcePortId]))
                .ThenBy(id => id, StringComparer.Ordinal).FirstOrDefault() ?? ids.First(id => id != FoodSourcePortId);
            StartPortId = configuration.Institutions.StartingPortIndex.HasValue ? ids[configuration.Institutions.StartingPortIndex.Value] : FoodSourcePortId;
            var sequence = new GenerationSequence(seed, 0x1273BE5Fu);
            var ports = new List<RegionalPortConfiguration>();
            foreach (var id in ids)
            {
                var position = Geography.Positions[id];
                var population = configuration.Population.Draw(ref sequence);
                var prosperity = configuration.Prosperity.Draw(ref sequence);
                var marketCash = configuration.MarketCash.Draw(ref sequence);
                var householdCash = checked((long)population * configuration.HouseholdCashPerResident.Draw(ref sequence));
                var stock = configuration.EnabledGoods.ToDictionary(good => good, good => 0, StringComparer.Ordinal);
                foreach (var pair in configuration.StartingInventories) stock[pair.Key] = Q(pair.Value.Draw(ref sequence));
                var capabilities = assignment.Where(x => x.AllPorts.Contains(id)).SelectMany(x => content.Recipes.Where(r => r.BuildingId == x.Distribution.IndustryId)).ToArray();
                var quotes = configuration.EnabledGoods.Select(good =>
                {
                    var price = GenerationEconomics.Good(good);
                    var producer = capabilities.Any(r => r.Outputs.Any(o => o.GoodId == good));
                    var consumer = capabilities.Any(r => r.Inputs.Any(i => i.GoodId == good));
                    var reference = Math.Max(1, price.Reference * (producer ? 80 : consumer ? 120 : 100) / 100);
                    return new MarketQuote(good, new InventoryPricing(InventoryPricingModel.Linear, reference,
                        Q(price.Target), price.Sensitivity, 1, price.Maximum, 1));
                }).ToArray();
                var needs = new List<ConsumptionConfiguration>
                {
                    new ConsumptionConfiguration(id + "_food", "bread", Q(1), 4,
                        kind: ConsumptionKind.Essential, populationPerUnit: 20, proportional: true)
                };
                if (configuration.EnabledGoods.Contains("furniture")) needs.Add(new ConsumptionConfiguration(id + "_durables", "furniture", Q(1), 24,
                    kind: ConsumptionKind.Luxury, populationPerUnit: 80, proportional: true));
                if (configuration.EnabledGoods.Contains("planks")) needs.Add(new ConsumptionConfiguration(id + "_municipal", "planks", Q(1), 12,
                    kind: ConsumptionKind.Municipal, populationPerUnit: 40));
                var role = "Production: " + (capabilities.Length == 0 ? "importing harbor" : string.Join(", ", capabilities.Select(r => content.GetBuilding(r.BuildingId).Name).Distinct())) +
                    (id == MintPortId ? "; public Royal Mint" : "") + (id == ShipyardPortId ? "; shipyard" : "");
                ports.Add(new RegionalPortConfiguration(new PortConfiguration(id, quotes, M(marketCash),
                    publicFacilities[id].Select(f => f.Instance), stock), LivingCityGeneration.PortName(id), position.X, position.Y,
                    publicFacilities[id], needs, role, new SettlementConfiguration(population, M(householdCash), prosperity,
                        minimumPopulation: 10, maximumPopulation: Math.Max(80, checked(configuration.Population.Max * 2)),
                        demographicIntervalDays: 12, pricingMode: pricingMode, quantityScale: EconomicUnits.QuantityScale)));
            }
            Ports = Array.AsReadOnly(ports.ToArray());
            Quality = new WorldGenerationQuality(this, content, attempted, failures);
            Signature = CreateSignature(content);
            SourceRoles = new LivingCityGeneration(this);
        }

        public static ExpandingEconomyGeneration Generate(int seed, int version = CurrentVersion,
            LivingPricingMode pricingMode = LivingPricingMode.StockOnly, int economicRulesVersion = 0,
            WorldGenerationConfiguration? configuration = null, ContentCatalog? content = null)
        {
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version), "Unsupported world generator version; only version 4 is supported.");
            if (!Enum.IsDefined(typeof(LivingPricingMode), pricingMode)) throw new ArgumentOutOfRangeException(nameof(pricingMode));
            if (economicRulesVersion != 0 && economicRulesVersion != 1) throw new ArgumentOutOfRangeException(nameof(economicRulesVersion));
            configuration = configuration ?? WorldGenerationConfiguration.Default;
            content = content ?? GenerationContent.Catalog;
            if (content.HasExpandingEconomy) content = content.ForExpandingEconomy();
            configuration.Validate(content);
            return new ExpandingEconomyGeneration(seed, pricingMode, economicRulesVersion, configuration, content);
        }

        public IReadOnlyList<string> PublicPorts(string recipeId) => Array.AsReadOnly(Ports.Where(p => p.Facilities.Any(f => f.Instance.RecipeId == recipeId)).Select(p => p.Id).ToArray());
        public IReadOnlyList<string> EligiblePorts(string recipeId) => Array.AsReadOnly(Blueprints.Where(b => b.RecipeId == recipeId).SelectMany(b => b.EligiblePortIds).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray());
        public string FirstEligiblePort(string recipeId) => EligiblePorts(recipeId).FirstOrDefault()
            ?? throw new WorldGenerationException("No private construction town is enabled for recipe " + recipeId + ".");
        private string? FirstEligibleOrPublic(string recipeId, Dictionary<string, List<ScheduledFacilityConfiguration>> facilities) =>
            EligiblePorts(recipeId).FirstOrDefault() ?? facilities.Where(p => p.Value.Any(f => f.Instance.RecipeId == recipeId)).Select(p => p.Key).FirstOrDefault();
        private string CreateSignature(ContentCatalog content)
        {
            var records = new List<string> { "expanding-v4", "content3", "value1", "quantity10000", "money10000", Configuration.Fingerprint,
                Seed.ToString(CultureInfo.InvariantCulture), PricingMode.ToString(), "economy" + EconomicRulesVersion.ToString(CultureInfo.InvariantCulture), Geography.Signature,
                "institutions:" + StartPortId + "," + MintPortId + "," + SilverPortId + "," + ShipyardPortId };
            foreach (var recipe in content.Recipes.OrderBy(r => r.Id, StringComparer.Ordinal))
                records.Add("recipe:" + recipe.Id + ":" + recipe.BuildingId + ":" + recipe.Kind + ":" +
                    string.Join(",", recipe.Inputs.OrderBy(i => i.GoodId, StringComparer.Ordinal).Select(i => i.GoodId + "=" + i.Quantity.ToString(CultureInfo.InvariantCulture))) + ":" +
                    string.Join(",", recipe.Outputs.OrderBy(o => o.GoodId, StringComparer.Ordinal).Select(o => o.GoodId + "=" + o.Quantity.ToString(CultureInfo.InvariantCulture))));
            foreach (var port in Ports)
            {
                records.Add(FormattableString.Invariant($"port:{port.Id}:{port.Name}:{port.Market.InitialMarketCash}:{port.Settlement!.InitialPopulation}:{port.Settlement.InitialHouseholdCash}:{port.Settlement.InitialProsperity}"));
                records.Add("stock:" + string.Join(",", port.Market.InitialQuantities.Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture))));
                foreach (var facility in port.Facilities) records.Add(FormattableString.Invariant($"public:{port.Id}:{facility.Instance.RecipeId}:{facility.IntervalDays}:{facility.OutputStockLimit}:{facility.WagePerBatch}"));
                foreach (var quote in port.Market.Quotes.Values) records.Add(FormattableString.Invariant($"quote:{port.Id}:{quote.GoodId}:{quote.Pricing!.ReferencePrice}:{quote.Pricing.TargetStock}:{quote.Pricing.Sensitivity}:{quote.Pricing.MaxPrice}"));
            }
            foreach (var blueprint in Blueprints) records.Add(FormattableString.Invariant($"private:{blueprint.Id}:{blueprint.RecipeId}:{blueprint.ConstructionCost}:{blueprint.IntervalDays}:{blueprint.WarehouseCapacity}:{blueprint.OutputStockLimit}:{blueprint.WagePerBatch}:") + string.Join(",", blueprint.EligiblePortIds));
            return string.Join("|", records);
        }

        private static List<Assignment> AssignIndustries(int seed, WorldGenerationConfiguration configuration, ContentCatalog content,
            ArchipelagoGeography geography, out int attempted, out IReadOnlyList<string> failures)
        {
            var errors = new List<string>(); var ids = geography.Positions.Keys.ToArray();
            var distributions = configuration.Industries.ToArray();
            var recipes = distributions.Select(d => content.Recipes.Where(r => r.BuildingId == d.IndustryId).ToArray()).ToArray();
            var conflicts = new bool[distributions.Length, distributions.Length];
            for (var a = 0; a < distributions.Length; a++)
                for (var b = 0; b < distributions.Length; b++)
                    conflicts[a, b] = a != b && configuration.EnforceProductionSeparation && recipes[a].Any(first => recipes[b].Any(second => ProductionSpecialization.AreIncompatible(first, second)));
            for (var candidate = 0; candidate < configuration.EconomicCandidateCount; candidate++)
            {
                var random = new GenerationSequence(seed, unchecked(0xAF1279C1u + (uint)candidate * 0x9E3779B9u));
                var publicCounts = distributions.Select(d => d.PublicTowns.Min + random.Next(Math.Min(d.PublicTowns.Max, ids.Length) - d.PublicTowns.Min + 1)).ToArray();
                var privateCounts = distributions.Select(d => d.PrivateTowns.Min + random.Next(Math.Min(d.PrivateTowns.Max, ids.Length) - d.PrivateTowns.Min + 1)).ToArray();
                var counts = publicCounts.Select((x, i) => Math.Max(x, privateCounts[i])).ToArray();
                for (var i = 0; i < counts.Length; i++)
                    if (counts[i] == 0)
                    {
                        counts[i] = 1;
                        if (distributions[i].PublicTowns.Max > 0) publicCounts[i] = 1; else privateCounts[i] = 1;
                    }
                var order = Enumerable.Range(0, distributions.Length).OrderByDescending(i => Enumerable.Range(0, distributions.Length).Count(j => conflicts[i, j]))
                    .ThenByDescending(i => counts[i]).ThenBy(i => distributions[i].IndustryId, StringComparer.Ordinal).ToArray();
                var ranks = new int[distributions.Length][];
                for (var i = 0; i < distributions.Length; i++)
                {
                    var anchorX = random.Next(configuration.Geography.Width + 1); var anchorY = random.Next(configuration.Geography.Height + 1);
                    var jitter = ids.Select(id => random.Next(20001)).ToArray();
                    ranks[i] = Enumerable.Range(0, ids.Length).OrderBy(p =>
                    {
                        var point = geography.Positions[ids[p]];
                        var dx = (long)point.X - anchorX; var dy = (long)point.Y - anchorY;
                        return dx * dx + dy * dy + jitter[p];
                    }).ThenBy(p => p).ToArray();
                }
                var selected = Enumerable.Range(0, distributions.Length).Select(i => new List<int>()).ToArray();
                var occupied = Enumerable.Range(0, ids.Length).Select(i => new List<int>()).ToArray();
                var nodes = 0;
                bool ValidPort(int industry, int port)
                {
                    if (occupied[port].Any(other => conflicts[industry, other])) return false;
                    var producesSilver = recipes[industry].Any(r => r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.MintInputGoodId));
                    if (producesSilver && configuration.Institutions.MintPortIndex == port) return false;
                    if (configuration.EnforceProductionSeparation && configuration.Institutions.ShipyardPortIndex == port &&
                        recipes[industry].Any(r => r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.ShipMaterialGoodId))) return false;
                    return true;
                }
                bool Search(int orderIndex, int rankStart)
                {
                    if (++nodes > 50000) return false;
                    if (orderIndex == order.Length) return true;
                    var industry = order[orderIndex];
                    if (selected[industry].Count == counts[industry]) return Search(orderIndex + 1, 0);
                    var eligible = ranks[industry].Skip(rankStart).Where(p => ValidPort(industry, p)).ToArray();
                    if (eligible.Length < counts[industry] - selected[industry].Count) return false;
                    for (var rank = rankStart; rank < ranks[industry].Length; rank++)
                    {
                        var port = ranks[industry][rank];
                        if (!ValidPort(industry, port)) continue;
                        selected[industry].Add(port); occupied[port].Add(industry);
                        if (Search(orderIndex, rank + 1)) return true;
                        selected[industry].RemoveAt(selected[industry].Count - 1); occupied[port].Remove(industry);
                    }
                    return false;
                }
                // Explicit Silver placement is a capability constraint, never a post-generation relocation.
                if (configuration.Institutions.SilverPortIndex.HasValue)
                {
                    var silverIndustry = Array.FindIndex(recipes, rs => rs.Any(r => r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.MintInputGoodId)));
                    if (silverIndustry >= 0)
                    {
                        var port = configuration.Institutions.SilverPortIndex.Value;
                        selected[silverIndustry].Add(port); occupied[port].Add(silverIndustry);
                        ranks[silverIndustry] = ranks[silverIndustry].Where(p => p != port).ToArray();
                    }
                }
                if (!Search(0, 0))
                { errors.Add("Economic candidate " + candidate.ToString(CultureInfo.InvariantCulture) + ": public/private counts cannot fit direct producer/consumer exclusions within the bounded search."); continue; }
                var result = new List<Assignment>();
                for (var i = 0; i < distributions.Length; i++)
                {
                    var selectedPorts = selected[i].Select(p => ids[p]).ToArray();
                    // Public and private independently choose subsets of the validated combined capability.
                    var privatePorts = selectedPorts.Take(privateCounts[i]).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    var publicPorts = selectedPorts.Reverse().Take(publicCounts[i]).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    result.Add(new Assignment(distributions[i], publicPorts, privatePorts));
                }
                var silverPorts = result.Where(x => content.Recipes.Any(r => r.BuildingId == x.Distribution.IndustryId && r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.MintInputGoodId)))
                    .SelectMany(x => x.AllPorts).Distinct(StringComparer.Ordinal).ToArray();
                if (silverPorts.Length == ids.Length)
                { errors.Add("Economic candidate " + candidate.ToString(CultureInfo.InvariantCulture) + ": Silver production leaves no separate public Mint location."); continue; }
                var hullMaterialPorts = result.Where(x => content.Recipes.Any(r => r.BuildingId == x.Distribution.IndustryId &&
                    r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.ShipMaterialGoodId)))
                    .SelectMany(x => x.AllPorts).Distinct(StringComparer.Ordinal).Count();
                if (configuration.EnforceProductionSeparation && hullMaterialPorts == ids.Length)
                { errors.Add("Economic candidate " + candidate.ToString(CultureInfo.InvariantCulture) + ": hull-material production leaves no separate shipyard location."); continue; }
                attempted = candidate + 1; failures = Array.AsReadOnly(errors.ToArray()); return result;
            }
            throw new WorldGenerationException("No valid economic assignment after " + configuration.EconomicCandidateCount.ToString(CultureInfo.InvariantCulture) +
                " bounded candidates. Reduce industry town minima/maxima, increase town count, or explicitly disable production separation. " + string.Join(" ", errors.Take(3)));
        }
        private sealed class Assignment
        {
            internal IndustryDistribution Distribution { get; }
            internal IReadOnlyList<string> PublicPorts { get; }
            internal IReadOnlyList<string> PrivatePorts { get; }
            internal IEnumerable<string> AllPorts => PublicPorts.Concat(PrivatePorts).Distinct(StringComparer.Ordinal);
            internal Assignment(IndustryDistribution distribution, string[] publicPorts, string[] privatePorts)
            { Distribution = distribution; PublicPorts = publicPorts; PrivatePorts = privatePorts; }
        }
        private static long Distance(PortPosition first, PortPosition second)
        { var dx = (long)first.X - second.X; var dy = (long)first.Y - second.Y; return dx * dx + dy * dy; }
        private static int Q(int quantity) => EconomicUnits.FromWhole(quantity);
        private static long M(long coins) => EconomicUnits.Coins(coins);
    }
}
