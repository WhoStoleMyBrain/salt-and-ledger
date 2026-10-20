using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class RegionalWorldTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static Dictionary<string, int> Stock(int logs = 0, int planks = 0) =>
        new() { ["logs"] = logs, ["planks"] = planks };
    private static RegionalPortConfiguration Port(string id, int logs = 10, int planks = 10,
        long cash = 1000, long logBid = 2, long logAsk = 3) =>
        new(new PortConfiguration(id, new[] { new MarketQuote("logs", logBid, logAsk), new MarketQuote("planks", 8, 9) },
            cash, Array.Empty<BuildingInstance>(), Stock(logs, planks)), id, 0, 0,
            Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
    private static TradeRoutePolicy Policy(long cap = long.MaxValue, long reserve = 0) =>
        new("north", "south", new RouteLeg("logs", 5, 2, 10, 1, cap),
            new RouteLeg("planks", 2, 1, 10, 1), reserve, true);
    private static RegionalShipConfiguration Ship(string id = "player_ship", long cash = 100,
        int capacity = 8, bool enabled = false, TradeRoutePolicy? policy = null, int priority = 0,
        int logs = 0, int threshold = 0) =>
        new(new ShipConfiguration(id, capacity, "north", Stock(logs)), id + "_owner", id, cash, 100,
            priority, id == "player_ship", policy ?? Policy(), enabled, threshold);
    private static RegionalWorldState World(IEnumerable<RegionalPortConfiguration>? ports = null,
        IEnumerable<RegionalShipConfiguration>? ships = null, IEnumerable<SeaRoute>? routes = null,
        long day = 0, long revision = 0) =>
        new(Content(), ports ?? new[] { Port("north"), Port("south", logBid: 7, logAsk: 8) },
            routes ?? new[] { new SeaRoute("north", "south", 250) }, ships ?? new[] { Ship() }, day, revision);
    private static TradeCommand Buy(int quantity = 1, string good = "logs") => new(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(int quantity = 1, string good = "logs") => new(TradeDirection.Sell, good, quantity);
    private static BigInteger Cash(RegionalWorldSnapshot snapshot) =>
        snapshot.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.MarketCash) +
        snapshot.Ships.Values.Aggregate(BigInteger.Zero, (sum, s) => sum + s.Cash);
    private static long Goods(RegionalWorldSnapshot snapshot, string good) =>
        snapshot.Ports.Values.Sum(p => (long)p.Inventory[good]) + snapshot.Ships.Values.Sum(s => (long)s.Cargo[good]);
    private static string Fingerprint(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
    {
        snapshot.Day, snapshot.Revision,
        Ports = snapshot.Ports.Values.Select(p => new { p.Id, p.Inventory, p.MarketCash,
            Prices = p.Quotes.Keys.Select(g => snapshot.GetMarketQuote(p.Id, g)), p.Facilities, p.Consumption }),
        Ships = snapshot.Ships.Values.Select(s => new { s.Id, s.OwnerId, s.Cash, s.Ship, s.VoyageDurationDays, s.AutomationEnabled })
    });

    [Fact]
    public void SeveralDocksideTradesCommitNewRevisionsWithoutAProductionOrTravelDay()
    {
        var world = World();
        var initial = world.Snapshot();
        var buy = world.ExecuteTrade("player_ship", "north", Buy(4));
        Assert.Equal((0L, 1L, 4, 88L), (buy.After.Day, buy.After.Revision,
            buy.After.Ships["player_ship"].CargoUnits, buy.After.Ships["player_ship"].Cash));
        var sell = world.ExecuteTrade("player_ship", "north", Sell(2));
        Assert.Equal((0L, 2L, 2, 92L), (sell.After.Day, sell.After.Revision,
            sell.After.Ships["player_ship"].CargoUnits, sell.After.Ships["player_ship"].Cash));
        Assert.Empty(sell.Production);
        Assert.Empty(sell.Consumption);
        Assert.Empty(sell.Movements);
        Assert.Equal(10, initial.Ports["north"].Inventory["logs"]);
        Assert.Equal(0, initial.Ships["player_ship"].CargoUnits);
        Assert.Equal(Cash(initial), Cash(sell.After));
        Assert.Equal(Goods(initial, "logs"), Goods(sell.After, "logs"));
    }

    [Fact]
    public void ExactDynamicPreviewUpdatesImmediatelyAndInspectionIsPassive()
    {
        var world = ArchipelagoScenario.Create(Content());
        var initial = world.Snapshot();
        var quote = initial.GetMarketQuote(ArchipelagoScenario.North, "logs");
        var preview = initial.PreviewTrade(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.North, Buy(6));
        var result = world.ExecuteTrade(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.North, Buy(6));
        Assert.Equal(preview.ExecutedAmount, result.Trades.Single().Trade.ExecutedAmount);
        Assert.Equal(0L, result.After.Day);
        Assert.True(result.After.GetMarketQuote(ArchipelagoScenario.North, "logs").Ask > quote.Ask);
        Assert.Equal(quote.Ask, initial.GetMarketQuote(ArchipelagoScenario.North, "logs").Ask);
        var current = world.Snapshot();
        current.PreviewTrade(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.East, Sell(6));
        current.GetMarketQuote(ArchipelagoScenario.East, "planks");
        world.EvaluateAutomation(ArchipelagoScenario.PlayerShipId);
        Assert.Same(current, world.Snapshot());
    }

    [Theory]
    [InlineData(0, 100, 8, 1, TradeOutcome.InsufficientMarketInventory)]
    [InlineData(10, 0, 8, 1, TradeOutcome.InsufficientTraderFunds)]
    [InlineData(10, 100, 2, 3, TradeOutcome.InsufficientCargoCapacity)]
    public void ExpectedTradeRejectionPreservesSnapshotAndRevision(int stock, long cash,
        int capacity, int quantity, TradeOutcome outcome)
    {
        var world = World(ports: new[] { Port("north", logs: stock), Port("south") },
            ships: new[] { Ship(cash: cash, capacity: capacity) });
        var initial = world.Snapshot();
        var result = world.ExecuteTrade("player_ship", "north", Buy(quantity));
        Assert.Equal(outcome, result.Trades.Single().Trade.Outcome);
        Assert.Same(initial, world.Snapshot());
        Assert.Same(initial, result.After);
    }

    [Fact]
    public void InsufficientCargoAndMarketFundsAreNonMutatingSaleRejections()
    {
        var world = World(ports: new[] { Port("north", cash: 0), Port("south") },
            ships: new[] { Ship(logs: 2) });
        var before = world.Snapshot();
        Assert.Equal(TradeOutcome.InsufficientTraderInventory,
            world.ExecuteTrade("player_ship", "north", Sell(3)).Trades.Single().Trade.Outcome);
        Assert.Equal(TradeOutcome.InsufficientMarketFunds,
            world.ExecuteTrade("player_ship", "north", Sell(2)).Trades.Single().Trade.Outcome);
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void InvalidRemoteSailingAndOverflowCommandsPreserveCompleteState()
    {
        var world = World(ports: new[] { Port("north", cash: long.MaxValue), Port("south") });
        var initial = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.ExecuteTrade("player_ship", "north", Buy()));
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("player_ship", "south", Buy()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Buy(0));
        Assert.Throws<KeyNotFoundException>(() => world.Depart("player_ship", "missing"));
        Assert.Throws<InvalidOperationException>(() => world.Depart("player_ship", "north"));
        Assert.Same(initial, world.Snapshot());
        world.Depart("player_ship", "south");
        var sailing = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("player_ship", "north", Sell()));
        Assert.Throws<InvalidOperationException>(() => world.Depart("player_ship", "north"));
        Assert.Same(sailing, world.Snapshot());
    }

    [Fact]
    public void DepartIsInstantAndArrivalPermitsTradingOnTheArrivalDay()
    {
        var world = World(ships: new[] { Ship(logs: 2) });
        var departure = world.Depart("player_ship", "south");
        Assert.Equal((0L, 3), (departure.After.Day, departure.After.Ships["player_ship"].RemainingTravelDays));
        Assert.Null(departure.After.Ships["player_ship"].DockedPortId);
        Assert.Single(departure.Movements);
        Assert.Empty(departure.Production);
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Null(world.Snapshot().Ships["player_ship"].DockedPortId);
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("player_ship", "south", Sell(2)));
        var arrival = world.AdvanceDay();
        Assert.Equal((3L, "south"), (arrival.After.Day, arrival.After.Ships["player_ship"].DockedPortId));
        Assert.Contains(arrival.Movements, m => m.Kind == ShipMovementKind.Arrival);
        var sale = world.ExecuteTrade("player_ship", "south", Sell(2));
        Assert.Equal(3L, sale.After.Day);
        Assert.Equal(TradeOutcome.Traded, sale.Trades.Single().Trade.Outcome);
        Assert.Equal(0, sale.After.Ships["player_ship"].CargoUnits);
    }

    [Theory]
    [InlineData(200, 100, 2)]
    [InlineData(201, 100, 3)]
    [InlineData(600, 100, 6)]
    [InlineData(1, 100, 1)]
    [InlineData(int.MaxValue, int.MaxValue, 1)]
    public void RoutesUseExactCeilingDistanceOverSpeed(int distance, int speed, int duration)
    {
        Assert.Equal(duration, new SeaRoute("north", "south", distance).DurationDays(speed));
    }

    [Fact]
    public void DockCountsAndAllCargoFollowActualShipLocations()
    {
        var world = World(ships: new[] { Ship(), Ship("npc", logs: 3) });
        Assert.Equal(2, world.Snapshot().DockedShips("north").Count);
        world.Depart("npc", "south");
        Assert.Single(world.Snapshot().DockedShips("north"));
        Assert.Empty(world.Snapshot().DockedShips("south"));
        var sourceStock = world.Snapshot().Ports["south"].Inventory["logs"];
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        Assert.Single(world.Snapshot().DockedShips("south"));
        Assert.Equal(sourceStock, world.Snapshot().Ports["south"].Inventory["logs"]);
        Assert.Equal(3, world.Snapshot().Ships["npc"].Cargo["logs"]);
    }

    [Fact]
    public void SameDayStateChangeInvalidatesAPreviouslyEvaluatedDecision()
    {
        var world = World(ships: new[] { Ship(enabled: true), Ship("manual") });
        var decision = world.EvaluateAutomation("player_ship");
        world.ExecuteTrade("manual", "north", Buy());
        Assert.Equal(decision.Before.Day, world.Snapshot().Day);
        Assert.NotEqual(decision.Before.Revision, world.Snapshot().Revision);
        var current = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteAutomation(decision));
        Assert.Same(current, world.Snapshot());
    }

    [Fact]
    public void DecisionsFromAnotherWorldAndSnapshotMutationAreRejected()
    {
        var first = World(ships: new[] { Ship(enabled: true) });
        var second = World(ships: new[] { Ship(enabled: true) });
        var snapshot = first.Snapshot();
        Assert.Throws<InvalidOperationException>(() => first.ExecuteAutomation(second.EvaluateAutomation("player_ship")));
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, RegionalPortSnapshot>)snapshot.Ports).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)snapshot.Ships["player_ship"].Cargo)["logs"] = 100);
        Assert.Throws<NotSupportedException>(() => ((IList<SeaRoute>)snapshot.Routes).Clear());
        Assert.Same(snapshot, first.Snapshot());
    }

    [Fact]
    public void ExceptionalSecondDocksideActionRollsBackTheEarlierSale()
    {
        var vessel = new RegionalShipConfiguration(new ShipConfiguration("player_ship", 8, "south", Stock(2)),
            "player", "Ledger", 100, 100, policy: Policy(), automationEnabled: true);
        var world = World(ports: new[] { Port("north"), Port("south", cash: long.MaxValue) },
            ships: new[] { vessel });
        var before = world.Snapshot();
        Assert.Equal(AutomationAction.Sell, world.EvaluateAutomation("player_ship").Action);
        Assert.Throws<OverflowException>(() => world.RunAutomation("player_ship"));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(2, world.Snapshot().Ships["player_ship"].Cargo["logs"]);
        Assert.Equal(long.MaxValue, world.Snapshot().Ports["south"].MarketCash);
    }

    [Fact]
    public void PolicyChangesAndPauseAreInstantAndInvalidateOldDecisions()
    {
        var world = World(ships: new[] { Ship(enabled: true) });
        var decision = world.EvaluateAutomation("player_ship");
        var initial = world.Snapshot();
        world.ConfigureAutomation("player_ship", Policy(cap: 8), true);
        Assert.Equal(0L, world.Snapshot().Day);
        Assert.Equal(Cash(initial), Cash(world.Snapshot()));
        Assert.Equal(Goods(initial, "logs"), Goods(world.Snapshot(), "logs"));
        Assert.Throws<InvalidOperationException>(() => world.ExecuteAutomation(decision));
        Assert.Equal(2, world.EvaluateAutomation("player_ship").SelectedQuantity);
        world.SetAutomationEnabled("player_ship", false);
        Assert.Equal(AutomationAction.Pause, world.EvaluateAutomation("player_ship").Action);
        var paused = world.Snapshot();
        Assert.Same(paused, world.RunAutomation("player_ship").After);
        Assert.Equal(0L, world.Snapshot().Day);
        world.ExecuteTrade("player_ship", "north", Buy());
    }

    [Fact]
    public void ManualAndAutomatedOrdersCannotMix()
    {
        var world = World(ships: new[] { Ship(enabled: true) });
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("player_ship", "north", Buy()));
        Assert.Throws<InvalidOperationException>(() => world.Depart("player_ship", "south"));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void ExpenditureAndReserveLimitTheExactSelectedPurchase()
    {
        var world = World(ships: new[] { Ship(enabled: true, policy: Policy(cap: 8)) });
        var result = world.RunAutomation("player_ship");
        Assert.Equal(2, result.Trades.Single().Trade.ExecutedQuantity);
        Assert.Equal(6L, result.Trades.Single().Trade.ExecutedAmount);
        Assert.Equal(0L, result.After.Day);
        var reserved = World(ships: new[] { Ship(enabled: true, policy: Policy(reserve: 95)) });
        Assert.Equal(AutomationReason.CashReserve, reserved.EvaluateAutomation("player_ship").Detail!.Reason);
        Assert.Same(reserved.Snapshot(), reserved.RunAutomation("player_ship").After);
        var capped = World(ships: new[] { Ship(enabled: true, policy: Policy(cap: 5)) });
        Assert.Equal(AutomationReason.PurchaseExpenditureLimit, capped.EvaluateAutomation("player_ship").Detail!.Reason);
    }

    [Fact]
    public void AReturnSalePurchaseAndDepartureCanShareOneDay()
    {
        var world = World(ships: new[] { Ship(enabled: true) });
        world.RunAutomation("player_ship");
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        var arrival = world.Snapshot();
        Assert.Equal("south", arrival.Ships["player_ship"].DockedPortId);
        var dockside = world.RunAutomation("player_ship");
        Assert.Equal(3L, dockside.After.Day);
        Assert.Equal(arrival.Revision + 1, dockside.After.Revision);
        Assert.Equal(new[] { TradeDirection.Sell, TradeDirection.Buy }, dockside.Trades.Select(t => t.Trade.Direction));
        Assert.Equal("north", dockside.After.Ships["player_ship"].DestinationPortId);
        Assert.Equal(2, dockside.After.Ships["player_ship"].Cargo["planks"]);
        Assert.Equal(0, dockside.After.Ships["player_ship"].Cargo["logs"]);
    }

    [Fact]
    public void BoundedRunnerRejectsUnlimitedCountsAndWaitsWithoutMutation()
    {
        var world = World(ships: new[] { Ship(enabled: true, threshold: 20) });
        var before = world.Snapshot();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RunAutomation("player_ship", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RunAutomation("player_ship", 5));
        var result = world.RunAutomation("player_ship");
        Assert.Single(result.Decisions);
        Assert.Equal(AutomationAction.Wait, result.Decisions[0].Action);
        Assert.Contains("20", result.Decisions[0].Reason);
        Assert.Same(before, result.After);
    }

    [Fact]
    public void SourceThresholdPreventsToppingUpAnAlreadyQualifyingCargo()
    {
        var world = World(ships: new[] { Ship(enabled: true, logs: 2, threshold: 20) });
        var result = world.RunAutomation("player_ship");
        Assert.Empty(result.Trades);
        Assert.Equal(2, result.After.Ships["player_ship"].CargoUnits);
        Assert.Equal("south", result.After.Ships["player_ship"].DestinationPortId);
    }

    [Theory]
    [InlineData(0, AutomationAction.Wait)]
    [InlineData(2, AutomationAction.Depart)]
    public void AThresholdForbiddenPurchaseDoesNotPreviewAnOverflowingTransfer(int cargo, AutomationAction action)
    {
        var world = World(ports: new[] { Port("north", logs: 5, cash: long.MaxValue), Port("south") },
            ships: new[] { Ship(enabled: true, logs: cargo, threshold: 10) });
        Assert.Equal(action, world.EvaluateAutomation("player_ship").Action);
        var result = world.AdvanceDay();
        Assert.Empty(result.Trades);
        Assert.Equal(long.MaxValue, result.After.Ports["north"].MarketCash);
        Assert.Equal(cargo, result.After.Ships["player_ship"].CargoUnits);
    }

    [Fact]
    public void ReturnStockThresholdRespectsTheExplicitEmptyReturnPolicy()
    {
        var ship = new RegionalShipConfiguration(new ShipConfiguration("player_ship", 8, "south"),
            "player", "Ledger", 100, 100, policy: Policy(), automationEnabled: true, minimumReturnStock: 20);
        var world = World(ships: new[] { ship });
        var result = world.RunAutomation("player_ship");
        Assert.Empty(result.Trades);
        Assert.Equal("north", result.After.Ships["player_ship"].DestinationPortId);
        Assert.Equal(0, result.After.Ships["player_ship"].CargoUnits);
    }

    [Fact]
    public void ABlockedMerchantRetainsCargoAndCashWhileDaysContinue()
    {
        var world = World(ports: new[] { Port("north"), Port("south", cash: 0) },
            ships: new[] { Ship(enabled: true) });
        world.RunAutomation("player_ship");
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        var blocked = world.Snapshot().Ships["player_ship"];
        for (var day = 0; day < 8; day++)
        {
            var result = world.AdvanceDay();
            Assert.Empty(result.Trades);
            Assert.Equal(AutomationReason.InsufficientMarketFunds, result.Decisions.Single().Detail!.Reason);
            Assert.Equal(blocked.Cash, result.After.Ships["player_ship"].Cash);
            Assert.Equal(5, result.After.Ships["player_ship"].Cargo["logs"]);
        }
    }

    [Fact]
    public void CompetingActorsUsePriorityThenOrdinalIdAndShareFiniteStock()
    {
        RegionalWorldState Create(bool reverse) => World(
            ports: new[] { Port("north", logs: 5), Port("south") },
            ships: reverse ? new[] { Ship("b", enabled: true), Ship("a", enabled: true) } :
                new[] { Ship("a", enabled: true), Ship("b", enabled: true) });
        var world = Create(false);
        var result = world.AdvanceDay();
        Assert.Equal("a", result.Trades.Single().ShipId);
        Assert.Equal(5, result.After.Ships["a"].CargoUnits);
        Assert.Equal(0, result.After.Ships["b"].CargoUnits);
        Assert.Equal(Fingerprint(result.After), Fingerprint(Create(true).AdvanceDay().After));
        var priority = World(ports: new[] { Port("north", logs: 5), Port("south") },
            ships: new[] { Ship("a", enabled: true, priority: 10), Ship("b", enabled: true, priority: 0) });
        Assert.Equal("b", priority.AdvanceDay().Trades.Single().ShipId);
    }

    [Fact]
    public void SameCommandsGroupedAsDifferentPlaybackSpeedsProduceIdenticalState()
    {
        string Run(int chunk)
        {
            var world = ArchipelagoScenario.Create(Content());
            world.SetAutomationEnabled(ArchipelagoScenario.PlayerShipId, true);
            for (var start = 0; start < 60; start += chunk)
                for (var offset = 0; offset < chunk && start + offset < 60; offset++) world.AdvanceDay();
            return Fingerprint(world.Snapshot());
        }
        Assert.Equal(Run(1), Run(3));
        Assert.Equal(Run(1), Run(10));
    }

    [Fact]
    public void ReorderedRegionalConfigurationProducesTheSameDailyStates()
    {
        var forward = ArchipelagoScenario.Create(Content());
        var reverse = ArchipelagoScenario.Create(Content(), true);
        for (var day = 0; day < 60; day++)
            Assert.Equal(Fingerprint(forward.AdvanceDay().After), Fingerprint(reverse.AdvanceDay().After));
    }

    [Fact]
    public void CashAndCargoStayFiniteAcrossRepeatedNpcDeliveries()
    {
        var world = ArchipelagoScenario.Create(Content());
        var cash = Cash(world.Snapshot());
        var deliveries = new Dictionary<string, int>();
        for (var day = 0; day < 60; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(cash, Cash(result.After));
            foreach (var ship in result.After.Ships.Values)
            {
                Assert.InRange(ship.CargoUnits, 0, ship.CargoCapacity);
                Assert.True(ship.Cash >= 0);
            }
            foreach (var trade in result.Trades.Where(t => t.Trade.Direction == TradeDirection.Sell))
                deliveries[trade.ShipId] = deliveries.GetValueOrDefault(trade.ShipId) + 1;
        }
        Assert.True(deliveries[ArchipelagoScenario.TimberShipId] >= 2);
        Assert.True(deliveries[ArchipelagoScenario.PlankShipId] >= 2);
    }

    [Fact]
    public void RevisionAndDayOverflowCannotCommitInstantOrDailyWork()
    {
        var revision = World(revision: long.MaxValue);
        var before = revision.Snapshot();
        Assert.Throws<OverflowException>(() => revision.ExecuteTrade("player_ship", "north", Buy()));
        Assert.Throws<OverflowException>(() => revision.AdvanceDay());
        Assert.Same(before, revision.Snapshot());
        var day = World(day: long.MaxValue);
        before = day.Snapshot();
        Assert.Throws<OverflowException>(() => day.AdvanceDay());
        Assert.Same(before, day.Snapshot());
        day.ExecuteTrade("player_ship", "north", Buy());
        Assert.Equal(long.MaxValue, day.Snapshot().Day);
    }

    [Fact]
    public void PolicyOutsideCurrentRoutePausesAndInvalidConfigurationIsAtomic()
    {
        var world = World(ports: new[] { Port("north"), Port("south"), Port("east") },
            routes: new[] { new SeaRoute("north", "south", 100), new SeaRoute("south", "east", 100) });
        var policy = new TradeRoutePolicy("south", "east", new RouteLeg("planks", 3, 1, 20, 1), allowEmptyReturn: true);
        world.ConfigureAutomation("player_ship", policy, true);
        Assert.Equal(AutomationAction.Pause, world.EvaluateAutomation("player_ship").Action);
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConfigureAutomation("player_ship",
            new TradeRoutePolicy("north", "east", new RouteLeg("logs", 2, 1, 5, 1), allowEmptyReturn: true), true));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void PortConnectionsAndAccountOwnershipAreValidated()
    {
        Assert.Throws<ArgumentException>(() => World(routes: new[] {
            new SeaRoute("north", "south", 10), new SeaRoute("south", "north", 20) }));
        Assert.Throws<ArgumentException>(() => World(routes: new[] { new SeaRoute("north", "missing", 10) }));
        var first = Ship();
        var second = new RegionalShipConfiguration(new ShipConfiguration("other", 3, "north"),
            first.OwnerId, "Other", 10, 100);
        Assert.Throws<ArgumentException>(() => World(ships: new[] { first, second }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RouteLeg("logs", 3, 1, 10, 1, 0));
    }
}
