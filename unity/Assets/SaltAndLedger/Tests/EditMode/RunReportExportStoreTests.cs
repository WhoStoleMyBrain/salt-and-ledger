using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class RunReportExportStoreTests
    {
        private string directory;
        private ContentCatalog content;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "salt-ledger-export-test-" + Guid.NewGuid().ToString("N"));
            content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestCase(CompanyStorageRules.Legacy)]
        [TestCase(CompanyStorageRules.Pooled)]
        public void ExactSaveAndReadableReportAreNewFilesWithoutChangingStateOrSlots(CompanyStorageRules rules)
        {
            var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.IndustrialCharter,
                RunWorldMode.ExpandingEconomy, -734282136, companyStorageRules: rules));
            world.AdvanceDay();
            var slots = new LocalRunSaveStore(Path.Combine(directory, "Runs"));
            slots.Save(3, world);
            var before = world.Snapshot();
            var save = WorldSaveCodec.Save(world);
            var hash = RunDiagnosticReport.StateFingerprint(before);
            var store = new RunReportExportStore(Path.Combine(directory, "Exports"), content);
            var first = store.Export(save, before, new[] { "Day advanced to 1." });
            var second = store.Export(save, before);
            Assert.That(first.SavePath, Is.Not.EqualTo(second.SavePath));
            Assert.That(first.ReportPath, Is.Not.EqualTo(second.ReportPath));
            Assert.That(File.ReadAllText(first.SavePath), Is.EqualTo(save));
            Assert.That(WorldSaveCodec.Save(WorldSaveCodec.Load(content, File.ReadAllText(first.SavePath))), Is.EqualTo(save));
            var report = JObject.Parse(File.ReadAllText(first.ReportPath));
            Assert.That((string)report["FullStateFingerprint"], Is.EqualTo(hash));
            Assert.That((bool)report["RecentHistory"]["IsPartial"], Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(directory, "Exports")).Length, Is.EqualTo(4));
            Assert.That(slots.ReadText(3), Is.EqualTo(save));
            Assert.That(slots.List(content).Count, Is.EqualTo(3));
            Assert.That(slots.List(content).Count(s => s.Exists), Is.EqualTo(1));
            Assert.That(world.Snapshot(), Is.SameAs(before));
            Assert.That(WorldSaveCodec.Save(world), Is.EqualTo(save));
        }

        [Test]
        public void InvalidOrMismatchedSaveCannotCreateAnExportOrChangeTheActiveState()
        {
            var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.MerchantCharter));
            var save = WorldSaveCodec.Save(world);
            world.AdvanceDay();
            var before = world.Snapshot();
            var store = new RunReportExportStore(directory, content);
            Assert.Throws<WorldSaveException>(() => store.Export(save, before));
            Assert.Throws<WorldSaveException>(() => store.Export("{broken", before));
            Assert.Throws<WorldSaveException>(() => store.Export(new string('x', WorldSaveCodec.MaximumJsonCharacters + 1), before));
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(world.Snapshot(), Is.SameAs(before));
            Assert.Throws<ArgumentException>(() => new RunReportExportStore("relative", content));
        }

        [Test]
        public void TerminalCommissionExportKeepsFrozenStateAndAllFourObjectives()
        {
            var draft = GrandCommissionDraft.Generate(42);
            var world = RunScenario.Create(content, new RunIdentity(GrandCommission.ChallengeId,
                RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42,
                commission: draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId)),
                companyStorageRules: CompanyStorageRules.Pooled));
            world.AbandonChallenge();
            var save = WorldSaveCodec.Save(world);
            var result = new RunReportExportStore(directory, content).Export(save, world.Snapshot());
            var report = JObject.Parse(File.ReadAllText(result.ReportPath));
            Assert.That((string)report["Status"], Is.EqualTo("Abandoned"));
            Assert.That(report["Objectives"]["Commission"]["Goals"].Count(), Is.EqualTo(4));
            Assert.That(WorldSaveCodec.Save(world), Is.EqualTo(save));
            Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        }
    }
}
