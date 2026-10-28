using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class IndustryTests
{
    private const string Owner = "company";
    private const string Facility = "private_sawmill";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static Dictionary<string, int> Stock(params (string Id, int Quantity)[] entries) =>
        entries.ToDictionary(e => e.Id, e => e.Quantity);
    private static IndustryBlueprint Blueprint(int capacity = 12, int interval = 2, int? ceiling = null,
        string building = "sawmill", string recipe = "saw_planks", string id = "sawmill_plan", long cost = 100) =>
        new(id, building, recipe, cost, interval, capacity, new[] { "north", "south" }, ceiling);
    private static RegionalPortConfiguration Port(ContentCatalog content, string id, long cash = 1000,
        IReadOnlyDictionary<string, int>? stock = null, MarketQuote? logQuote = null) =>
        new(new PortConfiguration(id, content.Goods.Select(g => g.Id == "logs" && logQuote != null ? logQuote :
            new MarketQuote(g.Id, g.Id == "planks" ? 8 : 2, g.Id == "planks" ? 9 : 3)), cash,
            Array.Empty<BuildingInstance>(), stock ?? Stock(("logs", 20), ("flour", 10), ("grain", 20))),
            id, 0, 0, Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
    private static RegionalWorldState World(long cash = 500, int shipCapacity = 8,
        IEnumerable<IndustryBlueprint>? blueprints = null, long marketCash = 1000, long day = 0, long revision = 0,
        IReadOnlyDictionary<string, int>? stock = null, MarketQuote? logQuote = null, bool secondShip = false)
    {
        var content = Content();
        var ships = new List<RegionalShipConfiguration>
        {
            new(new ShipConfiguration("ship", shipCapacity, "north"), Owner, "Player", 0, 100, isPlayer: true)
        };
        if (secondShip) ships.Add(new RegionalShipConfiguration(new ShipConfiguration("other", shipCapacity, "north"),
            Owner, "Other", 0, 100));
        return new RegionalWorldState(content, new[] { Port(content, "north", marketCash, stock, logQuote), Port(content, "south") },
            new[] { new SeaRoute("north", "south", 200) }, ships, day, revision,
            new[] { new CompanyConfiguration(Owner, cash) }, blueprints ?? new[] { Blueprint() });
    }
    private static TradeCommand Buy(int quantity, string good = "logs") => new(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(int quantity, string good = "planks") => new(TradeDirection.Sell, good, quantity);
    private static PrivateFacilitySnapshot Factory(RegionalWorldState world) => world.Snapshot().PrivateFacilities[Facility];
    private static BigInteger TotalCash(RegionalWorldSnapshot state) =>
        state.Companies.Values.Aggregate(BigInteger.Zero, (sum, c) => sum + c.Cash) +
        state.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.MarketCash);
    private static long TotalGoods(RegionalWorldSnapshot state, string id) =>
        state.Ports.Values.Sum(p => (long)p.Inventory[id]) + state.Ships.Values.Sum(s => (long)s.Cargo[id]) +
        state.PrivateFacilities.Values.Sum(f => (long)f.Inventory[id]);
    private static void Build(RegionalWorldState world, string blueprint = "sawmill_plan", string port = "north") =>
        Assert.Equal(ConstructionOutcome.Built, world.ConstructFacility(Owner, port, blueprint, Facility).Construction.Single().Outcome);
    private static IndustryPolicy Policy(int target = 4, long reserve = 0, long maxBuy = 3,
        bool sell = true, int outputReserve = 0, long minSell = 1) =>
        new(true, sell, reserve, new[] { new InputStockRule("logs", target, maxBuy) },
            new[] { new OutputSaleRule("planks", outputReserve, minSell) });

    [Fact]
    public void ExplicitCompanyOwnsOneAccountAcrossShipsAndIndustry()
    {
        var world = World(secondShip: true);
        var initial = world.Snapshot();
        Build(world);
        world.ExecuteTrade("ship", "north", Buy(2));
        var after = world.Snapshot();
        Assert.Single(after.Companies);
        Assert.All(after.Ships.Values, s => Assert.Equal(394, s.Cash));
        Assert.Equal(394, after.Companies[Owner].Cash);
        Assert.Equal(TotalCash(initial), TotalCash(after));
        Assert.Equal(500, initial.Ships["ship"].Cash);
        Assert.Empty(initial.PrivateFacilities);
    }

    [Fact]
    public void ConstructionPaysTownStartsEmptyAndSchedulesNoFreeBatch()
    {
        var world = World();
        var before = world.Snapshot();
        Build(world);
        var facility = Factory(world);
        Assert.Equal((0L, 1L, 400L, 1100L), (world.Snapshot().Day, world.Snapshot().Revision,
            world.Snapshot().Companies[Owner].Cash, world.Snapshot().Ports["north"].MarketCash));
        Assert.Equal((0L, 2L, 0, 0L), (facility.CreatedDay, facility.NextProductionDay,
            facility.StoredUnits, facility.BatchesProduced));
        Assert.All(facility.Inventory.Values, q => Assert.Equal(0, q));
        Assert.Equal(TotalCash(before), TotalCash(world.Snapshot()));
        Assert.Empty(world.AdvanceDay().PrivateProduction);
        Assert.Equal(ScheduledProductionOutcome.InputShortage, world.AdvanceDay().PrivateProduction.Single().Outcome);
        Assert.Equal(2, Factory(world).NextProductionDay);
    }

    [Fact]
    public void RejectedConstructionAndIllegalDuplicateLeaveExactSnapshot()
    {
        var poor = World(cash: 99);
        var before = poor.Snapshot();
        var rejected = poor.ConstructFacility(Owner, "north", "sawmill_plan", Facility);
        Assert.Equal(ConstructionOutcome.InsufficientFunds, rejected.Construction.Single().Outcome);
        Assert.Equal(0, rejected.Construction.Single().Cost);
        Assert.Same(before, rejected.After);
        var world = World();
        Build(world);
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(Owner, "north", "sawmill_plan", "duplicate"));
        Assert.Throws<ArgumentException>(() => world.ConstructFacility(Owner, "south", "sawmill_plan", Facility));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void ConstructionCashDeadlineAndRevisionOverflowRollBackCompletely()
    {
        foreach (var world in new[] { World(marketCash: long.MaxValue), World(day: long.MaxValue),
            World(revision: long.MaxValue) })
        {
            var before = world.Snapshot();
            Assert.Throws<OverflowException>(() => world.ConstructFacility(Owner, "north", "sawmill_plan", Facility));
            Assert.Same(before, world.Snapshot());
            Assert.Empty(world.Snapshot().PrivateFacilities);
        }
    }

    [Fact]
    public void ProductionConsumesOnlyOwnedInputsAndKeepsOutputsPrivate()
    {
        var world = World();
        Build(world);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(4));
        var market = world.Snapshot().Ports["north"].Inventory;
        world.AdvanceDay();
        var result = world.AdvanceDay();
        var facility = Factory(world);
        Assert.Equal(ScheduledProductionOutcome.Produced, result.PrivateProduction.Single().Outcome);
        Assert.Empty(result.Production);
        Assert.Equal((2, 1, 3, 1L, 4L), (facility.Inventory["logs"], facility.Inventory["planks"],
            facility.StoredUnits, facility.BatchesProduced, facility.NextProductionDay));
        Assert.Equal(2, facility.ConsumedQuantities["logs"]);
        Assert.Equal(1, facility.ProducedQuantities["planks"]);
        Assert.Equal(market, result.After.Ports["north"].Inventory);
        Assert.Equal(12, facility.ProcurementCost);
        Assert.Equal(0, facility.SalesRevenue);
    }

    [Fact]
    public void MultiInputRecipeCannotConsumeOneAvailableInputWhenOtherIsMissing()
    {
        var world = World(blueprints: new[] { Blueprint(building: "bakery", recipe: "bake_bread") });
        Build(world);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(1, "flour"));
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.InputShortage, Factory(world).LastOutcome);
        Assert.Equal(1, Factory(world).Inventory["flour"]);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(1));
        world.AdvanceDay();
        Assert.Equal((0, 0, 2), (Factory(world).Inventory["logs"], Factory(world).Inventory["flour"], Factory(world).Inventory["bread"]));
        Assert.Equal(5, Factory(world).NextProductionDay);
    }

    [Fact]
    public void TransfersConserveGoodsCashAndDayWithCapacityRejection()
    {
        var world = World(shipCapacity: 3, blueprints: new[] { Blueprint(capacity: 4) });
        Build(world);
        world.ExecuteTrade("ship", "north", Buy(3));
        var before = world.Snapshot();
        var moved = world.TransferGoods(Owner, "ship", Facility, TransferDirection.ShipToWarehouse, "logs", 2);
        Assert.Equal(TransferOutcome.Transferred, moved.Transfers.Single().Outcome);
        Assert.Equal(TotalCash(before), TotalCash(moved.After));
        Assert.Equal(TotalGoods(before, "logs"), TotalGoods(moved.After, "logs"));
        Assert.Equal(before.Day, moved.After.Day);
        Assert.Equal((1, 2), (moved.After.Ships["ship"].Cargo["logs"], Factory(world).Inventory["logs"]));
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(2));
        before = world.Snapshot();
        Assert.Equal(TransferOutcome.InsufficientInventory, world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.ShipToWarehouse, "logs", 2).Transfers.Single().Outcome);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(TransferOutcome.InsufficientCapacity, world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.ShipToWarehouse, "logs", 1).Transfers.Single().Outcome);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(TransferOutcome.InsufficientCapacity, world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.WarehouseToShip, "logs", 3).Transfers.Single().Outcome);
        Assert.Same(before, world.Snapshot());
        world.TransferGoods(Owner, "ship", Facility, TransferDirection.WarehouseToShip, "logs", 2);
        Assert.Equal((3, 2), (world.Snapshot().Ships["ship"].Cargo["logs"], Factory(world).Inventory["logs"]));
    }

    [Fact]
    public void TransfersRejectWrongOwnerRemoteSailingAndAutomatedShips()
    {
        var world = World();
        Build(world);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(2));
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods("stranger", "ship", Facility,
            TransferDirection.WarehouseToShip, "logs", 1));
        Assert.Throws<InvalidOperationException>(() => world.SetFacilityEnabled("stranger", Facility, false));
        Assert.Throws<InvalidOperationException>(() => world.ExecuteWarehouseTrade("stranger", Facility, Buy(1)));
        Assert.Throws<InvalidOperationException>(() => before.PreviewWarehouseTrade("stranger", Facility, Buy(1)));
        Assert.Same(before, world.Snapshot());
        world.ConfigureAutomation("ship", new TradeRoutePolicy("north", "south", new RouteLeg("logs", 2, 1, 10, 1),
            allowEmptyReturn: true), true);
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.WarehouseToShip, "logs", 1));
        world.SetAutomationEnabled("ship", false);
        world.Depart("ship", "south");
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.WarehouseToShip, "logs", 1));
        Assert.Same(before, world.Snapshot());
        world.AdvanceDay(); world.AdvanceDay();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.WarehouseToShip, "logs", 1));
    }

    [Fact]
    public void WarehouseSaleUsesExactMarketAccountingAndFiniteLiquidity()
    {
        var world = World(marketCash: 0, blueprints: new[] { Blueprint(cost: 1) });
        Build(world);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(2));
        world.AdvanceDay(); world.AdvanceDay();
        var before = world.Snapshot();
        var rejected = world.ExecuteWarehouseTrade(Owner, Facility, Sell(1));
        Assert.Equal(TradeOutcome.InsufficientMarketFunds, rejected.WarehouseTrades.Single().Trade.Outcome);
        Assert.Same(before, rejected.After);
        world.ExecuteTrade("ship", "north", Buy(1));
        before = world.Snapshot();
        var preview = before.PreviewWarehouseTrade(Owner, Facility, Sell(1));
        var sold = world.ExecuteWarehouseTrade(Owner, Facility, Sell(1), before);
        Assert.Equal(preview.ExecutedAmount, sold.WarehouseTrades.Single().Trade.ExecutedAmount);
        Assert.Equal(8, Factory(world).SalesRevenue);
        Assert.Equal(0, Factory(world).Inventory["planks"]);
        Assert.Equal(1, sold.After.Ports["north"].Inventory["planks"]);
        Assert.Equal(TotalCash(before), TotalCash(sold.After));
        Assert.Equal(TotalGoods(before, "planks"), TotalGoods(sold.After, "planks"));
    }

    [Fact]
    public void StaleShipAndWarehousePreviewsCannotExecuteOnTheSameDay()
    {
        var world = World();
        Build(world);
        var before = world.Snapshot();
        world.ExecuteTrade("ship", "north", Buy(1));
        var current = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("ship", "north", Buy(1), before));
        Assert.Throws<InvalidOperationException>(() => world.ExecuteWarehouseTrade(Owner, Facility, Buy(1), before));
        Assert.Same(current, world.Snapshot());
        Assert.Equal(before.Day, current.Day);
    }

    [Fact]
    public void PolicyRunsBoundedRealPurchasesProductionAndSalesWithoutToggles()
    {
        var world = World();
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy());
        var cash = TotalCash(world.Snapshot());
        for (var day = 1; day <= 6; day++)
        {
            var result = world.AdvanceDay();
            Assert.InRange(result.WarehouseTrades.Count, 0, 2);
            Assert.Equal(cash, TotalCash(result.After));
        }
        var facility = Factory(world);
        Assert.Equal(3, facility.BatchesProduced);
        Assert.Equal(3, facility.ProducedQuantities["planks"]);
        Assert.Equal(6, facility.ConsumedQuantities["logs"]);
        Assert.Equal((24L, 24L, 0, 2), (facility.ProcurementCost, facility.SalesRevenue,
            facility.Inventory["planks"], facility.Inventory["logs"]));
    }

    [Theory]
    [InlineData(3, 398, 20, IndustryWaitReason.CashReserve)]
    [InlineData(2, 0, 20, IndustryWaitReason.BuyPriceLimit)]
    [InlineData(3, 0, 0, IndustryWaitReason.InsufficientMarketInventory)]
    public void ProcurementWaitsExplainActualConstraints(long limit, long reserve, int logs, IndustryWaitReason expected)
    {
        var world = World(stock: Stock(("logs", logs)));
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy(reserve: reserve, maxBuy: limit));
        var result = world.AdvanceDay();
        Assert.Empty(result.WarehouseTrades);
        Assert.Equal(expected, Factory(world).ProcurementWaitReason);
        Assert.Equal(400, result.After.Companies[Owner].Cash);
    }

    [Fact]
    public void ProcurementPurchasesOnlyAffordablePrefixAndPreservesReserve()
    {
        var world = World();
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy(target: 10, reserve: 390, sell: false));
        var result = world.AdvanceDay();
        Assert.Equal(3, result.WarehouseTrades.Single().Trade.ExecutedQuantity);
        Assert.Equal(391, result.After.Companies[Owner].Cash);
        Assert.Equal(3, Factory(world).Inventory["logs"]);
    }

    [Fact]
    public void SalesRespectMinimumPriceWarehouseReserveAndMarketCash()
    {
        var world = World(blueprints: new[] { Blueprint(interval: 1) });
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy(outputReserve: 1, minSell: 9));
        world.AdvanceDay(); world.AdvanceDay();
        Assert.Equal(IndustryWaitReason.SellPriceLimit, Factory(world).SalesWaitReason);
        Assert.Equal(2, Factory(world).Inventory["planks"]);
        world.ConfigureFacility(Owner, Facility, Policy(outputReserve: 1));
        var result = world.AdvanceDay();
        Assert.Equal(2, result.WarehouseTrades.Single(t => t.Trade.Direction == TradeDirection.Sell).Trade.ExecutedQuantity);
        Assert.Equal(1, Factory(world).Inventory["planks"]);
        Assert.Equal(16, Factory(world).SalesRevenue);
    }

    [Fact]
    public void PrivateExtractionStopsAtFiniteCapacityAndResumesWithoutCatchUp()
    {
        var world = World(blueprints: new[] { Blueprint(capacity: 3, interval: 1,
            building: "lumber_camp", recipe: "harvest_logs") });
        Build(world);
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.WarehouseCapacity, Factory(world).LastOutcome);
        Assert.Equal((2, 1L, 2L), (Factory(world).StoredUnits, Factory(world).BatchesProduced, Factory(world).NextProductionDay));
        world.TransferGoods(Owner, "ship", Facility, TransferDirection.WarehouseToShip, "logs", 1);
        world.AdvanceDay();
        Assert.Equal((3, 2L, 4L), (Factory(world).StoredUnits, Factory(world).BatchesProduced, Factory(world).NextProductionDay));
    }

    [Fact]
    public void DisabledAndOutputLimitedFactoriesKeepDeadlineAndInputs()
    {
        var world = World(blueprints: new[] { Blueprint(interval: 1, ceiling: 1) });
        Build(world);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy(6));
        world.SetFacilityEnabled(Owner, Facility, false);
        world.AdvanceDay();
        Assert.Equal((1L, 6, ScheduledProductionOutcome.Disabled), (Factory(world).NextProductionDay,
            Factory(world).Inventory["logs"], Factory(world).LastOutcome));
        world.SetFacilityEnabled(Owner, Facility, true);
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal((3L, 4, 1, ScheduledProductionOutcome.OutputLimit), (Factory(world).NextProductionDay,
            Factory(world).Inventory["logs"], Factory(world).Inventory["planks"], Factory(world).LastOutcome));
        world.ExecuteWarehouseTrade(Owner, Facility, Sell(1));
        world.AdvanceDay();
        Assert.Equal(2, Factory(world).BatchesProduced);
    }

    [Fact]
    public void PolicyValidationAndWrongOwnerCannotCommitChanges()
    {
        var world = World();
        Build(world);
        var before = world.Snapshot();
        Assert.Throws<ArgumentException>(() => world.ConfigureFacility(Owner, Facility,
            new IndustryPolicy(inputs: new[] { new InputStockRule("planks", 1, 10) })));
        Assert.Throws<ArgumentException>(() => world.ConfigureFacility(Owner, Facility,
            new IndustryPolicy(outputs: new[] { new OutputSaleRule("logs", 0, 1) })));
        Assert.Throws<ArgumentException>(() => world.ConfigureFacility(Owner, Facility, Policy(target: 13)));
        Assert.Throws<InvalidOperationException>(() => world.ConfigureFacility("stranger", Facility, Policy()));
        Assert.Throws<ArgumentException>(() => new IndustryPolicy(inputs: new[] {
            new InputStockRule("logs", 1, 2), new InputStockRule("logs", 2, 3) }));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void LateProductionScheduleOverflowRollsBackEarlierPurchasesAndPrivateChanges()
    {
        var world = World(day: long.MaxValue - 2);
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy());
        world.AdvanceDay();
        world.ConfigureFacility(Owner, Facility, Policy(target: 6));
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0, Factory(world).BatchesProduced);
        Assert.Equal(4, Factory(world).Inventory["logs"]);
    }

    [Fact]
    public void AutomaticPrivateSalesCannotCreateMarketLiquidity()
    {
        var world = World(marketCash: 0, blueprints: new[] { Blueprint(cost: 1, interval: 1) });
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy(target: 2));
        var result = world.AdvanceDay();
        Assert.Single(result.WarehouseTrades);
        Assert.Equal(TradeDirection.Buy, result.WarehouseTrades.Single().Trade.Direction);
        Assert.Equal(IndustryWaitReason.InsufficientMarketFunds, Factory(world).SalesWaitReason);
        Assert.Equal(1, Factory(world).Inventory["planks"]);
        Assert.Equal(0, Factory(world).SalesRevenue);
        Assert.Equal(7, result.After.Ports["north"].MarketCash);
    }

    [Fact]
    public void RemoteFacilityManagementTradesLocallyWithoutMovingShipCargo()
    {
        var world = World();
        Build(world, port: "south");
        var before = world.Snapshot();
        var result = world.ExecuteWarehouseTrade(Owner, Facility, Buy(2));
        Assert.Equal("north", result.After.Ships["ship"].DockedPortId);
        Assert.Equal(0, result.After.Ships["ship"].CargoUnits);
        Assert.Equal(before.Ports["north"].Inventory["logs"], result.After.Ports["north"].Inventory["logs"]);
        Assert.Equal(before.Ports["south"].Inventory["logs"] - 2, result.After.Ports["south"].Inventory["logs"]);
        Assert.Equal(2, Factory(world).Inventory["logs"]);
        Assert.Equal(before.Day, result.After.Day);
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.WarehouseToShip, "logs", 1));
    }

    [Fact]
    public void MaximumQuantityUsesExactCurvesAndCashCapacityWithoutOverflowingProbes()
    {
        var quote = new MarketQuote("logs", new InventoryPricing(InventoryPricingModel.Linear, 8, 20, 8, 1, 20, 1));
        var world = World(cash: 150, shipCapacity: 20, logQuote: quote);
        var state = world.Snapshot();
        var maximum = state.MaximumTradeQuantity("ship", "north", TradeDirection.Buy, "logs");
        Assert.True(maximum > 0 && maximum < 20);
        Assert.Equal(TradeOutcome.Traded, state.PreviewTrade("ship", "north", Buy(maximum)).Outcome);
        Assert.Equal(TradeOutcome.InsufficientTraderFunds, state.PreviewTrade("ship", "north", Buy(maximum + 1)).Outcome);
        var huge = World(cash: long.MaxValue, shipCapacity: int.MaxValue,
            stock: Stock(("logs", int.MaxValue)), logQuote: new MarketQuote("logs", long.MaxValue, long.MaxValue), marketCash: 0);
        Assert.Equal(1, huge.Snapshot().MaximumTradeQuantity("ship", "north", TradeDirection.Buy, "logs"));
        huge.ExecuteTrade("ship", "north", Buy(1));
        Assert.Equal(1, huge.Snapshot().MaximumTradeQuantity("ship", "north", TradeDirection.Sell, "logs"));
    }

    [Fact]
    public void MaximumQuantityBoundsReceivingAccountsAndWarehouseCapacity()
    {
        var world = World(marketCash: long.MaxValue - 102, blueprints: new[] { Blueprint(capacity: 3) });
        Build(world);
        Assert.Equal(0, world.Snapshot().MaximumWarehouseTradeQuantity(Owner, Facility, TradeDirection.Buy, "logs"));
        var normal = World(blueprints: new[] { Blueprint(capacity: 3) });
        Build(normal);
        Assert.Equal(3, normal.Snapshot().MaximumWarehouseTradeQuantity(Owner, Facility, TradeDirection.Buy, "logs"));
        normal.ExecuteWarehouseTrade(Owner, Facility, Buy(2));
        Assert.Equal(1, normal.Snapshot().MaximumWarehouseTradeQuantity(Owner, Facility, TradeDirection.Buy, "flour"));
    }

    [Fact]
    public void LifetimePurchaseCounterOverflowRollsBackTheWholeWarehouseTrade()
    {
        var price = long.MaxValue / 2;
        var world = World(cash: long.MaxValue, marketCash: 0, logQuote: new MarketQuote("logs", price, price),
            blueprints: new[] { Blueprint(cost: 1, capacity: 1) });
        Build(world);
        for (var i = 0; i < 2; i++)
        {
            world.ExecuteWarehouseTrade(Owner, Facility, Buy(1));
            world.ExecuteWarehouseTrade(Owner, Facility, Sell(1, "logs"));
        }
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.ExecuteWarehouseTrade(Owner, Facility, Buy(1)));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(long.MaxValue - 1, Factory(world).ProcurementCost);
        Assert.Equal(0, Factory(world).Inventory["logs"]);
    }

    [Fact]
    public void ManualReceiptOverflowRollsBackWhileAutomaticMaximumWaits()
    {
        var world = World(cash: long.MaxValue, blueprints: new[] { Blueprint(cost: 1, interval: 1,
            building: "lumber_camp", recipe: "harvest_logs") });
        Build(world);
        world.AdvanceDay();
        var before = world.Snapshot();
        Assert.Equal(0, before.MaximumWarehouseTradeQuantity(Owner, Facility, TradeDirection.Sell, "logs"));
        Assert.Throws<OverflowException>(() => world.ExecuteWarehouseTrade(Owner, Facility, Sell(1, "logs")));
        Assert.Same(before, world.Snapshot());
        world.ConfigureFacility(Owner, Facility, new IndustryPolicy(salesEnabled: true,
            outputs: new[] { new OutputSaleRule("logs", 0, 1) }));
        var result = world.AdvanceDay();
        Assert.Empty(result.WarehouseTrades);
        Assert.Equal(IndustryWaitReason.CashAccountFull, Factory(world).SalesWaitReason);
        Assert.Equal(long.MaxValue - 1, result.After.Companies[Owner].Cash);
    }

    [Fact]
    public void PrivateProcurementCompetesWithPublicProductionBeforePrivateBatches()
    {
        var content = Content();
        var publicMill = new BuildingInstance("public_mill", "sawmill", "saw_planks");
        var north = new RegionalPortConfiguration(new PortConfiguration("north",
            content.Goods.Select(g => new MarketQuote(g.Id, 2, 3)), 1000, new[] { publicMill }, Stock(("logs", 2))),
            "North", 0, 0, new[] { new ScheduledFacilityConfiguration(publicMill, 1) }, Array.Empty<ConsumptionConfiguration>());
        var world = new RegionalWorldState(content, new[] { north, Port(content, "south") },
            new[] { new SeaRoute("north", "south", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration("ship", 8, "north"), Owner, "Player", 500, 100) },
            industryBlueprints: new[] { Blueprint(interval: 1) });
        Build(world);
        world.ConfigureFacility(Owner, Facility, Policy(target: 2, sell: false));
        var result = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.InputShortage, result.Production.Single().Outcome);
        Assert.Equal(ScheduledProductionOutcome.Produced, result.PrivateProduction.Single().Outcome);
        Assert.Equal(0, result.After.Ports["north"].Inventory["planks"]);
        Assert.Equal(1, Factory(world).Inventory["planks"]);
    }

    [Fact]
    public void BlueprintEligibilityCanonicalReferencesAndExplicitAccountsAreValidated()
    {
        Assert.Throws<ArgumentException>(() => World(blueprints: new[] { Blueprint(building: "bakery") }));
        Assert.Throws<ArgumentException>(() => World(blueprints: new[] {
            new IndustryBlueprint("bad", "sawmill", "saw_planks", 1, 1, 1, new[] { "missing" }) }));
        var world = World(blueprints: new[] { new IndustryBlueprint("sawmill_plan", "sawmill", "saw_planks",
            100, 2, 12, new[] { "south" }) });
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(Owner, "north", "sawmill_plan", Facility));
        Assert.Same(before, world.Snapshot());
        var content = Content();
        Assert.Throws<ArgumentException>(() => new RegionalWorldState(content, new[] { Port(content, "north"), Port(content, "south") },
            new[] { new SeaRoute("north", "south", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration("ship", 8, "north"), Owner, "Player", 1, 100) },
            companies: new[] { new CompanyConfiguration(Owner, 500) }));
    }

    [Fact]
    public void FactoriesCompeteDeterministicallyForFiniteLocalInputs()
    {
        static string Run(bool reverse)
        {
            var saw = Blueprint(id: "sawmill_plan", interval: 1);
            var bake = Blueprint(id: "bakery_plan", building: "bakery", recipe: "bake_bread", interval: 1);
            var world = World(stock: Stock(("logs", 2), ("flour", 1)),
                blueprints: reverse ? new[] { bake, saw } : new[] { saw, bake });
            var constructions = new[] { ("a_sawmill", "sawmill_plan"), ("z_bakery", "bakery_plan") };
            foreach (var (id, blueprint) in reverse ? constructions.Reverse() : constructions)
                world.ConstructFacility(Owner, "north", blueprint, id);
            world.ConfigureFacility(Owner, "a_sawmill", Policy(target: 2, sell: false));
            world.ConfigureFacility(Owner, "z_bakery", new IndustryPolicy(true, inputs: new[] {
                new InputStockRule("logs", 1, 3), new InputStockRule("flour", 1, 3) }));
            var result = world.AdvanceDay();
            Assert.Equal(ScheduledProductionOutcome.Produced, result.After.PrivateFacilities["a_sawmill"].LastOutcome);
            Assert.Equal(ScheduledProductionOutcome.InputShortage, result.After.PrivateFacilities["z_bakery"].LastOutcome);
            Assert.Equal(IndustryWaitReason.InsufficientMarketInventory, result.After.PrivateFacilities["z_bakery"].ProcurementWaitReason);
            return JsonConvert.SerializeObject(new { result.After.Companies,
                Facilities = result.After.PrivateFacilities.Values.Select(f => new { f.Id, f.Inventory, f.ProcurementCost, f.LastOutcome }) });
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void ReorderedCanonicalGoodsPreservePrivateSnapshotOrderingAndEconomicOutcomes()
    {
        var forwardContent = Content();
        var reversedContent = ContentLoader.Load(ContentLoaderTests.Change(root =>
            root["goods"] = new Newtonsoft.Json.Linq.JArray(root["goods"]!.Reverse())));
        var forward = RunScenario.Create(forwardContent, new RunIdentity(null, RunScenario.MerchantCharter, seed: 99));
        var reversed = RunScenario.Create(reversedContent, new RunIdentity(null, RunScenario.MerchantCharter, seed: 99));
        foreach (var world in new[] { forward, reversed })
        {
            world.ConstructFacility(WorldIds.PlayerOwnerId, WorldIds.West,
                WorldIds.BakeryBlueprint, Facility);
            world.ConfigureWarehouse(WorldIds.PlayerOwnerId, WorldIds.West, ExpandingEconomyScenario.BakeryWarehousePolicy(true), world.Snapshot());
        }
        var expectedIds = forwardContent.ForExpandingEconomy().Goods.Select(g => g.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        for (var day = 0; day <= 12; day++)
        {
            var first = forward.Snapshot();
            var second = reversed.Snapshot();
            foreach (var snapshot in new[] { first, second })
            {
                var facility = snapshot.PrivateFacilities[Facility];
                Assert.Equal(expectedIds, facility.Inventory.Keys);
                Assert.Equal(expectedIds, facility.ProducedQuantities.Keys);
                Assert.Equal(expectedIds, facility.ConsumedQuantities.Keys);
            }
            Assert.Equal(JsonConvert.SerializeObject(first.PrivateFacilities), JsonConvert.SerializeObject(second.PrivateFacilities));
            Assert.Equal(first.Companies[WorldIds.PlayerOwnerId].Cash, second.Companies[WorldIds.PlayerOwnerId].Cash);
            foreach (var good in expectedIds) Assert.Equal(TotalGoods(first, good), TotalGoods(second, good));
            Assert.Equal(TotalCash(first), TotalCash(second));
            if (day < 12) { forward.AdvanceDay(); reversed.AdvanceDay(); }
        }
        Assert.True(forward.Snapshot().PrivateFacilities[Facility].BatchesProduced > 0);
        Assert.True(forward.Snapshot().GetFacilityStorage(Facility).SalesRevenue > 0);
    }
}
