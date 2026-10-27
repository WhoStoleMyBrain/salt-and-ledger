using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingRouteQuantityTests
{
    [Theory]
    [InlineData(27500, 100000, 20000)]
    [InlineData(27500, 45000, 10000)]
    [InlineData(9999, 100000, 0)]
    [InlineData(27500, 29999, 0)]
    public void MerchantAcquisitionFloorsFractionalStockAndBudgetToWholeCargo(int stock, long cash, int expected)
    {
        var snapshot = Maritime(stock, cash);
        var policy = new TradeRoutePolicy("north", "south", new RouteLeg("planks", 30000, 10000, 3, 2),
            allowEmptyReturn: true);
        var decision = RoutePolicyEvaluator.Evaluate(snapshot, policy, quantityStep: EconomicUnits.QuantityScale);
        Assert.Equal(expected, decision.SelectedQuantity);
        Assert.Equal(expected == 0 ? AutomationAction.Wait : AutomationAction.BuyAndDepart, decision.Action);
        if (expected != 0) Assert.Equal(expected * 3L, decision.Preview!.ExecutedAmount);
    }

    [Theory]
    [InlineData(15000, 0)]
    [InlineData(20000, 10000)]
    [InlineData(35000, 10000)]
    [InlineData(40000, 20000)]
    public void MerchantSaleCannotPartiallyUnloadAWholeCargoUnit(long marketCash, int expected)
    {
        var snapshot = Maritime(10000, 100000, marketCash, cargo: 20000, dock: "south");
        var policy = new TradeRoutePolicy("north", "south", new RouteLeg("planks", 30000, 10000, 3, 2),
            allowEmptyReturn: true);
        var decision = RoutePolicyEvaluator.Evaluate(snapshot, policy, quantityStep: EconomicUnits.QuantityScale);
        Assert.Equal(expected, decision.SelectedQuantity);
        Assert.Equal(expected == 0 ? AutomationAction.Wait : AutomationAction.Sell, decision.Action);
    }

    [Fact]
    public void FractionalShipRouteTargetsAreRejectedBeforeEvaluation()
    {
        var policy = new TradeRoutePolicy("north", "south", new RouteLeg("planks", 27500, 10000, 3, 2),
            allowEmptyReturn: true);
        Assert.Throws<ArgumentException>(() => RoutePolicyEvaluator.Evaluate(Maritime(30000, 100000), policy,
            quantityStep: EconomicUnits.QuantityScale));
    }

    [Fact]
    public void RegionalMerchantProjectionRetainsTheWholeCargoContract()
    {
        var snapshot = Regional(new IndustrialRoutePolicy("north", "south",
            new IndustrialRouteLeg("planks", 30000, 10000)), cash: 45000);
        var ship = snapshot.Ships["ship"];
        var policy = new TradeRoutePolicy("north", "south", new RouteLeg("planks", 30000, 10000, 3, 2),
            allowEmptyReturn: true);
        var merchant = new RegionalWorldSnapshot(snapshot.Day, snapshot.Revision, snapshot.Ports.Values,
            new[] { new RegionalShipSnapshot(ship.Configuration, ship.Ship, ship.Cash, policy, true, 0) },
            snapshot.Routes, snapshot.Companies.Values, snapshot.IndustryBlueprints.Values,
            snapshot.PrivateFacilities.Values, snapshot.Challenge, snapshot.Accounting, economy: snapshot.Economy);
        var decision = RegionalAutomation.Evaluate(merchant, "ship");
        Assert.Equal(10000, decision.SelectedQuantity);
        Assert.Equal(AutomationAction.BuyAndDepart, decision.Action);
        Assert.Equal(30000, decision.Preview!.ExecutedAmount);
    }

    [Theory]
    [InlineData(27500, 5000, 20000)]
    [InlineData(17500, 7500, 10000)]
    [InlineData(17500, 7501, 0)]
    [InlineData(9999, 0, 0)]
    public void IndustrialWarehouseAcquisitionRetainsFractionalReserve(int stock, int reserve, int expected)
    {
        var policy = new IndustrialRoutePolicy("north", "south", new IndustrialRouteLeg("planks", 30000, 10000,
            IndustrialEndpointKind.Warehouse, sourceFacilityId: "source", warehouseReserve: reserve),
            allowEmptyReturn: true);
        var snapshot = Regional(policy, sourceStock: stock);
        var decision = IndustrialRouteEvaluator.Evaluate(snapshot, "ship");
        Assert.Equal(expected, decision.SelectedQuantity);
        Assert.Equal(expected == 0 ? IndustrialRouteAction.Wait : IndustrialRouteAction.LoadAndDepart, decision.Action);
        if (expected != 0) Assert.True(stock - decision.SelectedQuantity >= reserve);
    }

    [Theory]
    [InlineData(49999, 0)]
    [InlineData(40000, 10000)]
    [InlineData(32500, 10000)]
    [InlineData(30000, 20000)]
    public void IndustrialWarehouseDeliveryRequiresSpaceForAWholeUnit(int destinationStock, int expected)
    {
        var policy = new IndustrialRoutePolicy("north", "south", new IndustrialRouteLeg("planks", 30000, 10000,
            IndustrialEndpointKind.Market, destinationKind: IndustrialEndpointKind.Warehouse,
            destinationFacilityId: "destination"), allowEmptyReturn: true);
        var snapshot = Regional(policy, cargo: 20000, destinationStock: destinationStock,
            stage: IndustrialRouteStage.DeliverOutbound);
        var decision = IndustrialRouteEvaluator.Evaluate(snapshot, "ship");
        Assert.Equal(expected, decision.SelectedQuantity);
        Assert.Equal(expected == 0 ? IndustrialRouteAction.Wait : IndustrialRouteAction.Unload, decision.Action);
    }

    [Theory]
    [InlineData(45000, 100000, IndustrialRouteStage.AcquireOutbound, 10000)]
    [InlineData(29999, 100000, IndustrialRouteStage.AcquireOutbound, 0)]
    [InlineData(100000, 15000, IndustrialRouteStage.DeliverOutbound, 0)]
    [InlineData(100000, 35000, IndustrialRouteStage.DeliverOutbound, 10000)]
    public void IndustrialMarketOrdersRespectWholeCargoAfterBudgetSearch(long cash, long marketCash,
        IndustrialRouteStage stage, int expected)
    {
        var policy = new IndustrialRoutePolicy("north", "south", new IndustrialRouteLeg("planks", 30000, 10000,
            IndustrialEndpointKind.Market), allowEmptyReturn: true);
        var snapshot = Regional(policy, cash: cash, marketCash: marketCash,
            cargo: stage == IndustrialRouteStage.DeliverOutbound ? 20000 : 0, stage: stage);
        var decision = IndustrialRouteEvaluator.Evaluate(snapshot, "ship");
        Assert.Equal(expected, decision.SelectedQuantity);
        if (expected != 0) Assert.Equal(0, decision.Trade!.Trade.Quantity % EconomicUnits.QuantityScale);
    }

    private static MaritimeWorldSnapshot Maritime(int stock, long cash, long marketCash = 100000,
        int cargo = 0, string dock = "north")
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var quotes = new[] { new MarketQuote("planks", 2, 3) };
        return new MaritimeWorldSnapshot(0, new[] { "north", "south" }.Select(id =>
            new PortSnapshot(id, new Inventory(content, new Dictionary<string, int> { ["planks"] = stock }).Snapshot(),
                marketCash, quotes.ToDictionary(q => q.GoodId), Array.Empty<BuildingInstance>())), "owner", cash,
            new ShipSnapshot("ship", 30000,
                new Inventory(content, new Dictionary<string, int> { ["planks"] = cargo }).Snapshot(),
                dock, null, null, 0), 1);
    }

    private static RegionalWorldSnapshot Regional(IndustrialRoutePolicy policy, int sourceStock = 27500,
        int destinationStock = 0, int cargo = 0, long cash = 100000, long marketCash = 100000,
        IndustrialRouteStage stage = IndustrialRouteStage.AcquireOutbound)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var ports = new[] { "north", "south" }.Select(id => new RegionalPortConfiguration(new PortConfiguration(id,
            content.Goods.Select(g => new MarketQuote(g.Id, 2, 3)), marketCash, Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["planks"] = 27500 }), id, 0, 0,
            Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>()));
        var configuration = new RegionalShipConfiguration(new ShipConfiguration("ship", 30000,
            stage == IndustrialRouteStage.DeliverOutbound ? "south" : "north",
            new Dictionary<string, int> { ["planks"] = cargo }), "owner", "Ship", 0, 100, isPlayer: true);
        var world = new RegionalWorldState(content, ports, new[] { new SeaRoute("north", "south", 100) },
            new[] { configuration }, companies: new[] { new CompanyConfiguration("owner", cash) },
            industryBlueprints: new[] { new IndustryBlueprint("plan", "sawmill", "saw_planks", 100, 2, 50000,
                new[] { "north", "south" }) }, initialFacilities: new[] {
                new PrivateFacilityConfiguration("source", "owner", "north", "plan",
                    new Dictionary<string, int> { ["planks"] = sourceStock }, enabled: false),
                new PrivateFacilityConfiguration("destination", "owner", "south", "plan",
                    new Dictionary<string, int> { ["planks"] = destinationStock }, enabled: false)
            });
        var before = world.Snapshot();
        var ship = before.Ships["ship"];
        // A unit-test proposal fixture: identical physical snapshot with explicit v3 quantity metadata.
        // No command is executed on a legacy world with altered units.
        return new RegionalWorldSnapshot(before.Day, before.Revision, before.Ports.Values,
            new[] { new RegionalShipSnapshot(configuration, ship.Ship, ship.Cash, null, true, 0, policy, stage) },
            before.Routes, before.Companies.Values, before.IndustryBlueprints.Values, before.PrivateFacilities.Values,
            before.Challenge, before.Accounting, economy: ExpandingEconomySnapshot.Initial(
                ExpandingEconomyConfiguration.CreateDefault("north", "south")));
    }
}
