using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class RunScenarioTests
{
    private const string Player = EnterpriseScenario.PlayerShipId;
    private const string Owner = EnterpriseScenario.PlayerOwnerId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState Create(string charter = RunScenario.MerchantCharter, string? challenge = RunScenario.CoastalChallenge) =>
        RunScenario.Create(Content(), challenge, charter);

    [Theory]
    [InlineData(RunScenario.MerchantCharter, 600, 10, 0, 0, EnterpriseScenario.North)]
    [InlineData(RunScenario.IndustrialCharter, 300, 8, 0, 1, EnterpriseScenario.West)]
    [InlineData(RunScenario.IndependentCharter, 480, 12, 6, 0, EnterpriseScenario.North)]
    public void ChartersReplaceInitialAssetsWithoutChargingForGrants(string charter, long cash, int capacity,
        int cargo, int factories, string port)
    {
        var state = Create(charter).Snapshot();
        Assert.Equal(cash, state.Companies[Owner].Cash);
        Assert.Equal(capacity, state.Ships[Player].CargoCapacity);
        Assert.Equal(cargo, state.Ships[Player].CargoUnits);
        Assert.Equal(port, state.Ships[Player].DockedPortId);
        Assert.Equal(factories, state.PrivateFacilities.Count);
        var finance = state.Accounting.Companies[Owner];
        Assert.Equal(cash, finance.InitialCash);
        Assert.Equal(0, finance.InventoryPurchases);
        Assert.Equal(0, finance.ConstructionExpenditure);
        Assert.Equal(0, finance.HeldInventoryCost);
        foreach (var store in state.Accounting.Stores.Values.Where(s => s.OwnerId == Owner))
            foreach (var lot in store.Goods.Values.SelectMany(g => g.Lots))
            {
                Assert.Equal(InventoryCostOrigin.StarterGrant, lot.Origin);
                Assert.Equal(charter, lot.Provenance);
            }
        if (factories > 0)
        {
            var bakery = state.PrivateFacilities[RunScenario.StarterBakery];
            Assert.True(bakery.IsStarterGrant);
            Assert.Equal(charter, bakery.Provenance);
            Assert.Equal(2, bakery.Inventory["flour"]);
            Assert.Equal(2, bakery.Inventory["logs"]);
            Assert.Equal(2, bakery.NextProductionDay);
        }
    }

    [Fact]
    public void SetupChoicesAreValidatedAndFreshRetryRestoresExactlySelectedAssets()
    {
        Assert.Throws<ArgumentException>(() => Create("missing"));
        Assert.Throws<ArgumentException>(() => Create(challenge: "missing"));
        var original = Create(RunScenario.IndustrialCharter);
        var initial = Digest(original.Snapshot());
        original.ConfigureFacility(Owner, RunScenario.StarterBakery, EnterpriseScenario.BakeryPolicy());
        original.AdvanceDay();
        original.AdvanceDay();
        original.AbandonChallenge();
        Assert.Equal(initial, Digest(Create(RunScenario.IndustrialCharter).Snapshot()));
        Assert.NotEqual(initial, Digest(Create(RunScenario.MerchantCharter).Snapshot()));
        Assert.Empty(Create(RunScenario.MerchantCharter).Snapshot().PrivateFacilities);
    }

    public static IEnumerable<object[]> Pairings() =>
        from challenge in RunScenario.Challenges
        from charter in RunScenario.Charters
        select new object[] { challenge.Id, charter.Id };

    [Theory]
    [MemberData(nameof(Pairings))]
    public void EveryChallengeAndCharterHasAnExecutableWinningStrategy(string challenge, string charter)
    {
        var world = Create(charter, challenge);
        var initial = world.Snapshot();
        foreach (var cargo in initial.Ships[Player].Cargo.Where(p => p.Value > 0))
            world.ExecuteTrade(Player, initial.Ships[Player].DockedPortId!, new TradeCommand(TradeDirection.Sell, cargo.Key, cargo.Value));
        if (charter == RunScenario.IndustrialCharter || challenge == RunScenario.AmbitionChallenge)
        {
            var facility = RunScenario.StarterBakery;
            if (!world.Snapshot().PrivateFacilities.ContainsKey(facility))
            {
                facility = "built_bakery";
                world.ConstructFacility(Owner, EnterpriseScenario.West, EnterpriseScenario.BakeryBlueprint, facility);
            }
            world.ConfigureFacility(Owner, facility, EnterpriseScenario.BakeryPolicy());
            if (world.Snapshot().Ships[Player].DockedPortId != EnterpriseScenario.West)
            {
                world.Depart(Player, EnterpriseScenario.West);
                while (world.Snapshot().Ships[Player].DockedPortId == null) world.AdvanceDay();
            }
            world.ConfigureIndustrialAutomation(Player, RunScenario.BakeryShippingPolicy(facility), true);
        }
        else world.SetAutomationEnabled(Player, true);
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        var final = world.Snapshot();
        Assert.Equal(ChallengeStatus.Succeeded, final.Challenge!.Status);
        Assert.True(final.Day <= final.Challenge.Configuration.DeadlineDay);
        var result = new RunResults(final, charter);
        Assert.InRange(result.TotalScore, 1500, 2200);
        Assert.Equal(result.TotalScore, new RunResults(final, charter).TotalScore);
        Assert.Equal(result.Finance.InventoryPurchases, result.Finance.CostOfGoodsSold + result.Finance.HeldInventoryCost);
        Assert.Equal(final.Companies[Owner].Cash, result.Finance.InitialCash - result.Finance.InventoryPurchases -
            result.Finance.ConstructionExpenditure + result.Finance.SalesRevenue);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => world.SetAutomationEnabled(Player, false));
        Assert.Throws<InvalidOperationException>(() => world.ConstructFacility(Owner, EnterpriseScenario.West,
            EnterpriseScenario.BakeryBlueprint, "another"));
        Assert.Same(final, world.Snapshot());
    }

    [Theory]
    [MemberData(nameof(Pairings))]
    public void EveryIdleRunFailsAtItsDeadlineWithoutGrantedMoney(string challenge, string charter)
    {
        var world = Create(charter, challenge);
        var cash = world.Snapshot().Companies[Owner].Cash;
        while (world.Snapshot().Challenge!.Status == ChallengeStatus.Active) world.AdvanceDay();
        Assert.Equal(60, world.Snapshot().Day);
        Assert.Equal(ChallengeStatus.Failed, world.Snapshot().Challenge!.Status);
        Assert.Equal(cash, world.Snapshot().Companies[Owner].Cash);
        Assert.Equal(0, new RunResults(world.Snapshot(), charter).VictoryScore);
        Assert.Equal(0, new RunResults(world.Snapshot(), charter).TimeScore);
    }

    [Fact]
    public void RegionalReliefCountsScheduledConsumptionRatherThanSalesAndCanFinishOnDeadline()
    {
        var world = ReliefWorld();
        world.ExecuteTrade(Player, "east", new TradeCommand(TradeDirection.Sell, "bread", 2));
        Assert.Equal(2, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(0, world.Snapshot().Challenge!.ObjectiveQuantity);
        Assert.Equal(ChallengeStatus.Active, world.Snapshot().Challenge!.Status);
        world.AdvanceDay();
        Assert.Equal(0, world.Snapshot().Challenge!.RegionalConsumedQuantity);
        world.AdvanceDay();
        Assert.Equal(2, world.Snapshot().Challenge!.RegionalConsumedQuantity);
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
    }

    [Fact]
    public void RegionalReliefIsExplicitlyWorldWideAndIncludesUnpaidPublicConsumption()
    {
        var world = ReliefWorld(publicBread: 2);
        world.AdvanceDay();
        world.AdvanceDay();
        Assert.Equal(0, world.Snapshot().Challenge!.NetDeliveredQuantity);
        Assert.Equal(2, world.Snapshot().Challenge!.ObjectiveQuantity);
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
    }

    [Fact]
    public void AbandonmentKeepsHonestFinancesAndFreezesCommands()
    {
        var world = Create();
        world.ExecuteTrade(Player, EnterpriseScenario.North, new TradeCommand(TradeDirection.Buy, "bread", 2));
        var before = world.Snapshot();
        var abandoned = world.AbandonChallenge();
        Assert.Equal(ChallengeStatus.Abandoned, abandoned.After.Challenge!.Status);
        Assert.Equal(before.Day, abandoned.After.Day);
        Assert.Equal(before.Revision + 1, abandoned.After.Revision);
        Assert.Equal(JsonConvert.SerializeObject(before.Accounting), JsonConvert.SerializeObject(abandoned.After.Accounting));
        Assert.Empty(abandoned.EconomicEvents);
        var result = new RunResults(abandoned.After, RunScenario.MerchantCharter);
        Assert.Equal(0, result.VictoryScore);
        Assert.Equal(0, result.TimeScore);
        Assert.True(result.Finance.HeldInventoryCost > 0);
        Assert.Throws<InvalidOperationException>(() => world.AbandonChallenge());
        Assert.Throws<InvalidOperationException>(() => world.Depart(Player, EnterpriseScenario.East));
        Assert.Throws<InvalidOperationException>(() => world.ExecuteTrade(Player, EnterpriseScenario.North,
            new TradeCommand(TradeDirection.Sell, "bread", 1)));
    }

    [Fact]
    public void SandboxHasNoDeadlineOrScoreAndActiveRunHasNoFinalResults()
    {
        var sandbox = Create(challenge: null);
        for (var day = 0; day < 65; day++) sandbox.AdvanceDay();
        Assert.Null(sandbox.Snapshot().Challenge);
        Assert.Throws<InvalidOperationException>(() => sandbox.AbandonChallenge());
        Assert.Throws<ArgumentException>(() => new RunResults(sandbox.Snapshot(), RunScenario.MerchantCharter));
        Assert.Throws<InvalidOperationException>(() => new RunResults(Create().Snapshot(), RunScenario.MerchantCharter));
    }

    private static RegionalWorldState ReliefWorld(int publicBread = 0)
    {
        RegionalPortConfiguration Port(string id) => new(new PortConfiguration(id,
            new[] { new MarketQuote("bread", 10, 11) }, 100,
            Array.Empty<BuildingInstance>(), new Dictionary<string, int> { ["bread"] = id == "west" ? publicBread : 0 }),
            id, 0, 0, Array.Empty<ScheduledFacilityConfiguration>(),
            new[] { new ConsumptionConfiguration(id + "_food", "bread", 2, 2) });
        return new RegionalWorldState(Content(), new[] { Port("east"), Port("west") },
            new[] { new SeaRoute("east", "west", 100) },
            new[] { new RegionalShipConfiguration(new ShipConfiguration(Player, 2, "east",
                new Dictionary<string, int> { ["bread"] = 2 }), Owner, "Player", 100, 100) },
            challenge: new ChallengeConfiguration("relief", "Relief", Owner, "east", "bread", 2, 2, 0, ChallengeKind.RegionalRelief));
    }

    private static string Digest(RegionalWorldSnapshot state) => JsonConvert.SerializeObject(new
    {
        state.Day, state.Revision, state.Accounting, state.PrivateFacilities, state.Companies, state.Challenge,
        Ships = state.Ships.Values.Select(s => new { s.Id, s.Cargo, s.CargoCapacity, s.DockedPortId })
    });
}
