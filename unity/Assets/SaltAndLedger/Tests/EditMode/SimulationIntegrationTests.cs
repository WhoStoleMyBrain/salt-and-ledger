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
