using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ConstructionLifecycleTests
{
    private const string Owner = "company";
    private const string Facility = "private_bakery";
    private const string Blueprint = "bakery_plan";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static TradeCommand Buy(string good, int quantity) => new(TradeDirection.Buy, good, quantity);
    private static IndustryPolicy Policy(bool sell = true) => new(true, sell, inputs: new[]
    {
        new InputStockRule("flour", 4, 3), new InputStockRule("logs", 4, 3)
    }, outputs: new[] { new OutputSaleRule("bread", 0, 1) });

    private static RegionalWorldState World(RunDifficulty difficulty = RunDifficulty.SlowFoundations,
        long day = 0, long revision = 0, long companyCash = 1000, long marketCash = 1000,
        int interval = 2, bool reverse = false, bool lateOverflow = false,
        ChallengeConfiguration? challenge = null, RunIdentity? identity = null,
        IEnumerable<PrivateFacilityConfiguration>? initialFacilities = null)
    {
        var content = Content();
        var ports = new[] { "north", "south" }.Select(id =>
        {
            var publicFacilities = lateOverflow && id == "south"
                ? new[] { new ScheduledFacilityConfiguration(new BuildingInstance("overflow_farm", "farm", "harvest_grain"),
                    1, firstDueDay: 2) }
                : Array.Empty<ScheduledFacilityConfiguration>();
            var stock = new Dictionary<string, int> { ["flour"] = 20, ["logs"] = 20, ["bread"] = 10,
                ["grain"] = lateOverflow && id == "south" ? int.MaxValue : 0 };
            return new RegionalPortConfiguration(new PortConfiguration(id,
                content.Goods.Select(g => new MarketQuote(g.Id, g.Id == "bread" ? 8 : 2, g.Id == "bread" ? 9 : 3)),
                marketCash, publicFacilities.Select(f => f.Instance), stock), id, 0, 0,
                publicFacilities, Array.Empty<ConsumptionConfiguration>());
        }).ToArray();
        var policy = new TradeRoutePolicy("north", "south", new RouteLeg("logs", 1, 1, 3, 1), allowEmptyReturn: true);
        var ships = new[]
        {
            new RegionalShipConfiguration(new ShipConfiguration("ship", 10, "north"), Owner, "Player", 0, 100, isPlayer: true),
            new RegionalShipConfiguration(new ShipConfiguration("npc", 10, "north"), "npc_company", "NPC", 0, 100, policy: policy)
        };
        var blueprints = new[]
        {
            new IndustryBlueprint(Blueprint, "bakery", "bake_bread", 100, interval, 20, new[] { "north", "south" }),
            new IndustryBlueprint("second_bakery", "bakery", "bake_bread", 100, interval, 20, new[] { "north", "south" })
        };
        return new RegionalWorldState(content, reverse ? ports.Reverse() : ports,
            new[] { new SeaRoute("north", "south", 200) }, reverse ? ships.Reverse() : ships,
            day, revision, new[] { new CompanyConfiguration(Owner, companyCash), new CompanyConfiguration("npc_company", 100) },
            reverse ? blueprints.Reverse() : blueprints, challenge, initialFacilities,
            difficulty: difficulty, runIdentity: identity);
    }

    private static RegionalResult Order(RegionalWorldState world, string port = "north", string id = Facility,
        string blueprint = Blueprint, string owner = Owner) => world.ConstructFacility(owner, port, blueprint, id);
    private static PrivateFacilitySnapshot Factory(RegionalWorldState world) => world.Snapshot().PrivateFacilities[Facility];
    private static BigInteger Cash(RegionalWorldSnapshot state) =>
        state.Companies.Values.Aggregate(BigInteger.Zero, (sum, c) => sum + c.Cash) +
        state.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.MarketCash);

    [Fact]
    public void ConstructionPreviewIsPassiveAndMatchesOrderAtANonzeroDay()
    {
        var world = World(day: 7);
        var before = world.Snapshot();
        var preview = before.PreviewConstructionSchedule(Owner, Blueprint);
        Assert.Same(before, world.Snapshot());
        Assert.Equal((7L, 2, 9L, 11L),
            (preview.OrderedDay, preview.DelayDays, preview.CompletionDay, preview.FirstProductionDay));
        Assert.Throws<KeyNotFoundException>(() => before.PreviewConstructionSchedule("unknown", Blueprint));
        Assert.Throws<KeyNotFoundException>(() => before.PreviewConstructionSchedule(Owner, "unknown"));
        var order = Order(world).Construction.Single();
        Assert.Equal((preview.OrderedDay, preview.CompletionDay, preview.FirstProductionDay),
            (order.OrderedDay, order.CompletionDay, order.FirstProductionDay));
        Assert.Equal(7, world.Snapshot().Day);
    }

    [Fact]
    public void OrderingPaysOnceWithoutAdvancingDayAndCompletionWaitsTwoActualDays()
    {
        var world = World();
        var initialCash = Cash(world.Snapshot());
        var order = Order(world);
        var site = Factory(world);
        Assert.Equal((0L, 1L, 900L, 1100L), (order.After.Day, order.After.Revision,
            order.After.Companies[Owner].Cash, order.After.Ports["north"].MarketCash));
        Assert.Equal(ConstructionOutcome.Started, order.Construction.Single().Outcome);
        Assert.Equal((0L, 2L, 4L, 100L), (order.Construction.Single().OrderedDay,
            order.Construction.Single().CompletionDay, order.Construction.Single().FirstProductionDay,
            order.Construction.Single().Cost));
        Assert.Equal((FacilityConstructionStatus.UnderConstruction, 2, 0L, 2L, 4L),
            (site.ConstructionStatus, site.ConstructionDaysRemaining, site.OrderedDay,
                site.ConstructionCompletionDay, site.FirstProductionDay));
        Assert.Null(site.ConstructionCompletedDay);
        Assert.False(site.IsOperational);
        Assert.Equal(0, site.StoredUnits);
        Assert.Equal(ScheduledProductionOutcome.UnderConstruction, site.LastOutcome);
        Assert.Equal(100, order.After.Accounting.Companies[Owner].ConstructionExpenditure);
        Assert.Equal(EconomicEventKind.Construction, order.EconomicEvents.Single().Kind);
        world.SetFacilityEnabled(Owner, Facility, false);
        world.ConfigureFacility(Owner, Facility, Policy());
        Assert.Equal(2, Factory(world).ConstructionDaysRemaining);
        var first = world.AdvanceDay();
        Assert.Empty(first.Construction);
        Assert.Equal((1L, 1, false), (first.After.Day, Factory(world).ConstructionDaysRemaining, Factory(world).IsOperational));
        var second = world.AdvanceDay();
        var completed = second.Construction.Single();
        Assert.Equal((ConstructionOutcome.Completed, 0L, 2L, 4L),
            (completed.Outcome, completed.Cost, completed.CompletionDay, completed.FirstProductionDay));
        Assert.Equal((2L, 0, true, 2L, 4L), (second.After.Day, Factory(world).ConstructionDaysRemaining,
            Factory(world).IsOperational, Factory(world).ConstructionCompletedDay, Factory(world).NextProductionDay));
        Assert.False(Factory(world).Enabled);
        Assert.Equal(ScheduledProductionOutcome.Disabled, Factory(world).LastOutcome);
        var completionEvent = second.EconomicEvents.Single();
        Assert.Equal((EconomicEventKind.ConstructionCompleted, 2L, second.After.Revision, 0L, 0L, Facility),
            (completionEvent.Kind, completionEvent.Day, completionEvent.Revision,
                completionEvent.CashTotal, completionEvent.CompanyCashDelta, completionEvent.FacilityId));
        Assert.Equal((900L, 100L, initialCash), (second.After.Companies[Owner].Cash,
            second.After.Accounting.Companies[Owner].ConstructionExpenditure, Cash(second.After)));
        Assert.Empty(world.AdvanceDay().Construction);
        Assert.False(site.IsOperational); // Earlier immutable snapshots retain the pending state.
        Assert.Equal(2, site.ConstructionDaysRemaining);
    }

    [Fact]
    public void PendingFacilitiesCannotAutomaticallyProcureProduceOrSellEvenOnCompletionDay()
    {
        var world = World();
        Order(world);
        world.ConfigureFacility(Owner, Facility, Policy());
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("bread", 1));
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("flour", 1));
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("logs", 1));
        var accounting = JsonConvert.SerializeObject(world.Snapshot().Accounting);
        var stock = Factory(world).Inventory.ToArray();
        for (var day = 1; day <= 2; day++)
        {
            var result = world.AdvanceDay();
            Assert.Empty(result.PrivateProduction);
            Assert.Empty(result.WarehouseTrades);
            Assert.Equal(stock, Factory(world).Inventory.ToArray());
            Assert.Equal(accounting, JsonConvert.SerializeObject(result.After.Accounting));
            if (day == 1)
            {
                Assert.Equal(IndustryWaitReason.UnderConstruction, Factory(world).ProcurementWaitReason);
                Assert.Equal(IndustryWaitReason.UnderConstruction, Factory(world).SalesWaitReason);
            }
        }
        var third = world.AdvanceDay();
        Assert.Empty(third.PrivateProduction);
        Assert.Equal(2, third.WarehouseTrades.Count(t => t.Trade.Direction == TradeDirection.Buy));
        Assert.Single(third.WarehouseTrades, t => t.Trade.Direction == TradeDirection.Sell);
        Assert.Equal(0, Factory(world).BatchesProduced);
        var fourth = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, fourth.PrivateProduction.Single().Outcome);
        Assert.Equal((1L, 4L, 6L), (Factory(world).BatchesProduced, Factory(world).FirstProductionDay,
            Factory(world).NextProductionDay));
    }

    [Theory]
    [InlineData(RunDifficulty.Normal, ConstructionOutcome.Built, 0, 2)]
    [InlineData(RunDifficulty.SlowFoundations, ConstructionOutcome.Started, 2, 4)]
    public void FullIntervalStartsAtOperationalDay(RunDifficulty difficulty, ConstructionOutcome outcome,
        int completionDay, int productionDay)
    {
        var world = World(difficulty);
        var order = Order(world);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("flour", 1));
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("logs", 1));
        Assert.Equal(outcome, order.Construction.Single().Outcome);
        Assert.Equal(completionDay, Factory(world).ConstructionCompletionDay);
        Assert.Equal(productionDay, Factory(world).FirstProductionDay);
        while (world.Snapshot().Day < productionDay - 1)
        {
            Assert.Empty(world.AdvanceDay().PrivateProduction);
            Assert.Equal(0, Factory(world).BatchesProduced);
        }
        var produced = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, produced.PrivateProduction.Single().Outcome);
        Assert.Equal(1, Factory(world).BatchesProduced);
        Assert.Equal(2, Factory(world).Inventory["bread"]);
        Assert.Equal(6, produced.After.Accounting.Stores["facility:" + Facility].GetCostBasis("bread"));
        Assert.Equal(100, produced.After.Accounting.Companies[Owner].ConstructionExpenditure);
    }

    [Fact]
    public void PendingWarehouseAllowsNormalPhysicalTransfersPreservingFifoAndEnforcingDockAndCapacity()
    {
        var world = World();
        Order(world);
        world.ExecuteTrade("ship", "north", Buy("flour", 2));
        var basis = world.Snapshot().Accounting.Stores["ship:ship"].Goods["flour"].Lots.Single();
        var transfer = world.TransferGoods(Owner, "ship", Facility, TransferDirection.ShipToWarehouse, "flour", 2);
        var held = transfer.After.Accounting.Stores["facility:" + Facility].Goods["flour"].Lots.Single();
        Assert.Equal((basis.LotId, basis.CostBasis, basis.Provenance), (held.LotId, held.CostBasis, held.Provenance));
        Assert.Equal((0L, 2, 0L), (transfer.After.Day, Factory(world).ConstructionDaysRemaining, Factory(world).BatchesProduced));
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("logs", 18));
        world.ExecuteTrade("ship", "north", Buy("logs", 1));
        var full = world.Snapshot();
        Assert.Equal(TransferOutcome.InsufficientCapacity,
            world.TransferGoods(Owner, "ship", Facility, TransferDirection.ShipToWarehouse, "logs", 1).Transfers.Single().Outcome);
        Assert.Same(full, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods("npc_company", "ship", Facility,
            TransferDirection.ShipToWarehouse, "logs", 1));
        world.Depart("ship", "south");
        var sailing = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.TransferGoods(Owner, "ship", Facility,
            TransferDirection.WarehouseToShip, "flour", 1));
        Assert.Same(sailing, world.Snapshot());
        Assert.False(Factory(world).IsOperational);
        Assert.Equal(held.CostBasis, world.Snapshot().Accounting.Stores["facility:" + Facility].GetCostBasis("flour"));
    }

    [Fact]
    public void MultiplePendingSitesCompleteInOrdinalPortThenFacilityOrderRegardlessOfConfigurationOrder()
    {
        RegionalResult Complete(bool reverse)
        {
            var world = World(reverse: reverse);
            Order(world, "south", "z_site");
            Order(world, "north", "b_site");
            Order(world, "north", "a_site", "second_bakery");
            world.AdvanceDay();
            return world.AdvanceDay();
        }
        var normal = Complete(false);
        var reversed = Complete(true);
        Assert.Equal(new[] { "a_site", "b_site", "z_site" }, normal.Construction.Select(c => c.FacilityId));
        Assert.Equal(new[] { "a_site", "b_site", "z_site" }, normal.EconomicEvents.Select(e => e.FacilityId));
        Assert.Equal(JsonConvert.SerializeObject(normal.After), JsonConvert.SerializeObject(reversed.After));
        Assert.Equal(JsonConvert.SerializeObject(normal.Construction), JsonConvert.SerializeObject(reversed.Construction));
        Assert.Equal(JsonConvert.SerializeObject(normal.EconomicEvents), JsonConvert.SerializeObject(reversed.EconomicEvents));
        Assert.All(normal.After.PrivateFacilities.Values, f => Assert.Equal((true, 2L, 4L),
            (f.IsOperational, f.ConstructionCompletionDay, f.NextProductionDay)));
        Assert.Equal(300, normal.After.Accounting.Companies[Owner].ConstructionExpenditure);
    }

    [Theory]
    [InlineData(RunScenario.MerchantCharter)]
    [InlineData(RunScenario.IndustrialCharter)]
    [InlineData(RunScenario.IndependentCharter)]
    public void CharterEndowmentsAreUnaffectedButPurchasedPlayerFacilitiesUseRunDifficulty(string charterId)
    {
        var content = Content();
        var ordinary = RunScenario.Create(content, new RunIdentity(null, charterId)).Snapshot();
        var slow = RunScenario.Create(content, new RunIdentity(null, charterId, difficulty: RunDifficulty.SlowFoundations));
        var initial = slow.Snapshot();
        Assert.Equal(JsonConvert.SerializeObject(ordinary.Accounting), JsonConvert.SerializeObject(initial.Accounting));
        Assert.Equal(ordinary.Companies[WorldIds.PlayerOwnerId].Cash, initial.Companies[WorldIds.PlayerOwnerId].Cash);
        Assert.Equal(JsonConvert.SerializeObject(ordinary.PrivateFacilities), JsonConvert.SerializeObject(initial.PrivateFacilities));
        if (charterId == RunScenario.IndustrialCharter)
        {
            var starter = initial.PrivateFacilities[RunScenario.StarterBakery];
            Assert.Equal((true, true, 0L, 2L, 0), (starter.IsStarterGrant, starter.IsOperational,
                starter.ConstructionCompletionDay, starter.FirstProductionDay, starter.ConstructionDaysRemaining));
            slow.AdvanceDay();
            Assert.Equal(ScheduledProductionOutcome.Produced, slow.AdvanceDay().PrivateProduction.Single().Outcome);
            Assert.Equal(0, slow.Snapshot().Accounting.Companies[WorldIds.PlayerOwnerId].ConstructionExpenditure);
        }
        var order = slow.ConstructFacility(WorldIds.PlayerOwnerId, WorldIds.North,
            WorldIds.BakeryBlueprint, "new_player_bakery");
        Assert.Equal(ConstructionOutcome.Started, order.Construction.Single().Outcome);
        Assert.Equal(2, order.After.PrivateFacilities["new_player_bakery"].ConstructionDaysRemaining);
    }

    [Fact]
    public void DelayOnlyAffectsPlayerOwnedPurchases()
    {
        var world = World();
        var npc = Order(world, id: "npc_bakery", owner: "npc_company");
        Assert.Equal(ConstructionOutcome.Built, npc.Construction.Single().Outcome);
        Assert.True(npc.After.PrivateFacilities["npc_bakery"].IsOperational);
        Assert.Equal(2, npc.After.PrivateFacilities["npc_bakery"].NextProductionDay);
    }

    [Fact]
    public void UnderfundedDuplicateAndArithmeticInvalidOrdersLeaveNoPartialSiteOrPayment()
    {
        var poor = World(companyCash: 99);
        var before = poor.Snapshot();
        var rejected = Order(poor);
        Assert.Equal(ConstructionOutcome.InsufficientFunds, rejected.Construction.Single().Outcome);
        Assert.Null(rejected.Construction.Single().CompletionDay);
        Assert.Empty(rejected.EconomicEvents);
        Assert.Same(before, rejected.After);
        var world = World();
        Order(world);
        before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => Order(world, id: "duplicate"));
        Assert.Same(before, world.Snapshot());
        foreach (var invalid in new[] { World(day: long.MaxValue - 1), World(day: long.MaxValue - 3),
            World(marketCash: long.MaxValue), World(revision: long.MaxValue) })
        {
            before = invalid.Snapshot();
            Assert.Throws<OverflowException>(() => Order(invalid));
            Assert.Same(before, invalid.Snapshot());
            Assert.Empty(before.PrivateFacilities);
            Assert.Equal(0, before.Accounting.Companies[Owner].ConstructionExpenditure);
        }
    }

    [Fact]
    public void LateDailyOverflowRollsBackEarlierNpcTradeVoyageLotsAndDueConstruction()
    {
        var world = World(lateOverflow: true);
        Order(world);
        world.AdvanceDay();
        world.SetAutomationEnabled("npc", true);
        var before = world.Snapshot();
        var fingerprint = JsonConvert.SerializeObject(before);
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(fingerprint, JsonConvert.SerializeObject(world.Snapshot()));
        Assert.Equal((1L, false, 1, 900L, "north", 0), (before.Day, Factory(world).IsOperational,
            Factory(world).ConstructionDaysRemaining, before.Companies[Owner].Cash,
            before.Ships["npc"].DockedPortId, before.Ships["npc"].CargoUnits));
        world.SetProductionEnabled("south", "overflow_farm", false);
        var completed = world.AdvanceDay();
        Assert.Equal(ConstructionOutcome.Completed, completed.Construction.Single().Outcome);
        Assert.Single(completed.Trades);
        Assert.Equal(2, completed.After.Day);
        Assert.True(Factory(world).IsOperational);
    }

    [Fact]
    public void DisabledAndInputShortageRemainDueAfterCompletionWithoutCatchup()
    {
        var world = World();
        Order(world);
        world.SetFacilityEnabled(Owner, Facility, false);
        for (var i = 0; i < 3; i++) world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Disabled, world.AdvanceDay().PrivateProduction.Single().Outcome);
        Assert.Equal(4, Factory(world).NextProductionDay);
        world.SetFacilityEnabled(Owner, Facility, true);
        Assert.Equal(ScheduledProductionOutcome.InputShortage, world.AdvanceDay().PrivateProduction.Single().Outcome);
        Assert.Equal(4, Factory(world).NextProductionDay);
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("flour", 4));
        world.ExecuteWarehouseTrade(Owner, Facility, Buy("logs", 4));
        var resumed = world.AdvanceDay();
        Assert.Single(resumed.PrivateProduction);
        Assert.Equal((6L, 1L, 8L), (resumed.After.Day, Factory(world).BatchesProduced, Factory(world).NextProductionDay));
    }


    [Fact]
    public void DifficultyValidationAndIdentityDisagreementFailExplicitly()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => World((RunDifficulty)2));
        var identity = new RunIdentity(null, RunScenario.MerchantCharter, difficulty: RunDifficulty.SlowFoundations, companyStorageRules: CompanyStorageRules.Legacy, itineraryVersion: 0, economicRulesVersion: 0);
        Assert.Throws<ArgumentException>(() => World(RunDifficulty.Normal, identity: identity));
        Assert.Throws<ArgumentException>(() => World(identity: new RunIdentity("retired_coastal_supply",
            RunScenario.MerchantCharter, difficulty: RunDifficulty.SlowFoundations, companyStorageRules: CompanyStorageRules.Legacy, itineraryVersion: 0, economicRulesVersion: 0)));
        var world = World(identity: identity);
        Order(world);
        Assert.Same(identity, world.Snapshot().RunIdentity);
        Assert.Same(identity, world.AdvanceDay().After.RunIdentity);
        Assert.Equal(RunDifficulty.SlowFoundations, world.Snapshot().Difficulty);
    }
}
