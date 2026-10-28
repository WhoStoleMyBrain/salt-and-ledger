using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SaltAndLedger.Simulation.Tests;

public sealed class FactoryWarehouseWitnessTests
{
    private readonly ITestOutputHelper output;
    public FactoryWarehouseWitnessTests(ITestOutputHelper output) { this.output = output; }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PaidFlourVoyageAndCampSupplyThreeCompetingBakeries(bool changePriority)
    {
        var content = ContentLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content.json")));
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter, seed: 99));
        var generation = ExpandingEconomyGeneration.Generate(99);
        var owner = WorldIds.PlayerOwnerId;
        var ship = WorldIds.PlayerShipId;
        var town = generation.FoodSourcePortId;
        var initialCash = world.Snapshot().TotalCash;
        void Audit(RegionalWorldSnapshot state)
        {
            Assert.Equal(initialCash, state.TotalCash);
            foreach (var company in state.Accounting.Companies.Values)
                Assert.Equal(company.InventoryPurchases, company.CostOfGoodsSold + company.HeldInventoryCost + company.MintMaterialCost);
            foreach (var store in state.Accounting.Stores.Values)
            foreach (var good in store.Goods)
            {
                var inventory = store.StoreId.StartsWith("ship:", StringComparison.Ordinal)
                    ? state.Ships[store.StoreId.Substring(5)].Cargo : state.CompanyStockpiles[store.StoreId].Inventory;
                Assert.Equal(inventory[good.Key], store.GetQuantity(good.Key));
            }
        }
        void Days(int count) { for (var i = 0; i < count; i++) Audit(world.AdvanceDay().After); }
        void Sail(string port)
        {
            world.Depart(ship, port);
            while (world.Snapshot().Ships[ship].DockedPortId == null) Days(1);
        }
        Sail(generation.ProcessingPortId);
        var purchase = world.ExecuteTrade(ship, generation.ProcessingPortId, new TradeCommand(TradeDirection.Buy, "flour", EconomicUnits.FromWhole(10)));
        Assert.Equal(TradeOutcome.Traded, Assert.Single(purchase.Trades).Trade.Outcome);
        Sail(town);
        foreach (var site in new[] { ("a_camp", ExpandingEconomyGeneration.LumberBlueprint),
            ("b_bakery", WorldIds.BakeryBlueprint), ("c_bakery", WorldIds.BakeryBlueprint), ("d_bakery", WorldIds.BakeryBlueprint) })
            world.ConstructFacility(owner, town, site.Item2, site.Item1);
        world.ConfigureProduction(owner, "a_camp", true, 100, ProductionPriority.High, world.Snapshot());
        world.TransferGoods(owner, ship, "b_bakery", TransferDirection.ShipToWarehouse, "flour", EconomicUnits.FromWhole(10));
        world.ConfigureWarehouse(owner, town, new WarehousePolicy(EconomicUnits.Coins(100), new[] {
            new WarehouseCommodityRule("flour", true, EconomicUnits.FromWhole(6), 0, false, 0, 0),
            new WarehouseCommodityRule("bread", false, 0, 0, true, 0, 0) }), world.Snapshot());
        var setupDay = world.Snapshot().Day;
        Days(2);
        var competed = world.Snapshot();
        Assert.Equal(1, competed.PrivateFacilities["b_bakery"].BatchesProduced);
        Assert.Equal(1, competed.PrivateFacilities["c_bakery"].BatchesProduced);
        Assert.Equal(0, competed.PrivateFacilities["d_bakery"].BatchesProduced);
        Assert.Equal(ScheduledProductionOutcome.InputShortage, competed.PrivateFacilities["d_bakery"].LastOutcome);
        Assert.True(competed.GetFacilityStorage("a_camp").Inventory["flour"] > 0);
        Assert.Equal(0, competed.GetFacilityStorage("a_camp").Inventory["logs"]);

        if (changePriority)
            world.ConfigureProduction(owner, "d_bakery", true, 100, ProductionPriority.High, world.Snapshot());
        else
            world.ConfigureProduction(owner, "b_bakery", true, 50, ProductionPriority.Normal, world.Snapshot());
        var loaded = WorldSaveCodec.Load(content, WorldSaveCodec.Save(world));
        for (var i = 0; i < 2; i++)
        {
            Days(1);
            loaded.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(loaded));
        }
        var after = world.Snapshot();
        Assert.Equal(2, after.PrivateFacilities["a_camp"].BatchesProduced);
        Assert.Equal(changePriority ? 2 : 1, after.PrivateFacilities["b_bakery"].BatchesProduced);
        Assert.Equal(changePriority ? 1 : 2, after.PrivateFacilities["c_bakery"].BatchesProduced);
        Assert.Equal(1, after.PrivateFacilities["d_bakery"].BatchesProduced);
        Assert.Equal(EconomicUnits.Coins(36), after.Accounting.Companies[owner].WagesPaid);
        Assert.Equal(EconomicUnits.FromWhole(8), after.Accounting.Companies[owner].ProducedQuantities["bread"]);
        Assert.Equal(EconomicUnits.Coins(1120), after.Accounting.Companies[owner].ConstructionExpenditure);
        Assert.Equal(0, after.Economy!.MintedCurrency);
        output.WriteLine(JsonConvert.SerializeObject(new { changePriority, setupDay, after.Day,
            Cash = after.Companies[owner].Cash, Store = after.GetFacilityStorage("a_camp"),
            Factories = after.PrivateFacilities.Values.Select(f => new { f.Id, f.BatchesProduced, f.WagesPaid, f.OperatingRate, f.Priority, f.ProductionCredit }) }, Formatting.Indented));
    }
}
