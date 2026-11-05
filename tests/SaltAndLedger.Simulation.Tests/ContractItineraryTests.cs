using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;
using static SaltAndLedger.Simulation.Tests.OpportunityTestWorld;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ContractItineraryTests
{
    private const int Unit = EconomicUnits.QuantityScale;

    private static RegionalWorldState StockedWorld() => Create(configuration: Configuration(json =>
        json["startingInventories"]!["planks"] = new JObject { ["min"] = 30, ["max"] = 30 }));

    private static CargoItineraryCommand Deliver(string id, int units = 1,
        CargoItineraryShortagePolicy shortage = CargoItineraryShortagePolicy.Wait) =>
        new(CargoItineraryCommandKind.DeliverContract, "planks", units * Unit, shortagePolicy: shortage, contractId: id);

    private static CargoItineraryPolicy AtDock(RegionalWorldState world, params CargoItineraryCommand[] commands)
    {
        var snapshot = world.Snapshot();
        var dock = snapshot.Ships[Ship].DockedPortId!;
        return new CargoItineraryPolicy("contract_route", new[]
        {
            new CargoItineraryStop(dock, commands),
            new CargoItineraryStop(snapshot.Ports.Keys.First(port => port != dock), new[]
            { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "silver", Unit) })
        });
    }

    [Fact]
    public void AutomatedPaidCargoDeliveriesResumeExactlyAfterSaveWithoutRepeatingCompletedOrders()
    {
        var world = StockedWorld();
        var contract = world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == "royal_planks");
        world.AcceptOpportunity(Owner, contract.Id);
        Buy(world, contract.PortId, "planks", 2 * Unit);
        var initial = world.Snapshot();
        world.ConfigureItinerary(Ship, AtDock(world, Deliver(contract.Id), Deliver(contract.Id),
            new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "silver", Unit,
                shortagePolicy: CargoItineraryShortagePolicy.Wait)), true);
        var decision = CargoItineraryEvaluator.Evaluate(world.Snapshot(), Ship);
        Assert.Equal(CargoItineraryDecisionKind.Execute, decision.Kind);
        Assert.Equal(Unit, decision.SelectedQuantity);
        Assert.Null(decision.Trade);
        var first = world.RunAutomation(Ship, 1);
        Assert.Empty(first.Trades);
        Assert.Equal(1, first.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(Unit, first.After.Ships[Ship].Cargo["planks"]);
        Assert.Equal(initial.Companies[Owner].Cash + Unit * contract.UnitPrice, first.After.Companies[Owner].Cash);

        var restored = Reload(world);
        world.RunAutomation(Ship); restored.RunAutomation(Ship);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        var completed = world.Snapshot();
        Assert.Equal(2, completed.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(0, completed.Ships[Ship].Cargo["planks"]);
        Assert.Equal(2 * Unit, completed.Opportunities!.Entries.Single(entry => entry.Id == contract.Id).DeliveredQuantity);
        Assert.Equal(initial.Companies[Owner].Cash + 2 * Unit * contract.UnitPrice, completed.Companies[Owner].Cash);
        var payments = completed.Opportunities.CumulativePayments;
        for (var attempt = 0; attempt < 12; attempt++) world.RunAutomation(Ship);
        Assert.Equal(payments, world.Snapshot().Opportunities!.CumulativePayments);
        Assert.Equal(2, world.Snapshot().Ships[Ship].ItineraryProgress!.OperationsUsed);
        Audit(world.Snapshot(), initial.TotalCash);
    }

    [Theory]
    [InlineData(CargoItineraryShortagePolicy.Skip, 1)]
    [InlineData(CargoItineraryShortagePolicy.Wait, 0)]
    public void UnacceptedContractUsesOrdinarySkipWaitWithoutMovingCargo(CargoItineraryShortagePolicy shortage, int cursor)
    {
        var world = StockedWorld();
        var dock = world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == "royal_planks").PortId;
        Buy(world, dock, "planks", Unit);
        world.ConfigureItinerary(Ship, AtDock(world, Deliver("royal_planks", shortage: shortage),
            new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "silver", Unit,
                shortagePolicy: CargoItineraryShortagePolicy.Wait)), true);
        var before = world.Snapshot();
        var preview = CargoItineraryEvaluator.Evaluate(before, Ship);
        Assert.Equal(CargoItineraryBlockReason.ContractUnavailable, preview.BlockReason);
        world.RunAutomation(Ship);
        Assert.Equal(cursor, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(Unit, world.Snapshot().Ships[Ship].Cargo["planks"]);
        Assert.Equal(before.Companies[Owner].Cash, world.Snapshot().Companies[Owner].Cash);
        Reload(world);
    }

    [Fact]
    public void EndedContractWaitOrderFinishesAPartialStopWithoutResettingTheDailyBudget()
    {
        var world = StockedWorld();
        world.ConfigureItinerary(Ship, AtDock(world,
            new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "planks", Unit),
            Deliver("retired_contract")), true);
        world.RunAutomation(Ship, 1);
        var partial = world.Snapshot();
        Assert.Equal(1, partial.Ships[Ship].ItineraryProgress!.ActionIndex);
        var skipped = world.RunAutomation(Ship, 1);
        Assert.Equal(2, skipped.After.Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(CargoItineraryDecisionKind.Skip, skipped.After.Ships[Ship].ItineraryProgress!.LastDecision);
        Assert.Equal(CargoItineraryBlockReason.ContractUnavailable, skipped.After.Ships[Ship].ItineraryProgress!.LastReason);
        Assert.Equal(2, skipped.After.Ships[Ship].ItineraryProgress!.OperationsUsed);
        Assert.Equal(Unit, skipped.After.Ships[Ship].Cargo["planks"]);
        world.SetAutomationEnabled(Ship, false);
        var changed = AtDock(world, new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "planks", Unit));
        var beforeEdit = world.Snapshot();
        Assert.Throws<InvalidOperationException>(() => world.ConfigureItinerary(Ship, changed));
        Assert.Same(beforeEdit, world.Snapshot());
        world.AdvanceDay();
        world.ConfigureItinerary(Ship, changed);
        Assert.Equal(0, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Reload(world);
    }

    [Fact]
    public void RecurringDeliveryWaitsForEachWindowAndResumesTheExactOrderAcrossSave()
    {
        var world = Create(configuration: Configuration(json =>
        {
            foreach (var good in json["startingInventories"]!.Children<JProperty>().ToArray())
                good.Value = new JObject { ["min"] = 30, ["max"] = 30 };
        }));
        var contract = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.RecurringSupply);
        world.AcceptOpportunity(Owner, contract.Id);
        Buy(world, contract.PortId, contract.GoodId, contract.RequiredQuantity);
        var perWindow = contract.RequiredQuantity / contract.WindowCount;
        var orders = Enumerable.Range(0, contract.WindowCount).Select(_ =>
            new CargoItineraryCommand(CargoItineraryCommandKind.DeliverContract, contract.GoodId, perWindow,
                shortagePolicy: CargoItineraryShortagePolicy.Wait, contractId: contract.Id)).Concat(new[]
            { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "silver", Unit,
                shortagePolicy: CargoItineraryShortagePolicy.Wait) }).ToArray();
        world.ConfigureItinerary(Ship, AtDock(world, orders), true);
        world.RunAutomation(Ship);
        Assert.Equal(1, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(perWindow, world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == contract.Id).DeliveredQuantity);
        var restored = Reload(world);
        var initialCash = world.Snapshot().TotalCash;
        while (world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == contract.Id).Status == OpportunityStatus.Active)
        {
            Assert.True(world.Snapshot().Day <= contract.DeadlineDay + 1);
            world.AdvanceDay(); restored.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
            Audit(world.Snapshot(), initialCash);
        }
        var finished = world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == contract.Id);
        Assert.Equal(OpportunityStatus.Succeeded, finished.Status);
        Assert.Equal(contract.RequiredQuantity, finished.DeliveredQuantity);
        Assert.Equal(contract.FundingRequired, finished.PaidCash);
        Assert.Equal(contract.WindowCount, world.Snapshot().Ships[Ship].ItineraryProgress!.ActionIndex);
        Assert.Equal(0, world.Snapshot().Ships[Ship].Cargo[contract.GoodId]);
        Reload(world);
    }

    [Fact]
    public void ContractTargetsBindTermsAndDoNotPermitMismatchedGoodsOrTown()
    {
        Assert.Throws<ArgumentNullException>(() => new CargoItineraryCommand(CargoItineraryCommandKind.DeliverContract, "planks", Unit));
        Assert.Throws<ArgumentException>(() => new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "planks", Unit, contractId: "royal_planks"));
        var world = StockedWorld();
        var contract = world.Snapshot().Opportunities!.Entries.Single(entry => entry.Id == "royal_planks");
        Sail(world, contract.PortId);
        var wrongGood = new CargoItineraryCommand(CargoItineraryCommandKind.DeliverContract, "bread", Unit, contractId: contract.Id);
        var before = world.Snapshot();
        Assert.Throws<ArgumentException>(() => world.ConfigureItinerary(Ship, AtDock(world, wrongGood), true));
        Assert.Same(before, world.Snapshot());
    }

    [Fact]
    public void DepotOnlyTownLoadsAndUnloadsThroughTheOrdinaryItineraryAndFifoPath()
    {
        var world = StockedWorld();
        var project = world.Snapshot().Opportunities!.Entries.First(entry => entry.Kind == OpportunityKind.TradingDepot);
        world.AcceptOpportunity(Owner, project.Id);
        Buy(world, project.PortId, project.GoodId, project.RequiredQuantity + Unit);
        world.ContributeProject(Owner, project.Id, Ship, project.RequiredQuantity, project.RequiredCash);
        var before = world.Snapshot();
        Assert.DoesNotContain(before.PrivateFacilities.Values, facility => facility.PortId == project.PortId);
        var storeId = CompanyStockpileSnapshot.StoreId(Owner, project.PortId);
        Assert.True(before.CompanyStockpiles.ContainsKey(storeId));
        world.ConfigureItinerary(Ship, AtDock(world,
            new CargoItineraryCommand(CargoItineraryCommandKind.UnloadToCompanyTownStock, project.GoodId, Unit),
            new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, project.GoodId, Unit),
            new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "silver", Unit,
                shortagePolicy: CargoItineraryShortagePolicy.Wait)), true);
        var moved = world.RunAutomation(Ship);
        Assert.Equal(2, moved.Transfers.Count);
        Assert.All(moved.Transfers, transfer => Assert.Equal(TransferOutcome.Transferred, transfer.Outcome));
        Assert.Equal(Unit, moved.After.Ships[Ship].Cargo[project.GoodId]);
        Assert.Equal(0, moved.After.CompanyStockpiles[storeId].Inventory[project.GoodId]);
        Assert.Equal(before.Accounting.Stores["ship:" + Ship].TotalCostBasis,
            moved.After.Accounting.Stores["ship:" + Ship].TotalCostBasis);
        Assert.Equal(before.Companies[Owner].Cash, moved.After.Companies[Owner].Cash);
        Audit(moved.After, before.TotalCash);
        Reload(world);
    }
}
