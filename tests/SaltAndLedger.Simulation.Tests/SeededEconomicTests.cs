using System;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class SeededEconomicTests
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;

    [Theory]
    [InlineData(99, 28, 39)]
    [InlineData(-42, 43, 28)]
    public void GeographyReversesWhichPrescribedStrategyCompletesCoastalSupplySooner(int seed, int merchantDay, int industrialDay)
    {
        var merchant = Play(seed, "merchant");
        var industrial = Play(seed, "industrial");
        Assert.Equal(ChallengeStatus.Succeeded, merchant.Challenge!.Status);
        Assert.Equal(ChallengeStatus.Succeeded, industrial.Challenge!.Status);
        Assert.Equal(merchantDay, merchant.Day);
        Assert.Equal(industrialDay, industrial.Day);
        Assert.Equal(0, merchant.Accounting.Companies[Owner].ConstructionExpenditure);
        Assert.Equal(300, industrial.Accounting.Companies[Owner].ConstructionExpenditure);
        Assert.True(merchant.Companies[Owner].Cash > industrial.Companies[Owner].Cash);
        Assert.True(industrial.Accounting.Companies[Owner].RealizedGrossMargin > merchant.Accounting.Companies[Owner].RealizedGrossMargin);
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(-42)]
    [InlineData(99)]
    public void AscensionDoesNotChangeACompanyThatOrdersNoConstruction(int seed)
    {
        foreach (var strategy in new[] { "merchant", "industrial" })
        {
            var charter = strategy == "industrial" ? RunScenario.IndustrialCharter : RunScenario.MerchantCharter;
            var normal = Play(seed, strategy, charter: charter);
            var ascended = Play(seed, strategy, RunDifficulty.SlowFoundations, charter);
            Assert.Equal(ChallengeStatus.Succeeded, ascended.Challenge!.Status);
            Assert.Equal(normal.Day, ascended.Day);
            Assert.Equal(JsonConvert.SerializeObject(normal.Accounting), JsonConvert.SerializeObject(ascended.Accounting));
            Assert.Equal(JsonConvert.SerializeObject(normal.PrivateFacilities), JsonConvert.SerializeObject(ascended.PrivateFacilities));
            Assert.NotEqual(new RunResults(normal).ComparisonGroup, new RunResults(ascended).ComparisonGroup);
        }
    }

    [Theory]
    [InlineData(RunDifficulty.Normal)]
    [InlineData(RunDifficulty.SlowFoundations)]
    public void LocalManufacturingCannotReplacePhysicalFoodDistribution(RunDifficulty difficulty)
    {
        var local = Play(99, "local_factory", difficulty, challenge: RunScenario.ReliefChallenge);
        var shipping = Play(99, "industrial", difficulty, challenge: RunScenario.ReliefChallenge);
        Assert.Equal(ChallengeStatus.Failed, local.Challenge!.Status);
        Assert.Equal(60, local.Day);
        Assert.Equal(22, local.Challenge.ObjectiveQuantity);
        Assert.Equal(ChallengeStatus.Succeeded, shipping.Challenge!.Status);
        Assert.True(local.Accounting.Companies[Owner].SalesRevenue > 0);
        Assert.True(local.Accounting.Companies[Owner].RealizedGrossMargin > 0);
        Assert.Equal(300, local.Accounting.Companies[Owner].ConstructionExpenditure);
    }

    [Theory]
    [InlineData(RunScenario.MerchantCharter)]
    [InlineData(RunScenario.IndustrialCharter)]
    [InlineData(RunScenario.IndependentCharter)]
    public void SeededAscensionHasMeasuredVictoryAndFailureWithoutChangingObjectiveRules(string charter)
    {
        var winning = Play(int.MinValue, "industrial", RunDifficulty.SlowFoundations, charter, RunScenario.AmbitionChallenge);
        var trading = Play(int.MinValue, "merchant", RunDifficulty.SlowFoundations, charter, RunScenario.AmbitionChallenge);
        Assert.Equal(ChallengeStatus.Succeeded, winning.Challenge!.Status);
        Assert.True(winning.Challenge.ObjectiveQuantity >= 12);
        Assert.True(winning.Accounting.Companies[Owner].RealizedGrossMargin >= 200);
        Assert.Equal(ChallengeStatus.Failed, trading.Challenge!.Status);
        Assert.True(trading.Accounting.Companies[Owner].RealizedGrossMargin > 200);
        Assert.True(trading.Challenge.ObjectiveQuantity < 12);
        Assert.Equal(60, trading.Day);
    }

    private static RegionalWorldSnapshot Play(int seed, string strategy, RunDifficulty difficulty = RunDifficulty.Normal,
        string charter = RunScenario.MerchantCharter, string challenge = RunScenario.CoastalChallenge)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var world = RunScenario.Create(content, new RunIdentity(challenge, charter, RunWorldMode.Seeded, seed, difficulty));
        var initial = world.Snapshot();
        foreach (var cargo in initial.Ships[Player].Cargo.Where(item => item.Value > 0))
            world.ExecuteTrade(Player, initial.Ships[Player].DockedPortId!, new TradeCommand(TradeDirection.Sell, cargo.Key, cargo.Value));
        if (strategy == "merchant")
        {
            SailTo(EnterpriseScenario.North);
            world.SetAutomationEnabled(Player, true);
        }
        else
        {
            var facility = initial.PrivateFacilities.ContainsKey(RunScenario.StarterBakery) ? RunScenario.StarterBakery : "economic_bakery";
            if (facility != RunScenario.StarterBakery)
                world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, facility);
            world.ConfigureFacility(Owner, facility, EnterpriseScenario.BakeryPolicy(strategy == "local_factory"));
            if (strategy == "industrial")
            {
                SailTo(EnterpriseScenario.West);
                world.ConfigureIndustrialAutomation(Player, RunScenario.BakeryShippingPolicy(facility), true);
            }
        }
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        var final = world.Snapshot();
        var finance = final.Accounting.Companies[Owner];
        Assert.Equal(finance.InitialCash + finance.SalesRevenue - finance.InventoryPurchases - finance.ConstructionExpenditure, final.Companies[Owner].Cash);
        Assert.Equal(finance.InventoryPurchases, finance.CostOfGoodsSold + finance.HeldInventoryCost);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Same(final, world.Snapshot());
        return final;

        void SailTo(string destination)
        {
            if (world.Snapshot().Ships[Player].DockedPortId == destination) return;
            world.Depart(Player, destination);
            while (world.Snapshot().Ships[Player].DockedPortId == null) world.AdvanceDay();
        }
    }
}
