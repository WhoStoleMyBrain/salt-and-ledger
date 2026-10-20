using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class RegionalEconomyTests
{
    private const string North = "northport";
    private const string South = "southhaven";
    private const string Ship = "test_ship";

    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    private static ScheduledFacilityConfiguration Camp(int interval = 3, int? limit = null,
        long? firstDue = null, bool enabled = true) =>
        new(new BuildingInstance("camp", "lumber_camp", "harvest_logs", productionEnabled: enabled),
            interval, limit, firstDue);

    private static ScheduledFacilityConfiguration Mill(int interval = 2, long? firstDue = null) =>
        new(new BuildingInstance("mill", "sawmill", "saw_planks", priority: 1),
            interval, firstDueDay: firstDue);

    private static RegionalWorldState World(
        ScheduledFacilityConfiguration[]? facilities = null,
        ConsumptionConfiguration[]? consumption = null,
        int logs = 0, int planks = 0, int cargoLogs = 0, int cargoPlanks = 0,
        TradeRoutePolicy? policy = null, long initialDay = 0, ContentCatalog? content = null)
    {
        facilities ??= Array.Empty<ScheduledFacilityConfiguration>();
        consumption ??= Array.Empty<ConsumptionConfiguration>();
        var quotes = new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 5, 6) };
        var ports = new[]
        {
            new RegionalPortConfiguration(
                new PortConfiguration(North, quotes, 1000, facilities.Select(f => f.Instance),
                    new Dictionary<string, int> { ["logs"] = logs, ["planks"] = planks }),
                "Northport", 0, 0, facilities, consumption),
            new RegionalPortConfiguration(
                new PortConfiguration(South, quotes, 1000, Array.Empty<BuildingInstance>()),
                "Southhaven", 100, 0, Array.Empty<ScheduledFacilityConfiguration>(),
                Array.Empty<ConsumptionConfiguration>())
        };
        var ship = new RegionalShipConfiguration(
            new ShipConfiguration(Ship, 20, North,
                new Dictionary<string, int> { ["logs"] = cargoLogs, ["planks"] = cargoPlanks }),
            "test_owner", "Test ship", 1000, 1, policy: policy, automationEnabled: policy != null);
        return new RegionalWorldState(content ?? Content(), ports,
            new[] { new SeaRoute(North, South, 2) }, new[] { ship }, initialDay);
    }

    private static ScheduledFacilitySnapshot Facility(RegionalWorldState world, string id = "camp") =>
        world.Snapshot().Ports[North].Facilities.Single(f => f.Configuration.Instance.Id == id);

    [Fact]
    public void ExtractionRunsOnlyAtScheduledDaysAndPreservesUnrelatedGoods()
    {
        var world = World(new[] { Camp() }, planks: 3);
        Assert.Equal(3, Facility(world).NextDueDay);
        for (var day = 1; day <= 6; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(day, result.After.Day);
            Assert.Equal(2 * (day / 3), result.After.Ports[North].Inventory["logs"]);
            Assert.Equal(3, result.After.Ports[North].Inventory["planks"]);
            Assert.Equal(day % 3 == 0,
                result.Production.Any(p => p.Outcome == ScheduledProductionOutcome.Produced));
        }
        Assert.Equal(9, Facility(world).NextDueDay);
    }

    [Fact]
    public void FullExtractionBatchWaitsAtCeilingAndRetriesAfterStockLeaves()
    {
        var world = World(new[] { Camp(limit: 18, firstDue: 1) }, logs: 17);
        var blocked = world.AdvanceDay();
        Assert.Equal(17, blocked.After.Ports[North].Inventory["logs"]);
        Assert.Equal(ScheduledProductionOutcome.OutputLimit, Facility(world).LastOutcome);
        Assert.Equal(1, Facility(world).NextDueDay);
        Assert.All(blocked.Production, attempt => Assert.Empty(attempt.Changes));

        world.ExecuteTrade(Ship, North, new TradeCommand(TradeDirection.Buy, "logs", 1));
        Assert.Equal(1, world.Snapshot().Day);
        Assert.Equal(16, world.Snapshot().Ports[North].Inventory["logs"]);
        Assert.Equal(1, Facility(world).NextDueDay);
        var resumed = world.AdvanceDay();
        Assert.Equal(18, resumed.After.Ports[North].Inventory["logs"]);
        Assert.Equal(ScheduledProductionOutcome.Produced, Facility(world).LastOutcome);
        Assert.Equal(5, Facility(world).NextDueDay);
    }

    [Fact]
    public void OutputCeilingChecksEveryOutputWithoutTruncatingOrPartiallyProducing()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
        {
            var extraction = root["recipes"]!.Children<JObject>()
                .Single(recipe => (string?)recipe["id"] == "harvest_logs");
            ((JArray)extraction["outputs"]!).Add(new JObject { ["goodId"] = "planks", ["quantity"] = 1 });
        }));
        var world = World(new[] { Camp(interval: 1, limit: 18) }, logs: 16, planks: 18, content: content);
        var blocked = world.AdvanceDay();
        Assert.Equal(16, blocked.After.Ports[North].Inventory["logs"]);
        Assert.Equal(18, blocked.After.Ports[North].Inventory["planks"]);
        Assert.Equal(ScheduledProductionOutcome.OutputLimit, Facility(world).LastOutcome);
        world.ExecuteTrade(Ship, North, new TradeCommand(TradeDirection.Buy, "planks", 1));
        var resumed = world.AdvanceDay();
        Assert.Equal(18, resumed.After.Ports[North].Inventory["logs"]);
        Assert.Equal(18, resumed.After.Ports[North].Inventory["planks"]);
    }

    [Fact]
    public void ProcessingShortageKeepsWholeBatchAndDeadlineUntilInputsArrive()
    {
        var world = World(new[] { Mill() }, logs: 1, cargoLogs: 1);
        world.AdvanceDay();
        var shortage = world.AdvanceDay();
        Assert.Equal(1, shortage.After.Ports[North].Inventory["logs"]);
        Assert.Equal(0, shortage.After.Ports[North].Inventory["planks"]);
        Assert.Equal(ScheduledProductionOutcome.InputShortage, Facility(world, "mill").LastOutcome);
        Assert.Equal(2, Facility(world, "mill").NextDueDay);
        Assert.All(shortage.Production, attempt => Assert.Empty(attempt.Changes));

        world.ExecuteTrade(Ship, North, new TradeCommand(TradeDirection.Sell, "logs", 1));
        Assert.Equal(2, world.Snapshot().Day);
        Assert.Equal(0, world.Snapshot().Ports[North].Inventory["planks"]);
        var resumed = world.AdvanceDay();
        Assert.Equal(0, resumed.After.Ports[North].Inventory["logs"]);
        Assert.Equal(1, resumed.After.Ports[North].Inventory["planks"]);
        Assert.Equal(5, Facility(world, "mill").NextDueDay);
        world.AdvanceDay();
        Assert.Equal(1, world.Snapshot().Ports[North].Inventory["planks"]);
    }

    [Fact]
    public void ProductionTogglesPreserveDeadlineAndNeverCreateFreeOrCatchupBatches()
    {
        var world = World(new[] { Camp() });
        world.AdvanceDay();
        world.AdvanceDay();
        world.SetProductionEnabled(North, "camp", false);
        world.SetProductionEnabled(North, "camp", true);
        Assert.Equal(2, world.Snapshot().Day);
        Assert.Equal(0, world.Snapshot().Ports[North].Inventory["logs"]);
        Assert.Equal(3, Facility(world).NextDueDay);
        world.AdvanceDay();
        Assert.Equal(2, world.Snapshot().Ports[North].Inventory["logs"]);
        Assert.Equal(6, Facility(world).NextDueDay);

        world.SetProductionEnabled(North, "camp", false);
        for (var day = 4; day <= 8; day++) world.AdvanceDay();
        Assert.Equal(2, world.Snapshot().Ports[North].Inventory["logs"]);
        Assert.Equal(6, Facility(world).NextDueDay);
        Assert.Equal(ScheduledProductionOutcome.Disabled, Facility(world).LastOutcome);
        world.SetProductionEnabled(North, "camp", true);
        Assert.Equal(8, world.Snapshot().Day);
        Assert.Equal(2, world.Snapshot().Ports[North].Inventory["logs"]);
        world.AdvanceDay();
        Assert.Equal(4, world.Snapshot().Ports[North].Inventory["logs"]);
        Assert.Equal(12, Facility(world).NextDueDay);
    }

    [Fact]
    public void ConsumptionIsAllOrNothingAndUnmetOpportunityDoesNotShiftCadence()
    {
        var demand = new ConsumptionConfiguration("construction", "planks", 2, 3);
        var world = World(consumption: new[] { demand }, planks: 1, cargoPlanks: 1);
        Assert.Empty(world.AdvanceDay().Consumption);
        Assert.Empty(world.AdvanceDay().Consumption);
        var unmet = Assert.Single(world.AdvanceDay().Consumption);
        Assert.Equal(2, unmet.RequestedQuantity);
        Assert.Equal(0, unmet.ConsumedQuantity);
        Assert.Equal(2, unmet.UnmetQuantity);
        Assert.Equal(1, world.Snapshot().Ports[North].Inventory["planks"]);
        var schedule = Assert.Single(world.Snapshot().Ports[North].Consumption);
        Assert.Equal(6, schedule.NextDueDay);
        Assert.Equal(2, schedule.LastUnmetQuantity);
        Assert.Equal(2, schedule.TotalUnmetQuantity);

        world.ExecuteTrade(Ship, North, new TradeCommand(TradeDirection.Sell, "planks", 1));
        Assert.Empty(world.AdvanceDay().Consumption);
        Assert.Empty(world.AdvanceDay().Consumption);
        var fulfilled = Assert.Single(world.AdvanceDay().Consumption);
        Assert.Equal(2, fulfilled.ConsumedQuantity);
        Assert.Equal(0, fulfilled.UnmetQuantity);
        Assert.Equal(0, world.Snapshot().Ports[North].Inventory["planks"]);
        schedule = Assert.Single(world.Snapshot().Ports[North].Consumption);
        Assert.Equal(9, schedule.NextDueDay);
        Assert.Equal(0, schedule.LastUnmetQuantity);
        Assert.Equal(2, schedule.TotalUnmetQuantity);
    }

    [Fact]
    public void NewlyProducedGoodsCanSatisfySameDayConsumption()
    {
        var world = World(new[] { Mill(interval: 1), Camp(interval: 1) },
            new[] { new ConsumptionConfiguration("construction", "planks", 1, 1) });
        var result = world.AdvanceDay();
        Assert.Equal(new[] { "camp", "mill" }, result.Production.Select(p => p.FacilityId));
        Assert.All(result.Production, p => Assert.Equal(ScheduledProductionOutcome.Produced, p.Outcome));
        Assert.Equal(1, Assert.Single(result.Consumption).ConsumedQuantity);
        Assert.Equal(0, result.After.Ports[North].Inventory["logs"]);
        Assert.Equal(0, result.After.Ports[North].Inventory["planks"]);
    }

    [Fact]
    public void CompetingConsumptionRulesUseStableIdsAndNeverPartiallyConsume()
    {
        var world = World(consumption: new[]
        {
            new ConsumptionConfiguration("z_last", "planks", 2, 1),
            new ConsumptionConfiguration("a_first", "planks", 2, 1)
        }, planks: 3);
        var result = world.AdvanceDay();
        Assert.Equal(new[] { "a_first", "z_last" }, result.Consumption.Select(c => c.RuleId));
        Assert.Equal(new[] { 2, 0 }, result.Consumption.Select(c => c.ConsumedQuantity));
        Assert.Equal(new[] { 0, 2 }, result.Consumption.Select(c => c.UnmetQuantity));
        Assert.Equal(1, result.After.Ports[North].Inventory["planks"]);
    }

    [Fact]
    public void ProductionOverflowRollsBackEarlierAutomatedTradeDepartureAndAllSchedules()
    {
        var policy = new TradeRoutePolicy(North, South,
            new RouteLeg("logs", 1, 1, 10, 1), allowEmptyReturn: true);
        var world = World(new[] { Mill(interval: 1) }, logs: 4, planks: int.MaxValue, policy: policy);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(4, before.Ports[North].Inventory["logs"]);
        Assert.Equal(0, before.Ships[Ship].CargoUnits);
        Assert.Equal(North, before.Ships[Ship].DockedPortId);
        Assert.Equal(1000, before.Ships[Ship].Cash);
        Assert.Equal(1, Facility(world, "mill").NextDueDay);

        world.SetProductionEnabled(North, "mill", false);
        var retry = world.AdvanceDay();
        Assert.Equal(1, Assert.Single(retry.Trades).Trade.ExecutedQuantity);
        Assert.Equal(1, retry.After.Ships[Ship].Cargo["logs"]);
        Assert.Equal(South, retry.After.Ships[Ship].DestinationPortId);
        Assert.Equal(1, retry.After.Ships[Ship].RemainingTravelDays);
        Assert.Equal(997, retry.After.Ships[Ship].Cash);
    }

    [Fact]
    public void ExtractionDeadlineOverflowRollsBackProducedGoods()
    {
        var world = World(new[] { Camp(interval: 1, firstDue: long.MaxValue) },
            initialDay: long.MaxValue - 1);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0, before.Ports[North].Inventory["logs"]);
    }

    [Fact]
    public void ConsumptionDeadlineOverflowRollsBackRemovedGoods()
    {
        var world = World(consumption: new[]
        {
            new ConsumptionConfiguration("construction", "planks", 1, 2, long.MaxValue)
        }, planks: 2, initialDay: long.MaxValue - 1);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(2, before.Ports[North].Inventory["planks"]);
    }

    [Fact]
    public void SixtyDaysReconcileSourcesTransformationsSinksAndEveryOwnerTransfer()
    {
        var content = Content();
        var world = ArchipelagoScenario.Create(content);
        var initial = world.Snapshot();
        var expected = content.Goods.ToDictionary(g => g.Id, g => TotalGoods(initial, g.Id));
        var initialCash = TotalCash(initial);
        long extracted = 0, processed = 0, consumed = 0, shipped = 0;

        for (var day = 1; day <= 60; day++)
        {
            var result = world.AdvanceDay();
            foreach (var production in result.Production)
            {
                var recipe = content.GetRecipe(production.RecipeId);
                if (production.Outcome != ScheduledProductionOutcome.Produced)
                {
                    Assert.Empty(production.Changes);
                    continue;
                }
                var recipeDelta = content.Goods.ToDictionary(g => g.Id, _ => 0);
                foreach (var input in recipe.Inputs) recipeDelta[input.GoodId] -= input.Quantity;
                foreach (var output in recipe.Outputs) recipeDelta[output.GoodId] += output.Quantity;
                foreach (var good in content.Goods)
                {
                    Assert.Equal(recipeDelta[good.Id],
                        production.Changes.Where(c => c.GoodId == good.Id).Sum(c => c.Delta));
                    expected[good.Id] += recipeDelta[good.Id];
                }
                if (recipe.Kind == RecipeKind.Extraction) extracted += recipe.Outputs.Sum(o => o.Quantity);
                else processed++;
            }
            foreach (var consumption in result.Consumption)
            {
                Assert.True(consumption.ConsumedQuantity == 0 ||
                    consumption.ConsumedQuantity == consumption.RequestedQuantity);
                expected[consumption.GoodId] -= consumption.ConsumedQuantity;
                consumed += consumption.ConsumedQuantity;
            }
            foreach (var trade in result.Trades)
            {
                Assert.Equal(0, trade.Trade.MarketInventoryChange.Delta + trade.Trade.TraderInventoryChange.Delta);
                Assert.Equal(0, trade.Trade.MarketCashChange.Delta + trade.Trade.TraderCashChange.Delta);
                shipped += trade.Trade.ExecutedQuantity;
            }
            Assert.Equal(initialCash, TotalCash(result.After));
            foreach (var good in content.Goods)
                Assert.Equal(expected[good.Id], TotalGoods(result.After, good.Id));
            Assert.All(result.After.Ports.Values, port => Assert.All(port.Inventory.Values, n => Assert.True(n >= 0)));
            Assert.All(result.After.Ships.Values, ship => Assert.InRange(ship.CargoUnits, 0, ship.CargoCapacity));
        }
        Assert.True(extracted > 0);
        Assert.True(processed > 0);
        Assert.True(consumed > 0);
        Assert.True(shipped > 0);
        Assert.Equal(60, world.Snapshot().Day);
    }

    [Fact]
    public void InvalidFacilityAndConsumptionSchedulesAreRejectedAtConfigurationBoundary()
    {
        foreach (var interval in new[] { 0, -1 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Camp(interval));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ConsumptionConfiguration("demand", "planks", 1, interval));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Camp(limit: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Camp(firstDue: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConsumptionConfiguration("demand", "planks", 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConsumptionConfiguration("demand", "planks", 1, 1, firstDueDay: 0));
    }

    private static long TotalGoods(RegionalWorldSnapshot snapshot, string goodId) =>
        snapshot.Ports.Values.Sum(port => (long)port.Inventory[goodId]) +
        snapshot.Ships.Values.Sum(ship => (long)ship.Cargo[goodId]);

    private static BigInteger TotalCash(RegionalWorldSnapshot snapshot) =>
        snapshot.Ports.Values.Aggregate(BigInteger.Zero, (total, port) => total + port.MarketCash) +
        snapshot.Ships.Values.Aggregate(BigInteger.Zero, (total, ship) => total + ship.Cash);
}
