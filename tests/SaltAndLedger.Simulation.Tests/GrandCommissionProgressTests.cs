using System;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionProgressTests
{
    private const string Owner = WorldIds.PlayerOwnerId;
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World(int seed = -42, string charter = RunScenario.MerchantCharter,
        string? navigation = null, string? industry = null, string? commerce = null, string? civic = null,
        RunDifficulty difficulty = RunDifficulty.Normal)
    {
        var draft = GrandCommissionDraft.Generate(seed);
        var choices = new[] { navigation, industry, commerce, civic };
        var plan = draft.Resolve(draft.Rounds.Select((round, i) => choices[i] ?? round.Offers[0].TemplateId));
        return RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId, charter,
            RunWorldMode.ExpandingEconomy, seed, difficulty, commission: plan));
    }
    private static CommissionGoalProgress Goal(RegionalWorldState world, string id) => GrandCommission.Progress(world.Snapshot()).Goals.Single(g => g.Id == id);
    private static void Days(RegionalWorldState world, int count) { for (var i = 0; i < count; i++) world.AdvanceDay(); }
    private static void Sail(RegionalWorldState world, string port, string ship = Ship)
    {
        world.Depart(ship, port);
        while (world.Snapshot().Ships[ship].DockedPortId == null) world.AdvanceDay();
    }
    private static TradeCommand Buy(string good, int whole) => new(TradeDirection.Buy, good, whole * 10000);
    private static TradeCommand Sell(string good, int whole) => new(TradeDirection.Sell, good, whole * 10000);

    [Fact]
    public void NewIndustryNeedsFundedBatchesInBothSelectedFacilitiesAtDifferentTowns()
    {
        var world = World(charter: RunScenario.IndustrialCharter, industry: "new_industry");
        var g = ExpandingEconomyGeneration.Generate(-42);
        world.ConstructFacility(Owner, g.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, "new_mill");
        world.ExecuteWarehouseTrade(Owner, "new_mill", Buy("grain", 2));
        Days(world, 2);
        Assert.True(world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].BatchesProduced > 0);
        Assert.True(world.Snapshot().PrivateFacilities["new_mill"].BatchesProduced > 0);
        Assert.False(Goal(world, "new_industry").IsMet);
        Assert.Equal(0, Goal(world, "new_industry").Conditions.Single(c => c.Id == "bakery").Current);
        world.ConstructFacility(Owner, g.FoodSourcePortId, WorldIds.BakeryBlueprint, "other_bakery");
        Assert.Equal(0, Goal(world, "new_industry").Conditions.Single(c => c.Id == "bakery_batch").Current);
        world.ExecuteWarehouseTrade(Owner, "other_bakery", Buy("flour", 2));
        world.ExecuteWarehouseTrade(Owner, "other_bakery", Buy("logs", 2));
        Days(world, 2);
        var progress = Goal(world, "new_industry");
        Assert.True(progress.IsMet);
        Assert.False(progress.IsPermanent);
        Assert.All(progress.Conditions.Where(c => c.Id.EndsWith("batch", StringComparison.Ordinal)), c =>
        { Assert.True(c.IsPermanent); Assert.True(c.Current >= 1); });
        Assert.Contains("other_bakery", progress.Conditions.Single(c => c.Id == "bakery_batch").Label);
        Assert.True(world.Snapshot().PrivateFacilities["other_bakery"].WagesPaid > 0);
    }

    [Fact]
    public void PendingAndUnfundedProductionNeverMasqueradeAsCompletedBatches()
    {
        var world = World(industry: "new_industry", difficulty: RunDifficulty.SlowFoundations);
        var g = ExpandingEconomyGeneration.Generate(-42);
        world.ConstructFacility(Owner, g.ProcessingPortId, ExpandingEconomyGeneration.MillBlueprint, "mill");
        world.ConstructFacility(Owner, g.FoodSourcePortId, WorldIds.BakeryBlueprint, "bakery");
        Assert.All(Goal(world, "new_industry").Conditions, c => Assert.False(c.IsMet));
        Days(world, 2);
        var completed = Goal(world, "new_industry");
        Assert.True(completed.Conditions.Single(c => c.Id == "mill").IsMet);
        Assert.True(completed.Conditions.Single(c => c.Id == "bakery").IsMet);
        Days(world, 2);
        Assert.False(Goal(world, "new_industry").IsMet);
        Assert.Equal(0, world.Snapshot().PrivateFacilities["mill"].BatchesProduced);
        Assert.Equal(0, world.Snapshot().PrivateFacilities["bakery"].WagesPaid);
    }

    [Fact]
    public void LocalBuyResaleCancelsImportedSupplyCountsAndBuybackReversesExactProgress()
    {
        var world = World(commerce: "flour_for_bakers"); var g = ExpandingEconomyGeneration.Generate(-42);
        var initial = world.Snapshot();
        initial.PreviewTrade(Ship, g.FoodSourcePortId, Buy("flour", 4));
        Assert.Same(initial, world.Snapshot()); Assert.Empty(initial.CommissionHistory!.Markets);
        world.ExecuteTrade(Ship, g.FoodSourcePortId, Buy("flour", 4));
        var negative = Goal(world, "flour_for_bakers").Conditions.Single();
        Assert.Equal(-40000, negative.Current); Assert.Equal(0, negative.Percent); Assert.Equal("-4", negative.CurrentText);
        world.ExecuteTrade(Ship, g.FoodSourcePortId, Sell("flour", 4));
        Assert.Equal(0, Goal(world, "flour_for_bakers").Conditions.Single().Current);
        Sail(world, g.ProcessingPortId);
        world.ExecuteTrade(Ship, g.ProcessingPortId, Buy("flour", 4));
        Assert.Equal(0, Goal(world, "flour_for_bakers").Conditions.Single().Current);
        Sail(world, g.FoodSourcePortId);
        world.ExecuteTrade(Ship, g.FoodSourcePortId, Sell("flour", 4));
        Assert.True(Goal(world, "flour_for_bakers").IsMet);
        Assert.False(Goal(world, "flour_for_bakers").IsPermanent);
        world.ExecuteTrade(Ship, g.FoodSourcePortId, Buy("flour", 1));
        Assert.False(Goal(world, "flour_for_bakers").IsMet);
        Assert.Equal(75, Goal(world, "flour_for_bakers").Percent);
    }

    [Fact]
    public void WarehouseFractionalTradesUseSameSignedTargetHistoryAndNpcDaysDoNotAwardCredit()
    {
        var world = World(commerce: "flour_for_bakers"); var g = ExpandingEconomyGeneration.Generate(-42);
        world.ConstructFacility(Owner, g.FoodSourcePortId, WorldIds.BakeryBlueprint, "store");
        world.SetFacilityEnabled(Owner, "store", false);
        world.ExecuteWarehouseTrade(Owner, "store", new TradeCommand(TradeDirection.Buy, "flour", 12345));
        Assert.Equal(-12345, Goal(world, "flour_for_bakers").Conditions.Single().Current);
        Assert.Equal("-1.2345", Goal(world, "flour_for_bakers").Conditions.Single().CurrentText);
        world.ExecuteWarehouseTrade(Owner, "store", new TradeCommand(TradeDirection.Sell, "flour", 12345));
        Assert.Equal(0, Goal(world, "flour_for_bakers").Conditions.Single().Current);
        Days(world, 12);
        Assert.Equal(0, Goal(world, "flour_for_bakers").Conditions.Single().Current);
        Assert.Single(world.Snapshot().CommissionHistory!.Markets);
    }

    [Fact]
    public void VisitsRequireRealArrivalAndSecondShipCannotWinByRemainingAtShipyard()
    {
        var world = World(42, navigation: "second_command"); var g = ExpandingEconomyGeneration.Generate(42);
        Assert.Single(world.Snapshot().CommissionHistory!.Ships.Single().Visits);
        var initial = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.Depart(Ship, g.FoodSourcePortId));
        Assert.Same(initial, world.Snapshot());
        world.Depart(Ship, g.ProcessingPortId);
        Assert.Single(world.Snapshot().CommissionHistory!.Ships.Single().Visits);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
        Assert.Equal(2, world.Snapshot().CommissionHistory!.Ships.Single().Visits.Count);
        world.CommissionShip(Owner, g.MintPortId); Days(world, 4);
        Assert.Equal(2, world.Snapshot().CommissionHistory!.Ships.Count);
        var second = world.Snapshot().CommissionHistory!.Ships[1];
        Assert.Equal(1, second.AcquisitionOrder); Assert.Single(second.Visits);
        Assert.False(Goal(world, "second_command").IsMet);
        Sail(world, g.FoodSourcePortId, second.ShipId);
        Assert.True(Goal(world, "second_command").IsMet);
        Assert.Equal(2, world.Snapshot().CommissionHistory!.Ships[1].Visits.Count);
    }

    [Fact]
    public void CompanyValueGoalCanFallWhileCompletedVisitsStayPermanent()
    {
        var world = World(42, navigation: "all_harbors", industry: "timber_chain", civic: "prosperous_enterprise");
        var g = ExpandingEconomyGeneration.Generate(42);
        world.ConstructFacility(Owner, g.FoodSourcePortId, ExpandingEconomyGeneration.LumberBlueprint, "lumber");
        foreach (var port in world.Snapshot().Ports.Keys.Where(p => p != g.FoodSourcePortId).ToArray()) Sail(world, port);
        while (!Goal(world, "prosperous_enterprise").IsMet && world.Snapshot().Day < 18) world.AdvanceDay();
        Assert.True(Goal(world, "all_harbors").IsPermanent);
        Assert.True(Goal(world, "prosperous_enterprise").IsMet);
        world.CommissionMint(Owner);
        Assert.False(Goal(world, "prosperous_enterprise").IsMet);
        Assert.True(Goal(world, "all_harbors").IsPermanent);
        Assert.InRange(GrandCommission.Progress(world.Snapshot()).ObjectiveScore, 0, 500);
    }

    [Fact]
    public void PaidFoodWindowCanRecoverThenFallAfterRealDeliveredStockRunsOut()
    {
        var world = World(commerce: "flour_for_bakers", civic: "city_back_in_trade");
        var generation = ExpandingEconomyGeneration.Generate(-42);
        world.ExecuteTrade(Ship, generation.FoodSourcePortId, Buy("bread", 10));
        Sail(world, generation.RecoveryPortId);
        var first = world.Snapshot().MaximumTradeQuantity(Ship, generation.RecoveryPortId, TradeDirection.Sell, "bread") / 10000;
        Assert.True(first > 0);
        world.ExecuteTrade(Ship, generation.RecoveryPortId, Sell("bread", first));
        Days(world, 4);
        var remaining = world.Snapshot().Ships[Ship].Cargo["bread"] / 10000;
        if (remaining > 0) world.ExecuteTrade(Ship, generation.RecoveryPortId, Sell("bread", remaining));
        Assert.False(Goal(world, "city_back_in_trade").IsMet);
        while (!Goal(world, "city_back_in_trade").IsMet && world.Snapshot().Day < 60) world.AdvanceDay();
        Assert.True(Goal(world, "city_back_in_trade").IsMet, string.Join("; ", Goal(world, "city_back_in_trade").Conditions.Select(c => c.Label + " " + c.CurrentText)));
        Assert.False(Goal(world, "city_back_in_trade").IsPermanent);
        var city = world.Snapshot().Ports[generation.RecoveryPortId].City!;
        Assert.Equal(6, city.FoodWindow.Count);
        Assert.True(city.FoodWindow.Count(observation => observation.Payment > 0) >= 5);
        Assert.True(city.RecoveryFulfillmentPercent >= 80);
        while (Goal(world, "city_back_in_trade").IsMet && world.Snapshot().Day < 100) world.AdvanceDay();
        Assert.False(Goal(world, "city_back_in_trade").IsMet);
        Assert.Contains("below target", Goal(world, "city_back_in_trade").Reason);
    }

    [Fact]
    public void IdleDeadlineAndAbandonmentPreserveTruePartialProgressAndFreeze()
    {
        var world = World(); Days(world, 180);
        Assert.Equal(ChallengeStatus.Failed, world.Snapshot().Challenge!.Status);
        Assert.False(GrandCommission.Progress(world.Snapshot()).Qualified);
        var terminal = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay()); Assert.Same(terminal, world.Snapshot());
        var abandoned = World(); var initial = GrandCommission.Progress(abandoned.Snapshot()).ObjectiveScore;
        abandoned.AbandonChallenge();
        Assert.Equal(ChallengeStatus.Abandoned, abandoned.Snapshot().Challenge!.Status);
        Assert.Equal(initial, GrandCommission.Progress(abandoned.Snapshot()).ObjectiveScore);
        Assert.Throws<InvalidOperationException>(() => abandoned.AdvanceDay());
    }
}
