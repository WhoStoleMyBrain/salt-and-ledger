using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class AutomationTests
{
    private const string North = "northport";
    private const string South = "southhaven";
    private static Dictionary<string, int> Stock(int logs = 0, int planks = 0) =>
        new() { ["logs"] = logs, ["planks"] = planks };
    private static MarketQuote Linear(string good, long reference, int target, long sensitivity, long max) =>
        new(good, new InventoryPricing(InventoryPricingModel.Linear, reference, target, sensitivity, 1, max, 1));
    private static MarketQuote[] NorthQuotes() => new[] { Linear("logs", 10, 12, 6, 50), Linear("planks", 24, 6, 12, 60) };
    private static MarketQuote[] SouthQuotes() => new[] { Linear("logs", 20, 6, 12, 60), Linear("planks", 12, 6, 6, 50) };
    private static BuildingInstance[] Mill() => new[] { new BuildingInstance("mill", "sawmill", "saw_planks") };
    private static RouteLeg Leg(int maximum = 6, int minimum = 2, long buy = 10, long sell = 19) =>
        new("logs", maximum, minimum, buy, sell);
    private static TradeRoutePolicy Policy(long buy = 10, long sell = 19, int maximum = 6,
        int minimum = 2, long reserve = 0, bool emptyReturn = false) =>
        new(North, South, Leg(maximum, minimum, buy, sell), new RouteLeg("planks", 2, 2, 18, 31), reserve, emptyReturn);

    private static MaritimeWorldState World(int northLogs = 18, int southLogs = 2, int southPlanks = 0,
        long cash = 100, long northCash = 1000, long southCash = 1000, int capacity = 6,
        int cargoLogs = 0, int cargoPlanks = 0, string dock = North, bool production = true,
        IEnumerable<MarketQuote>? northQuotes = null, IEnumerable<MarketQuote>? southQuotes = null,
        int travelTicks = 2, long tick = 0, bool reverse = false)
    {
        var ports = new[]
        {
            new PortConfiguration(North, northQuotes ?? NorthQuotes(), northCash,
                Array.Empty<BuildingInstance>(), Stock(northLogs)),
            new PortConfiguration(South, southQuotes ?? SouthQuotes(), southCash,
                production ? Mill() : Array.Empty<BuildingInstance>(), Stock(southLogs, southPlanks))
        };
        return new MaritimeWorldState(ContentLoader.Load(ContentLoaderTests.CanonicalJson()),
            reverse ? ports.Reverse() : ports, "trader", cash,
            new ShipConfiguration("ship", capacity, dock, Stock(cargoLogs, cargoPlanks)), travelTicks, tick);
    }

    private static string State(MaritimeWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
    {
        snapshot.Tick, snapshot.TraderCash, snapshot.Ship, snapshot.TravelTicks,
        Ports = snapshot.Ports.Values.Select(port => new
        {
            port.Id, port.MarketCash, port.Inventory, port.Buildings,
            Prices = port.Quotes.Keys.Select(good => snapshot.GetMarketQuote(port.Id, good)).ToArray()
        }).ToArray()
    });
    private static string Decision(AutomationDecision decision) => JsonConvert.SerializeObject(new
    {
        decision.Tick, decision.Stage, decision.Action, decision.Reason, decision.SelectedQuantity,
        decision.Trade, decision.Departure, decision.Preview, Before = State(decision.Before)
    });
    private static string Result(MaritimeStepResult result) => JsonConvert.SerializeObject(new
    {
        result.Tick, After = State(result.After), result.TradeAttempts, result.ProductionAttempts, result.MovementEvents
    });
    private static BigInteger Cash(MaritimeWorldSnapshot snapshot) =>
        snapshot.Ports.Values.Aggregate((BigInteger)snapshot.TraderCash, (sum, port) => sum + port.MarketCash);
    private static long Goods(MaritimeWorldSnapshot snapshot, string good) =>
        snapshot.Ports.Values.Aggregate((long)snapshot.Ship.Cargo[good], (sum, port) => sum + port.Inventory[good]);
    private static AutomationDecision Evaluate(MaritimeWorldState world, TradeRoutePolicy? policy = null) =>
        RoutePolicyEvaluator.Evaluate(world.Snapshot(), policy ?? Policy());
    private static void Idle(AutomationDecision decision, AutomationAction action, AutomationReason reason)
    {
        Assert.Equal(action, decision.Action);
        Assert.Equal(reason, decision.Reason);
        Assert.Null(decision.Trade);
        Assert.Null(decision.Departure);
        Assert.Equal(0, decision.SelectedQuantity);
    }

    [Theory]
    [InlineData(8, 2, 16, 8)]
    [InlineData(10, 6, 54, 10)]
    public void MarginalBuyingLimitSelectsTheLargestEligibleShipment(long limit, int quantity, long total, long last)
    {
        var world = World();
        var before = world.Snapshot();
        var decision = Evaluate(world, Policy(buy: limit));
        Assert.Equal(AutomationStage.OutboundLoading, decision.Stage);
        Assert.Equal(AutomationAction.BuyAndDepart, decision.Action);
        Assert.Equal(AutomationReason.PurchaseReady, decision.Reason);
        Assert.Equal(quantity, decision.SelectedQuantity);
        Assert.Equal(North, decision.Trade!.PortId);
        Assert.Equal("logs", decision.Trade.Trade.GoodId);
        Assert.Equal(TradeDirection.Buy, decision.Trade.Trade.Direction);
        Assert.Equal(South, decision.Departure!.DestinationPortId);
        Assert.Equal(total, decision.Preview!.TotalAmount);
        Assert.Equal(last, decision.Preview.Price!.LastMarginalPrice);
        Assert.Same(before, world.Snapshot());
        Assert.Same(before, decision.Before);
        if (quantity == 6) Assert.NotEqual(quantity * decision.Preview.UnitPrice, total);
    }

    [Theory]
    [InlineData(25, 3, 81)]
    [InlineData(19, 6, 144)]
    public void MarginalSellingLimitUsesDestinationInventoryAndRetainsUnsoldCargo(long limit, int quantity, long total)
    {
        var world = World(dock: South, cargoLogs: 6, southLogs: 0, production: false);
        var decision = Evaluate(world, Policy(sell: limit));
        Assert.Equal(AutomationStage.OutboundSale, decision.Stage);
        Assert.Equal(AutomationAction.Sell, decision.Action);
        Assert.Equal(South, decision.Trade!.PortId);
        Assert.Equal(quantity, decision.SelectedQuantity);
        Assert.Equal(total, decision.Preview!.TotalAmount);
        Assert.Null(decision.Departure);
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Equal(6 - quantity, result.After.Ship.Cargo["logs"]);
        Assert.Equal(South, result.After.Ship.DockedPortId);
        Assert.Equal(100 + total, result.After.TraderCash);
        if (quantity == 3)
        {
            Idle(Evaluate(world, Policy(sell: limit)), AutomationAction.Wait, AutomationReason.SellPriceLimit);
            Assert.Equal(3, world.Snapshot().Ship.Cargo["logs"]);
        }
    }

    [Theory]
    [InlineData(3, 18, 6, 3)]
    [InlineData(6, 18, 4, 4)]
    [InlineData(6, 3, 6, 3)]
    public void CapacityMaximumAndAvailableStockBoundTheOrder(int capacity, int stock, int maximum, int expected)
    {
        var world = World(capacity: capacity, northLogs: stock);
        var decision = Evaluate(world, Policy(maximum: maximum, buy: 100));
        Assert.Equal(expected, decision.SelectedQuantity);
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Equal(expected, result.After.Ship.CargoUnits);
        Assert.InRange(result.After.Ship.CargoUnits, 0, capacity);
    }

    [Theory]
    [InlineData(25, 0, 3, 25)]
    [InlineData(100, 75, 3, 25)]
    [InlineData(100, 84, 2, 16)]
    public void ExactBulkCostRespectsCashAndReserve(long cash, long reserve, int expected, long total)
    {
        var world = World(cash: cash);
        var decision = Evaluate(world, Policy(reserve: reserve));
        Assert.Equal(expected, decision.SelectedQuantity);
        Assert.Equal(total, decision.Preview!.TotalAmount);
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Equal(cash - total, result.After.TraderCash);
        Assert.True(result.After.TraderCash >= reserve);
    }

    [Theory]
    [InlineData(0, 100, 0, 10, AutomationReason.InsufficientInventory)]
    [InlineData(1, 100, 0, 100, AutomationReason.InsufficientInventory)]
    [InlineData(18, 100, 0, 7, AutomationReason.BuyPriceLimit)]
    [InlineData(18, 15, 0, 10, AutomationReason.InsufficientTraderFunds)]
    [InlineData(18, 100, 85, 10, AutomationReason.CashReserve)]
    [InlineData(18, 100, 101, 10, AutomationReason.CashReserve)]
    public void UnavailableMinimumShipmentWaitsWithoutSubmittingImpossibleTrades(int stock, long cash,
        long reserve, long limit, AutomationReason reason)
    {
        var world = World(northLogs: stock, cash: cash, production: false);
        var policy = Policy(buy: limit, reserve: reserve);
        var before = world.Snapshot();
        var decision = Evaluate(world, policy);
        Idle(decision, AutomationAction.Wait, reason);
        Assert.Same(before, world.Snapshot());
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Empty(result.TradeAttempts);
        Assert.Empty(result.MovementEvents);
        Assert.Equal(1, world.Tick);
        Assert.Equal(0, result.After.Ship.CargoUnits);
        Idle(Evaluate(world, policy), AutomationAction.Wait, reason);
    }

    [Fact]
    public void MinimumExceedingPhysicalCapacityPausesInsteadOfWaitingForever()
    {
        var world = World(capacity: 1);
        Idle(Evaluate(world), AutomationAction.Pause, AutomationReason.MinimumExceedsCapacity);
        Assert.Equal(0, world.Tick);
    }

    [Fact]
    public void ExistingOutboundCargoCanBeToppedUpAndCountsTowardTheMinimum()
    {
        var world = World(cargoLogs: 1, cash: 8);
        var decision = Evaluate(world);
        Assert.Equal(1, decision.SelectedQuantity);
        Assert.Equal(AutomationAction.BuyAndDepart, decision.Action);
        Assert.Equal(2, TradeRouteAutomation.Execute(world, decision).After.Ship.CargoUnits);
    }

    [Fact]
    public void AnUnreadyPartialLoadIsRetainedButAReadyLoadCanDepartWithoutTopUp()
    {
        var partial = World(cargoLogs: 1, cash: 0);
        Idle(Evaluate(partial), AutomationAction.Wait, AutomationReason.InsufficientTraderFunds);
        Assert.Equal(1, partial.Snapshot().Ship.CargoUnits);
        var ready = World(cargoLogs: 2, cash: 0);
        var decision = Evaluate(ready);
        Assert.Equal(AutomationAction.Depart, decision.Action);
        Assert.Equal(AutomationReason.ShipmentReady, decision.Reason);
        Assert.Null(decision.Trade);
        Assert.Equal(2, TradeRouteAutomation.Execute(ready, decision).After.Ship.CargoUnits);
    }

    [Fact]
    public void ExistingLoadIsToppedUpToTheTargetRatherThanAddingTheWholeMaximum()
    {
        var world = World(cargoLogs: 2);
        var decision = Evaluate(world);
        Assert.Equal(4, decision.SelectedQuantity);
        Assert.Equal(6, TradeRouteAutomation.Execute(world, decision).After.Ship.CargoUnits);
    }

    [Fact]
    public void LoweringTheLoadingTargetDoesNotDiscardAnExistingUsefulShipment()
    {
        var world = World(cargoLogs: 6);
        var decision = Evaluate(world, Policy(maximum: 2));
        Assert.Equal(AutomationAction.Depart, decision.Action);
        Assert.Equal(AutomationReason.ShipmentReady, decision.Reason);
        Assert.Null(decision.Trade);
        Assert.Equal(6, TradeRouteAutomation.Execute(world, decision).After.Ship.CargoUnits);
    }

    [Fact]
    public void SalesAllowUsefulPartialFillsBelowThePurchaseMinimum()
    {
        var world = World(dock: South, cargoLogs: 6, southLogs: 0, southCash: 30, production: false);
        var decision = Evaluate(world);
        Assert.Equal(1, decision.SelectedQuantity);
        Assert.Equal(29, decision.Preview!.TotalAmount);
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Equal(5, result.After.Ship.Cargo["logs"]);
        Assert.Equal(1, result.After.Ports[South].MarketCash);
        Idle(Evaluate(world), AutomationAction.Wait, AutomationReason.InsufficientMarketFunds);
    }

    [Theory]
    [InlineData(1000, 30, AutomationReason.SellPriceLimit)]
    [InlineData(28, 19, AutomationReason.InsufficientMarketFunds)]
    public void UnsellableDestinationCargoNeverLeavesOrDisappears(long liquidity, long limit, AutomationReason reason)
    {
        var world = World(dock: South, cargoLogs: 6, southLogs: 0, southCash: liquidity, production: false);
        var policy = Policy(sell: limit, emptyReturn: true);
        for (var step = 0; step < 3; step++)
        {
            var decision = Evaluate(world, policy);
            Idle(decision, AutomationAction.Wait, reason);
            var result = TradeRouteAutomation.Execute(world, decision);
            Assert.Empty(result.TradeAttempts);
            Assert.Empty(result.MovementEvents);
            Assert.Equal(South, result.After.Ship.DockedPortId);
            Assert.Equal(6, result.After.Ship.Cargo["logs"]);
            Assert.Equal(100, result.After.TraderCash);
        }
    }

    [Fact]
    public void ReturnLoadingAndSaleUseTheReturnGoodAndItsLocalThresholds()
    {
        var world = World(dock: South, southLogs: 0, southPlanks: 2, production: false);
        var purchase = Evaluate(world);
        Assert.Equal(AutomationStage.ReturnLoading, purchase.Stage);
        Assert.Equal("planks", purchase.Trade!.Trade.GoodId);
        Assert.Equal(2, purchase.SelectedQuantity);
        Assert.Equal(35, purchase.Preview!.TotalAmount);
        Assert.Equal(North, purchase.Departure!.DestinationPortId);
        TradeRouteAutomation.Execute(world, purchase);
        TradeRouteAutomation.Advance(world, Policy());
        var sale = Evaluate(world);
        Assert.Equal(AutomationStage.ReturnSale, sale.Stage);
        Assert.Equal(North, sale.Trade!.PortId);
        Assert.Equal("planks", sale.Trade.Trade.GoodId);
        Assert.Equal(64, sale.Preview!.TotalAmount);
        Assert.Null(sale.Departure);
    }

    [Fact]
    public void EmptyReturnIsExplicitAndOnlyAppliesWithNoCargo()
    {
        var world = World(dock: South, southLogs: 0, production: false);
        Idle(Evaluate(world), AutomationAction.Wait, AutomationReason.InsufficientInventory);
        var permitted = Evaluate(world, Policy(emptyReturn: true));
        Assert.Equal(AutomationAction.Depart, permitted.Action);
        Assert.Equal(AutomationReason.EmptyReturnAllowed, permitted.Reason);
        Assert.Equal(North, permitted.Departure!.DestinationPortId);
        Assert.Null(permitted.Trade);
        var oneWay = new TradeRoutePolicy(North, South, Leg(), allowEmptyReturn: true);
        Assert.Equal(AutomationReason.EmptyReturnAllowed, Evaluate(world, oneWay).Reason);
        var partialReturn = World(dock: South, cargoPlanks: 1, southLogs: 0, production: false);
        Idle(Evaluate(partialReturn, Policy(emptyReturn: true)), AutomationAction.Wait, AutomationReason.InsufficientInventory);
        Assert.Equal(1, partialReturn.Snapshot().Ship.Cargo["planks"]);
    }

    [Fact]
    public void MixedOrUnexpectedCargoRequiresInterventionWithoutChangingIt()
    {
        var mixed = World(cargoLogs: 1, cargoPlanks: 1);
        var before = mixed.Snapshot();
        Idle(Evaluate(mixed), AutomationAction.Pause, AutomationReason.MixedCargo);
        Assert.Same(before, mixed.Snapshot());
        var unexpected = World(cargoPlanks: 1);
        var oneWay = new TradeRoutePolicy(North, South, Leg(), allowEmptyReturn: true);
        Idle(Evaluate(unexpected, oneWay), AutomationAction.Pause, AutomationReason.UnexpectedCargo);
        Assert.Equal(1, unexpected.Snapshot().Ship.Cargo["planks"]);
    }

    [Fact]
    public void PausingAndReconfiguringOnlyAffectFutureDecisions()
    {
        var world = World();
        var before = world.Snapshot();
        var oldDecision = Evaluate(world);
        var oldFingerprint = Decision(oldDecision);
        var restricted = Evaluate(world, Policy(buy: 8));
        Idle(RoutePolicyEvaluator.Evaluate(before, Policy(), enabled: false), AutomationAction.Pause, AutomationReason.Disabled);
        Assert.Equal(6, oldDecision.SelectedQuantity);
        Assert.Equal(2, restricted.SelectedQuantity);
        Assert.Same(before, world.Snapshot());
        world.Step(trades: new[] { new PortTradeCommand(North, TradeDirection.Buy, "logs", 1) });
        Assert.Equal(1, world.Snapshot().Ship.CargoUnits);
        Assert.Equal(oldFingerprint, Decision(oldDecision));
        Assert.Equal(0, before.Ship.CargoUnits);
    }

    [Fact]
    public void StaleAndForeignDecisionsCannotSubmitARejectedPurchaseAndDepart()
    {
        var world = World();
        var decision = Evaluate(world);
        var other = World();
        var otherBefore = other.Snapshot();
        Assert.Throws<InvalidOperationException>(() => TradeRouteAutomation.Execute(other, decision));
        Assert.Same(otherBefore, other.Snapshot());
        world.Step(trades: new[] { new PortTradeCommand(North, TradeDirection.Buy, "logs", 6) });
        var changed = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => TradeRouteAutomation.Execute(world, decision));
        Assert.Same(changed, world.Snapshot());
        Assert.Equal(North, changed.Ship.DockedPortId);
        Assert.Equal(6, changed.Ship.CargoUnits);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void AutomationKeepsDepartureTickAndNextStepArrivalTrading(int duration)
    {
        var world = World(travelTicks: duration);
        var first = TradeRouteAutomation.Advance(world, Policy());
        Assert.Equal(0, first.Decision.Tick);
        Assert.Equal(0, first.Result.Tick);
        Assert.Equal(1, first.Result.After.Tick);
        Assert.Single(first.Result.TradeAttempts);
        for (var tick = 1; tick < duration; tick++)
        {
            var next = TradeRouteAutomation.Advance(world, Policy());
            Assert.Equal(AutomationStage.InTransit, next.Decision.Stage);
            Assert.Equal(AutomationAction.ContinueSailing, next.Decision.Action);
            Assert.Equal(AutomationReason.Sailing, next.Decision.Reason);
            Assert.Null(next.Decision.Trade);
            Assert.Null(next.Decision.Departure);
            Assert.Empty(next.Result.TradeAttempts);
        }
        Assert.Equal(duration, world.Tick);
        Assert.Equal(South, world.Snapshot().Ship.DockedPortId);
        Assert.Equal(6, world.Snapshot().Ship.CargoUnits);
        Assert.Equal(AutomationAction.Sell, TradeRouteAutomation.Advance(world, Policy()).Decision.Action);
    }

    [Theory]
    [InlineData(10, 6, 119)]
    [InlineData(8, 2, 69)]
    public void AutomatedAndEquivalentManualExpeditionsMatchAtEveryTick(long limit, int logs, long profit)
    {
        var automatic = World();
        var manual = World();
        var initial = automatic.Snapshot();
        long costs = 0;
        long revenues = 0;
        for (var tick = 0; tick < 6; tick++)
        {
            var automated = TradeRouteAutomation.Advance(automatic, Policy(buy: limit));
            MaritimeStepResult expected = tick switch
            {
                0 => manual.Step(trades: new[] { new PortTradeCommand(North, TradeDirection.Buy, "logs", logs) },
                    sailCommands: new[] { new SailCommand(South) }),
                2 => manual.Step(trades: new[] { new PortTradeCommand(South, TradeDirection.Sell, "logs", logs) }),
                3 => manual.Step(trades: new[] { new PortTradeCommand(South, TradeDirection.Buy, "planks", 2) },
                    sailCommands: new[] { new SailCommand(North) }),
                5 => manual.Step(trades: new[] { new PortTradeCommand(North, TradeDirection.Sell, "planks", 2) }),
                _ => manual.Step()
            };
            Assert.Equal(Result(expected), Result(automated.Result));
            Assert.Equal(Cash(initial), Cash(automated.Result.After));
            foreach (var trade in automated.Result.TradeAttempts)
            {
                Assert.Equal(TradeOutcome.Traded, trade.Trade.Outcome);
                if (trade.Trade.Direction == TradeDirection.Buy) costs += trade.Trade.ExecutedAmount;
                else revenues += trade.Trade.ExecutedAmount;
            }
        }
        Assert.Equal(profit, revenues - costs);
        Assert.Equal(100 + profit, automatic.Snapshot().TraderCash);
        Assert.Equal(0, automatic.Snapshot().Ship.CargoUnits);
        Assert.Equal(North, automatic.Snapshot().Ship.DockedPortId);
        Assert.Equal(6, automatic.Tick);
        Assert.Equal(0, initial.Tick);
        Assert.Equal(18, initial.Ports[North].Inventory["logs"]);
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("linear")]
    [InlineData("scarcity")]
    public void SizingUsesExactAffordableTotalsForEveryPricingMode(string model)
    {
        var quote = model == "fixed" ? new MarketQuote("logs", 3, 4) :
            new MarketQuote("logs", new InventoryPricing(model == "linear" ? InventoryPricingModel.Linear : InventoryPricingModel.Scarcity,
                10, 20, 10, 1, 100, 1));
        var world = World(northLogs: 5, cash: 60, production: false,
            northQuotes: new[] { quote, new MarketQuote("planks", 4, 5) });
        var policy = Policy(maximum: 5, minimum: 1, buy: 100, reserve: 10);
        var snapshot = world.Snapshot();
        var feasible = Enumerable.Range(1, 5).Where(quantity =>
        {
            var preview = snapshot.PreviewTrade(North, new TradeCommand(TradeDirection.Buy, "logs", quantity));
            return preview.Outcome == TradeOutcome.Traded && preview.TotalAmount <= 50;
        }).Last();
        var decision = Evaluate(world, policy);
        Assert.Equal(feasible, decision.SelectedQuantity);
        Assert.Equal(JsonConvert.SerializeObject(decision.Preview),
            JsonConvert.SerializeObject(Assert.Single(TradeRouteAutomation.Execute(world, decision).TradeAttempts).Trade));
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("linear")]
    [InlineData("scarcity")]
    public void MaximumQuantityIsSelectedWithoutEnumeratingCargoUnits(string model)
    {
        var quote = model == "fixed" ? new MarketQuote("logs", 1, 1) :
            new MarketQuote("logs", new InventoryPricing(model == "linear" ? InventoryPricingModel.Linear : InventoryPricingModel.Scarcity,
                1, int.MaxValue, 0, 1, 1, 0));
        var world = World(northLogs: int.MaxValue, capacity: int.MaxValue, cash: long.MaxValue, northCash: 0,
            northQuotes: new[] { quote, new MarketQuote("planks", 1, 1) }, production: false);
        var decision = Evaluate(world, Policy(maximum: int.MaxValue, minimum: 1, buy: 1));
        Assert.Equal(int.MaxValue, decision.SelectedQuantity);
        Assert.Equal((long)int.MaxValue, decision.Preview!.TotalAmount);
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Equal(int.MaxValue, result.After.Ship.CargoUnits);
        Assert.Equal(0, result.After.Ports[North].Inventory["logs"]);
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("linear")]
    [InlineData("scarcity")]
    public void OversizedBudgetProbesSelectTheAffordableOrderWithoutHidingPublicOverflow(string model)
    {
        const long price = long.MaxValue / 2 + 1;
        var quote = model == "fixed" ? new MarketQuote("logs", price, price) :
            new MarketQuote("logs", new InventoryPricing(model == "linear" ? InventoryPricingModel.Linear : InventoryPricingModel.Scarcity,
                price, 10, 0, price, price, 0));
        var world = World(northLogs: int.MaxValue, capacity: int.MaxValue, cash: long.MaxValue, northCash: 0,
            northQuotes: new[] { quote, new MarketQuote("planks", 1, 1) }, production: false);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => before.PreviewTrade(North, new TradeCommand(TradeDirection.Buy, "logs", 2)));
        var decision = Evaluate(world, Policy(maximum: int.MaxValue, minimum: 1, buy: long.MaxValue));
        Assert.Equal(1, decision.SelectedQuantity);
        Assert.Equal(price, decision.Preview!.TotalAmount);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(price, Assert.Single(TradeRouteAutomation.Execute(world, decision).TradeAttempts).Trade.ExecutedAmount);
    }

    [Theory]
    [InlineData(TradeDirection.Buy)]
    [InlineData(TradeDirection.Sell)]
    public void ReceivingCashOverflowRemainsExceptionalRatherThanAWait(TradeDirection direction)
    {
        var world = direction == TradeDirection.Buy
            ? World(northCash: long.MaxValue)
            : World(dock: South, cargoLogs: 6, cash: long.MaxValue, southLogs: 0, production: false);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => Evaluate(world));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void AFullDestinationInventoryHasAnExplicitWaitReason()
    {
        var world = World(dock: South, cargoLogs: 6, southLogs: int.MaxValue, production: false);
        Idle(Evaluate(world), AutomationAction.Wait, AutomationReason.MarketInventoryFull);
        Assert.Equal(6, world.Snapshot().Ship.CargoUnits);
    }

    [Theory]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MaxValue - 1, 1)]
    public void SaleSizingRespectsIntegerStockHeadroomWithoutEnumeratingCargoUnits(int stock, int expected)
    {
        var world = World(dock: South, cargoLogs: int.MaxValue, capacity: int.MaxValue,
            southLogs: stock, southCash: long.MaxValue, cash: 0, production: false,
            southQuotes: new[] { new MarketQuote("logs", 1, 1), new MarketQuote("planks", 1, 1) });
        var decision = Evaluate(world, Policy(sell: 1));
        Assert.Equal(expected, decision.SelectedQuantity);
        var result = TradeRouteAutomation.Execute(world, decision);
        Assert.Equal(int.MaxValue, result.After.Ports[South].Inventory["logs"]);
        Assert.Equal(int.MaxValue - expected, result.After.Ship.Cargo["logs"]);
        Assert.Equal(expected, result.After.TraderCash);
        if (expected == 1)
            Idle(Evaluate(world, Policy(sell: 1)), AutomationAction.Wait, AutomationReason.MarketInventoryFull);
    }

    [Fact]
    public void ProductionFailureRollsBackAutomatedPurchaseDepartureAndEveryAccount()
    {
        var world = World(southPlanks: int.MaxValue);
        var before = world.Snapshot();
        var decision = Evaluate(world);
        Assert.Equal(AutomationAction.BuyAndDepart, decision.Action);
        Assert.Throws<OverflowException>(() => TradeRouteAutomation.Execute(world, decision));
        Assert.Same(before, world.Snapshot());
        Assert.Throws<OverflowException>(() => TradeRouteAutomation.Execute(world, decision));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(North, before.Ship.DockedPortId);
        Assert.Equal(0, before.Ship.CargoUnits);
        Assert.True(before.Ports[South].Buildings[0].ProductionEnabled);
    }

    [Fact]
    public void TickOverflowPropagatesThroughAutomationWithoutCommit()
    {
        var world = World(tick: long.MaxValue);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => TradeRouteAutomation.Advance(world, Policy()));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void RepeatedCultureAndReversedInputRunsProduceIdenticalDecisionsAndResults()
    {
        string Run(bool reverse)
        {
            var world = World(reverse: reverse,
                northQuotes: reverse ? NorthQuotes().Reverse() : NorthQuotes(),
                southQuotes: reverse ? SouthQuotes().Reverse() : SouthQuotes());
            var trace = new List<string>();
            for (var tick = 0; tick < 12; tick++)
            {
                var step = TradeRouteAutomation.Advance(world, Policy());
                trace.Add(Decision(step.Decision));
                trace.Add(Result(step.Result));
            }
            return string.Join("\n", trace);
        }
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var expected = Run(false);
            Assert.Equal(expected, Run(false));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, Run(true));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void AutomatedTradingConservesGoodsAndCashAcrossRepeatedTrips()
    {
        var fixedQuotes = new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 4, 5) };
        var world = World(northLogs: 30, southLogs: 0, southPlanks: 20, production: false,
            northQuotes: fixedQuotes, southQuotes: fixedQuotes);
        var policy = new TradeRoutePolicy(North, South, new RouteLeg("logs", 6, 2, 3, 2),
            new RouteLeg("planks", 2, 2, 5, 4));
        var before = world.Snapshot();
        for (var tick = 0; tick < 20; tick++)
        {
            var result = TradeRouteAutomation.Advance(world, policy).Result;
            Assert.Equal(Cash(before), Cash(result.After));
            Assert.Equal(Goods(before, "logs"), Goods(result.After, "logs"));
            Assert.Equal(Goods(before, "planks"), Goods(result.After, "planks"));
            Assert.All(result.TradeAttempts, attempt => Assert.Equal(TradeOutcome.Traded, attempt.Trade.Outcome));
            Assert.InRange(result.After.Ship.CargoUnits, 0, 6);
        }
    }

    [Fact]
    public void InvalidAndContradictoryPoliciesAreRejectedExplicitly()
    {
        Assert.Throws<ArgumentNullException>(() => new RouteLeg(null!, 6, 2, 10, 19));
        Assert.Throws<ArgumentException>(() => new RouteLeg("Bad ID", 6, 2, 10, 19));
        Assert.Throws<ArgumentOutOfRangeException>(() => Leg(maximum: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Leg(minimum: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Leg(minimum: 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => Leg(buy: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Leg(sell: 0));
        Assert.Throws<ArgumentNullException>(() => new TradeRoutePolicy(North, South, null!));
        Assert.Throws<ArgumentException>(() => new TradeRoutePolicy(North, North, Leg(), allowEmptyReturn: true));
        Assert.Throws<ArgumentException>(() => new TradeRoutePolicy(North, South, Leg(), Leg()));
        Assert.Throws<ArgumentException>(() => new TradeRoutePolicy(North, South, Leg()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(reserve: -1));
        Assert.Throws<ArgumentNullException>(() => RoutePolicyEvaluator.Evaluate(null!, Policy()));
        Assert.Throws<ArgumentNullException>(() => RoutePolicyEvaluator.Evaluate(World().Snapshot(), null!));
    }

    [Fact]
    public void UnknownPortsGoodsAndRequiredQuotesAreConfigurationErrors()
    {
        var world = World();
        var before = world.Snapshot();
        Assert.Throws<KeyNotFoundException>(() => Evaluate(world,
            new TradeRoutePolicy(North, "unknown", Leg(), allowEmptyReturn: true)));
        Assert.Throws<KeyNotFoundException>(() => Evaluate(world,
            new TradeRoutePolicy(North, South, new RouteLeg("unknown", 1, 1, 1, 1), allowEmptyReturn: true)));
        var noReturnQuote = World(northQuotes: new[] { NorthQuotes()[0] });
        Assert.Throws<KeyNotFoundException>(() => Evaluate(noReturnQuote));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void BoundedRunnerStopsAfterIdleTicksWithoutSubmittingRejectedTrades()
    {
        var world = World(dock: South, cargoLogs: 6, southLogs: 0, southCash: 0, production: false);
        var before = world.Snapshot();
        var observed = new List<AutomationStep>();
        var result = AutomationRunner.Run(world, Policy(), 20, 3, observed.Add);
        Assert.Equal(AutomationStopReason.NoProgress, result.StopReason);
        Assert.Equal(3, result.StepsExecuted);
        Assert.Equal(3, result.ConsecutiveNoProgressTicks);
        Assert.Equal(3, result.After.Tick);
        Assert.Same(before, result.Before);
        Assert.Same(world.Snapshot(), result.After);
        Assert.Equal(AutomationReason.InsufficientMarketFunds, result.LastDecision!.Reason);
        Assert.Equal(6, result.After.Ship.CargoUnits);
        Assert.Equal(South, result.After.Ship.DockedPortId);
        Assert.Equal(3, observed.Count);
        Assert.All(observed, step =>
        {
            Assert.Empty(step.Result.TradeAttempts);
            Assert.Empty(step.Result.MovementEvents);
        });
    }

    [Fact]
    public void ProductionProgressIsNotMistakenForAnIdleWorld()
    {
        var world = World(northLogs: 0, southLogs: 4);
        var result = AutomationRunner.Run(world, Policy(), 20, 3);
        Assert.Equal(AutomationStopReason.NoProgress, result.StopReason);
        Assert.Equal(5, result.StepsExecuted);
        Assert.Equal(3, result.ConsecutiveNoProgressTicks);
        Assert.Equal(2, result.After.Ports[South].Inventory["planks"]);
        Assert.Equal(0, result.After.Ports[South].Inventory["logs"]);
        Assert.Equal(AutomationReason.InsufficientInventory, result.LastDecision!.Reason);
    }

    [Fact]
    public void RunnerHasAFiniteTickBudgetEvenWhileTheRouteKeepsProgressing()
    {
        var world = World();
        var result = AutomationRunner.Run(world, Policy(), 6, 2, step =>
        {
            Assert.Same(world.Snapshot(), step.Result.After);
            Assert.Equal(step.Decision.Tick + 1, world.Tick);
        });
        Assert.Equal(AutomationStopReason.TickLimit, result.StopReason);
        Assert.Equal(6, result.StepsExecuted);
        Assert.Equal(0, result.ConsecutiveNoProgressTicks);
        Assert.Equal(219, result.After.TraderCash);
        Assert.Equal(0, result.After.Ship.CargoUnits);
        Assert.Equal(AutomationStage.ReturnSale, result.LastDecision!.Stage);
    }

    [Theory]
    [InlineData(false, 6, AutomationStopReason.Disabled)]
    [InlineData(true, 1, AutomationStopReason.Paused)]
    public void DisabledAndStructurallyPausedRunnerDoesNotConsumeATick(bool enabled, int capacity, AutomationStopReason reason)
    {
        var world = World(capacity: capacity);
        var before = world.Snapshot();
        var calls = 0;
        var result = AutomationRunner.Run(world, Policy(), 10, 3, _ => calls++, enabled);
        Assert.Equal(reason, result.StopReason);
        Assert.Equal(0, result.StepsExecuted);
        Assert.Equal(0, calls);
        Assert.Same(before, result.After);
        Assert.Same(before, world.Snapshot());
        world.Step();
        Assert.Equal(1, world.Tick);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void RunnerRejectsNonpositiveExecutionBounds(int ticks, int idleTicks)
    {
        var world = World();
        var before = world.Snapshot();
        Assert.Throws<ArgumentOutOfRangeException>(() => AutomationRunner.Run(world, Policy(), ticks, idleTicks));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void RunnerDoesNotConcealWorldFailureOrNotifyAnObserverBeforeCommit()
    {
        var world = World(southPlanks: int.MaxValue);
        var before = world.Snapshot();
        var calls = 0;
        Assert.Throws<OverflowException>(() => AutomationRunner.Run(world, Policy(), 10, 3, _ => calls++));
        Assert.Equal(0, calls);
        Assert.Same(before, world.Snapshot());

        var valid = World();
        Assert.Throws<InvalidOperationException>(() => AutomationRunner.Run(valid, Policy(), 10, 3,
            _ => throw new InvalidOperationException("Host observation failed after commit.")));
        Assert.Equal(1, valid.Tick);
        Assert.Equal(6, valid.Snapshot().Ship.CargoUnits);
        Assert.Null(valid.Snapshot().Ship.DockedPortId);
    }
}
