using System.Linq;
using System.Text.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingExperimentTests
{
    [Theory]
    [InlineData("industry")]
    [InlineData("network2")]
    public void PrescribedExpansionPaysForRealFacilitiesAndRecordsProductionOrCompetingInputShortage(string strategy)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var row = ExpandingEconomyExperiments.RunCase(content, 42, LivingPricingMode.StockOnly,
            RunDifficulty.Normal, strategy, 30);
        Assert.True(row.Final.PrivateProduced!["grain"] > 0);
        Assert.True(row.Final.PrivateProduced["flour"] > 0);
        Assert.True(row.Final.PrivateProduced.TryGetValue("bread", out var bread) && bread > 0 ||
            row.Attempts.Any(a => a.Key == "private/experiment_bakery/bake_bread/InputShortage" && a.Value > 0));
        Assert.True(row.Final.Wages > 0);
        Assert.Equal(0, row.Final.Minted);
        Assert.Contains(row.Flows, f => f.Key.StartsWith("voyages/") && f.Value > 0);
        if (strategy == "network2")
        {
            Assert.Equal(2, row.Final.PlayerShips);
            Assert.Equal(2, row.SaveChecks.Count);
        }
    }

    [Fact]
    public void MoneyExperimentUsesPhysicalSilverFiniteExportsAndRealPublicWorks()
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), 42,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "monetary", 60);
        Assert.True(row.Final.PrivateProduced!["silver"] > 0, "private Silver production");
        Assert.True(row.Final.SilverReserve > 0, "physical Silver delivery");
        Assert.True(row.Final.ExternalInflow > 0, "finite contract payment");
        Assert.True(row.Final.PublicWorksPlanks > 0, "physical public works Planks");
        Assert.Equal(row.Final.Minted, row.Final.SupplierIssuance + row.Final.TreasuryIssuance);
        Assert.Equal(2, row.Final.PlayerShips);
        Assert.Equal(2, row.SaveChecks.Count);
    }

    [Fact]
    public void MatchedNoMintControlCannotIssueMoneyOrSurrenderTheSameMetal()
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), 42,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "monetary_control", 60);
        Assert.True(row.Final.PrivateProduced!["silver"] > 0, "private Silver production");
        Assert.Equal(0, row.Final.Minted);
        Assert.Equal(0, row.Final.SilverReserve);
        Assert.Equal(0, row.Final.TreasurySpent);
    }


    [Fact]
    public void SeparatedLocalEmploymentRecordsRealFoodAndInputShortagesWithoutCurrencyCreation()
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), 42,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "circulation", 90);
        var generation = ExpandingEconomyGeneration.Generate(42);
        var target = generation.FirstEligiblePort("bake_bread");
        var town = JsonSerializer.SerializeToElement(row.Final.Towns).EnumerateArray().Single(t => t.GetProperty("Id").GetString() == target);
        Assert.InRange(town.GetProperty("RecoveryFulfillmentPercent").GetInt32(), 0, 100);
        Assert.True(town.GetProperty("TotalWagesReceived").GetInt64() > 0);
        Assert.True(town.GetProperty("TotalRetailSpend").GetInt64() > 0);
        Assert.True(row.Final.PrivateProduced!["flour"] > 0);
        Assert.True(row.Attempts["private/experiment_bakery/bake_bread/InputShortage"] > 0);
        Assert.Equal(0, row.Final.Minted);
        Assert.Equal(0, row.Final.ExternalInflow);
        Assert.Equal(row.Initial.GlobalCash, row.Final.GlobalCash);
        Assert.Equal(row.Initial.RegionalCash, row.Final.RegionalCash);
        var facilities = JsonSerializer.SerializeToElement(row.Final.Facilities).EnumerateArray().ToArray();
        string Port(string building) => facilities.Single(f => f.GetProperty("BuildingId").GetString() == building && f.GetProperty("OwnerId").GetString() == WorldIds.PlayerOwnerId).GetProperty("PortId").GetString()!;
        Assert.NotEqual(Port("bakery"), Port("mill"));
        Assert.NotEqual(Port("bakery"), Port("lumber_camp"));
        Assert.NotEqual(Port("farm"), Port("mill"));
        Assert.Contains(Port("bakery"), generation.EligiblePorts("bake_bread"));
    }

    [Fact]
    public void ThreeFundedShipsRunDistinctPhysicalRoutesAndSaveExactly()
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), 42,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "fleet3", 30);
        Assert.Equal(3, row.Final.PlayerShips);
        Assert.True(row.Flows["voyages/player_ship"] > 0);
        Assert.True(row.Flows["voyages/player_ship_2"] > 0);
        Assert.True(row.Flows["voyages/player_ship_3"] > 0);
        Assert.Equal(2, row.SaveChecks.Count);
        Assert.Equal(0, row.Final.Minted);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(99)]
    public void YearLongReinvestmentRetainsFiniteIssuanceRealGoodsAndExactContinuation(int seed)
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), seed,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "reinvestment", 365);
        var generation = ExpandingEconomyGeneration.Generate(seed);
        Assert.Equal(365, row.Final.Day);
        Assert.InRange(row.Final.Towns.Length, 8, 12);
        Assert.InRange(row.Final.PlayerShips, 1, generation.Configuration.FleetLimit);
        Assert.InRange(row.Final.Minted, 0, EconomicUnits.Coins(1920));
        Assert.Equal(row.Final.Minted, row.Final.SupplierIssuance + row.Final.TreasuryIssuance);
        Assert.Equal(row.Final.Minted, row.Final.SilverReserve * 80);
        Assert.True(row.Final.PrivateProduced!["silver"] >= row.Final.SilverReserve);
        Assert.True(row.Final.Wages > 0);
        Assert.True(row.Final.Cash >= 0);
        Assert.Equal(4, row.SaveChecks.Count);
        Assert.Contains(row.Flows, pair => pair.Key.StartsWith("voyages/") && pair.Value > 0);
        var towns = JsonSerializer.SerializeToElement(row.Final.Towns).EnumerateArray().ToArray();
        Assert.All(towns, town =>
        {
            Assert.True(town.GetProperty("MarketCash").GetInt64() >= 0);
            Assert.True(town.GetProperty("HouseholdCash").GetInt64() >= 0);
            Assert.InRange(town.GetProperty("RecoveryFulfillmentPercent").GetInt32(), 0, 100);
            foreach (var need in town.GetProperty("Needs").EnumerateArray())
                Assert.True(need.GetProperty("TotalConsumedQuantity").GetInt64() <= need.GetProperty("TotalDesiredQuantity").GetInt64());
        });
        Assert.True(towns.Sum(town => town.GetProperty("TotalRetailSpend").GetInt64()) > 0);
    }
}
