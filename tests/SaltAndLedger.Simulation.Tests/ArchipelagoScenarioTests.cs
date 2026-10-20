using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ArchipelagoScenarioTests
{
    private static ContentCatalog Content() => LegacyContent.Timber(ContentLoader.Load(ContentLoaderTests.CanonicalJson()));

    [Fact]
    public void SharedScenarioHasOnePlayerTwoIndependentMerchantsAndDistinctRoutes()
    {
        var snapshot = ArchipelagoScenario.Create(Content()).Snapshot();
        Assert.Equal(4, snapshot.Ports.Count);
        Assert.Equal(3, snapshot.Ships.Count);
        Assert.Single(snapshot.Ships.Values, ship => ship.IsPlayer);
        Assert.Equal(3, snapshot.Ships.Values.Select(ship => ship.OwnerId).Distinct().Count());
        Assert.Equal(2, snapshot.DockedShips(ArchipelagoScenario.North).Count);
        Assert.Equal(3, snapshot.TravelDays(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.South));
        Assert.Equal(2, snapshot.TravelDays(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.West));
        Assert.Equal(4, snapshot.TravelDays(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.East));
        Assert.Equal(6, snapshot.GetRoute(ArchipelagoScenario.East, ArchipelagoScenario.West).DurationDays(100));
        Assert.Equal(4, snapshot.GetRoute(ArchipelagoScenario.East, ArchipelagoScenario.West).DurationDays(160));
    }

    [Fact]
    public void SixtyIdleDaysReconcileSourcesTransformationsSinksAndRepeatedPhysicalDeliveries()
    {
        var content = Content();
        var world = ArchipelagoScenario.Create(content);
        var initial = world.Snapshot();
        var cash = Cash(initial);
        long extraction = 0, logsUsed = 0, planksMade = 0, consumed = 0, unmet = 0;
        var deliveries = initial.Ships.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var transported = initial.Ships.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        for (var day = 1; day <= 60; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(day, result.After.Day);
            Assert.Equal(cash, Cash(result.After));
            foreach (var good in content.Goods)
            {
                var delta = result.Production.Sum(p => p.Changes.Where(change => change.GoodId == good.Id).Sum(change => (long)change.Delta));
                var sink = result.Consumption.Where(c => c.GoodId == good.Id).Sum(c => (long)c.ConsumedQuantity);
                Assert.Equal(Goods(result.Before, good.Id) + delta - sink, Goods(result.After, good.Id));
            }
            foreach (var production in result.Production.Where(p => p.Outcome == ScheduledProductionOutcome.Produced))
            {
                if (content.GetRecipe(production.RecipeId).Kind == RecipeKind.Extraction)
                    extraction += production.Changes.Where(change => change.GoodId == "logs").Sum(change => change.Delta);
                else
                {
                    logsUsed -= production.Changes.Where(change => change.GoodId == "logs").Sum(change => change.Delta);
                    planksMade += production.Changes.Where(change => change.GoodId == "planks").Sum(change => change.Delta);
                }
            }
            consumed += result.Consumption.Sum(c => c.ConsumedQuantity);
            unmet += result.Consumption.Sum(c => c.UnmetQuantity);
            foreach (var trade in result.Trades.Where(t => t.Trade.Direction == TradeDirection.Sell && t.Trade.Outcome == TradeOutcome.Traded))
            {
                transported[trade.ShipId] += trade.Trade.ExecutedQuantity;
                if (trade.Trade.TraderInventoryChange.After == 0) deliveries[trade.ShipId]++;
                Assert.Equal(-trade.Trade.MarketCashChange.Delta, trade.Trade.TraderCashChange.Delta);
            }
        }
        Assert.True(extraction > 20);
        Assert.True(planksMade > 10);
        Assert.True(consumed > 10);
        Assert.True(unmet > 0); // Physical demand is not silently filled.
        Assert.Equal(Goods(initial, "logs") + extraction - logsUsed, Goods(world.Snapshot(), "logs"));
        Assert.Equal(Goods(initial, "planks") + planksMade - consumed, Goods(world.Snapshot(), "planks"));
        Assert.True(deliveries[ArchipelagoScenario.TimberShipId] >= 2);
        Assert.True(deliveries[ArchipelagoScenario.PlankShipId] >= 2);
        Assert.True(transported[ArchipelagoScenario.TimberShipId] >= 10);
        Assert.True(transported[ArchipelagoScenario.PlankShipId] >= 6);
        Assert.Equal(0, transported[ArchipelagoScenario.PlayerShipId]);
        Assert.Equal(initial.Ships[ArchipelagoScenario.PlayerShipId].Cash,
            world.Snapshot().Ships[ArchipelagoScenario.PlayerShipId].Cash);
    }

    [Fact]
    public void ReversingAllScenarioConfigurationOrderPreservesEveryDailyStateAndResult()
    {
        var content = Content();
        var forward = ArchipelagoScenario.Create(content);
        var reverse = ArchipelagoScenario.Create(content, true);
        forward.ConfigureAutomation(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.PlayerPolicy(), true);
        reverse.ConfigureAutomation(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.PlayerPolicy(), true);
        for (var day = 0; day < 60; day++)
        {
            var a = forward.AdvanceDay();
            var b = reverse.AdvanceDay();
            Assert.Equal(Describe(a), Describe(b));
        }
    }

    [Fact]
    public void PlayerCompetitionChangesSharedSupplyAndNpcDeliveriesWithoutChangingTotalCash()
    {
        var content = Content();
        var idle = ArchipelagoScenario.Create(content);
        var competitive = ArchipelagoScenario.Create(content);
        competitive.ConfigureAutomation(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.PlayerPolicy(), true);
        var idleNpcUnits = 0;
        var competingNpcUnits = 0;
        for (var day = 0; day < 60; day++)
        {
            idleNpcUnits += SoldByNpcs(idle.AdvanceDay());
            competingNpcUnits += SoldByNpcs(competitive.AdvanceDay());
        }
        Assert.True(competingNpcUnits < idleNpcUnits);
        Assert.True(competitive.Snapshot().Ships[ArchipelagoScenario.PlayerShipId].Cash > 200);
        Assert.Equal(Cash(idle.Snapshot()), Cash(competitive.Snapshot()));
        Assert.NotEqual(Goods(idle.Snapshot(), "planks"), Goods(competitive.Snapshot(), "planks"));
    }

    [Fact]
    public void RestrictivePolicyCannotInventADealDespiteSixtyProductionDays()
    {
        var world = ArchipelagoScenario.Create(Content());
        world.ConfigureAutomation(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.PlayerPolicy(true), true);
        for (var day = 0; day < 60; day++)
        {
            var result = world.AdvanceDay();
            Assert.DoesNotContain(result.Trades, trade => trade.ShipId == ArchipelagoScenario.PlayerShipId);
        }
        var player = world.Snapshot().Ships[ArchipelagoScenario.PlayerShipId];
        Assert.Equal(200, player.Cash);
        Assert.Equal(0, player.CargoUnits);
        Assert.Equal(ArchipelagoScenario.North, player.DockedPortId);
    }

    [Fact]
    public void ExtendedFiniteRunKeepsAccountingBoundedWithoutSubsidies()
    {
        var world = ArchipelagoScenario.Create(Content());
        var initialCash = Cash(world.Snapshot());
        for (var day = 0; day < 600; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(initialCash, Cash(result.After));
            Assert.All(result.After.Ports.Values, port =>
            {
                Assert.True(port.MarketCash >= 0);
                Assert.All(port.Inventory.Values, quantity => Assert.True(quantity >= 0));
            });
            Assert.All(result.After.Ships.Values, ship =>
            {
                Assert.True(ship.Cash >= 0);
                Assert.InRange(ship.CargoUnits, 0, ship.CargoCapacity);
            });
        }
        Assert.Equal(600, world.Snapshot().Day);
    }

    private static int SoldByNpcs(RegionalResult result) => result.Trades
        .Where(t => t.ShipId != ArchipelagoScenario.PlayerShipId && t.Trade.Direction == TradeDirection.Sell)
        .Sum(t => t.Trade.ExecutedQuantity);

    private static BigInteger Cash(RegionalWorldSnapshot snapshot) =>
        snapshot.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.MarketCash) +
        snapshot.Ships.Values.Aggregate(BigInteger.Zero, (sum, s) => sum + s.Cash);

    private static long Goods(RegionalWorldSnapshot snapshot, string good) =>
        snapshot.Ports.Values.Sum(p => (long)p.Inventory[good]) + snapshot.Ships.Values.Sum(s => (long)s.Cargo[good]);

    private static string Describe(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.After.Day, result.After.Revision,
        Ports = result.After.Ports.Values.Select(p => new
        {
            p.Id, p.Inventory, p.MarketCash, p.Facilities, p.Consumption,
            Quotes = p.Quotes.Keys.Select(good => result.After.GetMarketQuote(p.Id, good)).ToArray()
        }),
        Ships = result.After.Ships.Values.Select(s => new
        {
            s.Id, s.Cash, s.Cargo, s.DockedPortId, s.OriginPortId, s.DestinationPortId,
            s.RemainingTravelDays, s.VoyageDurationDays
        }),
        result.Trades, result.Production, result.Consumption, result.Movements,
        Decisions = result.Decisions.Select(d => new { d.ShipId, d.Stage, d.Action, d.Reason, d.SelectedQuantity })
    });
}
