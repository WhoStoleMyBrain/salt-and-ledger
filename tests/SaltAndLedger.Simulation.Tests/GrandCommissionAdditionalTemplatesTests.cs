using System;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionAdditionalTemplatesTests
{
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Ship = EnterpriseScenario.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World(int seed, string? navigation = null, string? industry = null, string? civic = null)
    {
        var draft = GrandCommissionDraft.Generate(seed);
        var ids = draft.Rounds.Select(r => r.Offers[0].TemplateId).ToArray();
        if (navigation != null) ids[0] = navigation;
        if (industry != null) ids[1] = industry;
        if (civic != null) ids[3] = civic;
        return RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, seed, commission: draft.Resolve(ids)));
    }
    private static CommissionGoalProgress Goal(RegionalWorldState world, string id) =>
        GrandCommission.Progress(world.Snapshot()).Goals.Single(g => g.Id == id);
    private static void Days(RegionalWorldState world, int count)
    { for (var index = 0; index < count; index++) world.AdvanceDay(); }
    private static void Sail(RegionalWorldState world, string port)
    {
        world.Depart(Ship, port);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
    }
    private static void BuyInput(RegionalWorldState world, string facility, string good, int whole) =>
        Assert.Equal(TradeOutcome.Traded, world.ExecuteWarehouseTrade(Owner, facility,
            new TradeCommand(TradeDirection.Buy, good, whole * 10000)).WarehouseTrades.Single().Trade.Outcome);
    private static void Trade(RegionalWorldState world, TradeDirection direction, string good, int whole) =>
        Assert.Equal(TradeOutcome.Traded, world.ExecuteTrade(Ship, world.Snapshot().Ships[Ship].DockedPortId!,
            new TradeCommand(direction, good, whole * 10000)).Trades.Single().Trade.Outcome);

    [Fact]
    public void RegionalSurveyRequiresTheThreeResolvedRolesAndRetainsTheirRealVisits()
    {
        var world = World(-42, navigation: "regional_survey");
        var generation = ExpandingEconomyGeneration.Generate(-42);
        Assert.Equal(0, Goal(world, "regional_survey").Conditions.Single().Current);
        Sail(world, generation.ProcessingPortId);
        Sail(world, generation.FurniturePortId);
        Assert.Equal(2, Goal(world, "regional_survey").Conditions.Single().Current);
        Assert.False(Goal(world, "regional_survey").IsMet);
        world.Depart(Ship, generation.RecoveryPortId);
        Assert.Equal(2, Goal(world, "regional_survey").Conditions.Single().Current);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
        Assert.True(Goal(world, "regional_survey").IsPermanent);
        Sail(world, generation.FoodSourcePortId);
        Assert.Equal(3, Goal(world, "regional_survey").Conditions.Single().Current);
        Assert.Equal(100, Goal(world, "regional_survey").Percent);
    }

    [Fact]
    public void OpenRoutesNeedsRemoteNetSalesBeyondThreeVisitsAndBuybackCanReverseOnlyTrade()
    {
        var world = World(-42, navigation: "open_routes");
        var generation = ExpandingEconomyGeneration.Generate(-42);
        Sail(world, generation.ProcessingPortId);
        Trade(world, TradeDirection.Buy, "flour", 1);
        Sail(world, generation.FurniturePortId);
        Assert.False(Goal(world, "open_routes").IsMet);
        var visited = Goal(world, "open_routes").Conditions.Single(c => c.Semantic == CommissionSemantic.Cumulative);
        Assert.True(visited.IsPermanent);
        Assert.Equal(3, visited.Current);
        Trade(world, TradeDirection.Sell, "flour", 1);
        Assert.True(Goal(world, "open_routes").IsMet);
        Assert.False(Goal(world, "open_routes").IsPermanent);
        Trade(world, TradeDirection.Buy, "flour", 1);
        Assert.False(Goal(world, "open_routes").IsMet);
        Assert.True(Goal(world, "open_routes").Conditions.Single(c => c.Semantic == CommissionSemantic.Cumulative).IsPermanent);
    }

    [Fact]
    public void BreadworksNeedsDifferentTownsEvenAfterTwoLocalBakeriesProduceEnoughBread()
    {
        var world = World(-42, industry: "breadworks");
        var generation = ExpandingEconomyGeneration.Generate(-42);
        foreach (var id in new[] { "breadworks_one", "breadworks_two" })
        {
            world.ConstructFacility(Owner, generation.FoodSourcePortId, EnterpriseScenario.BakeryBlueprint, id);
            BuyInput(world, id, "flour", 1);
            BuyInput(world, id, "logs", 1);
        }
        Days(world, 2);
        var local = Goal(world, "breadworks");
        Assert.Equal(40000, local.Conditions.Single(c => c.Id == "production").Current);
        Assert.True(local.Conditions.Single(c => c.Id == "production").IsPermanent);
        Assert.Equal(1, local.Conditions.Single(c => c.Id == "bakeries").Current);
        Assert.False(local.IsMet);
        world.ConstructFacility(Owner, generation.RecoveryPortId, EnterpriseScenario.BakeryBlueprint, "breadworks_coast");
        BuyInput(world, "breadworks_coast", "flour", 1);
        BuyInput(world, "breadworks_coast", "logs", 1);
        Days(world, 2);
        Assert.True(world.Snapshot().PrivateFacilities["breadworks_coast"].WagesPaid > 0);
        Assert.True(Goal(world, "breadworks").IsMet);
        Assert.False(Goal(world, "breadworks").IsPermanent);
    }

    [Fact]
    public void ArtisanGuildCountsPrivateFurnitureAfterRealPlanksProductionAndPhysicalDelivery()
    {
        var world = World(42, industry: "artisan_guild");
        var generation = ExpandingEconomyGeneration.Generate(42);
        world.ConstructFacility(Owner, generation.ProcessingPortId, EnterpriseScenario.SawmillBlueprint, "guild_sawmill");
        world.ConstructFacility(Owner, generation.FurniturePortId, EnterpriseScenario.WorkshopBlueprint, "guild_workshop");
        BuyInput(world, "guild_sawmill", "logs", 8);
        Days(world, 8);
        Assert.Equal(40000, world.Snapshot().PrivateFacilities["guild_sawmill"].Inventory["planks"]);
        Assert.Equal(0, Goal(world, "artisan_guild").Conditions.Single(c => c.Id == "production").Current);
        Assert.False(Goal(world, "artisan_guild").IsMet);
        Sail(world, generation.ProcessingPortId);
        world.TransferGoods(Owner, Ship, "guild_sawmill", TransferDirection.WarehouseToShip, "planks", 40000);
        Sail(world, generation.FurniturePortId);
        world.TransferGoods(Owner, Ship, "guild_workshop", TransferDirection.ShipToWarehouse, "planks", 40000);
        while (world.Snapshot().PrivateFacilities["guild_workshop"].BatchesProduced < 2)
        {
            Assert.True(world.Snapshot().Day < 35);
            world.AdvanceDay();
        }
        Assert.Equal(160000, world.Snapshot().PrivateFacilities["guild_workshop"].WagesPaid);
        Assert.True(Goal(world, "artisan_guild").Conditions.Single(c => c.Id == "production").IsPermanent);
        Assert.True(Goal(world, "artisan_guild").IsMet);
        Assert.False(Goal(world, "artisan_guild").IsPermanent);
    }

    [Fact]
    public void PublicWorksRequiresFundedPlanksAndSeparateRecentPaidFood()
    {
        var world = World(-42, civic: "public_works");
        var generation = ExpandingEconomyGeneration.Generate(-42);
        world.CommissionMint(Owner);
        Trade(world, TradeDirection.Buy, "bread", 8);
        Sail(world, generation.ProcessingPortId);
        Trade(world, TradeDirection.Buy, "planks", 2);
        Assert.Equal(DevelopmentOutcome.Completed, world.SupplyPublicWorks(Ship, 20000).Development.Single().Outcome);
        var funded = Goal(world, "public_works");
        Assert.True(funded.Conditions.Single(c => c.Id == "public_planks").IsPermanent);
        Assert.False(funded.IsMet);
        Sail(world, generation.RecoveryPortId);
        while (world.Snapshot().Ships[Ship].Cargo["bread"] > 0)
        {
            Assert.True(world.Snapshot().Day < 50);
            var possible = world.Snapshot().MaximumTradeQuantity(Ship, generation.RecoveryPortId, TradeDirection.Sell, "bread") / 10000;
            if (possible > 0) Trade(world, TradeDirection.Sell, "bread", possible);
            else world.AdvanceDay();
        }
        while (!Goal(world, "public_works").IsMet && world.Snapshot().Day < 70) world.AdvanceDay();
        Assert.True(Goal(world, "public_works").IsMet);
        Assert.False(Goal(world, "public_works").IsPermanent);
        Assert.Equal(6, world.Snapshot().Ports[generation.RecoveryPortId].City!.FoodWindow.Count);
        Assert.True(world.Snapshot().Ports[generation.RecoveryPortId].City!.RecoveryFulfillmentPercent >= 60);
        while (Goal(world, "public_works").IsMet && world.Snapshot().Day < 130) world.AdvanceDay();
        Assert.False(Goal(world, "public_works").IsMet);
        Assert.True(Goal(world, "public_works").Conditions.Single(c => c.Id == "public_planks").IsPermanent);
    }
}
