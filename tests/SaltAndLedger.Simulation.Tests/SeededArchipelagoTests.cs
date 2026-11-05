using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class SeededArchipelagoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(42)]
    public void VersionFourUsesStableIdentifiersAndExactReplay(int seed)
    {
        var map = SeededArchipelago.Generate(seed);
        Assert.Equal(4, map.GeneratorVersion);
        Assert.InRange(map.Positions.Count, 8, 12);
        Assert.Equal(Enumerable.Range(1, map.Positions.Count).Select(index => $"town_{index:D3}"), map.Positions.Keys);
        Assert.Equal(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(map)),
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(SeededArchipelago.Generate(seed))));
        Assert.InRange(map.SelectedCandidate, 0, map.CandidateCount - 1);
        Assert.Equal(map.RejectionReasons.Count, map.RejectedCandidateCount);
    }

    [Fact]
    public void BoundedCandidatesEnforceEveryDistanceAndConnectivityAcrossManySeeds()
    {
        var seeds = Enumerable.Range(0, 512).Select(index => unchecked(index * 104729))
            .Concat(new[] { int.MinValue, int.MaxValue });
        var counts = new HashSet<int>();
        foreach (var seed in seeds)
        {
            var map = SeededArchipelago.Generate(seed);
            counts.Add(map.Positions.Count);
            Assert.Equal(map.Positions.Count + 2, map.Routes.Count);
            Assert.True(map.Routes.Count < map.Positions.Count * (map.Positions.Count - 1) / 2);
            AssertGeometry(map, WorldGenerationConfiguration.Default);
        }
        Assert.Equal(new[] { 8, 9, 10, 11, 12 }, counts.OrderBy(count => count));
    }

    [Fact]
    public void ChangingSeedsChangesGeographyAndCollectionsAreImmutable()
    {
        var signatures = Enumerable.Range(0, 100).Select(seed => SeededArchipelago.Generate(seed).Signature).ToArray();
        Assert.Equal(signatures.Length, signatures.Distinct(StringComparer.Ordinal).Count());
        var map = SeededArchipelago.Generate(0);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, PortPosition>)map.Positions).Remove("town_001"));
        Assert.Throws<NotSupportedException>(() => ((IList<SeaRoute>)map.Routes).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<string>)map.RejectionReasons).Clear());
    }

    [Fact]
    public void GeographyAndSignatureIgnoreCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        var before = JsonConvert.SerializeObject(SeededArchipelago.Generate(int.MinValue));
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(before, JsonConvert.SerializeObject(SeededArchipelago.Generate(int.MinValue)));
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData(8, 900, 600, 135, 175, 325, 1)]
    [InlineData(12, 1000, 700, 110, 110, 300, 0)]
    [InlineData(64, 2000, 2000, 110, 110, 1000, 5)]
    public void CustomCountsSpacingAndNarrowLanesAreHonored(int towns, int width, int height,
        int spacing, int minimumLane, int maximumLane, int extraLanes)
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["townCount"]!["min"] = towns;
        json["townCount"]!["max"] = towns;
        var geography = json["geography"]!;
        geography["width"] = width;
        geography["height"] = height;
        geography["minimumTownSpacing"] = spacing;
        geography["minimumLaneDistance"] = minimumLane;
        geography["maximumLaneDistance"] = maximumLane;
        geography["extraLaneCount"] = extraLanes;
        geography["candidateCount"] = 32;
        var configuration = WorldGenerationConfiguration.Parse(json.ToString());
        foreach (var seed in new[] { 0, 42, 99, -734282136 })
        {
            var map = SeededArchipelago.Generate(seed, configuration: configuration);
            Assert.Equal(towns, map.Positions.Count);
            Assert.Equal(towns - 1 + extraLanes, map.Routes.Count);
            AssertGeometry(map, configuration);
        }
    }

    [Fact]
    public void UnsatisfiedPlacementFailsClearlyWithoutChangingUserBounds()
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(WorldGenerationConfiguration.Default.ToJson());
        json["townCount"]!["min"] = 12;
        json["townCount"]!["max"] = 12;
        json["geography"]!["placementAttempts"] = 1;
        json["geography"]!["candidateCount"] = 1;
        var configuration = WorldGenerationConfiguration.Parse(json.ToString());
        var failure = Assert.Throws<WorldGenerationException>(() => SeededArchipelago.Generate(42, configuration: configuration));
        Assert.Contains("12 towns", failure.Message);
        Assert.Contains("bounded candidates", failure.Message);
        Assert.Contains("placed 1/12", failure.Message);
        Assert.Equal(1, configuration.Geography.PlacementAttempts);
    }

    private static void AssertGeometry(ArchipelagoGeography map, WorldGenerationConfiguration configuration)
    {
        var settings = configuration.Geography;
        var positions = map.Positions.Values.ToArray();
        foreach (var position in positions)
        {
            Assert.InRange(position.X, settings.Margin, settings.Width - settings.Margin);
            Assert.InRange(position.Y, settings.Margin, settings.Height - settings.Margin);
            foreach (var other in positions.Where(other => other.PortId != position.PortId))
            {
                var dx = (long)position.X - other.X;
                var dy = (long)position.Y - other.Y;
                Assert.True(dx * dx + dy * dy >= (long)settings.MinimumTownSpacing * settings.MinimumTownSpacing);
            }
        }
        Assert.Equal(map.Routes.Count, map.Routes.Select(route => route.OriginPortId + "/" + route.DestinationPortId)
            .Distinct(StringComparer.Ordinal).Count());
        foreach (var route in map.Routes)
        {
            Assert.True(string.CompareOrdinal(route.OriginPortId, route.DestinationPortId) < 0);
            var first = map.Positions[route.OriginPortId];
            var second = map.Positions[route.DestinationPortId];
            var dx = (long)first.X - second.X;
            var dy = (long)first.Y - second.Y;
            var square = dx * dx + dy * dy;
            Assert.True((long)(route.Distance - 1) * (route.Distance - 1) < square);
            Assert.True((long)route.Distance * route.Distance >= square);
            Assert.InRange(route.Distance, settings.MinimumLaneDistance, settings.MaximumLaneDistance);
            Assert.Equal(route.Distance, route.DurationDays(1));
            Assert.Equal(1, route.DurationDays(int.MaxValue));
        }
        var visited = new HashSet<string>(StringComparer.Ordinal) { positions[0].PortId };
        var frontier = new Queue<string>(visited);
        while (frontier.Count != 0)
        {
            var current = frontier.Dequeue();
            foreach (var route in map.Routes.Where(route => route.OriginPortId == current || route.DestinationPortId == current))
            {
                var next = route.OriginPortId == current ? route.DestinationPortId : route.OriginPortId;
                if (visited.Add(next)) frontier.Enqueue(next);
            }
        }
        Assert.Equal(map.Positions.Count, visited.Count);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("+42", 42)]
    [InlineData("00042", 42)]
    [InlineData("-2147483648", int.MinValue)]
    [InlineData("2147483647", int.MaxValue)]
    public void SeedSyntaxAcceptsTheFullSignedAsciiIntegerRange(string text, int expected)
    {
        Assert.True(SeededArchipelago.TryParseSeed(text, out var seed));
        Assert.Equal(expected, seed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData(" 42")]
    [InlineData("42 ")]
    [InlineData("1,000")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    [InlineData("\u221242")]
    [InlineData("\u0664\u0662")]
    public void AmbiguousOrOutOfRangeSeedsAreRejected(string? text) =>
        Assert.False(SeededArchipelago.TryParseSeed(text, out _));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void UnknownGeneratorVersionsAreRejected(int version) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SeededArchipelago.Generate(0, version));


}
