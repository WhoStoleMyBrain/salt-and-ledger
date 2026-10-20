using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class IndustrialRouteTests
{
    private const string Owner = "company";
    private const string Ship = "ship";
    private const string Source = "north_factory";
    private const string Destination = "south_factory";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static TradeCommand Buy(string good, int amount) => new(TradeDirection.Buy, good, amount);

    private static RegionalWorldState World(int capacity = 10, long southCash = 1000,
        MarketQuote? destinationQuote = null, bool overflowProduction = false, int warehouseCapacity = 30,
        long cash = 1000, long revision = 0)
    {
        var content = Content();
        RegionalPortConfiguration Port(string id)
        {
            var production = overflowProduction && id == "south"
                ? new[] { new BuildingInstance("overflow_camp", "lumber_camp", "harvest_logs") }
                : Array.Empty<BuildingInstance>();
            var stock = content.Goods.ToDictionary(g => g.Id,
                g => overflowProduction && id == "south" && g.Id == "logs" ? int.MaxValue : 20);
            return new RegionalPortConfiguration(new PortConfiguration(id, content.Goods.Select(g =>
                id == "south" && g.Id == "planks" && destinationQuote != null ? destinationQuote :
                    new MarketQuote(g.Id, id == "south" ? 10 : 2, id == "south" ? 11 : 3)),
                id == "south" ? southCash : 1000, production, stock), id, 0, 0,
                production.Select(p => new ScheduledFacilityConfiguration(p, 1)),
                Array.Empty<ConsumptionConfiguration>());
        }
        return new RegionalWorldState(content, new[] { Port("north"), Port("south") },
            new[] { new SeaRoute("north", "south", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration(Ship, capacity, "north"), Owner,
                "Player", 0, 100, isPlayer: true) }, initialRevision: revision,
            companies: new[] { new CompanyConfiguration(Owner, cash), new CompanyConfiguration("other_company", 1000) },
            industryBlueprints: new[] { new IndustryBlueprint("plan", "sawmill", "saw_planks", 10, 2,
                warehouseCapacity, new[] { "north", "south" }) });
    }

    private static void Build(RegionalWorldState world, string id = Source, string port = "north", string owner = Owner)
    {
        world.ConstructFacility(owner, port, "plan", id);
        world.SetFacilityEnabled(owner, id, false);
    }

    private static IndustrialRoutePolicy WarehouseRoute(int target = 6, int minimum = 2, int reserve = 0,
        int threshold = 0, long floor = 1, IndustrialRouteLeg? returnLeg = null, bool emptyReturn = true) =>
        new("north", "south", new IndustrialRouteLeg("planks", target, minimum,
            IndustrialEndpointKind.Warehouse, sourceFacilityId: Source,
            minimumSourceStock: threshold, warehouseReserve: reserve, minimumSellPrice: floor),
            returnLeg, allowEmptyReturn: emptyReturn);

    private static void Configure(RegionalWorldState world, IndustrialRoutePolicy? policy = null) =>
        world.ConfigureIndustrialAutomation(Ship, policy ?? WarehouseRoute(), true);

    private static void StockSource(RegionalWorldState world, int quantity = 8)
    {
        Build(world);
        world.ExecuteWarehouseTrade(Owner, Source, Buy("planks", quantity));
    }

    private static IndustrialRouteDecision Evaluate(RegionalWorldState world) =>
        IndustrialRouteEvaluator.Evaluate(world.Snapshot(), Ship);

    private static string Economy(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
    {
        snapshot.Day,
        snapshot.Companies, snapshot.Accounting,
        Ports = snapshot.Ports.Values.Select(p => new { p.Id, p.Inventory, p.MarketCash, p.Facilities, p.Consumption }),
        Ships = snapshot.Ships.Values.Select(s => new { s.Id, s.Ship, s.Cash, s.VoyageDurationDays }),
        snapshot.PrivateFacilities
    });

    [Fact]
    public void WarehouseLoadingRespectsReserveAndCapacityWithoutCashOrDay()
    {
        var world = World(capacity: 4);
        StockSource(world);
        Configure(world, WarehouseRoute(reserve: 2));
        var before = world.Snapshot();
        var decision = Evaluate(world);
        Assert.Equal(IndustrialRouteAction.LoadAndDepart, decision.Action);
        Assert.Equal(4, decision.SelectedQuantity);
        Assert.Same(before, decision.Before);
        var result = world.RunAutomation(Ship);
        Assert.Equal(4, result.After.Ships[Ship].Cargo["planks"]);
        Assert.Equal(4, result.After.PrivateFacilities[Source].Inventory["planks"]);
        Assert.Equal(before.Companies[Owner].Cash, result.After.Companies[Owner].Cash);
        Assert.Equal(12, result.After.Accounting.Stores["ship:" + Ship].GetCostBasis("planks"));
        Assert.Equal(12, result.After.Accounting.Stores["facility:" + Source].GetCostBasis("planks"));
        Assert.Equal(before.Accounting.Companies[Owner].HeldInventoryCost,
            result.After.Accounting.Companies[Owner].HeldInventoryCost);
        Assert.Equal(before.Accounting.Companies[Owner].InventoryPurchases,
            result.After.Accounting.Companies[Owner].InventoryPurchases);
        Assert.Equal(before.Day, result.After.Day);
        Assert.Equal(before.Revision + 1, result.After.Revision);
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, result.After.Ships[Ship].IndustrialStage);
        Assert.Equal("south", result.After.Ships[Ship].DestinationPortId);
        Assert.Single(result.Transfers);
        Assert.Empty(result.Trades);
        Assert.Single(result.Decisions);
    }

    [Fact]
    public void StockThresholdAndReserveWaitWithoutChangingState()
    {
        var world = World();
        StockSource(world, 8);
        Configure(world, WarehouseRoute(threshold: 9));
        var before = world.Snapshot();
        Assert.Contains("reach 9", Evaluate(world).Reason);
        Assert.Same(before, world.RunAutomation(Ship).After);
        world.ConfigureIndustrialAutomation(Ship, WarehouseRoute(reserve: 7), true);
        before = world.Snapshot();
        Assert.Contains("reserve", Evaluate(world).Reason);
        Assert.Same(before, world.RunAutomation(Ship).After);
        Assert.Equal(8, before.PrivateFacilities[Source].Inventory["planks"]);
    }

    [Fact]
    public void MarketBuyingUsesReserveAndPerOrderSpendCap()
    {
        var world = World(cash: 100);
        var policy = new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("logs", 10, 2, maximumBuyPrice: 3, maximumPurchaseExpenditure: 9),
            cashReserve: 95);
        Configure(world, policy);
        Assert.Contains("cash reserve", Evaluate(world).Reason);
        policy = new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("logs", 10, 2, maximumBuyPrice: 3, maximumPurchaseExpenditure: 9),
            cashReserve: 90);
        world.ConfigureIndustrialAutomation(Ship, policy, true);
        var result = world.RunAutomation(Ship);
        Assert.Equal(3, result.After.Ships[Ship].Cargo["logs"]);
        Assert.Equal(91, result.After.Companies[Owner].Cash);
        Assert.Equal(9, result.Trades.Single().Trade.ExecutedAmount);
    }

    [Fact]
    public void InvalidWarehouseOwnershipOrEndpointRejectsConfiguration()
    {
        var world = World();
        Build(world, owner: "other_company");
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => Configure(world));
        Assert.Same(before, world.Snapshot());
        Build(world, Destination, "south");
        var wrongPort = new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("planks", 6, 2, IndustrialEndpointKind.Warehouse, sourceFacilityId: Destination));
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => Configure(world, wrongPort));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void LoadingCannotRunRemotelyOrReconfigureAFullOrSailingShip()
    {
        var world = World();
        StockSource(world);
        Configure(world);
        world.SetAutomationEnabled(Ship, false);
        world.Depart(Ship, "south");
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() =>
            world.TransferGoods(Owner, Ship, Source, TransferDirection.WarehouseToShip, "planks", 2));
        Assert.Throws<InvalidOperationException>(() => Configure(world));
        Assert.Same(before, world.Snapshot());
        world.AdvanceDay();
        world.SetAutomationEnabled(Ship, true);
        Assert.Equal(IndustrialRouteAction.Pause, Evaluate(world).Action);
        before = world.Snapshot();
        Assert.Same(before, world.RunAutomation(Ship).After);
        world.SetAutomationEnabled(Ship, false);
        world.ExecuteTrade(Ship, "south", Buy("logs", 1));
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => Configure(world));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void DestinationSaleUsesExactMarginalFloorAndRetainsBlockedCargo()
    {
        var pricing = new InventoryPricing(InventoryPricingModel.Linear, 20, 20, 20, 1, 100, 1);
        var world = World(destinationQuote: new MarketQuote("planks", pricing));
        StockSource(world);
        Configure(world, WarehouseRoute(floor: 16));
        world.RunAutomation(Ship);
        world.AdvanceDay();
        var before = world.Snapshot();
        var expected = before.PreviewTrade(Ship, "south", new TradeCommand(TradeDirection.Sell, "planks", 3));
        var result = world.RunAutomation(Ship);
        var sale = Assert.Single(result.Trades).Trade;
        Assert.Equal(3, sale.ExecutedQuantity);
        Assert.Equal(expected.ExecutedAmount, sale.ExecutedAmount);
        Assert.Equal(51, sale.ExecutedAmount);
        Assert.Equal(3, result.After.Ships[Ship].Cargo["planks"]);
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, result.After.Ships[Ship].IndustrialStage);
        Assert.Equal("south", result.After.Ships[Ship].DockedPortId);
        Assert.Contains("selling floor", Evaluate(world).Reason);
        Assert.Equal(before.Companies[Owner].Cash + 51, result.After.Companies[Owner].Cash);
    }

    [Fact]
    public void DestinationWithoutLiquidityRetainsEntireShipmentAndPhase()
    {
        var world = World(southCash: 0);
        StockSource(world);
        Configure(world);
        world.RunAutomation(Ship);
        world.AdvanceDay();
        var before = world.Snapshot();
        Assert.Contains("cannot pay", Evaluate(world).Reason);
        Assert.Same(before, world.RunAutomation(Ship).After);
        Assert.Equal(6, before.Ships[Ship].Cargo["planks"]);
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, before.Ships[Ship].IndustrialStage);
    }

    [Fact]
    public void ExplicitStageSupportsSameCommodityReturnIntoPrivateStorage()
    {
        var world = World();
        StockSource(world, 8);
        var returnLeg = new IndustrialRouteLeg("planks", 2, 2,
            destinationKind: IndustrialEndpointKind.Warehouse, destinationFacilityId: Source);
        Configure(world, WarehouseRoute(returnLeg: returnLeg, emptyReturn: false));
        world.RunAutomation(Ship);
        world.AdvanceDay();
        var turnaround = world.RunAutomation(Ship);
        Assert.Equal(new[] { TradeDirection.Sell, TradeDirection.Buy }, turnaround.Trades.Select(t => t.Trade.Direction));
        Assert.Equal(IndustrialRouteStage.DeliverReturn, turnaround.After.Ships[Ship].IndustrialStage);
        Assert.Equal(2, turnaround.After.Ships[Ship].Cargo["planks"]);
        Assert.Equal("north", turnaround.After.Ships[Ship].DestinationPortId);
        Assert.InRange(turnaround.Decisions.Count, 1, RegionalWorldState.MaximumDocksideActions);
        world.AdvanceDay();
        var unloaded = world.RunAutomation(Ship, 1);
        Assert.Equal(2, Assert.Single(unloaded.Transfers).TransferredQuantity);
        Assert.Equal(TransferDirection.ShipToWarehouse, unloaded.Transfers.Single().Direction);
        Assert.Equal(IndustrialRouteStage.AcquireOutbound, unloaded.After.Ships[Ship].IndustrialStage);
        Assert.Equal(4, unloaded.After.PrivateFacilities[Source].Inventory["planks"]);
        Assert.Equal(0, unloaded.After.Ships[Ship].CargoUnits);
    }

    [Fact]
    public void WarehouseDeliveryUnloadsOnlyPhysicalCapacityAndRetainsRemainder()
    {
        var world = World(warehouseCapacity: 8);
        StockSource(world, 8);
        Build(world, Destination, "south");
        world.ExecuteWarehouseTrade(Owner, Destination, Buy("logs", 6));
        Configure(world, new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("planks", 6, 2, IndustrialEndpointKind.Warehouse, IndustrialEndpointKind.Warehouse,
                Source, Destination)));
        world.RunAutomation(Ship);
        world.AdvanceDay();
        var result = world.RunAutomation(Ship);
        Assert.Equal(2, Assert.Single(result.Transfers).TransferredQuantity);
        Assert.Equal(8, result.After.PrivateFacilities[Destination].StoredUnits);
        Assert.Equal(4, result.After.Ships[Ship].Cargo["planks"]);
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, result.After.Ships[Ship].IndustrialStage);
        Assert.Contains("warehouse is full", Evaluate(world).Reason);
    }

    [Fact]
    public void PrivateTransfersNeedNoPublicQuoteAndRetainStarterGrantProvenance()
    {
        var content = Content();
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id, Array.Empty<MarketQuote>(),
            0, Array.Empty<BuildingInstance>()), id, 0, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            Array.Empty<ConsumptionConfiguration>());
        var world = new RegionalWorldState(content, new[] { Port("north"), Port("south") },
            new[] { new SeaRoute("north", "south", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration(Ship, 8, "north"), Owner, "Player", 0, 100) },
            companies: new[] { new CompanyConfiguration(Owner, 0) },
            industryBlueprints: new[] { new IndustryBlueprint("plan", "sawmill", "saw_planks", 10, 2, 10,
                new[] { "north", "south" }) },
            initialFacilities: new[]
            {
                new PrivateFacilityConfiguration(Source, Owner, "north", "plan",
                    new Dictionary<string, int> { ["planks"] = 6 }, enabled: false, provenance: "test_charter"),
                new PrivateFacilityConfiguration(Destination, Owner, "south", "plan", enabled: false)
            });
        Configure(world, new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("planks", 6, 2, IndustrialEndpointKind.Warehouse, IndustrialEndpointKind.Warehouse,
                Source, Destination)));
        world.RunAutomation(Ship);
        world.AdvanceDay();
        var result = world.RunAutomation(Ship);
        Assert.Equal(6, result.After.PrivateFacilities[Destination].Inventory["planks"]);
        var stock = result.After.Accounting.Stores["facility:" + Destination].Goods["planks"];
        Assert.Equal(0, stock.CostBasis);
        Assert.All(stock.Lots, lot => Assert.Equal(InventoryCostOrigin.StarterGrant, lot.Origin));
        Assert.All(stock.Lots, lot => Assert.Equal("test_charter", lot.Provenance));
        Assert.Empty(result.Trades);
        Assert.Equal(0, result.After.Companies[Owner].Cash);
    }

    [Fact]
    public void IncompatibleMixedCargoPausesAndEnableTogglesPreserveStage()
    {
        var world = World();
        StockSource(world);
        Configure(world);
        world.SetAutomationEnabled(Ship, false);
        world.ExecuteTrade(Ship, "north", Buy("logs", 1));
        world.ExecuteTrade(Ship, "north", Buy("planks", 1));
        world.SetAutomationEnabled(Ship, true);
        var before = world.Snapshot();
        Assert.Equal(IndustrialRouteAction.Pause, Evaluate(world).Action);
        Assert.Contains("incompatible", Evaluate(world).Reason);
        Assert.Same(before, world.RunAutomation(Ship).After);
        world.SetAutomationEnabled(Ship, false);
        Assert.Equal(IndustrialRouteStage.AcquireOutbound, world.Snapshot().Ships[Ship].IndustrialStage);
        Assert.Equal(2, world.Snapshot().Ships[Ship].CargoUnits);
    }

    [Fact]
    public void ReapplyingSamePolicyIsPassiveAndCannotResetACarriedShipment()
    {
        var world = World();
        StockSource(world);
        var policy = WarehouseRoute();
        Configure(world, policy);
        var before = world.Snapshot();
        Assert.Same(before, world.ConfigureIndustrialAutomation(Ship, policy, true).After);
        world.RunAutomation(Ship);
        before = world.Snapshot();
        Assert.Same(before, world.ConfigureIndustrialAutomation(Ship, policy, true).After);
        var paused = world.ConfigureIndustrialAutomation(Ship, policy, false).After;
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, paused.Ships[Ship].IndustrialStage);
        Assert.Equal(6, paused.Ships[Ship].Cargo["planks"]);
        Assert.Equal(before.Ships[Ship].DestinationPortId, paused.Ships[Ship].DestinationPortId);
        Assert.Equal(before.Day, paused.Day);
        world.ConfigureIndustrialAutomation(Ship, policy, true);
        Assert.Equal(IndustrialRouteAction.ContinueSailing, Evaluate(world).Action);
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, world.Snapshot().Ships[Ship].IndustrialStage);
    }

    [Fact]
    public void SameDayStaleDecisionRejectedAndLateDailyFailureRollsBackLoadingAndStage()
    {
        var world = World();
        StockSource(world);
        Configure(world);
        var pending = world.EvaluateAutomation(Ship);
        world.SetFacilityEnabled(Owner, Source, true);
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteAutomation(pending));
        Assert.Same(before, world.Snapshot());
        var overflow = World(overflowProduction: true);
        StockSource(overflow);
        Configure(overflow);
        before = overflow.Snapshot();
        Assert.Throws<OverflowException>(() => overflow.AdvanceDay());
        Assert.Same(before, overflow.Snapshot());
        Assert.Equal(8, before.PrivateFacilities[Source].Inventory["planks"]);
        Assert.Equal(IndustrialRouteStage.AcquireOutbound, before.Ships[Ship].IndustrialStage);
        Assert.Equal(0, before.Ships[Ship].CargoUnits);
    }

    [Fact]
    public void EquivalentManualAndAutomaticWarehouseShippingHaveIdenticalEconomicTransitions()
    {
        var manual = World();
        var automatic = World();
        StockSource(manual);
        StockSource(automatic);
        Configure(automatic);
        manual.TransferGoods(Owner, Ship, Source, TransferDirection.WarehouseToShip, "planks", 6);
        manual.Depart(Ship, "south");
        automatic.RunAutomation(Ship);
        Assert.Equal(Economy(manual.Snapshot()), Economy(automatic.Snapshot()));
        manual.AdvanceDay();
        automatic.AdvanceDay();
        Assert.Equal(Economy(manual.Snapshot()), Economy(automatic.Snapshot()));
        manual.ExecuteTrade(Ship, "south", new TradeCommand(TradeDirection.Sell, "planks", 6));
        manual.Depart(Ship, "north");
        automatic.RunAutomation(Ship);
        Assert.Equal(Economy(manual.Snapshot()), Economy(automatic.Snapshot()));
        manual.AdvanceDay();
        automatic.AdvanceDay();
        automatic.RunAutomation(Ship, 1);
        Assert.Equal(Economy(manual.Snapshot()), Economy(automatic.Snapshot()));
        Assert.Equal(IndustrialRouteStage.AcquireOutbound, automatic.Snapshot().Ships[Ship].IndustrialStage);
    }

    [Fact]
    public void InvalidPolicyBoundsAreRejectedAndMinimumAboveCapacityPauses()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndustrialRouteLeg("logs", 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndustrialRouteLeg("logs", 2, 3));
        Assert.Throws<ArgumentException>(() => new IndustrialRouteLeg("logs", 2, 1, warehouseReserve: 1));
        Assert.Throws<ArgumentException>(() => new IndustrialRouteLeg("logs", 2, 1, sourceFacilityId: Source));
        Assert.Throws<ArgumentNullException>(() => new IndustrialRouteLeg("logs", 2, 1, IndustrialEndpointKind.Warehouse));
        Assert.Throws<ArgumentException>(() => new IndustrialRoutePolicy("north", "north", new IndustrialRouteLeg("logs", 2, 1)));
        Assert.Throws<ArgumentException>(() => new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("logs", 2, 1), allowEmptyReturn: false));
        var world = World(capacity: 1);
        StockSource(world);
        Configure(world);
        Assert.Equal(IndustrialRouteAction.Pause, Evaluate(world).Action);
        Assert.Contains("minimum shipment exceeds", Evaluate(world).Reason);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RunAutomation(Ship, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.RunAutomation(Ship, 5));
    }

    [Fact]
    public void BakerySupplyChainRepeatsWithProcurementAndUsefulPrivateInputReturn()
    {
        var content = Content();
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id,
            content.Goods.Select(g => new MarketQuote(g.Id, id == "south" ? 25 : 2, id == "south" ? 26 : 3)),
            10000, Array.Empty<BuildingInstance>(), content.Goods.ToDictionary(g => g.Id, g => 100)),
            id, 0, 0, Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
        var world = new RegionalWorldState(content, new[] { Port("north"), Port("south") },
            new[] { new SeaRoute("north", "south", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration(Ship, 8, "north"), Owner,
                "Player", 0, 100, isPlayer: true) }, companies: new[] { new CompanyConfiguration(Owner, 2000) },
            industryBlueprints: new[] { new IndustryBlueprint("bakery_plan", "bakery", "bake_bread",
                300, 2, 30, new[] { "north" }) });
        world.ConstructFacility(Owner, "north", "bakery_plan", Source);
        world.ExecuteWarehouseTrade(Owner, Source, Buy("flour", 4));
        world.ConfigureFacility(Owner, Source, new IndustryPolicy(true, false, 100,
            new[] { new InputStockRule("logs", 4, 3) }, Array.Empty<OutputSaleRule>()));
        Configure(world, new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("bread", 6, 6, IndustrialEndpointKind.Warehouse, sourceFacilityId: Source,
                minimumSourceStock: 8, warehouseReserve: 2, minimumSellPrice: 25),
            new IndustrialRouteLeg("flour", 4, 4, destinationKind: IndustrialEndpointKind.Warehouse,
                destinationFacilityId: Source, maximumBuyPrice: 26), cashReserve: 100, allowEmptyReturn: false));
        var results = Enumerable.Range(0, 35).Select(_ => world.AdvanceDay()).ToArray();
        Assert.True(results.SelectMany(r => r.Trades).Count(t => t.Trade.Direction == TradeDirection.Sell &&
            t.Trade.GoodId == "bread") >= 3);
        Assert.True(results.SelectMany(r => r.Transfers).Count(t => t.Direction == TransferDirection.WarehouseToShip) >= 3);
        Assert.True(results.SelectMany(r => r.Transfers).Count(t => t.Direction == TransferDirection.ShipToWarehouse &&
            t.GoodId == "flour") >= 3);
        Assert.True(world.Snapshot().PrivateFacilities[Source].BatchesProduced >= 12);
        Assert.All(results, r => Assert.InRange(r.Decisions.Count, 1, RegionalWorldState.MaximumDocksideActions));
        Assert.All(results.SelectMany(r => r.Transfers), t => Assert.Equal(TransferOutcome.Transferred, t.Outcome));
        Assert.All(results.SelectMany(r => r.Trades), t => Assert.Equal(TradeOutcome.Traded, t.Trade.Outcome));
        Assert.Equal(22000, world.Snapshot().Companies.Values.Sum(c => c.Cash) +
            world.Snapshot().Ports.Values.Sum(p => p.MarketCash));
    }
}
