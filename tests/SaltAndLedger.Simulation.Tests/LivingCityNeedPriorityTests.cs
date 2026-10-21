using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class LivingCityNeedPriorityTests
{
    [Fact]
    public void CommittedFoodPurchasePrecedesEarlierNamedLuxuryAndProtectsTheNextFoodReserve()
    {
        // Three paid meals establish luxury eligibility. On day four only 15 cash remains:
        // food costs 4, two Furniture cost 6, and 5 stays available for the next 4-cash meal.
        // Lexical luxury-first execution would buy all three Furniture and leave only 2.
        var world = World(27, new Dictionary<string, int> { ["bread"] = 20, ["furniture"] = 10 },
            new ConsumptionConfiguration("aaa_luxury", "furniture", 3, 4,
                kind: ConsumptionKind.Luxury, populationPerUnit: 20));
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        var before = world.Snapshot();
        Assert.True(before.Ports["town"].City!.LuxuryEnabled);
        Assert.Equal(15, before.Ports["town"].City!.HouseholdCash);

        var result = world.AdvanceDay();
        Assert.Equal(new[] { "zzz_food", "aaa_luxury" }, result.Consumption.Select(need => need.RuleId));
        var food = result.Consumption[0];
        var luxury = result.Consumption[1];
        Assert.Equal(1, food.ConsumedQuantity);
        Assert.Equal(4, food.Payment);
        Assert.Equal(3, luxury.RequestedQuantity);
        Assert.Equal(2, luxury.ConsumedQuantity);
        Assert.Equal(6, luxury.Payment);
        Assert.Equal(0, luxury.UnmetStock);
        Assert.Equal(1, luxury.UnmetAffordability);
        Assert.Equal(new[] { "bread", "furniture" }, result.CashTransfers.Select(transfer => transfer.GoodId));
        Assert.All(result.CashTransfers, transfer => Assert.Equal(CityCashTransferKind.Retail, transfer.Kind));
        var after = result.After.Ports["town"];
        Assert.Equal(5, after.City!.HouseholdCash);
        Assert.Equal(before.Ports["town"].MarketCash + 10, after.MarketCash);
        Assert.Equal(16, after.Inventory["bread"]);
        Assert.Equal(8, after.Inventory["furniture"]);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
    }

    [Fact]
    public void MunicipalUseConsumesPartialStockWithoutHouseholdFundsOrAnyCashTransfer()
    {
        var world = World(0, new Dictionary<string, int> { ["planks"] = 2 },
            new ConsumptionConfiguration("aaa_municipal", "planks", 3, 1,
                kind: ConsumptionKind.Municipal));
        var before = world.Snapshot();
        var result = world.AdvanceDay();
        Assert.Equal(new[] { "zzz_food", "aaa_municipal" }, result.Consumption.Select(need => need.RuleId));
        var municipal = result.Consumption.Single(need => need.RuleId == "aaa_municipal");
        Assert.Equal(3, municipal.RequestedQuantity);
        Assert.Equal(2, municipal.ConsumedQuantity);
        Assert.Equal(1, municipal.UnmetStock);
        Assert.Equal(0, municipal.UnmetAffordability);
        Assert.Equal(0, municipal.Payment);
        Assert.Equal(0, result.After.Ports["town"].Inventory["planks"]);
        Assert.Empty(result.CashTransfers);
        Assert.Equal(before.Ports["town"].MarketCash, result.After.Ports["town"].MarketCash);
        Assert.Equal(before.Ports["town"].City!.HouseholdCash, result.After.Ports["town"].City!.HouseholdCash);
        Assert.Equal(before.Companies["owner"].Cash, result.After.Companies["owner"].Cash);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
        var need = result.After.Ports["town"].Consumption.Single(rule => rule.Configuration.Id == "aaa_municipal");
        Assert.Equal(2, need.TotalConsumedQuantity);
        Assert.Equal(0, need.TotalPayment);
        Assert.Equal(2, need.NextDueDay);
    }

    private static RegionalWorldState World(long householdCash, IReadOnlyDictionary<string, int> stock,
        ConsumptionConfiguration additionalNeed)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var quotes = content.Goods.Select(good => new MarketQuote(good.Id,
            good.Id == "bread" ? 3 : good.Id == "furniture" ? 2 : 1,
            good.Id == "bread" ? 4 : good.Id == "furniture" ? 3 : 2)).ToArray();
        var port = new RegionalPortConfiguration(new PortConfiguration("town", quotes, 100,
            Array.Empty<BuildingInstance>(), stock), "Town", 0, 0,
            Array.Empty<ScheduledFacilityConfiguration>(), new[] {
                additionalNeed,
                new ConsumptionConfiguration("zzz_food", "bread", 1, 1,
                    kind: ConsumptionKind.Essential, populationPerUnit: 20)
            }, settlement: new SettlementConfiguration(20, householdCash, initialProsperity: 80,
                minimumPopulation: 20, maximumPopulation: 20));
        var other = new RegionalPortConfiguration(new PortConfiguration("other", quotes, 100,
            Array.Empty<BuildingInstance>()), "Other", 100, 0,
            Array.Empty<ScheduledFacilityConfiguration>(), Array.Empty<ConsumptionConfiguration>());
        var ship = new RegionalShipConfiguration(new ShipConfiguration("ship", 10, "town"),
            "owner", "Ship", 50, 100, isPlayer: true);
        return new RegionalWorldState(content, new[] { port, other },
            new[] { new SeaRoute("town", "other", 100) }, new[] { ship });
    }
}
