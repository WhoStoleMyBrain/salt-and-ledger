using System;
using System.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionAtomicTests
{
    private const string Owner = WorldIds.PlayerOwnerId;
    private const string Ship = WorldIds.PlayerShipId;
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static RegionalWorldState World()
    {
        var plan = GrandCommissionDraft.Generate(42).Resolve(new[]
            { "all_harbors", "timber_chain", "royal_exporter", "royal_institution" });
        return RunScenario.Create(Content(), new RunIdentity(GrandCommission.ChallengeId,
            RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42, commission: plan));
    }

    private static RegionalWorldState OverflowFixture(long revision, bool oneDayRoutes = false)
    {
        var initial = World().Snapshot();
        return new RegionalWorldState(Content().ForExpandingEconomy(), initial.Ports.Values.Select(p => p.Configuration),
            oneDayRoutes ? initial.Routes.Select(r => new SeaRoute(r.OriginPortId, r.DestinationPortId, 1)) : initial.Routes,
            initial.Ships.Values.Select(s => s.Configuration), initialRevision: revision,
            companies: initial.Companies.Values.Select(c => new CompanyConfiguration(c.Id, c.Cash)),
            industryBlueprints: initial.IndustryBlueprints.Values, challenge: initial.Challenge!.Configuration,
            runIdentity: initial.RunIdentity, economy: initial.Economy!.Configuration, circulation: initial.Circulation!.Configuration);
    }

    [Fact]
    public void LateCommitOverflowRollsBackRealLocalTradeAndMissionHistoryTogether()
    {
        var world = OverflowFixture(long.MaxValue);
        var before = world.Snapshot();
        var port = before.Ships[Ship].DockedPortId!;
        Assert.Equal(TradeOutcome.Traded, before.PreviewTrade(Ship, port,
            new TradeCommand(TradeDirection.Buy, "bread", 10000)).Outcome);
        Assert.Throws<OverflowException>(() => world.ExecuteTrade(Ship, port,
            new TradeCommand(TradeDirection.Buy, "bread", 10000)));
        Assert.Same(before, world.Snapshot());
        Assert.Empty(world.Snapshot().CommissionHistory!.Markets);
        Assert.Equal(0, world.Snapshot().Accounting.Companies[Owner].InventoryPurchases);
    }

    [Fact]
    public void LateDailyOverflowRollsBackStagedArrivalVisitAndEveryEarlierDailyPhase()
    {
        var world = OverflowFixture(long.MaxValue - 1, oneDayRoutes: true);
        var origin = world.Snapshot().Ships[Ship].DockedPortId!;
        var route = world.Snapshot().Routes.First(r => r.OriginPortId == origin || r.DestinationPortId == origin);
        var destination = route.OriginPortId == origin ? route.DestinationPortId : route.OriginPortId;
        world.Depart(Ship, destination);
        var before = world.Snapshot();
        Assert.Equal(1, before.Ships[Ship].RemainingTravelDays);
        Assert.Throws<OverflowException>(() => world.AdvanceDay());
        Assert.Same(before, world.Snapshot());
        Assert.Null(world.Snapshot().Ships[Ship].DockedPortId);
        Assert.DoesNotContain(destination, world.Snapshot().CommissionHistory!.Ships.Single().Visits.Keys);
        Assert.Equal(0, world.Snapshot().Day);
    }

    [Fact]
    public void GenuineFinalHarborArrivalOnConfiguredDeadlineWinsBeforeFailureAndRestoresFrozen()
    {
        var world = GrandCommissionWitness.Victory(Content(), 42, finalArrivalDay: GrandCommission.DeadlineDay);
        Assert.Equal(GrandCommission.DeadlineDay, world.Snapshot().Day);
        Assert.Equal(GrandCommission.DeadlineDay, world.Snapshot().CommissionHistory!.Ships.Single().Visits.Values.Max());
        Assert.Equal(ChallengeStatus.Succeeded, world.Snapshot().Challenge!.Status);
        Assert.Equal(4, GrandCommission.Progress(world.Snapshot()).CurrentlyMetCount);
        Assert.True(world.Snapshot().Companies[Owner].Cash >= GrandCommission.MinimumCash);
        var terminal = WorldSaveCodec.Save(world);
        var resumed = WorldSaveCodec.Load(Content(), terminal);
        Assert.Equal(terminal, WorldSaveCodec.Save(resumed));
        Assert.Throws<InvalidOperationException>(() => resumed.Depart(Ship, resumed.Snapshot().Ports.Keys.First()));
        Assert.Throws<InvalidOperationException>(() => resumed.AdvanceDay());
        Assert.Throws<InvalidOperationException>(() => resumed.AbandonChallenge());
        Assert.Equal(terminal, WorldSaveCodec.Save(resumed));
    }
}
