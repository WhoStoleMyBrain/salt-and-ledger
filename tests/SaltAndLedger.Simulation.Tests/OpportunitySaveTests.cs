using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class OpportunitySaveTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World() => RunScenario.Create(Content(),
        new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1, economicRulesVersion: 1));

    [Fact]
    public void AcceptedEscrowAndPartialProjectInvestmentResumeExactlyAcrossGenerationAndExpiration()
    {
        var world = World();
        var offer = world.Snapshot().Opportunities!.Entries.First(e => !e.IsProject && e.Status == OpportunityStatus.Available);
        world.AcceptOpportunity(WorldIds.PlayerOwnerId, offer.Id);
        var project = world.Snapshot().Opportunities!.Entries.First(e => e.IsProject && e.Status == OpportunityStatus.Available);
        world.AcceptOpportunity(WorldIds.PlayerOwnerId, project.Id);
        world.ContributeProject(WorldIds.PlayerOwnerId, project.Id, null, 0, project.RequiredCash / 2);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(offer.FundingRequired, restored.Snapshot().Opportunities!.ReservedCash);
        Assert.Equal(project.RequiredCash / 2, restored.Snapshot().Opportunities!.Entries.Single(e => e.Id == project.Id).ContributedCash);
        for (var day = 0; day < 50; day++)
        {
            world.AdvanceDay();
            restored.AdvanceDay();
            Assert.Equal(WorldSaveCodec.Fingerprint(world.Snapshot()), WorldSaveCodec.Fingerprint(restored.Snapshot()));
            if (day % 16 == 0 || day == 49)
            {
                var checkpoint = WorldSaveCodec.Save(restored);
                Assert.Equal(WorldSaveCodec.Save(world), checkpoint);
                restored = WorldSaveCodec.Load(Content(), checkpoint);
            }
        }
    }

    [Theory]
    [InlineData("missing_board")]
    [InlineData("duplicate_id")]
    [InlineData("sequence")]
    [InlineData("generation_date")]
    [InlineData("unknown_kind")]
    [InlineData("unaccepted_credit")]
    [InlineData("unaccepted_escrow")]
    [InlineData("invented_payment")]
    [InlineData("invented_rewards")]
    [InlineData("invented_export")]
    [InlineData("depot_capacity")]
    [InlineData("depot_cost")]
    [InlineData("project_completed_without_investment")]
    [InlineData("orphan_public_facility")]
    public void ResealedImpossibleOpportunityStateIsRejectedWithoutChangingActiveRun(string corruption)
    {
        var world = World();
        var original = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        var data = JObject.Parse(saved);
        var board = data["Opportunities"]!;
        var entries = (JArray)board["Entries"]!;
        var contract = entries.First(e => (int)e["Kind"]! == (int)OpportunityKind.BulkProcurement);
        var depot = entries.First(e => (int)e["Kind"]! == (int)OpportunityKind.TradingDepot);
        switch (corruption)
        {
            case "missing_board": data["Opportunities"] = null; break;
            case "duplicate_id": entries.Add(entries[0]!.DeepClone()); break;
            case "sequence": board["NextSequence"] = 0; break;
            case "generation_date": board["NextGenerationDay"] = 0; break;
            case "unknown_kind": contract["Kind"] = 999; break;
            case "unaccepted_credit": contract["DeliveredQuantity"] = EconomicUnits.QuantityScale; break;
            case "unaccepted_escrow": contract["ReservedCash"] = 1; break;
            case "invented_payment": board["CumulativePayments"] = 1; break;
            case "invented_rewards": board["CumulativeRewards"] = 1; board["CumulativePayments"] = 1; break;
            case "invented_export": board["ExportedGoods"]!["planks"] = 1; break;
            case "depot_capacity": depot["StorageCapacity"] = int.MaxValue; break;
            case "depot_cost": depot["RequiredCash"] = 1; break;
            case "project_completed_without_investment":
                depot["Status"] = (int)OpportunityStatus.Succeeded;
                depot["OwnerId"] = WorldIds.PlayerOwnerId;
                depot["CompletedDay"] = 0;
                break;
            case "orphan_public_facility":
                var port = data["Ports"]!.First(p => ((JArray)p["Facilities"]!).Count > 0);
                var facilities = (JArray)port["Facilities"]!;
                var fake = facilities[0]!.DeepClone();
                fake["Id"] = "commissioned_fake";
                facilities.Add(fake);
                break;
        }
        data["StateFingerprint"] = "";
        data["StateFingerprint"] = WorldSaveCodec.Fingerprint(data);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), data.ToString(Formatting.None)));
        Assert.Same(original, world.Snapshot());
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    [Fact]
    public void PreviousPreReleaseSchemaFailsExplicitly()
    {
        var saved = JObject.Parse(WorldSaveCodec.Save(World()));
        saved["SchemaVersion"] = 7;
        var failure = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), saved.ToString(Formatting.None)));
        Assert.Contains("schema 9", failure.Message);
    }
}
