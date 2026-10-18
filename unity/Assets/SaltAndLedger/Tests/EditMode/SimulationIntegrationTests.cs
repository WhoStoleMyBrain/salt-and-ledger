using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEditor;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class SimulationIntegrationTests
    {
        [Test]
        public void StagedCanonicalContentRunsThreeBatchesAndRejectsFourthAtomically()
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var content = ContentLoader.Load(asset.text);
            var inventory = new Inventory(content, new Dictionary<string, int> { ["logs"] = 6 });

            Assert.That(content.SchemaVersion, Is.EqualTo(1));
            Assert.That(content.Goods.Count, Is.EqualTo(2));
            Assert.That(content.Buildings.Count, Is.EqualTo(1));
            Assert.That(content.Recipes.Count, Is.EqualTo(1));
            Assert.That(inventory.GetQuantity("planks"), Is.Zero);

            for (var batch = 1; batch <= 3; batch++)
            {
                Assert.That(Production.TryRunBatch(inventory, "sawmill", "saw_planks"), Is.True);
                Assert.That(inventory.GetQuantity("logs"), Is.EqualTo(6 - 2 * batch));
                Assert.That(inventory.GetQuantity("planks"), Is.EqualTo(batch));
            }

            var beforeRejection = inventory.Snapshot();
            Assert.That(Production.TryRunBatch(inventory, "sawmill", "saw_planks"), Is.False);
            Assert.That(inventory.Snapshot(), Is.EquivalentTo(beforeRejection));
        }

        [Test]
        public void StagedWorldSchedulesSharedInventoryByInstanceId()
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var world = new WorldState(ContentLoader.Load(asset.text), "harbor", new[]
            {
                new BuildingInstance("sawmill_b", "sawmill", "saw_planks"),
                new BuildingInstance("sawmill_a", "sawmill", "saw_planks")
            }, new Dictionary<string, int> { ["logs"] = 2 });

            var step = world.Step();

            Assert.That(step.Tick, Is.Zero);
            Assert.That(world.Tick, Is.EqualTo(1L));
            Assert.That(step.ProductionAttempts.Select(attempt => attempt.BuildingInstanceId),
                Is.EqualTo(new[] { "sawmill_a", "sawmill_b" }));
            Assert.That(step.ProductionAttempts.Select(attempt => attempt.Outcome),
                Is.EqualTo(new[] { ProductionOutcome.Produced, ProductionOutcome.ResourceShortage }));
            Assert.That(step.After.Inventory["logs"], Is.Zero);
            Assert.That(step.After.Inventory["planks"], Is.EqualTo(1));
        }

        [Test]
        public void StagedMarketTransfersGoodsAndCashBeforeProduction()
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var world = new WorldState(ContentLoader.Load(asset.text), "harbor",
                new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") },
                market: new MarketConfiguration(new[] { new MarketQuote("logs", 2, 3), new MarketQuote("planks", 4, 5) },
                    20, "trader", 7, new Dictionary<string, int> { ["logs"] = 2 }));

            var sold = world.Step(trades: new[] { new TradeCommand(TradeDirection.Sell, "logs", 2) });
            Assert.That(sold.TradeAttempts[0].Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(sold.TradeAttempts[0].TotalAmount, Is.EqualTo(4L));
            Assert.That(sold.ProductionAttempts[0].Outcome, Is.EqualTo(ProductionOutcome.Produced));
            Assert.That(sold.After.Inventory["logs"], Is.Zero);
            Assert.That(sold.After.Inventory["planks"], Is.EqualTo(1));
            Assert.That(sold.After.Trading.MarketCash, Is.EqualTo(16L));
            Assert.That(sold.After.Trading.TraderCash, Is.EqualTo(11L));
            var bought = world.Step(trades: new[] { new TradeCommand(TradeDirection.Buy, "planks", 1) });
            Assert.That(bought.TradeAttempts[0].Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(bought.After.Inventory["planks"], Is.Zero);
            Assert.That(bought.After.Trading.TraderInventory["logs"], Is.Zero);
            Assert.That(bought.After.Trading.TraderInventory["planks"], Is.EqualTo(1));
            Assert.That(bought.After.Trading.MarketCash, Is.EqualTo(21L));
            Assert.That(bought.After.Trading.TraderCash, Is.EqualTo(6L));
            Assert.That(sold.After.Trading.TraderCash, Is.EqualTo(11L), "Earlier snapshots must remain detached.");
        }

        [TestCase(InventoryPricingModel.Linear, 57L, 20L)]
        [TestCase(InventoryPricingModel.Scarcity, 72L, 35L)]
        public void StagedDynamicQuoteMatchesBulkExecutionAndReversal(InventoryPricingModel model, long total, long lastPrice)
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var world = new WorldState(ContentLoader.Load(asset.text), "harbor", new BuildingInstance[0],
                new Dictionary<string, int> { ["logs"] = 3 }, market: new MarketConfiguration(new[]
                {
                    new MarketQuote("logs", new InventoryPricing(model, 10, 10, 10, 1, 100, 1))
                }, 100, "trader", 1000));
            var before = world.Snapshot();
            var command = new TradeCommand(TradeDirection.Buy, "logs", 3);
            var preview = before.PreviewTrade(command);
            Assert.That(before.GetMarketQuote("logs").Ask, Is.EqualTo(18L));
            Assert.That(preview.Price.FirstMarginalPrice, Is.EqualTo(18L));
            Assert.That(preview.Price.LastMarginalPrice, Is.EqualTo(lastPrice));
            Assert.That(preview.Price.TotalAmount, Is.EqualTo(total));
            Assert.That(world.Snapshot(), Is.SameAs(before));
            var bought = world.Step(trades: new[] { command });
            Assert.That(bought.TradeAttempts[0].Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(bought.TradeAttempts[0].TotalAmount, Is.EqualTo(preview.TotalAmount));
            Assert.That(bought.TradeAttempts[0].ExecutedQuantity, Is.EqualTo(3));
            Assert.That(bought.After.GetMarketQuote("logs").Ask, Is.Null);
            Assert.That(bought.After.Trading.TraderCash, Is.EqualTo(1000L - total));
            var returned = world.Step(trades: new[] { new TradeCommand(TradeDirection.Sell, "logs", 3) });
            Assert.That(returned.After.Inventory["logs"], Is.EqualTo(3));
            Assert.That(returned.After.Trading.TraderInventory["logs"], Is.Zero);
            Assert.That(returned.After.Trading.TraderCash, Is.EqualTo(994L));
            Assert.That(returned.After.Trading.MarketCash, Is.EqualTo(106L));
            Assert.That(before.GetMarketQuote("logs").Ask, Is.EqualTo(18L));
        }

        [Test]
        public void SimulationResolvesOneNewtonsoftAssemblyFromTheUnityPackage()
        {
            var simulationReference = typeof(ContentLoader).Assembly.GetReferencedAssemblies()
                .Single(assembly => assembly.Name == "Newtonsoft.Json");
            var jsonAssembly = typeof(JsonReader).Assembly;
            Assert.That(jsonAssembly.GetName().FullName, Is.EqualTo(simulationReference.FullName));
            Assert.That(UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                .Count(assembly => assembly.GetName().Name == "Newtonsoft.Json"), Is.EqualTo(1));

            const string jsonAssetPath = "Packages/com.unity.nuget.newtonsoft-json/Runtime/Newtonsoft.Json.dll";
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(jsonAssetPath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo("com.unity.nuget.newtonsoft-json"));
            Assert.That(package.version, Is.EqualTo("3.2.2"));
            var importer = AssetImporter.GetAtPath(jsonAssetPath) as PluginImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetCompatibleWithEditor(), Is.True);
            var packageDll = Path.Combine(package.resolvedPath, "Runtime", "Newtonsoft.Json.dll");
            var packageAssembly = AssemblyName.GetAssemblyName(packageDll);
            Assert.That(packageAssembly.FullName, Is.EqualTo(jsonAssembly.GetName().FullName));
            using (var hash = SHA256.Create())
            {
                Assert.That(hash.ComputeHash(File.ReadAllBytes(jsonAssembly.GetLoadedAssemblyPath())),
                    Is.EqualTo(hash.ComputeHash(File.ReadAllBytes(packageDll))),
                    "The loaded Newtonsoft assembly must be the binary supplied by Unity's pinned package.");
            }
        }
    }
}
