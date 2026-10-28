using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class CommodityFlowSignalsTests
{
    private const int Unit = EconomicUnits.QuantityScale;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();

    [Fact]
    public void PublicScheduledPotentialMatchesActualResourcedBatchesAndPaidNeeds()
    {
        var world = Fixture(facilities: new[] { Public("bakery", "bake_bread", 2) });
        var before = world.Snapshot();
        var flow = EconomicSignals.CommodityFlow(before, Content(), "town", "bread");
        Assert.Equal(12, flow.HorizonDays);
        Assert.Equal(1, flow.FirstDay);
        Assert.Equal(12, flow.LastDay);
        Assert.True(flow.HasStore);
        Assert.True(flow.Produces);
        Assert.True(flow.Uses);
        Assert.Equal(12 * Unit, flow.EnabledProductionQuantity);
        Assert.Equal(9 * Unit, flow.ConsumptionNeedQuantity);
        Assert.Equal(3 * Unit, flow.NetQuantity);
        Assert.Equal(6, Assert.Single(flow.Production).Opportunities);
        Assert.Equal(3, Assert.Single(flow.Consumption).Opportunities);
        var consumed = 0L;
        for (var day = 0; day < 12; day++)
            consumed += world.AdvanceDay().Consumption.Where(c => c.PortId == "town").Sum(c => (long)c.ConsumedQuantity);
        Assert.Equal(flow.ConsumptionNeedQuantity, consumed);
        Assert.Equal(flow.CurrentStock + flow.NetQuantity, world.Snapshot().Ports["town"].Inventory["bread"]);
    }

    [Fact]
    public void OverdueProductionHasOneNextDayOpportunityAndNeverCatchesUp()
    {
        var world = Fixture(initialDay: 20, facilities: new[] { Public("bakery", "bake_bread", 5, firstDue: 3) });
        var flow = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "bread");
        var batch = Assert.Single(flow.Production);
        Assert.Equal(3, batch.Opportunity.ScheduledDueDay);
        Assert.Equal(21, batch.Opportunity.NextEligibleDay);
        Assert.Equal(3, batch.Opportunities); // D21,26,31, never the missed D3/8/13/18 slots.
        Assert.Equal(6 * Unit, batch.OutputQuantity);
        var result = world.AdvanceDay();
        Assert.Single(result.Production);
        Assert.Equal(ScheduledProductionOutcome.Produced, result.Production[0].Outcome);
        Assert.Equal(26, result.After.Ports["town"].Facilities.Single().NextDueDay);
    }

    [Fact]
    public void InstalledRolesRemainVisibleWhenNextBatchIsBeyondTheHorizon()
    {
        var world = Fixture(facilities: new[] { Public("mill", "mill_flour", 30) });
        var flour = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "flour");
        Assert.True(flour.Produces);
        Assert.False(flour.Uses);
        Assert.Equal(0, flour.InstalledProductionQuantity);
        Assert.Equal(0, Assert.Single(flour.Production).Opportunities);
        var grain = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "grain");
        Assert.True(grain.Uses);
        Assert.False(grain.Produces);
        Assert.Equal(0, grain.UsageQuantity);
    }

    [Fact]
    public void InputsAndMunicipalUseAreAddedWhileGrossProductionStaysSeparate()
    {
        var world = Fixture(facilities: new[] { Public("sawmill", "saw_planks", 3),
            Public("furniture_workshop", "make_furniture", 4) },
            extraNeeds: new[] { new ConsumptionConfiguration("maintenance", "planks", Unit / 2, 6,
                kind: ConsumptionKind.Municipal) });
        var flow = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "planks");
        Assert.True(flow.Produces);
        Assert.True(flow.Uses);
        Assert.Equal(4 * Unit, flow.EnabledProductionQuantity);
        Assert.Equal(6 * Unit, flow.InputUsageQuantity);
        Assert.Equal(Unit, flow.ConsumptionNeedQuantity);
        Assert.Equal(7 * Unit, flow.UsageQuantity);
        Assert.Equal(-3 * Unit, flow.NetQuantity);
        Assert.Equal(0, Assert.Single(flow.Consumption).NextPayment);
    }

    [Fact]
    public void PrivateFlowsCountSharedStockOnceAndExcludeOtherOwnersPublicNeedsAndPendingSites()
    {
        var world = Fixture(facilities: new[] { Public("bakery", "bake_bread", 2) },
            difficulty: RunDifficulty.SlowFoundations, sites: new[]
            {
                Site("active", stock: new Dictionary<string, int> { ["bread"] = 2 * Unit, ["flour"] = Unit, ["logs"] = Unit }),
                Site("active_second"),
                Site("disabled", enabled: false),
                Site("rival_site", owner: "rival", stock: new Dictionary<string, int> { ["bread"] = 7 * Unit }),
                Site("remote_site", port: "other", stock: new Dictionary<string, int> { ["bread"] = 9 * Unit })
            });
        world.ConstructFacility("owner", "town", "plan", "pending");
        var before = world.Snapshot();
        var flow = EconomicSignals.CommodityFlow(before, Content(), "town", "bread", "owner");
        Assert.True(flow.HasStore);
        Assert.Equal(2 * Unit, flow.CurrentStock);
        Assert.Equal(4, flow.Production.Count);
        Assert.Empty(flow.Consumption);
        Assert.False(flow.Uses);
        Assert.Equal(46 * Unit, flow.InstalledProductionQuantity);
        Assert.Equal(24 * Unit, flow.EnabledProductionQuantity);
        Assert.Equal(0, flow.UsageQuantity);
        var disabled = flow.Production.Single(p => p.Opportunity.FacilityId == "disabled");
        Assert.False(disabled.IsActive);
        Assert.Equal(ScheduledProductionOutcome.Disabled, disabled.Opportunity.CurrentReadiness);
        var pending = flow.Production.Single(p => p.Opportunity.FacilityId == "pending");
        Assert.False(pending.IsActive);
        Assert.True(pending.Opportunity.UnderConstruction);
        Assert.Equal(4, pending.Opportunity.NextEligibleDay);
        Assert.Equal(5, pending.Opportunities);
        Assert.Equal(2 * Unit, before.CompanyStockpiles["company:owner:town"].Inventory["bread"]);
        Assert.Equal(30 * Unit, EconomicSignals.CommodityFlow(before, Content(), "town", "bread").CurrentStock);
        var absent = EconomicSignals.CommodityFlow(before, Content(), "other", "bread", "rival");
        Assert.False(absent.HasStore);
        Assert.Equal(0, absent.CurrentStock);
        Assert.Empty(absent.Production);
        Assert.Empty(absent.Consumption);
        Assert.Same(before, world.Snapshot());
        Assert.All(flow.Production.Where(p => p.IsActive), p => Assert.True(p.Opportunity.ReadyFromCurrentState));
        world.AdvanceDay();
        var result = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, result.PrivateProduction.Single(p => p.FacilityId == "active").Outcome);
        Assert.Equal(ScheduledProductionOutcome.InputShortage,
            result.PrivateProduction.Single(p => p.FacilityId == "active_second").Outcome);
    }

    [Theory]
    [InlineData(0, 10000000, null, true, ScheduledProductionOutcome.InputShortage)]
    [InlineData(200000, 59999, null, true, ScheduledProductionOutcome.PayrollShortage)]
    [InlineData(200000, 10000000, 10000, true, ScheduledProductionOutcome.OutputLimit)]
    [InlineData(200000, 10000000, null, false, ScheduledProductionOutcome.Disabled)]
    public void BlockedCapacityIsConditionalAndDisabledCapacityIsExcluded(int flour, long cash,
        int? limit, bool enabled, ScheduledProductionOutcome expected)
    {
        var world = Fixture(flour: flour, marketCash: cash,
            facilities: new[] { Public("bakery", "bake_bread", 2, enabled: enabled, limit: limit) });
        var before = world.Snapshot();
        var flow = EconomicSignals.CommodityFlow(before, Content(), "town", "bread");
        var batch = Assert.Single(flow.Production);
        Assert.Equal(expected, batch.Opportunity.CurrentReadiness);
        Assert.Equal(12 * Unit, flow.InstalledProductionQuantity);
        Assert.Equal(enabled ? 12 * Unit : 0, flow.EnabledProductionQuantity);
        Assert.Equal(enabled, batch.IsActive);
        Assert.False(batch.Opportunity.ReadyFromCurrentState);
        Assert.Same(before, world.Snapshot());
        world.AdvanceDay();
        Assert.Equal(expected, Assert.Single(world.AdvanceDay().Production).Outcome);
    }

    [Fact]
    public void PrivateCapacityBlockIsShownEvenWhenScheduledPotentialIsPositive()
    {
        var world = Fixture(extraction: true, capacity: Unit, sites: new[] { Site("camp") });
        var flow = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "logs", "owner");
        Assert.Equal(12 * Unit, flow.EnabledProductionQuantity);
        Assert.Equal(ScheduledProductionOutcome.WarehouseCapacity, Assert.Single(flow.Production).Opportunity.CurrentReadiness);
        world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.WarehouseCapacity, Assert.Single(world.AdvanceDay().PrivateProduction).Outcome);
    }

    [Fact]
    public void FractionalNeedCarriesTheRealResidualAcrossTheWindowWithoutConsumingIt()
    {
        var world = Fixture(population: 10, populationPerUnit: 3);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var before = world.Snapshot();
        var flow = EconomicSignals.CommodityFlow(before, Content(), "town", "bread");
        var food = Assert.Single(flow.Consumption);
        Assert.Equal(5, flow.FirstDay);
        Assert.Equal(16, flow.LastDay);
        Assert.Equal(8, food.NextDueDay);
        Assert.Equal(1, food.DemandRemainder);
        Assert.Equal(2, food.NextDemandRemainder);
        Assert.Equal(33333, food.NextDesiredQuantity);
        Assert.Equal(100000, food.NeedQuantity); // 33333 + 33334 + 33333, not 3 * truncated demand.
        Assert.True(food.HasObservation);
        Assert.Equal(33333, food.LastDesiredQuantity);
        Assert.Equal(33333, food.LastConsumedQuantity);
        Assert.Equal(0, food.LastUnmetStock);
        Assert.Equal(0, food.LastUnmetAffordability);
        Assert.Equal(199998, food.LastPayment);
        Assert.Equal(1, before.Ports["town"].Consumption.Single().DemandRemainder);
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void LuxuryRolePersistsWhileEligibilityGatesItsCurrentDemand()
    {
        var world = Fixture(population: 10, populationPerUnit: 3, foodInterval: 2,
            extraNeeds: new[] { new ConsumptionConfiguration("luxury", "furniture", Unit, 4,
                kind: ConsumptionKind.Luxury, populationPerUnit: 3, proportional: true) });
        var initial = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "furniture");
        Assert.True(initial.Uses);
        var inactive = Assert.Single(initial.Consumption);
        Assert.False(inactive.IsEligible);
        Assert.False(inactive.HasObservation);
        Assert.Equal(0, inactive.NeedQuantity);
        Assert.Equal(0, inactive.NextDesiredQuantity);
        for (var day = 0; day < 6; day++) world.AdvanceDay();
        Assert.True(world.Snapshot().Ports["town"].City!.LuxuryEnabled);
        var active = Assert.Single(EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "furniture").Consumption);
        Assert.True(active.IsEligible);
        Assert.True(active.HasObservation); // D4 was an observed, gated zero-demand opportunity.
        Assert.Equal(0, active.LastDesiredQuantity);
        Assert.Equal(100000, active.NeedQuantity);
        Assert.Equal(world.Snapshot().PreviewHouseholdConsumption("town", "luxury").ConsumedQuantity,
            active.NextAffordableQuantity);
    }

    [Fact]
    public void ActualRetailShortfallsAndCurrentConstraintsAreSeparateFromScheduledNeed()
    {
        var world = Fixture(bread: 2 * Unit, householdCash: 6 * Unit);
        var initial = Assert.Single(EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "bread").Consumption);
        Assert.Equal(3 * Unit, initial.NextDesiredQuantity);
        Assert.Equal(Unit, initial.NextStockShortfall);
        Assert.Equal(Unit, initial.NextAffordableQuantity);
        Assert.Equal(Unit, initial.NextAffordabilityShortfall);
        Assert.False(initial.HasObservation);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var after = Assert.Single(EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "bread").Consumption);
        Assert.Equal(9 * Unit, after.NeedQuantity);
        Assert.Equal(3 * Unit, after.LastDesiredQuantity);
        Assert.Equal(Unit, after.LastConsumedQuantity);
        Assert.Equal(Unit, after.LastUnmetStock);
        Assert.Equal(Unit, after.LastUnmetAffordability);
        Assert.Equal(6 * Unit, after.LastPayment);
        Assert.Equal(2 * Unit, after.NextStockShortfall);
        Assert.Equal(0, after.NextAffordableQuantity);
        Assert.Equal(Unit, after.NextAffordabilityShortfall);
    }

    [Fact]
    public void OverflowingRetailProposalDoesNotInventAffordabilityFacts()
    {
        var world = Fixture(marketCash: long.MaxValue);
        var before = world.Snapshot();
        var flow = EconomicSignals.CommodityFlow(before, Content(), "town", "bread");
        var food = Assert.Single(flow.Consumption);
        Assert.Equal(9 * Unit, flow.ConsumptionNeedQuantity);
        Assert.Equal(3 * Unit, food.NextDesiredQuantity);
        Assert.Equal(0, food.NextStockShortfall);
        Assert.False(food.PreviewAvailable);
        Assert.True(food.NumericCapacityBlocked);
        Assert.Null(food.NextAffordableQuantity);
        Assert.Null(food.NextAffordabilityShortfall);
        Assert.Null(food.NextPayment);
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void HorizonNeedCanExceedAnInventoryWithoutOverflowingPhysicalDemand()
    {
        var world = Fixture(population: 200000, populationPerUnit: 1, foodInterval: 1);
        var flow = EconomicSignals.CommodityFlow(world.Snapshot(), Content(), "town", "bread");
        var food = Assert.Single(flow.Consumption);
        Assert.Equal(2000000000, food.NextDesiredQuantity);
        Assert.Equal(24000000000L, flow.ConsumptionNeedQuantity);
        Assert.Equal(-24000000000L, flow.NetQuantity);
    }

    [Fact]
    public void EveryModernCommodityScopeIsDeterministicAndLeavesSnapshotAndSaveBytesUnchanged()
    {
        var content = Content();
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.IndustrialCharter,
            RunWorldMode.ExpandingEconomy, 99));
        var snapshot = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        string? prior = null;
        for (var iteration = 0; iteration < 3; iteration++)
        {
            var signals = snapshot.Ports.Keys.SelectMany(port => content.Goods.SelectMany(good =>
                snapshot.Companies.Keys.Select(owner => EconomicSignals.CommodityFlow(snapshot, content, port, good.Id, owner))
                    .Prepend(EconomicSignals.CommodityFlow(snapshot, content, port, good.Id)))).ToArray();
            var serialized = JsonConvert.SerializeObject(signals);
            if (prior != null) Assert.Equal(prior, serialized);
            prior = serialized;
            Assert.Equal(saved, WorldSaveCodec.Save(world));
            Assert.Same(snapshot, world.Snapshot());
        }
        var restored = WorldSaveCodec.Load(content, saved);
        _ = EconomicSignals.CommodityFlow(restored.Snapshot(), content, restored.Snapshot().Ports.Keys.First(), "bread");
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
    }

    private static ScheduledFacilityConfiguration Public(string building, string recipe, int interval,
        long? firstDue = null, bool enabled = true, int? limit = null) => new(
            new BuildingInstance("public_" + building, building, recipe, productionEnabled: enabled), interval,
            limit, firstDue, 6 * Unit);

    private static PrivateFacilityConfiguration Site(string id, string owner = "owner", string port = "town",
        IReadOnlyDictionary<string, int>? stock = null, bool enabled = true) =>
        new(id, owner, port, "plan", stock, enabled: enabled);

    private static RegionalWorldState Fixture(ScheduledFacilityConfiguration[]? facilities = null,
        ConsumptionConfiguration[]? extraNeeds = null, PrivateFacilityConfiguration[]? sites = null,
        int bread = 30 * Unit, int flour = 20 * Unit, long householdCash = 1000 * Unit,
        long marketCash = 1000 * Unit, int population = 30, int populationPerUnit = 10, int foodInterval = 4,
        int capacity = 100 * Unit, bool extraction = false, long initialDay = 0,
        RunDifficulty difficulty = RunDifficulty.Normal)
    {
        var content = Content();
        facilities ??= Array.Empty<ScheduledFacilityConfiguration>();
        var needs = new[] { new ConsumptionConfiguration("food", "bread", Unit, foodInterval,
            kind: ConsumptionKind.Essential, populationPerUnit: populationPerUnit, proportional: true) }
            .Concat(extraNeeds ?? Array.Empty<ConsumptionConfiguration>()).ToArray();
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id,
            content.Goods.Select(g => new MarketQuote(g.Id, 5, 6)), id == "town" ? marketCash : 1000 * Unit,
            id == "town" ? facilities.Select(f => f.Instance) : Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["bread"] = bread, ["flour"] = flour, ["logs"] = 40 * Unit,
                ["planks"] = 20 * Unit, ["grain"] = 20 * Unit, ["furniture"] = 20 * Unit }),
            id, id == "town" ? 0 : 100, 0, id == "town" ? facilities : Array.Empty<ScheduledFacilityConfiguration>(),
            needs, settlement: new SettlementConfiguration(population, householdCash, initialProsperity: 70,
                minimumPopulation: population, maximumPopulation: population, quantityScale: Unit));
        var plan = new IndustryBlueprint("plan", extraction ? "lumber_camp" : "bakery",
            extraction ? "harvest_logs" : "bake_bread", 100 * Unit, 2, capacity, new[] { "town", "other" },
            wagePerBatch: 6 * Unit);
        return new RegionalWorldState(content, new[] { Port("town"), Port("other") },
            new[] { new SeaRoute("town", "other", 100) }, new[] { new RegionalShipConfiguration(
                new ShipConfiguration("ship", 10 * Unit, "town"), "owner", "Player", 0, 100, isPlayer: true) },
            initialDay: initialDay, companies: new[] { new CompanyConfiguration("owner", 1000 * Unit),
                new CompanyConfiguration("rival", 1000 * Unit) }, industryBlueprints: new[] { plan },
            initialFacilities: sites, difficulty: difficulty,
            runIdentity: new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 99,
                difficulty, companyStorageRules: CompanyStorageRules.Pooled, economicRulesVersion: 0),
            economy: ExpandingEconomyConfiguration.CreateDefault("town", "other"));
    }
}
