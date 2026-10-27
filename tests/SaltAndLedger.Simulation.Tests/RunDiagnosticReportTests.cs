using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class RunDiagnosticReportTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    [Theory]
    [InlineData(RunWorldMode.ExpandingEconomy, CompanyStorageRules.Pooled)]
    public void ReportIsParseableDeterministicAndDoesNotMutateAnyNamedMode(RunWorldMode mode, CompanyStorageRules rules)
    {
        var content = Content();
        var world = RunScenario.Create(content, new RunIdentity(null, RunScenario.IndustrialCharter,
            mode, 42, companyStorageRules: rules));
        world.SetAutomationEnabled(WorldIds.PlayerShipId, true);
        world.AdvanceDay();
        var before = world.Snapshot();
        var hash = RunDiagnosticReport.StateFingerprint(before);
        var save = WorldSaveCodec.Save(world);
        var text = RunDiagnosticReport.Create(content, before, new[] { "One actual host event" });
        var report = JObject.Parse(text);
        Assert.Equal(hash, (string)report["FullStateFingerprint"]!);
        Assert.Equal(before.Day, (long)report["Day"]!);
        Assert.Equal(before.Revision, (long)report["Revision"]!);
        Assert.Equal(before.TotalCash.ToString(CultureInfo.InvariantCulture), (string)report["Accounts"]!["AllAccountCash"]!);
        Assert.Equal(before.Companies.Count, report["Accounts"]!["Companies"]!.Count());
        Assert.Equal(before.Accounting.Stores.Count, report["FifoStores"]!.Count());
        Assert.Equal(rules == CompanyStorageRules.Pooled ? before.CompanyStockpiles.Count : 0, report["CompanyStockpiles"]!.Count());
        Assert.Equal(rules == CompanyStorageRules.Pooled ? 0 : before.PrivateFacilities.Count, report["LegacyFacilityStores"]!.Count());
        Assert.True((bool)report["RecentHistory"]!["IsPartial"]!);
        Assert.Equal("One actual host event", (string)report["RecentHistory"]!["Events"]![0]!);
        Assert.Equal(text, RunDiagnosticReport.Create(content, before, new[] { "One actual host event" }));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(hash, RunDiagnosticReport.StateFingerprint(world.Snapshot()));
        Assert.Equal(save, WorldSaveCodec.Save(world));
    }

    [Fact]
    public void DraftedTerminalReportRetainsGoalsReasonsAndFrozenIdentity()
    {
        var draft = GrandCommissionDraft.Generate(42);
        var world = RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.IndustrialCharter, RunWorldMode.ExpandingEconomy, 42,
            commission: draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId)),
            companyStorageRules: CompanyStorageRules.Pooled));
        world.AbandonChallenge();
        var before = WorldSaveCodec.Save(world);
        var report = JObject.Parse(RunDiagnosticReport.Create(Content(), world.Snapshot()));
        Assert.Equal("Abandoned", (string)report["Status"]!);
        Assert.Equal(4, report["Objectives"]!["Commission"]!["Goals"]!.Count());
        Assert.All(report["Objectives"]!["Commission"]!["Goals"]!, goal =>
        {
            Assert.False(string.IsNullOrEmpty((string?)goal["Reason"]));
            Assert.NotEmpty(goal["Conditions"]!);
        });
        Assert.Equal(world.Snapshot().RunIdentity!.Commission!.Signature, (string)report["Identity"]!["Commission"]!["Signature"]!);
        Assert.Equal(before, WorldSaveCodec.Save(world));
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
    }

    [Fact]
    public void HostHistoryIsCappedAndClearlyPartialEvenForAnUnboundedEnumerable()
    {
        var world = RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter));
        var report = JObject.Parse(RunDiagnosticReport.Create(Content(), world.Snapshot(), Forever()));
        var history = report["RecentHistory"]!;
        Assert.True((bool)history["IsPartial"]!);
        Assert.True((bool)history["Truncated"]!);
        Assert.Equal(RunDiagnosticReport.MaximumRecentEvents, history["Events"]!.Count());
        Assert.All(history["Events"]!, line => Assert.EndsWith(" [truncated]", (string)line!));

        static System.Collections.Generic.IEnumerable<string> Forever()
        {
            while (true) yield return new string('x', RunDiagnosticReport.MaximumEventCharacters + 1);
        }
    }
}
