using System;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class WorldSaveCodecTests
{
    private const string Owner = WorldIds.PlayerOwnerId;
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RunIdentity Identity(RunWorldMode mode, string charter = RunScenario.MerchantCharter,
        string? challenge = null, RunDifficulty difficulty = RunDifficulty.Normal,
        LivingPricingMode pricing = LivingPricingMode.StockOnly) =>
        new(challenge, charter, mode, -42, difficulty, livingPricingMode: pricing);

    [Theory]
    [InlineData(RunWorldMode.ExpandingEconomy)]
    public void FreshAndActiveWorldsResumeWithIdenticalStateAndCommittedEvents(RunWorldMode mode)
    {
        var world = RunScenario.Create(Content(), Identity(mode));
        var initial = WorldSaveCodec.Save(world);
        Assert.Equal(initial, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), initial)));
        world.SetAutomationEnabled(Ship, true);
        for (var day = 0; day < 11; day++) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        var resumed = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(resumed));
        for (var day = 0; day < 35; day++)
        {
            var uninterrupted = world.AdvanceDay();
            var continued = resumed.AdvanceDay();
            Assert.Equal(Events(uninterrupted), Events(continued));
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        }
    }

    [Theory]
    [InlineData(RunDifficulty.Normal)]
    [InlineData(RunDifficulty.SlowFoundations)]
    public void PendingIndustryAndWarehouseFifoContinueExactly(RunDifficulty difficulty)
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, difficulty: difficulty));
        world.ConstructFacility(Owner, ExpandingEconomyGeneration.Generate(-42).ProcessingPortId, WorldIds.BakeryBlueprint, "saved_bakery");
        world.ConfigureWarehouse(Owner, world.Snapshot().PrivateFacilities["saved_bakery"].PortId,
            ExpandingEconomyScenario.BakeryWarehousePolicy(), world.Snapshot());
        world.ExecuteWarehouseTrade(Owner, "saved_bakery", new TradeCommand(TradeDirection.Buy, "flour", EconomicUnits.FromWhole(3)));
        world.Depart(Ship, ExpandingEconomyGeneration.Generate(-42).ProcessingPortId);
        var resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        for (var day = 0; day < 25; day++)
        {
            Assert.Equal(Events(world.AdvanceDay()), Events(resumed.AdvanceDay()));
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        }
        Assert.True(resumed.Snapshot().PrivateFacilities["saved_bakery"].BatchesProduced > 0);
    }

    [Fact]
    public void IndustrialRouteStagesCostSlicesAndBlockedPoliciesSurviveReload()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, RunScenario.IndustrialCharter));
        world.ConfigureWarehouse(Owner, world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].PortId,
            ExpandingEconomyScenario.BakeryWarehousePolicy(), world.Snapshot());
        world.ConfigureIndustrialAutomation(Ship, ExpandingEconomyScenario.IndustrialPolicy(ExpandingEconomyGeneration.Generate(-42, economicRulesVersion: 1)), true);
        for (var index = 0; index < 15; index++)
        {
            var saved = WorldSaveCodec.Save(world);
            var resumed = WorldSaveCodec.Load(Content(), saved);
            Assert.Equal(saved, WorldSaveCodec.Save(resumed));
            Assert.Equal(Events(world.AdvanceDay()), Events(resumed.AdvanceDay()));
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        }
    }


    [Fact]
    public void CoverageParametersAndBoundedHouseholdHistoryDoNotRefreshDuringLoad()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, pricing: LivingPricingMode.Coverage));
        world.SetAutomationEnabled(Ship, true);
        for (var day = 0; day < 36; day++) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var resumed = WorldSaveCodec.Load(Content(), saved);
            Assert.Equal(saved, WorldSaveCodec.Save(resumed));
            Assert.Equal(Events(world.AdvanceDay()), Events(resumed.AdvanceDay()));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Fact]
    public void V3PendingFleetMintFactoriesSilverAndContractsHaveExactContinuation()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy,
            difficulty: RunDifficulty.SlowFoundations));
        var economy = world.Snapshot().Economy!;
        var config = economy.Configuration;
        world.CommissionMint(Owner);
        world.CommissionShip(Owner, config.ShipwrightPortId);
        world.ConstructFacility(Owner, config.SilverPortId, ExpandingEconomyGeneration.SilverMineBlueprint, "save_silver_mine");
        world.ConstructFacility(Owner, config.MintPortId, ExpandingEconomyGeneration.MillBlueprint, "save_mill");
        world.Depart(Ship, config.SilverPortId);
        Assert.Single(world.Snapshot().Economy!.ShipOrders);
        Assert.Equal(MintStatus.UnderConstruction, world.Snapshot().Economy!.MintStatus);
        Assert.Equal(FacilityConstructionStatus.UnderConstruction, world.Snapshot().PrivateFacilities["save_silver_mine"].ConstructionStatus);
        var resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        while (world.Snapshot().Day < 8 || world.Snapshot().Ships[Ship].DockedPortId == null)
        {
            AdvanceBoth(world, resumed);
            if (world.Snapshot().Day == 4)
            {
                Assert.Contains(world.Snapshot().Ports.Values, p => p.Inventory.Values.Any(q => q % 10000 != 0));
                resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(resumed));
            }
        }
        Assert.Equal(2, world.Snapshot().Ships.Values.Count(s => s.IsPlayer));
        var mine = world.Snapshot().PrivateFacilities["save_silver_mine"];
        Assert.True(world.Snapshot().GetFacilityStorage(mine.Id).Inventory["silver"] >= 10000);
        Assert.Equal(Events(world.TransferGoods(Owner, Ship, mine.Id, TransferDirection.WarehouseToShip, "silver", 10000)),
            Events(resumed.TransferGoods(Owner, Ship, mine.Id, TransferDirection.WarehouseToShip, "silver", 10000)));
        world.Depart(Ship, config.MintPortId);
        resumed.Depart(Ship, config.MintPortId);
        var secondPolicy = new TradeRoutePolicy(config.MintPortId, config.SilverPortId,
            new RouteLeg("logs", 20000, 10000, 100, 1), allowEmptyReturn: true);
        world.ConfigureAutomation("player_ship_2", secondPolicy, true);
        resumed.ConfigureAutomation("player_ship_2", secondPolicy, true);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) AdvanceBoth(world, resumed);
        Assert.Equal(Events(world.MintSilver(Ship)), Events(resumed.MintSilver(Ship)));
        var contract = config.Contracts.Single(c => c.GoodId == "planks");
        for (var wait = 0; world.Snapshot().Ships[Ship].Cargo["planks"] < 30000 && wait < 60; wait++)
        {
            var quantity = Math.Min(30000 - world.Snapshot().Ships[Ship].Cargo["planks"],
                world.Snapshot().MaximumTradeQuantity(Ship, config.MintPortId, TradeDirection.Buy, "planks"));
            if (quantity > 0)
            {
                world.ExecuteTrade(Ship, config.MintPortId, new TradeCommand(TradeDirection.Buy, "planks", quantity));
                resumed.ExecuteTrade(Ship, config.MintPortId, new TradeCommand(TradeDirection.Buy, "planks", quantity));
            }
            else AdvanceBoth(world, resumed);
        }
        Assert.True(world.Snapshot().Ships[Ship].Cargo["planks"] >= 30000);
        Assert.Equal(Events(world.FulfillExternalContract(Ship, contract.Id, 10000)),
            Events(resumed.FulfillExternalContract(Ship, contract.Id, 10000)));
        Assert.Equal(Events(world.SupplyPublicWorks(Ship, 20000)), Events(resumed.SupplyPublicWorks(Ship, 20000)));
        Assert.True(world.Snapshot().Economy!.MintedCurrency > 0);
        Assert.True(world.Snapshot().Economy!.ExternalNetInflow > 0);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        for (var day = 0; day < 30; day++) AdvanceBoth(world, resumed);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        Assert.Equal(world.Snapshot().CompanyValue(Owner).Total, resumed.Snapshot().CompanyValue(Owner).Total);
    }


    [Fact]
    public void FractionalAcquisitionIntervalsKeepExactBasisAcrossSaveAndWholeCargoTransfer()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy));
        var port = world.Snapshot().Ships[Ship].DockedPortId!;
        world.ConstructFacility(Owner, port, WorldIds.BakeryBlueprint, "fractional_store");
        world.ExecuteWarehouseTrade(Owner, "fractional_store", new TradeCommand(TradeDirection.Buy, "flour", 12501));
        world.ExecuteWarehouseTrade(Owner, "fractional_store", new TradeCommand(TradeDirection.Sell, "flour", 2501));
        world.TransferGoods(Owner, Ship, "fractional_store", TransferDirection.WarehouseToShip, "flour", 10000);
        var lot = Assert.Single(world.Snapshot().Accounting.Stores["ship:" + Ship].Goods["flour"].Lots);
        Assert.Equal(2501, lot.Start);
        Assert.Equal(10000, lot.Quantity);
        var resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        Assert.Equal(Events(world.ExecuteTrade(Ship, port, new TradeCommand(TradeDirection.Sell, "flour", 10000))),
            Events(resumed.ExecuteTrade(Ship, port, new TradeCommand(TradeDirection.Sell, "flour", 10000))));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
    }

    [Fact]
    public void FactoryRatesPrioritiesAndTownTradingResumeTheSameWholeBatchesAndFifo()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, RunScenario.IndustrialCharter));
        var port = world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].PortId;
        world.ConstructFacility(Owner, port, WorldIds.BakeryBlueprint, "second_bakery");
        world.ConfigureProduction(Owner, RunScenario.StarterBakery, true, 75, ProductionPriority.High, world.Snapshot());
        world.ConfigureProduction(Owner, "second_bakery", true, 50, ProductionPriority.Low, world.Snapshot());
        world.ConfigureWarehouse(Owner, port, new WarehousePolicy(EconomicUnits.Coins(80), new[]
        {
            new WarehouseCommodityRule("flour", true, 50000, 0, false, 0, 0),
            new WarehouseCommodityRule("logs", true, 40000, 24, true, 40000, 30),
            new WarehouseCommodityRule("bread", false, 0, 0, true, 10000, 20),
            new WarehouseCommodityRule("silver", true, 10000, 1, false, 0, 0)
        }), world.Snapshot());
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal(75, world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].ProductionCredit);
        Assert.Equal(50, world.Snapshot().PrivateFacilities["second_bakery"].ProductionCredit);
        var saved = WorldSaveCodec.Save(world);
        var json = JObject.Parse(saved);
        Assert.Equal(6, (int)json["SchemaVersion"]!);
        Assert.All(json["Facilities"]!, facility => Assert.Null(facility["Policy"]));
        Assert.Equal(4, json["CompanyStockpiles"]![0]!["Policy"]!["Rules"]!.Count());
        var resumed = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(resumed));
        for (var day = 0; day < 32; day++)
        {
            Assert.Equal(Events(world.AdvanceDay()), Events(resumed.AdvanceDay()));
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
            if (day == 8) resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(resumed));
        }
        Assert.All(resumed.Snapshot().PrivateFacilities.Values, factory =>
        {
            Assert.True(factory.BatchesProduced > 0);
            Assert.Equal(factory.BatchesProduced * factory.Blueprint.WagePerBatch, factory.WagesPaid);
        });
        var warehouse = Assert.Single(resumed.Snapshot().CompanyStockpiles.Values);
        Assert.True(warehouse.ProcurementCost > 0);
        Assert.True(warehouse.SalesRevenue > 0);
    }

    [Fact]
    public void AdmittedBlockedBatchSurvivesPausedSaveWithoutAccumulatingCredit()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, RunScenario.IndustrialCharter));
        var port = world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].PortId;
        world.ExecuteWarehouseTrade(Owner, RunScenario.StarterBakery, new TradeCommand(TradeDirection.Sell, "flour", 20000));
        world.ConfigureProduction(Owner, RunScenario.StarterBakery, true, 75, ProductionPriority.High, world.Snapshot());
        for (var day = 0; day < 7; day++) world.AdvanceDay();
        var blocked = world.Snapshot().PrivateFacilities[RunScenario.StarterBakery];
        Assert.Equal(150, blocked.ProductionCredit);
        Assert.Equal(4, blocked.NextProductionDay);
        Assert.Equal(0, blocked.BatchesProduced);
        world.ConfigureProduction(Owner, blocked.Id, true, 0, ProductionPriority.Normal, world.Snapshot());
        var resumed = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        for (var day = 0; day < 5; day++) AdvanceBoth(world, resumed);
        Assert.Equal(150, resumed.Snapshot().PrivateFacilities[blocked.Id].ProductionCredit);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        foreach (var current in new[] { world, resumed })
        {
            current.ConfigureWarehouse(Owner, port, new WarehousePolicy(rules: new[]
            { new WarehouseCommodityRule("flour", true, 20000, 0, false, 0, 0) }), current.Snapshot());
            current.ConfigureProduction(Owner, blocked.Id, true, 50, ProductionPriority.Normal, current.Snapshot());
        }
        AdvanceBoth(world, resumed);
        var produced = resumed.Snapshot().PrivateFacilities[blocked.Id];
        Assert.Equal(1, produced.BatchesProduced);
        Assert.Equal(50, produced.ProductionCredit);
        Assert.Equal(produced.Blueprint.WagePerBatch, produced.WagesPaid);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
    }

    [Fact]
    public void InvalidAutomationSaveFieldsAreRejectedEvenWithARecomputedChecksum()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, RunScenario.IndustrialCharter));
        var port = world.Snapshot().PrivateFacilities[RunScenario.StarterBakery].PortId;
        world.ConfigureWarehouse(Owner, port, new WarehousePolicy(rules: new[]
        { new WarehouseCommodityRule("flour", true, 20000, 28, true, 30000, 30) }), world.Snapshot());
        var saved = WorldSaveCodec.Save(world);
        var snapshot = world.Snapshot();
        var changes = new Action<JObject>[]
        {
            j => j["Facilities"]![0]!["OperatingRate"] = -1,
            j => j["Facilities"]![0]!["OperatingRate"] = 101,
            j => j["Facilities"]![0]!["Priority"] = 99,
            j => j["Facilities"]![0]!["ProductionCredit"] = -1,
            j => j["Facilities"]![0]!["ProductionCredit"] = 200,
            j => j["Facilities"]![0]!["ProductionCredit"] = 100,
            j => ((JObject)j["Facilities"]![0]!).Property("OperatingRate")!.Remove(),
            j => j["CompanyStockpiles"]![0]!["Policy"]!["Rules"]![0]!["GoodId"] = "invented",
            j => j["CompanyStockpiles"]![0]!["Policy"]!["Rules"]![0]!["BuyTarget"] = int.MaxValue,
            j => j["CompanyStockpiles"]![0]!["Policy"]!["Rules"]![0]!["SellReserve"] = 10000,
            j => j["CompanyStockpiles"]![0]!["Policy"]!["Rules"]![0]!["MaximumBuyPrice"] = -1,
            j => j["CompanyStockpiles"]![0]!["Policy"]!["CashReserve"] = -1,
            j => ((JArray)j["CompanyStockpiles"]![0]!["Policy"]!["Rules"]!).Add(
                j["CompanyStockpiles"]![0]!["Policy"]!["Rules"]![0]!.DeepClone()),
            j => j["CompanyStockpiles"]![0]!["Policy"] = null,
            j => j["CompanyStockpiles"]![0]!["Procurement"] = 1,
            j => j["CompanyStockpiles"]![0]!["SalesWait"] = 99,
            j => j["Ports"]!.First(p => p["Facilities"]!.Any())["Facilities"]![0]!["LastOutcome"] =
                (int)ScheduledProductionOutcome.RateLimited
        };
        foreach (var change in changes)
        {
            var json = JObject.Parse(saved);
            change(json);
            Rechecksum(json);
            Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), json.ToString(Formatting.None)));
            Assert.Same(snapshot, world.Snapshot());
        }
        var obsolete = JObject.Parse(saved);
        obsolete["SchemaVersion"] = 5;
        obsolete["Facilities"]![0]!["Policy"] = new JObject();
        var failure = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), obsolete.ToString(Formatting.None)));
        Assert.Contains("schema 6", failure.Message);
        Assert.Same(snapshot, world.Snapshot());
    }
    private static void AdvanceBoth(RegionalWorldState first, RegionalWorldState second) =>
        Assert.Equal(Events(first.AdvanceDay()), Events(second.AdvanceDay()));

    [Fact]
    public void CorruptionSchemaAndContentMismatchNeverChangeExistingWorld()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy));
        var before = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        foreach (var damaged in new[] { "", saved[..^1], saved + " {}", saved.Replace("\"Day\":0", "\"Day\":1"),
            saved.Replace("\"SchemaVersion\":6", "\"SchemaVersion\":99"),
            saved.Replace("\"Day\":0", "\"Day\":0,\"Day\":0"),
            saved.Replace("\"Day\":0", "\"Day\":0.0"),
            saved.Insert(1, "\"$type\":\"System.IO.FileInfo\","), saved.Insert(1, "\"unexpected\":0,") })
            Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), damaged));
        var changedContent = ContentLoader.Load(ContentLoaderTests.CanonicalJson().Replace("Bread", "Changed Bread"));
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(changedContent, saved));
        Assert.Same(before, world.Snapshot());
    }

    [Theory]
    [InlineData("negative_cash")]
    [InlineData("duplicate_ship")]
    [InlineData("unknown_good")]
    [InlineData("impossible_voyage")]
    [InlineData("missing_store")]
    [InlineData("fifo_quantity")]
    [InlineData("fifo_overlap")]
    [InlineData("grant_cost")]
    [InlineData("negative_counter")]
    [InlineData("wrong_configuration")]
    public void SemanticCorruptionIsRejectedEvenWithARecomputedChecksum(string corruption)
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, RunScenario.IndependentCharter));
        var json = JObject.Parse(WorldSaveCodec.Save(world));
        var ships = (JArray)json["Ships"]!;
        var stores = (JArray)json["Stores"]!;
        var populated = stores.Single(s => ((JArray)s["Goods"]!).Count > 0);
        var lots = (JArray)populated["Goods"]![0]!["Lots"]!;
        switch (corruption)
        {
            case "negative_cash": json["Companies"]![0]!["Cash"] = -1; break;
            case "duplicate_ship": ships.Add(ships[0].DeepClone()); break;
            case "unknown_good": ships[0]!["Cargo"]!["fake_good"] = 1; break;
            case "impossible_voyage": ships[0]!["RemainingDays"] = 8; break;
            case "missing_store": stores.RemoveAt(0); break;
            case "fifo_quantity": lots[0]!["Quantity"] = 1; break;
            case "fifo_overlap": lots.Add(lots[0].DeepClone()); break;
            case "grant_cost": lots[0]!["OriginalCost"] = 100; break;
            case "negative_counter": json["Companies"]![0]!["Wages"] = -1; break;
            case "wrong_configuration": json["ConfigurationFingerprint"] = new string('a', 64); break;
        }
        Rechecksum(json);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), json.ToString(Formatting.None)));
    }

    private static void Rechecksum(JObject root)
    {
        root["StateFingerprint"] = "";
        static JToken Canonical(JToken token) => token switch
        {
            JObject obj => new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, Canonical(p.Value)))),
            JArray array => new JArray(array.Select(Canonical)),
            _ => token.DeepClone()
        };
        root["StateFingerprint"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Canonical(root).ToString(Formatting.None)))).ToLowerInvariant();
    }

    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades,
        result.Production, result.Consumption, result.Movements, result.WarehouseTrades, result.PrivateProduction,
        result.Construction, result.Transfers, result.Development
    });
}
