using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;
using Xunit.Abstractions;

namespace SaltAndLedger.Simulation.Tests;

public sealed class NpcCompanyTests
{
    private readonly ITestOutputHelper output;
    public NpcCompanyTests(ITestOutputHelper output) => this.output = output;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World(int seed = 42, WorldGenerationConfiguration? config = null, bool reverse = false,
        RunDifficulty difficulty = RunDifficulty.SlowFoundations) => ExpandingEconomyScenario.Create(Content(),
        new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, seed,
            difficulty: difficulty, companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1, economicRulesVersion: 1,
            generationConfiguration: config), reverse);
    private static WorldGenerationConfiguration Configure(Action<JObject> change)
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson()); change(json);
        return WorldGenerationConfiguration.Parse(json.ToString(), Content().ForExpandingEconomy());
    }

    [Theory]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(99)]
    public void OrdinaryAutonomyPaysForShipsFactoriesInputsAndDistributionAndResumesExactly(int seed)
    {
        var world = World(seed); var initial = world.Snapshot();
        Assert.True(initial.Companies.Values.Where(c => c.Npc != null).Select(c => c.Npc!.Profile).Distinct().Count() >= 3);
        var replay = World(seed, reverse: true);
        var transferred = false; var privateSales = false; var factoryInputs = false;
        for (var day = 0; day < 100; day++)
        {
            var result = world.AdvanceDay(); replay.AdvanceDay();
            transferred |= result.Transfers.Any(t => t.Outcome == TransferOutcome.Transferred);
            privateSales |= result.WarehouseTrades.Any(t => t.Trade.Direction == TradeDirection.Sell && t.Trade.Outcome == TradeOutcome.Traded);
            factoryInputs |= result.After.Accounting.Companies.Values.Any(c => c.ConsumedQuantities.Values.Any(q => q > 0));
            Assert.Equal(WorldSaveCodec.Fingerprint(world.Snapshot()), WorldSaveCodec.Fingerprint(replay.Snapshot()));
            if (day == 0 || day == 1 || day == 4 || day == 32 || day == 79)
            {
                var saved = WorldSaveCodec.Save(world);
                replay = WorldSaveCodec.Load(Content(), saved);
                Assert.Equal(saved, WorldSaveCodec.Save(replay));
            }
        }
        var state = world.Snapshot();
        Assert.Contains(state.Companies.Values, c => c.Npc != null && state.Ships.Values.Count(s => s.OwnerId == c.Id) > 1);
        Assert.Contains(state.PrivateFacilities.Values, f => state.Companies[f.OwnerId].Npc != null && f.BatchesProduced > 0 && !f.IsStarterGrant);
        Assert.True(transferred); Assert.True(privateSales); Assert.True(factoryInputs);
        Assert.Equal(initial.TotalCash, state.TotalCash);
        foreach (var c in state.Companies.Values.Where(c => c.Npc != null))
        {
            var finance = state.Accounting.Companies[c.Id];
            Assert.Equal(0, finance.MintReceipts); Assert.Equal(0, finance.PublicCommissionExpenditure);
            Assert.Equal(0, finance.RewardReceipts);
            Assert.All(state.Ships.Values.Where(s => s.OwnerId == c.Id), s => Assert.False(s.IsPlayer));
            output.WriteLine($"{seed} {c.Npc!.Name}: ships={state.Ships.Values.Count(s => s.OwnerId == c.Id)} sites={state.PrivateFacilities.Values.Count(f => f.OwnerId == c.Id)} capital={finance.ConstructionExpenditure} produced={finance.ProducedQuantities.Values.Sum()} sold={finance.SoldQuantities.Values.Sum()}");
        }
        Assert.NotEqual(initial.Ports.Values.Sum(p => p.Inventory["bread"]), state.Ports.Values.Sum(p => p.Inventory["bread"]));
    }

    [Fact]
    public void NoFundsMeansNoInvestmentAndZeroCompaniesRemainsSupported()
    {
        var poor = World(config: Configure(j => { j["npc"]!["startingCash"] = new JObject { ["min"] = 0, ["max"] = 0 }; }));
        for (var day = 0; day < 80; day++) poor.AdvanceDay();
        Assert.Empty(poor.Snapshot().PrivateFacilities);
        Assert.Empty(poor.Snapshot().Economy!.ShipOrders);
        Assert.All(poor.Snapshot().Companies.Values.Where(c => c.Npc != null), c =>
        {
            Assert.Equal(0, c.Cash); Assert.Equal(0, c.Npc!.ShipsOrdered);
            Assert.Equal(0, poor.Snapshot().Accounting.Companies[c.Id].ConstructionExpenditure);
        });
        var empty = World(config: Configure(j => { j["npc"]!["minimumCount"] = 0; j["npc"]!["maximumCount"] = 0; }));
        Assert.Single(empty.Snapshot().Companies); empty.AdvanceDay();
        Assert.Equal(WorldSaveCodec.Save(empty), WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(empty))));
    }

    [Theory]
    [InlineData(42, 12)]
    [InlineData(-734282136, 24)]
    public void HorizonConservesAccountsAndCultureAndEnumerationIndependentContinuation(int seed, int towns)
    {
        var config = Configure(j => j["townCount"] = new JObject { ["min"] = towns, ["max"] = towns });
        var timer = Stopwatch.StartNew(); var world = World(seed, config); var cash = world.Snapshot().TotalCash;
        for (var day = 0; day < 720; day++)
        {
            world.AdvanceDay();
            if (day == 15 || day == 359 || day == 718)
            {
                var saved = WorldSaveCodec.Save(world); var old = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                    var loaded = WorldSaveCodec.Load(Content(), saved); Assert.Equal(saved, WorldSaveCodec.Save(loaded));
                    world.AdvanceDay(); loaded.AdvanceDay(); day++;
                    Assert.Equal(WorldSaveCodec.Fingerprint(world.Snapshot()), WorldSaveCodec.Fingerprint(loaded.Snapshot()));
                }
                finally { CultureInfo.CurrentCulture = old; }
            }
        }
        Assert.Equal(cash, world.Snapshot().TotalCash);
        output.WriteLine($"{towns} towns, 720 days: {timer.ElapsedMilliseconds} ms, {world.Snapshot().Ships.Count} ships, {world.Snapshot().PrivateFacilities.Count} sites");
    }

    [Fact]
    public void MultipleInitialShipsAndForeignAssetAndRoyalBoundaries()
    {
        var config = Configure(j => j["npc"]!["initialShips"] = 2);
        var world = World(config: config); var state = world.Snapshot();
        Assert.All(state.Companies.Values.Where(c => c.Npc != null), c => Assert.Equal(2, state.Ships.Values.Count(s => s.OwnerId == c.Id)));
        world.AdvanceDay(); state = world.Snapshot();
        var site = state.PrivateFacilities.Values.First();
        Assert.Throws<InvalidOperationException>(() => world.ConfigureProduction(WorldIds.PlayerOwnerId, site.Id, true, 0, ProductionPriority.Normal));
        Assert.Same(state, world.Snapshot());
        Assert.Throws<InvalidOperationException>(() => world.CommissionMint(site.OwnerId)); Assert.Same(state, world.Snapshot());
        var ship = state.Ships.Values.First(s => s.OwnerId == site.OwnerId);
        Assert.Throws<InvalidOperationException>(() => state.PreviewMintExchange(ship.Id));
    }
    [Theory]
    [InlineData("plan")]
    [InlineData("cooldown")]
    [InlineData("hull")]
    [InlineData("controller")]
    public void ResealedImpossibleCompanyStateIsRejected(string corruption)
    {
        var world = World(); world.AdvanceDay();
        var original = world.Snapshot();
        var saved = JObject.Parse(WorldSaveCodec.Save(world));
        var company = saved["Companies"]!.First(c => c["Npc"]!.Type != JTokenType.Null);
        switch (corruption)
        {
            case "plan": company["Npc"]!["GoodId"] = "silver"; break;
            case "cooldown": company["Npc"]!["NextReviewDay"] = 0; break;
            case "hull": company["Npc"]!["ShipsOrdered"] = 7; break;
            case "controller": company["Npc"] = null; break;
        }
        saved["StateFingerprint"] = "";
        saved["StateFingerprint"] = WorldSaveCodec.Fingerprint(saved);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), saved.ToString(Newtonsoft.Json.Formatting.None)));
        Assert.Same(original, world.Snapshot());
    }

    [Fact]
    public void LateLotSequenceOverflowRollsBackCompanyInvestmentsPoliciesAndCooldowns()
    {
        var json = JObject.Parse(WorldSaveCodec.Save(World()));
        json["NextLotId"] = long.MaxValue;
        json["StateFingerprint"] = ""; json["StateFingerprint"] = WorldSaveCodec.Fingerprint(json);
        var world = WorldSaveCodec.Load(Content(), json.ToString(Newtonsoft.Json.Formatting.None));
        var before = world.Snapshot();
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Empty(before.PrivateFacilities); Assert.Empty(before.Economy!.ShipOrders);
        Assert.All(before.Companies.Values.Where(c => c.Npc != null), c => Assert.Equal(0, c.Npc!.NextReviewDay));
    }

    [Fact]
    public void ConfigurationRejectsImpossibleCompanyBounds()
    {
        Assert.Throws<WorldGenerationException>(() => Configure(j => j["npc"]!["initialShips"] = 4));
        Assert.Throws<WorldGenerationException>(() => Configure(j => j["npc"]!["maximumShips"] = 9));
        Assert.Throws<WorldGenerationException>(() => Configure(j => j["npc"]!["investmentIntervalDays"] = 0));
        Assert.Throws<WorldGenerationException>(() => Configure(j =>
        { j["npc"]!["merchantWeight"] = 0; j["npc"]!["industrialWeight"] = 0; j["npc"]!["diversifiedWeight"] = 0; }));
    }

    [Fact]
    public void CommercialShipOrdersUseIdenticalPaymentsMaterialsDelayAndPendingFleetLimit()
    {
        var world = World(config: Configure(j => j["npc"]!["maximumShips"] = 2));
        var before = world.Snapshot(); var owner = before.Companies.Values.First(c => c.Npc != null).Id;
        var port = before.Economy!.Configuration.ShipwrightPortId;
        var npc = before.PreviewShipCommission(owner, port); var player = before.PreviewShipCommission(WorldIds.PlayerOwnerId, port);
        Assert.Equal(DevelopmentOutcome.Started, npc.Outcome);
        Assert.Equal((player.Payment, player.Quantity, player.HouseholdAmount, player.CompletionDay),
            (npc.Payment, npc.Quantity, npc.HouseholdAmount, npc.CompletionDay));
        var result = world.CommissionShip(owner, port); var after = result.After;
        Assert.Equal(before.Companies[owner].Cash - npc.Payment, after.Companies[owner].Cash);
        Assert.Equal(before.Ports[port].Inventory["planks"] - npc.Quantity, after.Ports[port].Inventory["planks"]);
        Assert.Equal(before.Ports[port].MarketCash + npc.Payment - npc.HouseholdAmount, after.Ports[port].MarketCash);
        Assert.Equal(before.Ports[port].City!.HouseholdCash + npc.HouseholdAmount, after.Ports[port].City!.HouseholdCash);
        Assert.Equal(before.TotalCash, after.TotalCash);
        Assert.Equal(DevelopmentOutcome.FleetLimit, after.PreviewShipCommission(owner, port).Outcome);
        Assert.Same(after, world.CommissionShip(owner, port).After);
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world))));
        for (var day = 0; day < 4; day++) world.AdvanceDay();
        Assert.False(world.Snapshot().Ships[npc.AssetId!].IsPlayer);
        Assert.Equal(owner, world.Snapshot().Ships[npc.AssetId!].OwnerId);
    }

    [Fact]
    public void MissingHullMaterialRejectsNpcInvestmentWithoutChangingAnyState()
    {
        var shipyard = ExpandingEconomyGeneration.Generate(42).ShipyardPortId;
        var config = Configure(j => j["institutions"]!["startingPortIndex"] = int.Parse(shipyard.Substring(5), CultureInfo.InvariantCulture) - 1);
        var world = World(config: config); var initial = world.Snapshot(); var port = initial.Economy!.Configuration.ShipwrightPortId;
        var owner = initial.Companies.Values.First(c => c.Npc != null).Id;
        var player = initial.Ships[WorldIds.PlayerShipId];
        // Spend real player cash to remove the shipwright's finite Planks before the company orders.
        if (player.DockedPortId != port) world.Depart(player.Id, port);
        while (world.Snapshot().Ships[player.Id].DockedPortId == null) world.AdvanceDay();
        var stock = world.Snapshot().Ports[port].Inventory["planks"];
        if (stock > 0) world.ExecuteTrade(player.Id, port, new TradeCommand(TradeDirection.Buy, "planks", stock));
        var before = world.Snapshot();
        Assert.Equal(DevelopmentOutcome.InsufficientStock, before.PreviewShipCommission(owner, port).Outcome);
        Assert.Same(before, world.CommissionShip(owner, port).After);
    }

}
