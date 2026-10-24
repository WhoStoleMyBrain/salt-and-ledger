using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingEconomyGenerationTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RunIdentity Identity(int seed = 42, string charter = RunScenario.MerchantCharter,
        string? challenge = null, RunDifficulty difficulty = RunDifficulty.Normal,
        LivingPricingMode pricing = LivingPricingMode.StockOnly) =>
        new(challenge, charter, RunWorldMode.ExpandingEconomy, seed, difficulty, livingPricingMode: pricing);

    [Theory]
    [InlineData(-42, "northport,southhaven,westhaven,eastport")]
    [InlineData(42, "westhaven,southhaven,eastport,northport")]
    [InlineData(99, "southhaven,northport,eastport,westhaven")]
    [InlineData(int.MinValue, "southhaven,westhaven,northport,eastport")]
    [InlineData(int.MaxValue, "southhaven,eastport,westhaven,northport")]
    public void VersionThreePreservesExactGeographyAndRoleGolden(int seed, string roles)
    {
        var generated = ExpandingEconomyGeneration.Generate(seed);
        Assert.Equal(3, generated.GeneratorVersion);
        Assert.Equal(roles, string.Join(",", new[] { generated.FoodSourcePortId, generated.ProcessingPortId,
            generated.FurniturePortId, generated.RecoveryPortId }));
        Assert.Equal(SeededArchipelago.Generate(seed).Signature, generated.Geography.Signature);
        Assert.Equal(LivingCityGeneration.Generate(seed).Signature, generated.SourceRoles.Signature);
        Assert.Equal(generated.Signature, ExpandingEconomyGeneration.Generate(seed).Signature);
        Assert.StartsWith("expanding-v3|content3|value1|quantity10000|money10000|", generated.Signature);
        Assert.NotEqual(generated.MintPortId, generated.SilverPortId);
    }

    [Fact]
    public void RoleMatrixGuaranteesEveryExistingIndustryAndPhysicalSilverPathAcrossBroadSeeds()
    {
        var content = Content().ForExpandingEconomy();
        foreach (var seed in Enumerable.Range(0, 1024).Select(index => unchecked(index * 104729 * (index % 2 == 0 ? 1 : -1)))
            .Concat(new[] { int.MinValue, int.MaxValue }))
        {
            var generated = ExpandingEconomyGeneration.Generate(seed);
            Assert.Equal(4, generated.Ports.Count);
            Assert.Equal(6, generated.Geography.Routes.Count);
            Assert.Equal(content.Recipes.Select(recipe => recipe.Id).OrderBy(id => id),
                generated.Blueprints.Select(blueprint => blueprint.RecipeId).OrderBy(id => id));
            Assert.All(generated.Blueprints, blueprint =>
            {
                Assert.Equal(blueprint.BuildingId, content.GetRecipe(blueprint.RecipeId).BuildingId);
                Assert.NotEmpty(blueprint.EligiblePortIds);
                Assert.All(blueprint.EligiblePortIds, portId => Assert.Contains(generated.Ports, port => port.Id == portId));
                Assert.True(blueprint.WagePerBatch > 0);
            });
            Assert.DoesNotContain(generated.FurniturePortId, generated.Blueprints
                .Single(blueprint => blueprint.BuildingId == "sawmill").EligiblePortIds);
            Assert.DoesNotContain(generated.Blueprints, blueprint => blueprint.BuildingId == "royal_mint");
            Assert.Contains(generated.Geography.Routes, route =>
                (route.OriginPortId == generated.SilverPortId && route.DestinationPortId == generated.MintPortId) ||
                (route.DestinationPortId == generated.SilverPortId && route.OriginPortId == generated.MintPortId));
        }
    }

    [Fact]
    public void StocksCashQuotesNeedsAndSchedulesUseOneExplicitScale()
    {
        var generated = ExpandingEconomyGeneration.Generate(42);
        foreach (var port in generated.Ports)
        {
            var previous = generated.SourceRoles.Ports.Single(old => old.Id == port.Id);
            Assert.Equal(previous.Market.InitialMarketCash * 10000, port.Market.InitialMarketCash);
            Assert.Equal(previous.Settlement!.InitialHouseholdCash * 10000, port.Settlement!.InitialHouseholdCash);
            Assert.Equal(10000, port.Settlement.QuantityScale);
            Assert.Equal(0, port.Market.InitialQuantities["silver"]);
            foreach (var entry in previous.Market.InitialQuantities)
                Assert.Equal(entry.Value * 10000, port.Market.InitialQuantities[entry.Key]);
            foreach (var quote in previous.Market.Quotes.Values)
            {
                var current = port.Market.Quotes[quote.GoodId].Pricing!;
                Assert.Equal(quote.Pricing!.ReferencePrice, current.ReferencePrice);
                Assert.Equal(quote.Pricing.TargetStock * 10000, current.TargetStock);
            }
            Assert.All(port.Facilities, facility => Assert.Equal(previous.Facilities
                .Single(old => old.Instance.Id == facility.Instance.Id).WagePerBatch * 10000, facility.WagePerBatch));
            var food = port.Consumption.Single(need => need.Kind == ConsumptionKind.Essential);
            Assert.True(food.Proportional);
            Assert.Equal(4, food.IntervalDays);
            Assert.Equal(7500, food.PreviewDemand(15).Quantity);
            Assert.False(port.Consumption.Single(need => need.Kind == ConsumptionKind.Municipal).Proportional);
        }
    }

    [Theory]
    [InlineData(RunScenario.MerchantCharter, 1800, 10, 0)]
    [InlineData(RunScenario.IndustrialCharter, 1200, 8, 1)]
    [InlineData(RunScenario.IndependentCharter, 1500, 12, 0)]
    public void CharterPreservesWholePhysicalEndowmentsAndDeclaresV3InvestmentCapital(string charterId,
        long coins, int capacity, int facilities)
    {
        var generated = ExpandingEconomyGeneration.Generate(42);
        var charter = ExpandingEconomyScenario.GetCharter(generated, charterId);
        Assert.Equal(coins * 10000, charter.Cash);
        Assert.Equal(capacity * 10000, charter.CargoCapacity);
        Assert.Equal(facilities, charter.Facilities.Count);
        Assert.Equal(charterId == RunScenario.IndustrialCharter ? generated.ProcessingPortId : generated.FoodSourcePortId,
            charter.StartingPortId);
        var legacy = RunScenario.GetCharter(charterId);
        Assert.Equal(legacy.Cargo.Select(pair => (pair.Key, pair.Value * 10000)), charter.Cargo.Select(pair => (pair.Key, pair.Value)));
        if (facilities != 0)
            Assert.Equal(20000, charter.Facilities[0].InitialInventory["flour"]);
    }

    [Fact]
    public void VersionIdentityCannotReinterpretOldWorldsOrChallenges()
    {
        var identity = Identity(difficulty: RunDifficulty.SlowFoundations, pricing: LivingPricingMode.Coverage);
        Assert.Equal(3, identity.GeneratorVersion);
        Assert.Equal(3, identity.ContentVersion);
        Assert.Equal(1, identity.ValuationVersion);
        Assert.Contains("expanding-v3-seed42-coverage-content3-value1", identity.ComparisonGroup);
        Assert.NotEqual(identity, new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, 42,
            RunDifficulty.SlowFoundations, livingPricingMode: LivingPricingMode.Coverage));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 42, generatorVersion: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 42, contentVersion: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 42, valuationVersion: 2));
        Assert.Throws<ArgumentException>(() => Identity(challenge: RunScenario.CoastalChallenge));
        Assert.Throws<ArgumentException>(() => new RunIdentity(RunScenario.RoyalChallenge,
            RunScenario.MerchantCharter, RunWorldMode.LivingCities, 42));
        Assert.Equal(4, RunScenario.GetChallenges(RunWorldMode.ExpandingEconomy).Count);
        Assert.Equal(GrandCommission.ChallengeId, RunScenario.GetChallenges(RunWorldMode.ExpandingEconomy)[0].Id);
        Assert.Equal(ExpandingChallenges.Definitions, RunScenario.GetChallenges(RunWorldMode.ExpandingEconomy).Skip(1));
        Assert.Equal(3, RunScenario.Challenges.Count);
        Assert.Single(RunScenario.GetChallenges(RunWorldMode.LivingCities));
    }

    [Fact]
    public void ScaledWorldIsCanonicalAndReplayConfigurationAndCultureIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var first = RunScenario.Create(Content(), Identity());
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var reversed = RunScenario.Create(Content(), Identity(), reverseConfiguration: true);
            Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(reversed));
            for (var day = 0; day < 30; day++)
            {
                Assert.Equal(Events(first.AdvanceDay()), Events(reversed.AdvanceDay()));
                Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(reversed));
            }
            Assert.All(first.Snapshot().Ports.Values, port => Assert.Equal(7, port.Inventory.Count));
            Assert.Equal(10000, first.Snapshot().QuantityScale);
            Assert.Equal(10000, first.Snapshot().MoneyScale);
        }
        finally { CultureInfo.CurrentCulture = originalCulture; }
    }

    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades,
        result.Production, result.Consumption, result.Movements, result.PrivateProduction,
        result.Construction, result.Transfers, result.Development
    });

    [Fact]
    public void PrivateIndustryEligibilityAndMultipleSiteOwnershipAreAuthoritative()
    {
        var generation = ExpandingEconomyGeneration.Generate(42);
        var world = RunScenario.Create(Content(), Identity());
        var initial = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(EnterpriseScenario.PlayerOwnerId,
            generation.FurniturePortId, EnterpriseScenario.SawmillBlueprint, "illegal_sawmill"));
        Assert.Same(initial, world.Snapshot());
        world.ConstructFacility(EnterpriseScenario.PlayerOwnerId, generation.ProcessingPortId,
            ExpandingEconomyGeneration.MillBlueprint, "mill_one");
        world.ConstructFacility(EnterpriseScenario.PlayerOwnerId, generation.ProcessingPortId,
            ExpandingEconomyGeneration.MillBlueprint, "mill_two");
        var snapshot = world.Snapshot();
        Assert.Equal(2, snapshot.PrivateFacilities.Count);
        Assert.NotSame(snapshot.PrivateFacilities["mill_one"].Inventory, snapshot.PrivateFacilities["mill_two"].Inventory);
        Assert.Equal(12800000, snapshot.Companies[EnterpriseScenario.PlayerOwnerId].Cash);
    }
}
