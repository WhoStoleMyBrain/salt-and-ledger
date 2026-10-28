using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SaltAndLedger.Simulation.Tests;

public sealed class CargoItineraryTests
{
    private const string Owner = "owner", Ship = "ship", North = "north", South = "south", Site = "store";
    private const int Unit = EconomicUnits.QuantityScale;
    private readonly ITestOutputHelper output;
    public CargoItineraryTests(ITestOutputHelper output) => this.output = output;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson()).ForExpandingEconomy();
    private static Dictionary<string, int> Stock(params (string Id, int Quantity)[] values) => values.ToDictionary(v => v.Id, v => v.Quantity);
    private static CargoItineraryCommand Buy(string good, int units = 1, CargoItineraryShortagePolicy shortage = CargoItineraryShortagePolicy.Skip,
        int minimum = 1, long limit = 0, long reserve = 0, long expenditure = long.MaxValue) =>
        new(CargoItineraryCommandKind.BuyFromMarket, good, units * Unit, minimum * Unit, shortage, limit, reserve, expenditure);
    private static CargoItineraryCommand Sell(string good, int units = 1, CargoItineraryShortagePolicy shortage = CargoItineraryShortagePolicy.Skip,
        long limit = 0) => new(CargoItineraryCommandKind.SellToMarket, good, units * Unit, shortagePolicy: shortage, marginalPriceLimit: limit);
    private static CargoItineraryCommand Load(string good, int units = 1, int reserve = 0,
        CargoItineraryShortagePolicy shortage = CargoItineraryShortagePolicy.Skip) =>
        new(CargoItineraryCommandKind.LoadFromCompanyTownStock, good, units * Unit, shortagePolicy: shortage, warehouseReserve: reserve);
    private static CargoItineraryCommand Unload(string good, int units = 1) =>
        new(CargoItineraryCommandKind.UnloadToCompanyTownStock, good, units * Unit);
    private static CargoItineraryPolicy Policy(params CargoItineraryCommand[] commands) => new("test_itinerary", new[]
    {
        new CargoItineraryStop(North, commands), new CargoItineraryStop(South, new[] { Sell("grain", 20) })
    });

    private static RegionalWorldState World(int capacity = 10, long cash = 1000 * Unit, long marketCash = 1000 * Unit,
        IReadOnlyDictionary<string, int>? stock = null, IReadOnlyDictionary<string, int>? cargo = null,
        IReadOnlyDictionary<string, int>? warehouse = null, int warehouseCapacity = 30,
        MarketQuote? grainQuote = null, bool overflowProduction = false, long revision = 0,
        int shipCount = 1, bool reverse = false, int itineraryVersion = 1)
    {
        var content = Content();
        RegionalPortConfiguration Port(string id)
        {
            var buildings = overflowProduction && id == South
                ? new[] { new BuildingInstance("overflow_camp", "lumber_camp", "harvest_logs") }
                : Array.Empty<BuildingInstance>();
            var inventory = content.Goods.ToDictionary(g => g.Id, g => g.Id == "silver" ? 0 : 100 * Unit);
            if (stock != null) foreach (var pair in stock) inventory[pair.Key] = pair.Value;
            if (overflowProduction && id == South) inventory["logs"] = int.MaxValue;
            return new RegionalPortConfiguration(new PortConfiguration(id,
                content.Goods.Select(g => g.Id == "grain" && grainQuote != null ? grainQuote : new MarketQuote(g.Id, 2, 3)),
                marketCash, buildings, inventory), id, id == North ? 0 : 100, 0,
                buildings.Select(b => new ScheduledFacilityConfiguration(b, 1)),
                new[] { new ConsumptionConfiguration("food", "bread", Unit, 1000, kind: ConsumptionKind.Essential,
                    populationPerUnit: 20, proportional: true) },
                settlement: new SettlementConfiguration(20, 500 * Unit, demographicIntervalDays: 1000, quantityScale: Unit));
        }
        var ships = Enumerable.Range(0, shipCount).Select(index => new RegionalShipConfiguration(
            new ShipConfiguration(index == 0 ? Ship : Ship + "_" + (index + 1), capacity * Unit, North, cargo),
            Owner, "Player " + index, 0, 100, isPlayer: true)).ToArray();
        return new RegionalWorldState(content, new[] { Port(North), Port(South) }, new[] { new SeaRoute(North, South, 100) },
            reverse ? ships.Reverse() : ships, initialRevision: revision,
            companies: new[] { new CompanyConfiguration(Owner, cash) },
            industryBlueprints: new[] { new IndustryBlueprint("plan", "sawmill", "saw_planks", 10 * Unit, 1,
                warehouseCapacity * Unit, new[] { North, South }) },
            initialFacilities: warehouse == null ? null : new[] { new PrivateFacilityConfiguration(Site, Owner, North,
                "plan", warehouse, enabled: false) },
            runIdentity: new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 99,
                companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: itineraryVersion, economicRulesVersion: 0),
            economy: ExpandingEconomyConfiguration.CreateDefault(South, North));
    }

    private static void Configure(RegionalWorldState world, CargoItineraryPolicy policy, string ship = Ship) =>
        world.ConfigureItinerary(ship, policy, true);
    private static CargoItineraryDecision Decision(RegionalWorldState world, string ship = Ship) =>
        CargoItineraryEvaluator.Evaluate(world.Snapshot(), ship);

    [Fact]
    public void OrderedCommandsUseIndependentExactPriceAndExpenditureCaps()
    {
        var curve = new MarketQuote("grain", new InventoryPricing(InventoryPricingModel.Linear, 20, 10 * Unit, 10, 1, 100, 1));
        var world = World(cash: 100 * Unit, stock: Stock(("grain", 10 * Unit)), grainQuote: curve);
        Configure(world, Policy(Buy("grain", 4, limit: 25, expenditure: 50 * Unit),
            Buy("logs", 4, reserve: 70 * Unit), Sell("silver", shortage: CargoItineraryShortagePolicy.Wait)));
        var before = world.Snapshot();
        var first = Decision(world);
        Assert.Equal(CargoItineraryDecisionKind.Execute, first.Kind);
        Assert.InRange(first.SelectedQuantity, Unit, 4 * Unit);
        Assert.Equal(0, first.SelectedQuantity % Unit);
        var expected = before.PreviewTrade(Ship, North, new TradeCommand(TradeDirection.Buy, "grain", first.SelectedQuantity));
        Assert.Equal(expected.ExecutedAmount, first.Preview!.ExecutedAmount);
        Assert.True(expected.ExecutedAmount <= 50 * Unit);
        Assert.True(expected.Price!.LastMarginalPrice <= 25);
        var result = world.RunAutomation(Ship);
        Assert.Equal(expected.ExecutedAmount, result.Trades[0].Trade.ExecutedAmount);
        Assert.True(result.After.Companies[Owner].Cash >= 70 * Unit || result.Trades.Count == 1);
        Assert.Equal(first.SelectedQuantity, result.After.Ships[Ship].Cargo["grain"]);
        Assert.True(result.After.Ships[Ship].Cargo["logs"] <= 4 * Unit);
        Assert.Equal(2, result.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(CargoItineraryDecisionKind.Wait, Decision(world).Kind);
        Audit(result);
    }

    [Fact]
    public void CompletedOrdersNeverRepeatWhenAThirdOrderWaits()
    {
        var world = World();
        Configure(world, Policy(Buy("grain", 2), Buy("logs", 3), Buy("silver", shortage: CargoItineraryShortagePolicy.Wait)));
        var result = world.RunAutomation(Ship);
        Assert.Equal(2, result.Trades.Count);
        Assert.Equal((2 * Unit, 3 * Unit), (result.After.Ships[Ship].Cargo["grain"], result.After.Ships[Ship].Cargo["logs"]));
        Assert.Equal(2, result.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        var cash = result.After.Companies[Owner].Cash;
        var purchases = result.After.Accounting.Companies[Owner].InventoryPurchases;
        for (var click = 0; click < 20; click++)
        {
            var waiting = world.RunAutomation(Ship);
            Assert.Empty(waiting.Trades);
            Assert.Empty(waiting.Transfers);
            Assert.Equal(2, waiting.After.Ships[Ship].ItineraryProgress!.ActionIndex);
            Assert.Equal(cash, waiting.After.Companies[Owner].Cash);
            Assert.Equal(purchases, waiting.After.Accounting.Companies[Owner].InventoryPurchases);
        }
        world.AdvanceDay();
        Assert.Equal(2, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(purchases, world.Snapshot().Accounting.Companies[Owner].InventoryPurchases);
    }

    [Theory]
    [InlineData(CargoItineraryShortagePolicy.Skip, true)]
    [InlineData(CargoItineraryShortagePolicy.Wait, false)]
    public void UnavailableSecondGoodEitherSkipsAndDepartsOrWaitsAtTheExactCommand(CargoItineraryShortagePolicy shortage, bool departs)
    {
        var world = World();
        Configure(world, Policy(Buy("grain", 2), Buy("silver", shortage: shortage)));
        var result = world.RunAutomation(Ship);
        Assert.Single(result.Trades);
        Assert.Equal(2 * Unit, result.After.Ships[Ship].Cargo["grain"]);
        Assert.Equal(departs ? South : null, result.After.Ships[Ship].DestinationPortId);
        Assert.Equal(departs ? null : North, result.After.Ships[Ship].DockedPortId);
        Assert.Equal(6 * Unit, result.After.Accounting.Companies[Owner].InventoryPurchases);
        Assert.Equal(departs ? 0 : 1, result.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        Audit(result);
    }

    [Theory]
    [InlineData(29999, 0, CargoItineraryBlockReason.CompanyFunds)]
    [InlineData(100000, 70001, CargoItineraryBlockReason.CashReserve)]
    public void UnfundedMinimumWaitsWithoutSpendingOrAdvancing(long cash, long reserve, CargoItineraryBlockReason reason)
    {
        var world = World(cash: cash);
        Configure(world, Policy(Buy("grain", 2, shortage: CargoItineraryShortagePolicy.Wait, reserve: reserve)));
        Assert.Equal(reason, Decision(world).BlockReason);
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Trades);
        Assert.Equal(0, result.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(cash, result.After.Companies[Owner].Cash);
    }

    [Theory]
    [InlineData(2, long.MaxValue, CargoItineraryBlockReason.PriceLimit)]
    [InlineData(0, 29999, CargoItineraryBlockReason.ExpenditureLimit)]
    public void PriceAndExpenditureLimitsExplainWhyWholeMinimumCannotBeBought(long price, long expenditure,
        CargoItineraryBlockReason reason)
    {
        var world = World();
        Configure(world, Policy(Buy("grain", 2, shortage: CargoItineraryShortagePolicy.Wait,
            limit: price, expenditure: expenditure)));
        Assert.Equal(reason, Decision(world).BlockReason);
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Trades);
        Assert.Equal(0, result.After.Accounting.Companies[Owner].InventoryPurchases);
    }

    [Fact]
    public void MixedCargoRetainsUnmentionedGoodsAndAFullHoldSkipsOnlyTheRequestedLoad()
    {
        var world = World(capacity: 3, cargo: Stock(("bread", Unit), ("flour", Unit), ("logs", Unit)));
        Configure(world, Policy(Buy("grain"), Sell("bread")));
        Assert.Equal(CargoItineraryBlockReason.CargoFull, Decision(world).BlockReason);
        var result = world.RunAutomation(Ship);
        Assert.Single(result.Trades);
        Assert.Equal(TradeDirection.Sell, result.Trades[0].Trade.Direction);
        Assert.Equal(0, result.After.Ships[Ship].Cargo["grain"]);
        Assert.Equal((Unit, Unit), (result.After.Ships[Ship].Cargo["flour"], result.After.Ships[Ship].Cargo["logs"]));
        Assert.Equal(South, result.After.Ships[Ship].DestinationPortId);
        Assert.Equal(0, result.After.Accounting.Companies[Owner].InventoryPurchases);
        Audit(result);
    }

    [Fact]
    public void MinimumRejectsPartialCapacityAndSkipDoesNotFillCargo()
    {
        var world = World(capacity: 3, cargo: Stock(("logs", 2 * Unit)));
        Configure(world, Policy(Buy("grain", 2, minimum: 2)));
        Assert.Equal(CargoItineraryBlockReason.CargoFull, Decision(world).BlockReason);
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Trades);
        Assert.Equal(2 * Unit, result.After.Ships[Ship].CargoUnits);
        Assert.Equal(South, result.After.Ships[Ship].DestinationPortId);
    }

    [Fact]
    public void FiniteMarketFundsPreventSaleWithoutLosingMixedCargo()
    {
        var world = World(marketCash: Unit, cargo: Stock(("bread", Unit), ("flour", Unit), ("logs", Unit)));
        Configure(world, Policy(Sell("bread", shortage: CargoItineraryShortagePolicy.Wait)));
        Assert.Equal(CargoItineraryBlockReason.MarketFunds, Decision(world).BlockReason);
        var before = world.Snapshot();
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Trades);
        Assert.Equal(before.Companies[Owner].Cash, result.After.Companies[Owner].Cash);
        Assert.Equal(3 * Unit, result.After.Ships[Ship].CargoUnits);
        Assert.Equal(North, result.After.Ships[Ship].DockedPortId);
    }

    [Fact]
    public void WarehouseReserveAndMixedTransfersPreserveExactPaidFifoBasis()
    {
        var world = World(warehouse: Stock());
        world.ExecuteWarehouseTrade(Owner, Site, new TradeCommand(TradeDirection.Buy, "flour", 27500));
        world.ExecuteWarehouseTrade(Owner, Site, new TradeCommand(TradeDirection.Buy, "logs", 22500));
        Configure(world, Policy(Load("flour", 3, 7500), Load("logs", 2, 2500), Unload("flour"),
            Buy("silver", shortage: CargoItineraryShortagePolicy.Wait)));
        var before = world.Snapshot();
        var result = world.RunAutomation(Ship);
        Assert.Equal(3, result.Transfers.Count);
        Assert.Empty(result.Trades);
        Assert.Equal((Unit, 2 * Unit), (result.After.Ships[Ship].Cargo["flour"], result.After.Ships[Ship].Cargo["logs"]));
        Assert.Equal((17500, 2500), (result.After.GetFacilityStorage(Site).Inventory["flour"], result.After.GetFacilityStorage(Site).Inventory["logs"]));
        Assert.Equal(before.Companies[Owner].Cash, result.After.Companies[Owner].Cash);
        Assert.Equal(before.CompanyValue(Owner).Total, result.After.CompanyValue(Owner).Total);
        Assert.Equal(before.Accounting.Companies[Owner].HeldInventoryCost, result.After.Accounting.Companies[Owner].HeldInventoryCost);
        Assert.Equal(9 * Unit, result.After.Accounting.Stores["ship:" + Ship].TotalCostBasis);
        Audit(result);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    public void WarehouseCapacityEitherAllowsOnePartialOrderOrWaitsForItsWholeMinimum(int minimum, int moved)
    {
        var world = World(warehouse: Stock(("logs", 29 * Unit)), cargo: Stock(("flour", 2 * Unit)));
        Configure(world, Policy(new CargoItineraryCommand(CargoItineraryCommandKind.UnloadToCompanyTownStock,
            "flour", 2 * Unit, minimum * Unit, CargoItineraryShortagePolicy.Wait),
            Buy("silver", shortage: CargoItineraryShortagePolicy.Wait)));
        var before = world.Snapshot();
        var result = world.RunAutomation(Ship);
        Assert.Equal(moved * Unit, result.Transfers.Sum(t => t.TransferredQuantity));
        Assert.Equal((2 - moved) * Unit, result.After.Ships[Ship].Cargo["flour"]);
        Assert.Equal((29 + moved) * Unit, result.After.GetFacilityStorage(Site).StoredUnits);
        Assert.Equal(moved, result.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(before.Companies[Owner].Cash, result.After.Companies[Owner].Cash);
        for (var click = 0; click < 3; click++) Assert.Empty(world.RunAutomation(Ship).Transfers);
        Audit(result);
    }

    [Fact]
    public void ReceivingAccountHeadroomIsARejectionRatherThanOverflowOrFreeGoods()
    {
        var world = World(marketCash: long.MaxValue);
        Configure(world, Policy(Buy("grain", shortage: CargoItineraryShortagePolicy.Wait)));
        Assert.Equal(CargoItineraryBlockReason.AccountCapacity, Decision(world).BlockReason);
        var before = world.Snapshot();
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Trades);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
        Assert.Equal(before.Companies[Owner].Cash, result.After.Companies[Owner].Cash);
        Assert.Equal(0, result.After.Ships[Ship].CargoUnits);
    }

    [Fact]
    public void OrderedMarketAndWarehouseOperationsMatchTheSameManualEconomicCommands()
    {
        var automated = World(warehouse: Stock());
        var manual = World(warehouse: Stock());
        foreach (var world in new[] { automated, manual })
        {
            world.ExecuteWarehouseTrade(Owner, Site, new TradeCommand(TradeDirection.Buy, "flour", 2 * Unit));
            world.ExecuteWarehouseTrade(Owner, Site, new TradeCommand(TradeDirection.Buy, "logs", 2 * Unit));
        }
        Configure(automated, Policy(Load("flour", 2), Load("logs", 2), Unload("flour"), Sell("logs"),
            Buy("silver", shortage: CargoItineraryShortagePolicy.Wait)));
        automated.RunAutomation(Ship);
        manual.TransferGoods(Owner, Ship, Site, TransferDirection.WarehouseToShip, "flour", 2 * Unit);
        manual.TransferGoods(Owner, Ship, Site, TransferDirection.WarehouseToShip, "logs", 2 * Unit);
        manual.TransferGoods(Owner, Ship, Site, TransferDirection.ShipToWarehouse, "flour", Unit);
        manual.ExecuteTrade(Ship, North, new TradeCommand(TradeDirection.Sell, "logs", Unit));
        string Economy(RegionalWorldSnapshot state) => JsonConvert.SerializeObject(new
        {
            state.Day, state.Ports, state.Companies, state.CompanyStockpiles, state.Accounting,
            Ships = state.Ships.Values.Select(s => s.Ship), state.PrivateFacilities, state.Economy
        });
        Assert.Equal(Economy(manual.Snapshot()), Economy(automated.Snapshot()));
    }

    [Fact]
    public void EightOperationBudgetSurvivesRepeatedDocksideRequestsUntilNextDay()
    {
        var world = World(capacity: 20);
        Configure(world, Policy(Enumerable.Range(0, 8).Select(_ => Buy("grain")).ToArray()));
        for (var click = 0; click < 20; click++) world.RunAutomation(Ship);
        var after = world.Snapshot();
        Assert.Equal(8 * Unit, after.Ships[Ship].Cargo["grain"]);
        Assert.Equal(8, after.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(8, after.Ships[Ship].ItineraryProgress!.OperationsUsed);
        Assert.Equal(North, after.Ships[Ship].DockedPortId);
        Assert.Equal(24 * Unit, after.Accounting.Companies[Owner].InventoryPurchases);
        Assert.Equal(CargoItineraryBlockReason.DailyLimit, Decision(world).BlockReason);
        world.AdvanceDay();
        world.RunAutomation(Ship);
        Assert.Equal(24 * Unit, world.Snapshot().Accounting.Companies[Owner].InventoryPurchases);
        Assert.NotEqual(North, world.Snapshot().Ships[Ship].DockedPortId);
    }

    [Fact]
    public void SkipsConsumeBoundedBudgetWithoutCreatingMoneyOrPhantomGoods()
    {
        var world = World();
        Configure(world, Policy(Enumerable.Range(0, 8).Select(_ => Buy("silver")).ToArray()));
        var before = world.Snapshot();
        for (var click = 0; click < 20; click++) world.RunAutomation(Ship);
        var after = world.Snapshot();
        Assert.Equal(8, after.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(8, after.Ships[Ship].ItineraryProgress!.OperationsUsed);
        Assert.Equal(0, after.Ships[Ship].CargoUnits);
        Assert.Equal(before.Companies[Owner].Cash, after.Companies[Owner].Cash);
        Assert.Equal(before.Day, after.Day);
        Assert.Equal(North, after.Ships[Ship].DockedPortId);
    }

    [Fact]
    public void SafePolicyReplacementCannotResetSameDayExecutionBudget()
    {
        var world = World(capacity: 20);
        Configure(world, Policy(Enumerable.Range(0, 8).Select(_ => Buy("grain")).ToArray()));
        for (var click = 0; click < 3; click++) world.RunAutomation(Ship);
        world.SetAutomationEnabled(Ship, false);
        Assert.Throws<InvalidOperationException>(() => world.ConfigureItinerary(Ship, Policy(Buy("logs")), true));
        var replaced = world.Snapshot();
        Assert.Equal(8, replaced.Ships[Ship].ItineraryProgress!.OperationsUsed);
        world.SetAutomationEnabled(Ship, true);
        Assert.Equal(CargoItineraryBlockReason.DailyLimit, Decision(world).BlockReason);
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Trades);
        Assert.Equal(8 * Unit, result.After.Ships[Ship].Cargo["grain"]);
        Assert.Equal(0, result.After.Ships[Ship].Cargo["logs"]);
    }

    [Fact]
    public void ThreeIndependentShipsKeepStableOrderWhenOneWaits()
    {
        string Run(bool reverse)
        {
            var world = World(shipCount: 3, reverse: reverse, cash: 9 * Unit);
            Configure(world, Policy(Buy("silver", shortage: CargoItineraryShortagePolicy.Wait)));
            Configure(world, Policy(Buy("grain", 2)), Ship + "_2");
            Configure(world, Policy(Buy("logs", 2)), Ship + "_3");
            var result = world.AdvanceDay();
            Assert.Equal(North, result.After.Ships[Ship].DockedPortId);
            Assert.Equal(0, result.After.Ships[Ship].CargoUnits);
            Assert.Equal(2 * Unit, result.After.Ships[Ship + "_2"].Cargo["grain"]);
            Assert.Equal(Unit, result.After.Ships[Ship + "_3"].Cargo["logs"]);
            Assert.Equal(0, result.After.Companies[Owner].Cash);
            Assert.Equal(new[] { Ship + "_2", Ship + "_3" }, result.Trades.Select(t => t.ShipId));
            Assert.All(result.After.Ships.Values, s => Assert.InRange(s.ItineraryProgress!.OperationsUsed, 0, 8));
            Audit(result);
            return RunDiagnosticReport.StateFingerprint(result.After);
        }
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void SameDayStaleDecisionRejectsAndLaterProductionOverflowRollsBackEveryAction()
    {
        var world = World();
        Configure(world, Policy(Buy("grain")));
        var stale = world.EvaluateAutomation(Ship);
        world.SetAutomationEnabled(Ship, false);
        var before = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ExecuteAutomation(stale));
        Assert.Same(before, world.Snapshot());

        var overflow = World(overflowProduction: true);
        Configure(overflow, Policy(Buy("grain"), Buy("logs")));
        before = overflow.Snapshot();
        var hash = RunDiagnosticReport.StateFingerprint(before);
        Assert.Throws<OverflowException>(() => overflow.AdvanceDay());
        Assert.Same(before, overflow.Snapshot());
        Assert.Equal(hash, RunDiagnosticReport.StateFingerprint(overflow.Snapshot()));
        Assert.Equal(0, before.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(0, before.Accounting.Companies[Owner].InventoryPurchases);
    }

    [Fact]
    public void PauseAndSamePolicyKeepCursorCargoAndBudgetWhileUnsafeEditsReject()
    {
        var world = World();
        var policy = Policy(Buy("grain"), Buy("silver", shortage: CargoItineraryShortagePolicy.Wait));
        Configure(world, policy);
        world.RunAutomation(Ship);
        world.SetAutomationEnabled(Ship, false);
        var before = world.Snapshot();
        world.ConfigureItinerary(Ship, policy, false);
        Assert.Equal(before.Ships[Ship].ItineraryProgress!.ActionIndex, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(Unit, world.Snapshot().Ships[Ship].Cargo["grain"]);
        Assert.Throws<InvalidOperationException>(() => world.ConfigureItinerary(Ship, Policy(Buy("logs"))));
        Assert.Equal(Unit, world.Snapshot().Ships[Ship].Cargo["grain"]);
        world.SetAutomationEnabled(Ship, true);
        Assert.Equal(CargoItineraryDecisionKind.Wait, Decision(world).Kind);
        Assert.Equal(1, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
    }

    [Fact]
    public void WrongPortAndOldIdentityCannotGrantRemoteTransferAuthority()
    {
        var old = World(itineraryVersion: 0);
        Assert.Throws<InvalidOperationException>(() => Configure(old, Policy(Buy("grain"))));

        var world = World(warehouse: Stock(("bread", Unit)));
        var policy = Policy(Load("bread"));
        Configure(world, policy);
        world.SetAutomationEnabled(Ship, false);
        world.Depart(Ship, South);
        world.AdvanceDay();
        var remote = CargoItineraryEvaluator.PreviewCommand(world.Snapshot(), Ship, policy, 0, 0);
        Assert.Equal(Unit, remote.SelectedQuantity); // Informational current local stock, never execution permission.
        Assert.Throws<InvalidOperationException>(() => world.SetAutomationEnabled(Ship, true));
        var result = world.RunAutomation(Ship);
        Assert.Empty(result.Transfers);
        Assert.Equal(Unit, result.After.GetFacilityStorage(Site).Inventory["bread"]);
    }

    [Theory]
    [InlineData(CargoItineraryCommandKind.LoadFromCompanyTownStock, CargoItineraryShortagePolicy.Skip)]
    [InlineData(CargoItineraryCommandKind.LoadFromCompanyTownStock, CargoItineraryShortagePolicy.Wait)]
    [InlineData(CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryShortagePolicy.Skip)]
    [InlineData(CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryShortagePolicy.Wait)]
    public void MissingWarehouseIsAPlannableSkipOrWaitWithoutCreatingAssets(
        CargoItineraryCommandKind kind, CargoItineraryShortagePolicy shortage)
    {
        var world = World(cargo: Stock(("logs", 2 * Unit)));
        var policy = Policy(new CargoItineraryCommand(kind, "logs", Unit, shortagePolicy: shortage),
            Buy("silver", shortage: CargoItineraryShortagePolicy.Wait));
        var before = world.Snapshot();
        CargoItineraryEvaluator.ValidatePolicy(before, Ship, policy);
        var preview = CargoItineraryEvaluator.PreviewCommand(before, Ship, policy, 0, 0);
        Assert.Same(before, world.Snapshot());
        Assert.Equal(CargoItineraryBlockReason.MissingCompanyStock, preview.BlockReason);
        Assert.Equal(shortage == CargoItineraryShortagePolicy.Skip ? CargoItineraryDecisionKind.Skip :
            CargoItineraryDecisionKind.Wait, preview.Kind);
        Configure(world, policy);
        Assert.Equal(preview.BlockReason, Decision(world).BlockReason);
        var result = world.RunAutomation(Ship, 1);
        Audit(result);
        Assert.Empty(result.Trades);
        Assert.Empty(result.Transfers);
        Assert.Empty(result.After.PrivateFacilities);
        Assert.Empty(result.After.CompanyStockpiles);
        Assert.Equal(before.TotalCash, result.After.TotalCash);
        Assert.Equal(2 * Unit, result.After.Ships[Ship].Cargo["logs"]);
        var progress = result.After.Ships[Ship].ItineraryProgress!;
        Assert.Equal(shortage == CargoItineraryShortagePolicy.Skip ? 1 : 0, progress.ActionIndex);
        Assert.Equal(progress.ActionIndex, progress.OperationsUsed);
        Assert.Equal(CargoItineraryBlockReason.MissingCompanyStock, progress.LastReason);
        Assert.Equal(North, result.After.Ships[Ship].DockedPortId);
        if (shortage == CargoItineraryShortagePolicy.Wait)
        {
            Assert.Same(result.After, world.RunAutomation(Ship).After);
            world.ConstructFacility(Owner, North, "plan", Site);
            if (kind == CargoItineraryCommandKind.LoadFromCompanyTownStock)
                world.ExecuteWarehouseTrade(Owner, Site, new TradeCommand(TradeDirection.Buy, "logs", Unit));
            var resumed = world.RunAutomation(Ship, 1);
            Assert.Same(policy, resumed.After.Ships[Ship].Itinerary);
            Assert.Equal(Unit, Assert.Single(resumed.Transfers).TransferredQuantity);
            Assert.Equal(1, resumed.After.Ships[Ship].ItineraryProgress!.ActionIndex);
            Audit(resumed);
        }
    }

    [Fact]
    public void PolicyBoundsRejectMalformedOrUnboundedSequencesAndCopyCallerArrays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Buy("grain", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", Unit + 1));
        Assert.Throws<ArgumentException>(() => new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", Unit, cashReserve: 1));
        Assert.Throws<ArgumentException>(() => new CargoItineraryStop(North, Array.Empty<CargoItineraryCommand>()));
        Assert.Throws<ArgumentException>(() => new CargoItineraryStop(North, Enumerable.Repeat(Buy("grain"), 9)));
        Assert.Throws<ArgumentException>(() => new CargoItineraryPolicy("test", new[] { new CargoItineraryStop(North, new[] { Buy("grain") }) }));
        Assert.Throws<ArgumentException>(() => new CargoItineraryPolicy("test", Enumerable.Repeat(new CargoItineraryStop(North, new[] { Buy("grain") }), 2)));
        Assert.Throws<ArgumentException>(() => new CargoItineraryPolicy("test", new[] { "a", "b", "c", "d" }
            .Select(p => new CargoItineraryStop(p, Enumerable.Repeat(Buy("grain"), 7)))));
        var commands = new[] { Buy("grain") };
        var stops = new[] { new CargoItineraryStop(North, commands), new CargoItineraryStop(South, new[] { Sell("grain") }) };
        var policy = new CargoItineraryPolicy("test", stops);
        commands[0] = Buy("logs");
        stops[0] = new CargoItineraryStop("elsewhere", commands);
        Assert.Equal(North, policy.Stops[0].PortId);
        Assert.Equal("grain", policy.Stops[0].Commands[0].GoodId);
    }

    [Fact]
    public void OrdinarySeededBreadLoopRepeatsWithRealPaidProductionAndReconcilesSixtyDays()
    {
        var first = RunOrdinaryWitness(false);
        var repeated = RunOrdinaryWitness(true);
        Assert.Equal(first, repeated);
        output.WriteLine(first);
    }

    private static string RunOrdinaryWitness(bool reverse)
    {
        var content = Content();
        var identity = new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 99,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1);
        var world = RunScenario.Create(content, identity, reverse);
        var generation = ExpandingEconomyGeneration.Generate(99);
        var owner = WorldIds.PlayerOwnerId;
        var shipId = WorldIds.PlayerShipId;
        const string bakeryId = "itinerary_bakery";
        var records = new List<RegionalResult>();
        void Record(RegionalResult result) { Audit(result); records.Add(result); }
        Record(world.ConstructFacility(owner, generation.RecoveryPortId, WorldIds.BakeryBlueprint, bakeryId));
        Record(world.Depart(shipId, generation.RecoveryPortId));
        while (world.Snapshot().Ships[shipId].DockedPortId == null) Record(world.AdvanceDay());
        var enabledDay = world.Snapshot().Day;
        var policy = CargoItineraryPresets.BreadLoop(world.Snapshot(), shipId);
        Assert.Equal(new[] { generation.RecoveryPortId, generation.FoodSourcePortId, generation.ProcessingPortId }, policy.Stops.Select(s => s.PortId));
        Record(world.ConfigureItinerary(shipId, policy, true));
        var returns = new List<long>();
        while (world.Snapshot().Day < 60)
        {
            var result = world.AdvanceDay();
            Record(result);
            if (result.Movements.Any(m => m.ShipId == shipId && m.Kind == ShipMovementKind.Arrival && m.DestinationPortId == generation.RecoveryPortId))
                returns.Add(result.After.Day);
        }
        var after = world.Snapshot();
        var finance = after.Accounting.Companies[owner];
        var playerTrades = records.SelectMany(r => r.Trades).Where(t => t.ShipId == shipId).ToArray();
        var playerTransfers = records.SelectMany(r => r.Transfers).Where(t => t.ShipId == shipId).ToArray();
        Assert.True(returns.Count >= 2, "The route must physically complete at least two full A→B→C→A cycles.");
        Assert.True(after.PrivateFacilities[bakeryId].BatchesProduced >= 2);
        Assert.True(after.PrivateFacilities[bakeryId].WagesPaid > 0);
        Assert.True(playerTrades.Count(t => t.Trade.Direction == TradeDirection.Buy && t.Trade.GoodId == "grain") >= 2);
        Assert.True(playerTrades.Count(t => t.Trade.Direction == TradeDirection.Buy && t.Trade.GoodId == "logs") >= 2);
        Assert.True(playerTrades.Count(t => t.Trade.Direction == TradeDirection.Buy && t.Trade.GoodId == "flour") >= 2);
        Assert.True(playerTrades.Count(t => t.Trade.Direction == TradeDirection.Sell && t.Trade.GoodId == "grain") >= 2);
        Assert.Contains(playerTrades, t => t.Trade.Direction == TradeDirection.Sell && t.Trade.GoodId == "bread");
        Assert.True(playerTransfers.Count(t => t.Direction == TransferDirection.ShipToWarehouse && t.GoodId == "flour") >= 2);
        Assert.True(playerTransfers.Count(t => t.Direction == TransferDirection.ShipToWarehouse && t.GoodId == "logs") >= 2);
        Assert.Contains(playerTransfers, t => t.Direction == TransferDirection.WarehouseToShip && t.GoodId == "bread");
        Assert.Equal(EconomicUnits.Coins(300), finance.ConstructionExpenditure);
        Assert.Equal(0, after.Economy!.MintedCurrency);
        Assert.Equal(0, after.Economy.ExternalNetInflow);
        Assert.All(after.Ships.Values.Where(s => !s.IsPlayer), s => Assert.NotNull(s.Policy));
        Assert.Contains(records, r => r.After.Ships[shipId].Cargo.Count(g => g.Value > 0) >= 2);
        Assert.All(records.SelectMany(r => r.Decisions).Where(d => d.ShipId == shipId && d.ItineraryDetail != null),
            d => Assert.InRange(d.Before.Ships[shipId].ItineraryProgress!.OperationsUsed, 0, 8));
        return JsonConvert.SerializeObject(new
        {
            Seed = 99, Days = after.Day, EnabledDay = enabledDay, A = generation.RecoveryPortId,
            B = generation.FoodSourcePortId, C = generation.ProcessingPortId, ReturnDays = returns,
            Cash = after.Companies[owner].Cash, Value = after.CompanyValue(owner).Total,
            finance.InventoryPurchases, finance.SalesRevenue, finance.CostOfGoodsSold, finance.HeldInventoryCost,
            finance.WagesPaid, finance.RealizedGrossMargin, finance.OperatingSurplusAfterWages,
            Batches = after.PrivateFacilities[bakeryId].BatchesProduced,
            Produced = finance.ProducedQuantities, Held = finance.HeldInventoryQuantities,
            Transfers = playerTransfers.GroupBy(t => t.Direction + "/" + t.GoodId).ToDictionary(g => g.Key, g => g.Sum(t => (long)t.TransferredQuantity)),
            Trades = playerTrades.GroupBy(t => t.Trade.Direction + "/" + t.Trade.GoodId).ToDictionary(g => g.Key, g => g.Sum(t => (long)t.Trade.ExecutedQuantity)),
            AllAccountCash = after.TotalCash.ToString(), Fingerprint = RunDiagnosticReport.StateFingerprint(after)
        }, Formatting.Indented);
    }

    private static void Audit(RegionalResult result)
    {
        var before = result.Before;
        var after = result.After;
        Assert.Equal(before.TotalCash + after.Economy!.MintedCurrency - before.Economy!.MintedCurrency, after.TotalCash);
        foreach (var good in after.Ports.First().Value.Inventory.Keys)
        {
            BigInteger Physical(RegionalWorldSnapshot state) => state.Ports.Values.Aggregate(BigInteger.Zero, (n, p) => n + p.Inventory[good]) +
                state.Ships.Values.Aggregate(BigInteger.Zero, (n, s) => n + s.Cargo[good]) +
                state.CompanyStockpiles.Values.Aggregate(BigInteger.Zero, (n, s) => n + s.Inventory[good]) +
                state.PrivateFacilities.Values.Aggregate(BigInteger.Zero, (n, f) => n + f.Inventory[good]) +
                (good == "silver" ? state.Economy!.SilverReserve : 0);
            var changes = result.Production.SelectMany(p => p.Changes).Concat(result.PrivateProduction.SelectMany(p => p.Changes))
                .Where(c => c.GoodId == good).Aggregate(BigInteger.Zero, (n, c) => n + c.Delta);
            var consumed = result.Consumption.Where(c => c.GoodId == good).Aggregate(BigInteger.Zero, (n, c) => n + c.ConsumedQuantity);
            Assert.Equal(Physical(before) + changes - consumed, Physical(after));
        }
        foreach (var finance in after.Accounting.Companies.Values)
        {
            Assert.Equal(finance.InventoryPurchases, finance.CostOfGoodsSold + finance.HeldInventoryCost + finance.MintMaterialCost);
            Assert.Equal(finance.InitialCash - finance.InventoryPurchases + finance.SalesRevenue - finance.ConstructionExpenditure -
                finance.WagesPaid - finance.FeesPaid - finance.PublicCommissionExpenditure + finance.MintReceipts, after.Companies[finance.OwnerId].Cash);
            foreach (var good in after.Ports.First().Value.Inventory.Keys)
            {
                long Amount(IReadOnlyDictionary<string, long> values) => values.TryGetValue(good, out var value) ? value : 0;
                Assert.Equal(Amount(finance.GrantedQuantities) + Amount(finance.PurchasedQuantities) + Amount(finance.ProducedQuantities) -
                    Amount(finance.ConsumedQuantities) - Amount(finance.SoldQuantities) - Amount(finance.SurrenderedQuantities), Amount(finance.HeldInventoryQuantities));
            }
        }
        foreach (var store in after.Accounting.Stores.Values)
        {
            var inventory = store.StoreId.StartsWith("ship:", StringComparison.Ordinal)
                ? after.Ships[store.StoreId.Substring(5)].Cargo : after.CompanyStockpiles[store.StoreId].Inventory;
            foreach (var good in inventory) Assert.Equal(good.Value, store.GetQuantity(good.Key));
        }
    }
}
