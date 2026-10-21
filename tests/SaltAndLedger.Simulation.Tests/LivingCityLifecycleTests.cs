using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class LivingCityLifecycleTests
{
    [Fact]
    public void RecoveryNeedsSixPaidOpportunitiesNotAnInstantShipmentAndFreezesAtVictory()
    {
        var world = World(bread: 0, challenge: true, prosperity: 60, cargo: 20);
        var initial = world.Snapshot();
        Assert.NotNull(initial.Challenge!.Recovery);
        Assert.Same(initial, world.SetAutomationEnabled("ship", false).After);
        world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "bread", 20));
        Assert.Equal(0, world.Snapshot().Challenge!.ObjectiveQuantity);
        for (var day = 1; day < 12; day++)
        {
            var after = world.AdvanceDay().After;
            Assert.Equal(ChallengeStatus.Active, after.Challenge!.Status);
            Assert.Equal(day / 2, after.Challenge.Recovery!.Observations);
        }
        var result = world.AdvanceDay();
        Assert.Equal(ChallengeStatus.Succeeded, result.After.Challenge!.Status);
        Assert.Equal(6, result.After.Challenge.Recovery!.Observations);
        Assert.Equal(100, result.After.Challenge.Recovery.FulfillmentPercent);
        Assert.Equal(12, result.After.Challenge.CompletedDay);
        Assert.Equal(initial.TotalCash, result.After.TotalCash);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility("owner", "town", "bakery", "site"));
        Assert.Same(result.After, world.Snapshot());
    }

    [Fact]
    public void ShrinkingPopulationCannotEraseUnmetBaselineNeeds()
    {
        var world = World(bread: 0, population: 21, prosperity: 10, cargo: 20, challenge: true, deadline: 90);
        for (var day = 0; day < 36; day++) world.AdvanceDay();
        Assert.Equal(18, world.Snapshot().Ports["town"].City!.Population);
        world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "bread", 20));
        for (var day = 0; day < 12; day++) world.AdvanceDay();
        var city = world.Snapshot().Ports["town"].City!;
        Assert.Equal(100, city.EssentialFulfillmentPercent);
        Assert.Equal(50, city.RecoveryFulfillmentPercent);
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
        Assert.False(world.Snapshot().Challenge!.Recovery!.Qualified);
        Assert.True(city.Population < city.InitialPopulation);
    }

    [Fact]
    public void ProsperityAndPopulationReactSlowlyAndGrowthChangesNextNeed()
    {
        var world = World(bread: 100, prosperity: 70);
        var first = world.AdvanceDay().After.Ports["town"].City!;
        Assert.Equal(20, first.Population);
        Assert.Equal(70, first.Prosperity);
        for (var day = 1; day < 12; day++) world.AdvanceDay();
        var grown = world.Snapshot().Ports["town"].City!;
        Assert.Equal(21, grown.Population);
        Assert.Equal(1, grown.PopulationDelta);
        Assert.InRange(grown.Prosperity, 70, 88);
        Assert.Equal(2, world.Snapshot().PreviewHouseholdConsumption("town", "food").RequestedQuantity);
        Assert.Equal(6, grown.FoodWindow.Count);
        for (var day = 12; day < 24; day++) world.AdvanceDay();
        Assert.Equal(22, world.Snapshot().Ports["town"].City!.Population);
        Assert.Equal(6, world.Snapshot().Ports["town"].City!.FoodWindow.Count);
    }

    [Fact]
    public void LuxuryNeedsSustainedWelfareAndStopsAfterSustainedDeprivation()
    {
        var world = World(bread: 100, prosperity: 70);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        Assert.False(world.Snapshot().Ports["town"].City!.LuxuryEnabled);
        world.AdvanceDay();
        var active = world.AdvanceDay();
        Assert.True(active.After.Ports["town"].City!.LuxuryEnabled);
        Assert.Equal(0, active.Consumption.Single(c => c.GoodId == "furniture").ConsumedQuantity);
        for (var day = 6; day < 12; day++) world.AdvanceDay();
        Assert.True(world.Snapshot().Ports["town"].Consumption.Single(c => c.Configuration.GoodId == "furniture").TotalConsumedQuantity > 0);
        // An ordinary company purchase removes the remaining food, transferring real money to market.
        var stock = world.Snapshot().Ports["town"].Inventory["bread"];
        world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Buy, "bread", stock));
        world.AdvanceDay();
        Assert.True(world.Snapshot().Ports["town"].City!.LuxuryEnabled);
        for (var day = 13; day < 90; day++) world.AdvanceDay();
        var poor = world.Snapshot().Ports["town"].City!;
        Assert.False(poor.LuxuryEnabled);
        Assert.True(poor.Population < poor.InitialPopulation);
        Assert.InRange(poor.Prosperity, 0, 40);
    }

    [Fact]
    public void RetailReceiverOverflowRollsBackEarlierPublicProductionAndWages()
    {
        // Two wages create only two units of headroom; population41 needs3 Bread costing6.
        var overflow = World(bread: 100, marketCash: long.MaxValue, publicWage: 1, foodAsk: 2, population: 41);
        overflow.AdvanceDay();
        var before = overflow.Snapshot();
        var fingerprint = JsonConvert.SerializeObject(before);
        Assert.Throws<OverflowException>(() => overflow.AdvanceDay());
        Assert.Same(before, overflow.Snapshot());
        Assert.Equal(fingerprint, JsonConvert.SerializeObject(overflow.Snapshot()));
    }

    [Fact]
    public void HouseholdWageOverflowRollsBackBatchAndLeavesEveryAccountUntouched()
    {
        var world = World(householdCash: long.MaxValue, publicWage: 1);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0, before.Ports["town"].Inventory["grain"]);
    }

    [Fact]
    public void RecoveryDeadlineAndAbandonmentAreHonestTerminalOutcomes()
    {
        var failed = World(bread: 0, challenge: true, deadline: 12);
        for (var day = 0; day < 12; day++) failed.AdvanceDay();
        Assert.Equal(ChallengeStatus.Failed, failed.Snapshot().Challenge!.Status);
        Assert.Equal(0, failed.Snapshot().Challenge!.Recovery!.FulfillmentPercent);
        var abandoned = World(challenge: true);
        var ending = abandoned.AbandonChallenge().After;
        Assert.Equal(ChallengeStatus.Abandoned, ending.Challenge!.Status);
        Assert.NotNull(ending.Challenge.Recovery);
        Assert.Throws<InvalidOperationException>(() => abandoned.AdvanceDay());
        Assert.Same(ending, abandoned.Snapshot());
    }

    private static RegionalWorldState World(int bread = 0, long householdCash = 1000,
        int population = 20, int prosperity = 60, long marketCash = 1000, long publicWage = 0,
        long foodAsk = 2, int cargo = 0, bool challenge = false, long deadline = 90)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var quotes = content.Goods.Select(g => new MarketQuote(g.Id, 1, g.Id == "bread" ? foodAsk : 2)).ToArray();
        var facilities = publicWage == 0 ? Array.Empty<ScheduledFacilityConfiguration>() : new[] {
            new ScheduledFacilityConfiguration(new BuildingInstance("farm", "farm", "harvest_grain"), 1, wagePerBatch: publicWage) };
        var port = new RegionalPortConfiguration(new PortConfiguration("town", quotes, marketCash,
            facilities.Select(f => f.Instance), new Dictionary<string, int> { ["bread"] = bread, ["furniture"] = 20 }),
            "Town", 0, 0, facilities, new[] {
                new ConsumptionConfiguration("food", "bread", 1, 2, kind: ConsumptionKind.Essential, populationPerUnit: 20),
                new ConsumptionConfiguration("luxury", "furniture", 1, 6, kind: ConsumptionKind.Luxury, populationPerUnit: 80) },
            settlement: new SettlementConfiguration(population, householdCash, prosperity));
        var other = new RegionalPortConfiguration(new PortConfiguration("other", quotes, 1000, Array.Empty<BuildingInstance>()),
            "Other", 100, 100, Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
        var ship = new RegionalShipConfiguration(new ShipConfiguration("ship", 200, "town",
            new Dictionary<string, int> { ["bread"] = cargo }), "owner", "Ship", 1000, 100, isPlayer: true);
        return new RegionalWorldState(content, new[] { port, other }, new[] { new SeaRoute("town", "other", 100) },
            new[] { ship }, challenge: challenge ? new ChallengeConfiguration("recovery", "Recovery", "owner", "town",
                "bread", 90, deadline, 100, ChallengeKind.CityRecovery) : null);
    }
}
