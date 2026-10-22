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
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Ship = EnterpriseScenario.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RunIdentity Identity(RunWorldMode mode, string charter = RunScenario.MerchantCharter,
        string? challenge = null, RunDifficulty difficulty = RunDifficulty.Normal,
        LivingPricingMode pricing = LivingPricingMode.StockOnly) =>
        new(challenge, charter, mode, mode == RunWorldMode.Classic ? null : -42, difficulty, livingPricingMode: pricing);

    [Theory]
    [InlineData(RunWorldMode.Classic)]
    [InlineData(RunWorldMode.Seeded)]
    [InlineData(RunWorldMode.LivingCities)]
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
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.Seeded, difficulty: difficulty));
        world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, "saved_bakery");
        world.ConfigureFacility(Owner, "saved_bakery", EnterpriseScenario.BakeryPolicy());
        world.ExecuteWarehouseTrade(Owner, "saved_bakery", new TradeCommand(TradeDirection.Buy, "flour", 3));
        world.Depart(Ship, EnterpriseScenario.West);
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
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.Classic, RunScenario.IndustrialCharter));
        world.ConfigureFacility(Owner, RunScenario.StarterBakery, EnterpriseScenario.BakeryPolicy());
        world.ConfigureIndustrialAutomation(Ship, RunScenario.BakeryShippingPolicy(), true);
        for (var index = 0; index < 15; index++)
        {
            var saved = WorldSaveCodec.Save(world);
            var resumed = WorldSaveCodec.Load(Content(), saved);
            Assert.Equal(saved, WorldSaveCodec.Save(resumed));
            Assert.Equal(Events(world.AdvanceDay()), Events(resumed.AdvanceDay()));
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        }
    }

    [Theory]
    [InlineData(ChallengeStatus.Succeeded)]
    [InlineData(ChallengeStatus.Failed)]
    [InlineData(ChallengeStatus.Abandoned)]
    public void TerminalChallengesRemainInspectableAndFrozen(ChallengeStatus status)
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.Classic, challenge: RunScenario.CoastalChallenge));
        if (status == ChallengeStatus.Abandoned) world.AbandonChallenge();
        else
        {
            if (status == ChallengeStatus.Succeeded) world.SetAutomationEnabled(Ship, true);
            while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        }
        Assert.Equal(status, world.Snapshot().Challenge!.Status);
        var saved = WorldSaveCodec.Save(world);
        var resumed = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(resumed));
        Assert.Throws<InvalidOperationException>(() => resumed.AdvanceDay());
        Assert.Equal(saved, WorldSaveCodec.Save(resumed));
    }

    [Fact]
    public void CoverageParametersAndBoundedHouseholdHistoryDoNotRefreshDuringLoad()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.LivingCities, pricing: LivingPricingMode.Coverage));
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
            challenge: RunScenario.RoyalChallenge, difficulty: RunDifficulty.SlowFoundations));
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
        Assert.True(mine.Inventory["silver"] >= 10000);
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

    [Theory]
    [InlineData(RunScenario.SustainableChallenge)]
    [InlineData(RunScenario.NetworkChallenge)]
    [InlineData(RunScenario.RoyalChallenge)]
    public void V3IdleFailureAndExpiredContractsLoadWithoutReevaluation(string challenge)
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, challenge: challenge));
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        Assert.Equal(ChallengeStatus.Failed, world.Snapshot().Challenge!.Status);
        var saved = WorldSaveCodec.Save(world);
        var resumed = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(resumed));
        Assert.Throws<InvalidOperationException>(() => resumed.AdvanceDay());
    }

    [Fact]
    public void FractionalAcquisitionIntervalsKeepExactBasisAcrossSaveAndWholeCargoTransfer()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy));
        var port = world.Snapshot().Ships[Ship].DockedPortId!;
        world.ConstructFacility(Owner, port, EnterpriseScenario.BakeryBlueprint, "fractional_store");
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

    [Theory]
    [InlineData(RunScenario.SustainableChallenge)]
    [InlineData(RunScenario.NetworkChallenge)]
    [InlineData(RunScenario.RoyalChallenge)]
    public void ForgedV3SuccessCannotTrustTheSavedPercentage(string challenge)
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, challenge: challenge));
        var json = JObject.Parse(WorldSaveCodec.Save(world));
        json["Challenge"]!["Status"] = (int)ChallengeStatus.Succeeded;
        json["Challenge"]!["Produced"] = 100;
        json["Challenge"]!["CompletedDay"] = 0;
        Rechecksum(json);
        var exception = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), json.ToString(Formatting.None)));
        Assert.Contains("authoritative", exception.Message);
    }

    [Theory]
    [InlineData(ChallengeStatus.Active)]
    [InlineData(ChallengeStatus.Failed)]
    public void QualifiedV3VictoryCannotBeRestoredAsActiveOrFailed(ChallengeStatus forgedStatus)
    {
        var world = RoyalVictory();
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.True(ExpandingChallenges.Progress(world.Snapshot()).Qualified);
        var saved = WorldSaveCodec.Save(world);
        Assert.Equal(saved, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), saved)));
        var json = JObject.Parse(saved);
        json["Challenge"]!["Status"] = (int)forgedStatus;
        if (forgedStatus == ChallengeStatus.Active)
            json["Challenge"]!["CompletedDay"] = null;
        else
        {
            var deadline = world.Snapshot().Challenge!.Configuration.DeadlineDay;
            json["Day"] = deadline;
            json["Revision"] = Math.Max(world.Snapshot().Revision, deadline);
            json["Challenge"]!["CompletedDay"] = deadline;
        }
        Rechecksum(json);
        var exception = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), json.ToString(Formatting.None)));
        Assert.Contains("lifecycle", exception.Message);
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    [Theory]
    [InlineData(RunScenario.SustainableChallenge)]
    [InlineData(RunScenario.NetworkChallenge)]
    [InlineData(RunScenario.RoyalChallenge)]
    public void UnqualifiedV3ActiveAndExplicitAbandonmentKeepTheirExactLifecycle(string challenge)
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.ExpandingEconomy, challenge: challenge));
        for (var day = 0; day < 11; day++) world.AdvanceDay();
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
        Assert.False(ExpandingChallenges.Progress(world.Snapshot()).Qualified);
        var active = WorldSaveCodec.Save(world);
        var resumed = WorldSaveCodec.Load(Content(), active);
        Assert.Equal(active, WorldSaveCodec.Save(resumed));
        Assert.Equal(Events(world.AdvanceDay()), Events(resumed.AdvanceDay()));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(resumed));
        world.AbandonChallenge();
        var abandoned = WorldSaveCodec.Save(world);
        resumed = WorldSaveCodec.Load(Content(), abandoned);
        Assert.Equal(ChallengeStatus.Abandoned, resumed.Snapshot().Challenge!.Status);
        Assert.Equal(abandoned, WorldSaveCodec.Save(resumed));
        Assert.Throws<InvalidOperationException>(() => resumed.AdvanceDay());
    }

    private static RegionalWorldState RoyalVictory()
    {
        var world = RunScenario.Create(Content(), new RunIdentity(RunScenario.RoyalChallenge,
            RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
        var config = world.Snapshot().Economy!.Configuration;
        world.ConstructFacility(Owner, config.SilverPortId, ExpandingEconomyGeneration.SilverMineBlueprint, "royal_save_mine");
        world.CommissionMint(Owner);
        world.CommissionShip(Owner, config.ShipwrightPortId);
        world.ConfigureAutomation(Ship, ExpandingEconomyScenario.MerchantPolicy(ExpandingEconomyGeneration.Generate(42), true), true);
        void Until(int day) { while (world.Snapshot().Day < day) world.AdvanceDay(); }
        void Buy(string good, int quantity) => world.ExecuteTrade("player_ship_2",
            world.Snapshot().Ships["player_ship_2"].DockedPortId!, new TradeCommand(TradeDirection.Buy, good, quantity * 10000));
        void LoadSilver(int quantity) => world.TransferGoods(Owner, "player_ship_2", "royal_save_mine",
            TransferDirection.WarehouseToShip, "silver", quantity * 10000);
        Until(4); Buy("planks", 2); world.SupplyPublicWorks("player_ship_2", 20000);
        world.Depart("player_ship_2", config.SilverPortId);
        Until(8); LoadSilver(2); Buy("furniture", 2); world.Depart("player_ship_2", config.MintPortId);
        Until(12); world.FulfillExternalContract("player_ship_2", "royal_furniture", 20000); world.MintSilver("player_ship_2");
        Until(16); world.MintSilver("player_ship_2"); world.Depart("player_ship_2", config.SilverPortId);
        Until(20); LoadSilver(3); Buy("furniture", 2); world.Depart("player_ship_2", config.MintPortId);
        Until(24); world.FulfillExternalContract("player_ship_2", "royal_furniture", 20000); world.MintSilver("player_ship_2");
        return world;
    }

    private static void AdvanceBoth(RegionalWorldState first, RegionalWorldState second) =>
        Assert.Equal(Events(first.AdvanceDay()), Events(second.AdvanceDay()));

    [Fact]
    public void CorruptionSchemaAndContentMismatchNeverChangeExistingWorld()
    {
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.Classic));
        var before = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        foreach (var damaged in new[] { "", saved[..^1], saved + " {}", saved.Replace("\"Day\":0", "\"Day\":1"),
            saved.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":99"),
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
        var world = RunScenario.Create(Content(), Identity(RunWorldMode.Classic, RunScenario.IndependentCharter));
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
        result.Production, result.Consumption, result.Movements, result.PrivateProduction,
        result.Construction, result.Transfers, result.Development
    });
}
