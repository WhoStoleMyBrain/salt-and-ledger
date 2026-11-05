using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class SparseNavigationTests
{
    private const string Ship = "navigator";
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    [Fact]
    public void PathMinimizesPhysicalDaysBeforeDistanceAndIsEnumerationIndependent()
    {
        var routes = new[]
        {
            new SeaRoute("a", "b", 101), new SeaRoute("b", "d", 101),
            new SeaRoute("a", "c", 150), new SeaRoute("c", "d", 150),
            new SeaRoute("a", "d", 299)
        };
        // The shorter two-leg route takes four days because each real sea lane rounds up.
        var path = SeaNavigation.FindPath(routes, "a", "d", 100);
        Assert.Equal(new[] { "a", "d" }, path.PortIds);
        Assert.Equal(3, path.DurationDays);
        Assert.Equal(299, path.Distance);
        Assert.Equal(path.PortIds, SeaNavigation.FindPath(routes.Reverse(), "a", "d", 100).PortIds);
        var faster = SeaNavigation.FindPath(routes, "a", "d", 110);
        Assert.Equal(new[] { "a", "b", "d" }, faster.PortIds);
        Assert.Equal(2, faster.DurationDays);
    }

    [Fact]
    public void EqualRoutesUseOrdinalFullPathRegardlessOfInputDirectionAndOrder()
    {
        var routes = new[]
        {
            new SeaRoute("a", "c", 100), new SeaRoute("c", "d", 100),
            new SeaRoute("a", "b", 100), new SeaRoute("b", "d", 100)
        };
        var first = SeaNavigation.FindPath(routes, "a", "d", 100);
        var reversed = routes.Reverse().Select(r => new SeaRoute(r.DestinationPortId, r.OriginPortId, r.Distance));
        Assert.Equal(new[] { "a", "b", "d" }, first.PortIds);
        Assert.Equal(first.PortIds, SeaNavigation.FindPath(reversed, "a", "d", 100).PortIds);
        Assert.Equal(new[] { "d", "b", "a" }, SeaNavigation.FindPath(routes, "d", "a", 100).PortIds);
    }

    [Fact]
    public void DisconnectedAndImpossibleDurationPathsFailWithoutInventingLanes()
    {
        var disconnected = new[] { new SeaRoute("a", "b", 1), new SeaRoute("c", "d", 1) };
        Assert.Throws<InvalidOperationException>(() => SeaNavigation.FindPath(disconnected, "a", "d", 1));
        Assert.Throws<InvalidOperationException>(() => SeaNavigation.FindPath(disconnected, "a", "a", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SeaNavigation.FindPath(disconnected, "a", "b", 0));
        var huge = new[] { new SeaRoute("a", "b", int.MaxValue), new SeaRoute("b", "c", int.MaxValue) };
        Assert.Throws<OverflowException>(() => SeaNavigation.FindPath(huge, "a", "c", 1));
        Assert.Equal(2L * int.MaxValue, SeaNavigation.FindPath(huge, "a", "c", int.MaxValue).Distance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualAndMerchantVoyagesKeepCargoThroughIntermediatePorts(bool automate)
    {
        var world = SmallWorld(automate);
        var initial = world.Snapshot();
        if (automate) world.RunAutomation(Ship);
        else
        {
            world.ExecuteTrade(Ship, "a", new TradeCommand(TradeDirection.Buy, "logs", 2));
            world.Depart(Ship, "c");
        }
        Assert.Equal(4, world.Snapshot().Ships[Ship].RemainingTravelDays);
        Assert.Throws<InvalidOperationException>(() => world.Snapshot().GetRoute("a", "c"));
        for (var day = 1; day <= 4; day++)
        {
            var result = world.AdvanceDay();
            Assert.Empty(result.Trades);
            Assert.Equal(2, result.After.Ships[Ship].Cargo["logs"]);
            Assert.Equal(initial.TotalCash, result.After.TotalCash);
            Assert.Equal(initial.Ports["b"].Inventory, result.After.Ports["b"].Inventory);
            Assert.Empty(result.After.DockedShips("b"));
            Assert.Equal(day < 4 ? null : "c", result.After.Ships[Ship].DockedPortId);
            Assert.Equal(30, result.After.Ports.Values.Sum(p => p.Inventory["logs"]) + result.After.Ships[Ship].Cargo["logs"]);
        }
        var sale = automate ? world.RunAutomation(Ship) :
            world.ExecuteTrade(Ship, "c", new TradeCommand(TradeDirection.Sell, "logs", 2));
        Assert.Equal(2, sale.Trades.Single().Trade.ExecutedQuantity);
        Assert.Equal("c", sale.Trades.Single().PortId);
        Assert.Equal(initial.TotalCash, sale.After.TotalCash);
    }

    [Fact]
    public void GeneratedItineraryAndSaveContinuationDoNotCreditTransitDocks()
    {
        var content = Content();
        var draft = GrandCommissionDraft.Generate(42);
        var identity = new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter,
            seed: 42, commission: draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId).ToArray()));
        var world = RunScenario.Create(content, identity);
        var player = WorldIds.PlayerShipId;
        var snapshot = world.Snapshot();
        var path = snapshot.Ports.Keys.SelectMany(from => snapshot.Ports.Keys.Where(to => to != from)
                .Select(to => snapshot.GetPath(from, to, snapshot.Ships[player].Configuration.Speed)))
            .First(p => p.Legs.Count > 1);
        Sail(world, player, path.PortIds[0]);
        var start = world.Snapshot();
        var originalVisits = start.CommissionHistory!.Ships.Single(s => s.ShipId == player).Visits.ToArray();
        var good = start.Ports[path.PortIds[0]].Inventory.First(p => p.Value >= EconomicUnits.QuantityScale).Key;
        var policy = new CargoItineraryPolicy("sparse_journey", new[]
        {
            new CargoItineraryStop(path.PortIds[0], new[] { new CargoItineraryCommand(
                CargoItineraryCommandKind.BuyFromMarket, good, EconomicUnits.QuantityScale) }),
            new CargoItineraryStop(path.PortIds.Last(), new[] { new CargoItineraryCommand(
                CargoItineraryCommandKind.SellToMarket, good, EconomicUnits.QuantityScale) })
        });
        world.ConfigureItinerary(player, policy, true);
        world.RunAutomation(player);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(content, saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        var carried = world.Snapshot().Ships[player].Cargo.ToArray();
        while (world.Snapshot().Ships[player].DockedPortId == null)
        {
            var result = world.AdvanceDay();
            restored.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
            Assert.Equal(carried, result.After.Ships[player].Cargo.ToArray());
            Assert.DoesNotContain(result.Trades, trade => trade.ShipId == player);
            if (result.After.Ships[player].DockedPortId == null)
                Assert.Equal(originalVisits, result.After.CommissionHistory!.Ships.Single(s => s.ShipId == player).Visits.ToArray());
        }
        var visits = world.Snapshot().CommissionHistory!.Ships.Single(s => s.ShipId == player).Visits;
        foreach (var transit in path.PortIds.Skip(1).Take(path.PortIds.Count - 2))
            Assert.Equal(originalVisits.Any(v => v.Key == transit), visits.ContainsKey(transit));
        Assert.Contains(path.PortIds.Last(), visits.Keys);
        var sale = world.RunAutomation(player);
        restored.RunAutomation(player);
        Assert.Contains(sale.Trades, trade => trade.ShipId == player && trade.PortId == path.PortIds.Last());
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
    }

    private static void Sail(RegionalWorldState world, string ship, string destination)
    {
        if (world.Snapshot().Ships[ship].DockedPortId == destination) return;
        world.Depart(ship, destination);
        while (world.Snapshot().Ships[ship].DockedPortId == null) world.AdvanceDay();
    }

    private static RegionalWorldState SmallWorld(bool automate)
    {
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id,
            new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 4, 5) }, 1000,
            Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["logs"] = 10 }), id, 0, 0,
            Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
        var policy = new TradeRoutePolicy("a", "c", new RouteLeg("logs", 2, 1, 10, 1), allowEmptyReturn: true);
        return new RegionalWorldState(Content(), new[] { Port("a"), Port("b"), Port("c") },
            new[] { new SeaRoute("a", "b", 101), new SeaRoute("b", "c", 101) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration(Ship, 4, "a"), "merchant", "Navigator", 100,
                100, policy: policy, automationEnabled: automate) });
    }
}
