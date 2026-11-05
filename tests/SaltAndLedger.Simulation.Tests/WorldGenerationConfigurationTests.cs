using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class WorldGenerationConfigurationTests
{
    private static JObject Default() => JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
    private static WorldGenerationConfiguration Change(Action<JObject> edit)
    { var json = Default(); edit(json); return WorldGenerationConfiguration.Parse(json.ToString()); }

    [Fact]
    public void DefaultsRoundTripCanonicallyAndCollectionsAreImmutable()
    {
        var config = WorldGenerationConfiguration.Default;
        Assert.Equal(8, config.TownCount.Min); Assert.Equal(12, config.TownCount.Max);
        Assert.Equal(720, config.DeadlineDays); Assert.Equal(3, config.FleetLimit);
        Assert.True(config.EnforceProductionSeparation); Assert.True(config.RequireSupplyChains);
        Assert.Equal(config.ToJson(), WorldGenerationConfiguration.Parse(config.ToJson()).ToJson());
        Assert.Equal(config.Fingerprint, WorldGenerationConfiguration.Parse(config.ToJson()).Fingerprint);
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<string>)config.EnabledGoods).Clear());
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IDictionary<string, GenerationRange>)config.StartingInventories).Clear());
    }

    [Theory]
    [InlineData("enforceProductionSeparation")]
    [InlineData("requireSupplyChains")]
    [InlineData("enabledGoods")]
    [InlineData("townCount")]
    [InlineData("industries")]
    [InlineData("startingInventories")]
    [InlineData("population")]
    public void MissingRequiredFieldsDoNotSilentlyRelaxConstraints(string property)
    {
        var root = Default(); root.Remove(property);
        Assert.Throws<WorldGenerationException>(() => WorldGenerationConfiguration.Parse(root.ToString()));
    }

    [Fact]
    public void UnknownDuplicateNullAndCoercedFieldsAreRejected()
    {
        Assert.Throws<WorldGenerationException>(() => Change(root => root["surprise"] = true));
        Assert.Throws<WorldGenerationException>(() => Change(root => root["DeadlineDays"] = 1));
        Assert.Throws<WorldGenerationException>(() => Change(root => root["townCount"]!["Min"] = 1));
        Assert.Throws<WorldGenerationException>(() => Change(root => root["townCount"]!["min"] = "8"));
        Assert.Throws<WorldGenerationException>(() => Change(root => root["npc"]!["speed"] = 1.5));
        Assert.Throws<WorldGenerationException>(() => Change(root => ((JArray)root["industries"]!).Add(JValue.CreateNull())));
        Assert.Throws<WorldGenerationException>(() => Change(root => ((JArray)root["enabledGoods"]!).Add("bread")));
        Assert.Throws<WorldGenerationException>(() => Change(root => root["industries"]![0]!["industryId"] = "unknown_mine"));
        Assert.Throws<WorldGenerationException>(() => Change(root => root["startingInventories"]!["alien"] = new JObject { ["min"] = 0, ["max"] = 1 }));
        Assert.Throws<WorldGenerationException>(() => WorldGenerationConfiguration.Parse("{\"schemaVersion\":1,\"schemaVersion\":1}"));
        Assert.Throws<WorldGenerationException>(() => WorldGenerationConfiguration.Parse("/* comment */" + Default().ToString()));
        Assert.Throws<WorldGenerationException>(() => WorldGenerationConfiguration.Parse(Default().ToString().Replace("\"schemaVersion\": 1,", "\"schemaVersion\": 1.0,")));
        Assert.Throws<WorldGenerationException>(() => WorldGenerationConfiguration.Parse(Default().ToString().TrimEnd().TrimEnd('}') + ",}"));
    }

    [Fact]
    public void InvertedRangesImpossibleAvailabilityAndDistanceConstraintsFailClearly()
    {
        Assert.Contains("geography", Assert.Throws<WorldGenerationException>(() => Change(root => root["geography"]!["margin"] = int.MaxValue)).Message);
        Assert.Contains("townCount", Assert.Throws<WorldGenerationException>(() => Change(root => root["townCount"]!["min"] = 20)).Message);
        Assert.Contains("maximumLaneDistance", Assert.Throws<WorldGenerationException>(() => Change(root => root["geography"]!["maximumLaneDistance"] = 50)).Message);
        Assert.Contains("separate towns", Assert.Throws<WorldGenerationException>(() => Change(root =>
        {
            foreach (JObject industry in (JArray)root["industries"]!)
                if ((string)industry["industryId"]! == "farm" || (string)industry["industryId"]! == "mill")
                    industry["privateTowns"] = new JObject { ["min"] = 8, ["max"] = 8 };
        })).Message);
        Assert.Contains("shipyard", Assert.Throws<WorldGenerationException>(() => Change(root =>
        {
            var sawmill = ((JArray)root["industries"]!).Cast<JObject>().Single(i => (string)i["industryId"]! == "sawmill");
            sawmill["privateTowns"] = new JObject { ["min"] = 8, ["max"] = 8 };
        })).Message);
        Assert.Contains("Mint", Assert.Throws<WorldGenerationException>(() => Change(root =>
        { root["institutions"]!["mintPortIndex"] = 1; root["institutions"]!["silverPortIndex"] = 1; })).Message);
    }

    [Fact]
    public void SupplyChainChecksRejectMissingUpstreamWhileExplicitFiniteChallengesAreAllowed()
    {
        void RemoveMill(JObject root)
        { root["industries"] = new JArray(((JArray)root["industries"]!).Where(i => (string)i["industryId"]! != "mill")); }
        Assert.Contains("flour", Assert.Throws<WorldGenerationException>(() => Change(RemoveMill)).Message);
        var difficult = Change(root => { RemoveMill(root); root["requireSupplyChains"] = false; });
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: difficult);
        Assert.False(generation.Quality.AllEnabledInputsObtainable);
        Assert.Empty(generation.EligiblePorts("mill_flour"));
        Assert.True(generation.Quality.Connected);
    }

    [Fact]
    public void OptionalSpecializationCanBeDisabledWithoutChangingDirectRecipeDefinitions()
    {
        var config = Change(root =>
        {
            root["enforceProductionSeparation"] = false;
            foreach (JObject industry in (JArray)root["industries"]!)
                if ((string)industry["industryId"]! == "farm" || (string)industry["industryId"]! == "mill")
                    industry["privateTowns"] = new JObject { ["min"] = 8, ["max"] = 8 };
        });
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: config);
        Assert.True(generation.Quality.LocalDependencyViolations > 0);
        Assert.True(generation.Quality.Connected);
    }

    [Theory]
    [InlineData("sparse-eight.json")]
    [InlineData("twelve-town-commerce.json")]
    [InlineData("narrow-specialization.json")]
    public void DocumentedExamplesMeetTheirOwnBoundsAcrossTwentySeeds(string filename)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "WorldGenerationExamples", filename);
        var config = WorldGenerationConfiguration.Parse(System.IO.File.ReadAllText(path));
        var seeds = Enumerable.Range(0, 16).Select(i => unchecked(i * 104729)).Concat(new[] { -42, 42, int.MinValue, int.MaxValue });
        foreach (var seed in seeds)
        {
            var generation = ExpandingEconomyGeneration.Generate(seed, configuration: config);
            Assert.InRange(generation.Ports.Count, config.TownCount.Min, config.TownCount.Max);
            Assert.Equal(generation.Ports.Count - 1 + config.Geography.ExtraLaneCount, generation.Geography.Routes.Count);
            Assert.All(generation.Geography.Routes, lane => Assert.InRange(lane.Distance, config.Geography.MinimumLaneDistance, config.Geography.MaximumLaneDistance));
            Assert.True(generation.Quality.Connected);
            Assert.True(generation.Quality.AllEnabledInputsObtainable);
            Assert.Equal(0, generation.Quality.LocalDependencyViolations);
            foreach (var industry in generation.Quality.Industries)
            {
                var rule = config.Industries.Single(i => i.IndustryId == industry.IndustryId);
                Assert.InRange(industry.PublicTowns, rule.PublicTowns.Min, rule.PublicTowns.Max);
                Assert.InRange(industry.PrivateTowns, rule.PrivateTowns.Min, rule.PrivateTowns.Max);
            }
        }
    }

    [Fact]
    public void TwoTownMinimumSupportsACompleteNarrowComplementaryEconomy()
    {
        var config = Change(root =>
        {
            root["townCount"] = new JObject { ["min"] = 2, ["max"] = 2 };
            root["geography"]!["extraLaneCount"] = 0;
            foreach (JObject industry in (JArray)root["industries"]!)
            {
                industry["publicTowns"] = new JObject { ["min"] = 1, ["max"] = 1 };
                industry["privateTowns"] = new JObject { ["min"] = 1, ["max"] = 1 };
            }
        });
        foreach (var seed in new[] { 0, 42, -42, int.MaxValue })
        {
            var generation = ExpandingEconomyGeneration.Generate(seed, configuration: config);
            Assert.Equal(2, generation.Ports.Count); Assert.Single(generation.Geography.Routes);
            Assert.True(generation.Quality.AllEnabledInputsObtainable); Assert.Equal(0, generation.Quality.LocalDependencyViolations);
        }
    }

    [Fact]
    public void SupportedLargeBoundaryProducesBoundedQualityReport()
    {
        var config = Change(root =>
        {
            root["townCount"] = new JObject { ["min"] = 64, ["max"] = 64 };
            root["geography"]!["width"] = 2000; root["geography"]!["height"] = 1400;
            root["geography"]!["minimumTownSpacing"] = 90; root["geography"]!["maximumLaneDistance"] = 500;
            root["geography"]!["extraLaneCount"] = 20; root["fleetLimit"] = 32; root["deadlineDays"] = 36500;
        });
        var timer = Stopwatch.StartNew();
        var generation = ExpandingEconomyGeneration.Generate(42, configuration: config);
        Assert.Equal(64, generation.Ports.Count); Assert.Equal(83, generation.Geography.Routes.Count);
        Assert.True(generation.Quality.Connected); Assert.Equal(0, generation.Quality.LocalDependencyViolations);
        Assert.InRange(generation.Quality.RejectedConstraints.Count, 0, 96);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(15), "64-town generation exceeded the bounded performance budget.");
    }
}
