using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class SeededRunTests
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RunIdentity Identity(int seed = 42, string charter = RunScenario.MerchantCharter,
        string? challenge = RunScenario.CoastalChallenge, RunDifficulty difficulty = RunDifficulty.Normal) =>
        new(challenge, charter, RunWorldMode.Seeded, seed, difficulty);

    [Fact]
    public void RunIdentityValidatesItsWholeConfigurationAndUsesStableValueEquality()
    {
        var identity = Identity(int.MinValue, difficulty: RunDifficulty.SlowFoundations);
        var equal = Identity(int.MinValue, difficulty: RunDifficulty.SlowFoundations);
        Assert.Equal(identity, equal);
        Assert.Equal(identity.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(identity, Identity(int.MinValue));
        Assert.NotEqual(identity, Identity(int.MaxValue, difficulty: RunDifficulty.SlowFoundations));
        Assert.Equal(2, identity.ConstructionDelayDays);
        Assert.Equal(1, identity.Ascension);
        Assert.Equal("coastal_supply_charter/merchant_company/ascension1/seeded-v1-seed-2147483648", identity.ComparisonGroup);
        var classic = new RunIdentity(null, RunScenario.IndustrialCharter);
        Assert.Null(classic.Seed);
        Assert.Equal(0, classic.GeneratorVersion);
        Assert.Equal(0, classic.ConstructionDelayDays);
        Assert.Equal("sandbox/industrial_consortium/ascension0/classic", classic.ComparisonGroup);
        Assert.Throws<ArgumentException>(() => new RunIdentity("missing", RunScenario.MerchantCharter));
        Assert.Throws<ArgumentException>(() => new RunIdentity(null, "missing"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter, (RunWorldMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter, difficulty: (RunDifficulty)2));
        Assert.Throws<ArgumentException>(() => new RunIdentity(null, RunScenario.MerchantCharter, seed: 1));
        Assert.Throws<ArgumentException>(() => new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.Seeded));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter, generatorVersion: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.Seeded, 1, generatorVersion: 0));
        Assert.Throws<ArgumentNullException>(() => RunScenario.Create(Content(), null!));
        Assert.Throws<ArgumentNullException>(() => RunScenario.PreviewGeography(null!));
    }

    public static IEnumerable<object[]> Choices() =>
        from challenge in RunScenario.Challenges
        from charter in RunScenario.Charters
        from difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations }
        select new object[] { challenge.Id, charter.Id, difficulty };

    [Theory]
    [MemberData(nameof(Choices))]
    public void SeededSetupChangesOnlyGeographyAndCarriesTheChosenDifficulty(string challenge, string charter, RunDifficulty difficulty)
    {
        var identity = Identity(int.MinValue, charter, challenge, difficulty);
        var baseline = RunScenario.Create(Content(), challenge, charter).Snapshot();
        var actual = RunScenario.Create(Content(), identity).Snapshot();
        Assert.Same(identity, actual.RunIdentity);
        Assert.Equal(difficulty, actual.Difficulty);
        Assert.Equal(Fingerprint(baseline, false), Fingerprint(actual, false));
        Assert.Equal(6, actual.Ports[EnterpriseScenario.North].Inventory.Count);
        Assert.Equal(4, actual.Ships.Count);
        var preview = RunScenario.PreviewGeography(identity);
        foreach (var position in preview.Positions.Values)
            Assert.Equal((position.X, position.Y), (actual.Ports[position.PortId].X, actual.Ports[position.PortId].Y));
        Assert.Equal(JsonConvert.SerializeObject(preview.Routes), JsonConvert.SerializeObject(actual.Routes));
        var ship = actual.Ships[Player];
        foreach (var destination in actual.Ports.Keys.Where(id => id != ship.DockedPortId))
            Assert.Equal(actual.GetRoute(ship.DockedPortId!, destination).DurationDays(ship.Configuration.Speed),
                actual.TravelDays(Player, destination));
    }

    [Theory]
    [InlineData(RunScenario.MerchantCharter)]
    [InlineData(RunScenario.IndustrialCharter)]
    [InlineData(RunScenario.IndependentCharter)]
    public void RecreatingAnIdentityRestoresTheCompleteInitialWorldAfterEconomicActivity(string charter)
    {
        var identity = Identity(-42, charter, difficulty: RunDifficulty.SlowFoundations);
        var world = RunScenario.Create(Content(), identity);
        var initial = Fingerprint(world.Snapshot());
        foreach (var cargo in world.Snapshot().Ships[Player].Cargo.Where(item => item.Value > 0))
            world.ExecuteTrade(Player, world.Snapshot().Ships[Player].DockedPortId!,
                new TradeCommand(TradeDirection.Sell, cargo.Key, cargo.Value));
        if (charter == RunScenario.IndustrialCharter)
            world.ConfigureFacility(Owner, RunScenario.StarterBakery, EnterpriseScenario.BakeryPolicy());
        else world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, "retry_bakery");
        world.Depart(Player, world.Snapshot().Ships[Player].DockedPortId == EnterpriseScenario.East ? EnterpriseScenario.West : EnterpriseScenario.East);
        for (var day = 0; day < 6; day++) world.AdvanceDay();
        world.AbandonChallenge();
        Assert.NotEqual(initial, Fingerprint(world.Snapshot()));
        Assert.Equal(initial, Fingerprint(RunScenario.Create(Content(), identity).Snapshot()));
        Assert.Equal(initial, Fingerprint(RunScenario.Create(Content(), Identity(-42, charter,
            difficulty: RunDifficulty.SlowFoundations)).Snapshot()));
        Assert.NotEqual(initial, Fingerprint(RunScenario.Create(Content(), Identity(99, charter,
            difficulty: RunDifficulty.SlowFoundations)).Snapshot()));
        Assert.Equal(-42, identity.Seed);
    }

    [Fact]
    public void ReversedConfigurationAndAlternateCulturePreserveACompleteSeededRun()
    {
        string Run(bool reverse)
        {
            var world = RunScenario.Create(Content(), Identity(-42), reverse);
            world.SetAutomationEnabled(Player, true);
            while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
            Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
            return Fingerprint(world.Snapshot());
        }
        var expected = Run(false);
        Assert.Equal(expected, Run(true));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal(expected, Run(true));
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Fact]
    public void ClassicIdentityRetainsLegacyInitialStateAndCompleteRunEconomics()
    {
        var classicIdentity = new RunIdentity(RunScenario.CoastalChallenge, RunScenario.MerchantCharter);
        var legacy = RunScenario.Create(Content(), RunScenario.CoastalChallenge, RunScenario.MerchantCharter);
        var classic = RunScenario.Create(Content(), classicIdentity);
        Assert.Equal(Fingerprint(legacy.Snapshot(), includeIdentity: false), Fingerprint(classic.Snapshot(), includeIdentity: false));
        Assert.Same(ArchipelagoGeography.Classic, RunScenario.PreviewGeography(classicIdentity));
        legacy.SetAutomationEnabled(Player, true);
        classic.SetAutomationEnabled(Player, true);
        while (legacy.Snapshot().Challenge!.Status == ChallengeStatus.Active)
        {
            legacy.AdvanceDay();
            classic.AdvanceDay();
            Assert.Equal(Fingerprint(legacy.Snapshot(), includeIdentity: false), Fingerprint(classic.Snapshot(), includeIdentity: false));
        }
        Assert.Equal(29, classic.Snapshot().Day);
        Assert.Equal(1014, classic.Snapshot().Companies[Owner].Cash);
        Assert.Equal(1910, new RunResults(classic.Snapshot()).TotalScore);
        Assert.Equal("coastal_supply_charter/merchant_company", new RunResults(legacy.Snapshot(), RunScenario.MerchantCharter).ComparisonGroup);
        Assert.Equal(classicIdentity.ComparisonGroup, new RunResults(classic.Snapshot(), RunScenario.MerchantCharter).ComparisonGroup);
        Assert.Throws<ArgumentException>(() => new RunResults(legacy.Snapshot()));
    }

    [Fact]
    public void ResultsCarryDifficultySeedAndSelectedCharterThroughAbandonment()
    {
        var identity = Identity(42, difficulty: RunDifficulty.SlowFoundations);
        var world = RunScenario.Create(Content(), identity);
        var abandoned = world.AbandonChallenge().After;
        var result = new RunResults(abandoned, identity);
        Assert.Equal(identity, result.Identity);
        Assert.Equal(identity.ComparisonGroup, result.ComparisonGroup);
        Assert.Equal(result.ComparisonGroup, new RunResults(abandoned).ComparisonGroup);
        Assert.Equal(result.ComparisonGroup, new RunResults(abandoned, RunScenario.MerchantCharter).ComparisonGroup);
        Assert.Throws<ArgumentException>(() => new RunResults(abandoned, RunScenario.IndustrialCharter));
        Assert.Throws<ArgumentException>(() => new RunResults(abandoned, Identity(99, difficulty: RunDifficulty.SlowFoundations)));
        Assert.Equal(0, result.VictoryScore);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Same(abandoned, world.Snapshot());
        var sandbox = RunScenario.Create(Content(), Identity(challenge: null));
        Assert.Null(sandbox.Snapshot().Challenge);
        Assert.Throws<ArgumentException>(() => new RunResults(sandbox.Snapshot()));
    }

    private static string Fingerprint(RegionalWorldSnapshot snapshot, bool includeGeography = true, bool includeIdentity = true) =>
        JsonConvert.SerializeObject(new
        {
            snapshot.Day, snapshot.Revision, snapshot.Accounting, snapshot.Companies, snapshot.IndustryBlueprints,
            snapshot.PrivateFacilities, snapshot.Challenge,
            Identity = includeGeography && includeIdentity ? snapshot.RunIdentity : null,
            Ports = snapshot.Ports.Values.Select(port => new
            {
                port.Id, port.Name, port.Role, port.Inventory, port.MarketCash, port.Facilities, port.Consumption,
                Prices = port.Quotes.Values.Select(quote => new { quote.GoodId, quote.Pricing, Price = snapshot.GetMarketQuote(port.Id, quote.GoodId) })
            }),
            Ships = snapshot.Ships.Values,
            Geography = includeGeography ? new
            {
                Positions = snapshot.Ports.Values.Select(port => new { port.Id, port.X, port.Y }), snapshot.Routes
            } : null
        });
}
