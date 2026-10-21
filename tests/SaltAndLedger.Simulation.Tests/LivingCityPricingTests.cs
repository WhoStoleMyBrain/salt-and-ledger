using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class LivingCityPricingTests
{
    [Fact]
    public void CoverageUsesFiniteExactBaseQuoteBudgetInsteadOfUnfundedDesire()
    {
        var city = World(25).Snapshot().Ports["town"];
        // Two Bread every two days -> eight over the seven-day coverage horizon.
        // Original target shelf is ten. First two marginal asks are eleven each;
        // twenty-five cash funds two units, not eight desired units.
        Assert.Equal(2, city.Quotes["bread"].Pricing!.TargetStock);
        Assert.Equal(1, World(0).Snapshot().Ports["town"].Quotes["bread"].Pricing!.TargetStock);
        Assert.Equal(8, World(1000).Snapshot().Ports["town"].Quotes["bread"].Pricing!.TargetStock);
    }

    [Fact]
    public void StockOnlyRetainsExactConfiguredPricingObjects()
    {
        var port = World(25, LivingPricingMode.StockOnly).Snapshot().Ports["town"];
        Assert.Same(port.Configuration.Market.Quotes["bread"], port.Quotes["bread"]);
        Assert.Equal(10, port.Quotes["bread"].Pricing!.TargetStock);
    }

    [Fact]
    public void InstantTradesCannotRefreshCoverageOrEarnAnImmediateRoundTripProfit()
    {
        var world = World(1000);
        var initial = world.Snapshot();
        var quote = initial.Ports["town"].Quotes["bread"];
        var buy = new TradeCommand(TradeDirection.Buy, "bread", 3);
        var preview = initial.PreviewTrade("ship", "town", buy);
        var purchase = world.ExecuteTrade("ship", "town", buy);
        Assert.Equal(preview.ExecutedAmount, purchase.Trades.Single().Trade.ExecutedAmount);
        Assert.Same(quote, world.Snapshot().Ports["town"].Quotes["bread"]);
        var sale = world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "bread", 3));
        Assert.Same(quote, sale.After.Ports["town"].Quotes["bread"]);
        Assert.Equal(initial.Ports["town"].Inventory["bread"], sale.After.Ports["town"].Inventory["bread"]);
        Assert.True(sale.After.Companies["owner"].Cash <= initial.Companies["owner"].Cash);
        Assert.Equal(initial.TotalCash, sale.After.TotalCash);
    }

    [Fact]
    public void HouseholdCoverageDescribesPhysicalNeedNotGuaranteedPurchases()
    {
        var port = World(0).Snapshot().Ports["town"];
        Assert.Equal(5, LivingCityPricing.HouseholdCoverageDays(port.Configuration, port.City!, "bread", 5));
        Assert.Null(LivingCityPricing.HouseholdCoverageDays(port.Configuration, port.City!, "logs", 100));
        Assert.Equal(2, LivingCityPricing.DesiredQuantity(port.Consumption[0].Configuration, 11));
        Assert.Equal(1, LivingCityPricing.DesiredQuantity(port.Consumption[0].Configuration, 10));
    }

    [Fact]
    public void CoverageTargetRemainsBoundedForLargePopulationAndBudget()
    {
        var world = World(long.MaxValue - 30000, population: 1000);
        Assert.InRange(world.Snapshot().Ports["town"].Quotes["bread"].Pricing!.TargetStock, 1,
            LivingCityPricing.MaximumTargetStock);
    }

    [Fact]
    public void FundedLargerPopulationRaisesCoverageDepthAtTheSamePhysicalStock()
    {
        var small = World(1000, population: 20).Snapshot();
        var large = World(1000, population: 40).Snapshot();
        Assert.Equal(8, small.Ports["town"].Quotes["bread"].Pricing!.TargetStock);
        Assert.Equal(16, large.Ports["town"].Quotes["bread"].Pricing!.TargetStock);
        Assert.Equal(small.Ports["town"].Inventory["bread"], large.Ports["town"].Inventory["bread"]);
        Assert.True(large.GetMarketQuote("town", "bread").Ask > small.GetMarketQuote("town", "bread").Ask);
    }

    [Fact]
    public void CoverageAllocatesItsSingleFiniteBudgetToFoodBeforeEligibleLuxury()
    {
        var world = World(150, luxury: true, shelf: 100);
        for (var day = 0; day < 6; day++) world.AdvanceDay();
        var town = world.Snapshot().Ports["town"];
        Assert.True(town.City!.LuxuryEnabled);
        Assert.Equal(8, town.Quotes["bread"].Pricing!.TargetStock);
        Assert.Equal(4, town.Quotes["furniture"].Pricing!.TargetStock);
        // The luxury rule sorts first by ID. Essential-kind priority still reserves the first
        // 96 planning currency for food; only the remaining budget determines Furniture depth.
        Assert.True(string.CompareOrdinal(town.Configuration.Consumption[0].Id, "food") < 0);
    }

    private static RegionalWorldState World(long householdCash, LivingPricingMode mode = LivingPricingMode.Coverage,
        int population = 20, bool luxury = false, int shelf = 20)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var quotes = content.Goods.Select(good => new MarketQuote(good.Id,
            new InventoryPricing(InventoryPricingModel.Linear, 10, 10, 4, 1, 100, 1))).ToArray();
        var needs = new List<ConsumptionConfiguration> { new("food", "bread", 1, 2,
            kind: ConsumptionKind.Essential, populationPerUnit: 10) };
        if (luxury) needs.Add(new ConsumptionConfiguration("aaa_luxury", "furniture", 1, 2,
            kind: ConsumptionKind.Luxury, populationPerUnit: 10));
        var town = new RegionalPortConfiguration(new PortConfiguration("town", quotes, 10000,
            Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["bread"] = shelf, ["furniture"] = shelf }),
            "Town", 0, 0, Array.Empty<ScheduledFacilityConfiguration>(), needs,
            settlement: new SettlementConfiguration(population, householdCash, initialProsperity: luxury ? 80 : 50,
                maximumPopulation: Math.Max(120, population),
                pricingMode: mode));
        var other = new RegionalPortConfiguration(new PortConfiguration("other", quotes, 10000,
            Array.Empty<BuildingInstance>()), "Other", 100, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            Array.Empty<ConsumptionConfiguration>());
        var ship = new RegionalShipConfiguration(new ShipConfiguration("ship", 20, "town"),
            "owner", "Ship", 1000, 100, isPlayer: true);
        return new RegionalWorldState(content, new[] { town, other }, new[] { new SeaRoute("town", "other", 100) },
            new[] { ship });
    }
}
