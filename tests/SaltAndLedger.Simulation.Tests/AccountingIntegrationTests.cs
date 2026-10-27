using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class AccountingIntegrationTests
{
    private const string Owner = "company";
    private const string Ship = "ship";
    private const string Factory = "private_bakery";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static TradeCommand Buy(string good, int quantity) => new(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(string good, int quantity) => new(TradeDirection.Sell, good, quantity);
    private static RegionalPortConfiguration Port(ContentCatalog content, string id, long cash,
        MarketQuote? logQuote = null) =>
        new(new PortConfiguration(id, content.Goods.Select(g => g.Id == "logs" && logQuote != null ? logQuote :
                new MarketQuote(g.Id, g.Id == "bread" ? 20 : g.Id == "flour" ? 7 : 2,
                    g.Id == "bread" ? 21 : g.Id == "flour" ? 8 : 3)),
            cash, Array.Empty<BuildingInstance>(), new Dictionary<string, int>
            { ["logs"] = 20, ["flour"] = 10, ["bread"] = 10 }),
            id, 0, 0, Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
    private static RegionalWorldState World(long companyCash = 600, long marketCash = 1000,
        MarketQuote? logQuote = null, long day = 0, IEnumerable<RegionalShipConfiguration>? ships = null,
        ContentCatalog? content = null, IEnumerable<PrivateFacilityConfiguration>? initialFacilities = null)
    {
        content ??= Content();
        return new RegionalWorldState(content, new[] { Port(content, "north", marketCash, logQuote),
                Port(content, "south", marketCash, logQuote) },
            new[] { new SeaRoute("north", "south", 2) },
            ships ?? new[] { new RegionalShipConfiguration(new ShipConfiguration(Ship, 10, "north"),
                Owner, "Ship", 0, 1, isPlayer: true) }, initialDay: day,
            companies: new[] { new CompanyConfiguration(Owner, companyCash) },
            industryBlueprints: new[] { new IndustryBlueprint("bakery_plan", "bakery", "bake_bread",
                100, 2, 20, new[] { "north", "south" }) }, initialFacilities: initialFacilities);
    }
    private static void Build(RegionalWorldState world) =>
        Assert.Equal(ConstructionOutcome.Built, world.ConstructFacility(Owner, "north", "bakery_plan", Factory).Construction.Single().Outcome);

    private static void Reconcile(RegionalWorldSnapshot state)
    {
        foreach (var company in state.Companies.Values)
        {
            var finance = state.Accounting.Companies[company.Id];
            Assert.Equal((BigInteger)company.Cash,
                (BigInteger)finance.InitialCash + finance.SalesRevenue - finance.InventoryPurchases - finance.ConstructionExpenditure - finance.WagesPaid - finance.FeesPaid + finance.MintReceipts - finance.PublicCommissionExpenditure);
            Assert.Equal(finance.InventoryPurchases, checked(finance.CostOfGoodsSold + finance.HeldInventoryCost + finance.MintMaterialCost));
            var stores = state.Accounting.Stores.Values.Where(s => s.OwnerId == company.Id).ToArray();
            Assert.Equal(finance.HeldInventoryCost, stores.Sum(s => s.TotalCostBasis));
            foreach (var good in state.Ports.First().Value.Inventory.Keys)
            {
                long Amount(IReadOnlyDictionary<string, long> values) => values.TryGetValue(good, out var quantity) ? quantity : 0;
                var physical = state.Ships.Values.Where(s => s.OwnerId == company.Id).Sum(s => (long)s.Cargo[good]) +
                    state.PrivateFacilities.Values.Where(f => f.OwnerId == company.Id).Sum(f => (long)f.Inventory[good]) +
                    state.CompanyStockpiles.Values.Where(f => f.OwnerId == company.Id).Sum(f => (long)f.Inventory[good]);
                Assert.Equal(physical, Amount(finance.GrantedQuantities) + Amount(finance.PurchasedQuantities) +
                    Amount(finance.ProducedQuantities) - Amount(finance.ConsumedQuantities) - Amount(finance.SoldQuantities) - Amount(finance.SurrenderedQuantities));
                Assert.Equal(physical, stores.Sum(s => (long)s.GetQuantity(good)));
                Assert.Equal(physical, Amount(finance.HeldInventoryQuantities));
            }
        }
        foreach (var ship in state.Ships.Values)
            foreach (var good in ship.Cargo)
                Assert.Equal(good.Value, state.Accounting.Stores["ship:" + ship.Id].GetQuantity(good.Key));
        foreach (var facility in state.PrivateFacilities.Values.Where(f => f.SharedStockpileId == null))
            foreach (var good in facility.Inventory)
                Assert.Equal(good.Value, state.Accounting.Stores["facility:" + facility.Id].GetQuantity(good.Key));
    }

    [Fact]
    public void RealWorldPurchaseManufactureTransferVoyageAndSaleReconcileEveryCommit()
    {
        var world = World();
        var results = new List<RegionalResult>();
        void Apply(RegionalResult result) { results.Add(result); Reconcile(result.After); }
        Apply(world.ConstructFacility(Owner, "north", "bakery_plan", Factory));
        Apply(world.ExecuteTrade(Ship, "north", Buy("logs", 2)));
        Apply(world.TransferGoods(Owner, Ship, Factory, TransferDirection.ShipToWarehouse, "logs", 1));
        Apply(world.ExecuteWarehouseTrade(Owner, Factory, Buy("flour", 1)));
        Apply(world.AdvanceDay());
        Apply(world.AdvanceDay());
        Apply(world.TransferGoods(Owner, Ship, Factory, TransferDirection.WarehouseToShip, "bread", 1));
        Apply(world.Depart(Ship, "south"));
        Apply(world.AdvanceDay());
        Apply(world.AdvanceDay());
        Apply(world.ExecuteTrade(Ship, "south", Sell("bread", 1)));
        var final = world.Snapshot();
        var finance = final.Accounting.Companies[Owner];
        Assert.Equal((506L, 14L, 20L, 5L, 9L, 15L, 100L), (final.Companies[Owner].Cash,
            finance.InventoryPurchases, finance.SalesRevenue, finance.CostOfGoodsSold,
            finance.HeldInventoryCost, finance.RealizedGrossMargin, finance.ConstructionExpenditure));
        var journal = results.SelectMany(r => r.EconomicEvents).ToArray();
        Assert.Equal(finance.InventoryPurchases, journal.Where(e => e.Kind == EconomicEventKind.Purchase).Sum(e => e.CashTotal));
        Assert.Equal(finance.SalesRevenue, journal.Where(e => e.Kind == EconomicEventKind.Sale).Sum(e => e.CashTotal));
        Assert.Equal(finance.CostOfGoodsSold, journal.Where(e => e.Kind == EconomicEventKind.Sale).Sum(e => e.AllocatedCost));
        Assert.Equal(finance.ConstructionExpenditure, journal.Where(e => e.Kind == EconomicEventKind.Construction).Sum(e => e.CashTotal));
        foreach (var result in results)
            Assert.All(result.EconomicEvents, e => Assert.Equal((result.After.Day, result.After.Revision), (e.Day, e.Revision)));
        Assert.All(journal.Where(e => e.Kind == EconomicEventKind.Transfer), e => Assert.Equal(0, e.CompanyCashDelta));
    }

    [Fact]
    public void DynamicBulkPricesAreRecordedExactlyRatherThanCurrentQuotesOrMarginalProducts()
    {
        var world = World(logQuote: new MarketQuote("logs",
            new InventoryPricing(InventoryPricingModel.Linear, 10, 20, 10, 1, 40, 1)));
        var first = world.ExecuteTrade(Ship, "north", Buy("logs", 3));
        var second = world.ExecuteTrade(Ship, "north", Buy("logs", 2));
        var firstTotal = first.Trades.Single().Trade.ExecutedAmount;
        var secondTotal = second.Trades.Single().Trade.ExecutedAmount;
        Assert.NotEqual(first.Trades.Single().Trade.UnitPrice * 3, firstTotal);
        Assert.Equal(firstTotal + secondTotal, world.Snapshot().Accounting.Companies[Owner].HeldInventoryCost);
        var sale = world.ExecuteTrade(Ship, "north", Sell("logs", 2));
        Assert.Equal(firstTotal * 2 / 3, sale.EconomicEvents.Single().AllocatedCost);
        Reconcile(world.Snapshot());
    }

    [Fact]
    public void RejectedAndStaleOperationsDoNotEmitEventsOrChangeAccounting()
    {
        var world = World();
        Build(world);
        var before = world.Snapshot();
        var failed = world.ExecuteTrade(Ship, "north", Buy("logs", 11));
        Assert.Same(before, failed.After);
        Assert.Empty(failed.EconomicEvents);
        failed = world.TransferGoods(Owner, Ship, Factory, TransferDirection.ShipToWarehouse, "logs", 1);
        Assert.Same(before, failed.After);
        Assert.Empty(failed.EconomicEvents);
        world.ExecuteTrade(Ship, "north", Buy("logs", 1));
        var current = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade(Ship, "north", Sell("logs", 1), before));
        Assert.Same(current, world.Snapshot());
        failed = world.TransferGoods(Owner, Ship, Factory, TransferDirection.ShipToWarehouse, "logs", 1 + 10);
        Assert.Same(current, failed.After);
        Assert.Empty(failed.EconomicEvents);
    }

    [Fact]
    public void LateProductionDeadlineOverflowRevertsPurchasesLotsProductionAndAccounts()
    {
        var world = World(day: long.MaxValue - 2);
        Build(world);
        IndustryPolicy Policy(int target) => new(true, false, inputs: new[]
        { new InputStockRule("flour", target, 8), new InputStockRule("logs", target, 3) });
        world.ConfigureFacility(Owner, Factory, Policy(1));
        world.AdvanceDay();
        world.ConfigureFacility(Owner, Factory, Policy(2));
        var before = world.Snapshot();
        var previous = JsonConvert.SerializeObject(before.Accounting);
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(previous, JsonConvert.SerializeObject(world.Snapshot().Accounting));
        Assert.Equal(0, before.PrivateFacilities[Factory].BatchesProduced);
        Assert.Equal(1, before.PrivateFacilities[Factory].Inventory["logs"]);
        Assert.Equal(1, before.PrivateFacilities[Factory].Inventory["flour"]);
        Assert.Equal(11, before.Accounting.Companies[Owner].InventoryPurchases);
        Reconcile(before);
    }

    [Fact]
    public void AccountingAggregateOverflowRollsBackOtherwiseValidShipTrade()
    {
        var price = long.MaxValue / 2;
        var world = World(companyCash: long.MaxValue, marketCash: 0, logQuote: new MarketQuote("logs", price, price));
        for (var index = 0; index < 2; index++)
        {
            world.ExecuteTrade(Ship, "north", Buy("logs", 1));
            world.ExecuteTrade(Ship, "north", Sell("logs", 1));
        }
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.ExecuteTrade(Ship, "north", Buy("logs", 1)));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(long.MaxValue - 1, before.Accounting.Companies[Owner].InventoryPurchases);
        Assert.Equal(0, before.Accounting.Stores["ship:" + Ship].TotalCostBasis);
        Assert.Equal(0, before.Ships[Ship].CargoUnits);
        Reconcile(before);
    }

    [Fact]
    public void InitialGrantLotOrderIsIndependentOfShipContentAndDictionaryOrder()
    {
        static RegionalShipConfiguration ShipWithCargo(string id, bool reverse) => new(
            new ShipConfiguration(id, 10, "north", (reverse ? new[] { ("logs", 2), ("flour", 1) } :
                new[] { ("flour", 1), ("logs", 2) }).ToDictionary(p => p.Item1, p => p.Item2)),
            Owner, id, 0, 1);
        var content = Content();
        var reverseContent = new ContentCatalog(content.Goods.Reverse(), content.Buildings.Reverse(), content.Recipes.Reverse());
        var normal = World(ships: new[] { ShipWithCargo("alpha", false), ShipWithCargo("beta", false) }, content: content);
        var reversed = World(ships: new[] { ShipWithCargo("beta", true), ShipWithCargo("alpha", true) }, content: reverseContent);
        Assert.Equal(JsonConvert.SerializeObject(normal.Snapshot().Accounting), JsonConvert.SerializeObject(reversed.Snapshot().Accounting));
        Reconcile(normal.Snapshot());
        Reconcile(reversed.Snapshot());
    }

    [Fact]
    public void StarterFacilityAndInputsRetainCharterProvenanceWithoutConstructionOrPurchases()
    {
        var world = World(initialFacilities: new[]
        {
            new PrivateFacilityConfiguration(Factory, Owner, "north", "bakery_plan",
                new Dictionary<string, int> { ["logs"] = 1, ["flour"] = 1 }, provenance: "industrial_charter")
        });
        var initial = world.Snapshot();
        Assert.True(initial.PrivateFacilities[Factory].IsStarterGrant);
        Assert.Equal("industrial_charter", initial.PrivateFacilities[Factory].Provenance);
        Assert.Equal((600L, 0L, 0L), (initial.Companies[Owner].Cash,
            initial.Accounting.Companies[Owner].ConstructionExpenditure,
            initial.Accounting.Companies[Owner].InventoryPurchases));
        Assert.All(initial.Accounting.Stores["facility:" + Factory].Goods.Values.SelectMany(g => g.Lots),
            lot => Assert.Equal((InventoryCostOrigin.StarterGrant, "industrial_charter", 0L),
                (lot.Origin, lot.Provenance, lot.CostBasis)));
        world.AdvanceDay();
        var production = world.AdvanceDay();
        Assert.Equal(2, production.After.PrivateFacilities[Factory].Inventory["bread"]);
        Assert.Equal(0, production.After.Accounting.Stores["facility:" + Factory].GetCostBasis("bread"));
        Assert.All(production.EconomicEvents.Single(e => e.Kind == EconomicEventKind.Production)
            .Consumed.SelectMany(g => g.Lots), lot => Assert.Equal("industrial_charter", lot.Provenance));
        Assert.True(production.After.PrivateFacilities[Factory].IsStarterGrant);
        Assert.Equal("industrial_charter", production.After.PrivateFacilities[Factory].Provenance);
        Reconcile(initial);
        Reconcile(production.After);
    }

    [Fact]
    public void NinetyDaysOfEnterpriseAutomationReconcileAllCompaniesAndEveryHeldGood()
    {
        var world = RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter, seed: 99));
        world.ConstructFacility(WorldIds.PlayerOwnerId, WorldIds.West,
            WorldIds.BakeryBlueprint, Factory);
        world.ConfigureFacility(WorldIds.PlayerOwnerId, Factory, ExpandingEconomyScenario.BakeryPolicy(true));
        var startingCash = world.Snapshot().TotalCash;
        for (var day = 0; day < 90; day++)
        {
            var result = world.AdvanceDay();
            Reconcile(result.After);
            var cash = result.After.TotalCash;
            Assert.Equal(startingCash, cash);
            Assert.All(result.EconomicEvents, e => Assert.Equal((result.After.Day, result.After.Revision), (e.Day, e.Revision)));
        }
        Assert.True(world.Snapshot().Accounting.Companies[WorldIds.PlayerOwnerId].ProducedQuantities["bread"] > 0);
        Assert.True(world.Snapshot().Accounting.Companies[WorldIds.PlayerOwnerId].SalesRevenue > 0);
    }
}
