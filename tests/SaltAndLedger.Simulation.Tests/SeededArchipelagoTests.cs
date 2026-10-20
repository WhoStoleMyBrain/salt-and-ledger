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
    [InlineData(0, "eastport@717,173;northport@789,473;southhaven@286,202;westhaven@269,534", "309,432,576,572,524,333")]
    [InlineData(1, "eastport@265,517;northport@225,211;southhaven@784,188;westhaven@730,528", "309,615,466,560,597,345")]
    [InlineData(-1, "eastport@239,476;northport@785,168;southhaven@247,229;westhaven@781,478", "627,248,543,542,311,590")]
    [InlineData(int.MinValue, "eastport@733,512;northport@242,232;southhaven@743,232;westhaven@267,533", "566,281,467,501,303,564")]
    [InlineData(int.MaxValue, "eastport@217,520;northport@258,185;southhaven@780,531;westhaven@776,213", "338,564,638,627,519,319")]
    [InlineData(42, "eastport@715,475;northport@246,502;southhaven@787,160;westhaven@222,200", "470,324,565,641,303,567")]
    public void VersionOneGoldenVectorsPreserveCoordinatesAndDistances(int seed, string positions, string distances)
    {
        var map = SeededArchipelago.Generate(seed);
        Assert.Equal(1, map.GeneratorVersion);
        Assert.Equal(positions, map.Signature.Split('|')[1]);
        Assert.Equal(distances, string.Join(",", map.Routes.Select(route => route.Distance)));
        Assert.Equal(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(map)),
            Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(SeededArchipelago.Generate(seed))));
    }

    [Fact]
    public void BoundedSlotsGuaranteeNavigableSeparatedGeographyAcrossAStableWideSample()
    {
        // The slots prove the bounds for every int32 seed; this sample catches implementation drift.
        var seeds = Enumerable.Range(0, 4096).Select(index => unchecked(index * 104729))
            .Concat(new[] { int.MinValue, int.MaxValue });
        foreach (var seed in seeds)
        {
            var map = SeededArchipelago.Generate(seed);
            Assert.Equal(new[] { "eastport", "northport", "southhaven", "westhaven" }, map.Positions.Keys);
            Assert.Equal(6, map.Routes.Count);
            Assert.Equal(6, map.Routes.Select(route => route.OriginPortId + "/" + route.DestinationPortId).Distinct().Count());
            foreach (var position in map.Positions.Values)
            {
                Assert.InRange(position.X, SeededArchipelago.MinimumX, SeededArchipelago.MaximumX);
                Assert.InRange(position.Y, SeededArchipelago.MinimumY, SeededArchipelago.MaximumY);
            }
            foreach (var route in map.Routes)
            {
                Assert.True(string.CompareOrdinal(route.OriginPortId, route.DestinationPortId) < 0);
                var first = map.Positions[route.OriginPortId];
                var second = map.Positions[route.DestinationPortId];
                var square = (first.X - second.X) * (first.X - second.X) + (first.Y - second.Y) * (first.Y - second.Y);
                Assert.True(square >= SeededArchipelago.MinimumSeparation * SeededArchipelago.MinimumSeparation);
                Assert.True((route.Distance - 1) * (route.Distance - 1) < square);
                Assert.True(route.Distance * route.Distance >= square);
                Assert.InRange(route.Distance, SeededArchipelago.MinimumSeparation, SeededArchipelago.MaximumDistance);
                Assert.InRange(route.DurationDays(100), 3, 7);
                Assert.Equal(route.Distance, route.DurationDays(1));
                Assert.Equal(1, route.DurationDays(int.MaxValue));
            }
        }
    }

    [Fact]
    public void ChangingSeedsChangesEconomicTravelRelationshipsAndCollectionsAreImmutable()
    {
        var times = Enumerable.Range(0, 100).Select(seed => SeededArchipelago.Generate(seed).Routes
            .Single(route => route.OriginPortId == EnterpriseScenario.East && route.DestinationPortId == EnterpriseScenario.North)
            .DurationDays(100)).Distinct().ToArray();
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, times.OrderBy(value => value));
        var map = SeededArchipelago.Generate(0);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, PortPosition>)map.Positions).Remove(EnterpriseScenario.North));
        Assert.Throws<NotSupportedException>(() => ((IList<SeaRoute>)map.Routes).Clear());
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
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void UnknownGeneratorVersionsAreRejected(int version) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SeededArchipelago.Generate(0, version));

    [Fact]
    public void ClassicRetainsExactlyTheAuthoredCoordinatesAndIndependentRouteDistances()
    {
        var map = ArchipelagoGeography.Classic;
        Assert.Equal("v0|eastport@850,250;northport@340,120;southhaven@570,550;westhaven@120,400|" +
            "eastport/northport:400;eastport/southhaven:300;eastport/westhaven:600;" +
            "northport/southhaven:300;northport/westhaven:200;southhaven/westhaven:400", map.Signature);
    }
}
