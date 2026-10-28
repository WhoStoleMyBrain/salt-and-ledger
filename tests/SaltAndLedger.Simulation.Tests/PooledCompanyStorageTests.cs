using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class PooledCompanyStorageTests
{
    private const string Owner = "owner", Rival = "rival", Ship = "ship", North = "north", South = "south";
    private const int Unit = EconomicUnits.QuantityScale;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static Dictionary<string, int> Stock(params (string Id, int Quantity)[] values) => values.ToDictionary(v => v.Id, v => v.Quantity);
    private static IndustryBlueprint Plan(string id, string building, string recipe, string port, int capacity = 8 * Unit,
        int? ceiling = 4 * Unit, long wage = Unit) => new(id, building, recipe, 10 * Unit, 1, capacity, new[] { port }, ceiling, wage);
    private static IndustryBlueprint[] Plans() => new[]
    {
        Plan("farm_plan", "farm", "harvest_grain", North, ceiling: 8 * Unit),
        Plan("mill_plan", "mill", "mill_flour", South),
        Plan("bakery_plan", "bakery", "bake_bread", South),
        Plan("sawmill_plan", "sawmill", "saw_planks", South)
    };

    private static RegionalWorldState World(IEnumerable<PrivateFacilityConfiguration>? sites = null,
        IEnumerable<IndustryBlueprint>? plans = null, long cash = 1000 * Unit, string dock = South,
        long revision = 0, RunDifficulty difficulty = RunDifficulty.Normal)
    {
        var content = Content();
        RegionalPortConfiguration Town(string id) => new(new PortConfiguration(id,
            content.Goods.Select(g => new MarketQuote(g.Id, 2, 3)), 1000 * Unit,
            Array.Empty<BuildingInstance>(), Stock(("grain", 100 * Unit), ("logs", 100 * Unit), ("flour", 100 * Unit))),
            id, id == North ? 0 : 100, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            new[] { new ConsumptionConfiguration("food", "bread", Unit, 1000, kind: ConsumptionKind.Essential,
                populationPerUnit: 20, proportional: true) }, settlement: new SettlementConfiguration(30, 1000 * Unit,
                demographicIntervalDays: 1000, quantityScale: Unit));
        return new RegionalWorldState(content, new[] { Town(North), Town(South) },
            new[] { new SeaRoute(North, South, 100) }, new[]
            {
                new RegionalShipConfiguration(new ShipConfiguration(Ship, 10 * Unit, dock), Owner, "Player", 0, 100, isPlayer: true),
                new RegionalShipConfiguration(new ShipConfiguration("rival_ship", 10 * Unit, South), Rival, "Other company", 0, 100)
            }, initialRevision: revision, companies: new[] { new CompanyConfiguration(Owner, cash), new CompanyConfiguration(Rival, 1000 * Unit) },
            industryBlueprints: plans ?? Plans(), initialFacilities: sites,
            difficulty: difficulty, runIdentity: new RunIdentity(null, RunScenario.MerchantCharter,
                RunWorldMode.ExpandingEconomy, 42, difficulty, companyStorageRules: CompanyStorageRules.Pooled, economicRulesVersion: 0),
            economy: ExpandingEconomyConfiguration.CreateDefault(South, North));
    }

    private static PrivateFacilityConfiguration Site(string id, string plan = "mill_plan", string owner = Owner,
        string port = South, IReadOnlyDictionary<string, int>? stock = null, bool enabled = true) =>
        new(id, owner, port, plan, stock, enabled: enabled);
    private static TradeCommand Buy(string good, int quantity) => new(TradeDirection.Buy, good, quantity);
    private static TradeCommand Sell(string good, int quantity) => new(TradeDirection.Sell, good, quantity);
    private static CompanyStockpileSnapshot Store(RegionalWorldState world, string facility) => world.Snapshot().GetFacilityStorage(facility);
    private static void Build(RegionalWorldState world, string id, string plan, string port = South) =>
        Assert.Equal(ConstructionOutcome.Built, world.ConstructFacility(Owner, port, plan, id).Construction.Single().Outcome);

    [Fact]
    public void FundedFarmVoyageMillBakeryChainUsesOneLocalStoreAndPreservesMaterialBasis()
    {
        var world = World(dock: North);
        Build(world, "farm", "farm_plan", North);
        Build(world, "a_mill", "mill_plan");
        Build(world, "b_bakery", "bakery_plan");
        world.ExecuteWarehouseTrade(Owner, "a_mill", Buy("logs", 2 * Unit));
        var before = world.Snapshot();
        world.AdvanceDay();
        world.SetFacilityEnabled(Owner, "farm", false);
        Assert.Equal(4 * Unit, Store(world, "farm").Inventory["grain"]);
        world.TransferGoods(Owner, Ship, "farm", TransferDirection.WarehouseToShip, "grain", 4 * Unit);
        world.Depart(Ship, South);
        world.AdvanceDay();
        world.TransferGoods(Owner, Ship, "b_bakery", TransferDirection.ShipToWarehouse, "grain", 4 * Unit);
        var first = world.AdvanceDay();
        Assert.Equal(new[] { "a_mill", "b_bakery" }, first.PrivateProduction.Where(p => p.Outcome == ScheduledProductionOutcome.Produced).Select(p => p.FacilityId));
        world.AdvanceDay();
        var after = world.Snapshot();
        var store = after.GetFacilityStorage("b_bakery");
        Assert.Same(store, after.GetFacilityStorage("a_mill"));
        Assert.Equal((0, 0, 0, 4 * Unit), (store.Inventory["grain"], store.Inventory["flour"], store.Inventory["logs"], store.Inventory["bread"]));
        Assert.All(after.PrivateFacilities.Values, f => Assert.All(f.Inventory.Values, q => Assert.Equal(0, q)));
        Assert.Equal(2, after.PrivateFacilities["a_mill"].BatchesProduced);
        Assert.Equal(2, after.PrivateFacilities["b_bakery"].BatchesProduced);
        Assert.Equal(5 * Unit, after.Accounting.Companies[Owner].WagesPaid);
        Assert.Equal(6 * Unit, after.Accounting.Stores[store.Id].GetCostBasis("bread"));
        Assert.Equal(before.TotalCash, after.TotalCash);
        Assert.Equal(0, after.Ships[Ship].CargoUnits);
        Assert.False(after.Accounting.Stores.ContainsKey("facility:a_mill"));
        Assert.False(after.Accounting.Stores.ContainsKey("facility:b_bakery"));
        Assert.Equal(4L * Unit * 30, after.CompanyValue(Owner).Goods);
        Assert.Equal(after.Accounting.Companies[Owner].InventoryPurchases, after.Accounting.Companies[Owner].HeldInventoryCost);
    }

    [Fact]
    public void SameTownChainConsumesPurchasedInputsAndKeepsEachProductionEventAttributed()
    {
        var world = World(new[] { Site("a_mill"), Site("b_bakery", "bakery_plan") });
        world.ExecuteWarehouseTrade(Owner, "b_bakery", Buy("grain", 2 * Unit));
        world.ExecuteWarehouseTrade(Owner, "a_mill", Buy("logs", Unit));
        var initial = world.Snapshot();
        var result = world.AdvanceDay();
        var store = Store(world, "a_mill");
        Assert.Equal(2 * Unit, store.Inventory["bread"]);
        Assert.Equal(9 * Unit, result.After.Accounting.Stores[store.Id].TotalCostBasis);
        Assert.Equal(2 * Unit, result.After.Accounting.Companies[Owner].WagesPaid);
        Assert.Equal(initial.TotalCash, result.After.TotalCash);
        var production = result.EconomicEvents.Where(e => e.Kind == EconomicEventKind.Production).ToArray();
        Assert.Equal(new[] { "a_mill", "b_bakery" }, production.Select(e => e.FacilityId));
        Assert.All(production, e => Assert.Equal(store.Id, e.SourceStoreId));
        Assert.All(production, e => Assert.Equal(store.Id, e.DestinationStoreId));
        world.ExecuteWarehouseTrade(Owner, "a_mill", Sell("bread", 2 * Unit));
        var finance = world.Snapshot().Accounting.Companies[Owner];
        Assert.Equal(9 * Unit, finance.CostOfGoodsSold);
        Assert.Equal(0, finance.HeldInventoryCost);
        Assert.Equal(finance.RealizedGrossMargin - 2 * Unit, finance.OperatingSurplusAfterWages);
    }

    [Fact]
    public void ScarceInputsUseStableFacilityOrderRegardlessOfConfigurationInsertionOrder()
    {
        var sites = new[] { Site("a_mill"), Site("b_mill"), Site("rival_mill", owner: Rival) };
        var a = World(sites);
        var b = World(sites.Reverse());
        foreach (var world in new[] { a, b })
        {
            world.ExecuteWarehouseTrade(Owner, "b_mill", Buy("grain", 2 * Unit));
            var before = world.Snapshot();
            var after = world.AdvanceDay().After;
            Assert.Equal(ScheduledProductionOutcome.Produced, after.PrivateFacilities["a_mill"].LastOutcome);
            Assert.Equal(ScheduledProductionOutcome.InputShortage, after.PrivateFacilities["b_mill"].LastOutcome);
            Assert.Equal(ScheduledProductionOutcome.InputShortage, after.PrivateFacilities["rival_mill"].LastOutcome);
            Assert.Equal(Unit, after.PrivateFacilities["a_mill"].WagesPaid);
            Assert.Equal(0, after.PrivateFacilities["b_mill"].WagesPaid);
            Assert.Equal(0, after.Accounting.Companies[Rival].WagesPaid);
            Assert.Equal(before.Ports[South].City!.HouseholdCash + Unit, after.Ports[South].City!.HouseholdCash);
            Assert.Equal(before.TotalCash, after.TotalCash);
            Assert.All(Store(world, "b_mill").Inventory.Values, q => Assert.True(q >= 0));
            Assert.Equal(0, Store(world, "rival_mill").StoredUnits);
        }
        Assert.Equal(RunDiagnosticReport.StateFingerprint(a.Snapshot()), RunDiagnosticReport.StateFingerprint(b.Snapshot()));
    }

    [Fact]
    public void FailedPayrollDoesNotConsumeSharedInputsOrCreateOutput()
    {
        var world = World(new[] { Site("mill", stock: Stock(("grain", 2 * Unit))) }, cash: Unit - 1);
        var before = world.Snapshot();
        var after = world.AdvanceDay().After;
        Assert.Equal(ScheduledProductionOutcome.PayrollShortage, after.PrivateFacilities["mill"].LastOutcome);
        Assert.Equal(2 * Unit, after.GetFacilityStorage("mill").Inventory["grain"]);
        Assert.Equal(0, after.Accounting.Companies[Owner].WagesPaid);
        Assert.Equal(before.TotalCash, after.TotalCash);
    }

    [Fact]
    public void AggregateCapacityAndInstalledRecipeCeilingsAllowProductiveSecondFactory()
    {
        var plan = Plan("mill_plan", "mill", "mill_flour", South, 4 * Unit, Unit);
        var world = World(new[] { Site("a_mill"), Site("b_mill") }, new[] { plan });
        Assert.Equal(8 * Unit, Store(world, "a_mill").Capacity);
        world.ExecuteWarehouseTrade(Owner, "a_mill", Buy("grain", 8 * Unit));
        var full = world.Snapshot();
        Assert.Equal(TradeOutcome.InsufficientCargoCapacity, world.ExecuteWarehouseTrade(Owner, "b_mill", Buy("logs", 1)).WarehouseTrades.Single().Trade.Outcome);
        Assert.Same(full, world.Snapshot());
        world.AdvanceDay();
        Assert.Equal(2 * Unit, Store(world, "a_mill").Inventory["flour"]);
        Assert.All(world.Snapshot().PrivateFacilities.Values, f => Assert.Equal(1, f.BatchesProduced));
        var blocked = world.AdvanceDay();
        Assert.All(blocked.PrivateProduction, p => Assert.Equal(ScheduledProductionOutcome.OutputLimit, p.Outcome));
        Assert.Equal(2 * Unit, blocked.After.Accounting.Companies[Owner].WagesPaid);
        Assert.Equal(4 * Unit, Store(world, "a_mill").Inventory["grain"]);
    }

    [Fact]
    public void FractionalPaidWarehouseTradesAndWholeCargoTransfersHaveExactDifferentAccounting()
    {
        var world = World(new[] { Site("a_mill"), Site("b_bakery", "bakery_plan", enabled: false) });
        var initial = world.Snapshot();
        var preview = initial.PreviewWarehouseTrade(Owner, "a_mill", Buy("grain", 22500));
        var paid = world.ExecuteWarehouseTrade(Owner, "b_bakery", Buy("grain", 22500), initial);
        Assert.Equal(preview.ExecutedAmount, paid.WarehouseTrades.Single().Trade.ExecutedAmount);
        Assert.Equal(67500, initial.Companies[Owner].Cash - paid.After.Companies[Owner].Cash);
        var before = world.Snapshot();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.TransferGoods(Owner, Ship, "a_mill", TransferDirection.WarehouseToShip, "grain", 2500));
        Assert.Same(before, world.Snapshot());
        var moved = world.TransferGoods(Owner, Ship, "b_bakery", TransferDirection.WarehouseToShip, "grain", Unit, before);
        Assert.Equal(before.Day, moved.After.Day);
        Assert.Equal(before.Companies[Owner].Cash, moved.After.Companies[Owner].Cash);
        Assert.Equal(30000, moved.After.Accounting.Stores["ship:" + Ship].GetCostBasis("grain"));
        Assert.Equal(37500, moved.After.Accounting.Stores[Store(world, "a_mill").Id].GetCostBasis("grain"));
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, Ship, "a_mill", TransferDirection.ShipToWarehouse, "grain", Unit, before));
        world.TransferGoods(Owner, Ship, "a_mill", TransferDirection.ShipToWarehouse, "grain", Unit);
        var sold = world.ExecuteWarehouseTrade(Owner, "b_bakery", Sell("grain", 2500));
        Assert.Equal(TradeOutcome.Traded, sold.WarehouseTrades.Single().Trade.Outcome);
        Assert.Equal(20000, Store(world, "a_mill").Inventory["grain"]);
        Assert.Equal(initial.TotalCash, sold.After.TotalCash);
    }

    [Fact]
    public void WrongCompanyAndRemoteOrSailingShipsCannotAccessLocalPool()
    {
        var world = World(new[] { Site("mill", stock: Stock(("grain", 2 * Unit))), Site("rival_mill", owner: Rival) });
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Rival, Ship, "mill", TransferDirection.WarehouseToShip, "grain", Unit));
        Assert.Throws<InvalidOperationException>(() => world.ExecuteWarehouseTrade(Rival, "mill", Buy("grain", Unit)));
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, "rival_ship", "mill", TransferDirection.WarehouseToShip, "grain", Unit));
        Assert.Same(before, world.Snapshot());
        world.Depart(Ship, North);
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, Ship, "mill", TransferDirection.WarehouseToShip, "grain", Unit));
        Assert.Same(before, world.Snapshot());
        world.AdvanceDay();
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, Ship, "mill", TransferDirection.WarehouseToShip, "grain", Unit));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void PendingSiteAddsPaidCapacityButDoesNotProduceUntilCompletionAndFullInterval()
    {
        var world = World(new[] { Site("mill", enabled: false) }, difficulty: RunDifficulty.SlowFoundations);
        var built = world.ConstructFacility(Owner, South, "bakery_plan", "bakery");
        Assert.Equal(ConstructionOutcome.Started, built.Construction.Single().Outcome);
        Assert.Equal(16 * Unit, Store(world, "mill").Capacity);
        world.ExecuteWarehouseTrade(Owner, "bakery", Buy("flour", Unit));
        world.ExecuteWarehouseTrade(Owner, "mill", Buy("logs", Unit));
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.True(world.Snapshot().PrivateFacilities["bakery"].IsOperational);
        Assert.Equal(0, world.Snapshot().PrivateFacilities["bakery"].BatchesProduced);
        world.AdvanceDay();
        Assert.Equal(1, world.Snapshot().PrivateFacilities["bakery"].BatchesProduced);
        Assert.Equal(2 * Unit, Store(world, "mill").Inventory["bread"]);
    }

    [Fact]
    public void WarehouseTradesDuringConstructionButPendingFactoryAddsNoOutputCeilingOrProduction()
    {
        var world = World(new[] { Site("a_mill", stock: Stock(("flour", Unit)), enabled: false) },
            difficulty: RunDifficulty.SlowFoundations);
        world.ConstructFacility(Owner, South, "mill_plan", "b_pending_mill");
        world.ConfigureWarehouse(Owner, South, new WarehousePolicy(rules: new[] {
            new WarehouseCommodityRule("grain", true, 2 * Unit, 3, false, 0, 0),
            new WarehouseCommodityRule("flour", false, 0, 0, true, 0, 2) }), world.Snapshot());
        Assert.Equal(16 * Unit, Store(world, "a_mill").Capacity);
        Assert.Equal(4 * Unit, world.Snapshot().FacilityOutputStockLimit("a_mill"));
        var before = world.Snapshot();
        for (var day = 1; day <= 2; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(day == 1 ? 2 : 0, result.WarehouseTrades.Count);
            Assert.Equal(0, Store(world, "a_mill").Inventory["flour"]);
            Assert.Equal(2 * Unit, Store(world, "a_mill").Inventory["grain"]);
            Assert.Equal(0, result.After.PrivateFacilities["b_pending_mill"].BatchesProduced);
            Assert.Equal(before.Companies[Owner].Cash - 4 * Unit, result.After.Companies[Owner].Cash);
        }
        Assert.Equal(8 * Unit, world.Snapshot().FacilityOutputStockLimit("a_mill"));
        var operational = world.AdvanceDay();
        Assert.Equal(1, operational.After.PrivateFacilities["b_pending_mill"].BatchesProduced);
        Assert.Equal(Unit, operational.After.PrivateFacilities["b_pending_mill"].WagesPaid);
        Assert.Single(operational.WarehouseTrades);
        Assert.Equal(0, Store(world, "a_mill").StoredUnits);
    }

    [Fact]
    public void EndOfCommitOverflowRollsBackEarlierSharedProductionAndPayroll()
    {
        var world = World(new[] { Site("a_mill", stock: Stock(("grain", 2 * Unit))),
            Site("b_bakery", "bakery_plan", stock: Stock(("logs", Unit))) }, revision: long.MaxValue);
        var before = world.Snapshot();
        var hash = RunDiagnosticReport.StateFingerprint(before);
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(hash, RunDiagnosticReport.StateFingerprint(world.Snapshot()));
        Assert.Equal(0, world.Snapshot().Accounting.Companies[Owner].WagesPaid);
    }

    [Fact]
    public void OneWarehousePolicyBuysAndSellsOnceForTwoCompetingFactories()
    {
        var world = World(new[] { Site("a_mill"), Site("b_mill") });
        var policy = new WarehousePolicy(rules: new[] {
            new WarehouseCommodityRule("grain", true, 2 * Unit, 3, false, 0, 0),
            new WarehouseCommodityRule("flour", false, 0, 0, true, 0, 2) });
        world.ConfigureWarehouse(Owner, South, policy, world.Snapshot());
        var before = world.Snapshot();
        var result = world.AdvanceDay();
        var finance = result.After.Accounting.Companies[Owner];
        Assert.Equal(6 * Unit, finance.InventoryPurchases);
        Assert.Equal(2 * Unit, finance.SalesRevenue);
        Assert.Equal(Unit, finance.WagesPaid);
        Assert.Equal(0, Store(world, "a_mill").StoredUnits);
        Assert.Equal(1, result.EconomicEvents.Count(e => e.Kind == EconomicEventKind.Purchase));
        Assert.Equal(1, result.EconomicEvents.Count(e => e.Kind == EconomicEventKind.Sale));
        Assert.Equal(6 * Unit, Store(world, "a_mill").ProcurementCost);
        Assert.Equal(2 * Unit, Store(world, "a_mill").SalesRevenue);
        Assert.All(result.After.PrivateFacilities.Values, f => Assert.Equal(0, f.ProcurementCost + f.SalesRevenue));
        Assert.Equal(ScheduledProductionOutcome.InputShortage, result.After.PrivateFacilities["b_mill"].LastOutcome);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
    }

    [Fact]
    public void ExtractionRespectsFiniteAggregateCapacityAndPaysOnlyForSuccessfulWholeBatch()
    {
        var plan = Plan("farm_plan", "farm", "harvest_grain", North, 6 * Unit, 20 * Unit);
        var world = World(new[] { Site("farm", "farm_plan", port: North, stock: Stock(("grain", 3 * Unit))) },
            new[] { plan }, dock: North);
        var blocked = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.WarehouseCapacity, blocked.After.PrivateFacilities["farm"].LastOutcome);
        Assert.Equal(3 * Unit, Store(world, "farm").StoredUnits);
        Assert.Equal(0, blocked.After.Accounting.Companies[Owner].WagesPaid);
        world.ExecuteWarehouseTrade(Owner, "farm", Sell("grain", Unit));
        var produced = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, produced.After.PrivateFacilities["farm"].LastOutcome);
        Assert.Equal(6 * Unit, Store(world, "farm").StoredUnits);
        Assert.Equal(Unit, produced.After.Accounting.Companies[Owner].WagesPaid);
    }

    [Fact]
    public void ConstructionCapacityOverflowRollsBackPaymentAndNewSite()
    {
        var plan = Plan("mill_plan", "mill", "mill_flour", South, int.MaxValue);
        var world = World(new[] { Site("a_mill") }, new[] { plan });
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.ConstructFacility(Owner, South, "mill_plan", "b_mill"));
        Assert.Same(before, world.Snapshot());
        Assert.Single(world.Snapshot().PrivateFacilities);
    }

    [Fact]
    public void IndustrialFacilityEndpointsResolveExactlyOnePoolAndPreserveFifoAcrossVoyage()
    {
        var sites = new[] { Site("farm", "farm_plan", port: North, stock: Stock(("grain", 2 * Unit)), enabled: false),
            Site("a_mill", enabled: false), Site("b_bakery", "bakery_plan", enabled: false) };
        var world = World(sites, dock: North);
        var policy = new IndustrialRoutePolicy(North, South, new IndustrialRouteLeg("grain", 2 * Unit, Unit,
            IndustrialEndpointKind.Warehouse, IndustrialEndpointKind.Warehouse, "farm", "b_bakery"));
        world.ConfigureIndustrialAutomation(Ship, policy, true);
        var before = world.Snapshot();
        var decision = world.EvaluateAutomation(Ship);
        Assert.Equal(2 * Unit, decision.SelectedQuantity);
        world.AdvanceDay();
        Assert.Equal(South, world.Snapshot().Ships[Ship].DockedPortId);
        world.AdvanceDay();
        var after = world.Snapshot();
        Assert.Equal(2 * Unit, after.GetFacilityStorage("a_mill").Inventory["grain"]);
        Assert.Equal(0, after.GetFacilityStorage("farm").Inventory["grain"]);
        Assert.Equal(0, after.Ships[Ship].CargoUnits);
        Assert.Equal(before.TotalCash, after.TotalCash);
        Assert.Equal(0, after.Accounting.Companies[Owner].InventoryPurchases);
        Assert.Equal(2 * Unit, after.Accounting.Stores[after.GetFacilityStorage("a_mill").Id].GetQuantity("grain"));
        Assert.Single(after.Accounting.Stores[after.GetFacilityStorage("a_mill").Id].Goods["grain"].Lots);
    }
}
