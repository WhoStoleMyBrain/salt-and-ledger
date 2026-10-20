using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class AccountingTests
{
    private const string Owner = "player";
    private const string Ship = "ship:ledger";
    private const string Warehouse = "facility:bakery";
    private static WorkingAccounting Ledger()
    {
        var ledger = new WorkingAccounting();
        ledger.RegisterCompany(Owner, 600);
        ledger.RegisterStore(Ship, Owner);
        ledger.RegisterStore(Warehouse, Owner);
        return ledger;
    }
    private static CompanyFinancialSnapshot Company(WorkingAccounting ledger) => ledger.Snapshot().Companies[Owner];
    private static GoodCostSnapshot Stock(WorkingAccounting ledger, string store, string good) =>
        ledger.Snapshot().Stores[store].Goods[good];
    private static RecipeDefinition Bread() => new("bake", "Bake", "Bake bread.", "bakery", RecipeKind.Transformation,
        new[] { new RecipeQuantity("flour", 1), new RecipeQuantity("logs", 1) },
        new[] { new RecipeQuantity("bread", 2) });

    [Fact]
    public void EmptyCompaniesAndStoresHaveExplicitInitialCashAndZeroBasis()
    {
        var ledger = Ledger();
        var before = ledger.Snapshot();
        Assert.Equal(600, before.Companies[Owner].InitialCash);
        Assert.Equal(2, before.Stores.Count);
        Assert.Empty(before.Stores[Ship].Goods);
        Assert.Equal(0, before.Stores[Ship].GetQuantity("bread"));
        Assert.Equal(0, before.Stores[Ship].GetCostBasis("bread"));
        Assert.Equal(0, before.Companies[Owner].HeldInventoryCost);
        Assert.Empty(ledger.Events(0, 0));
    }

    [Fact]
    public void RepeatedPurchasesAndPartialSalesUseActualFifoAcquisitions()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 3, 10);
        ledger.Purchase(Ship, "south", "logs", 2, 11);
        ledger.Sell(Ship, "west", "logs", 2, 15);
        var company = Company(ledger);
        Assert.Equal((21, 15, 6, 15, 9),
            (company.InventoryPurchases, company.SalesRevenue, company.CostOfGoodsSold,
                company.HeldInventoryCost, company.RealizedGrossMargin));
        Assert.Equal(3, Stock(ledger, Ship, "logs").Quantity);
        ledger.Sell(Ship, "west", "logs", 2, 17);
        company = Company(ledger);
        Assert.Equal(15, company.CostOfGoodsSold); // Remaining 4 from first lot, then 5 from second.
        Assert.Equal(6, company.HeldInventoryCost);
        ledger.Sell(Ship, "west", "logs", 1, 8);
        Assert.Equal(21, Company(ledger).CostOfGoodsSold);
        Assert.Equal(0, Company(ledger).HeldInventoryCost);
        Assert.Empty(ledger.Snapshot().Stores[Ship].Goods);
    }

    [Theory]
    [InlineData(3, 10)]
    [InlineData(5, 3)]
    [InlineData(17, 199)]
    [InlineData(23, 1)]
    public void PartialSalesAllocateEveryOriginalIntegerExactly(int quantity, long cost)
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", quantity, cost);
        var assigned = 0L;
        for (var index = 1; index <= quantity; index++)
        {
            ledger.Sell(Ship, "south", "logs", 1, 1);
            var company = Company(ledger);
            assigned = company.CostOfGoodsSold;
            Assert.Equal(cost * index / quantity, assigned);
            Assert.Equal(cost, assigned + company.HeldInventoryCost);
        }
        Assert.Equal(cost, assigned);
    }

    [Fact]
    public void TransfersKeepOriginalDenominatorAndAppendAtDestination()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 5, 3);
        var original = Stock(ledger, Ship, "logs").Lots.Single();
        ledger.Transfer(Ship, Warehouse, "north", "logs", 1);
        ledger.Transfer(Ship, Warehouse, "north", "logs", 1);
        var transferred = Stock(ledger, Warehouse, "logs");
        Assert.Equal(1, transferred.CostBasis); // Re-pooling the remaining 4 units would incorrectly yield zero.
        Assert.Equal(new long[] { 0, 1 }, transferred.Lots.Select(l => l.CostBasis));
        Assert.All(transferred.Lots, lot => Assert.Equal((original.LotId, 5L, 3L),
            (lot.LotId, lot.OriginalQuantity, lot.OriginalCost)));
        ledger.Transfer(Warehouse, Ship, "north", "logs", 1);
        var inShip = Stock(ledger, Ship, "logs");
        Assert.Equal(new long[] { 2, 0 }, inShip.Lots.Select(l => l.Start));
        ledger.Sell(Ship, "north", "logs", 3, 6);
        Assert.Equal(2, Company(ledger).CostOfGoodsSold);
        Assert.Equal(1, Company(ledger).HeldInventoryCost);
        Assert.Equal(0, Stock(ledger, Ship, "logs").CostBasis);
        Assert.Equal(3, Company(ledger).InventoryPurchases);
        Assert.Equal(6, Company(ledger).SalesRevenue);
        Assert.All(ledger.Events(0, 1).Where(e => e.Kind == EconomicEventKind.Transfer),
            e => Assert.Equal((0L, 0L, Owner, Owner), (e.CashTotal, e.CompanyCashDelta, e.SourceOwnerId, e.DestinationOwnerId)));
    }

    [Fact]
    public void StarterGrantsRetainProvenanceWithoutFabricatingPurchaseCost()
    {
        var ledger = Ledger();
        ledger.Grant(Ship, "bread", 2, "independent_charter");
        ledger.Purchase(Ship, "north", "bread", 3, 9);
        ledger.Transfer(Ship, Warehouse, "north", "bread", 1);
        var grant = Stock(ledger, Warehouse, "bread").Lots.Single();
        Assert.Equal(InventoryCostOrigin.StarterGrant, grant.Origin);
        Assert.Equal("independent_charter", grant.Provenance);
        Assert.Equal(0, grant.CostBasis);
        ledger.Sell(Ship, "east", "bread", 2, 10);
        Assert.Equal(2, Company(ledger).GrantedQuantities["bread"]);
        Assert.Equal((9, 3, 6), (Company(ledger).InventoryPurchases,
            Company(ledger).CostOfGoodsSold, Company(ledger).HeldInventoryCost));
        var first = ledger.Events(0, 3).First();
        Assert.Equal(EconomicEventKind.StarterGrant, first.Kind);
        Assert.Null(first.SourceOwnerId);
        Assert.Equal(Owner, first.DestinationOwnerId);
        Assert.Equal(0, first.CashTotal);
    }

    [Fact]
    public void MultiInputManufactureTransfersAndSalesReconcileActualInputBasis()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 2, 7);
        ledger.Transfer(Ship, Warehouse, "north", "logs", 1); // Basis 3; 4 remains in cargo.
        ledger.Purchase(Warehouse, "north", "flour", 1, 8);
        ledger.Produce(Warehouse, "north", Bread());
        var bread = Stock(ledger, Warehouse, "bread");
        Assert.Equal((2, 11), (bread.Quantity, bread.CostBasis));
        Assert.Equal(InventoryCostOrigin.Manufactured, bread.Lots.Single().Origin);
        Assert.Equal("bake", bread.Lots.Single().Provenance);
        ledger.Transfer(Warehouse, Ship, "north", "bread", 1);
        ledger.Sell(Ship, "east", "bread", 1, 20);
        var company = Company(ledger);
        Assert.Equal((15, 20, 5, 10, 15),
            (company.InventoryPurchases, company.SalesRevenue, company.CostOfGoodsSold,
                company.HeldInventoryCost, company.RealizedGrossMargin));
        Assert.Equal(2, company.ProducedQuantities["bread"]);
        Assert.Equal(1, company.ConsumedQuantities["flour"]);
        Assert.Equal(1, company.ConsumedQuantities["logs"]);
        var production = ledger.Events(7, 12).Single(e => e.Kind == EconomicEventKind.Production);
        Assert.Equal((7, 12, 11, "bake", "bakery"),
            (production.Day, production.Revision, production.AllocatedCost, production.RecipeId, production.FacilityId));
        Assert.Equal(11, production.Consumed.Sum(g => g.CostBasis));
        Assert.Equal(11, production.Produced.Sum(g => g.CostBasis));
        Assert.Equal(new[] { "flour", "logs" }, production.Consumed.Select(g => g.GoodId));
        Assert.All(production.Consumed.SelectMany(g => g.Lots), lot => Assert.Equal(InventoryCostOrigin.Purchased, lot.Origin));
    }

    [Fact]
    public void PurchasedAndManufacturedStockShareOneArrivalOrderedFifo()
    {
        var ledger = Ledger();
        ledger.Purchase(Warehouse, "north", "bread", 1, 20);
        ledger.Purchase(Warehouse, "north", "flour", 1, 8);
        ledger.Purchase(Warehouse, "north", "logs", 1, 3);
        ledger.Produce(Warehouse, "north", Bread());
        ledger.Sell(Warehouse, "north", "bread", 2, 40);
        Assert.Equal((25, 6), (Company(ledger).CostOfGoodsSold, Company(ledger).HeldInventoryCost));
        Assert.Equal(InventoryCostOrigin.Manufactured, Stock(ledger, Warehouse, "bread").Lots.Single().Origin);
    }

    [Fact]
    public void MultipleOutputRemaindersFollowOrdinalGoodIdsIndependentlyOfRecipeOrder()
    {
        static WorkingAccounting Run(bool reverse)
        {
            var ledger = Ledger();
            ledger.Purchase(Warehouse, "north", "logs", 2, 10);
            var outputs = new[] { new RecipeQuantity("planks", 1), new RecipeQuantity("bread", 2) };
            var recipe = new RecipeDefinition("mixed", "Mixed", "Multiple outputs.", "bakery", RecipeKind.Transformation,
                new[] { new RecipeQuantity("logs", 2) }, reverse ? outputs.Reverse() : outputs);
            ledger.Produce(Warehouse, "north", recipe);
            return ledger;
        }
        var forward = Run(false);
        var reverse = Run(true);
        Assert.Equal(JsonConvert.SerializeObject(forward.Snapshot()), JsonConvert.SerializeObject(reverse.Snapshot()));
        Assert.Equal(6, Stock(forward, Warehouse, "bread").CostBasis);
        Assert.Equal(4, Stock(forward, Warehouse, "planks").CostBasis);
        Assert.Equal(3, Stock(forward, Warehouse, "bread").Lots.Single().OriginalQuantity);
        Assert.Equal(2, Stock(forward, Warehouse, "planks").Lots.Single().Start);
    }

    [Fact]
    public void RecipeConsumingItsOwnOutputUsesGrossMovements()
    {
        var ledger = Ledger();
        ledger.Purchase(Warehouse, "north", "logs", 2, 9);
        var recipe = new RecipeDefinition("rework", "Rework", "Consumes its own output.", "sawmill",
            RecipeKind.Transformation, new[] { new RecipeQuantity("logs", 2) },
            new[] { new RecipeQuantity("logs", 1), new RecipeQuantity("planks", 1) });
        ledger.Produce(Warehouse, "north", recipe);
        Assert.Equal((1, 4), (Stock(ledger, Warehouse, "logs").Quantity, Stock(ledger, Warehouse, "logs").CostBasis));
        Assert.Equal((1, 5), (Stock(ledger, Warehouse, "planks").Quantity, Stock(ledger, Warehouse, "planks").CostBasis));
        var produced = ledger.Events(1, 1).Last();
        Assert.Equal(2, produced.Consumed.Single().Quantity);
        Assert.Equal(2, produced.Produced.Sum(g => g.Quantity));
        Assert.Equal(9, Company(ledger).HeldInventoryCost);
    }

    [Fact]
    public void InputFreeExtractionHasZeroModeledMaterialBasisAndProductionProvenance()
    {
        var ledger = Ledger();
        ledger.Produce(Warehouse, "north", new RecipeDefinition("harvest", "Harvest", "Extract logs.", "camp",
            RecipeKind.Extraction, Array.Empty<RecipeQuantity>(), new[] { new RecipeQuantity("logs", 2) }));
        var lot = Stock(ledger, Warehouse, "logs").Lots.Single();
        Assert.Equal((InventoryCostOrigin.Manufactured, "harvest", 0L), (lot.Origin, lot.Provenance, lot.CostBasis));
        Assert.Empty(ledger.Events(1, 1).Single().Consumed);
        Assert.Equal(0, Company(ledger).InventoryPurchases);
        Assert.Equal(2, Company(ledger).ProducedQuantities["logs"]);
    }

    [Fact]
    public void ProductionOfGrantedInputsPreservesAuditableGrantProvenance()
    {
        var ledger = Ledger();
        ledger.Grant(Warehouse, "logs", 1, "industrial_charter");
        ledger.Purchase(Warehouse, "north", "flour", 1, 8);
        ledger.Produce(Warehouse, "north", Bread());
        var production = ledger.Events(2, 1).Last();
        var logs = production.Consumed.Single(g => g.GoodId == "logs").Lots.Single();
        Assert.Equal((InventoryCostOrigin.StarterGrant, "industrial_charter", 0L),
            (logs.Origin, logs.Provenance, logs.CostBasis));
        Assert.Equal(8, Stock(ledger, Warehouse, "bread").CostBasis);
    }

    [Fact]
    public void ConstructionIsCapitalExpenditureAndCannotBecomeInventoryBasis()
    {
        var ledger = Ledger();
        ledger.Construct(Owner, "bakery", "north", 300);
        ledger.Purchase(Warehouse, "north", "logs", 1, 3);
        var company = Company(ledger);
        Assert.Equal((300, 3, 3, 0), (company.ConstructionExpenditure, company.InventoryPurchases,
            company.HeldInventoryCost, company.CostOfGoodsSold));
        var construction = ledger.Events(0, 1).First();
        Assert.Equal(EconomicEventKind.Construction, construction.Kind);
        Assert.Equal((300, -300, 0), (construction.CashTotal, construction.CompanyCashDelta, construction.AllocatedCost));
        Assert.Empty(construction.Consumed);
        Assert.Empty(construction.Produced);
    }

    [Fact]
    public void EventsIdentifyCommitOwnersStoresAndSignedActualCash()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 2, 7);
        ledger.Transfer(Ship, Warehouse, "north", "logs", 1);
        ledger.Sell(Warehouse, "north", "logs", 1, 4);
        var events = ledger.Events(9, 33);
        Assert.All(events, e => Assert.Equal((9, 33, Owner), (e.Day, e.Revision, e.CompanyId)));
        Assert.Equal(new long[] { -7, 0, 4 }, events.Select(e => e.CompanyCashDelta));
        Assert.Equal(("market:north", Owner, "market:north", Ship),
            (events[0].SourceOwnerId, events[0].DestinationOwnerId, events[0].SourceStoreId, events[0].DestinationStoreId));
        Assert.Equal((Owner, "market:north", Warehouse, "market:north"),
            (events[2].SourceOwnerId, events[2].DestinationOwnerId, events[2].SourceStoreId, events[2].DestinationStoreId));
        Assert.Equal(3, events[2].AllocatedCost);
        Assert.Equal(Company(ledger).InventoryPurchases, events.Where(e => e.Kind == EconomicEventKind.Purchase).Sum(e => e.CashTotal));
        Assert.Equal(Company(ledger).SalesRevenue, events.Where(e => e.Kind == EconomicEventKind.Sale).Sum(e => e.CashTotal));
        Assert.Equal(Company(ledger).CostOfGoodsSold, events.Where(e => e.Kind == EconomicEventKind.Sale).Sum(e => e.AllocatedCost));
    }

    [Fact]
    public void WorkingCopiesAndReturnedCollectionsCannotChangePriorAccounting()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 3, 10);
        var before = ledger.Snapshot();
        var text = JsonConvert.SerializeObject(before);
        var work = new WorkingAccounting(before);
        work.Transfer(Ship, Warehouse, "north", "logs", 2);
        work.Sell(Warehouse, "north", "logs", 1, 4);
        Assert.Equal(text, JsonConvert.SerializeObject(before));
        Assert.Equal(3, before.Stores[Ship].Goods["logs"].Quantity);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, InventoryCostSnapshot>)before.Stores).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<InventoryCostLot>)before.Stores[Ship].Goods["logs"].Lots).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EconomicEvent>)work.Events(0, 2)).Clear());
    }

    [Fact]
    public void NewOperationHasNoRetainedJournalAndSpentLotsAreDiscarded()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 1, 3);
        ledger.Sell(Ship, "south", "logs", 1, 5);
        var next = new WorkingAccounting(ledger.Snapshot());
        Assert.Empty(next.Events(0, 3));
        Assert.Empty(next.Snapshot().Stores[Ship].Goods);
        Assert.Equal(2, Company(next).RealizedGrossMargin);
        next.Purchase(Ship, "north", "logs", 1, 4);
        Assert.Single(next.Events(0, 3));
        Assert.Single(next.Snapshot().Stores[Ship].Goods["logs"].Lots);
    }

    [Fact]
    public void LargeQuantitiesAndCostsUseExactBoundedIntermediateArithmetic()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", int.MaxValue, long.MaxValue);
        ledger.Sell(Ship, "south", "logs", int.MaxValue - 1, 1);
        var remaining = Stock(ledger, Ship, "logs");
        var expectedReleased = (long)((System.Numerics.BigInteger)long.MaxValue * (int.MaxValue - 1) / int.MaxValue);
        Assert.Equal(expectedReleased, Company(ledger).CostOfGoodsSold);
        Assert.Equal(long.MaxValue - expectedReleased, remaining.CostBasis);
        Assert.Single(remaining.Lots);
    }

    [Fact]
    public void CumulativeOverflowIsExceptionalAndPriorSnapshotRemainsUntouched()
    {
        var ledger = Ledger();
        ledger.Purchase(Ship, "north", "logs", 1, long.MaxValue);
        ledger.Sell(Ship, "north", "logs", 1, long.MaxValue);
        var before = ledger.Snapshot();
        var work = new WorkingAccounting(before);
        Assert.Throws<OverflowException>(() => work.Purchase(Ship, "north", "logs", 1, 1));
        Assert.Equal(long.MaxValue, before.Companies[Owner].InventoryPurchases);
        Assert.Equal(0, before.Stores[Ship].GetQuantity("logs"));
        work = new WorkingAccounting(before);
        work.Grant(Ship, "logs", 1);
        Assert.Throws<OverflowException>(() => work.Sell(Ship, "north", "logs", 1, 1));
        Assert.Empty(before.Stores[Ship].Goods);
        var capital = Ledger();
        capital.Construct(Owner, "one", "north", long.MaxValue);
        Assert.Throws<OverflowException>(() => capital.Construct(Owner, "two", "north", 1));
    }

    [Fact]
    public void CrossCompanyTransfersAndUnavailableDisposalsAreRejected()
    {
        var ledger = Ledger();
        ledger.RegisterCompany("other", 1);
        ledger.RegisterStore("ship:other", "other");
        ledger.Grant(Ship, "logs", 1);
        var before = JsonConvert.SerializeObject(ledger.Snapshot());
        Assert.Throws<InvalidOperationException>(() => ledger.Transfer(Ship, "ship:other", "north", "logs", 1));
        Assert.Throws<InvalidOperationException>(() => ledger.Sell(Ship, "north", "logs", 2, 1));
        Assert.Throws<ArgumentException>(() => ledger.Transfer(Ship, Ship, "north", "logs", 1));
        Assert.Equal(before, JsonConvert.SerializeObject(ledger.Snapshot()));
    }

    [Fact]
    public void NegativePricesQuantitiesAndMissingStoreOwnersAreInvalid()
    {
        var ledger = Ledger();
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Purchase(Ship, "north", "logs", 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Grant(Ship, "logs", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ledger.RegisterCompany("negative", -1));
        Assert.Throws<KeyNotFoundException>(() => ledger.RegisterStore("missing", "missing"));
        Assert.Throws<ArgumentException>(() => ledger.RegisterStore("", Owner));
        Assert.Empty(ledger.Events(0, 0));
    }

    [Fact]
    public void AccountingOrderAndFormattingAreCultureIndependent()
    {
        static string Run()
        {
            var ledger = Ledger();
            ledger.Purchase(Ship, "north", "logs", 2, 7);
            ledger.Purchase(Warehouse, "north", "flour", 1, 8);
            ledger.Transfer(Ship, Warehouse, "north", "logs", 1);
            ledger.Produce(Warehouse, "north", Bread());
            return JsonConvert.SerializeObject(new { State = ledger.Snapshot(), Events = ledger.Events(3, 9) });
        }
        var expected = Run();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, Run());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
