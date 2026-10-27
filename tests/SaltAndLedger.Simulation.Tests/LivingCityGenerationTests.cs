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
                generated.Blueprints.Single(blueprint => blueprint.Id == WorldIds.SawmillBlueprint).EligiblePortIds);
            Assert.All(generated.Blueprints, blueprint =>
            {
                Assert.Equal(blueprint.BuildingId, content.GetRecipe(blueprint.RecipeId).BuildingId);
                Assert.All(blueprint.EligiblePortIds, id => Assert.Contains(generated.Ports, port => port.Id == id));
            });
        }
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
