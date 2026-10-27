using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class EconomicSignalsTests
{
    private const int Unit = EconomicUnits.QuantityScale;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static string Fingerprint(object value) => JsonConvert.SerializeObject(value);

    [Fact]
    public void NextFoodUsesPersistedFractionalCarryAndActualScheduleWithoutConsumingIt()
    {
        var world = Fixture(population: 10, populationPerUnit: 3, bread: 20 * Unit);
        var first = EconomicSignals.NextEssentialNeed(world.Snapshot(), "town");
        Assert.Equal(4, first.NextDueDay);
        Assert.Equal(33333, first.DesiredQuantity);
        Assert.Equal(0, first.DemandRemainder);
        Assert.Equal(1, first.NextDemandRemainder);
        Assert.False(first.HasObservation);
        Assert.Equal(199998, first.CurrentPayment);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var snapshot = world.Snapshot();
        var serialized = Fingerprint(snapshot);
        var next = EconomicSignals.NextEssentialNeed(snapshot, "town");
        Assert.True(next.HasObservation);
        Assert.Equal(8, next.NextDueDay);
        Assert.Equal(1, next.DemandRemainder);
        Assert.Equal(33333, next.DesiredQuantity);
        Assert.Equal(2, next.NextDemandRemainder);
        Assert.Equal(serialized, Fingerprint(world.Snapshot()));
        Assert.Same(snapshot, world.Snapshot());
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        next = EconomicSignals.NextEssentialNeed(world.Snapshot(), "town");
        Assert.Equal(33334, next.DesiredQuantity);
        Assert.Equal(0, next.NextDemandRemainder);
        Assert.Equal(12, next.NextDueDay);
    }

    [Theory]
    [InlineData(0, 1000000, 30000, 0, 0)]
    [InlineData(20000, 60000, 10000, 10000, 10000)]
    [InlineData(30000, 0, 0, 0, 30000)]
    [InlineData(30000, 180000, 0, 30000, 0)]
    public void CurrentFoodFactsSeparateStockFromAffordabilityAndInitialObservation(int stock, long funds,
        int missingStock, int affordable, int missingFunds)
    {
        var world = Fixture(bread: stock, householdCash: funds);
        var snapshot = world.Snapshot();
        var signal = EconomicSignals.NextEssentialNeed(snapshot, "town");
        Assert.True(signal.Modeled);
        Assert.True(signal.HasNeed);
        Assert.True(signal.PreviewAvailable);
        Assert.False(signal.NumericCapacityBlocked);
        Assert.False(signal.HasObservation);
        Assert.Equal(3 * Unit, signal.DesiredQuantity);
        Assert.Equal(stock, signal.MarketStock);
        Assert.Equal(missingStock, signal.StockShortfall);
        Assert.Equal(affordable, signal.AffordableQuantity);
        Assert.Equal(missingFunds, signal.AffordabilityShortfall);
        var canonical = snapshot.PreviewHouseholdConsumption("town", "food");
        Assert.Equal(canonical.Payment, signal.CurrentPayment);
        Assert.Same(snapshot, world.Snapshot());
    }

    [Fact]
    public void AZeroSubunitOpportunityIsNotAStockOrAffordabilityCrisis()
    {
        var signal = EconomicSignals.NextEssentialNeed(Fixture(population: 1, populationPerUnit: int.MaxValue,
            bread: 0, householdCash: 0).Snapshot(), "town");
        Assert.True(signal.HasNeed);
        Assert.Equal(0, signal.DesiredQuantity);
        Assert.Equal(0, signal.StockShortfall);
        Assert.Equal(0, signal.AffordabilityShortfall);
        Assert.Equal(Unit, signal.NextDemandRemainder);
    }

    [Theory]
    [InlineData(RunWorldMode.ExpandingEconomy, true)]
    public void CurrentSignalsLeaveTheWorldAndSaveUnchanged(RunWorldMode mode, bool living)
    {
        var content = Content();
        var identity = new RunIdentity(null, RunScenario.IndustrialCharter, mode,
            99);
        var world = RunScenario.Create(content, identity);
        var saved = WorldSaveCodec.Save(world);
        var snapshot = world.Snapshot();
        foreach (var port in snapshot.Ports.Values)
        {
            var need = EconomicSignals.NextEssentialNeed(snapshot, port.Id);
            Assert.Equal(living, need.Modeled);
            Assert.Equal(living, need.HasNeed);
            if (!living)
            {
                Assert.Null(need.NextDueDay);
                Assert.Null(need.GoodId);
                Assert.False(need.HasObservation);
                Assert.False(need.PreviewAvailable);
                Assert.False(need.NumericCapacityBlocked);
                Assert.Null(need.AffordableQuantity);
                Assert.Null(need.CurrentPayment);
            }
            foreach (var facility in port.Facilities)
                _ = EconomicSignals.PublicProduction(snapshot, content, port.Id, facility.Configuration.Instance.Id);
        }
        foreach (var facility in snapshot.PrivateFacilities.Values)
        {
            var signal = EconomicSignals.PrivateProduction(snapshot, content, facility.Id);
            Assert.True(signal.UsesSharedStock);
            Assert.Equal(facility.SharedStockpileId, signal.StoreId);
        }
        Assert.Equal(saved, WorldSaveCodec.Save(world));
        Assert.Same(snapshot, world.Snapshot());
    }

    [Theory]
    [InlineData(60000, 10000, null, true, ScheduledProductionOutcome.Produced)]
    [InlineData(59999, 10000, null, true, ScheduledProductionOutcome.PayrollShortage)]
    [InlineData(60000, 9999, null, true, ScheduledProductionOutcome.InputShortage)]
    [InlineData(60000, 10000, 10000, true, ScheduledProductionOutcome.OutputLimit)]
    [InlineData(60000, 10000, null, false, ScheduledProductionOutcome.Disabled)]
    public void PublicReadinessSharesActualProductionAndSeparatesDueFromReadiness(long cash, int flour,
        int? ceiling, bool enabled, ScheduledProductionOutcome expected)
    {
        var world = Fixture(publicBakery: true, marketCash: cash, flour: flour,
            outputLimit: ceiling, enabled: enabled, bread: 0);
        var before = world.Snapshot();
        var signal = EconomicSignals.PublicProduction(before, Content(), "town", "public_bakery");
        Assert.Equal(2, signal.NextEligibleDay);
        Assert.Equal(ScheduledProductionOutcome.NotDue, signal.NextDayOutcome);
        Assert.Equal(expected, signal.CurrentReadiness);
        Assert.Equal(expected == ScheduledProductionOutcome.Produced, signal.ReadyFromCurrentState);
        Assert.Equal(ScheduledProductionOutcome.NotDue, signal.LastOutcome);
        Assert.Equal("market:town", signal.StoreId);
        Assert.Null(signal.OwnerId);
        Assert.Null(signal.StorageCapacity);
        Assert.Equal(2 * Unit, signal.Outputs.Single().Quantity);
        Assert.Equal(flour, signal.Inputs.Single(i => i.GoodId == "flour").CurrentStock);
        world.AdvanceDay();
        var next = EconomicSignals.PublicProduction(world.Snapshot(), Content(), "town", "public_bakery");
        Assert.Equal(expected, next.NextDayOutcome);
        var result = world.AdvanceDay();
        Assert.Equal(expected, result.Production.Single().Outcome);
        Assert.Equal(expected, EconomicSignals.PublicProduction(result.After, Content(), "town", "public_bakery").LastOutcome);
    }

    [Theory]
    [InlineData(60000, 10000, 10000, 400000, null, true, ScheduledProductionOutcome.Produced)]
    [InlineData(59999, 10000, 10000, 400000, null, true, ScheduledProductionOutcome.PayrollShortage)]
    [InlineData(60000, 0, 10000, 400000, null, true, ScheduledProductionOutcome.InputShortage)]
    [InlineData(60000, 10000, 10000, 400000, 10000, true, ScheduledProductionOutcome.OutputLimit)]
    [InlineData(60000, 0, 0, 10000, null, true, ScheduledProductionOutcome.WarehouseCapacity)]
    [InlineData(60000, 10000, 10000, 400000, null, false, ScheduledProductionOutcome.Disabled)]
    public void PrivateReadinessUsesOwnedStorePayrollAndRealConstraints(long cash, int flour, int logs,
        int capacity, int? ceiling, bool enabled, ScheduledProductionOutcome expected)
    {
        var extraction = expected == ScheduledProductionOutcome.WarehouseCapacity;
        var world = Fixture(privateSite: true, companyCash: cash, privateFlour: flour, privateLogs: logs,
            capacity: capacity, outputLimit: ceiling, enabled: enabled, extraction: extraction);
        var snapshot = world.Snapshot();
        var signal = EconomicSignals.PrivateProduction(snapshot, Content(), "private_site");
        Assert.Equal(expected, signal.CurrentReadiness);
        Assert.Equal(ScheduledProductionOutcome.NotDue, signal.NextDayOutcome);
        Assert.Equal(capacity, signal.StorageCapacity);
        Assert.True(signal.IsPrivate);
        Assert.True(signal.UsesSharedStock);
        Assert.Equal("company:owner:town", signal.StoreId);
        Assert.Same(snapshot, world.Snapshot());
        world.AdvanceDay();
        Assert.Equal(expected, EconomicSignals.PrivateProduction(world.Snapshot(), Content(), "private_site").NextDayOutcome);
        Assert.Equal(expected, world.AdvanceDay().PrivateProduction.Single().Outcome);
    }

    [Fact]
    public void PooledReadinessDoesNotReserveScarceInputsOrPromiseBothBatches()
    {
        var world = Fixture(privateSite: true, secondPrivateSite: true, privateFlour: Unit, privateLogs: Unit,
            flour: 20 * Unit);
        var before = world.Snapshot();
        var first = EconomicSignals.PrivateProduction(before, Content(), "private_site");
        var second = EconomicSignals.PrivateProduction(before, Content(), "second_site");
        Assert.True(first.ReadyFromCurrentState);
        Assert.True(second.ReadyFromCurrentState);
        Assert.True(first.UsesSharedStock);
        Assert.Equal(first.StoreId, second.StoreId);
        Assert.Equal(Unit, first.Inputs.Single(i => i.GoodId == "flour").CurrentStock);
        Assert.Equal(20 * Unit, before.Ports["town"].Inventory["flour"]);
        world.AdvanceDay();
        var result = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, result.PrivateProduction.Single(p => p.FacilityId == "private_site").Outcome);
        Assert.Equal(ScheduledProductionOutcome.InputShortage, result.PrivateProduction.Single(p => p.FacilityId == "second_site").Outcome);
        Assert.Equal(ScheduledProductionOutcome.InputShortage,
            EconomicSignals.PrivateProduction(result.After, Content(), "second_site").CurrentReadiness);
    }

    [Fact]
    public void PendingSiteReportsItsActualFirstEligibilityWithoutEarlyProduction()
    {
        var world = Fixture(difficulty: RunDifficulty.SlowFoundations);
        world.ConstructFacility("owner", "town", "plan", "pending");
        var snapshot = world.Snapshot();
        var signal = EconomicSignals.PrivateProduction(snapshot, Content(), "pending");
        Assert.True(signal.UnderConstruction);
        Assert.False(signal.ReadyFromCurrentState);
        Assert.Equal(ScheduledProductionOutcome.UnderConstruction, signal.CurrentReadiness);
        Assert.Equal(4, signal.NextEligibleDay);
        Assert.Equal(snapshot.PrivateFacilities["pending"].FirstProductionDay, signal.NextEligibleDay);
        Assert.Same(snapshot, world.Snapshot());
    }

    [Fact]
    public void PublicNumericHeadroomIsReportedWithoutMutatingOrSuppressingActualOverflow()
    {
        var world = Fixture(publicBakery: true, bread: int.MaxValue, flour: Unit);
        var before = world.Snapshot();
        var signal = EconomicSignals.PublicProduction(before, Content(), "town", "public_bakery");
        Assert.False(signal.HasNumericHeadroom);
        Assert.False(signal.ReadyFromCurrentState);
        Assert.Same(before, world.Snapshot());
        world.AdvanceDay();
        before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void RetailReceiverOverflowExposesPhysicalFactsWithoutInventingPovertyOrPayments()
    {
        var world = Fixture(bread: 3 * Unit, marketCash: long.MaxValue);
        var before = world.Snapshot();
        var signal = EconomicSignals.NextEssentialNeed(before, "town");
        Assert.False(signal.PreviewAvailable);
        Assert.True(signal.NumericCapacityBlocked);
        Assert.Equal(3 * Unit, signal.DesiredQuantity);
        Assert.Equal(3 * Unit, signal.MarketStock);
        Assert.Equal(0, signal.StockShortfall);
        Assert.Equal(4, signal.NextDueDay);
        Assert.Null(signal.AffordableQuantity);
        Assert.Null(signal.AffordabilityShortfall);
        Assert.Null(signal.CurrentPayment);
        Assert.Throws<OverflowException>(() => before.PreviewHouseholdConsumption("town", "food"));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void RepeatedModernInspectionAndExactItineraryWaitLeaveSchemaFiveSaveUnchanged()
    {
        var content = Content();
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 99, companyStorageRules: CompanyStorageRules.Pooled,
            itineraryVersion: 1, economicRulesVersion: 1));
        var ship = world.Snapshot().Ships.Values.Single(s => s.IsPlayer);
        var dock = ship.DockedPortId!;
        var other = world.Snapshot().Ports.Keys.First(p => p != dock);
        world.ConfigureItinerary(ship.Id, new CargoItineraryPolicy("inspection_route", new[]
        {
            new CargoItineraryStop(dock, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket,
                "silver", Unit, shortagePolicy: CargoItineraryShortagePolicy.Wait) }),
            new CargoItineraryStop(other, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket,
                "logs", Unit) })
        }), true);
        var before = world.Snapshot();
        var save = WorldSaveCodec.Save(world);
        var values = new List<string>();
        for (var repeat = 0; repeat < 3; repeat++)
        {
            values.Add(Fingerprint(before.Ports.Values.Select(p => new
            {
                Food = EconomicSignals.NextEssentialNeed(before, p.Id),
                Batches = p.Facilities.Select(f => EconomicSignals.PublicProduction(before, content, p.Id, f.Configuration.Instance.Id)).ToArray()
            }).ToArray()));
            var decision = CargoItineraryEvaluator.Evaluate(before, ship.Id);
            Assert.Equal(CargoItineraryDecisionKind.Wait, decision.Kind);
            Assert.Equal(CargoItineraryBlockReason.SourceStock, decision.BlockReason);
            Assert.Equal(save, WorldSaveCodec.Save(world));
            Assert.Same(before, world.Snapshot());
        }
        Assert.All(values, value => Assert.Equal(values[0], value));
        var restored = WorldSaveCodec.Load(content, save);
        _ = EconomicSignals.NextEssentialNeed(restored.Snapshot(), dock);
        Assert.Equal(save, WorldSaveCodec.Save(restored));
    }

    private static RegionalWorldState Fixture(int bread = 3 * Unit, long householdCash = 1000 * Unit,
        long marketCash = 1000 * Unit, int population = 30, int populationPerUnit = 10,
        bool publicBakery = false, bool privateSite = false, bool secondPrivateSite = false,
        int flour = Unit, long companyCash = 1000 * Unit, int privateFlour = Unit, int privateLogs = Unit,
        int capacity = 40 * Unit, int? outputLimit = null, bool enabled = true, bool extraction = false,
        RunDifficulty difficulty = RunDifficulty.Normal)
    {
        var content = Content();
        var facilities = publicBakery ? new[] { new ScheduledFacilityConfiguration(
            new BuildingInstance("public_bakery", "bakery", "bake_bread", productionEnabled: enabled), 2,
            outputLimit, wagePerBatch: 6 * Unit) } : Array.Empty<ScheduledFacilityConfiguration>();
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id,
            content.Goods.Select(g => new MarketQuote(g.Id, 5, 6)), id == "town" ? marketCash : 1000 * Unit,
            id == "town" ? facilities.Select(f => f.Instance) : Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["bread"] = bread, ["flour"] = flour, ["logs"] = 10 * Unit }),
            id, id == "town" ? 0 : 100, 0, id == "town" ? facilities : Array.Empty<ScheduledFacilityConfiguration>(),
            new[] { new ConsumptionConfiguration("food", "bread", Unit, 4, kind: ConsumptionKind.Essential,
                populationPerUnit: populationPerUnit, proportional: true) }, settlement: new SettlementConfiguration(population,
                householdCash, minimumPopulation: population, maximumPopulation: population, quantityScale: Unit));
        var plan = new IndustryBlueprint("plan", extraction ? "lumber_camp" : "bakery",
            extraction ? "harvest_logs" : "bake_bread", 100 * Unit, 2, capacity, new[] { "town" }, outputLimit, 6 * Unit);
        var sites = new List<PrivateFacilityConfiguration>();
        if (privateSite) sites.Add(new PrivateFacilityConfiguration("private_site", "owner", "town", "plan",
            new Dictionary<string, int> { ["flour"] = privateFlour, ["logs"] = privateLogs }, enabled: enabled));
        if (secondPrivateSite) sites.Add(new PrivateFacilityConfiguration("second_site", "owner", "town", "plan"));
        return new RegionalWorldState(content, new[] { Port("town"), Port("other") },
            new[] { new SeaRoute("town", "other", 100) }, new[] { new RegionalShipConfiguration(
                new ShipConfiguration("ship", 10 * Unit, "town"), "owner", "Player", 0, 100, isPlayer: true) },
            companies: new[] { new CompanyConfiguration("owner", companyCash) }, industryBlueprints: new[] { plan },
            initialFacilities: sites, difficulty: difficulty,
            runIdentity: new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 99,
                difficulty, companyStorageRules: CompanyStorageRules.Pooled, economicRulesVersion: 0),
            economy: ExpandingEconomyConfiguration.CreateDefault("town", "other"));
    }
}
