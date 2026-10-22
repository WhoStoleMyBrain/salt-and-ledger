using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class LocalRunSaveStoreTests
    {
        private string directory;
        private ContentCatalog content;
        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "salt-ledger-save-test-" + Guid.NewGuid().ToString("N"));
            content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
        }
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        [Test]
        public void SaveReplacementAndLoadKeepExactStateAndExposeThreeSlots()
        {
            var store = new LocalRunSaveStore(directory);
            var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter));
            Assert.That(store.List(content).Count, Is.EqualTo(3));
            Assert.That(store.LatestCompatibleSlot(content), Is.Null);
            store.Save(1, world);
            world.AdvanceDay();
            store.Save(1, world);
            Assert.That(WorldSaveCodec.Save(store.Load(1, content)), Is.EqualTo(WorldSaveCodec.Save(world)));
            Assert.That(store.List(content).Single(s => s.Number == 1).Day, Is.EqualTo(1));
            Assert.That(store.LatestCompatibleSlot(content), Is.EqualTo(1));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
        [Test]
        public void CorruptSlotIsListedWithoutLoadingItOrChangingOtherSlots()
        {
            var store = new LocalRunSaveStore(directory);
            var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter));
            store.Save(1, world);
            File.WriteAllText(Path.Combine(directory, "slot-2.json"), "{broken");
            var slots = store.List(content);
            Assert.That(slots[0].IsCompatible, Is.True);
            Assert.That(slots[1].Exists, Is.True);
            Assert.That(slots[1].IsCompatible, Is.False);
            Assert.That(slots[1].Description, Does.Contain("Cannot load"));
            Assert.That(WorldSaveCodec.Save(store.Load(1, content)), Is.EqualTo(WorldSaveCodec.Save(world)));
            Assert.Throws<WorldSaveException>(() => store.Load(2, content));
        }
        [Test]
        public void InvalidSlotAndFailedReplacementPreserveExistingSave()
        {
            var store = new LocalRunSaveStore(directory);
            var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter));
            store.Save(1, world);
            var original = store.ReadText(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(0, world));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Load(4, content));
            var path = Path.Combine(directory, "slot-1.json");
            using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.Throws<IOException>(() => store.Save(1, world));
            Assert.That(store.ReadText(1), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(directory, "*.tmp"), Is.Empty);
        }
    }
}
