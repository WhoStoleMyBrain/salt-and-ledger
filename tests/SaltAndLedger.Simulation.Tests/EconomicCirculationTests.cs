using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class EconomicCirculationTests
{
    private static int Q(int value) => EconomicUnits.FromWhole(value);
    private static long M(long value) => EconomicUnits.Coins(value);

    [Theory]
    [InlineData("ship", "owner")]
    [InlineData("npc_ship", "npc")]
    public void PlayerAndNpcSalesPayIdenticalFeesFromRealProceedsWithZeroOpeningCash(string ship, string owner)
    {
        var world = World(companyCash: 0);
        var before = world.Snapshot();
        var command = new TradeCommand(TradeDirection.Sell, "logs", Q(1));
        var preview = before.PreviewTrade(ship, "town", command);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(M(10), preview.ExecutedAmount);
        Assert.Equal(M(10) / 50, preview.ServiceFee);
        Assert.Equal(1000, preview.MarketServiceFee);
        Assert.Equal(1000, preview.HouseholdServiceFee);
        Assert.Equal(98000, preview.NetProceeds);
        var result = world.ExecuteTrade(ship, "town", command, before);
        Assert.Equal(JsonConvert.SerializeObject(preview), JsonConvert.SerializeObject(result.Trades.Single().Trade));
        Assert.Equal(98000, result.After.Companies[owner].Cash);
        Assert.Equal(before.Ports["town"].MarketCash - 99000, result.After.Ports["town"].MarketCash);
        Assert.Equal(before.Ports["town"].City!.HouseholdCash + 1000, result.After.Ports["town"].City!.HouseholdCash);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
        var finance = result.After.Accounting.Companies[owner];
        Assert.Equal(2000, finance.FeesPaid);
        Assert.Equal(98000, finance.OperatingSurplusAfterExpenses);
        Assert.Equal(0, finance.WagesPaid);
        Assert.Equal(0, result.After.Ports["town"].City!.TotalWagesReceived);
        Assert.Equal(2, result.CashTransfers.Count);
        Assert.All(result.CashTransfers, t => Assert.Equal(result.After.Revision, t.Revision));
        Assert.Equal(2000, result.EconomicEvents.Where(e => e.Kind == EconomicEventKind.ServiceFee).Sum(e => e.CashTotal));
    }

    [Fact]
    public void NpcAutomationPreviewAndExecutionShareTheNetSettlement()
    {
        var world = World(companyCash: 0, npcAutomation: true);
        var decision = world.EvaluateAutomation("npc_ship");
        Assert.Equal(AutomationAction.Sell, decision.Action);
        Assert.NotNull(decision.Preview);
        Assert.Equal(2000, decision.Preview!.ServiceFee);
        var result = world.RunAutomation("npc_ship", 1);
        Assert.Equal(JsonConvert.SerializeObject(decision.Preview), JsonConvert.SerializeObject(result.Trades.Single().Trade));
        Assert.Equal(98000, result.After.Companies["npc"].Cash);
    }

    [Fact]
    public void WarehouseFractionalOrdersUseOnePrefixAndDoNotLoseFeeRemainders()
    {
        var whole = World(localBid: 7);
        var split = World(localBid: 7);
        whole.ExecuteWarehouseTrade("owner", "factory", new TradeCommand(TradeDirection.Sell, "logs", 10000));
        foreach (var quantity in new[] { 1, 6, 1, 41, 55, 9896 })
            split.ExecuteWarehouseTrade("owner", "factory", new TradeCommand(TradeDirection.Sell, "logs", quantity));
        var left = whole.Snapshot(); var right = split.Snapshot();
        Assert.Equal(left.Companies["owner"].Cash, right.Companies["owner"].Cash);
        Assert.Equal(left.Ports["town"].MarketCash, right.Ports["town"].MarketCash);
        Assert.Equal(left.Ports["town"].City!.HouseholdCash, right.Ports["town"].City!.HouseholdCash);
        Assert.Equal(JsonConvert.SerializeObject(left.Circulation), JsonConvert.SerializeObject(right.Circulation));
        Assert.Equal(1400, right.Accounting.Companies["owner"].FeesPaid);
        Assert.Equal(70000, right.Accounting.Companies["owner"].SalesRevenue);
    }

    [Fact]
    public void FractionalWarehouseAndShipSalesShareTheirCompanyTownPrefix()
    {
        var world = World(localBid: 1);
        var tiny = world.ExecuteWarehouseTrade("owner", "factory", new TradeCommand(TradeDirection.Sell, "logs", 49));
        Assert.Equal(0, tiny.WarehouseTrades.Single().Trade.ServiceFee);
        var full = world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "logs", Q(1)));
        Assert.Equal(200, full.Trades.Single().Trade.ServiceFee);
        var crossing = world.ExecuteWarehouseTrade("owner", "factory", new TradeCommand(TradeDirection.Sell, "logs", 1));
        Assert.Equal(1, crossing.WarehouseTrades.Single().Trade.ServiceFee);
        Assert.Equal(0, crossing.WarehouseTrades.Single().Trade.MarketServiceFee);
        Assert.Equal(1, crossing.WarehouseTrades.Single().Trade.HouseholdServiceFee);
        Assert.Equal(201, world.Snapshot().Accounting.Companies["owner"].FeesPaid);
    }

    [Fact]
    public void DynamicFractionalSplitOrdersPreserveGrossNetFeesAndRecipientsExactly()
    {
        var whole = World(dynamicLogs: true);
        var split = World(dynamicLogs: true);
        var bulk = whole.ExecuteWarehouseTrade("owner", "factory", new TradeCommand(TradeDirection.Sell, "logs", Q(1)));
        long gross = 0, fees = 0;
        foreach (var quantity in new[] { 1, 3332, 6667 })
        {
            var state = split.Snapshot();
            var command = new TradeCommand(TradeDirection.Sell, "logs", quantity);
            var preview = state.PreviewWarehouseTrade("owner", "factory", command);
            var result = split.ExecuteWarehouseTrade("owner", "factory", command, state);
            Assert.Equal(JsonConvert.SerializeObject(preview), JsonConvert.SerializeObject(result.WarehouseTrades.Single().Trade));
            gross += preview.ExecutedAmount; fees += preview.ServiceFee;
        }
        Assert.Equal(bulk.WarehouseTrades.Single().Trade.ExecutedAmount, gross);
        Assert.Equal(bulk.WarehouseTrades.Single().Trade.ServiceFee, fees);
        Assert.Equal(whole.Snapshot().Companies["owner"].Cash, split.Snapshot().Companies["owner"].Cash);
        Assert.Equal(whole.Snapshot().Ports["town"].MarketCash, split.Snapshot().Ports["town"].MarketCash);
        Assert.Equal(whole.Snapshot().Ports["town"].City!.HouseholdCash, split.Snapshot().Ports["town"].City!.HouseholdCash);
        Assert.Equal(JsonConvert.SerializeObject(whole.Snapshot().Circulation), JsonConvert.SerializeObject(split.Snapshot().Circulation));
    }

    [Fact]
    public void GrossAffordabilityRejectionsPreviewAndPurchasesNeverChargeFees()
    {
        var world = World(marketCash: 99999);
        var before = world.Snapshot();
        var rejected = world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "logs", Q(1)));
        Assert.Equal(TradeOutcome.InsufficientMarketFunds, rejected.Trades.Single().Trade.Outcome);
        Assert.Same(before, rejected.After);
        Assert.Empty(rejected.CashTransfers);
        Assert.Equal(0, rejected.After.Accounting.Companies["owner"].FeesPaid);
        var bought = world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Buy, "bread", Q(1)));
        Assert.Equal(TradeOutcome.Traded, bought.Trades.Single().Trade.Outcome);
        Assert.Equal(0, bought.Trades.Single().Trade.ServiceFee);
        Assert.Empty(bought.CashTransfers);
        Assert.Equal(0, bought.After.Circulation!.Commercial.Values.Sum(c => c.GrossLocalSales));
    }

    [Fact]
    public void LossMakingResaleRetainsMaterialBasisAndSeparateServiceExpense()
    {
        var world = World(cargo: 0, publicLogs: Q(2));
        var buy = world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Buy, "logs", Q(1)));
        Assert.Equal(120000, buy.Trades.Single().Trade.ExecutedAmount);
        world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "logs", Q(1)));
        var finance = world.Snapshot().Accounting.Companies["owner"];
        Assert.Equal(120000, finance.CostOfGoodsSold);
        Assert.Equal(-20000, finance.RealizedGrossMargin);
        Assert.Equal(2000, finance.FeesPaid);
        Assert.Equal(-22000, finance.OperatingSurplusAfterExpenses);
        Assert.Equal(0, finance.HeldInventoryCost);
    }

    [Theory]
    [InlineData(CirculationMode.None, 0)]
    [InlineData(CirculationMode.MunicipalOnly, 0)]
    [InlineData(CirculationMode.FeesOnly, 2000)]
    [InlineData(CirculationMode.Combined, 2000)]
    public void DiagnosticModesIsolateTransfersWithoutInventingCash(CirculationMode mode, long expectedFee)
    {
        var world = World(mode: mode);
        var initial = world.Snapshot().TotalCash;
        world.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "logs", Q(1)));
        Assert.Equal(expectedFee, world.Snapshot().Accounting.Companies["owner"].FeesPaid);
        for (var day = 0; day < 8; day++) world.AdvanceDay();
        Assert.Equal(initial, world.Snapshot().TotalCash);
        Assert.Equal(12, world.Snapshot().Circulation!.Towns["town"].NextReviewDay);
    }

    [Fact]
    public void LimitedSurplusLevyFundsARealOtherwiseBlockedPublicBatch()
    {
        var treated = World(marketCash: M(2), householdCash: M(200), bread: 0, publicLogs: Q(2), publicWage: M(12));
        var control = World(marketCash: M(2), householdCash: M(200), bread: 0, publicLogs: Q(2), publicWage: M(12), mode: CirculationMode.None);
        var initial = treated.Snapshot().TotalCash;
        RegionalResult? result = null;
        for (var day = 0; day < 4; day++) { result = treated.AdvanceDay(); control.AdvanceDay(); }
        Assert.Equal(ScheduledProductionOutcome.Produced, result!.Production.Single().Outcome);
        Assert.Equal(ScheduledProductionOutcome.PayrollShortage, control.Snapshot().Ports["town"].Facilities.Single().LastOutcome);
        var town = treated.Snapshot().Circulation!.Towns["town"];
        Assert.Equal(M(10), town.TotalLevy);
        Assert.Equal(0, town.TotalRelief);
        Assert.Equal(M(12), town.DuePayroll);
        Assert.Equal(M(3), town.FoodReserve);
        Assert.Equal(MunicipalReviewReason.PayrollLevy, town.LastReason);
        Assert.Equal(M(12), treated.Snapshot().Ports["town"].City!.TotalWagesReceived);
        Assert.Equal(Q(1), treated.Snapshot().Ports["town"].Inventory["planks"]);
        Assert.Equal(initial, treated.Snapshot().TotalCash);
        Assert.Equal(0, treated.Snapshot().Economy!.MintedCurrency);
        Assert.Equal(0, treated.Snapshot().Economy!.ExternalNetInflow);
    }

    [Fact]
    public void HouseholdsAtTwoFoodReserveCannotBeTaxedToFundPayroll()
    {
        var world = World(marketCash: M(2), householdCash: M(6), bread: 0, publicLogs: Q(2), publicWage: M(12));
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var state = world.Snapshot();
        Assert.Equal(0, state.Circulation!.Towns["town"].TotalLevy);
        Assert.Equal(MunicipalReviewReason.HouseholdFoodReserveProtected, state.Circulation.Towns["town"].LastReason);
        Assert.Equal(ScheduledProductionOutcome.PayrollShortage, state.Ports["town"].Facilities.Single().LastOutcome);
        Assert.Equal(M(6), state.Ports["town"].City!.HouseholdCash);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(20000, 10000, true)]
    [InlineData(20000, 0, false)]
    public void MissingInputsOutputCeilingsAndDisabledWorkDoNotJustifyLevy(int logs, int planks, bool enabled)
    {
        var world = World(marketCash: 0, householdCash: M(200), bread: 0, publicLogs: logs, publicPlanks: planks,
            publicWage: M(12), productionEnabled: enabled);
        for (var day = 0; day < 8; day++) world.AdvanceDay();
        Assert.Equal(0, world.Snapshot().Circulation!.Towns["town"].TotalLevy);
        Assert.Equal(0, world.Snapshot().Ports["town"].City!.TotalWagesReceived);
    }

    [Fact]
    public void PoorHouseholdReliefBuysActualFractionalBreadAndProtectsUpcomingPayroll()
    {
        var world = World(marketCash: M(20), householdCash: 0, bread: Q(1), publicLogs: Q(2), publicWage: M(12), publicDue: 6);
        RegionalResult? result = null;
        for (var day = 0; day < 4; day++) result = world.AdvanceDay();
        var town = world.Snapshot().Circulation!.Towns["town"];
        Assert.Equal(M(12), town.PayrollReserve);
        Assert.Equal(0, town.DuePayroll);
        Assert.Equal(8000, town.TotalRelief); // 10% of eight real coins above reserved payroll.
        Assert.Equal(0, town.TotalLevy);
        var food = result!.Consumption.Single(c => c.PortId == "town");
        Assert.Equal(2666, food.ConsumedQuantity);
        Assert.Equal(7998, food.Payment);
        Assert.True(world.Snapshot().Ports["town"].MarketCash >= M(12));
        world.AdvanceDay();
        Assert.Equal(ScheduledProductionOutcome.Produced, world.AdvanceDay().Production.Single().Outcome);
    }

    [Fact]
    public void NoBreadMeansNoReliefEvenWithWealthyPublicAccount()
    {
        var world = World(marketCash: M(1000), householdCash: 0, bread: 0);
        for (var day = 0; day < 12; day++) world.AdvanceDay();
        Assert.Equal(0, world.Snapshot().Circulation!.Towns["town"].TotalRelief);
        Assert.Equal(MunicipalReviewReason.NoFoodStock, world.Snapshot().Circulation!.Towns["town"].LastReason);
        Assert.Equal(0, world.Snapshot().Ports["town"].Consumption.Single().TotalConsumedQuantity);
    }

    [Fact]
    public void StaticAccountsDoNotOscillateAfterOneFoodReserveIsFunded()
    {
        var world = World(marketCash: M(1000), householdCash: 0, bread: Q(10), foodFirstDue: 1000);
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        var cash = world.Snapshot().Ports["town"].City!.HouseholdCash;
        Assert.Equal(M(3), cash);
        for (var day = 0; day < 36; day++) world.AdvanceDay();
        var town = world.Snapshot().Circulation!.Towns["town"];
        Assert.Equal(M(3), town.TotalRelief);
        Assert.Equal(0, town.TotalLevy);
        Assert.Equal(cash, world.Snapshot().Ports["town"].City!.HouseholdCash);
    }

    [Fact]
    public void ServiceRecipientOverflowAndLaterProductionFailureRollbackEveryStagedEffect()
    {
        var recipientOverflow = World(householdCash: long.MaxValue);
        var before = recipientOverflow.Snapshot();
        Assert.Throws<OverflowException>(() => recipientOverflow.Snapshot().PreviewTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "logs", Q(1))));
        Assert.Throws<OverflowException>(() => recipientOverflow.ExecuteTrade("ship", "town", new TradeCommand(TradeDirection.Sell, "logs", Q(1))));
        Assert.Same(before, recipientOverflow.Snapshot());
        var productionOverflow = World(publicLogs: Q(2), publicPlanks: int.MaxValue, publicWage: 1, publicDue: 1,
            outputLimit: null, npcAutomation: true);
        before = productionOverflow.Snapshot();
        Assert.Throws<OverflowException>(() => productionOverflow.AdvanceDay());
        Assert.Same(before, productionOverflow.Snapshot());
        Assert.Equal(0, before.Accounting.Companies["npc"].FeesPaid);
    }

    [Fact]
    public void CombinedRulesReplayAcrossCulturesAndReverseConfiguration()
    {
        string Run(bool reverse)
        {
            var world = World(npcAutomation: true, marketCash: M(2), householdCash: M(200), publicLogs: Q(2),
                publicWage: M(12), reverse: reverse);
            for (var day = 0; day < 24; day++) world.AdvanceDay();
            return JsonConvert.SerializeObject(world.Snapshot());
        }
        var expected = Run(false);
        var previous = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); Assert.Equal(expected, Run(true)); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void LaterPrivateProductionOverflowRollsBackEarlierLevyPublicWageAndReviewDate()
    {
        var template = World(marketCash: M(2), householdCash: M(200), bread: 0, publicLogs: Q(2), publicWage: M(12)).Snapshot();
        var content = ContentLoader.Load(ContentLoaderTests.Change(root =>
            root["recipes"]!.Single(r => (string?)r["id"] == "bake_bread")["outputs"]![0]!["quantity"] = int.MaxValue)).ForExpandingEconomy();
        var world = new RegionalWorldState(content, template.Ports.Values.Select(p => p.Configuration), template.Routes,
            template.Ships.Values.Select(s => s.Configuration),
            companies: template.Companies.Values.Select(c => new CompanyConfiguration(c.Id, c.Cash)),
            industryBlueprints: new[] { new IndustryBlueprint("overflow_plan", "bakery", "bake_bread", M(10), 4, Q(10), new[] { "town" }) },
            initialFacilities: new[] { new PrivateFacilityConfiguration("overflow_factory", "owner", "town", "overflow_plan",
                new Dictionary<string, int> { ["logs"] = Q(1), ["flour"] = Q(1) }) },
            economy: ExpandingEconomyConfiguration.CreateDefault("town", "other"), circulation: new CirculationConfiguration());
        for (var day = 0; day < 3; day++) world.AdvanceDay();
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Equal(0, before.Circulation!.Towns["town"].TotalLevy);
        Assert.Equal(4, before.Circulation.Towns["town"].NextReviewDay);
        Assert.Equal(0, before.Ports["town"].City!.TotalWagesReceived);
        Assert.Equal(M(2), before.Ports["town"].MarketCash);
    }

    private static RegionalWorldState World(long? marketCash = null, long? householdCash = null, long? companyCash = null,
        int bread = 20000, int cargo = 10000, int publicLogs = 20000, int publicPlanks = 0, long publicWage = 0,
        int publicDue = 4, int? outputLimit = 10000, bool productionEnabled = true, bool npcAutomation = false,
        long foodFirstDue = 4, long localBid = 10, CirculationMode mode = CirculationMode.Combined, bool reverse = false,
        bool dynamicLogs = false)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
        RegionalPortConfiguration Port(string id)
        {
            var scheduled = id == "town" && publicWage > 0 ? new[] { new ScheduledFacilityConfiguration(
                new BuildingInstance("public_sawmill", "sawmill", "saw_planks", productionEnabled: productionEnabled),
                4, outputLimit, publicDue, publicWage) } : Array.Empty<ScheduledFacilityConfiguration>();
            var stock = id == "town" ? new Dictionary<string, int> { ["logs"] = publicLogs, ["planks"] = publicPlanks, ["bread"] = bread }
                : new Dictionary<string, int>();
            return new RegionalPortConfiguration(new PortConfiguration(id,
                content.Goods.Select(g => dynamicLogs && g.Id == "logs"
                    ? new MarketQuote(g.Id, new InventoryPricing(InventoryPricingModel.Linear, 10, Q(4), 3, 1, 30, 1))
                    : new MarketQuote(g.Id, g.Id == "logs" ? localBid : 2, g.Id == "bread" ? 3 : Math.Max(12, localBid))),
                marketCash ?? M(1000), scheduled.Select(f => f.Instance), stock), id, 0, 0, scheduled,
                new[] { new ConsumptionConfiguration("food", "bread", Q(1), 4, foodFirstDue,
                    ConsumptionKind.Essential, 20, true) }, settlement: new SettlementConfiguration(20, householdCash ?? M(1000),
                    minimumPopulation: 10, demographicIntervalDays: 1000, quantityScale: EconomicUnits.QuantityScale));
        }
        var ports = new[] { Port("town"), Port("other") };
        var companies = new[] { new CompanyConfiguration("owner", companyCash ?? M(100)), new CompanyConfiguration("npc", companyCash ?? M(100)) };
        var ships = new[] {
            new RegionalShipConfiguration(new ShipConfiguration("ship", Q(5), "town", new Dictionary<string,int> { ["logs"] = cargo }),
                "owner", "Player", 0, 100, isPlayer: true),
            new RegionalShipConfiguration(new ShipConfiguration("npc_ship", Q(5), "town", new Dictionary<string,int> { ["logs"] = Q(1) }),
                "npc", "NPC", 0, 100, policy: new TradeRoutePolicy("other", "town", new RouteLeg("logs", Q(1), Q(1), 20, 1), allowEmptyReturn: true),
                automationEnabled: npcAutomation)
        };
        return new RegionalWorldState(content, reverse ? ports.Reverse() : ports, new[] { new SeaRoute("town", "other", 100) },
            reverse ? ships.Reverse() : ships, companies: reverse ? companies.Reverse() : companies,
            industryBlueprints: new[] { new IndustryBlueprint("plan", "sawmill", "saw_planks", M(10), 4, Q(10), new[] { "town" }, Q(5)) },
            initialFacilities: new[] { new PrivateFacilityConfiguration("factory", "owner", "town", "plan",
                new Dictionary<string, int> { ["logs"] = Q(1) }, enabled: false) },
            economy: ExpandingEconomyConfiguration.CreateDefault("town", "other"), circulation: new CirculationConfiguration(mode: mode));
    }
}
