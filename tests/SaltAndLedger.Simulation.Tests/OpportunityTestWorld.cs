using System;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

internal static class OpportunityTestWorld
{
    internal const string Owner = WorldIds.PlayerOwnerId;
    internal const string Ship = WorldIds.PlayerShipId;
    internal static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    internal static WorldGenerationConfiguration Configuration(Action<JObject>? configure = null)
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["npc"]!["minimumCount"] = 0;
        json["npc"]!["maximumCount"] = 0;
        json["npc"]!["tradersPerTownPermille"] = 0;
        configure?.Invoke(json);
        return WorldGenerationConfiguration.Parse(json.ToString());
    }

    internal static RegionalWorldState Create(int seed = 42, WorldGenerationConfiguration? configuration = null) =>
        RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter, seed: seed,
            generationConfiguration: configuration ?? Configuration()));

    internal static void Sail(RegionalWorldState world, string destination, Action? afterDay = null)
    {
        if (world.Snapshot().Ships[Ship].DockedPortId == destination) return;
        world.Depart(Ship, destination);
        var arrivalLimit = checked(world.Snapshot().Day + 100);
        while (world.Snapshot().Ships[Ship].DockedPortId == null)
        {
            Assert.True(world.Snapshot().Day < arrivalLimit, "A configured sea journey must finish within its bounded path duration.");
            world.AdvanceDay();
            afterDay?.Invoke();
        }
    }

    internal static void Buy(RegionalWorldState world, string port, string good, int quantity)
    {
        Sail(world, port);
        Assert.Equal(TradeOutcome.Traded, Assert.Single(world.ExecuteTrade(Ship, port,
            new TradeCommand(TradeDirection.Buy, good, quantity)).Trades).Trade.Outcome);
    }

    internal static void Audit(RegionalWorldSnapshot snapshot, BigInteger initialCash)
    {
        Assert.Equal(initialCash + snapshot.Economy!.MintedCurrency, snapshot.TotalCash);
        foreach (var company in snapshot.Accounting.Companies.Values)
        {
            Assert.Equal(company.InventoryPurchases,
                company.CostOfGoodsSold + company.HeldInventoryCost + company.MintMaterialCost + company.ProjectMaterialCost);
            foreach (var good in company.HeldInventoryQuantities.Keys
                .Concat(company.PurchasedQuantities.Keys).Concat(company.SurrenderedQuantities.Keys).Distinct())
            {
                long Amount(System.Collections.Generic.IReadOnlyDictionary<string, long> quantities) =>
                    quantities.TryGetValue(good, out var quantity) ? quantity : 0;
                Assert.Equal(Amount(company.GrantedQuantities) + Amount(company.PurchasedQuantities) +
                    Amount(company.ProducedQuantities) - Amount(company.ConsumedQuantities) -
                    Amount(company.SoldQuantities) - Amount(company.SurrenderedQuantities),
                    Amount(company.HeldInventoryQuantities));
            }
        }
        foreach (var store in snapshot.Accounting.Stores.Values)
        {
            var inventory = store.StoreId.StartsWith("ship:", StringComparison.Ordinal)
                ? snapshot.Ships[store.StoreId.Substring(5)].Cargo
                : snapshot.CompanyStockpiles[store.StoreId].Inventory;
            foreach (var good in inventory)
                Assert.Equal(good.Value, store.GetQuantity(good.Key));
            foreach (var good in store.Goods)
                Assert.Equal(inventory[good.Key], store.GetQuantity(good.Key));
        }
    }

    internal static RegionalWorldState Reload(RegionalWorldState world)
    {
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        return restored;
    }
}
