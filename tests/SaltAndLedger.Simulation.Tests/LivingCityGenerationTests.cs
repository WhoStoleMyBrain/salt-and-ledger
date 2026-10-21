using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class LivingCityGenerationTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RunIdentity Identity(int seed = 42, string charter = RunScenario.MerchantCharter,
        string? challenge = null, RunDifficulty difficulty = RunDifficulty.Normal,
        LivingPricingMode pricing = LivingPricingMode.StockOnly) =>
        new(challenge, charter, RunWorldMode.LivingCities, seed, difficulty, livingPricingMode: pricing);

    [Theory]
    [InlineData(0, "southhaven,northport,westhaven,eastport", "30,25,30,20", "1300,1200,1600,400")]
    [InlineData(1, "northport,westhaven,eastport,southhaven", "30,20,30,20", "1400,1100,1400,400")]
    [InlineData(-1, "westhaven,northport,southhaven,eastport", "30,20,35,30", "1200,1000,1600,350")]
    [InlineData(-42, "northport,southhaven,westhaven,eastport", "30,30,30,25", "1200,1100,1500,400")]
    [InlineData(42, "westhaven,southhaven,eastport,northport", "20,20,30,20", "1400,1200,1500,350")]
    [InlineData(99, "southhaven,northport,eastport,westhaven", "25,30,30,20", "1200,1100,1600,400")]
    [InlineData(int.MinValue, "southhaven,westhaven,northport,eastport", "25,30,35,20", "1300,1200,1600,350")]
    [InlineData(int.MaxValue, "southhaven,eastport,westhaven,northport", "25,25,35,25", "1200,1200,1600,300")]
    public void VersionTwoGoldenRolesPopulationAndCashAreStable(int seed, string roles, string population, string marketCash)
    {
        var generated = LivingCityGeneration.Generate(seed);
        var order = new[] { generated.FoodSourcePortId, generated.ProcessingPortId,
            generated.FurniturePortId, generated.RecoveryPortId };
        Assert.Equal(2, generated.GeneratorVersion);
        Assert.Equal(roles, string.Join(",", order));
        Assert.Equal(population, string.Join(",", order.Select(id => generated.Ports.Single(port => port.Id == id).Settlement!.InitialPopulation)));
        Assert.Equal(marketCash, string.Join(",", order.Select(id => generated.Ports.Single(port => port.Id == id).Market.InitialMarketCash)));
        Assert.Equal(SeededArchipelago.Generate(seed).Signature, generated.Geography.Signature);
        Assert.Equal(generated.Signature, LivingCityGeneration.Generate(seed).Signature);
        Assert.StartsWith("living-v2|", generated.Signature);
        Assert.Contains("private:private_sawmill", generated.Signature);
        Assert.Contains("need:", generated.Signature);
    }

    [Theory]
    [InlineData(-42, "1c08c206548109e136c691bf626ec4542c8a76d0b3ca8db3cf4016a671afd3d7")]
    [InlineData(42, "5feab1cb26582503568d80e919ba9f67524fd5a5c3cb5e5674ac961f48a0501f")]
    [InlineData(99, "27978f23aaa7666c0900a9ab38ee4a66005b623e9462a8460206f485050293cd")]
    [InlineData(int.MinValue, "e8cd2d43421c0c1fdd729b4560bb62dd076273e37eb1af9cfdc0743d4ee3f163")]
    [InlineData(int.MaxValue, "5218c6cf0d2ca48b8377b462cd69b9b2f6de520c46926786d28e4d6b0abd0403")]
    public void CompleteVersionTwoSignatureGoldenIncludesAllGeneratedEconomicFactors(int seed, string expectedHash)
    {
        var bytes = Encoding.UTF8.GetBytes(LivingCityGeneration.Generate(seed).Signature);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    [Fact]
    public void EveryGeneratedRoleMatrixHasAllSixProducersAndMissingLocalChains()
    {
        var content = Content();
        foreach (var seed in Enumerable.Range(0, 256).Select(index => unchecked(index * 104729))
            .Concat(new[] { int.MinValue, int.MaxValue }))
        {
            var generated = LivingCityGeneration.Generate(seed);
            Assert.Equal(4, generated.Ports.Count);
            Assert.Equal(6, generated.Geography.Routes.Count);
            Assert.Equal(content.Recipes.Select(recipe => recipe.Id).OrderBy(id => id), generated.Ports
                .SelectMany(port => port.Facilities).Select(facility => facility.Instance.RecipeId).Distinct().OrderBy(id => id));
            foreach (var port in generated.Ports)
            {
                Assert.InRange(port.Settlement!.InitialPopulation, 20, 40);
                Assert.InRange(port.Settlement.InitialHouseholdCash, 440, 1680);
                Assert.True(port.Market.InitialMarketCash > 0);
                Assert.All(port.Facilities, facility => Assert.True(facility.WagePerBatch > 0));
                Assert.DoesNotContain(port.Facilities, facility => facility.Instance.BuildingId == "mill" &&
                    port.Facilities.Any(other => other.Instance.BuildingId == "farm"));
                Assert.Single(port.Consumption, need => need.Kind == ConsumptionKind.Essential && need.GoodId == "bread");
                Assert.Single(port.Consumption, need => need.Kind == ConsumptionKind.Luxury && need.GoodId == "furniture");
                Assert.Single(port.Consumption, need => need.Kind == ConsumptionKind.Municipal && need.GoodId == "planks");
            }
            var furniture = generated.Ports.Single(port => port.Id == generated.FurniturePortId);
            Assert.Contains(furniture.Facilities, facility => facility.Instance.BuildingId == "lumber_camp");
            Assert.Contains(furniture.Facilities, facility => facility.Instance.BuildingId == "furniture_workshop");
            Assert.DoesNotContain(furniture.Facilities, facility => facility.Instance.BuildingId == "sawmill");
            Assert.DoesNotContain(generated.FurniturePortId,
                generated.Blueprints.Single(blueprint => blueprint.Id == EnterpriseScenario.SawmillBlueprint).EligiblePortIds);
            Assert.All(generated.Blueprints, blueprint =>
            {
                Assert.Equal(blueprint.BuildingId, content.GetRecipe(blueprint.RecipeId).BuildingId);
                Assert.All(blueprint.EligiblePortIds, id => Assert.Contains(generated.Ports, port => port.Id == id));
            });
        }
    }

    [Fact]
    public void CapabilityIsEnforcedByWorldAndTradeNeverGrantsMissingProduction()
    {
        var generation = LivingCityGeneration.Generate(42);
        var world = RunScenario.Create(Content(), Identity());
        var initial = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(EnterpriseScenario.PlayerOwnerId,
            generation.FurniturePortId, EnterpriseScenario.SawmillBlueprint, "forbidden_sawmill"));
        Assert.Same(initial, world.Snapshot());
        foreach (var ship in initial.Ships.Values.Where(ship => ship.AutomationEnabled))
            world.SetAutomationEnabled(ship.Id, false);
        for (var day = 0; day < 24; day++) world.AdvanceDay();
        var town = world.Snapshot().Ports[generation.FurniturePortId];
        Assert.True(town.Inventory["logs"] > 0);
        Assert.True(town.Inventory["planks"] < 2);
        Assert.Equal(ScheduledProductionOutcome.InputShortage,
            town.Facilities.Single(facility => facility.Configuration.Instance.BuildingId == "furniture_workshop").LastOutcome);
    }

    [Theory]
    [InlineData(RunScenario.MerchantCharter)]
    [InlineData(RunScenario.IndependentCharter)]
    [InlineData(RunScenario.IndustrialCharter)]
    public void GeneratedChartersPreserveAllAssetsAndOnlyAdaptTheirLocations(string charterId)
    {
        var identity = Identity(charter: charterId, difficulty: RunDifficulty.SlowFoundations);
        var historical = RunScenario.GetCharter(charterId);
        var adapted = RunScenario.GetCharter(identity);
        var generated = LivingCityGeneration.Generate(42);
        var world = RunScenario.Create(Content(), identity).Snapshot();
        Assert.Equal(historical.Cash, adapted.Cash);
        Assert.Equal(historical.CargoCapacity, adapted.CargoCapacity);
        Assert.Equal(historical.Cargo, adapted.Cargo);
        Assert.Equal(historical.Facilities.Count, adapted.Facilities.Count);
        Assert.Equal(adapted.StartingPortId, world.Ships[EnterpriseScenario.PlayerShipId].DockedPortId);
        Assert.Equal(historical.Cash, world.Companies[EnterpriseScenario.PlayerOwnerId].Cash);
        Assert.Equal(0, world.Accounting.Companies[EnterpriseScenario.PlayerOwnerId].ConstructionExpenditure);
        Assert.Equal(charterId == RunScenario.IndustrialCharter ? generated.ProcessingPortId : generated.FoodSourcePortId,
            adapted.StartingPortId);
        if (charterId == RunScenario.IndustrialCharter)
        {
            var bakery = world.PrivateFacilities[RunScenario.StarterBakery];
            Assert.True(bakery.IsOperational);
            Assert.True(bakery.IsStarterGrant);
            Assert.Equal(2, bakery.FirstProductionDay);
            Assert.Equal(2, bakery.Inventory["flour"]);
            Assert.Equal(2, bakery.Inventory["logs"]);
            Assert.Contains(bakery.PortId, bakery.Blueprint.EligiblePortIds);
        }
    }

    [Fact]
    public void WholeGeneratedWorldAndRetryIgnoreConfigurationOrderAndCulture()
    {
        var identity = Identity(-42, pricing: LivingPricingMode.Coverage);
        string Run(bool reverse)
        {
            var world = RunScenario.Create(Content(), identity, reverse);
            world.SetAutomationEnabled(EnterpriseScenario.PlayerShipId, true);
            for (var day = 0; day < 48; day++) world.AdvanceDay();
            return Serialize(world.Snapshot());
        }
        var before = Run(false);
        var signature = LivingCityGeneration.Generate(-42, pricingMode: LivingPricingMode.Coverage).Signature;
        Assert.Equal(before, Run(true));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(before, Run(true));
            Assert.Equal(signature, LivingCityGeneration.Generate(-42, pricingMode: LivingPricingMode.Coverage).Signature);
        }
        finally { CultureInfo.CurrentCulture = culture; }
        var first = RunScenario.Create(Content(), identity);
        var initial = Serialize(first.Snapshot());
        for (var day = 0; day < 20; day++) first.AdvanceDay();
        Assert.Equal(initial, Serialize(RunScenario.Create(Content(), identity).Snapshot()));
    }

    [Fact]
    public void IdentityDistinguishesVersionsPricingAndRecoveryWithoutChangingHistoricalLists()
    {
        var identity = Identity(-42, challenge: RunScenario.RecoveryChallenge, difficulty: RunDifficulty.SlowFoundations);
        Assert.Equal("city_recovery/merchant_company/ascension1/living-v2-seed-42-stockonly", identity.ComparisonGroup);
        Assert.Equal(2, identity.GeneratorVersion);
        Assert.Equal(identity, Identity(-42, challenge: RunScenario.RecoveryChallenge, difficulty: RunDifficulty.SlowFoundations));
        Assert.NotEqual(identity, Identity(-42, challenge: RunScenario.RecoveryChallenge,
            difficulty: RunDifficulty.SlowFoundations, pricing: LivingPricingMode.Coverage));
        Assert.NotEqual(LivingCityGeneration.Generate(-42).Signature,
            LivingCityGeneration.Generate(-42, pricingMode: LivingPricingMode.Coverage).Signature);
        Assert.Equal(new[] { RunScenario.CoastalChallenge, RunScenario.ReliefChallenge, RunScenario.AmbitionChallenge },
            RunScenario.Challenges.Select(challenge => challenge.Id));
        Assert.Same(RunScenario.Challenges, RunScenario.GetChallenges(RunWorldMode.Classic));
        Assert.Same(RunScenario.Challenges, RunScenario.GetChallenges(RunWorldMode.Seeded));
        Assert.Equal(RunScenario.RecoveryChallenge, Assert.Single(RunScenario.GetChallenges(RunWorldMode.LivingCities)).Id);
        Assert.Throws<ArgumentException>(() => RunScenario.Create(Content(), RunScenario.RecoveryChallenge, RunScenario.MerchantCharter));
        Assert.Throws<ArgumentException>(() => new RunIdentity(RunScenario.RecoveryChallenge, RunScenario.MerchantCharter));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.Seeded, 42, livingPricingMode: LivingPricingMode.Coverage));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.LivingCities, 42, generatorVersion: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LivingCityGeneration.Generate(42, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LivingCityGeneration.Generate(42, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => LivingCityGeneration.Generate(42, pricingMode: (LivingPricingMode)99));
    }

    [Fact]
    public void PreviewIsImmutableAndMatchesTheActuallyConstructedScenario()
    {
        var identity = Identity(99, challenge: RunScenario.RecoveryChallenge);
        var generation = LivingCityGeneration.Generate(99);
        var actual = RunScenario.Create(Content(), identity).Snapshot();
        Assert.Equal(generation.Geography.Signature, RunScenario.PreviewGeography(identity).Signature);
        Assert.Equal(generation.RecoveryPortId, actual.Challenge!.Configuration.PortId);
        Assert.Equal(90, actual.Challenge.Configuration.DeadlineDay);
        foreach (var port in generation.Ports)
        {
            Assert.Equal(Serialize(port), Serialize(actual.Ports[port.Id].Configuration));
            Assert.Equal(port.Settlement!.InitialHouseholdCash, actual.Ports[port.Id].City!.HouseholdCash);
            Assert.Equal(port.Settlement.InitialPopulation, actual.Ports[port.Id].City!.Population);
        }
        Assert.Throws<NotSupportedException>(() => ((IList<RegionalPortConfiguration>)generation.Ports).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<IndustryBlueprint>)generation.Blueprints).Clear());
    }

    [Theory]
    [InlineData(-42, false, RunDifficulty.Normal, 28)]
    [InlineData(42, false, RunDifficulty.SlowFoundations, 28)]
    [InlineData(99, false, RunDifficulty.Normal, 24)]
    [InlineData(-42, true, RunDifficulty.Normal, 44)]
    [InlineData(-42, true, RunDifficulty.SlowFoundations, 48)]
    [InlineData(42, true, RunDifficulty.SlowFoundations, 51)]
    public void GeneratedRecoveryHasPhysicalMerchantAndIndustrialWinningExamples(int seed, bool industry,
        RunDifficulty difficulty, int expectedDay)
    {
        var identity = Identity(seed, challenge: RunScenario.RecoveryChallenge, difficulty: difficulty);
        var generation = LivingCityGeneration.Generate(seed);
        var world = RunScenario.Create(Content(), identity);
        var initialCash = world.Snapshot().TotalCash;
        var player = EnterpriseScenario.PlayerShipId;
        void Step()
        {
            world.AdvanceDay();
            Assert.Equal(initialCash, world.Snapshot().TotalCash);
            Assert.All(world.Snapshot().Ports.Values, port => Assert.InRange(port.City!.FoodWindow.Count, 0, 6));
        }
        if (industry)
        {
            world.ConstructFacility(EnterpriseScenario.PlayerOwnerId, generation.ProcessingPortId,
                EnterpriseScenario.BakeryBlueprint, "recovery_bakery");
            world.ConfigureFacility(EnterpriseScenario.PlayerOwnerId, "recovery_bakery", LivingCityScenario.BakeryPolicy());
            world.Depart(player, generation.ProcessingPortId);
            while (world.Snapshot().Ships[player].DockedPortId == null) Step();
            world.ConfigureIndustrialAutomation(player, LivingCityScenario.IndustrialPolicy(generation, "recovery_bakery"), true);
        }
        else world.ConfigureAutomation(player, LivingCityScenario.MerchantPolicy(generation), true);
        Assert.Equal(initialCash, world.Snapshot().TotalCash);
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) Step();
        var final = world.Snapshot();
        Assert.Equal(ChallengeStatus.Succeeded, final.Challenge!.Status);
        Assert.Equal(expectedDay, final.Day);
        Assert.True(final.Challenge.Recovery!.Qualified);
        Assert.Equal(6, final.Challenge.Recovery.Observations);
        Assert.True(final.Ports[generation.RecoveryPortId].City!.TotalRetailSpend > 0);
        Assert.Equal(identity.ComparisonGroup, new RunResults(final).ComparisonGroup);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Same(final, world.Snapshot());
        if (industry)
        {
            Assert.True(final.PrivateFacilities["recovery_bakery"].BatchesProduced > 0);
            Assert.True(final.Accounting.Companies[EnterpriseScenario.PlayerOwnerId].WagesPaid > 0);
        }
    }

    [Fact]
    public void IdleRecoveryFailsFromMissingGoodsDespiteRemainingHouseholdMoney()
    {
        var identity = Identity(-42, challenge: RunScenario.RecoveryChallenge);
        var world = RunScenario.Create(Content(), identity);
        var generation = LivingCityGeneration.Generate(-42);
        var initialCash = world.Snapshot().TotalCash;
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        var final = world.Snapshot();
        var city = final.Ports[generation.RecoveryPortId].City!;
        Assert.Equal(ChallengeStatus.Failed, final.Challenge!.Status);
        Assert.Equal(90, final.Day);
        Assert.Equal(initialCash, final.TotalCash);
        Assert.True(city.HouseholdCash > 0);
        Assert.True(city.Population < city.InitialPopulation);
        Assert.All(city.FoodWindow, observation =>
        {
            Assert.Equal(0, observation.ConsumedQuantity);
            Assert.Equal(observation.DesiredQuantity, observation.UnmetStock);
            Assert.Equal(0, observation.UnmetAffordability);
        });
        Assert.Equal(0, new RunResults(final).VictoryScore);
        Assert.Equal(identity, RunScenario.Create(Content(), identity).Snapshot().RunIdentity);
    }

    private static string Serialize(object value) => JsonConvert.SerializeObject(value,
        new JsonSerializerSettings { ContractResolver = new QuoteContractResolver() });

    private sealed class QuoteContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
        {
            var properties = base.CreateProperties(type, serialization);
            if (type == typeof(MarketQuote))
                foreach (var property in properties.Where(property => property.PropertyName == "Bid" || property.PropertyName == "Ask"))
                    property.ShouldSerialize = instance => ((MarketQuote)instance).IsFixed;
            return properties;
        }
    }
}
