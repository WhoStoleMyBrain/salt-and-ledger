using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionSaveTests
{
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Ship = EnterpriseScenario.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World(int seed = 42, RunDifficulty difficulty = RunDifficulty.Normal)
    {
        var draft = GrandCommissionDraft.Generate(seed);
        var ids = draft.Rounds.Select(r => r.Offers[0].TemplateId).ToArray();
        ids[2] = draft.Rounds[2].Offers.First(g => g.Conditions.Any(c => c.Rule == CommissionRule.NetMarketSales)).TemplateId;
        return RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, seed, difficulty,
            commission: draft.Resolve(ids)));
    }

    [Theory]
    [InlineData(RunWorldMode.Classic, "e11073e322c9940077b2db61fb27ffecdbef04990b42ecdce3991d52789c3eeb")]
    [InlineData(RunWorldMode.Seeded, "47f08f09bfceb0268d9b38c7083b03f1dd3a015e54f3ae478ba4cf7ee1d5737f")]
    [InlineData(RunWorldMode.LivingCities, "1e8f59af76c18801900d4507f60e7ce6236893a7e336cb095f52941e846e3abe")]
    [InlineData(RunWorldMode.ExpandingEconomy, "416b937308a19d6a1c7028963315d862a67c2c51e22545d233190d8e04fb40e4")]
    public void HistoricalSchemaOneSaveBytesAndContinuationRemainExact(RunWorldMode mode, string baselineHash)
    {
        // Captured from the pre-Task-2.1 Release assembly, with the unchanged canonical content.
        var world = RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter, mode,
            mode == RunWorldMode.Classic ? null : -42));
        world.SetAutomationEnabled(Ship, true);
        for (var day = 0; day < 11; day++) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        Assert.Equal(baselineHash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(saved))).ToLowerInvariant());
        var root = JObject.Parse(saved);
        Assert.Equal(1, (int)root["SchemaVersion"]!);
        Assert.Null(root["CommissionHistory"]);
        Assert.Null(root["Identity"]!["Commission"]);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        AdvanceBoth(world, restored);
    }

    [Fact]
    public void GenuineVoyageTargetSaleAndBuybackResumeWithoutDuplicateCredit()
    {
        var world = World();
        var condition = world.Snapshot().RunIdentity!.Commission!.Goals[2].Conditions
            .Single(c => c.Rule == CommissionRule.NetMarketSales);
        var target = condition.PortId!;
        var good = condition.GoodId!;
        var source = world.Snapshot().Ports.Keys.First(port => port != target &&
            world.Snapshot().MaximumTradeQuantity(Ship, port, TradeDirection.Buy, good) >= EconomicUnits.QuantityScale);
        Sail(world, source);
        Assert.Equal(TradeOutcome.Traded, world.ExecuteTrade(Ship, source,
            new TradeCommand(TradeDirection.Buy, good, EconomicUnits.QuantityScale)).Trades.Single().Trade.Outcome);
        world.Depart(Ship, target);
        var visitedEarlier = world.Snapshot().CommissionHistory!.Ships.Single(s => s.ShipId == Ship)
            .Visits.TryGetValue(target, out var originalVisitDay);
        var saved = WorldSaveCodec.Save(world);
        Assert.Equal(2, (int)JObject.Parse(saved)["SchemaVersion"]!);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Null(restored.Snapshot().Ships[Ship].DockedPortId);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) AdvanceBoth(world, restored);
        Assert.Equal(visitedEarlier ? originalVisitDay : world.Snapshot().Day,
            restored.Snapshot().CommissionHistory!.Ships.Single(s => s.ShipId == Ship).Visits[target]);
        world.ExecuteTrade(Ship, target, new TradeCommand(TradeDirection.Sell, good, EconomicUnits.QuantityScale));
        restored.ExecuteTrade(Ship, target, new TradeCommand(TradeDirection.Sell, good, EconomicUnits.QuantityScale));
        Assert.Equal(EconomicUnits.QuantityScale, Balance(restored, target, good));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        restored = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(restored));
        world.ExecuteTrade(Ship, target, new TradeCommand(TradeDirection.Buy, good, EconomicUnits.QuantityScale));
        restored.ExecuteTrade(Ship, target, new TradeCommand(TradeDirection.Buy, good, EconomicUnits.QuantityScale));
        Assert.Equal(0, Balance(restored, target, good));
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
    }

    [Fact]
    public void WarehouseFractionalTradePendingFactoryAndNewShipHistorySurviveExactly()
    {
        var world = World(-42, RunDifficulty.SlowFoundations);
        var config = world.Snapshot().Economy!.Configuration;
        var port = world.Snapshot().Ships[Ship].DockedPortId!;
        world.ConstructFacility(Owner, port, EnterpriseScenario.BakeryBlueprint, "commission_save_bakery");
        world.ExecuteWarehouseTrade(Owner, "commission_save_bakery", new TradeCommand(TradeDirection.Buy, "flour", 12501));
        world.ExecuteWarehouseTrade(Owner, "commission_save_bakery", new TradeCommand(TradeDirection.Sell, "flour", 2501));
        world.CommissionShip(Owner, config.ShipwrightPortId);
        var restored = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
        Assert.Equal(-10000, Balance(restored, port, "flour"));
        while (world.Snapshot().Day < config.ShipConstructionDays) AdvanceBoth(world, restored);
        var second = restored.Snapshot().CommissionHistory!.Ships.Single(s => s.AcquisitionOrder == 1);
        Assert.Equal("player_ship_2", second.ShipId);
        Assert.Single(second.Visits);
        Assert.Equal(config.ShipConstructionDays, second.Visits[config.ShipwrightPortId]);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(restored))));
        var destination = world.Snapshot().Ports.Keys.First(p => p != config.ShipwrightPortId);
        world.Depart(second.ShipId, destination);
        restored.Depart(second.ShipId, destination);
        while (world.Snapshot().Ships[second.ShipId].DockedPortId == null) AdvanceBoth(world, restored);
        Assert.Equal(2, restored.Snapshot().CommissionHistory!.Ships.Single(s => s.AcquisitionOrder == 1).Visits.Count);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(restored))));
    }

    [Fact]
    public void RealExternalAndPublicSalesAreReconciledSeparatelyFromLocalMarketCredit()
    {
        var world = World();
        var config = world.Snapshot().Economy!.Configuration;
        world.CommissionMint(Owner);
        Sail(world, config.MintPortId);
        var contract = config.Contracts.Single(c => c.GoodId == "planks");
        while (world.Snapshot().Day < contract.OpensDay ||
            world.Snapshot().MaximumTradeQuantity(Ship, config.MintPortId, TradeDirection.Buy, "planks") < 30000)
        {
            Assert.True(world.Snapshot().Day < 40, "The real public delivery fixture must obtain finite Planks.");
            world.AdvanceDay();
        }
        world.ExecuteTrade(Ship, config.MintPortId, new TradeCommand(TradeDirection.Buy, "planks", 30000));
        world.FulfillExternalContract(Ship, contract.Id, 10000);
        world.SupplyPublicWorks(Ship, 20000);
        Assert.Equal(30000, world.Snapshot().Accounting.Companies[Owner].SoldQuantities["planks"]);
        Assert.Equal(-30000, Balance(world, config.MintPortId, "planks"));
        var saved = WorldSaveCodec.Save(world);
        Assert.Equal(saved, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), saved)));
    }

    [Fact]
    public void ResealedFacilityBatchClaimMustMatchActualCompanyPayrollAndProduction()
    {
        var world = World();
        var generation = ExpandingEconomyGeneration.Generate(42);
        world.ConstructFacility(Owner, generation.RecoveryPortId, ExpandingEconomyGeneration.LumberBlueprint, "saved_camp");
        while (world.Snapshot().PrivateFacilities["saved_camp"].BatchesProduced == 0) world.AdvanceDay();
        world.SetFacilityEnabled(Owner, "saved_camp", false);
        while (world.Snapshot().Day < 10) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        var json = JObject.Parse(saved);
        var site = json["Facilities"]!.Single(f => (string)f["Id"]! == "saved_camp");
        site["Batches"] = (long)site["Batches"]! * 2;
        site["Wages"] = (long)site["Wages"]! * 2;
        site["Produced"]!["logs"] = (long)site["Produced"]!["logs"]! * 2;
        Rechecksum(json);
        var error = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), json.ToString(Formatting.None)));
        Assert.Contains("payroll", error.Message);
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureAndAbandonmentKeepChosenPlanAndFreeze(bool abandon)
    {
        var world = World();
        if (abandon) world.AbandonChallenge();
        else while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(abandon ? ChallengeStatus.Abandoned : ChallengeStatus.Failed, restored.Snapshot().Challenge!.Status);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Throws<InvalidOperationException>(() => restored.AdvanceDay());
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        var retry = RunScenario.Create(Content(), restored.Snapshot().RunIdentity!);
        Assert.Equal(0, retry.Snapshot().Day);
        Assert.Equal(restored.Snapshot().RunIdentity, retry.Snapshot().RunIdentity);
        Assert.Single(retry.Snapshot().CommissionHistory!.Ships.Single().Visits);
        Assert.Empty(retry.Snapshot().CommissionHistory!.Markets);
        Assert.Equal(WorldSaveCodec.Save(World()), WorldSaveCodec.Save(retry));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("missing_plan")]
    [InlineData("missing_history")]
    [InlineData("version")]
    [InlineData("world")]
    [InlineData("signature")]
    [InlineData("goal")]
    [InlineData("template")]
    [InlineData("parameters")]
    [InlineData("ship_owner")]
    [InlineData("ship_order")]
    [InlineData("ghost_visit")]
    [InlineData("future_visit")]
    [InlineData("market_credit")]
    [InlineData("forged_success")]
    public void ResealedTamperingIsRejectedWithoutChangingTheActiveWorld(string mutation)
    {
        var world = World();
        var snapshot = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        var root = JObject.Parse(saved);
        var plan = root["Identity"]!["Commission"]!;
        var history = root["CommissionHistory"]!;
        var remote = snapshot.Ports.Keys.First(p => p != snapshot.Ships[Ship].DockedPortId);
        switch (mutation)
        {
            case "schema": root["SchemaVersion"] = 1; break;
            case "missing_plan": ((JObject)root["Identity"]!).Remove("Commission"); break;
            case "missing_history": root.Remove("CommissionHistory"); break;
            case "version": plan["Version"] = 99; break;
            case "world": plan["Seed"] = -42; break;
            case "signature": plan["Signature"] = new string('0', 64); break;
            case "goal": plan["Goals"]![0]!["Id"] = "unknown_goal"; break;
            case "template": plan["Goals"]![0]!["TemplateId"] = "unknown_goal"; break;
            case "parameters": plan["Goals"]![2]!["CanonicalParameters"] = "forged_target"; break;
            case "ship_owner": history["Ships"]![0]!["ShipId"] = snapshot.Ships.Values.First(s => !s.IsPlayer).Id; break;
            case "ship_order": history["Ships"]![0]!["AcquisitionOrder"] = 1; break;
            case "ghost_visit": history["Ships"]![0]!["Visits"]![remote] = 0; break;
            case "future_visit": history["Ships"]![0]!["Visits"]![remote] = 999; break;
            case "market_credit": ((JArray)history["Markets"]!).Add(new JObject
                { ["PortId"] = remote, ["GoodId"] = "bread", ["PurchasedQuantity"] = 0, ["SoldQuantity"] = 10000 }); break;
            case "forged_success": root["Challenge"]!["Status"] = (int)ChallengeStatus.Succeeded;
                root["Challenge"]!["Produced"] = 100; root["Challenge"]!["CompletedDay"] = 0; break;
        }
        Rechecksum(root);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), root.ToString(Formatting.None)));
        Assert.Same(snapshot, world.Snapshot());
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    private static long Balance(RegionalWorldState world, string port, string good) =>
        world.Snapshot().CommissionHistory!.Markets.Single(m => m.PortId == port && m.GoodId == good).NetQuantity;

    private static void Sail(RegionalWorldState world, string destination)
    {
        if (world.Snapshot().Ships[Ship].DockedPortId == destination) return;
        world.Depart(Ship, destination);
        while (world.Snapshot().Ships[Ship].DockedPortId == null) world.AdvanceDay();
    }

    private static void AdvanceBoth(RegionalWorldState first, RegionalWorldState second)
    {
        Assert.Equal(Events(first.AdvanceDay()), Events(second.AdvanceDay()));
        Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(second));
    }

    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades,
        result.Production, result.Consumption, result.Movements, result.PrivateProduction,
        result.Construction, result.Transfers, result.Development
    });

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
}
