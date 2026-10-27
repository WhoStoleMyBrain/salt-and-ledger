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
    public void PrescribedExpansionPaysForRealFacilitiesAndProducesCanonicalGoods(string strategy)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var row = ExpandingEconomyExperiments.RunCase(content, 42, LivingPricingMode.StockOnly,
            RunDifficulty.Normal, strategy, 30);
        Assert.True(row.Final.PrivateProduced!["grain"] > 0);
        Assert.True(row.Final.PrivateProduced["flour"] > 0);
        Assert.True(row.Final.PrivateProduced["bread"] > 0);
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
        Assert.True(row.Final.PrivateProduced!["silver"] > 0);
        Assert.True(row.Final.SilverReserve > 0);
        Assert.True(row.Final.ExternalInflow > 0);
        Assert.True(row.Final.PublicWorksPlanks > 0);
        Assert.Equal(row.Final.Minted, row.Final.SupplierIssuance + row.Final.TreasuryIssuance);
        Assert.Equal(2, row.Final.PlayerShips);
        Assert.Equal(2, row.SaveChecks.Count);
    }

    [Fact]
    public void MatchedNoMintControlCannotIssueMoneyOrSurrenderTheSameMetal()
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), 42,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "monetary_control", 60);
        Assert.True(row.Final.PrivateProduced!["silver"] > 0);
        Assert.Equal(0, row.Final.Minted);
        Assert.Equal(0, row.Final.SilverReserve);
        Assert.Equal(0, row.Final.TreasurySpent);
    }


    [Fact]
    public void LocalEmploymentAndPhysicalInputsSustainMeasuredPaidRecoveryWithoutCurrencyCreation()
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), 42,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "circulation", 90);
        var target = ExpandingEconomyGeneration.Generate(42).RecoveryPortId;
        var town = JsonSerializer.SerializeToElement(row.Final.Towns).EnumerateArray().Single(t => t.GetProperty("Id").GetString() == target);
        Assert.Equal(100, town.GetProperty("RecoveryFulfillmentPercent").GetInt32());
        Assert.True(town.GetProperty("Population").GetInt32() >= town.GetProperty("InitialPopulation").GetInt32());
        Assert.True(town.GetProperty("HouseholdCash").GetInt64() > 0);
        Assert.True(row.Final.ValueChange > 0);
        Assert.True(row.Final.PrivateProduced!["flour"] > 0);
        Assert.True(row.Final.PrivateProduced["bread"] > 0);
        Assert.Equal(0, row.Final.Minted);
        Assert.Equal(0, row.Final.ExternalInflow);
        Assert.DoesNotContain("food_funds/" + target, row.FirstProblems.Keys);
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
    [InlineData(42, 47, 1027219)]
    [InlineData(99, 49, 1025280)]
    public void FixedReinvestmentPreservesMeasuredFoodShortfallDespiteRealProductiveInvestment(int seed, int flour, long cash)
    {
        var row = ExpandingEconomyExperiments.RunCase(ContentLoader.Load(ContentLoaderTests.CanonicalJson()), seed,
            LivingPricingMode.StockOnly, RunDifficulty.Normal, "reinvestment", 365);
        var generation = ExpandingEconomyGeneration.Generate(seed);
        var target = generation.ProcessingPortId;
        var town = JsonSerializer.SerializeToElement(row.Final.Towns).EnumerateArray().Single(t => t.GetProperty("Id").GetString() == target);
        // These prescribed policies are controls, not proof of sustainable recovery.
        // Under current pooled/circulation rules both processing towns lack paid food at year end.
        Assert.Equal(0, town.GetProperty("RecoveryFulfillmentPercent").GetInt32());
        Assert.True(town.GetProperty("Population").GetInt32() < town.GetProperty("InitialPopulation").GetInt32());
        Assert.True(town.GetProperty("HouseholdCash").GetInt64() > 0);
        Assert.Equal(3, row.Final.PlayerShips);
        Assert.Equal(EconomicUnits.Coins(1920), row.Final.Minted);
        Assert.Equal(EconomicUnits.FromWhole(24), row.Final.SilverReserve);
        Assert.Equal(EconomicUnits.FromWhole(flour), row.Final.PrivateProduced!["flour"]);
        Assert.Equal(EconomicUnits.FromWhole(104), row.Final.PrivateProduced["bread"]);
        Assert.Equal(cash, row.Final.Cash);
        Assert.True(row.Final.ValueChange > 0);
        Assert.Equal(4, row.SaveChecks.Count);
    }
}
