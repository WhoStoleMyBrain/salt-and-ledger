using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingEconomyGenerationTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static RunIdentity Identity(int seed = 42, string charter = RunScenario.MerchantCharter,
        RunDifficulty difficulty = RunDifficulty.Normal, LivingPricingMode pricing = LivingPricingMode.StockOnly) =>
        new(null, charter, RunWorldMode.ExpandingEconomy, seed, difficulty, livingPricingMode: pricing);

    [Fact]
    public void BroadSeedSweepPreservesAllConstraintsAndFiniteEconomics()
    {
        var content = Content();
        var counts = new System.Collections.Generic.HashSet<int>();
        foreach (var seed in Enumerable.Range(0, 128).Select(i => unchecked(i * 104729)).Concat(new[] { int.MinValue, int.MaxValue, -42 }))
        {
            var generated = ExpandingEconomyGeneration.Generate(seed, content: content);
            var config = generated.Configuration;
            counts.Add(generated.Ports.Count);
            Assert.InRange(generated.Ports.Count, config.TownCount.Min, config.TownCount.Max);
            Assert.Equal(generated.Ports.Count, generated.Ports.Select(p => p.Id).Distinct().Count());
            Assert.Equal(generated.Ports.Count, generated.Ports.Select(p => p.Name).Distinct().Count());
            Assert.True(generated.Quality.Connected);
            Assert.True(generated.Quality.AllEnabledInputsObtainable);
            Assert.Equal(0, generated.Quality.LocalDependencyViolations);
            Assert.Equal(0, generated.Quality.InstitutionDependencyViolations);
            Assert.True(generated.Quality.TransportDependencies > 0);
            Assert.NotEqual(generated.MintPortId, generated.SilverPortId);
            Assert.DoesNotContain(generated.MintPortId, generated.EligiblePorts("mine_silver"));
            Assert.DoesNotContain(generated.MintPortId, generated.PublicPorts("mine_silver"));
            Assert.DoesNotContain(generated.ShipyardPortId, generated.Blueprints.Where(b =>
                content.GetRecipe(b.RecipeId).Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.ShipMaterialGoodId)).SelectMany(b => b.EligiblePortIds));
            Assert.DoesNotContain(generated.Ports.Single(p => p.Id == generated.ShipyardPortId).Facilities,
                facility => content.GetRecipe(facility.Instance.RecipeId).Outputs.Any(o => o.GoodId == ExpandingEconomyConfiguration.ShipMaterialGoodId));
            foreach (var industry in config.Industries)
            {
                var count = generated.Quality.Industries.Single(i => i.IndustryId == industry.IndustryId);
                Assert.InRange(count.PublicTowns, industry.PublicTowns.Min, industry.PublicTowns.Max);
                Assert.InRange(count.PrivateTowns, industry.PrivateTowns.Min, industry.PrivateTowns.Max);
                Assert.True(count.CapabilityTowns > 0);
            }
            foreach (var port in generated.Ports)
            {
                var capabilities = port.Facilities.Select(f => f.Instance.RecipeId).Concat(generated.Blueprints.Where(b => b.EligiblePortIds.Contains(port.Id)).Select(b => b.RecipeId));
                Assert.Null(ProductionSpecialization.Conflict(content, capabilities));
                Assert.InRange(port.Settlement!.InitialPopulation, config.Population.Min, config.Population.Max);
                Assert.InRange(port.Settlement.InitialProsperity, config.Prosperity.Min, config.Prosperity.Max);
                Assert.InRange(port.Market.InitialMarketCash, config.MarketCash.Min * 10000L, config.MarketCash.Max * 10000L);
                Assert.InRange(port.Settlement.InitialHouseholdCash, config.HouseholdCashPerResident.Min * (long)port.Settlement.InitialPopulation * 10000,
                    config.HouseholdCashPerResident.Max * (long)port.Settlement.InitialPopulation * 10000);
                foreach (var stock in config.StartingInventories)
                    Assert.InRange(port.Market.InitialQuantities[stock.Key], stock.Value.Min * 10000, stock.Value.Max * 10000);
            }
        }
        Assert.Equal(new[] { 8, 9, 10, 11, 12 }, counts.OrderBy(x => x));
    }

    [Fact]
    public void PublicAndPrivateCountsAreIndependentAndMayBeNarrow()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        foreach (JObject industry in (JArray)json["industries"]!)
        { industry["publicTowns"] = new JObject { ["min"] = 0, ["max"] = 0 }; industry["privateTowns"] = new JObject { ["min"] = 1, ["max"] = 1 }; }
        var config = WorldGenerationConfiguration.Parse(json.ToString());
        foreach (var seed in new[] { 0, 42, -42, int.MinValue })
        {
            var generation = ExpandingEconomyGeneration.Generate(seed, configuration: config);
            Assert.All(generation.Ports, port => Assert.Empty(port.Facilities));
            Assert.All(generation.Blueprints, blueprint => Assert.Single(blueprint.EligiblePortIds));
            Assert.True(generation.Quality.AllEnabledInputsObtainable);
            Assert.Equal(0, generation.Quality.LocalDependencyViolations);
        }
    }

    [Fact]
    public void ChangingCanonicalRecipeAutomaticallyIntroducesNewExclusions()
    {
        var root = JObject.Parse(ContentLoaderTests.CanonicalJson());
        var workshop = ((JArray)root["recipes"]!).Cast<JObject>().Single(r => (string)r["id"]! == "make_furniture");
        workshop["outputs"] = new JArray(new JObject { ["goodId"] = "grain", ["quantity"] = 1 });
        var content = ContentLoader.Load(root.ToString()).ForExpandingEconomy();
        Assert.True(ProductionSpecialization.AreIncompatible(content.GetRecipe("make_furniture"), content.GetRecipe("mill_flour")));
        foreach (var seed in new[] { 0, 1, 42, -99 })
        {
            var generation = ExpandingEconomyGeneration.Generate(seed, content: content);
            var workshops = generation.PublicPorts("make_furniture").Concat(generation.EligiblePorts("make_furniture"));
            var mills = generation.PublicPorts("mill_flour").Concat(generation.EligiblePorts("mill_flour"));
            Assert.Empty(workshops.Intersect(mills));
        }
    }

    [Fact]
    public void ShipyardExclusionFollowsCanonicalRecipeOutputsRatherThanIndustryNames()
    {
        var root = JObject.Parse(ContentLoaderTests.CanonicalJson());
        var workshop = ((JArray)root["recipes"]!).Cast<JObject>().Single(r => (string)r["id"]! == "make_furniture");
        ((JArray)workshop["outputs"]!).Add(new JObject { ["goodId"] = ExpandingEconomyConfiguration.ShipMaterialGoodId, ["quantity"] = 1 });
        var content = ContentLoader.Load(root.ToString()).ForExpandingEconomy();
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["institutions"]!["shipyardPortIndex"] = 0;
        var config = WorldGenerationConfiguration.Parse(json.ToString(), content);
        foreach (var seed in new[] { 0, 1, 42, -99 })
        {
            var generation = ExpandingEconomyGeneration.Generate(seed, configuration: config, content: content);
            Assert.Equal("town_001", generation.ShipyardPortId);
            Assert.DoesNotContain(generation.ShipyardPortId, generation.PublicPorts("saw_planks").Concat(generation.EligiblePorts("saw_planks")));
            Assert.DoesNotContain(generation.ShipyardPortId, generation.PublicPorts("make_furniture").Concat(generation.EligiblePorts("make_furniture")));
        }
    }

    [Fact]
    public void EveryDirectDependencyIsSymmetricButIndirectChainsAreNotForbidden()
    {
        var content = Content();
        foreach (var first in content.Recipes)
            foreach (var second in content.Recipes)
            {
                var expected = first.Id != second.Id && (first.Outputs.Any(o => second.Inputs.Any(i => i.GoodId == o.GoodId)) ||
                    second.Outputs.Any(o => first.Inputs.Any(i => i.GoodId == o.GoodId)));
                Assert.Equal(expected, ProductionSpecialization.AreIncompatible(first, second));
            }
        Assert.False(ProductionSpecialization.AreIncompatible(content.GetRecipe("harvest_grain"), content.GetRecipe("bake_bread")));
        Assert.False(ProductionSpecialization.AreIncompatible(content.GetRecipe("harvest_logs"), content.GetRecipe("make_furniture")));
    }

    [Fact]
    public void ExplicitInstitutionLocationsAreHonoredAndMintCannotSourceSilver()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["institutions"] = new JObject { ["startingPortIndex"] = 0, ["mintPortIndex"] = 1, ["silverPortIndex"] = 2, ["shipyardPortIndex"] = 3 };
        var config = WorldGenerationConfiguration.Parse(json.ToString());
        foreach (var seed in new[] { 0, 42, -99 })
        {
            var g = ExpandingEconomyGeneration.Generate(seed, configuration: config);
            Assert.Equal("town_001", g.StartPortId); Assert.Equal("town_002", g.MintPortId);
            Assert.Equal("town_003", g.SilverPortId); Assert.Equal("town_004", g.ShipyardPortId);
            Assert.Contains(g.SilverPortId, g.PublicPorts("mine_silver").Concat(g.EligiblePorts("mine_silver")));
            Assert.DoesNotContain(g.ShipyardPortId, g.PublicPorts("saw_planks").Concat(g.EligiblePorts("saw_planks")));
        }
    }

    [Fact]
    public void SignatureIsDeterministicCultureIndependentAndConfigurationSensitive()
    {
        var original = CultureInfo.CurrentCulture;
        var expected = ExpandingEconomyGeneration.Generate(int.MinValue).Signature;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, ExpandingEconomyGeneration.Generate(int.MinValue).Signature);
            var root = JObject.Parse(WorldGenerationConfiguration.Default.ToJson()); root["deadlineDays"] = 365;
            var config = WorldGenerationConfiguration.Parse(root.ToString());
            Assert.NotEqual(expected, ExpandingEconomyGeneration.Generate(int.MinValue, configuration: config).Signature);
            var reverse = JObject.Parse(WorldGenerationConfiguration.Default.ToJson()); reverse["industries"] = new JArray(((JArray)reverse["industries"]!).Reverse());
            reverse["enabledGoods"] = new JArray(((JArray)reverse["enabledGoods"]!).Reverse());
            Assert.Equal(expected, ExpandingEconomyGeneration.Generate(int.MinValue, configuration: WorldGenerationConfiguration.Parse(reverse.ToString())).Signature);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData(RunScenario.MerchantCharter, 1800, 10, 0)]
    [InlineData(RunScenario.IndustrialCharter, 1200, 8, 1)]
    [InlineData(RunScenario.IndependentCharter, 1500, 12, 0)]
    public void ChartersRetainExplicitFiniteEndowmentsAtValidGeneratedLocations(string charterId, long coins, int capacity, int facilities)
    {
        var generation = ExpandingEconomyGeneration.Generate(42);
        var charter = ExpandingEconomyScenario.GetCharter(generation, charterId);
        Assert.Equal(coins * 10000, charter.Cash); Assert.Equal(capacity * 10000, charter.CargoCapacity);
        Assert.Equal(facilities, charter.Facilities.Count);
        Assert.Contains(charter.StartingPortId, generation.Ports.Select(p => p.Id));
        foreach (var facility in charter.Facilities)
            Assert.Contains(facility.PortId, generation.Blueprints.Single(b => b.Id == facility.BlueprintId).EligiblePortIds);
        var world = RunScenario.Create(Content(), Identity(charter: charterId));
        Assert.Equal(charter.StartingPortId, world.Snapshot().Ships[WorldIds.PlayerShipId].DockedPortId);
    }

    [Fact]
    public void ScaledWorldReplayAndSaveContinueExactlyUnderReversedConfiguration()
    {
        var first = RunScenario.Create(Content(), Identity());
        var reversed = RunScenario.Create(Content(), Identity(), reverseConfiguration: true);
        Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(reversed));
        for (var day = 0; day < 30; day++)
        {
            Assert.Equal(Events(first.AdvanceDay()), Events(reversed.AdvanceDay()));
            Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(reversed));
        }
        Assert.All(first.Snapshot().Ports.Values, port => Assert.Equal(7, port.Inventory.Count));
        Assert.Equal(10000, first.Snapshot().QuantityScale); Assert.Equal(10000, first.Snapshot().MoneyScale);
    }
    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    { result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades, result.Production, result.Consumption,
        result.Movements, result.PrivateProduction, result.Construction, result.Transfers, result.Development });

    [Fact]
    public void ConstructionRespectsEligibilityAndMultipleSitesRetainSeparateFacilityIdentity()
    {
        var generation = ExpandingEconomyGeneration.Generate(42);
        var world = RunScenario.Create(Content(), Identity());
        var blueprint = generation.Blueprints.Single(b => b.Id == WorldIds.SawmillBlueprint);
        var excluded = generation.Ports.Select(p => p.Id).First(id => !blueprint.EligiblePortIds.Contains(id));
        var initial = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(WorldIds.PlayerOwnerId, excluded, blueprint.Id, "illegal_sawmill"));
        Assert.Same(initial, world.Snapshot());
        var port = generation.FirstEligiblePort("mill_flour");
        world.ConstructFacility(WorldIds.PlayerOwnerId, port, ExpandingEconomyGeneration.MillBlueprint, "mill_one");
        world.ConstructFacility(WorldIds.PlayerOwnerId, port, ExpandingEconomyGeneration.MillBlueprint, "mill_two");
        var snapshot = world.Snapshot();
        Assert.Equal(2, snapshot.PrivateFacilities.Count);
        Assert.Equal(snapshot.PrivateFacilities["mill_one"].SharedStockpileId, snapshot.PrivateFacilities["mill_two"].SharedStockpileId);
        Assert.Equal(12800000, snapshot.Companies[WorldIds.PlayerOwnerId].Cash);
    }
}
