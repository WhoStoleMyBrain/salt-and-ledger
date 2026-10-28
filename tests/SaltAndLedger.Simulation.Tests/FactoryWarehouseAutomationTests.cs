using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class FactoryWarehouseAutomationTests
{
    private const int Unit = EconomicUnits.QuantityScale;
    private const string Owner = "owner", Town = "town";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static Dictionary<string, int> Stock(params (string Id, int Quantity)[] items) => items.ToDictionary(x => x.Id, x => x.Quantity);
    private static PrivateFacilityConfiguration Site(string id, string blueprint = "bakery", bool enabled = true,
        IReadOnlyDictionary<string, int>? stock = null) => new(id, Owner, Town, blueprint, stock, enabled: enabled);
    private static WarehouseCommodityRule Rule(string good, int target = 0, int? reserve = null,
        long maximum = 0, long minimum = 0) => new(good, target > 0, target, maximum, reserve.HasValue, reserve ?? 0, minimum);
    private static CompanyStockpileSnapshot Store(RegionalWorldState world) => world.Snapshot().CompanyStockpiles[CompanyStockpileSnapshot.StoreId(Owner, Town)];

    private static RegionalWorldState World(IEnumerable<PrivateFacilityConfiguration>? sites = null,
        int capacity = 100 * Unit, long companyCash = 1000 * Unit, long marketCash = 1000 * Unit,
        long householdCash = 1000 * Unit, bool dynamic = false, RunDifficulty difficulty = RunDifficulty.Normal)
    {
        var content = Content();
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id,
            content.Goods.Select(g => dynamic
                ? new MarketQuote(g.Id, new InventoryPricing(InventoryPricingModel.Linear, 10, 20 * Unit, 8, 1, 40, 1))
                : new MarketQuote(g.Id, 2, 3)), marketCash, Array.Empty<BuildingInstance>(),
            Stock(("grain", 20 * Unit), ("logs", 20 * Unit), ("flour", 20 * Unit), ("silver", 20 * Unit))),
            id, id == Town ? 0 : 100, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            new[] { new ConsumptionConfiguration("food", "bread", Unit, 1000, kind: ConsumptionKind.Essential,
                populationPerUnit: 20, proportional: true) }, settlement: new SettlementConfiguration(30, householdCash,
                demographicIntervalDays: 1000, quantityScale: Unit));
        IndustryBlueprint Plan(string id, string building, string recipe) => new(id, building, recipe,
            10 * Unit, 2, capacity, new[] { Town }, wagePerBatch: Unit);
        return new RegionalWorldState(content, new[] { Port(Town), Port("other") }, new[] { new SeaRoute(Town, "other", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration("ship", 10 * Unit, Town), Owner, "Player", 0, 100, isPlayer: true) },
            companies: new[] { new CompanyConfiguration(Owner, companyCash) },
            industryBlueprints: new[] { Plan("bakery", "bakery", "bake_bread"), Plan("camp", "lumber_camp", "harvest_logs") },
            initialFacilities: sites, difficulty: difficulty,
            runIdentity: new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42,
                difficulty, companyStorageRules: CompanyStorageRules.Pooled, economicRulesVersion: 0),
            economy: ExpandingEconomyConfiguration.CreateDefault(Town, "other"));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(25, 1)]
    [InlineData(50, 3)]
    [InlineData(75, 4)]
    [InlineData(100, 6)]
    public void RateSchedulesWholeBatchesAndFullWagesMatchingPassiveSignals(int rate, int batches)
    {
        var world = World(new[] { Site("bakery", stock: Stock(("flour", 10 * Unit), ("logs", 10 * Unit))) });
        world.ConfigureProduction(Owner, "bakery", true, rate, ProductionPriority.Normal);
        var before = world.Snapshot();
        var flow = EconomicSignals.CommodityFlow(before, Content(), Town, "bread", Owner);
        Assert.Equal(12 * Unit, flow.InstalledProductionQuantity);
        Assert.Equal(batches * 2 * Unit, flow.EnabledProductionQuantity);
        Assert.Same(before, world.Snapshot());
        for (var i = 0; i < 12; i++) world.AdvanceDay();
        var facility = world.Snapshot().PrivateFacilities["bakery"];
        Assert.Equal(batches, facility.BatchesProduced);
        Assert.Equal(batches * Unit, facility.WagesPaid);
        Assert.Equal(batches * Unit, facility.ConsumedQuantities["logs"]);
        Assert.Equal(batches * 2 * Unit, Store(world).Inventory["bread"]);
        Assert.Equal(before.TotalCash, world.Snapshot().TotalCash);
        Assert.InRange(facility.ProductionCredit, 0, 99);
    }

    [Fact]
    public void AdmittedShortageRetainsOneBatchAcrossRateChangesAndPauseWithoutCatchup()
    {
        var world = World(new[] { Site("bakery") });
        world.ConfigureProduction(Owner, "bakery", true, 75, ProductionPriority.Normal);
        for (var i = 0; i < 4; i++) world.AdvanceDay();
        Assert.Equal(150, world.Snapshot().PrivateFacilities["bakery"].ProductionCredit);
        Assert.Equal(ScheduledProductionOutcome.InputShortage, world.Snapshot().PrivateFacilities["bakery"].LastOutcome);
        world.ConfigureProduction(Owner, "bakery", true, 0, ProductionPriority.High);
        for (var i = 0; i < 4; i++) world.AdvanceDay();
        Assert.Equal(150, world.Snapshot().PrivateFacilities["bakery"].ProductionCredit);
        Assert.Equal(4, world.Snapshot().PrivateFacilities["bakery"].NextProductionDay);
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(rules: new[] { Rule("flour", 5 * Unit), Rule("logs", 5 * Unit) }));
        world.ConfigureProduction(Owner, "bakery", true, 25, ProductionPriority.Low);
        Assert.Equal(9, EconomicSignals.PrivateProduction(world.Snapshot(), Content(), "bakery").NextRateAdmittedDay);
        var result = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, Assert.Single(result.PrivateProduction).Outcome);
        Assert.Equal(50, result.After.PrivateFacilities["bakery"].ProductionCredit);
        Assert.Equal(11, result.After.PrivateFacilities["bakery"].NextProductionDay);
        Assert.Equal(Unit, result.After.PrivateFacilities["bakery"].WagesPaid);
        for (var i = 0; i < 3; i++) world.AdvanceDay();
        Assert.Equal(1, world.Snapshot().PrivateFacilities["bakery"].BatchesProduced);
    }

    [Fact]
    public void ScarceLogsFollowPriorityThenOrdinalIdAndChangingRateFreesInputs()
    {
        var world = World(new[] { Site("z_camp", "camp"), Site("a_bakery"), Site("b_bakery"), Site("c_bakery") });
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(rules: new[] { Rule("flour", 12 * Unit) }));
        world.ConfigureProduction(Owner, "z_camp", true, 100, ProductionPriority.High);
        world.ConfigureProduction(Owner, "a_bakery", true, 100, ProductionPriority.Low);
        world.AdvanceDay();
        var result = world.AdvanceDay();
        Assert.Equal(new[] { "z_camp", "b_bakery", "c_bakery" }, result.PrivateProduction
            .Where(p => p.Outcome == ScheduledProductionOutcome.Produced).Select(p => p.FacilityId));
        Assert.Equal(ScheduledProductionOutcome.InputShortage, result.After.PrivateFacilities["a_bakery"].LastOutcome);
        world.ConfigureProduction(Owner, "b_bakery", true, 50, ProductionPriority.Normal);
        world.ConfigureProduction(Owner, "a_bakery", true, 100, ProductionPriority.Normal);
        world.AdvanceDay();
        result = world.AdvanceDay();
        Assert.Equal(new[] { "z_camp", "a_bakery", "c_bakery" }, result.PrivateProduction
            .Where(p => p.Outcome == ScheduledProductionOutcome.Produced).Select(p => p.FacilityId));
        Assert.Equal(ScheduledProductionOutcome.RateLimited, result.After.PrivateFacilities["b_bakery"].LastOutcome);
        Assert.Equal(6 * Unit, result.After.Accounting.Companies[Owner].WagesPaid);
    }

    [Fact]
    public void WarehouseTradesLogisticsGoodsWhileFactoriesAreDisabledAndNeverRoundTripsTargetStock()
    {
        var world = World(new[] { Site("a", enabled: false), Site("b", enabled: false) });
        var policy = new WarehousePolicy(rules: new[] { Rule("silver", 2 * Unit, 2 * Unit) });
        world.ConfigureWarehouse(Owner, Town, policy);
        var before = world.Snapshot();
        var preview = before.PreviewWarehouseAutomation(Owner, Town, TradeDirection.Buy, "silver");
        Assert.Equal(2 * Unit, preview.Quantity);
        var result = world.AdvanceDay();
        var trade = Assert.Single(result.WarehouseTrades);
        Assert.Equal(preview.TotalAmount, trade.Trade.ExecutedAmount);
        Assert.Equal(Store(world).Id, trade.StoreId);
        Assert.Equal("", trade.FacilityId);
        Assert.Equal(6 * Unit, Store(world).ProcurementCost);
        Assert.Equal(0, Store(world).SalesRevenue);
        Assert.Equal(IndustryWaitReason.ReserveRetained, Store(world).SalesWaitReason);
        Assert.All(result.After.PrivateFacilities.Values, f => Assert.Equal(0, f.ProcurementCost));
        Assert.Empty(world.AdvanceDay().WarehouseTrades);
        world.TransferGoods(Owner, "ship", "a", TransferDirection.WarehouseToShip, "silver", Unit);
        Assert.Equal(3 * Unit, world.Snapshot().Accounting.Stores["ship:ship"].GetCostBasis("silver"));
        world.TransferGoods(Owner, "ship", "b", TransferDirection.ShipToWarehouse, "silver", Unit);
        Assert.Equal(6 * Unit, world.Snapshot().Accounting.Stores[Store(world).Id].GetCostBasis("silver"));
        Assert.Equal(before.TotalCash, world.Snapshot().TotalCash);
    }

    [Fact]
    public void PaidPendingWarehouseCanStockBeforeFactoryBecomesOperational()
    {
        var world = World(difficulty: RunDifficulty.SlowFoundations);
        world.ConstructFacility(Owner, Town, "bakery", "pending");
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(rules: new[] { Rule("flour", Unit), Rule("logs", Unit) }));
        var result = world.AdvanceDay();
        Assert.Equal(2, result.WarehouseTrades.Count);
        Assert.False(result.After.PrivateFacilities["pending"].IsOperational);
        Assert.Empty(result.PrivateProduction);
        Assert.Equal(0, result.After.PrivateFacilities["pending"].WagesPaid);
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal(0, world.Snapshot().PrivateFacilities["pending"].BatchesProduced);
        Assert.Equal(ScheduledProductionOutcome.Produced, Assert.Single(world.AdvanceDay().PrivateProduction).Outcome);
    }

    [Fact]
    public void DraftPreviewSharesMarginalLimitsAndExactExecutionWithoutMutatingPolicy()
    {
        var world = World(new[] { Site("bakery", enabled: false) }, dynamic: true);
        var draft = new WarehousePolicy(rules: new[] { Rule("silver", 20 * Unit, maximum: 12) });
        var before = world.Snapshot();
        var preview = before.PreviewWarehouseAutomation(Owner, Town, TradeDirection.Buy, "silver", draft);
        Assert.InRange(preview.Quantity, 1, 20 * Unit - 1);
        Assert.Equal(IndustryWaitReason.BuyPriceLimit, preview.Reason);
        Assert.True(preview.MarginalPrice <= 12);
        Assert.Equal(TradeOutcome.Traded, preview.Trade!.Outcome);
        Assert.Empty(Store(world).Policy.Rules);
        Assert.Same(before, world.Snapshot());
        world.ConfigureWarehouse(Owner, Town, draft, before);
        var result = world.AdvanceDay();
        var trade = Assert.Single(result.WarehouseTrades).Trade;
        Assert.Equal(preview.Quantity, trade.ExecutedQuantity);
        Assert.Equal(preview.TotalAmount, trade.ExecutedAmount);
        Assert.Equal(IndustryWaitReason.BuyPriceLimit, Store(world).ProcurementWaitReason);
    }

    [Theory]
    [InlineData(10000, 1000000, 7000, 1000, IndustryWaitReason.CashReserve)]
    [InlineData(1000000, 1000000, 0, 10000, IndustryWaitReason.WarehouseFull)]
    public void WarehousePurchasesRespectCashReserveAndCapacity(long cash, long marketCash, long reserve,
        int expected, IndustryWaitReason reason)
    {
        var world = World(new[] { Site("bakery", enabled: false) }, capacity: Unit, companyCash: cash, marketCash: marketCash);
        // Multiple independent goods can target more than combined capacity. Ordinal order wins deterministically.
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(reserve, new[] { Rule("flour", Unit), Rule("silver", Unit) }));
        var result = world.AdvanceDay();
        Assert.Equal(expected, Store(world).Inventory["flour"]);
        Assert.Equal(0, Store(world).Inventory["silver"]);
        Assert.Equal(reason, Store(world).ProcurementWaitReason);
        Assert.True(result.After.Companies[Owner].Cash >= reserve);
    }

    [Fact]
    public void SaleReserveFiniteLiquidityAndPriceMinimumShareTheSameProposal()
    {
        var world = World(new[] { Site("bakery", enabled: false, stock: Stock(("silver", 3 * Unit))) }, marketCash: 3 * Unit);
        var tooDear = new WarehousePolicy(rules: new[] { Rule("silver", reserve: Unit, minimum: 3) });
        Assert.Equal(IndustryWaitReason.SellPriceLimit,
            world.Snapshot().PreviewWarehouseAutomation(Owner, Town, TradeDirection.Sell, "silver", tooDear).Reason);
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(rules: new[] { Rule("silver", reserve: Unit, minimum: 2) }));
        var preview = world.Snapshot().PreviewWarehouseAutomation(Owner, Town, TradeDirection.Sell, "silver");
        Assert.Equal(15000, preview.Quantity);
        Assert.Equal(IndustryWaitReason.InsufficientMarketFunds, preview.Reason);
        var result = world.AdvanceDay();
        Assert.Equal(preview.TotalAmount, Assert.Single(result.WarehouseTrades).Trade.ExecutedAmount);
        Assert.Equal(15000, Store(world).Inventory["silver"]);
        Assert.Equal(0, result.After.Ports[Town].MarketCash);
        Assert.Equal(3 * Unit, Store(world).SalesRevenue);
    }

    [Fact]
    public void SettingsAreAtomicSnapshotBoundAndRejectCircularOrUnknownWarehouseRules()
    {
        var world = World(new[] { Site("bakery") });
        var before = world.Snapshot();
        world.ConfigureProduction(Owner, "bakery", true, 100, ProductionPriority.Normal, before);
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(), before);
        Assert.Same(before, world.Snapshot());
        Assert.Throws<ArgumentException>(() => new WarehouseCommodityRule("logs", true, 2 * Unit, 0, true, Unit, 0));
        Assert.Throws<ArgumentException>(() => world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(rules: new[] { Rule("unknown", Unit) })));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.ConfigureProduction(Owner, "bakery", true, 101, ProductionPriority.Normal));
        Assert.Throws<InvalidOperationException>(() => world.ConfigureFacility(Owner, "bakery", new IndustryPolicy()));
        Assert.Throws<InvalidOperationException>(() => world.ConfigureProduction("rival", "bakery", true, 50, ProductionPriority.Low));
        Assert.Same(before, world.Snapshot());
        world.ConfigureProduction(Owner, "bakery", true, 50, ProductionPriority.Normal, before);
        var current = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConfigureProduction(Owner, "bakery", false, 50, ProductionPriority.Normal, before));
        Assert.Throws<InvalidOperationException>(() => world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(), before));
        Assert.Same(current, world.Snapshot());
    }

    [Fact]
    public void LatePayrollOverflowRollsBackProcurementRateAdmissionCashAndFifoTogether()
    {
        var world = World(new[] { Site("bakery") }, householdCash: long.MaxValue);
        world.AdvanceDay();
        world.ConfigureWarehouse(Owner, Town, new WarehousePolicy(rules: new[] { Rule("flour", Unit), Rule("logs", Unit) }));
        var before = world.Snapshot();
        var fingerprint = JsonConvert.SerializeObject(before);
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(fingerprint, JsonConvert.SerializeObject(world.Snapshot()));
        Assert.Equal(0, Store(world).ProcurementCost);
        Assert.Equal(0, before.PrivateFacilities["bakery"].ProductionCredit);
    }
}
