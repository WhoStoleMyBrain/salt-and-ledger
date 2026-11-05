using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class IndustryGenerationCount
    {
        public string IndustryId { get; }
        public int PublicTowns { get; }
        public int PrivateTowns { get; }
        public int CapabilityTowns { get; }
        internal IndustryGenerationCount(string id, int publicTowns, int privateTowns, int capabilityTowns)
        { IndustryId = id; PublicTowns = publicTowns; PrivateTowns = privateTowns; CapabilityTowns = capabilityTowns; }
    }

    public sealed class TownGenerationQuality
    {
        public string PortId { get; }
        public int LaneDegree { get; }
        public long TotalTravelDistance { get; }
        public int Population { get; }
        public int Prosperity { get; }
        public long MarketCash { get; }
        public long HouseholdCash { get; }
        public int ProductionCapabilities { get; }
        internal TownGenerationQuality(string id, int degree, long totalDistance, RegionalPortConfiguration port, int capabilities)
        {
            PortId = id; LaneDegree = degree; TotalTravelDistance = totalDistance; Population = port.Settlement!.InitialPopulation;
            Prosperity = port.Settlement.InitialProsperity; MarketCash = port.Market.InitialMarketCash;
            HouseholdCash = port.Settlement.InitialHouseholdCash; ProductionCapabilities = capabilities;
        }
    }

    /// <summary>Structural quality only. These diagnostics do not promise profitable or indefinitely sustainable play.</summary>
    public sealed class WorldGenerationQuality
    {
        public int TownCount { get; }
        public int LaneCount { get; }
        public bool Connected { get; }
        public long TotalLaneDistance { get; }
        public long MaximumShortestTravelDistance { get; }
        public int MaximumTravelDaysAtSpeed100 { get; }
        public long MeanNearestTownSquaredDistance { get; }
        public int TransportDependencies { get; }
        public int DirectDependencyPairs { get; }
        public int LocalDependencyViolations { get; }
        public int InstitutionDependencyViolations { get; }
        public bool AllEnabledInputsObtainable { get; }
        public int TotalPopulation { get; }
        public long TotalMarketCash { get; }
        public long TotalHouseholdCash { get; }
        public string MostCentralPortId { get; }
        public IReadOnlyList<string> LeafPortIds { get; }
        public IReadOnlyList<IndustryGenerationCount> Industries { get; }
        public IReadOnlyList<TownGenerationQuality> Towns { get; }
        public int GeographyCandidates { get; }
        public int SelectedGeographyCandidate { get; }
        public int EconomicCandidates { get; }
        public IReadOnlyList<string> RejectedConstraints { get; }

        internal WorldGenerationQuality(ExpandingEconomyGeneration generation, ContentCatalog content, int economicCandidates,
            IReadOnlyList<string> economicFailures)
        {
            TownCount = generation.Ports.Count; LaneCount = generation.Geography.Routes.Count;
            GeographyCandidates = generation.Geography.CandidateCount; SelectedGeographyCandidate = generation.Geography.SelectedCandidate;
            EconomicCandidates = economicCandidates;
            RejectedConstraints = Array.AsReadOnly(generation.Geography.RejectionReasons.Concat(economicFailures).ToArray());
            TotalLaneDistance = generation.Geography.Routes.Sum(r => (long)r.Distance);
            var ids = generation.Ports.Select(p => p.Id).ToArray();
            var capabilities = ids.ToDictionary(id => id, id => generation.Ports.Single(p => p.Id == id).Facilities.Select(f => f.Instance.RecipeId)
                .Concat(generation.Blueprints.Where(b => b.EligiblePortIds.Contains(id)).Select(b => b.RecipeId))
                .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            var totals = ids.ToDictionary(id => id, id => 0L, StringComparer.Ordinal);
            // All-pairs integer closure keeps quality evaluation bounded at O(towns^3).
            var distances = new long[ids.Length, ids.Length];
            var days = new int[ids.Length, ids.Length];
            var indices = ids.Select((id, index) => (id, index)).ToDictionary(p => p.id, p => p.index, StringComparer.Ordinal);
            for (var first = 0; first < ids.Length; first++)
                for (var second = 0; second < ids.Length; second++)
                { distances[first, second] = first == second ? 0 : long.MaxValue / 4; days[first, second] = first == second ? 0 : int.MaxValue / 4; }
            foreach (var route in generation.Geography.Routes)
            {
                var first = indices[route.OriginPortId]; var second = indices[route.DestinationPortId];
                distances[first, second] = distances[second, first] = route.Distance;
                days[first, second] = days[second, first] = route.DurationDays(100);
            }
            for (var via = 0; via < ids.Length; via++)
                for (var first = 0; first < ids.Length; first++)
                    for (var second = 0; second < ids.Length; second++)
                    {
                        distances[first, second] = Math.Min(distances[first, second], distances[first, via] + distances[via, second]);
                        days[first, second] = Math.Min(days[first, second], days[first, via] + days[via, second]);
                    }
            for (var first = 0; first < ids.Length; first++)
                for (var second = first + 1; second < ids.Length; second++)
                {
                    if (distances[first, second] >= long.MaxValue / 4) throw new WorldGenerationException("Generated sea lanes contain an isolated town.");
                    MaximumShortestTravelDistance = Math.Max(MaximumShortestTravelDistance, distances[first, second]);
                    MaximumTravelDaysAtSpeed100 = Math.Max(MaximumTravelDaysAtSpeed100, days[first, second]);
                    totals[ids[first]] += distances[first, second]; totals[ids[second]] += distances[first, second];
                }
            Connected = true;
            MostCentralPortId = totals.OrderBy(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
            var towns = generation.Ports.Select(port => new TownGenerationQuality(port.Id,
                generation.Geography.Routes.Count(r => r.OriginPortId == port.Id || r.DestinationPortId == port.Id),
                totals[port.Id], port, capabilities[port.Id].Length)).ToArray();
            Towns = Array.AsReadOnly(towns);
            LeafPortIds = Array.AsReadOnly(towns.Where(t => t.LaneDegree == 1).Select(t => t.PortId).ToArray());
            TotalPopulation = towns.Sum(t => t.Population); TotalMarketCash = towns.Sum(t => t.MarketCash); TotalHouseholdCash = towns.Sum(t => t.HouseholdCash);
            MeanNearestTownSquaredDistance = generation.Geography.Positions.Values.Sum(a => generation.Geography.Positions.Values.Where(b => a.PortId != b.PortId).Min(b =>
            { var dx = (long)a.X - b.X; var dy = (long)a.Y - b.Y; return dx * dx + dy * dy; })) / TownCount;
            var enabledRecipes = content.Recipes.Where(r => generation.Configuration.Industries.Any(i => i.IndustryId == r.BuildingId)).ToArray();
            DirectDependencyPairs = enabledRecipes.Sum(first => enabledRecipes.Count(second => first.Id != second.Id &&
                first.Outputs.Any(o => second.Inputs.Any(i => i.GoodId == o.GoodId))));
            foreach (var port in generation.Ports)
            {
                var local = capabilities[port.Id].Select(content.GetRecipe).ToArray();
                if (port.Id == generation.MintPortId && local.Any(r => r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.MintInputGoodId)))
                    InstitutionDependencyViolations++;
                if (port.Id == generation.ShipyardPortId && local.Any(r => r.Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.ShipMaterialGoodId)))
                    InstitutionDependencyViolations++;
                for (var first = 0; first < local.Length; first++)
                    for (var second = first + 1; second < local.Length; second++)
                        if (ProductionSpecialization.AreIncompatible(local[first], local[second])) LocalDependencyViolations++;
                foreach (var recipe in local)
                    foreach (var input in recipe.Inputs)
                        if (!local.Any(r => r.Outputs.Any(o => o.GoodId == input.GoodId)) &&
                            capabilities.Where(p => p.Key != port.Id).Any(p => p.Value.Any(id => content.GetRecipe(id).Outputs.Any(o => o.GoodId == input.GoodId))))
                            TransportDependencies++;
            }
            var obtainable = new HashSet<string>(StringComparer.Ordinal);
            for (var pass = 0; pass < enabledRecipes.Length; pass++)
                foreach (var recipe in enabledRecipes)
                    if (recipe.Inputs.All(i => obtainable.Contains(i.GoodId)))
                        foreach (var output in recipe.Outputs) obtainable.Add(output.GoodId);
            AllEnabledInputsObtainable = enabledRecipes.All(r => r.Inputs.All(i => obtainable.Contains(i.GoodId)));
            Industries = Array.AsReadOnly(generation.Configuration.Industries.Select(i =>
            {
                var publicIds = generation.Ports.Where(p => p.Facilities.Any(f => f.Instance.BuildingId == i.IndustryId)).Select(p => p.Id).ToArray();
                var privateIds = generation.Blueprints.Where(b => b.BuildingId == i.IndustryId).SelectMany(b => b.EligiblePortIds).Distinct(StringComparer.Ordinal).ToArray();
                return new IndustryGenerationCount(i.IndustryId, publicIds.Length, privateIds.Length, publicIds.Concat(privateIds).Distinct(StringComparer.Ordinal).Count());
            }).ToArray());
            if (generation.Configuration.EnforceProductionSeparation && (LocalDependencyViolations != 0 || InstitutionDependencyViolations != 0))
                throw new WorldGenerationException("Generated production violates " + LocalDependencyViolations + " direct recipe dependencies and " + InstitutionDependencyViolations + " institution input dependencies.");
            if (generation.Configuration.RequireSupplyChains && !AllEnabledInputsObtainable)
                throw new WorldGenerationException("Generated industries have an unobtainable production input.");
        }
    }
}
