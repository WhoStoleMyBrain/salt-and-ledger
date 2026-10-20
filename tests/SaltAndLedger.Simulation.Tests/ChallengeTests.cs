using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ChallengeTests
{
    private static ContentCatalog Content() => LegacyContent.Timber(ContentLoader.Load(ContentLoaderTests.CanonicalJson()));
    private static RegionalPortConfiguration Port(string id, long cash = 1000) =>
        new(new PortConfiguration(id, new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 8, 9) },
            cash, Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["logs"] = 20, ["planks"] = 10 }),
            id, 0, 0, Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
    private static RegionalShipConfiguration Ship(string id, string owner, int logs = 0) =>
        new(new ShipConfiguration(id, 20, "east", new Dictionary<string, int> { ["logs"] = logs }),
            owner, id, 100, 100, isPlayer: owner == "player");
    private static ChallengeConfiguration Goal(long quantity = 4, long deadline = 3, long minimumCash = 0) =>
        new("charter", "Supply charter", "player", "east", "logs", quantity, deadline, minimumCash);
    private static RegionalWorldState World(ChallengeConfiguration? goal = null, int cargo = 0,
        long initialDay = 0, long initialRevision = 0, long eastCash = 1000) =>
        new(Content(), new[] { Port("east", eastCash), Port("west") }, new[] { new SeaRoute("east", "west", 100) },
            new[] { Ship("player_ship", "player", cargo), Ship("npc_ship", "npc", 10) },
            initialDay, initialRevision, challenge: goal ?? Goal());
    private static TradeCommand Buy(int quantity, string good = "logs") => new(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(int quantity, string good = "logs") => new(TradeDirection.Sell, good, quantity);

    [Fact]
    public void CompletedOwnerSaleAtDestinationCountsAndFreezesTheFinishedChallenge()
    {
        var world = World(cargo: 4);
        var before = world.Snapshot();
        var result = world.ExecuteTrade("player_ship", "east", Sell(4));
        var challenge = result.After.Challenge!;
        Assert.Equal(4, challenge.NetDeliveredQuantity);
        Assert.Equal(ChallengeStatus.Succeeded, challenge.Status);
        Assert.Equal(0, challenge.CompletedDay);
        Assert.Equal(0, before.Challenge!.DeliveredQuantity);
        Assert.Equal(0, result.After.Day);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("player_ship", "east", Buy(1)));
        Assert.Same(result.After, world.Snapshot());
    }

    [Fact]
    public void BuybacksAndRepeatedSalesCannotCreateCredit()
    {
        var world = World();
        world.ExecuteTrade("player_ship", "east", Buy(6));
        Assert.Equal(-6, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(0, world.Snapshot().Challenge!.DeliveredQuantity);
        world.ExecuteTrade("player_ship", "east", Sell(3));
        world.ExecuteTrade("player_ship", "east", Sell(3));
        Assert.Equal(0, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
        world.AdvanceDay();
        world.ExecuteTrade("player_ship", "east", Buy(2));
        world.ExecuteTrade("player_ship", "east", Sell(2));
        Assert.Equal(0, world.Snapshot().Challenge!.DeliveredQuantity);
    }

    [Fact]
    public void NpcOtherGoodsAndOtherPortsDoNotCount()
    {
        var world = World();
        world.ExecuteTrade("npc_ship", "east", Sell(5));
        world.ExecuteTrade("player_ship", "east", Buy(1, "planks"));
        world.ExecuteTrade("player_ship", "east", Sell(1, "planks"));
        world.Depart("player_ship", "west");
        world.AdvanceDay();
        world.ExecuteTrade("player_ship", "west", Buy(4));
        world.ExecuteTrade("player_ship", "west", Sell(4));
        Assert.Equal(0, world.Snapshot().Challenge!.NetDeliveredQuantity);
    }

    [Fact]
    public void FailedTradeDoesNotCountOrChangeTheSnapshot()
    {
        var world = World(cargo: 4, eastCash: 0);
        var before = world.Snapshot();
        var result = world.ExecuteTrade("player_ship", "east", Sell(4));
        Assert.Equal(TradeOutcome.InsufficientMarketFunds, result.Trades.Single().Trade.Outcome);
        Assert.Same(before, result.After);
        Assert.Equal(0, result.After.Challenge!.DeliveredQuantity);
    }

    [Fact]
    public void WaitingFailsAtTheDeadlineAndResetStartsFresh()
    {
        var world = World();
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
        world.AdvanceDay();
        Assert.Equal((3L, ChallengeStatus.Failed, 3L),
            (world.Snapshot().Day, world.Snapshot().Challenge!.Status, world.Snapshot().Challenge!.CompletedDay));
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        var reset = World().Snapshot();
        Assert.Equal((0L, 0L, ChallengeStatus.Active), (reset.Day, reset.Challenge!.DeliveredQuantity, reset.Challenge.Status));
    }

    [Fact]
    public void DeliveredGoodsAloneDoNotBypassSolvencyCondition()
    {
        var world = World(Goal(minimumCash: 110), cargo: 5);
        world.ExecuteTrade("player_ship", "east", Sell(4));
        Assert.Equal(4, world.Snapshot().Challenge!.DeliveredQuantity);
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
        world.ExecuteTrade("player_ship", "east", Sell(1));
        Assert.Equal(110, world.Snapshot().Ships["player_ship"].Cash);
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
    }

    [Fact]
    public void RevisionOverflowRollsBackDeliveryCashAndInventory()
    {
        var world = World(cargo: 4, initialRevision: long.MaxValue);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.ExecuteTrade("player_ship", "east", Sell(4)));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0, before.Challenge!.DeliveredQuantity);
        Assert.Equal(4, before.Ships["player_ship"].Cargo["logs"]);
    }

    [Fact]
    public void FinalDayAutomaticSaleIsEvaluatedBeforeFailure()
    {
        var world = World(Goal(deadline: 1), cargo: 4);
        var policy = new TradeRoutePolicy("west", "east", new RouteLeg("logs", 4, 1, 10, 1), allowEmptyReturn: true);
        world.ConfigureAutomation("player_ship", policy, true);
        world.AdvanceDay();
        Assert.Equal(1, world.Snapshot().Day);
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(4, world.Snapshot().Challenge!.NetDeliveredQuantity);
    }

    [Fact]
    public void InvalidChallengeReferencesAndExpiredDeadlineAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Goal(quantity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Goal(deadline: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Goal(minimumCash: -1));
        Assert.Throws<ArgumentException>(() => World(initialDay: 3));
        Assert.Throws<ArgumentException>(() => World(new ChallengeConfiguration("c", "Bad owner", "missing", "east", "logs", 4, 3, 0)));
        Assert.Throws<ArgumentException>(() => World(new ChallengeConfiguration("c", "Bad port", "player", "missing", "logs", 4, 3, 0)));
        Assert.Throws<KeyNotFoundException>(() => World(new ChallengeConfiguration("c", "Bad good", "player", "east", "missing", 4, 3, 0)));
    }

    private static RegionalWorldState IndustryWorld(long goal = 2) =>
        new(Content(), new[] { Port("east"), Port("west") }, new[] { new SeaRoute("east", "west", 100) },
            new[] { Ship("player_ship", "player") },
            industryBlueprints: new[] { new IndustryBlueprint("camp", "lumber_camp", "harvest_logs", 10, 1, 20, new[] { "east" }) },
            challenge: Goal(goal, deadline: 3));

    [Fact]
    public void WarehouseProductionAndTransfersGrantNoCreditButItsRealSaleDoes()
    {
        var world = IndustryWorld();
        world.ConstructFacility("player", "east", "camp", "private_camp");
        world.AdvanceDay();
        Assert.Equal(0, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(2, world.Snapshot().PrivateFacilities["private_camp"].Inventory["logs"]);
        world.TransferGoods("player", "player_ship", "private_camp", TransferDirection.WarehouseToShip, "logs", 2);
        world.TransferGoods("player", "player_ship", "private_camp", TransferDirection.ShipToWarehouse, "logs", 2);
        Assert.Equal(0, world.Snapshot().Challenge!.NetDeliveredQuantity);
        world.ExecuteWarehouseTrade("player", "private_camp", Sell(2));
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(2, world.Snapshot().Challenge!.NetDeliveredQuantity);
    }

    [Fact]
    public void WarehousePurchaseAndShipResaleShareTheSameSignedDeliveryCounter()
    {
        var world = IndustryWorld();
        world.ConstructFacility("player", "east", "camp", "private_camp");
        world.ExecuteWarehouseTrade("player", "private_camp", Buy(4));
        world.TransferGoods("player", "player_ship", "private_camp", TransferDirection.WarehouseToShip, "logs", 4);
        world.ExecuteTrade("player_ship", "east", Sell(4));
        Assert.Equal(0, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
    }

    [Fact]
    public void AutomaticPrivateSalesFeedTheAuthoritativeChallenge()
    {
        var world = IndustryWorld();
        world.ConstructFacility("player", "east", "camp", "private_camp");
        world.ConfigureFacility("player", "private_camp",
            new IndustryPolicy(salesEnabled: true, outputs: new[] { new OutputSaleRule("logs", 0, 1) }));
        var result = world.AdvanceDay();
        Assert.Equal(2, result.WarehouseTrades.Single().Trade.ExecutedQuantity);
        Assert.Equal(ChallengeStatus.Succeeded, result.After.Challenge!.Status);
        Assert.Equal(1, result.After.Challenge!.CompletedDay);
    }

    [Fact]
    public void SandboxHasNoChallengeDeadline()
    {
        var world = new RegionalWorldState(Content(), new[] { Port("east"), Port("west") },
            new[] { new SeaRoute("east", "west", 100) }, new[] { Ship("player_ship", "player") });
        for (var day = 0; day < 65; day++) world.AdvanceDay();
        Assert.Null(world.Snapshot().Challenge);
        Assert.Equal(65, world.Snapshot().Day);
    }
}
