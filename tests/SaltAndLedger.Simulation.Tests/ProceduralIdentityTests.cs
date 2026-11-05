using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class ProceduralIdentityTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static WorldGenerationConfiguration Configuration(int deadline = 90, int fleetLimit = 1)
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["deadlineDays"] = deadline;
        json["fleetLimit"] = fleetLimit;
        json["industries"]!.Single(i => (string)i["industryId"]! == "bakery")["privateTowns"] =
            new JObject { ["min"] = 1, ["max"] = 1 };
        return WorldGenerationConfiguration.Parse(json.ToString());
    }

    [Theory]
    [InlineData(-42)]
    [InlineData(42)]
    [InlineData(99)]
    public void CustomConfigurationFiltersImpossibleGoalsAndPersistsExactIdentity(int seed)
    {
        var configuration = Configuration();
        var draft = GrandCommissionDraft.Generate(seed, configuration: configuration);
        Assert.DoesNotContain(draft.Rounds.SelectMany(r => r.Catalog), g => g.Id == "second_command" || g.Id == "breadworks");
        Assert.All(draft.Rounds, r => Assert.InRange(r.Offers.Count, 1, 3));
        var plan = draft.Resolve(draft.Rounds.Select(r => r.Offers[0].Id));
        var identity = new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter, seed: seed,
            commission: plan, generationConfiguration: configuration);
        var world = RunScenario.Create(Content(), identity);
        Assert.Equal(90, world.Snapshot().Challenge!.Configuration.DeadlineDay);
        Assert.Equal(1, world.Snapshot().Economy!.Configuration.MaximumPlayerShips);
        Assert.Equal(configuration.Fingerprint, identity.GenerationConfigurationFingerprint);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(identity, restored.Snapshot().RunIdentity);
        Assert.Equal(configuration.ToJson(), restored.Snapshot().RunIdentity!.GenerationConfiguration.ToJson());
        for (var day = 0; day < 7; day++)
        {
            world.AdvanceDay();
            restored.AdvanceDay();
        }
        Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        Assert.Throws<ArgumentException>(() => new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.MerchantCharter, seed: seed, commission: plan,
            generationConfiguration: Configuration(deadline: 120)));
    }

    [Fact]
    public void AllHarborsUsesEveryGeneratedTownAndHistorySurvivesBeyondFourVisits()
    {
        const int seed = 99;
        var draft = GrandCommissionDraft.Generate(seed);
        var choices = draft.Rounds.Select(r => r.Offers[0].Id).ToArray();
        choices[0] = "all_harbors";
        var world = RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.MerchantCharter, seed: seed, commission: draft.Resolve(choices)));
        var count = world.Snapshot().Ports.Count;
        Assert.InRange(count, 8, 12);
        var condition = world.Snapshot().RunIdentity!.Commission!.Goals[0].Conditions.Single();
        Assert.Equal(count, condition.Target);
        foreach (var port in world.Snapshot().Ports.Keys)
        {
            if (world.Snapshot().Ships[WorldIds.PlayerShipId].DockedPortId == port) continue;
            world.Depart(WorldIds.PlayerShipId, port);
            while (world.Snapshot().Ships[WorldIds.PlayerShipId].DockedPortId == null) world.AdvanceDay();
        }
        Assert.Equal(count, world.Snapshot().CommissionHistory!.Ships.Single().Visits.Count);
        Assert.True(GrandCommission.Progress(world.Snapshot()).Goals[0].IsMet);
        var saved = WorldSaveCodec.Save(world);
        Assert.Equal(saved, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), saved)));
    }

    [Fact]
    public void FiniteUnsuppliedRecipesAndImpossibleTravelHorizonAreNeverOffered()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["requireSupplyChains"] = false;
        json["industries"]!.Single(i => (string)i["industryId"]! == "farm").Remove();
        foreach (var good in new[] { "grain", "flour" })
            json["startingInventories"]![good] = new JObject { ["min"] = 0, ["max"] = 0 };
        var draft = GrandCommissionDraft.Generate(42, configuration: WorldGenerationConfiguration.Parse(json.ToString()));
        Assert.DoesNotContain(draft.Rounds.SelectMany(r => r.Catalog), goal =>
            goal.Id == "new_industry" || goal.Id == "breadworks" || goal.Id == "flour_for_bakers");
        json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["deadlineDays"] = 1;
        var error = Assert.Throws<ArgumentException>(() => GrandCommissionDraft.Generate(42,
            configuration: WorldGenerationConfiguration.Parse(json.ToString())));
        Assert.Contains("Navigation", error.Message);
    }

    [Fact]
    public void MintGoalRequiresConstructionProductionAndDeliveryWithinTheHorizon()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        var mine = json["industries"]!.Single(i => (string)i["industryId"]! == "silver_mine");
        mine["publicTowns"] = new JObject { ["min"] = 0, ["max"] = 0 };
        mine["privateTowns"] = new JObject { ["min"] = 1, ["max"] = 1 };
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: WorldGenerationConfiguration.Parse(json.ToString()));
        var source = Assert.Single(generation.EligiblePorts("mine_silver"));
        Assert.Empty(generation.PublicPorts("mine_silver"));
        Assert.All(generation.Ports, p => Assert.Equal(0, p.Market.InitialQuantities["silver"]));
        var travel = SeaNavigation.FindPath(generation.Geography.Routes, source, generation.MintPortId, RunScenario.StartingShipSpeed).DurationDays;
        var earliest = 2 + generation.Blueprints.Single(b => b.RecipeId == "mine_silver").IntervalDays + travel;
        json["deadlineDays"] = earliest - 1;
        var draft = GrandCommissionDraft.Generate(42, configuration: WorldGenerationConfiguration.Parse(json.ToString()));
        Assert.DoesNotContain(draft.Rounds.SelectMany(r => r.Catalog), goal => goal.Id == "royal_institution");
    }

    [Fact]
    public void SecondCommandIncludesPlankDeliveryShipConstructionAndFirstVoyageTime()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        var sawmill = json["industries"]!.Single(i => (string)i["industryId"]! == "sawmill");
        sawmill["publicTowns"] = new JObject { ["min"] = 0, ["max"] = 0 };
        sawmill["privateTowns"] = new JObject { ["min"] = 1, ["max"] = 1 };
        json["startingInventories"]!["planks"] = new JObject { ["min"] = 0, ["max"] = 0 };
        json["startingInventories"]!["logs"] = new JObject { ["min"] = 100, ["max"] = 100 };
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: WorldGenerationConfiguration.Parse(json.ToString()));
        var source = Assert.Single(generation.EligiblePorts("saw_planks"));
        var delivery = SeaNavigation.FindPath(generation.Geography.Routes, source, generation.ShipyardPortId, RunScenario.StartingShipSpeed).DurationDays;
        var firstVoyage = generation.Ports.Where(p => p.Id != generation.ShipyardPortId).Min(p =>
            SeaNavigation.FindPath(generation.Geography.Routes, generation.ShipyardPortId, p.Id, RunScenario.StartingShipSpeed).DurationDays);
        var production = 2 + 4 * generation.Blueprints.Single(b => b.RecipeId == "saw_planks").IntervalDays;
        json["deadlineDays"] = production + delivery + 4 + firstVoyage - 1;
        var draft = GrandCommissionDraft.Generate(42, configuration: WorldGenerationConfiguration.Parse(json.ToString()));
        Assert.DoesNotContain(draft.Rounds.SelectMany(r => r.Catalog), goal => goal.Id == "second_command");
    }

    [Fact]
    public void RoyalExporterRequiresCargoAtTheMintBeforeTheFiniteContractDeadline()
    {
        var json = JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["requireSupplyChains"] = false;
        json["townCount"] = new JObject { ["min"] = 8, ["max"] = 8 };
        var geography = json["geography"]!;
        geography["width"] = 10000; geography["height"] = 10000; geography["margin"] = 100;
        geography["minimumTownSpacing"] = 1200; geography["minimumLaneDistance"] = 1500;
        geography["maximumLaneDistance"] = 15000; geography["extraLaneCount"] = 0; geography["candidateCount"] = 1;
        foreach (var id in new[] { "mill", "furniture_workshop" })
            json["industries"]!.Single(i => (string)i["industryId"]! == id).Remove();
        var sawmill = json["industries"]!.Single(i => (string)i["industryId"]! == "sawmill");
        sawmill["publicTowns"] = new JObject { ["min"] = 0, ["max"] = 0 };
        sawmill["privateTowns"] = new JObject { ["min"] = 1, ["max"] = 1 };
        foreach (var good in new[] { "planks", "flour", "furniture" })
            json["startingInventories"]![good] = new JObject { ["min"] = 0, ["max"] = 0 };
        json["startingInventories"]!["logs"] = new JObject { ["min"] = 100, ["max"] = 100 };
        var configuration = WorldGenerationConfiguration.Parse(json.ToString());
        var generation = Enumerable.Range(0, 64).Select(seed => ExpandingEconomyGeneration.Generate(seed, configuration: configuration))
            .First(g => g.EligiblePorts("saw_planks").Single() != g.MintPortId &&
                SeaNavigation.FindPath(g.Geography.Routes, g.EligiblePorts("saw_planks").Single(), g.MintPortId, RunScenario.StartingShipSpeed).DurationDays > 112);
        var draft = GrandCommissionDraft.Generate(generation.Seed, configuration: configuration);
        Assert.DoesNotContain(draft.Rounds.SelectMany(r => r.Catalog), goal => goal.Id == "royal_exporter");
    }

    [Fact]
    public void OldSaveSchemaAndMissingConfigurationAreRejectedSafely()
    {
        var world = RunScenario.Create(Content(), new RunIdentity(null, RunScenario.MerchantCharter));
        var before = world.Snapshot();
        var json = JObject.Parse(WorldSaveCodec.Save(world));
        json["SchemaVersion"] = 6;
        var error = Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), json.ToString(Formatting.None)));
        Assert.Contains("schema 7", error.Message);
        var missing = JObject.Parse(WorldSaveCodec.Save(world));
        ((JObject)missing["Identity"]!).Remove("GenerationConfiguration");
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), missing.ToString(Formatting.None)));
        Assert.Same(before, world.Snapshot());
    }
}
