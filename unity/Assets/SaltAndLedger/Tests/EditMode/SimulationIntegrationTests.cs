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
            Assert.That(content.Buildings.Count, Is.EqualTo(2));
            Assert.That(content.Recipes.Count, Is.EqualTo(2));
            Assert.That(content.GetRecipe("saw_planks").Kind, Is.EqualTo(RecipeKind.Transformation));
            Assert.That(content.GetRecipe("harvest_logs").Kind, Is.EqualTo(RecipeKind.Extraction));
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

        [TestCase(InventoryPricingModel.Linear, 35L)]
        [TestCase(InventoryPricingModel.Scarcity, 50L)]
        public void StagedShipTransportsCargoAndTradesAtDestination(InventoryPricingModel model, long saleRevenue)
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var world = new MaritimeWorldState(ContentLoader.Load(asset.text), new[]
            {
                new PortConfiguration("northport", new[] { new MarketQuote("logs", 2, 3) }, 100,
                    new BuildingInstance[0], new Dictionary<string, int> { ["logs"] = 2 }),
                new PortConfiguration("southhaven", new[]
                    { new MarketQuote("logs", new InventoryPricing(model, 10, 10, 10, 1, 100, 1)) }, 100,
                    new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") })
            }, "trader", 100, new ShipConfiguration("sloop", 2, "northport"), 2);
            var initial = world.Snapshot();
            var departed = world.Step(trades: new[] { new PortTradeCommand("northport", TradeDirection.Buy, "logs", 2) },
                sailCommands: new[] { new SailCommand("southhaven") });
            Assert.That(departed.After.Ship.DockedPortId, Is.Null);
            Assert.That(departed.After.Ship.RemainingTravelTicks, Is.EqualTo(1));
            Assert.That(departed.After.Ship.Cargo["logs"], Is.EqualTo(2));
            Assert.That(departed.After.TraderCash, Is.EqualTo(94L));
            var preview = departed.After.PreviewTrade("southhaven", new TradeCommand(TradeDirection.Sell, "logs", 2));
            Assert.That(preview.TotalAmount, Is.EqualTo(saleRevenue), "Remote preview is hypothetical, not permission to trade.");
            Assert.Throws<System.InvalidOperationException>(() => world.Step(trades:
                new[] { new PortTradeCommand("southhaven", TradeDirection.Sell, "logs", 2) }));
            Assert.That(world.Snapshot(), Is.SameAs(departed.After));
            var arrived = world.Step();
            Assert.That(arrived.After.Ship.DockedPortId, Is.EqualTo("southhaven"));
            Assert.That(arrived.After.Tick, Is.EqualTo(2L));
            Assert.That(arrived.MovementEvents.Last().Kind, Is.EqualTo(ShipMovementKind.Arrival));
            var sold = world.Step(trades: new[] { new PortTradeCommand("southhaven", TradeDirection.Sell, "logs", 2) });
            Assert.That(sold.TradeAttempts[0].Trade.TotalAmount, Is.EqualTo(saleRevenue));
            Assert.That(sold.After.TraderCash, Is.EqualTo(94L + saleRevenue));
            Assert.That(sold.After.Ports["northport"].MarketCash, Is.EqualTo(106L));
            Assert.That(sold.After.Ports["southhaven"].MarketCash, Is.EqualTo(100L - saleRevenue));
            Assert.That(sold.After.Ports["southhaven"].Inventory["logs"], Is.Zero);
            Assert.That(sold.After.Ports["southhaven"].Inventory["planks"], Is.EqualTo(1));
            Assert.That(sold.After.Ship.CargoUnits, Is.Zero);
            Assert.That(initial.Ship.DockedPortId, Is.EqualTo("northport"));
            Assert.That(initial.Ports["northport"].Inventory["logs"], Is.EqualTo(2));
        }

        [TestCase(10L, 6, 219L, 990L, 891L, 2)]
        [TestCase(8L, 2, 169L, 952L, 979L, 0)]
        public void StagedPolicyCompletesTheMeasuredTradingExpedition(long buyLimit, int logs,
            long traderCash, long northCash, long southCash, int southPlanks)
        {
            var asset = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(asset, Is.Not.Null, "Run python tools/prepare_unity.py before opening Unity.");
            var world = new MaritimeWorldState(ContentLoader.Load(asset.text), new[]
            {
                new PortConfiguration("northport", new[]
                {
                    AutomationLinear("logs", 10, 12, 6, 50),
                    AutomationLinear("planks", 24, 6, 12, 60)
                }, 1000, new BuildingInstance[0], new Dictionary<string, int> { ["logs"] = 18 }),
                new PortConfiguration("southhaven", new[]
                {
                    AutomationLinear("logs", 20, 6, 12, 60),
                    AutomationLinear("planks", 12, 6, 6, 50)
                }, 1000, new[] { new BuildingInstance("sawmill_a", "sawmill", "saw_planks") },
                    new Dictionary<string, int> { ["logs"] = 2 })
            }, "trader", 100, new ShipConfiguration("ship", 6, "northport"), 2);
            var policy = new TradeRoutePolicy("northport", "southhaven",
                new RouteLeg("logs", 6, 2, buyLimit, 19), new RouteLeg("planks", 2, 2, 18, 31));
            var initial = world.Snapshot();
            var decision = RoutePolicyEvaluator.Evaluate(initial, policy);
            Assert.That(world.Snapshot(), Is.SameAs(initial));
            Assert.That(decision.SelectedQuantity, Is.EqualTo(logs));
            Assert.That(decision.Action, Is.EqualTo(AutomationAction.BuyAndDepart));
            var departed = TradeRouteAutomation.Execute(world, decision);
            Assert.That(departed.After.Ship.RemainingTravelTicks, Is.EqualTo(1));
            Assert.That(departed.After.Ship.Cargo["logs"], Is.EqualTo(logs));
            Assert.Throws<System.InvalidOperationException>(() => TradeRouteAutomation.Execute(world, decision));
            var arrived = TradeRouteAutomation.Advance(world, policy);
            Assert.That(arrived.Decision.Action, Is.EqualTo(AutomationAction.ContinueSailing));
            Assert.That(arrived.Result.After.Ship.DockedPortId, Is.EqualTo("southhaven"));
            var delivered = TradeRouteAutomation.Advance(world, policy);
            Assert.That(delivered.Result.TradeAttempts[0].Trade.ExecutedQuantity, Is.EqualTo(logs));
            Assert.That(delivered.Result.After.Ports["southhaven"].Inventory["planks"], Is.EqualTo(2));
            var remainder = AutomationRunner.Run(world, policy, 3, 3);
            Assert.That(remainder.StopReason, Is.EqualTo(AutomationStopReason.TickLimit));
            var after = remainder.After;
            Assert.That(after.Tick, Is.EqualTo(6L));
            Assert.That(after.Ship.DockedPortId, Is.EqualTo("northport"));
            Assert.That(after.Ship.CargoUnits, Is.Zero);
            Assert.That(after.TraderCash, Is.EqualTo(traderCash));
            Assert.That(after.Ports["northport"].MarketCash, Is.EqualTo(northCash));
            Assert.That(after.Ports["southhaven"].MarketCash, Is.EqualTo(southCash));
            Assert.That(after.Ports["northport"].Inventory["logs"], Is.EqualTo(18 - logs));
            Assert.That(after.Ports["northport"].Inventory["planks"], Is.EqualTo(2));
            Assert.That(after.Ports["southhaven"].Inventory["logs"], Is.Zero);
            Assert.That(after.Ports["southhaven"].Inventory["planks"], Is.EqualTo(southPlanks));
            Assert.That(initial.TraderCash, Is.EqualTo(100L));
            Assert.That(initial.Ship.CargoUnits, Is.Zero);
        }

        private static MarketQuote AutomationLinear(string goodId, long price, int target, long sensitivity, long maximum)
        {
            return new MarketQuote(goodId, new InventoryPricing(InventoryPricingModel.Linear,
                price, target, sensitivity, 1, maximum, 1));
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
