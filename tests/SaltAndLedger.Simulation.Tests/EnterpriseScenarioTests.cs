using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class EnterpriseScenarioTests
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    [Fact]
    public void LegacyProjectionReusesCanonicalObjectsAndPreservesTwoGoodFixture()
    {
        var content = Content();
        var legacy = LegacyContent.Timber(content);
        Assert.Equal(new[] { "logs", "planks" }, legacy.Goods.Select(g => g.Id));
        Assert.Same(content.GetGood("logs"), legacy.GetGood("logs"));
        Assert.Same(content.GetRecipe("saw_planks"), legacy.GetRecipe("saw_planks"));
        Assert.Same(content.GetBuilding("lumber_camp"), legacy.GetBuilding("lumber_camp"));
        Assert.Equal(6, content.Goods.Count);
    }

    [Fact]
    public void OriginalVersionOneContentWithoutExtractionRemainsAUsableLegacyFixture()
    {
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
        {
            root["goods"] = new JArray(root["goods"]!.Take(2));
            root["buildings"] = new JArray(root["buildings"]!.Take(1));
            root["recipes"] = new JArray(root["recipes"]!.Take(1));
        }));
        var legacy = LegacyContent.Timber(content);
        Assert.Single(legacy.Buildings);
        Assert.Single(legacy.Recipes);
        var world = new WorldState(legacy, "original_port",
            new[] { new BuildingInstance("original_mill", "sawmill", "saw_planks") },
            new Dictionary<string, int> { ["logs"] = 2 });
        Assert.Equal(1, world.Step().After.Inventory["planks"]);
    }

    [Theory]
    [InlineData("harvest_grain", "farm", "", 0, "grain", 4)]
    [InlineData("mill_flour", "mill", "grain", 2, "flour", 1)]
    [InlineData("make_furniture", "furniture_workshop", "planks", 2, "furniture", 1)]
    public void ExpandedCanonicalRecipesExecuteDeclaredFlows(string recipe, string building,
        string input, int consumed, string output, int produced)
    {
        var content = Content();
        var stock = new Inventory(content, consumed == 0 ? null : new Dictionary<string, int> { [input] = consumed });
        Assert.True(Production.TryRunBatch(stock, building, recipe));
        if (consumed > 0) Assert.Equal(0, stock.GetQuantity(input));
        Assert.Equal(produced, stock.GetQuantity(output));
    }

    [Fact]
    public void BakeryRequiresBothInputsAndCompetesWithSawmillForLogs()
    {
        var content = Content();
        var stock = new Inventory(content, new Dictionary<string, int> { ["logs"] = 2, ["flour"] = 1 });
        Assert.True(Production.TryRunBatch(stock, "sawmill", "saw_planks"));
        var before = stock.Snapshot().ToArray();
        Assert.False(Production.TryRunBatch(stock, "bakery", "bake_bread"));
        Assert.Equal(before, stock.Snapshot().ToArray());
        var bakeryFirst = new Inventory(content, new Dictionary<string, int> { ["logs"] = 2, ["flour"] = 1 });
        Assert.True(Production.TryRunBatch(bakeryFirst, "bakery", "bake_bread"));
        Assert.Equal(2, bakeryFirst.GetQuantity("bread"));
        Assert.Equal(0, bakeryFirst.GetQuantity("flour"));
        Assert.False(Production.TryRunBatch(bakeryFirst, "sawmill", "saw_planks"));
    }

    [Fact]
    public void SharedEnterpriseScenarioHasSixGoodsFourIndependentMarketsAndThreePhysicalMerchants()
    {
        var snapshot = EnterpriseScenario.Create(Content()).Snapshot();
        Assert.Equal(4, snapshot.Ports.Count);
        Assert.Equal(4, snapshot.Companies.Count);
        Assert.Equal(4, snapshot.Ships.Count);
        Assert.Equal(3, snapshot.Ships.Values.Count(s => !s.IsPlayer && s.AutomationEnabled));
        Assert.Equal(600, snapshot.Companies[Owner].Cash);
        Assert.Equal(10, snapshot.Ships[Player].CargoCapacity);
        Assert.Null(snapshot.Challenge);
        Assert.Empty(snapshot.PrivateFacilities);
        Assert.Equal(3, snapshot.IndustryBlueprints.Count);
        foreach (var port in snapshot.Ports.Values)
        {
            Assert.Equal(6, port.Inventory.Count);
            Assert.Equal(6, port.Quotes.Count);
        }
        Assert.Equal(6, snapshot.GetRoute(EnterpriseScenario.East, EnterpriseScenario.West)
            .DurationDays(snapshot.Ships[Player].Configuration.Speed));
    }

    [Fact]
    public void NinetyIdleDaysReconcileAllSixGoodsAndKeepPublicChainsOperating()
    {
        var content = Content();
        var world = EnterpriseScenario.Create(content);
        var initialCash = TotalCash(world.Snapshot());
        var batches = new Dictionary<string, int>();
        var delivered = new HashSet<string>();
        for (var day = 0; day < 90; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(initialCash, TotalCash(result.After));
            foreach (var good in content.Goods)
            {
                var flow = result.Production.SelectMany(p => p.Changes).Where(c => c.GoodId == good.Id).Sum(c => (long)c.Delta);
                var sink = result.Consumption.Where(c => c.GoodId == good.Id).Sum(c => (long)c.ConsumedQuantity);
                Assert.Equal(TotalGoods(result.Before, good.Id) + flow - sink, TotalGoods(result.After, good.Id));
            }
            foreach (var p in result.Production.Where(p => p.Outcome == ScheduledProductionOutcome.Produced))
                batches[p.RecipeId] = batches.TryGetValue(p.RecipeId, out var count) ? count + 1 : 1;
            foreach (var t in result.Trades.Where(t => t.Trade.Direction == TradeDirection.Sell && t.Trade.Outcome == TradeOutcome.Traded))
                delivered.Add(t.ShipId);
        }
        Assert.Equal(3, delivered.Count);
        foreach (var recipe in content.Recipes) Assert.True(batches[recipe.Id] > 2, recipe.Id);
        Assert.Equal(600, world.Snapshot().Companies[Owner].Cash);
        Assert.Equal(90, world.Snapshot().Day);
    }

    [Theory]
    [InlineData(EnterpriseScenario.BakeryBlueprint, EnterpriseScenario.West, 30, 705)]
    [InlineData(EnterpriseScenario.WorkshopBlueprint, EnterpriseScenario.South, 16, 356)]
    public void OneInvestmentAndPolicyOperateForNinetyDaysWithoutRestockingClicks(
        string blueprint, string port, int batches, long cash)
    {
        var world = EnterpriseScenario.Create(Content());
        var totalCash = TotalCash(world.Snapshot());
        world.ConstructFacility(Owner, port, blueprint, "private_factory");
        world.ConfigureFacility(Owner, "private_factory", EnterpriseScenario.DefaultIndustryPolicy(blueprint));
        for (var i = 0; i < 90; i++) world.AdvanceDay();
        var state = world.Snapshot();
        Assert.Equal(batches, state.PrivateFacilities["private_factory"].BatchesProduced);
        Assert.Equal(cash, state.Companies[Owner].Cash);
        Assert.Equal(totalCash, TotalCash(state));
        Assert.True(state.PrivateFacilities["private_factory"].ProcurementCost > 0);
        Assert.True(state.PrivateFacilities["private_factory"].SalesRevenue > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MerchantCharterSucceedsUsingOnlyConfiguredPhysicalShipping(bool reverse)
    {
        var world = EnterpriseScenario.Create(Content(), challenge: true, reverseConfiguration: reverse);
        world.SetAutomationEnabled(Player, true);
        for (var i = 0; i < 60 && world.Snapshot().Challenge!.Status == ChallengeStatus.Active; i++)
            world.AdvanceDay();
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(29, world.Snapshot().Day);
        Assert.Equal(24, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(1014, world.Snapshot().Companies[Owner].Cash);
        Assert.Empty(world.Snapshot().PrivateFacilities);
    }

    [Fact]
    public void IndustrialHybridCharterSucceedsWithPrivateBreadPhysicallyShippedToEast()
    {
        var world = EnterpriseScenario.Create(Content(), challenge: true);
        world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, "bakery");
        world.ConfigureFacility(Owner, "bakery", EnterpriseScenario.BakeryPolicy());
        world.Depart(Player, EnterpriseScenario.West);
        var loaded = 0;
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active)
        {
            var state = world.Snapshot();
            var ship = state.Ships[Player];
            if (ship.DockedPortId == EnterpriseScenario.West)
            {
                var quantity = Math.Min(ship.CargoCapacity, state.PrivateFacilities["bakery"].Inventory["bread"]);
                if (quantity >= 4)
                {
                    var transfer = world.TransferGoods(Owner, Player, "bakery",
                        TransferDirection.WarehouseToShip, "bread", quantity);
                    loaded += transfer.Transfers.Single().TransferredQuantity;
                    world.Depart(Player, EnterpriseScenario.East);
                }
            }
            else if (ship.DockedPortId == EnterpriseScenario.East)
            {
                var sale = world.ExecuteTrade(Player, EnterpriseScenario.East,
                    new TradeCommand(TradeDirection.Sell, "bread", ship.Cargo["bread"]));
                Assert.Equal(TradeOutcome.Traded, sale.Trades.Single().Trade.Outcome);
                if (world.Snapshot().Challenge!.Status == ChallengeStatus.Active)
                    world.Depart(Player, EnterpriseScenario.West);
            }
            if (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        }
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(46, world.Snapshot().Day);
        Assert.Equal(28, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(28, loaded);
        Assert.Equal(1123, world.Snapshot().Companies[Owner].Cash);
        Assert.True(world.Snapshot().PrivateFacilities["bakery"].BatchesProduced >= 14);
    }

    [Fact]
    public void IdleCharterFailsAtDeadlineAndNewScenarioResetsEveryObjectiveAndAsset()
    {
        var world = EnterpriseScenario.Create(Content(), challenge: true);
        for (var i = 0; i < 60; i++) world.AdvanceDay();
        Assert.Equal(ChallengeStatus.Failed, world.Snapshot().Challenge!.Status);
        Assert.Equal(0, world.Snapshot().Challenge!.DeliveredQuantity);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        var reset = EnterpriseScenario.Create(Content(), challenge: true).Snapshot();
        Assert.Equal(0, reset.Day);
        Assert.Equal(0, reset.Revision);
        Assert.Equal(600, reset.Companies[Owner].Cash);
        Assert.Empty(reset.PrivateFacilities);
        Assert.Equal(ChallengeStatus.Active, reset.Challenge!.Status);
        Assert.Equal(0, reset.Challenge.NetDeliveredQuantity);
    }

    [Fact]
    public void ExpandedWorldIsDeterministicUnderReversedConfigurationAndCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            var first = EnterpriseScenario.Create(Content());
            var second = EnterpriseScenario.Create(Content(), reverseConfiguration: true);
            first.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, "bakery");
            second.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, "bakery");
            first.ConfigureFacility(Owner, "bakery", EnterpriseScenario.BakeryPolicy(true));
            second.ConfigureFacility(Owner, "bakery", EnterpriseScenario.BakeryPolicy(true));
            for (var i = 0; i < 90; i++)
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                var a = Fingerprint(first.AdvanceDay().After);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.Equal(a, Fingerprint(second.AdvanceDay().After));
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static string Fingerprint(RegionalWorldSnapshot s) => JsonConvert.SerializeObject(new
    {
        s.Day, s.Revision,
        Companies = s.Companies.Values.Select(c => new { c.Id, c.Cash }),
        Ports = s.Ports.Values.Select(p => new
        {
            p.Id, p.Inventory, p.MarketCash,
            Facilities = p.Facilities.Select(f => new { f.NextDueDay, f.Enabled, f.LastOutcome }),
            Demand = p.Consumption.Select(c => new { c.NextDueDay, c.TotalUnmetQuantity })
        }),
        Ships = s.Ships.Values.Select(ship => new { ship.Id, ship.Cargo, ship.DockedPortId, ship.DestinationPortId, ship.RemainingTravelDays }),
        Private = s.PrivateFacilities.Values.Select(f => new { f.Id, f.Inventory, f.NextProductionDay, f.BatchesProduced, f.ProcurementCost, f.SalesRevenue })
    });

    private static BigInteger TotalCash(RegionalWorldSnapshot s) =>
        s.Companies.Values.Aggregate(BigInteger.Zero, (sum, c) => sum + c.Cash) +
        s.Ports.Values.Aggregate(BigInteger.Zero, (sum, p) => sum + p.MarketCash);
    private static long TotalGoods(RegionalWorldSnapshot s, string good) =>
        s.Ports.Values.Sum(p => (long)p.Inventory[good]) + s.Ships.Values.Sum(ship => (long)ship.Cargo[good]) +
        s.PrivateFacilities.Values.Sum(f => (long)f.Inventory[good]);
}
