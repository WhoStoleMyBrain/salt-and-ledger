using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingDevelopmentTests
{
    private const string Owner = "player_company", Ship = "ledger";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static RegionalWorldState Fixture(long cash = 30000000, long external = 12000000,
        IReadOnlyDictionary<string, int>? cargo = null, long initialDay = 0, long treasury = 0)
    {
        var generation = ExpandingEconomyGeneration.Generate(42);
        var config = ExpandingEconomyConfiguration.CreateDefault(generation.MintPortId, generation.SilverPortId);
        config = new ExpandingEconomyConfiguration(config.MintPortId, config.SilverPortId,
            config.Contracts, config.GoodsReferencePrices, external, treasury);
        return new RegionalWorldState(Content(), generation.Ports, generation.Geography.Routes,
            new[] { new RegionalShipConfiguration(new ShipConfiguration(Ship, 1000000, generation.MintPortId, cargo),
                Owner, "Test vessel", 0, 100, isPlayer: true) }, initialDay: initialDay,
            companies: new[] { new CompanyConfiguration(Owner, cash) }, industryBlueprints: generation.Blueprints, economy: config);
    }

    [Fact]
    public void CommissionActivatesOnExactDayThenConvertsPhysicalSilverOnlyOncePerInterval()
    {
        var world = Fixture(cargo: new Dictionary<string, int> { ["silver"] = 20000 });
        var initial = world.Snapshot();
        Assert.Equal(DevelopmentOutcome.NotReady, world.MintSilver(Ship).Development.Single().Outcome);
        Assert.Same(initial, world.Snapshot());
        var commissioned = world.CommissionMint(Owner);
        Assert.Equal(MintStatus.UnderConstruction, commissioned.After.Economy!.MintStatus);
        Assert.Equal(initial.TotalCash, commissioned.After.TotalCash);
        Assert.Equal(initial.CompanyValue(Owner).Total - 4000000, commissioned.After.CompanyValue(Owner).Total);
        for (var i = 0; i < 3; i++) world.AdvanceDay();
        Assert.Equal(MintStatus.UnderConstruction, world.Snapshot().Economy!.MintStatus);
        world.AdvanceDay();
        Assert.Equal(MintStatus.Active, world.Snapshot().Economy!.MintStatus);
        var before = world.Snapshot();
        var issue = world.MintSilver(Ship, expectedSnapshot: before);
        Assert.Equal(800000, issue.After.Economy!.MintedCurrency);
        Assert.Equal(10000, issue.After.Economy.SilverReserve);
        Assert.Equal(before.TotalCash + 800000, issue.After.TotalCash);
        Assert.Equal(before.Companies[Owner].Cash + 600000, issue.After.Companies[Owner].Cash);
        Assert.Equal(before.Economy!.TreasuryCash + 200000, issue.After.Economy.TreasuryCash);
        Assert.Equal(600000, issue.After.Accounting.Companies[Owner].MintReceipts);
        Assert.Equal(0, issue.After.Accounting.Companies[Owner].SalesRevenue);
        Assert.Equal(10000, issue.After.Accounting.Companies[Owner].SurrenderedQuantities["silver"]);
        Assert.Equal(EconomicEventKind.MintExchange, issue.EconomicEvents.Single().Kind);
        Assert.Equal(DevelopmentOutcome.NotReady, world.MintSilver(Ship).Development.Single().Outcome);
        Assert.Same(issue.After, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.MintSilver(Ship, expectedSnapshot: before));
        Assert.Same(issue.After, world.Snapshot());
    }

    [Fact]
    public void MintAuthorizationIsFiniteAndHasNoPrivateAssetOrDuplicateMetal()
    {
        var world = Fixture(cargo: new Dictionary<string, int> { ["silver"] = 250000 });
        world.CommissionMint(Owner);
        for (var i = 0; i < 4; i++) world.AdvanceDay();
        for (var batch = 0; batch < 24; batch++)
        {
            Assert.Equal(DevelopmentOutcome.Completed, world.MintSilver(Ship).Development.Single().Outcome);
            for (var i = 0; i < 4; i++) world.AdvanceDay();
        }
        var before = world.Snapshot();
        Assert.Equal(DevelopmentOutcome.Exhausted, world.MintSilver(Ship).Development.Single().Outcome);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(240000, before.Economy!.SilverReserve);
        Assert.Equal(10000, before.Ships[Ship].Cargo["silver"]);
        Assert.Empty(before.PrivateFacilities);
        Assert.Equal(0, before.CompanyValue(Owner).Facilities);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(249999)]
    public void ExternalAuthorityCannotOverspendFiniteFunding(long budget)
    {
        var world = Fixture(external: budget, cargo: new Dictionary<string, int> { ["planks"] = 10000 });
        var before = world.Snapshot();
        Assert.Equal(DevelopmentOutcome.InsufficientFunds,
            world.FulfillExternalContract(Ship, "royal_planks", 10000).Development.Single().Outcome);
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void ExternalDeliveryMovesExistingMoneyAndTreasuryProcurementUsesRealFunding()
    {
        var world = Fixture(cargo: new Dictionary<string, int> { ["planks"] = 120000 });
        var initial = world.Snapshot();
        var exported = world.FulfillExternalContract(Ship, "royal_planks", 100000);
        Assert.Equal(initial.TotalCash, exported.After.TotalCash);
        Assert.Equal(initial.RegionalCash + 2500000, exported.After.RegionalCash);
        Assert.Equal(2500000, exported.After.Economy!.ExternalNetInflow);
        Assert.Equal(0, exported.After.Economy.MintedCurrency);
        Assert.Equal(DevelopmentOutcome.Exhausted, world.FulfillExternalContract(Ship, "royal_planks", 10000).Development.Single().Outcome);
        Assert.Equal(DevelopmentOutcome.InsufficientFunds, world.SupplyPublicWorks(Ship, 20000).Development.Single().Outcome);
        world.CommissionMint(Owner);
        var works = world.SupplyPublicWorks(Ship, 20000);
        Assert.Equal(500000, works.After.Economy!.TreasuryExpenditure);
        Assert.Equal(20000, works.After.Economy.TreasuryPlanks);
        Assert.Equal(initial.TotalCash, works.After.TotalCash);
        Assert.Equal(0, works.After.Ships[Ship].Cargo["planks"]);
    }

    [Fact]
    public void TwoAdditionalShipsRequireHullMaterialsPaymentLeadTimeAndIndependentControls()
    {
        var world = Fixture();
        var before = world.Snapshot(); var port = before.Economy!.Configuration.ShipwrightPortId;
        var value = before.CompanyValue(Owner).Total;
        var first = world.CommissionShip(Owner, port);
        Assert.Equal(DevelopmentOutcome.Started, first.Development.Single().Outcome);
        Assert.Equal(before.Ports[port].Inventory["planks"] - 40000, first.After.Ports[port].Inventory["planks"]);
        Assert.Equal(5000000, first.After.CompanyValue(Owner).PendingCapital);
        Assert.Equal(value, first.After.CompanyValue(Owner).Total);
        world.CommissionShip(Owner, port);
        Assert.Equal(DevelopmentOutcome.FleetLimit, world.CommissionShip(Owner, port).Development.Single().Outcome);
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        Assert.Single(world.Snapshot().Ships);
        var completed = world.AdvanceDay();
        Assert.Equal(3, completed.After.Ships.Count);
        Assert.Empty(completed.After.Economy!.ShipOrders);
        Assert.All(completed.After.Ships.Values, ship => Assert.Equal(0, ship.CargoUnits));
        Assert.Equal(0, completed.After.CompanyValue(Owner).PendingCapital);
        Assert.Equal(10000000, completed.After.Accounting.Companies[Owner].ConstructionExpenditure);
        var destination = completed.After.Ports.Keys.First(id => id != port);
        world.Depart("player_ship_2", destination);
        Assert.Equal(port, world.Snapshot().Ships["player_ship_3"].DockedPortId);
        Assert.Null(world.Snapshot().Ships["player_ship_2"].DockedPortId);
        var snapshot = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade("player_ship_2", port, new TradeCommand(TradeDirection.Buy, "grain", 10000)));
        Assert.Same(snapshot, world.Snapshot());
    }

    [Fact]
    public void ShortageAndOverflowLeaveCommissionStateUntouched()
    {
        var poor = Fixture(cash: 0);
        var before = poor.Snapshot();
        Assert.Equal(DevelopmentOutcome.InsufficientFunds, poor.CommissionShip(Owner, before.Economy!.Configuration.MintPortId).Development.Single().Outcome);
        Assert.Same(before, poor.Snapshot());
        var overflow = Fixture(treasury: long.MaxValue);
        var snapshot = overflow.Snapshot();
        Assert.Throws<OverflowException>(() => overflow.CommissionMint(Owner));
        Assert.Same(snapshot, overflow.Snapshot());
    }

}
