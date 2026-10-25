using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class Task22SaveTests
{
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private const string Ship = EnterpriseScenario.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World(bool commission = true, string charter = RunScenario.IndustrialCharter,
        CompanyStorageRules storage = CompanyStorageRules.Pooled)
    {
        var draft = GrandCommissionDraft.Generate(42);
        return RunScenario.Create(Content(), new RunIdentity(commission ? GrandCommission.ChallengeId : null,
            charter, RunWorldMode.ExpandingEconomy, 42, RunDifficulty.SlowFoundations,
            livingPricingMode: LivingPricingMode.Coverage,
            commission: commission ? draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId)) : null,
            companyStorageRules: storage));
    }

    [Fact]
    public void HistoricalSchemaTwoBytesAndFacilitySemanticsRemainUnchanged()
    {
        // Captured before Task 2.2 from the eafb452 Release assembly, using actual automated days.
        var world = World(storage: CompanyStorageRules.Legacy);
        world.SetAutomationEnabled(Ship, true);
        for (var day = 0; day < 11; day++) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        Assert.Equal("dfd7505f32993e64881cba8cd74462d6ecf822c4a967d560181d18402fc016dd", Hash(saved));
        var json = JObject.Parse(saved);
        Assert.Equal(2, (int)json["SchemaVersion"]!);
        Assert.Null(json["Identity"]!["CompanyStorageRules"]);
        Assert.Null(json["CompanyStockpiles"]);
        Assert.All(json["Facilities"]!, f => Assert.Null(f["SharedStockpileId"]));
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.False(restored.Snapshot().UsesPooledCompanyStorage);
        Assert.Empty(restored.Snapshot().CompanyStockpiles);
        Assert.Contains("facility:" + RunScenario.StarterBakery, restored.Snapshot().Accounting.Stores.Keys);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        AdvanceBoth(world, restored);
    }

    [Theory]
    [InlineData(false, RunScenario.MerchantCharter)]
    [InlineData(false, RunScenario.IndustrialCharter)]
    [InlineData(true, RunScenario.MerchantCharter)]
    [InlineData(true, RunScenario.IndustrialCharter)]
    [InlineData(true, RunScenario.IndependentCharter)]
    public void PooledIdentitySelectsSchemaThreeAndRecreatesExactInitialWorld(bool commission, string charter)
    {
        var world = World(commission, charter);
        var saved = WorldSaveCodec.Save(world);
        var json = JObject.Parse(saved);
        Assert.Equal(3, (int)json["SchemaVersion"]!);
        Assert.Equal((int)CompanyStorageRules.Pooled, (int)json["Identity"]!["CompanyStorageRules"]!);
        Assert.NotNull(json["CompanyStockpiles"]);
        Assert.Equal(commission, json["CommissionHistory"] != null);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(world.Snapshot().RunIdentity, restored.Snapshot().RunIdentity);
        Assert.Contains("/storage-v2", restored.Snapshot().RunIdentity!.ComparisonGroup);
        Assert.Equal(saved, WorldSaveCodec.Save(RunScenario.Create(Content(), restored.Snapshot().RunIdentity!)));
        AdvanceBoth(world, restored);
    }

    [Fact]
    public void FractionalPooledLotsPendingIndustryVoyageAndFleetResumeWithoutDuplicateStock()
    {
        var world = World();
        var port = world.Snapshot().Ships[Ship].DockedPortId!;
        var generation = ExpandingEconomyGeneration.Generate(42);
        world.ConstructFacility(Owner, port, ExpandingEconomyGeneration.MillBlueprint, "pooled_save_mill");
        Assert.Equal(TradeOutcome.Traded, world.ExecuteWarehouseTrade(Owner, "pooled_save_mill",
            new TradeCommand(TradeDirection.Buy, "grain", 52501)).WarehouseTrades.Single().Trade.Outcome);
        world.ExecuteWarehouseTrade(Owner, RunScenario.StarterBakery,
            new TradeCommand(TradeDirection.Sell, "grain", 2501));
        world.TransferGoods(Owner, Ship, RunScenario.StarterBakery, TransferDirection.WarehouseToShip, "grain", 10000);
        world.CommissionShip(Owner, world.Snapshot().Economy!.Configuration.ShipwrightPortId);
        world.Depart(Ship, generation.FoodSourcePortId);
        var before = world.Snapshot();
        var pool = Assert.Single(before.CompanyStockpiles.Values);
        Assert.Equal(40000, pool.Inventory["grain"]);
        Assert.Equal(800000, pool.Capacity);
        Assert.All(before.PrivateFacilities.Values, f => Assert.All(f.Inventory.Values, q => Assert.Equal(0, q)));
        Assert.All(before.PrivateFacilities.Values, f => Assert.Equal(pool.Id, f.SharedStockpileId));
        Assert.DoesNotContain(before.Accounting.Stores.Keys, id => id.StartsWith("facility:", StringComparison.Ordinal));
        var cargoLot = Assert.Single(before.Accounting.Stores["ship:" + Ship].Goods["grain"].Lots);
        Assert.Equal(2501, cargoLot.Start);
        Assert.Equal(FacilityConstructionStatus.UnderConstruction, before.PrivateFacilities["pooled_save_mill"].ConstructionStatus);
        Assert.Null(before.Ships[Ship].DockedPortId);
        Assert.Single(before.Economy!.ShipOrders);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        for (var day = 0; day < 12; day++)
        {
            AdvanceBoth(world, restored);
            if (day == 3)
            {
                // Save all interacting features together, not only in successive phases of the run.
                var secondShip = world.Snapshot().Ships.Values.Single(s => s.IsPlayer && s.Id != Ship).Id;
                foreach (var current in new[] { world, restored })
                {
                    Assert.Equal(ConstructionOutcome.Started, current.ConstructFacility(Owner, port,
                        ExpandingEconomyGeneration.MillBlueprint, "pooled_save_second_mill").Construction.Single().Outcome);
                    current.Depart(secondShip, generation.FoodSourcePortId);
                }
                var combined = world.Snapshot();
                Assert.Equal(2, combined.Ships.Values.Count(s => s.IsPlayer));
                Assert.Null(combined.Ships[secondShip].DockedPortId);
                Assert.Equal(FacilityConstructionStatus.UnderConstruction,
                    combined.PrivateFacilities["pooled_save_second_mill"].ConstructionStatus);
                Assert.True(combined.CompanyStockpiles.Values.Single().StoredUnits > 0);
                Assert.NotEmpty(combined.Accounting.Stores[pool.Id].Goods.Values.SelectMany(g => g.Lots));
                Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
            }
            restored = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(restored));
        }
        Assert.Equal(2, restored.Snapshot().Ships.Values.Count(s => s.IsPlayer));
        Assert.True(restored.Snapshot().PrivateFacilities["pooled_save_mill"].BatchesProduced > 0);
        Assert.True(restored.Snapshot().PrivateFacilities[RunScenario.StarterBakery].BatchesProduced > 1);
        Assert.Equal(world.Snapshot().CompanyValue(Owner).Total, restored.Snapshot().CompanyValue(Owner).Total);
        Assert.Equal(world.Snapshot().Accounting.Companies[Owner].HeldInventoryCost,
            restored.Snapshot().Accounting.Companies[Owner].HeldInventoryCost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PooledTerminalSaveRetainsFrozenLifecycleAndFreshRetry(bool abandon)
    {
        var world = World();
        if (abandon) world.AbandonChallenge();
        else while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(abandon ? ChallengeStatus.Abandoned : ChallengeStatus.Failed, restored.Snapshot().Challenge!.Status);
        Assert.Throws<InvalidOperationException>(() => restored.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => restored.ExecuteWarehouseTrade(Owner,
            RunScenario.StarterBakery, new TradeCommand(TradeDirection.Buy, "grain", 1)));
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        var retry = RunScenario.Create(Content(), restored.Snapshot().RunIdentity!);
        Assert.Equal(WorldSaveCodec.Save(World()), WorldSaveCodec.Save(retry));
    }

    [Fact]
    public void GenuinePooledCommissionVictoryRestoresFrozenWithoutReissuingMoneyOrProgress()
    {
        var plan = GrandCommissionDraft.Generate(42).Resolve(new[]
            { "all_harbors", "timber_chain", "royal_exporter", "royal_institution" });
        var world = RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.IndependentCharter, RunWorldMode.ExpandingEconomy, 42, commission: plan,
            companyStorageRules: CompanyStorageRules.Pooled));
        var generation = ExpandingEconomyGeneration.Generate(42);
        void Trade(TradeDirection direction, string good, int quantity) =>
            Assert.Equal(TradeOutcome.Traded, world.ExecuteTrade(Ship, world.Snapshot().Ships[Ship].DockedPortId!,
                new TradeCommand(direction, good, quantity * 10000)).Trades.Single().Trade.Outcome);
        Trade(TradeDirection.Sell, "grain", 4);
        Trade(TradeDirection.Sell, "logs", 2);
        world.ConstructFacility(Owner, generation.RecoveryPortId, ExpandingEconomyGeneration.LumberBlueprint, "saved_camp");
        world.ConstructFacility(Owner, generation.ProcessingPortId, EnterpriseScenario.SawmillBlueprint, "saved_sawmill");
        world.ExecuteWarehouseTrade(Owner, "saved_sawmill", new TradeCommand(TradeDirection.Buy, "logs", 80000));
        world.ConstructFacility(Owner, generation.FurniturePortId, ExpandingEconomyGeneration.SilverMineBlueprint, "saved_mine");
        world.CommissionMint(Owner);
        void Sail(string port)
        {
            world.Depart(Ship, port);
            while (world.Snapshot().Ships[Ship].DockedPortId == null)
            {
                world.AdvanceDay();
                if (world.Snapshot().Challenge!.Status != ChallengeStatus.Active) break;
                foreach (var facility in world.Snapshot().PrivateFacilities.Values.ToArray())
                    if (facility.Enabled && facility.BatchesProduced >= (facility.Id == "saved_sawmill" ? 4 : 1))
                        world.SetFacilityEnabled(Owner, facility.Id, false);
            }
        }
        Trade(TradeDirection.Buy, "bread", 4);
        Sail(generation.RecoveryPortId);
        Trade(TradeDirection.Sell, "bread", 4);
        Sail(generation.FurniturePortId);
        world.TransferGoods(Owner, Ship, "saved_mine", TransferDirection.WarehouseToShip, "silver", 10000);
        Trade(TradeDirection.Buy, "furniture", 1);
        Sail(generation.ProcessingPortId);
        world.FulfillExternalContract(Ship, "royal_furniture", 10000);
        world.MintSilver(Ship);
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(4, GrandCommission.Progress(world.Snapshot()).CurrentlyMetCount);
        Assert.Equal(800000, world.Snapshot().Economy!.MintedCurrency);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(3, restored.Snapshot().CompanyStockpiles.Count);
        Assert.Throws<InvalidOperationException>(() => restored.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => restored.MintSilver(Ship));
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
    }

    [Theory]
    [InlineData("schema_two")]
    [InlineData("missing_rules")]
    [InlineData("unknown_rules")]
    [InlineData("missing_pools")]
    [InlineData("extra_pool")]
    [InlineData("wrong_owner")]
    [InlineData("wrong_port")]
    [InlineData("wrong_capacity")]
    [InlineData("duplicate_inventory")]
    [InlineData("wrong_reference")]
    [InlineData("fifo_quantity")]
    [InlineData("duplicate_fifo")]
    public void ResealedPooledStorageCorruptionRejectsWithoutReplacingTheActiveWorld(string corruption)
    {
        var world = World();
        var before = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        var root = JObject.Parse(saved);
        var pools = (JArray)root["CompanyStockpiles"]!;
        var pool = pools[0]!;
        var facility = root["Facilities"]![0]!;
        var stores = (JArray)root["Stores"]!;
        var fifo = stores.Single(s => (string)s["Id"]! == (string)pool["Id"]!);
        switch (corruption)
        {
            case "schema_two": root["SchemaVersion"] = 2; break;
            case "missing_rules": ((JObject)root["Identity"]!).Remove("CompanyStorageRules"); break;
            case "unknown_rules": root["Identity"]!["CompanyStorageRules"] = 8; break;
            case "missing_pools": root.Remove("CompanyStockpiles"); break;
            case "extra_pool": pools.Add(pool.DeepClone()); break;
            case "wrong_owner": pool["OwnerId"] = "grain_company"; break;
            case "wrong_port": pool["PortId"] = before.Ports.Keys.First(p => p != (string)pool["PortId"]!); break;
            case "wrong_capacity": pool["Capacity"] = (int)pool["Capacity"]! + 10000; break;
            case "duplicate_inventory": facility["Inventory"]!["flour"] = 20000; break;
            case "wrong_reference": facility["SharedStockpileId"] = "company:grain_company:" + (string)pool["PortId"]!; break;
            case "fifo_quantity": fifo["Goods"]![0]!["Lots"]![0]!["Quantity"] = 1; break;
            case "duplicate_fifo": var duplicate = fifo.DeepClone();
                duplicate["Id"] = "facility:" + (string)facility["Id"]!; stores.Add(duplicate); break;
        }
        Rechecksum(root);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), root.ToString(Formatting.None)));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    private static void AdvanceBoth(RegionalWorldState first, RegionalWorldState second)
    {
        Assert.Equal(Events(first.AdvanceDay()), Events(second.AdvanceDay()));
        Assert.Equal(WorldSaveCodec.Save(first), WorldSaveCodec.Save(second));
    }

    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades,
        result.WarehouseTrades, result.Production, result.Consumption, result.Movements,
        result.PrivateProduction, result.Construction, result.Transfers, result.Development
    });

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

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
        root["StateFingerprint"] = Hash(Canonical(root).ToString(Formatting.None));
    }
}
