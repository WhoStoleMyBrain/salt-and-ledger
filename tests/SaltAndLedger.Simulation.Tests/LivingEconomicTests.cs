using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class LivingEconomicTests
{
    [Theory]
    [InlineData(3, 18, 3, 18, 0, 0)]
    [InlineData(2, 7, 1, 6, 1, 1)]
    [InlineData(0, 100, 0, 0, 3, 0)]
    [InlineData(3, 0, 0, 0, 0, 3)]
    public void RetailPaysOnlyForActualMealsAndSeparatesBothShortageCauses(int stock, long cash,
        int consumed, long payment, int stockUnmet, int affordabilityUnmet)
    {
        var world = Fixture(stock, cash);
        var before = world.Snapshot();
        var preview = before.PreviewHouseholdConsumption("town", "food");
        var result = world.AdvanceDay();
        var need = result.After.Ports["town"].Consumption.Single();
        Assert.Equal(3, need.LastDesiredQuantity);
        Assert.Equal(consumed, need.LastConsumedQuantity);
        Assert.Equal(payment, need.LastPayment);
        Assert.Equal(stockUnmet, need.LastUnmetStock);
        Assert.Equal(affordabilityUnmet, need.LastUnmetAffordability);
        Assert.Equal(preview.ConsumedQuantity, consumed);
        Assert.Equal(cash - payment, result.After.Ports["town"].City!.HouseholdCash);
        Assert.Equal(before.Ports["town"].MarketCash + payment, result.After.Ports["town"].MarketCash);
        Assert.Equal(stock - consumed, result.After.Ports["town"].Inventory["bread"]);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
        Assert.Equal(consumed > 0 ? 1 : 0, result.CashTransfers.Count);
    }

    [Fact]
    public void DynamicRetailUsesMarginalSumNotQuantityTimesFirstPrice()
    {
        var quote = new MarketQuote("bread", new InventoryPricing(InventoryPricingModel.Linear, 10, 10, 4, 1, 100, 1));
        var world = Fixture(5, 53, population: 40, breadQuote: quote);
        var preview = world.Snapshot().PreviewHouseholdConsumption("town", "food");
        world.AdvanceDay();
        var need = world.Snapshot().Ports["town"].Consumption.Single();
        Assert.Equal(4, preview.ConsumedQuantity);
        Assert.Equal(53, need.LastPayment); // edges5,4,3,2 cost13+13+13+14.
        Assert.NotEqual(4 * 13, need.LastPayment);
        Assert.Equal(0, world.Snapshot().Ports["town"].City!.HouseholdCash);
    }

    [Fact]
    public void PublicPayrollShortfallProducesNoOutputAndKeepsItsDeadlineDue()
    {
        var world = Fixture(0, 1000, marketCash: 5, publicBakery: true, wage: 6);
        var result = world.AdvanceDay();
        var town = result.After.Ports["town"];
        Assert.Equal(ScheduledProductionOutcome.PayrollShortage, town.Facilities.Single().LastOutcome);
        Assert.Equal(1, town.Facilities.Single().NextDueDay);
        Assert.Equal(10, town.Inventory["flour"]);
        Assert.Equal(10, town.Inventory["logs"]);
        Assert.Equal(0, town.Inventory["bread"]);
        Assert.Equal(0, town.City!.TotalWagesReceived);
        Assert.Empty(result.CashTransfers);
    }

    [Fact]
    public void ActualPublicProductionPaysItsWageBeforeSameDayRetail()
    {
        var world = Fixture(0, 0, marketCash: 6, publicBakery: true, wage: 6, population: 10);
        var result = world.AdvanceDay();
        var town = result.After.Ports["town"];
        Assert.Equal(2, result.CashTransfers.Count);
        Assert.Equal(CityCashTransferKind.Wage, result.CashTransfers[0].Kind);
        Assert.Equal(CityCashTransferKind.Retail, result.CashTransfers[1].Kind);
        Assert.Equal(6, town.City!.TotalWagesReceived);
        Assert.Equal(6, town.City.TotalRetailSpend);
        Assert.Equal(6, town.MarketCash);
        Assert.Equal(0, town.City.HouseholdCash);
        Assert.Equal(1, town.Inventory["bread"]);
        Assert.Equal(result.Before.TotalCash, result.After.TotalCash);
    }

    [Theory]
    [InlineData(5, 0, 5)]
    [InlineData(20, 2, 14)]
    public void PrivatePayrollIsARealPeriodExpenseAndDoesNotFabricateMaterialBasis(long cash,
        int produced, long expectedCash)
    {
        var world = Fixture(0, 0, companyCash: cash, privateBakery: true, wage: 6);
        var result = world.AdvanceDay();
        var finance = result.After.Accounting.Companies["owner"];
        Assert.Equal(expectedCash, result.After.Companies["owner"].Cash);
        Assert.Equal(produced, result.After.PrivateFacilities["private_bakery"].Inventory["bread"]);
        Assert.Equal(produced == 0 ? 0 : 6, finance.WagesPaid);
        Assert.Equal(0, finance.HeldInventoryCost); // Explicit starter grants retain zero material basis.
        Assert.Equal(0, finance.InventoryPurchases);
        Assert.Equal(-finance.WagesPaid, finance.OperatingSurplusAfterWages);
        Assert.Equal(produced == 0 ? ScheduledProductionOutcome.PayrollShortage : ScheduledProductionOutcome.Produced,
            result.PrivateProduction.Single().Outcome);
        Assert.Equal(result.Before.TotalCash, result.After.TotalCash);
    }

    [Fact]
    public void RealRetailMoneyRestoresBothPlayerAndNpcSalePermission()
    {
        var world = Fixture(10, 60, marketCash: 0, population: 20, cargoPlanks: true);
        var before = world.Snapshot();
        foreach (var ship in new[] { "ship", "npc_ship" })
        {
            var rejected = world.ExecuteTrade(ship, "town", new TradeCommand(TradeDirection.Sell, "planks", 1));
            Assert.Equal(TradeOutcome.InsufficientMarketFunds, rejected.Trades.Single().Trade.Outcome);
            Assert.Same(before, world.Snapshot());
        }
        world.AdvanceDay();
        Assert.Equal(12, world.Snapshot().Ports["town"].MarketCash);
        foreach (var ship in new[] { "ship", "npc_ship" })
            Assert.Equal(TradeOutcome.Traded,
                world.ExecuteTrade(ship, "town", new TradeCommand(TradeDirection.Sell, "planks", 1)).Trades.Single().Trade.Outcome);
        Assert.Equal(2, world.Snapshot().Ports["town"].MarketCash);
        Assert.Equal(before.TotalCash, world.Snapshot().TotalCash);
    }

    [Fact]
    public void LateRetailReceiverOverflowRollsBackEarlierWagesProductionMealsAndHistory()
    {
        var world = Fixture(0, 0, marketCash: 6, population: 10, publicBakery: true,
            wage: 6, overflowingOtherTown: true);
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0, world.Snapshot().Day);
        Assert.Equal(0, world.Snapshot().Ports["town"].City!.TotalWagesReceived);
        Assert.Equal(0, world.Snapshot().Ports["town"].City!.TotalRetailSpend);
        Assert.Equal(10, world.Snapshot().Ports["town"].Inventory["flour"]);
        Assert.Empty(world.Snapshot().Ports["town"].City!.FoodWindow);
    }

    [Theory]
    [InlineData(-42, LivingPricingMode.StockOnly)]
    [InlineData(42, LivingPricingMode.Coverage)]
    [InlineData(99, LivingPricingMode.StockOnly)]
    public void GeneratedCitiesRemainFiniteAndAccountedOverAYear(int seed, LivingPricingMode pricing)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.LivingCities, seed, livingPricingMode: pricing));
        var cash = world.Snapshot().TotalCash;
        for (var day = 0; day < 365; day++)
        {
            var result = world.AdvanceDay();
            Assert.Equal(cash, result.After.TotalCash);
            foreach (var port in result.After.Ports.Values)
            {
                var city = port.City!;
                Assert.InRange(city.Population, city.Configuration.MinimumPopulation, city.Configuration.MaximumPopulation);
                Assert.InRange(city.Prosperity, 0, 100);
                Assert.InRange(city.HouseholdCash, 0, long.MaxValue);
                Assert.InRange(city.FoodWindow.Count, 0, CitySnapshot.FoodWindowCapacity);
                Assert.Equal((BigInteger)city.Configuration.InitialHouseholdCash + city.TotalWagesReceived - city.TotalRetailSpend,
                    city.HouseholdCash);
                foreach (var need in port.Consumption)
                    Assert.Equal(need.TotalDesiredQuantity,
                        need.TotalConsumedQuantity + need.TotalUnmetStock + need.TotalUnmetAffordability);
            }
        }
    }

    [Fact]
    public void ForestFurnitureTownActuallyImportsPlanksBeforeExceedingItsInitialInputEndowment()
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var generated = LivingCityGeneration.Generate(42);
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.LivingCities, 42));
        var port = world.Snapshot().Ports[generated.FurniturePortId];
        Assert.Contains(port.Facilities, facility => facility.Configuration.Instance.BuildingId == "lumber_camp");
        Assert.Contains(port.Facilities, facility => facility.Configuration.Instance.BuildingId == "furniture_workshop");
        Assert.DoesNotContain(port.Facilities, facility => facility.Configuration.Instance.BuildingId == "sawmill");
        Assert.DoesNotContain(generated.FurniturePortId, world.Snapshot().IndustryBlueprints[EnterpriseScenario.SawmillBlueprint].EligiblePortIds);
        var initialPlanks = port.Inventory["planks"];
        var imports = 0;
        var batches = 0;
        for (var day = 0; day < 90; day++)
        {
            var result = world.AdvanceDay();
            imports += result.Trades.Where(trade => trade.PortId == generated.FurniturePortId &&
                trade.Trade.GoodId == "planks" && trade.Trade.Direction == TradeDirection.Sell).Sum(trade => trade.Trade.ExecutedQuantity);
            batches += result.Production.Count(production => production.PortId == generated.FurniturePortId &&
                production.RecipeId == "make_furniture" && production.Outcome == ScheduledProductionOutcome.Produced);
        }
        Assert.True(imports > 0);
        var planksPerBatch = content.GetRecipe("make_furniture").Inputs.Single(input => input.GoodId == "planks").Quantity;
        Assert.True(batches * planksPerBatch > initialPlanks);
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility("player", generated.FurniturePortId,
            EnterpriseScenario.SawmillBlueprint, "forbidden_sawmill"));
    }

    private static RegionalWorldState Fixture(int bread, long householdCash, long marketCash = 100,
        int population = 30, MarketQuote? breadQuote = null, bool publicBakery = false,
        bool privateBakery = false, long wage = 0, long companyCash = 100, bool cargoPlanks = false,
        bool overflowingOtherTown = false)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var quotes = content.Goods.Select(good => good.Id == "bread" && breadQuote != null ? breadQuote :
            new MarketQuote(good.Id, 5, 6)).ToArray();
        var facilities = publicBakery ? new[] { new ScheduledFacilityConfiguration(
            new BuildingInstance("public_bakery", "bakery", "bake_bread"), 1, wagePerBatch: wage) }
            : Array.Empty<ScheduledFacilityConfiguration>();
        var port = new RegionalPortConfiguration(new PortConfiguration("town", quotes, marketCash,
            facilities.Select(facility => facility.Instance), new Dictionary<string, int>
            { ["bread"] = bread, ["flour"] = 10, ["logs"] = 10 }), "Town", 0, 0, facilities,
            new[] { new ConsumptionConfiguration("food", "bread", 1, 1,
                kind: ConsumptionKind.Essential, populationPerUnit: 10) },
            settlement: new SettlementConfiguration(population, householdCash, minimumPopulation: population, maximumPopulation: population));
        var other = new RegionalPortConfiguration(new PortConfiguration("zz_other", quotes,
            overflowingOtherTown ? long.MaxValue : 100, Array.Empty<BuildingInstance>(),
            new Dictionary<string, int> { ["bread"] = 1 }), "Other", 100, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            overflowingOtherTown ? new[] { new ConsumptionConfiguration("food", "bread", 1, 1,
                kind: ConsumptionKind.Essential, populationPerUnit: 10) } : Array.Empty<ConsumptionConfiguration>(),
            settlement: overflowingOtherTown ? new SettlementConfiguration(10, 6) : null);
        var cargo = new Dictionary<string, int> { ["planks"] = cargoPlanks ? 1 : 0 };
        var ships = new[]
        {
            new RegionalShipConfiguration(new ShipConfiguration("ship", 20, "town", cargo), "owner", "Player", 0, 100, isPlayer: true),
            new RegionalShipConfiguration(new ShipConfiguration("npc_ship", 20, "town", cargo), "npc", "NPC", 0, 100)
        };
        var blueprint = new IndustryBlueprint("bakery_blueprint", "bakery", "bake_bread", 100, 1, 40,
            new[] { "town" }, wagePerBatch: wage);
        var initial = privateBakery ? new[] { new PrivateFacilityConfiguration("private_bakery", "owner", "town",
            blueprint.Id, new Dictionary<string, int> { ["flour"] = 1, ["logs"] = 1 }) }
            : Array.Empty<PrivateFacilityConfiguration>();
        return new RegionalWorldState(content, new[] { port, other }, new[] { new SeaRoute("town", "zz_other", 100) }, ships,
            companies: new[] { new CompanyConfiguration("owner", companyCash), new CompanyConfiguration("npc", 100) },
            industryBlueprints: new[] { blueprint }, initialFacilities: initial);
    }
}
