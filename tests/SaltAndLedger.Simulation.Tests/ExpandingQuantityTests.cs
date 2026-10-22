using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ExpandingQuantityTests
{
    [Theory]
    [InlineData(10, 5000)]
    [InlineData(15, 7500)]
    [InlineData(20, 10000)]
    [InlineData(21, 10500)]
    [InlineData(30, 15000)]
    public void HouseholdDemandAndPaidPurchaseAreProportional(int population, int expected)
    {
        var port = Port(population, 1000000, 20000);
        var need = port.Consumption.Single();
        var desired = need.PreviewDemand(population);
        var purchase = LivingRetail.Preview(port, CitySnapshot.Initial(port.Settlement!, 0), need,
            port.Market.Quotes, port.Market.InitialQuantities, 0);
        Assert.Equal(expected, desired.Quantity);
        Assert.Equal(0, desired.Remainder);
        Assert.Equal(expected, purchase.RequestedQuantity);
        Assert.Equal(expected, purchase.ConsumedQuantity);
        Assert.Equal(expected * 3L, purchase.Payment);
        Assert.Equal(0, purchase.UnmetStock);
        Assert.Equal(0, purchase.UnmetAffordability);
    }

    [Fact]
    public void NumeratorRemainderIsCarriedExactlyAndPopulationChangesDoNotDiscardIt()
    {
        var need = Need(10000, 3);
        var first = need.PreviewDemand(1);
        var second = need.PreviewDemand(1, first.Remainder);
        var third = need.PreviewDemand(1, second.Remainder);
        Assert.Equal(new[] { 3333, 3333, 3334 }, new[] { first.Quantity, second.Quantity, third.Quantity });
        Assert.Equal(new[] { 1, 2, 0 }, new[] { first.Remainder, second.Remainder, third.Remainder });
        Assert.Equal(10000, first.Quantity + second.Quantity + third.Quantity);
        var grown = need.PreviewDemand(2, first.Remainder);
        Assert.Equal(6667, grown.Quantity);
        Assert.Equal(0, grown.Remainder);
        Assert.Equal(3333, need.PreviewDemand(1).Quantity); // Inspection consumes no residual.
        Assert.Throws<ArgumentOutOfRangeException>(() => need.PreviewDemand(1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => need.PreviewDemand(1, -1));
    }

    [Theory]
    [InlineData(10, 5000)]
    [InlineData(15, 7500)]
    [InlineData(20, 10000)]
    [InlineData(21, 10500)]
    [InlineData(30, 15000)]
    public void RegionalCommitMatchesFractionalPreviewAndConservesEveryAccount(int population, int quantity)
    {
        var world = World(population);
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        var before = world.Snapshot();
        var preview = before.PreviewHouseholdConsumption("town", "food");
        var result = world.AdvanceDay();
        var purchase = result.Consumption.Single(c => c.PortId == "town");
        Assert.Equal(quantity, purchase.ConsumedQuantity);
        Assert.Equal(preview.Payment, purchase.Payment);
        Assert.Equal(20000 - quantity, result.After.Ports["town"].Inventory["bread"]);
        Assert.Equal(before.Ports["town"].MarketCash + quantity * 3L, result.After.Ports["town"].MarketCash);
        Assert.Equal(before.Ports["town"].City!.HouseholdCash - quantity * 3L,
            result.After.Ports["town"].City!.HouseholdCash);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
        Assert.Equal(100, result.After.Ports["town"].City!.EssentialFulfillmentPercent);
        Assert.Equal(quantity, result.After.Ports["town"].City!.FoodWindow.Single().BaselineDesiredQuantity);
    }

    [Fact]
    public void RegionalDemandAndInitialPopulationRemaindersAdvanceOnlyOnDueOpportunities()
    {
        var world = World(1, denominator: 3);
        var quantities = new List<int>();
        for (var day = 1; day <= 12; day++)
        {
            var before = world.Snapshot();
            var previous = before.Ports["town"].Consumption.Single();
            var result = world.AdvanceDay();
            var need = result.After.Ports["town"].Consumption.Single();
            if (day % 4 != 0)
            {
                Assert.Equal(previous.DemandRemainder, need.DemandRemainder);
                Assert.Equal(previous.BaselineDemandRemainder, need.BaselineDemandRemainder);
            }
            else
            {
                var purchase = result.Consumption.Single(c => c.PortId == "town");
                quantities.Add(purchase.ConsumedQuantity);
                Assert.Equal(previous.Configuration.PreviewDemand(1, previous.DemandRemainder).Quantity,
                    purchase.RequestedQuantity);
                Assert.Equal(purchase.RequestedQuantity, need.LastBaselineDesiredQuantity);
                Assert.Equal(need.DemandRemainder, need.BaselineDemandRemainder);
            }
        }
        Assert.Equal(new[] { 3333, 3333, 3334 }, quantities);
        Assert.Equal(10000, world.Snapshot().Ports["town"].Inventory["bread"]);
        Assert.Equal(0, world.Snapshot().Ports["town"].Consumption.Single().DemandRemainder);
    }

    [Fact]
    public void RegionalScaledPublicAndPrivateBatchesPayOnceAndRespectScaledOutputCaps()
    {
        var world = World(20, production: true);
        var before = world.Snapshot();
        var first = world.AdvanceDay();
        var market = first.After.Ports["town"];
        var factory = first.After.PrivateFacilities["factory"];
        Assert.Equal(22500, market.Inventory["logs"]);
        Assert.Equal(10000, market.Inventory["planks"]);
        Assert.Equal(22500, factory.Inventory["logs"]);
        Assert.Equal(10000, factory.Inventory["planks"]);
        Assert.Equal(20000, factory.ConsumedQuantities["logs"]);
        Assert.Equal(10000, factory.ProducedQuantities["planks"]);
        Assert.Equal(50000, factory.WagesPaid);
        Assert.Equal(100000, market.City!.LastWagesReceived);
        Assert.Equal(before.TotalCash, first.After.TotalCash);
        Assert.Equal(10000, first.After.Accounting.Stores["facility:factory"].GetQuantity("planks"));
        var second = world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.OutputLimit, second.Production.Single().Outcome);
        Assert.Equal(ScheduledProductionOutcome.OutputLimit, second.PrivateProduction.Single().Outcome);
        Assert.Equal(0, second.After.Ports["town"].City!.LastWagesReceived);
        Assert.Equal(first.After.Companies["owner"].Cash, second.After.Companies["owner"].Cash);
    }

    [Fact]
    public void RegionalShipOrdersRejectFractionsWhileLocalWarehousePurchasesRetainThem()
    {
        var world = World(20, production: true);
        var before = world.Snapshot();
        Assert.Throws<ArgumentOutOfRangeException>(() => before.PreviewTrade("ship", "town",
            new TradeCommand(TradeDirection.Buy, "bread", 7500)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.ExecuteTrade("ship", "town",
            new TradeCommand(TradeDirection.Buy, "bread", 7500)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.TransferGoods("owner", "ship", "factory",
            TransferDirection.WarehouseToShip, "logs", 7500));
        Assert.Same(before, world.Snapshot());
        var local = world.ExecuteWarehouseTrade("owner", "factory", new TradeCommand(TradeDirection.Buy, "bread", 7500));
        Assert.Equal(7500, local.After.PrivateFacilities["factory"].Inventory["bread"]);
        Assert.Equal(12500, local.After.Ports["town"].Inventory["bread"]);
        Assert.Equal(10000, local.After.MaximumTradeQuantity("ship", "town", TradeDirection.Buy, "bread"));
    }

    [Fact]
    public void FractionalRetailSeparatesStockAndFundsShortfalls()
    {
        var port = Port(15, 6000, 5000);
        var result = LivingRetail.Preview(port, CitySnapshot.Initial(port.Settlement!, 0), port.Consumption.Single(),
            port.Market.Quotes, port.Market.InitialQuantities, 0);
        Assert.Equal(7500, result.RequestedQuantity);
        Assert.Equal(2000, result.ConsumedQuantity);
        Assert.Equal(6000, result.Payment);
        Assert.Equal(2500, result.UnmetStock);
        Assert.Equal(3000, result.UnmetAffordability);
        Assert.Equal(5000, port.Market.InitialQuantities["bread"]);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 0, 0)]
    [InlineData(3, 1, 3)]
    public void SmallestPhysicalQuantumNeverBecomesAFreePurchase(long funds, int consumed, long payment)
    {
        var port = Port(20, funds, 1);
        var result = LivingRetail.Preview(port, CitySnapshot.Initial(port.Settlement!, 0), port.Consumption.Single(),
            port.Market.Quotes, port.Market.InitialQuantities, 0);
        Assert.Equal(consumed, result.ConsumedQuantity);
        Assert.Equal(payment, result.Payment);
    }

    [Fact]
    public void FractionalReceiverOverflowIsExceptionalWithoutMutatingInputs()
    {
        var port = Port(20, 100000, 20000);
        var before = CitySnapshot.Initial(port.Settlement!, 0);
        Assert.Throws<OverflowException>(() => LivingRetail.Preview(port, before, port.Consumption.Single(),
            port.Market.Quotes, port.Market.InitialQuantities, long.MaxValue));
        Assert.Equal(100000, before.HouseholdCash);
        Assert.Equal(20000, port.Market.InitialQuantities["bread"]);
    }

    [Fact]
    public void ExactNeedAndReserveRatiosRoundOnlyAfterCombiningForV3Prosperity()
    {
        WorkingCity City(int scale) => new(CitySnapshot.Initial(new SettlementConfiguration(20, 19,
            initialProsperity: 74, quantityScale: scale), 0));
        var legacy = City(1);
        var expanding = City(EconomicUnits.QuantityScale);
        var observation = new NeedObservation(4, 10000, 9990, 10000, 10, 0, 1);
        legacy.Observe(observation, 1000);
        expanding.Observe(observation, 1000);
        Assert.Equal(99, expanding.Snapshot().EssentialFulfillmentPercent);
        Assert.Equal(1, expanding.Snapshot().PurchasingPowerPercent);
        Assert.Equal(74, legacy.Prosperity); // floor((3*99 + 1)/4) = 74
        Assert.Equal(75, expanding.Prosperity); // floor((3*99.9 + 1.9)/4) = 75
    }

    [Fact]
    public void FoodReserveAndCoverageRespectFractionalNeedWithoutWholeUnitCeiling()
    {
        var port = Port(15, 1000000, 20000, LivingPricingMode.Coverage);
        var city = CitySnapshot.Initial(port.Settlement!, 0);
        Assert.Equal(22500, LivingRetail.FoodBudget(port, 15, port.Market.Quotes, port.Market.InitialQuantities));
        Assert.Equal(10, LivingCityPricing.HouseholdCoverageDays(port, city, "bread", 20000));
        Assert.Equal(15000, LivingCityPricing.Derive(port, city)["bread"].Pricing!.TargetStock);
        var large = Port(1000, long.MaxValue, 20000, LivingPricingMode.Coverage);
        Assert.Equal(EconomicUnits.FromWhole(64), LivingCityPricing.Derive(large,
            CitySnapshot.Initial(large.Settlement!, 0))["bread"].Pricing!.TargetStock);
    }

    [Fact]
    public void CompleteProductionBatchUsesScaledCanonicalInputsAndOutputs()
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var inventory = new Inventory(content, new Dictionary<string, int> { ["logs"] = 22500 },
            EconomicUnits.QuantityScale);
        Assert.True(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.Equal(2500, inventory.GetQuantity("logs"));
        Assert.Equal(10000, inventory.GetQuantity("planks"));
        Assert.False(Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.Equal(2500, inventory.GetQuantity("logs"));
        Assert.True(Production.TryRunBatch(inventory, "lumber_camp", "harvest_logs"));
        Assert.Equal(22500, inventory.GetQuantity("logs"));
    }

    [Fact]
    public void ScaledOutputOrRecipeOverflowPreservesAllInventory()
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var inventory = new Inventory(content, new Dictionary<string, int>
            { ["logs"] = 20000, ["planks"] = int.MaxValue - 9999 }, EconomicUnits.QuantityScale);
        var before = inventory.Snapshot();
        Assert.Throws<OverflowException>(() => Production.TryRunBatch(inventory, "sawmill", "saw_planks"));
        Assert.Equal(before, inventory.Snapshot());
        var enormousRecipe = ContentLoader.Load(ContentLoaderTests.Change(root =>
            root["recipes"]![0]!["inputs"]![0]!["quantity"] = int.MaxValue));
        var impossible = new Inventory(enormousRecipe, quantityScale: EconomicUnits.QuantityScale);
        Assert.Throws<OverflowException>(() => Production.TryRunBatch(impossible, "sawmill", "saw_planks"));
        Assert.All(impossible.Snapshot(), entry => Assert.Equal(0, entry.Value));
    }

    [Fact]
    public void ScalingAndProportionalDemandHaveCheckedPublicBounds()
    {
        Assert.Equal(2147480000, EconomicUnits.FromWhole(214748));
        Assert.Throws<OverflowException>(() => EconomicUnits.FromWhole(214749));
        Assert.Throws<OverflowException>(() => EconomicUnits.Coins(long.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => EconomicUnits.Coins(-1));
        Assert.Throws<OverflowException>(() => Need(int.MaxValue, 1).PreviewDemand(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Inventory(
            ContentLoader.Load(ContentLoaderTests.CanonicalJson()), quantityScale: 100));
        var legacy = new ConsumptionConfiguration("food", "bread", 1, 4,
            kind: ConsumptionKind.Essential, populationPerUnit: 20);
        Assert.Equal(2, legacy.PreviewDemand(21).Quantity);
        Assert.Throws<ArgumentOutOfRangeException>(() => legacy.PreviewDemand(21, 1));
    }

    [Theory]
    [InlineData(InventoryPricingModel.Linear)]
    [InlineData(InventoryPricingModel.Scarcity)]
    public void WideFractionalSplitsAndReversalsUseIdenticalExactEdges(InventoryPricingModel model)
    {
        uint sequence = 0xC0FFEE;
        uint Next() { sequence = unchecked(sequence * 1664525 + 1013904223); return sequence; }
        for (var sample = 0; sample < 1000; sample++)
        {
            var target = 1 + (int)(Next() % 1000000000);
            var stock = 2 + (int)(Next() % 1000000000);
            var quantity = 2 + (int)(Next() % (uint)(stock - 1));
            var firstQuantity = 1 + (int)(Next() % (uint)(quantity - 1));
            var pricing = new InventoryPricing(model, 100, target, Next() % 301, 1, 500, Next() % 50);
            var total = pricing.Quote(TradeDirection.Buy, stock, quantity).TotalAmount;
            var split = pricing.Quote(TradeDirection.Buy, stock, firstQuantity).TotalAmount +
                pricing.Quote(TradeDirection.Buy, stock - firstQuantity, quantity - firstQuantity).TotalAmount;
            var reversal = pricing.Quote(TradeDirection.Sell, stock - quantity, quantity).TotalAmount;
            Assert.Equal(total, split);
            Assert.InRange(reversal, quantity, total);
            Assert.True(pricing.PriceAtEdge(TradeDirection.Buy, stock) >= pricing.PriceAtEdge(TradeDirection.Sell, stock));
            Assert.True(pricing.PriceAtEdge(TradeDirection.Buy, stock - quantity + 1) >=
                pricing.PriceAtEdge(TradeDirection.Buy, stock));
        }
    }

    [Fact]
    public void NumericalExtremesAndCulturesPreserveBoundedExactPricing()
    {
        var simple = new InventoryPricing(InventoryPricingModel.Linear, 1, int.MaxValue, 0, 1, 1, 0);
        Assert.Equal(int.MaxValue, simple.Quote(TradeDirection.Buy, int.MaxValue, int.MaxValue).TotalAmount);
        var huge = new InventoryPricing(InventoryPricingModel.Scarcity, long.MaxValue, int.MaxValue,
            long.MaxValue, 1, long.MaxValue, long.MaxValue);
        Assert.Throws<OverflowException>(() => huge.Quote(TradeDirection.Buy, int.MaxValue, 2));
        var original = CultureInfo.CurrentCulture;
        try
        {
            var pricing = new InventoryPricing(InventoryPricingModel.Scarcity, 37, 15000, 19, 1, 99, 2);
            var expected = pricing.Quote(TradeDirection.Buy, 27500, 7500).TotalAmount;
            foreach (var culture in new[] { "en-US", "de-DE", "tr-TR", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(expected, pricing.Quote(TradeDirection.Buy, 27500, 7500).TotalAmount);
                Assert.Equal(7500, Need(10000, 20).PreviewDemand(15).Quantity);
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    private static ConsumptionConfiguration Need(int quantity, int denominator) => new("food", "bread",
        quantity, 4, kind: ConsumptionKind.Essential, populationPerUnit: denominator, proportional: true);

    private static RegionalWorldState World(int population, int denominator = 20, bool production = false)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
        var instance = new BuildingInstance("public_sawmill", "sawmill", "saw_planks");
        RegionalPortConfiguration Town(string id)
        {
            var facilities = id == "town" && production ? new[] { new ScheduledFacilityConfiguration(instance, 1,
                outputStockLimit: 10000, wagePerBatch: 50000) } : Array.Empty<ScheduledFacilityConfiguration>();
            return new RegionalPortConfiguration(new PortConfiguration(id,
                content.Goods.Select(g => new MarketQuote(g.Id, 2, 3)), 1000000,
                facilities.Select(f => f.Instance), new Dictionary<string, int>
                    { ["bread"] = 20000, ["logs"] = production ? 42500 : 0 }), id, 0, 0, facilities,
                new[] { Need(10000, denominator) }, settlement: new SettlementConfiguration(population, 1000000,
                    minimumPopulation: 1, demographicIntervalDays: 1000, quantityScale: EconomicUnits.QuantityScale));
        }
        return new RegionalWorldState(content, new[] { Town("town"), Town("other") },
            new[] { new SeaRoute("town", "other", 100) }, new[] {
                new RegionalShipConfiguration(new ShipConfiguration("ship", 30000, "town"), "owner", "Ship", 0, 100, isPlayer: true)
            }, companies: new[] { new CompanyConfiguration("owner", 1000000) },
            industryBlueprints: new[] { new IndustryBlueprint("plan", "sawmill", "saw_planks", 100000, 1, 50000,
                new[] { "town" }, outputStockLimit: 10000, wagePerBatch: 50000) },
            initialFacilities: production ? new[] { new PrivateFacilityConfiguration("factory", "owner", "town", "plan",
                new Dictionary<string, int> { ["logs"] = 42500 }) } : Array.Empty<PrivateFacilityConfiguration>(),
            economy: ExpandingEconomyConfiguration.CreateDefault("town", "other"));
    }

    private static RegionalPortConfiguration Port(int population, long cash, int stock,
        LivingPricingMode mode = LivingPricingMode.StockOnly)
    {
        var quote = new MarketQuote("bread", new InventoryPricing(InventoryPricingModel.Linear, 3, 20000, 0, 3, 3, 0));
        return new RegionalPortConfiguration(new PortConfiguration("town", new[] { quote }, 0,
            Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["bread"] = stock }), "Town", 0, 0,
            Array.Empty<ScheduledFacilityConfiguration>(), new[] { Need(10000, 20) }, settlement:
            new SettlementConfiguration(population, cash, minimumPopulation: 1, maximumPopulation: Math.Max(120, population),
                pricingMode: mode, quantityScale: EconomicUnits.QuantityScale));
    }
}
