using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingHostileDevelopmentTests
{
    private const string Owner = "player_company", Ship = "ledger";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static ExpandingEconomyGeneration Generation() => ExpandingEconomyGeneration.Generate(42);

    private static RegionalWorldState World(long cash = 30000000, IReadOnlyDictionary<string, int>? cargo = null,
        bool startAtSilver = false, Func<RegionalPortConfiguration, RegionalPortConfiguration>? changePort = null,
        bool second = false, bool npc = false, long initialDay = 0)
    {
        var generation = Generation();
        var ships = new List<RegionalShipConfiguration>
        {
            new(new ShipConfiguration(Ship, 1000000, startAtSilver ? generation.SilverPortId : generation.MintPortId, cargo),
                Owner, "Test vessel", 0, 100, isPlayer: true)
        };
        if (second) ships.Add(new RegionalShipConfiguration(new ShipConfiguration("second", 100000, generation.MintPortId),
            Owner, "Second", 0, 100, priority: 1, isPlayer: true));
        if (npc) ships.Add(new RegionalShipConfiguration(new ShipConfiguration("npc", 100000, generation.MintPortId,
            new Dictionary<string, int> { ["planks"] = 10000, ["silver"] = 10000 }), "npc_owner", "NPC", 0, 100));
        var companies = new List<CompanyConfiguration> { new(Owner, cash) };
        if (npc) companies.Add(new CompanyConfiguration("npc_owner", 1000000));
        return new RegionalWorldState(Content(), generation.Ports.Select(p => changePort?.Invoke(p) ?? p),
            generation.Geography.Routes, ships, initialDay: initialDay, companies: companies,
            industryBlueprints: generation.Blueprints,
            economy: ExpandingEconomyConfiguration.CreateDefault(generation.MintPortId, generation.SilverPortId));
    }

    private static RegionalPortConfiguration CopyPort(RegionalPortConfiguration p,
        Dictionary<string, int>? stock = null, long? cash = null, long? householdCash = null,
        ScheduledFacilityConfiguration[]? facilities = null)
    {
        var schedules = facilities ?? p.Facilities.ToArray();
        var c = p.Settlement!;
        return new RegionalPortConfiguration(new PortConfiguration(p.Id, p.Market.Quotes.Values,
            cash ?? p.Market.InitialMarketCash, schedules.Select(f => f.Instance), stock ?? p.Market.InitialQuantities),
            p.Name, p.X, p.Y, schedules, p.Consumption, p.Role,
            new SettlementConfiguration(c.InitialPopulation, householdCash ?? c.InitialHouseholdCash,
                c.InitialProsperity, c.MinimumPopulation, c.MaximumPopulation, c.DemographicIntervalDays,
                c.PricingMode, c.QuantityScale));
    }

    [Fact]
    public void PaidSilverRetainsFifoBasisThroughVoyageAndReleasesItSeparatelyFromTradeMargin()
    {
        var generation = Generation();
        var world = World(startAtSilver: true, changePort: p =>
        {
            if (p.Id != generation.SilverPortId) return p;
            var stock = p.Market.InitialQuantities.ToDictionary(v => v.Key, v => v.Value);
            stock["silver"] = 20000;
            return CopyPort(p, stock);
        });
        var purchase = world.ExecuteTrade(Ship, generation.SilverPortId, new TradeCommand(TradeDirection.Buy, "silver", 20000));
        var paid = purchase.Trades.Single().Trade.ExecutedAmount;
        Assert.True(paid > 0);
        world.CommissionMint(Owner);
        world.Depart(Ship, generation.MintPortId);
        while (world.Snapshot().Day < 4 || world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
        var before = world.Snapshot();
        var issued = world.MintSilver(Ship);
        var finance = issued.After.Accounting.Companies[Owner];
        Assert.Equal(paid / 2, finance.MintMaterialCost);
        Assert.Equal(paid - paid / 2, finance.HeldInventoryCost);
        Assert.Equal(0, finance.CostOfGoodsSold);
        Assert.Equal(0, finance.SalesRevenue);
        Assert.Equal(0, finance.RealizedGrossMargin);
        Assert.Equal(600000 - paid / 2, finance.MintNetReceipts);
        Assert.Equal(before.TotalCash + 800000, issued.After.TotalCash);
        var movement = Assert.Single(Assert.Single(issued.EconomicEvents).Consumed);
        Assert.Equal(10000, movement.Quantity);
        Assert.Equal(paid / 2, movement.CostBasis);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var twice = world.MintSilver(Ship).After.Accounting.Companies[Owner];
        Assert.Equal(paid, twice.MintMaterialCost);
        Assert.Equal(0, twice.HeldInventoryCost);
        Assert.Equal(1200000, twice.MintReceipts);
        Assert.Equal(0, twice.SalesRevenue);
    }

    [Fact]
    public void LaterProductionOverflowRollsBackEarlierPayrollVoyagesAndDuePublicAndShipCompletion()
    {
        var world = World(changePort: p =>
        {
            if (p.Id != "westhaven") return p;
            var stock = p.Market.InitialQuantities.ToDictionary(v => v.Key, v => v.Value);
            stock["logs"] = int.MaxValue - 1;
            return CopyPort(p, stock, facilities: new[]
            {
                new ScheduledFacilityConfiguration(new BuildingInstance("late_overflow", "lumber_camp", "harvest_logs"),
                    4, firstDueDay: 4, wagePerBatch: 10000)
            });
        });
        var config = world.Snapshot().Economy!.Configuration;
        world.CommissionMint(Owner);
        world.CommissionShip(Owner, config.MintPortId);
        world.Depart(Ship, config.SilverPortId);
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(MintStatus.UnderConstruction, before.Economy!.MintStatus);
        Assert.Single(before.Economy.ShipOrders);
        Assert.DoesNotContain("player_ship_2", before.Ships.Keys);
        Assert.Equal(3, before.Day);
    }

    [Fact]
    public void MintIssuanceThatWouldOverflowReferenceValueRollsBackMetalMoneyCountersAndRevision()
    {
        // Initial reference assets: 100-capacity-unit ship = 5,000 coins; 24 Silver = 960.
        var assets = 50000000L + 240000L * 40;
        var world = World(cash: long.MaxValue - assets - 10,
            cargo: new Dictionary<string, int> { ["silver"] = 240000 });
        Assert.Equal(long.MaxValue - 10, world.Snapshot().CompanyValue(Owner).Total);
        world.CommissionMint(Owner);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        for (var batch = 0; batch < 20; batch++)
        {
            world.MintSilver(Ship);
            for (var day = 0; day < 4; day++) world.AdvanceDay();
        }
        var before = world.Snapshot();
        Assert.Equal(long.MaxValue - 10, before.CompanyValue(Owner).Total);
        Assert.Throws<OverflowException>(() => world.MintSilver(Ship));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(20, before.Economy!.MintBatches);
        Assert.Equal(40000, before.Ships[Ship].Cargo["silver"]);
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("market_overflow")]
    [InlineData("household_overflow")]
    public void ShipCommissionShortStockAndReceiverOverflowDoNotSpendOrCreateOrders(string cause)
    {
        var generation = Generation();
        var world = World(changePort: p =>
        {
            if (p.Id != generation.MintPortId) return p;
            var stock = p.Market.InitialQuantities.ToDictionary(v => v.Key, v => v.Value);
            if (cause == "stock") stock["planks"] = 39999;
            return CopyPort(p, stock, cause == "market_overflow" ? long.MaxValue - 1 : null,
                cause == "household_overflow" ? long.MaxValue - 1 : null);
        });
        var before = world.Snapshot();
        if (cause == "stock") Assert.Equal(DevelopmentOutcome.InsufficientStock,
            world.CommissionShip(Owner, generation.MintPortId).Development.Single().Outcome);
        else Assert.Throws<OverflowException>(() => world.CommissionShip(Owner, generation.MintPortId));
        Assert.Same(before, world.Snapshot());
        Assert.Empty(before.Economy!.ShipOrders);
        Assert.Equal(2, before.Economy.NextShipNumber);
    }

    [Fact]
    public void RoyalServicesRejectNpcRemoteAndAutomatedShipCommandsWithoutMutation()
    {
        var world = World(npc: true, cargo: new Dictionary<string, int> { ["silver"] = 10000, ["planks"] = 10000 });
        var config = world.Snapshot().Economy!.Configuration;
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.CommissionMint("npc_owner"));
        Assert.Throws<InvalidOperationException>(() => world.CommissionShip("npc_owner", config.MintPortId));
        Assert.Throws<InvalidOperationException>(() => world.MintSilver("npc"));
        Assert.Throws<InvalidOperationException>(() => world.FulfillExternalContract("npc", "royal_planks", 10000));
        Assert.Throws<InvalidOperationException>(() => world.SupplyPublicWorks("npc", 10000));
        Assert.Same(before, world.Snapshot());
        world.ConfigureAutomation(Ship, new TradeRoutePolicy(config.MintPortId, config.SilverPortId,
            new RouteLeg("planks", 10000, 10000, 100, 1), allowEmptyReturn: true), true);
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.MintSilver(Ship));
        Assert.Throws<InvalidOperationException>(() => world.FulfillExternalContract(Ship, "royal_planks", 10000));
        Assert.Throws<InvalidOperationException>(() => world.SupplyPublicWorks(Ship, 10000));
        Assert.Same(before, world.Snapshot());
        world.SetAutomationEnabled(Ship, false);
        world.Depart(Ship, config.SilverPortId);
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.MintSilver(Ship));
        Assert.Throws<InvalidOperationException>(() => world.FulfillExternalContract(Ship, "royal_planks", 10000));
        Assert.Throws<InvalidOperationException>(() => world.SupplyPublicWorks(Ship, 10000));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void TwoIndustrialShipsKeepIndependentStagesAndCompeteForActualSharedStock()
    {
        var world = World(second: true);
        var config = world.Snapshot().Economy!.Configuration;
        world.ConstructFacility(Owner, config.MintPortId, WorldIds.SawmillBlueprint, "shipping_store");
        world.SetFacilityEnabled(Owner, "shipping_store", false);
        world.ExecuteWarehouseTrade(Owner, "shipping_store", new TradeCommand(TradeDirection.Buy, "planks", 40000));
        IndustrialRoutePolicy Policy(int maximum, int minimum) => new(config.MintPortId, config.SilverPortId,
            new IndustrialRouteLeg("planks", maximum, minimum, IndustrialEndpointKind.Warehouse,
                sourceFacilityId: "shipping_store", minimumSellPrice: 1), allowEmptyReturn: true);
        world.ConfigureIndustrialAutomation(Ship, Policy(20000, 10000), true);
        world.ConfigureIndustrialAutomation("second", Policy(40000, 30000), true);
        world.RunAutomation(Ship);
        var first = world.Snapshot();
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, first.Ships[Ship].IndustrialStage);
        Assert.Equal(IndustrialRouteStage.AcquireOutbound, first.Ships["second"].IndustrialStage);
        Assert.Equal(20000, first.Ships[Ship].Cargo["planks"]);
        Assert.Equal(20000, first.PrivateFacilities["shipping_store"].Inventory["planks"]);
        world.RunAutomation("second");
        Assert.Equal(IndustrialRouteStage.AcquireOutbound, world.Snapshot().Ships["second"].IndustrialStage);
        Assert.Equal(0, world.Snapshot().Ships["second"].CargoUnits);
        world.AdvanceDay();
        world.ExecuteWarehouseTrade(Owner, "shipping_store", new TradeCommand(TradeDirection.Buy, "planks", 20000));
        world.RunAutomation("second");
        var after = world.Snapshot();
        Assert.Equal(IndustrialRouteStage.DeliverOutbound, after.Ships["second"].IndustrialStage);
        Assert.Equal(40000, after.Ships["second"].Cargo["planks"]);
        Assert.Equal(20000, after.Ships[Ship].Cargo["planks"]);
        Assert.Equal(0, after.PrivateFacilities["shipping_store"].Inventory["planks"]);
        Assert.Equal(after.Ships[Ship].RemainingTravelDays + 1, after.Ships["second"].RemainingTravelDays);
        Assert.Equal(60000, after.Accounting.Companies[Owner].HeldInventoryQuantities["planks"]);
        Assert.Equal(after.Companies[Owner].Cash, after.Ships[Ship].Cash);
        Assert.Equal(after.Companies[Owner].Cash, after.Ships["second"].Cash);
    }

    [Fact]
    public void WealthyHouseholdsCannotPayPublicPayrollUntilARealMarketPurchaseFundsEmployer()
    {
        var portId = Generation().MintPortId;
        var world = World(changePort: p => p.Id != portId ? p : CopyPort(p, cash: 0,
            householdCash: 10000000, facilities: new[]
            {
                new ScheduledFacilityConfiguration(new BuildingInstance("paid_logger", "lumber_camp", "harvest_logs"),
                    1, firstDueDay: 1, wagePerBatch: 10000)
            }));
        world.AdvanceDay();
        var blocked = world.Snapshot().Ports[portId];
        Assert.Equal(ScheduledProductionOutcome.PayrollShortage, blocked.Facilities.Single().LastOutcome);
        Assert.Equal(10000000, blocked.City!.HouseholdCash);
        Assert.Equal(0, blocked.MarketCash);
        var purchased = world.ExecuteTrade(Ship, portId, new TradeCommand(TradeDirection.Buy, "planks", 10000));
        Assert.True(purchased.Trades.Single().Trade.ExecutedAmount >= 10000);
        var funded = world.Snapshot().Ports[portId];
        world.AdvanceDay();
        var produced = world.Snapshot().Ports[portId];
        Assert.Equal(ScheduledProductionOutcome.Produced, produced.Facilities.Single().LastOutcome);
        Assert.Equal(funded.MarketCash - 10000, produced.MarketCash);
        Assert.Equal(funded.City!.HouseholdCash + 10000, produced.City!.HouseholdCash);
        Assert.Equal(funded.Inventory["logs"] + 20000, produced.Inventory["logs"]);
        Assert.Equal(0, world.Snapshot().Economy!.MintedCurrency);
    }

    [Fact]
    public void FoodRichMarketCannotFeedUnfundedHouseholdsUntilRealShipwrightWagesArrive()
    {
        var portId = Generation().MintPortId;
        var world = World(changePort: p =>
        {
            if (p.Id != portId) return p;
            var stock = p.Market.InitialQuantities.ToDictionary(v => v.Key, v => v.Value);
            stock["bread"] = 100000;
            return CopyPort(p, stock, cash: 10000000, householdCash: 0, facilities: Array.Empty<ScheduledFacilityConfiguration>());
        });
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var blocked = world.Snapshot().Ports[portId];
        var need = blocked.Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential);
        Assert.True(need.LastDesiredQuantity > 0);
        Assert.Equal(0, need.LastConsumedQuantity);
        Assert.Equal(need.LastDesiredQuantity, need.LastUnmetAffordability);
        Assert.Equal(0, need.LastUnmetStock);
        Assert.Equal(100000, blocked.Inventory["bread"]);
        var total = world.Snapshot().TotalCash;
        world.CommissionShip(Owner, portId);
        Assert.Equal(400000, world.Snapshot().Ports[portId].City!.HouseholdCash);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        need = world.Snapshot().Ports[portId].Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential);
        Assert.True(need.LastConsumedQuantity > 0);
        Assert.True(need.LastPayment > 0);
        Assert.Equal(total, world.Snapshot().TotalCash);
        Assert.Equal(0, world.Snapshot().Economy!.MintedCurrency);
    }

    [Fact]
    public void IssuingCurrencyCannotProduceFoodWhenEveryPhysicalSourceAndInputIsAbsent()
    {
        var world = World(cargo: new Dictionary<string, int> { ["silver"] = 10000 }, changePort: p =>
        {
            var stock = p.Market.InitialQuantities.ToDictionary(v => v.Key, v => v.Value);
            foreach (var good in new[] { "grain", "logs", "flour", "bread" }) stock[good] = 0;
            return CopyPort(p, stock, cash: 10000000, householdCash: 10000000,
                facilities: Array.Empty<ScheduledFacilityConfiguration>());
        });
        world.CommissionMint(Owner);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var before = world.Snapshot().TotalCash;
        world.MintSilver(Ship);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var after = world.Snapshot();
        Assert.Equal(before + 800000, after.TotalCash);
        Assert.Equal(800000, after.Economy!.MintedCurrency);
        foreach (var port in after.Ports.Values)
        {
            var need = port.Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential);
            Assert.Equal(0, need.TotalConsumedQuantity);
            Assert.Equal(0, need.TotalPayment);
            Assert.True(need.TotalUnmetStock > 0);
            Assert.Equal(0, need.TotalUnmetAffordability);
            Assert.Equal(0, port.Inventory["bread"]);
        }
    }
}
