using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class MaritimeWorldTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static Dictionary<string, int> Stock(int logs = 0, int planks = 0) =>
        new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks };
    private static BuildingInstance Mill(string id = "mill", int priority = 0, bool enabled = true) =>
        new BuildingInstance(id, "sawmill", "saw_planks", priority, enabled);
    private static InventoryPricing Curve(InventoryPricingModel model = InventoryPricingModel.Linear) =>
        new InventoryPricing(model, 10, 10, 10, 1, 100, 1);
    private static MarketQuote[] Fixed(long logBid = 2, long logAsk = 3) =>
        new[] { new MarketQuote("logs", logBid, logAsk), new MarketQuote("planks", 4, 5) };
    private static PortConfiguration Port(string id = "northport", int logs = 10, int planks = 10,
        long cash = 1000, IEnumerable<BuildingInstance>? buildings = null, IEnumerable<MarketQuote>? quotes = null) =>
        new PortConfiguration(id, quotes ?? Fixed(), cash, buildings ?? Array.Empty<BuildingInstance>(), Stock(logs, planks));
    private static MaritimeWorldState World(IEnumerable<PortConfiguration>? ports = null, int capacity = 10,
        int cargoLogs = 0, int cargoPlanks = 0, long cash = 1000, int travelTicks = 2, long tick = 0,
        string initialPort = "northport") =>
        new MaritimeWorldState(Content(), ports ?? new[] { Port(), Port("southhaven", logs: 0, quotes: Fixed(8, 9)) },
            "trader", cash, new ShipConfiguration("ship", capacity, initialPort, Stock(cargoLogs, cargoPlanks)), travelTicks, tick);
    private static PortTradeCommand Buy(int quantity = 1, string port = "northport", string good = "logs") =>
        new PortTradeCommand(port, TradeDirection.Buy, good, quantity);
    private static PortTradeCommand Sell(int quantity = 1, string port = "northport", string good = "logs") =>
        new PortTradeCommand(port, TradeDirection.Sell, good, quantity);
    private static SailCommand[] Sail(string destination = "southhaven") => new[] { new SailCommand(destination) };

    private static string Fingerprint(MaritimeWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
    {
        snapshot.Tick,
        snapshot.TraderId,
        snapshot.TraderCash,
        snapshot.TravelTicks,
        snapshot.Ship,
        Ports = snapshot.Ports.Values.Select(port => new
        {
            port.Id, port.Inventory, port.MarketCash, port.Buildings,
            Prices = port.Quotes.Keys.Select(good => snapshot.GetMarketQuote(port.Id, good)).ToArray()
        }).ToArray()
    });

    private static string Fingerprint(MaritimeStepResult result) => JsonConvert.SerializeObject(new
    {
        result.Tick, After = Fingerprint(result.After), result.TradeAttempts, result.ProductionAttempts, result.MovementEvents
    });

    private static void SameTrade(TradeAttempt expected, TradeAttempt actual) =>
        Assert.Equal(JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual));

    private static BigInteger Cash(MaritimeWorldSnapshot snapshot) =>
        snapshot.Ports.Values.Aggregate((BigInteger)snapshot.TraderCash, (total, port) => total + port.MarketCash);

    private static long Goods(MaritimeWorldSnapshot snapshot, string good) =>
        snapshot.Ports.Values.Aggregate((long)snapshot.Ship.Cargo[good], (total, port) => total + port.Inventory[good]);

    private static void Unchanged(TradeAttempt attempt)
    {
        Assert.Equal(0, attempt.ExecutedQuantity);
        Assert.Equal(0L, attempt.ExecutedAmount);
        Assert.Equal(0, attempt.MarketInventoryChange.Delta);
        Assert.Equal(0, attempt.TraderInventoryChange.Delta);
        Assert.Equal(0L, attempt.MarketCashChange.Delta);
        Assert.Equal(0L, attempt.TraderCashChange.Delta);
    }

    [Fact]
    public void InitialStateHasIndependentPortsAndOneTraderAndShip()
    {
        var world = World();
        var snapshot = world.Snapshot();
        Assert.Equal(0L, world.Tick);
        Assert.Equal(new[] { "northport", "southhaven" }, snapshot.Ports.Keys);
        Assert.Equal(("trader", 1000L, "ship", 10, 0),
            (snapshot.TraderId, snapshot.TraderCash, snapshot.Ship.Id, snapshot.Ship.CargoCapacity, snapshot.Ship.CargoUnits));
        Assert.Equal("northport", snapshot.Ship.DockedPortId);
        Assert.Null(snapshot.Ship.OriginPortId);
        Assert.Null(snapshot.Ship.DestinationPortId);
        Assert.Equal(0, snapshot.Ship.RemainingTravelTicks);
        Assert.Equal((10, 0), (snapshot.Ports["northport"].Inventory["logs"], snapshot.Ports["southhaven"].Inventory["logs"]));
        Assert.Equal((1000L, 1000L), (snapshot.Ports["northport"].MarketCash, snapshot.Ports["southhaven"].MarketCash));
    }

    [Fact]
    public void ExpeditionUsesOneCargoAndCashAccountAndTheDestinationPrice()
    {
        var world = World();
        var before = world.Snapshot();
        var departure = world.Step(trades: new[] { Buy(2) }, sailCommands: Sail());
        var purchase = Assert.Single(departure.TradeAttempts);
        Assert.Equal("northport", purchase.PortId);
        Assert.Equal((2, 6L), (purchase.Trade.ExecutedQuantity, purchase.Trade.ExecutedAmount));
        Assert.Equal((8, 1006L, 2, 994L), (departure.After.Ports["northport"].Inventory["logs"],
            departure.After.Ports["northport"].MarketCash, departure.After.Ship.Cargo["logs"], departure.After.TraderCash));
        Assert.Equal(before.Ports["southhaven"].Inventory, departure.After.Ports["southhaven"].Inventory);
        Assert.Equal(1000L, departure.After.Ports["southhaven"].MarketCash);
        world.Step();
        var sale = world.Step(trades: new[] { Sell(2, "southhaven") });
        Assert.Equal((2, 16L), (sale.TradeAttempts[0].Trade.ExecutedQuantity, sale.TradeAttempts[0].Trade.ExecutedAmount));
        Assert.Equal((2, 984L, 0, 1010L), (sale.After.Ports["southhaven"].Inventory["logs"],
            sale.After.Ports["southhaven"].MarketCash, sale.After.Ship.CargoUnits, sale.After.TraderCash));
        Assert.Equal(departure.After.Ports["northport"].Inventory, sale.After.Ports["northport"].Inventory);
        Assert.Equal(Cash(before), Cash(sale.After));
        Assert.Equal(Goods(before, "logs"), Goods(sale.After, "logs"));
        Assert.Equal(Goods(before, "planks"), Goods(sale.After, "planks"));
    }

    [Fact]
    public void CapacityCountsAllGoodsAndRejectsTheWholePurchase()
    {
        var world = World(capacity: 5, cargoLogs: 1, cargoPlanks: 3);
        var before = world.Snapshot();
        var preview = before.PreviewTrade("northport", new TradeCommand(TradeDirection.Buy, "logs", 2));
        var result = world.Step(trades: new[] { Buy(2), Buy() });
        Assert.Equal(TradeOutcome.InsufficientCargoCapacity, result.TradeAttempts[0].Trade.Outcome);
        SameTrade(preview, result.TradeAttempts[0].Trade);
        Unchanged(result.TradeAttempts[0].Trade);
        Assert.Equal(TradeOutcome.Traded, result.TradeAttempts[1].Trade.Outcome);
        Assert.Equal((2, 3, 5, 997L), (result.After.Ship.Cargo["logs"], result.After.Ship.Cargo["planks"],
            result.After.Ship.CargoUnits, result.After.TraderCash));
    }

    [Fact]
    public void SellingFreesCapacityForLaterCommandsInTheSameStep()
    {
        var world = World(capacity: 3, cargoLogs: 3);
        var result = world.Step(trades: new[] { Buy(good: "planks"), Sell(), Buy(good: "planks") });
        Assert.Equal(new[] { TradeOutcome.InsufficientCargoCapacity, TradeOutcome.Traded, TradeOutcome.Traded },
            result.TradeAttempts.Select(attempt => attempt.Trade.Outcome));
        Assert.Equal((2, 1, 3, 997L), (result.After.Ship.Cargo["logs"], result.After.Ship.Cargo["planks"],
            result.After.Ship.CargoUnits, result.After.TraderCash));
    }

    [Theory]
    [InlineData(TradeDirection.Buy, 11, 1000, 1000, 0, TradeOutcome.InsufficientMarketInventory)]
    [InlineData(TradeDirection.Sell, 1, 1000, 1000, 0, TradeOutcome.InsufficientTraderInventory)]
    [InlineData(TradeDirection.Buy, 1, 1000, 2, 0, TradeOutcome.InsufficientTraderFunds)]
    [InlineData(TradeDirection.Sell, 1, 1, 1000, 1, TradeOutcome.InsufficientMarketFunds)]
    public void EconomicRejectionsAreNonMutatingCompletedOutcomes(TradeDirection direction, int quantity,
        long portCash, long traderCash, int cargoLogs, TradeOutcome outcome)
    {
        var world = World(ports: new[] { Port(cash: portCash), Port("southhaven") }, cash: traderCash, cargoLogs: cargoLogs);
        var before = world.Snapshot();
        var command = new TradeCommand(direction, "logs", quantity);
        var preview = before.PreviewTrade("northport", command);
        var result = world.Step(trades: new[] { new PortTradeCommand("northport", direction, "logs", quantity) });
        var attempt = Assert.Single(result.TradeAttempts).Trade;
        Assert.Equal(outcome, attempt.Outcome);
        Unchanged(attempt);
        SameTrade(preview, attempt);
        Assert.Equal(1L, result.After.Tick);
        Assert.Equal(before.Ship.Cargo, result.After.Ship.Cargo);
        Assert.Equal(before.TraderCash, result.After.TraderCash);
        Assert.Equal(before.Ports["northport"].Inventory, result.After.Ports["northport"].Inventory);
        Assert.Equal(before.Ports["northport"].MarketCash, result.After.Ports["northport"].MarketCash);
    }

    [Fact]
    public void ExactCashBoundariesRemainFiniteAndConserveCash()
    {
        var world = World(ports: new[] { Port(cash: 0), Port("southhaven", cash: 8, quotes: Fixed(8, 9)) }, cash: 3);
        var before = world.Snapshot();
        world.Step(trades: new[] { Buy() }, sailCommands: Sail());
        Assert.Equal(0L, world.Snapshot().TraderCash);
        world.Step();
        var after = world.Step(trades: new[] { Sell(port: "southhaven") }).After;
        Assert.Equal(8L, after.TraderCash);
        Assert.Equal(0L, after.Ports["southhaven"].MarketCash);
        Assert.Equal(Cash(before), Cash(after));
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void DynamicPreviewUsesTheRequestedPortsStockAndSharedCargo(InventoryPricingModel model)
    {
        var quotes = new[] { new MarketQuote("logs", Curve(model)) };
        var world = World(ports: new[] { Port(logs: 10, quotes: quotes), Port("southhaven", logs: 3, quotes: quotes) });
        var snapshot = world.Snapshot();
        Assert.Equal(11L, snapshot.GetMarketQuote("northport", "logs").Ask);
        Assert.Equal(18L, snapshot.GetMarketQuote("southhaven", "logs").Ask);
        var command = new TradeCommand(TradeDirection.Buy, "logs", 3);
        var north = snapshot.PreviewTrade("northport", command);
        var south = snapshot.PreviewTrade("southhaven", command);
        Assert.Equal(36L, north.TotalAmount);
        Assert.Equal(model == InventoryPricingModel.Linear ? 57L : 72L, south.TotalAmount);
        Assert.Same(snapshot, world.Snapshot());
        var actual = Assert.Single(world.Step(trades: new[] { Buy(3) }).TradeAttempts).Trade;
        SameTrade(north, actual);
        Assert.Equal(18L, world.Snapshot().GetMarketQuote("southhaven", "logs").Ask);
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Buy(3, "southhaven") }));
        world.Step(sailCommands: Sail());
        var inTransit = world.Snapshot();
        Assert.Equal(TradeOutcome.Traded, inTransit.PreviewTrade("southhaven", command).Outcome);
        Assert.Same(inTransit, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Buy(3, "southhaven") }));
        world.Step();
        var destinationPreview = world.Snapshot().PreviewTrade("southhaven", command);
        SameTrade(destinationPreview, Assert.Single(world.Step(trades: new[] { Buy(3, "southhaven") }).TradeAttempts).Trade);
    }

    [Fact]
    public void RemotePreviewIncludesCapacityAndFundsWithoutGrantingRemoteExecution()
    {
        var world = World(capacity: 1, cargoLogs: 1, cash: 0);
        var before = world.Snapshot();
        Assert.Equal(TradeOutcome.InsufficientCargoCapacity,
            before.PreviewTrade("southhaven", new TradeCommand(TradeDirection.Buy, "planks", 1)).Outcome);
        Assert.Equal(TradeOutcome.Traded,
            before.PreviewTrade("southhaven", new TradeCommand(TradeDirection.Sell, "logs", 1)).Outcome);
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Sell(port: "southhaven") }));
        Assert.Same(before, world.Snapshot());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void TravelConsumesExactlyTheConfiguredTicksIncludingDeparture(int duration)
    {
        var world = World(travelTicks: duration, cargoLogs: 2);
        var result = world.Step(sailCommands: Sail());
        Assert.Equal(ShipMovementKind.Departure, result.MovementEvents[0].Kind);
        Assert.Equal("ship", result.MovementEvents[0].ShipId);
        Assert.Equal("northport", result.MovementEvents[0].OriginPortId);
        Assert.Equal("southhaven", result.MovementEvents[0].DestinationPortId);
        Assert.Equal((0, duration), (result.MovementEvents[0].RemainingTicksBefore, result.MovementEvents[0].RemainingTicksAfter));
        Assert.Equal(ShipMovementKind.Progress, result.MovementEvents[1].Kind);
        Assert.Equal((duration, duration - 1),
            (result.MovementEvents[1].RemainingTicksBefore, result.MovementEvents[1].RemainingTicksAfter));
        for (var completed = 1; completed < duration; completed++)
        {
            Assert.Null(result.After.Ship.DockedPortId);
            Assert.Equal("northport", result.After.Ship.OriginPortId);
            Assert.Equal("southhaven", result.After.Ship.DestinationPortId);
            Assert.Equal(duration - completed, result.After.Ship.RemainingTravelTicks);
            var before = world.Snapshot();
            Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Sell(port: "southhaven") }));
            Assert.Same(before, world.Snapshot());
            result = world.Step();
            Assert.Equal(ShipMovementKind.Progress, result.MovementEvents[0].Kind);
        }
        Assert.Equal(duration, result.After.Tick);
        Assert.Equal(ShipMovementKind.Arrival, result.MovementEvents.Last().Kind);
        Assert.Equal((0, 0), (result.MovementEvents.Last().RemainingTicksBefore, result.MovementEvents.Last().RemainingTicksAfter));
        Assert.Equal("southhaven", result.After.Ship.DockedPortId);
        Assert.Null(result.After.Ship.OriginPortId);
        Assert.Null(result.After.Ship.DestinationPortId);
        Assert.Equal(0, result.After.Ship.RemainingTravelTicks);
        Assert.Equal(2, result.After.Ship.Cargo["logs"]);
        Assert.Equal(TradeOutcome.Traded, Assert.Single(world.Step(trades: new[] { Sell(2, "southhaven") }).TradeAttempts).Trade.Outcome);
        Assert.Equal(duration + 1, world.Tick);
    }

    [Fact]
    public void OneTickDepartureStillTradesOnlyAtTheStartingPort()
    {
        var world = World(travelTicks: 1);
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Buy(), Sell(port: "southhaven") }, sailCommands: Sail()));
        Assert.Same(before, world.Snapshot());
        var result = world.Step(trades: new[] { Buy() }, sailCommands: Sail());
        Assert.Equal("southhaven", result.After.Ship.DockedPortId);
        Assert.Equal(1, result.After.Ship.Cargo["logs"]);
        Assert.Equal(3, result.MovementEvents.Count);
        Assert.Equal(new[] { ShipMovementKind.Departure, ShipMovementKind.Progress, ShipMovementKind.Arrival },
            result.MovementEvents.Select(movement => movement.Kind));
        Assert.Equal(8L, world.Step(trades: new[] { Sell(port: "southhaven") }).TradeAttempts[0].Trade.TotalAmount);
    }

    [Fact]
    public void DockedIdleStepsDoNotMoveCargoOrPrices()
    {
        var world = World(cargoLogs: 1);
        var before = world.Snapshot();
        for (var tick = 0; tick < 3; tick++)
        {
            var step = world.Step();
            Assert.Empty(step.MovementEvents);
            Assert.Equal("northport", step.After.Ship.DockedPortId);
            Assert.Equal(before.Ship.Cargo, step.After.Ship.Cargo);
            Assert.Equal(before.TraderCash, step.After.TraderCash);
            foreach (var port in before.Ports.Keys)
            {
                Assert.Equal(before.Ports[port].Inventory, step.After.Ports[port].Inventory);
                Assert.Equal(before.GetMarketQuote(port, "logs").Ask, step.After.GetMarketQuote(port, "logs").Ask);
            }
        }
    }

    [Fact]
    public void InvalidSailingAndRemoteTradesPreserveTheWholeState()
    {
        var world = World(cargoLogs: 1);
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.Step(sailCommands: Sail("northport")));
        Assert.Throws<KeyNotFoundException>(() => world.Step(sailCommands: Sail("unknown")));
        Assert.Throws<ArgumentException>(() => world.Step(sailCommands: new[] { new SailCommand("southhaven"), new SailCommand("southhaven") }));
        Assert.Throws<ArgumentException>(() => world.Step(sailCommands: new SailCommand[] { null! }));
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Sell(port: "southhaven") }));
        Assert.Same(before, world.Snapshot());
        world.Step(sailCommands: Sail());
        var sailing = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.Step(sailCommands: Sail("northport")));
        Assert.Throws<InvalidOperationException>(() => world.Step(sailCommands: Sail()));
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Sell() }));
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Sell(port: "southhaven") }));
        Assert.Same(sailing, world.Snapshot());
    }

    [Fact]
    public void RejectedEconomicOrdersStillAllowDepartureAndProduction()
    {
        var world = World(ports: new[] { Port(), Port("southhaven", logs: 2, planks: 0, buildings: new[] { Mill() }) }, capacity: 1);
        var result = world.Step(trades: new[] { Buy(2) }, sailCommands: Sail());
        Assert.Equal(TradeOutcome.InsufficientCargoCapacity, result.TradeAttempts[0].Trade.Outcome);
        Assert.Equal(1, result.After.Ship.RemainingTravelTicks);
        Assert.Equal(0, result.After.Ship.CargoUnits);
        Assert.Equal(ProductionOutcome.Produced, result.ProductionAttempts[0].Production.Outcome);
        Assert.Equal(1, result.After.Ports["southhaven"].Inventory["planks"]);
    }

    [Fact]
    public void ProductionIsLocalAndChangesDestinationQuotesDuringTravel()
    {
        var quotes = new[] { new MarketQuote("logs", Curve()), new MarketQuote("planks", Curve()) };
        var world = World(ports: new[] { Port(quotes: quotes),
            Port("southhaven", logs: 4, planks: 0, buildings: new[] { Mill() }, quotes: quotes) }, cargoLogs: 1);
        var before = world.Snapshot();
        var departure = world.Step(sailCommands: Sail());
        Assert.Equal("southhaven", Assert.Single(departure.ProductionAttempts).PortId);
        var after = world.Step().After;
        Assert.Equal(before.Ports["northport"].Inventory, after.Ports["northport"].Inventory);
        Assert.Equal((0, 2), (after.Ports["southhaven"].Inventory["logs"], after.Ports["southhaven"].Inventory["planks"]));
        Assert.Equal(before.Ship.Cargo, after.Ship.Cargo);
        Assert.Equal(Cash(before), Cash(after));
        Assert.True(after.GetMarketQuote("southhaven", "logs").Bid > before.GetMarketQuote("southhaven", "logs").Bid);
        Assert.Null(before.GetMarketQuote("southhaven", "planks").Ask);
        Assert.NotNull(after.GetMarketQuote("southhaven", "planks").Ask);
        Assert.Equal(before.GetMarketQuote("northport", "logs").Ask, after.GetMarketQuote("northport", "logs").Ask);
        Assert.Equal(Goods(before, "logs") - 4, Goods(after, "logs"));
        Assert.Equal(Goods(before, "planks") + 2, Goods(after, "planks"));
    }

    [Fact]
    public void SellingCanFeedProductionButCannotBuyTheOutputUntilTheNextStep()
    {
        var world = World(ports: new[] { Port(logs: 0, planks: 0, buildings: new[] { Mill() }), Port("southhaven") }, cargoLogs: 2);
        var step = world.Step(trades: new[] { Sell(2), Buy(good: "planks") });
        Assert.Equal(TradeOutcome.Traded, step.TradeAttempts[0].Trade.Outcome);
        Assert.Equal(TradeOutcome.InsufficientMarketInventory, step.TradeAttempts[1].Trade.Outcome);
        Assert.Equal(ProductionOutcome.Produced, Assert.Single(step.ProductionAttempts).Production.Outcome);
        Assert.Equal((0, 1, 0), (step.After.Ports["northport"].Inventory["logs"],
            step.After.Ports["northport"].Inventory["planks"], step.After.Ship.CargoUnits));
        var next = world.Step(trades: new[] { Buy(good: "planks") });
        Assert.Equal(TradeOutcome.Traded, next.TradeAttempts[0].Trade.Outcome);
        Assert.Equal(1, next.After.Ship.Cargo["planks"]);
    }

    [Fact]
    public void BuyingInputsBeforeProductionCanCauseALocalShortage()
    {
        var world = World(ports: new[] { Port(logs: 2, buildings: new[] { Mill() }), Port("southhaven") });
        var result = world.Step(trades: new[] { Buy() });
        Assert.Equal(ProductionOutcome.ResourceShortage, Assert.Single(result.ProductionAttempts).Production.Outcome);
        Assert.Equal((1, 1), (result.After.Ports["northport"].Inventory["logs"], result.After.Ship.Cargo["logs"]));
    }

    [Fact]
    public void ProductionSortsPortsThenPriorityThenOrdinalInstanceId()
    {
        var buildings = new[] { Mill("mill_z"), Mill("mill_b", -1), Mill("mill_a") };
        var world = World(ports: new[] { Port("southhaven", logs: 2, buildings: buildings), Port(logs: 2, buildings: buildings.Reverse()) });
        var result = world.Step();
        Assert.Equal(new[] { "northport:mill_b", "northport:mill_a", "northport:mill_z", "southhaven:mill_b", "southhaven:mill_a", "southhaven:mill_z" },
            result.ProductionAttempts.Select(attempt => attempt.PortId + ":" + attempt.Production.BuildingInstanceId));
        Assert.Equal(new[] { ProductionOutcome.Produced, ProductionOutcome.ResourceShortage, ProductionOutcome.ResourceShortage,
            ProductionOutcome.Produced, ProductionOutcome.ResourceShortage, ProductionOutcome.ResourceShortage },
            result.ProductionAttempts.Select(attempt => attempt.Production.Outcome));
    }

    [Fact]
    public void ControlsAreLocalDeclaredOrderedAndMayTargetPortsWhileSailing()
    {
        var world = World(ports: new[] { Port(buildings: new[] { Mill(enabled: false) }),
            Port("southhaven", buildings: new[] { Mill(enabled: false) }) });
        var first = world.Step(commands: new[] { new PortProductionCommand("southhaven", "mill", false),
            new PortProductionCommand("southhaven", "mill", true) }, sailCommands: Sail());
        Assert.Equal("southhaven", Assert.Single(first.ProductionAttempts).PortId);
        Assert.False(first.After.Ports["northport"].Buildings[0].ProductionEnabled);
        Assert.True(first.After.Ports["southhaven"].Buildings[0].ProductionEnabled);
        var next = world.Step(commands: new[] { new PortProductionCommand("southhaven", "mill", false) });
        Assert.Empty(next.ProductionAttempts);
        Assert.Equal("southhaven", next.After.Ship.DockedPortId);
    }

    [Fact]
    public void LaterPortOverflowRollsBackEarlierTradeProductionControlsAndDeparture()
    {
        var world = World(ports: new[] { Port(logs: 4, planks: 0, buildings: new[] { Mill() }),
            Port("southhaven", logs: 2, planks: int.MaxValue, buildings: new[] { Mill(enabled: false) }) });
        var before = world.Snapshot();
        var fingerprint = Fingerprint(before);
        Assert.Throws<OverflowException>(() => world.Step(commands: new[] { new PortProductionCommand("southhaven", "mill", true) },
            trades: new[] { Buy() }, sailCommands: Sail()));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(fingerprint, Fingerprint(world.Snapshot()));
        var retry = world.Step(trades: new[] { Buy() }, sailCommands: Sail());
        Assert.Equal(0L, retry.Tick);
        Assert.Equal(1, retry.After.Ship.RemainingTravelTicks);
        Assert.Equal(1, retry.After.Ship.Cargo["logs"]);
        Assert.Equal(1, retry.After.Ports["northport"].Inventory["planks"]);
        Assert.False(retry.After.Ports["southhaven"].Buildings[0].ProductionEnabled);
    }

    [Fact]
    public void ProductionOverflowWhileSailingPreservesTheRemainingJourney()
    {
        var world = World(ports: new[] { Port(), Port("southhaven", logs: 4, planks: int.MaxValue - 1, buildings: new[] { Mill() }) });
        world.Step(trades: new[] { Buy() }, sailCommands: Sail());
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(1, before.Ship.RemainingTravelTicks);
        var retry = world.Step(commands: new[] { new PortProductionCommand("southhaven", "mill", false) });
        Assert.Equal("southhaven", retry.After.Ship.DockedPortId);
        Assert.Equal(1L, retry.Tick);
    }

    [Fact]
    public void LaterTradeArithmeticFailureRollsBackEarlierSuccessfulTradesAndControls()
    {
        var quotes = new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", long.MaxValue, long.MaxValue) };
        var world = World(ports: new[] { Port(quotes: quotes, buildings: new[] { Mill() }), Port("southhaven") });
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(commands: new[] { new PortProductionCommand("northport", "mill", false) },
            trades: new[] { Buy(), Buy(2, good: "planks") }, sailCommands: Sail()));
        Assert.Same(before, world.Snapshot());
        Assert.True(before.Ports["northport"].Buildings[0].ProductionEnabled);
        Assert.Equal(0L, world.Step().Tick);
    }

    [Fact]
    public void InvalidLaterCommandsAreValidatedBeforeAnyEconomicEffects()
    {
        var world = World();
        var before = world.Snapshot();
        Assert.Throws<KeyNotFoundException>(() => world.Step(trades: new[] { Buy(), Buy(port: "unknown") }));
        Assert.Throws<KeyNotFoundException>(() => world.Step(trades: new[] { Buy(), Buy(good: "unknown") }));
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: new[] { Buy(), Buy(port: "southhaven") }));
        Assert.Throws<ArgumentException>(() => world.Step(trades: new[] { Buy(), null! }));
        Assert.Throws<KeyNotFoundException>(() => world.Step(commands: new[] { new PortProductionCommand("unknown", "mill", true) }));
        Assert.Throws<KeyNotFoundException>(() => world.Step(commands: new[] { new PortProductionCommand("northport", "unknown", true) }));
        Assert.Throws<ArgumentException>(() => world.Step(commands: new PortProductionCommand[] { null! }));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0L, world.Step().Tick);
    }

    [Fact]
    public void ThrowingAndReentrantCommandEnumerationsCannotPartiallyCommit()
    {
        var world = World();
        var before = world.Snapshot();
        IEnumerable<PortTradeCommand> Throwing()
        {
            yield return Buy();
            throw new InvalidOperationException("Enumeration failed.");
        }
        IEnumerable<PortTradeCommand> Reentrant()
        {
            yield return Buy();
            world.Step();
        }
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: Throwing()));
        Assert.Same(before, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.Step(trades: Reentrant()));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0L, world.Step(trades: new[] { Buy() }).Tick);
    }

    [Fact]
    public void TickOverflowPreservesPortsCargoControlsAndDockingState()
    {
        var world = World(tick: long.MaxValue, ports: new[] { Port(buildings: new[] { Mill() }), Port("southhaven") });
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(commands: new[] { new PortProductionCommand("northport", "mill", false) },
            trades: new[] { Buy() }, sailCommands: Sail()));
        Assert.Same(before, world.Snapshot());
        Assert.Throws<KeyNotFoundException>(() => world.Step(trades: new[] { Buy(port: "unknown") }));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void TickOverflowCannotCompleteAnInProgressVoyage()
    {
        var world = World(tick: long.MaxValue - 1, cargoLogs: 2);
        world.Step(sailCommands: Sail());
        var before = world.Snapshot();
        Assert.Equal(long.MaxValue, before.Tick);
        Assert.Equal(1, before.Ship.RemainingTravelTicks);
        Assert.Throws<OverflowException>(() => world.Step());
        Assert.Same(before, world.Snapshot());
        Assert.Null(before.Ship.DockedPortId);
        Assert.Equal(2, before.Ship.Cargo["logs"]);
    }

    [Fact]
    public void MaximumConfiguredTravelTimeAdvancesWithoutOverflow()
    {
        var world = World(travelTicks: int.MaxValue);
        var departure = world.Step(sailCommands: Sail());
        Assert.Equal(int.MaxValue - 1, departure.After.Ship.RemainingTravelTicks);
        Assert.Equal(int.MaxValue, departure.MovementEvents[0].RemainingTicksAfter);
        Assert.Null(departure.After.Ship.DockedPortId);
        var next = world.Step();
        Assert.Equal(int.MaxValue - 2, next.After.Ship.RemainingTravelTicks);
        Assert.Single(next.MovementEvents);
        Assert.Equal(ShipMovementKind.Progress, next.MovementEvents[0].Kind);
    }

    [Fact]
    public void EquivalentPortOrderProducesIdenticalResultsThroughoutAnExpedition()
    {
        var ports = new[] { Port(quotes: new[] { new MarketQuote("planks", 4, 5), new MarketQuote("logs", Curve()) }),
            Port("southhaven", logs: 2, planks: 0, buildings: new[] { Mill("mill_b"), Mill("mill_a", enabled: false) },
                quotes: new[] { new MarketQuote("logs", Curve(InventoryPricingModel.Scarcity)), new MarketQuote("planks", 4, 5) }) };
        var first = World(ports);
        var second = World(ports.Reverse());
        Assert.Equal(Fingerprint(first.Snapshot()), Fingerprint(second.Snapshot()));
        var firstResults = new[] { first.Step(trades: new[] { Buy(4) }, sailCommands: Sail()), first.Step(),
            first.Step(trades: new[] { Sell(4, "southhaven") }), first.Step(trades: new[] { Buy(2, "southhaven", "planks") }, sailCommands: Sail("northport")),
            first.Step(), first.Step(trades: new[] { Sell(2, good: "planks") }) };
        var secondResults = new[] { second.Step(trades: new[] { Buy(4) }, sailCommands: Sail()), second.Step(),
            second.Step(trades: new[] { Sell(4, "southhaven") }), second.Step(trades: new[] { Buy(2, "southhaven", "planks") }, sailCommands: Sail("northport")),
            second.Step(), second.Step(trades: new[] { Sell(2, good: "planks") }) };
        Assert.Equal(firstResults.Select(Fingerprint), secondResults.Select(Fingerprint));
        Assert.Equal(Fingerprint(first.Snapshot()), Fingerprint(second.Snapshot()));
    }

    [Fact]
    public void MixedTradesAndRoundTripsConserveAllCashAndGoodsWithoutProduction()
    {
        var world = World(cargoLogs: 2, cargoPlanks: 1, ports: new[] { Port(), Port("southhaven", quotes: Fixed(8, 9)) });
        var before = world.Snapshot();
        var steps = new[]
        {
            world.Step(trades: new[] { Buy(4), Sell(2), Buy(2, good: "planks") }, sailCommands: Sail()),
            world.Step(),
            world.Step(trades: new[] { Sell(4, "southhaven"), Sell(2, "southhaven", "planks"), Buy(3, "southhaven") }, sailCommands: Sail("northport")),
            world.Step(),
            world.Step(trades: new[] { Sell(3), Sell(1, good: "planks") })
        };
        foreach (var result in steps)
        {
            Assert.Equal(Cash(before), Cash(result.After));
            Assert.Equal(Goods(before, "logs"), Goods(result.After, "logs"));
            Assert.Equal(Goods(before, "planks"), Goods(result.After, "planks"));
            Assert.InRange(result.After.Ship.CargoUnits, 0, result.After.Ship.CargoCapacity);
            foreach (var entry in result.TradeAttempts)
            {
                Assert.Equal(BigInteger.Zero, (BigInteger)entry.Trade.MarketCashChange.Delta + entry.Trade.TraderCashChange.Delta);
                Assert.Equal(0L, (long)entry.Trade.MarketInventoryChange.Delta + entry.Trade.TraderInventoryChange.Delta);
            }
        }
    }

    [Fact]
    public void SnapshotsAndResultsCannotMutateTheLiveWorld()
    {
        var world = World(ports: new[] { Port(buildings: new[] { Mill() }), Port("southhaven") });
        var snapshot = world.Snapshot();
        var fingerprint = Fingerprint(snapshot);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)snapshot.Ship.Cargo)["logs"] = 50);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)snapshot.Ports["northport"].Inventory)["logs"] = 50);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, MarketQuote>)snapshot.Ports["northport"].Quotes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<BuildingInstance>)snapshot.Ports["northport"].Buildings).Clear());
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IDictionary)snapshot.Ports).Clear());
        var result = world.Step(trades: new[] { Buy() }, sailCommands: Sail());
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)result.TradeAttempts).Clear());
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)result.ProductionAttempts).Clear());
        Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)result.MovementEvents).Clear());
        Assert.Equal(fingerprint, Fingerprint(snapshot));
        var departed = Fingerprint(result.After);
        world.Step();
        world.Step(trades: new[] { Sell(port: "southhaven") });
        Assert.Equal(fingerprint, Fingerprint(snapshot));
        Assert.Equal(departed, Fingerprint(result.After));
        Assert.Equal("northport", snapshot.Ship.DockedPortId);
        Assert.Null(result.After.Ship.DockedPortId);
    }

    [Fact]
    public void ConfigurationInputsAreCopiedBeforeTheyCanBeChangedByTheHost()
    {
        var inventory = Stock(4, 0);
        var cargo = Stock(1, 0);
        var quotes = new List<MarketQuote>(Fixed());
        var buildings = new List<BuildingInstance> { Mill(enabled: false) };
        var port = new PortConfiguration("northport", quotes, 20, buildings, inventory);
        var ship = new ShipConfiguration("ship", 5, "northport", cargo);
        inventory["logs"] = 0;
        cargo["logs"] = 4;
        quotes.Clear();
        buildings.Clear();
        var ports = new List<PortConfiguration> { port, Port("southhaven") };
        var world = new MaritimeWorldState(Content(), ports, "trader", 20, ship, 2);
        ports.Clear();
        var snapshot = world.Snapshot();
        Assert.Equal(4, snapshot.Ports["northport"].Inventory["logs"]);
        Assert.Equal(1, snapshot.Ship.Cargo["logs"]);
        Assert.Equal(2, snapshot.Ports["northport"].Quotes.Count);
        Assert.Single(snapshot.Ports["northport"].Buildings);
        Assert.Equal(TradeOutcome.Traded, Assert.Single(world.Step(trades: new[] { Buy() }).TradeAttempts).Trade.Outcome);
    }

    [Fact]
    public void MaximumCapacityCountsAcrossGoodsWithoutOverflow()
    {
        var world = World(capacity: int.MaxValue, cargoLogs: int.MaxValue - 1);
        var before = world.Snapshot();
        var result = world.Step(trades: new[] { Buy(2, good: "planks"), Buy(good: "planks"), Buy() });
        Assert.Equal(new[] { TradeOutcome.InsufficientCargoCapacity, TradeOutcome.Traded, TradeOutcome.InsufficientCargoCapacity },
            result.TradeAttempts.Select(attempt => attempt.Trade.Outcome));
        Assert.Equal(int.MaxValue, result.After.Ship.CargoUnits);
        Assert.Equal(int.MaxValue - 1, result.After.Ship.Cargo["logs"]);
        Assert.Equal(1, result.After.Ship.Cargo["planks"]);
        Assert.Equal(Cash(before), Cash(result.After));
    }

    [Fact]
    public void MaximumOrderCanTransferIntoMaximumCapacityWithoutAUnitLoop()
    {
        var quotes = new[] { new MarketQuote("logs", new InventoryPricing(InventoryPricingModel.Scarcity, 1, 10, 0, 1, 1, 0)) };
        var world = World(capacity: int.MaxValue, cash: long.MaxValue,
            ports: new[] { Port(logs: int.MaxValue, cash: 0, quotes: quotes), Port("southhaven") });
        var result = world.Step(trades: new[] { Buy(int.MaxValue) });
        Assert.Equal(TradeOutcome.Traded, Assert.Single(result.TradeAttempts).Trade.Outcome);
        Assert.Equal((long)int.MaxValue, result.TradeAttempts[0].Trade.TotalAmount);
        Assert.Equal(int.MaxValue, result.After.Ship.CargoUnits);
        Assert.Equal(0, result.After.Ports["northport"].Inventory["logs"]);
        Assert.Equal(long.MaxValue - int.MaxValue, result.After.TraderCash);
    }

    [Fact]
    public void CapacityRejectionPrecedesDynamicPriceOverflowButFixedPricePrecedenceIsPreserved()
    {
        var dynamicQuotes = new[] { new MarketQuote("logs", new InventoryPricing(InventoryPricingModel.Linear,
            long.MaxValue, 1, 0, long.MaxValue, long.MaxValue, 0)) };
        var dynamicWorld = World(capacity: 1, ports: new[] { Port(quotes: dynamicQuotes), Port("southhaven") });
        var rejected = Assert.Single(dynamicWorld.Step(trades: new[] { Buy(2) }).TradeAttempts).Trade;
        Assert.Equal(TradeOutcome.InsufficientCargoCapacity, rejected.Outcome);
        Assert.Null(rejected.Price);
        Unchanged(rejected);
        var fixedWorld = World(capacity: 1, ports: new[] { Port(quotes: Fixed(long.MaxValue, long.MaxValue)), Port("southhaven") });
        var before = fixedWorld.Snapshot();
        Assert.Throws<OverflowException>(() => fixedWorld.Step(trades: new[] { Buy(2) }));
        Assert.Same(before, fixedWorld.Snapshot());
    }

    [Fact]
    public void StockCapacityAndFundsRejectionsHaveExplicitPrecedence()
    {
        var world = World(capacity: 1, cargoLogs: 1, cash: 0);
        var result = world.Step(trades: new[] { Buy(11), Buy(), Sell(2) });
        Assert.Equal(new[] { TradeOutcome.InsufficientMarketInventory, TradeOutcome.InsufficientCargoCapacity, TradeOutcome.InsufficientTraderInventory },
            result.TradeAttempts.Select(attempt => attempt.Trade.Outcome));
        foreach (var attempt in result.TradeAttempts) Unchanged(attempt.Trade);
    }

    [Theory]
    [InlineData(TradeDirection.Buy)]
    [InlineData(TradeDirection.Sell)]
    public void CashCreditOverflowRollsBackTheCompleteWorld(TradeDirection direction)
    {
        var world = World(ports: new[] { Port(cash: long.MaxValue), Port("southhaven") }, cash: long.MaxValue, cargoLogs: 1);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.Step(trades: new[] { new PortTradeCommand("northport", direction, "logs", 1) }, sailCommands: Sail()));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void PortStockOverflowOnSaleDoesNotRemoveCargoOrTransferMoney()
    {
        var world = World(ports: new[] { Port(logs: int.MaxValue), Port("southhaven") }, cargoLogs: 1);
        var before = world.Snapshot();
        Assert.Null(before.GetMarketQuote("northport", "logs").Bid);
        Assert.Throws<OverflowException>(() => before.PreviewTrade("northport", new TradeCommand(TradeDirection.Sell, "logs", 1)));
        Assert.Throws<OverflowException>(() => world.Step(trades: new[] { Sell() }));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void WorldConfigurationRejectsInvalidCardinalityAndIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => World(ports: Array.Empty<PortConfiguration>()));
        Assert.Throws<ArgumentException>(() => World(ports: new[] { Port() }));
        Assert.Throws<ArgumentException>(() => World(ports: new[] { Port(), Port("southhaven"), Port("third") }));
        Assert.Throws<ArgumentException>(() => World(ports: new[] { Port(), Port() }));
        Assert.Throws<ArgumentException>(() => World(ports: new[] { Port(), null! }));
        Assert.Throws<KeyNotFoundException>(() => World(initialPort: "unknown"));
        Assert.Throws<ArgumentNullException>(() => new MaritimeWorldState(null!, new[] { Port(), Port("southhaven") },
            "trader", 0, new ShipConfiguration("ship", 1, "northport"), 1));
        Assert.Throws<ArgumentNullException>(() => new MaritimeWorldState(Content(), null!, "trader", 0,
            new ShipConfiguration("ship", 1, "northport"), 1));
        Assert.Throws<ArgumentNullException>(() => new MaritimeWorldState(Content(), new[] { Port(), Port("southhaven") }, "trader", 0, null!, 1));
        Assert.Throws<ArgumentException>(() => new MaritimeWorldState(Content(), new[] { Port(), Port("southhaven") },
            "Invalid ID", 0, new ShipConfiguration("ship", 1, "northport"), 1));
    }

    [Fact]
    public void ConfigurationRejectsNonpositiveTravelCapacityAndInvalidInitialBalances()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => World(travelTicks: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(travelTicks: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(capacity: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(capacity: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(cash: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(tick: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Port(cash: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(cargoLogs: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(capacity: 1, cargoLogs: 1, cargoPlanks: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => World(capacity: int.MaxValue, cargoLogs: int.MaxValue, cargoPlanks: 1));
    }

    [Fact]
    public void PortConfigurationRejectsUnknownContentAndDuplicateLocalDefinitions()
    {
        Assert.Throws<ArgumentException>(() => World(ports: new[] { Port(buildings: new[] { Mill(), Mill() }), Port("southhaven") }));
        Assert.Throws<KeyNotFoundException>(() => World(ports: new[] { Port(quotes: new[] { new MarketQuote("unknown", 1, 1) }), Port("southhaven") }));
        Assert.Throws<KeyNotFoundException>(() => World(ports: new[] { Port(buildings: new[] { new BuildingInstance("mill", "unknown", "saw_planks") }), Port("southhaven") }));
        Assert.Throws<KeyNotFoundException>(() => World(ports: new[] { Port(buildings: new[] { new BuildingInstance("mill", "sawmill", "unknown") }), Port("southhaven") }));
        Assert.Throws<ArgumentException>(() => Port(quotes: new[] { new MarketQuote("logs", 1, 1), new MarketQuote("logs", 2, 2) }));
        Assert.Throws<ArgumentException>(() => Port(quotes: new MarketQuote[] { null! }));
        Assert.Throws<ArgumentException>(() => Port(buildings: new BuildingInstance[] { null! }));
        Assert.Throws<KeyNotFoundException>(() => new MaritimeWorldState(Content(), new[] { Port(), Port("southhaven") }, "trader", 0,
            new ShipConfiguration("ship", 1, "northport", new Dictionary<string, int> { ["unknown"] = 1 }), 1));
    }

    [Fact]
    public void QuoteAndPreviewValidatePortsGoodsAndRequiredArguments()
    {
        var world = World(ports: new[] { Port(quotes: new[] { new MarketQuote("logs", 2, 3) }), Port("southhaven") });
        var snapshot = world.Snapshot();
        var command = new TradeCommand(TradeDirection.Buy, "logs", 1);
        Assert.Throws<ArgumentNullException>(() => snapshot.GetMarketQuote(null!, "logs"));
        Assert.Throws<ArgumentNullException>(() => snapshot.GetMarketQuote("northport", null!));
        Assert.Throws<KeyNotFoundException>(() => snapshot.GetMarketQuote("unknown", "logs"));
        Assert.Throws<KeyNotFoundException>(() => snapshot.GetMarketQuote("northport", "unknown"));
        Assert.Throws<KeyNotFoundException>(() => snapshot.GetMarketQuote("northport", "planks"));
        Assert.Throws<ArgumentNullException>(() => snapshot.PreviewTrade(null!, command));
        Assert.Throws<ArgumentNullException>(() => snapshot.PreviewTrade("northport", null!));
        Assert.Throws<KeyNotFoundException>(() => snapshot.PreviewTrade("unknown", command));
        Assert.Throws<KeyNotFoundException>(() => snapshot.PreviewTrade("northport", new TradeCommand(TradeDirection.Buy, "planks", 1)));
        Assert.Throws<KeyNotFoundException>(() => world.Step(trades: new[] { Buy(good: "planks") }));
        Assert.Same(snapshot, world.Snapshot());
    }
}
