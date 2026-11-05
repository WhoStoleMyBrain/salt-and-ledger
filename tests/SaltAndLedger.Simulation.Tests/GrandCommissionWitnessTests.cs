using System;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionWitnessTests
{
    [Fact]
    public void CurrentCommissionWinsThroughTradingProductionItineraryAndExactVoyageSave()
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var final = GrandCommissionWitness.Victory(content, 99, itinerary: true).Snapshot();
        Assert.Equal(ChallengeStatus.Succeeded, final.Challenge!.Status);
        Assert.Equal(4, GrandCommission.Progress(final).CurrentlyMetCount);
        Assert.True(final.Companies[WorldIds.PlayerOwnerId].Cash >= GrandCommission.MinimumCash);
        Assert.InRange(final.Day, 1, GrandCommission.DeadlineDay);
        Assert.NotNull(final.Ships[WorldIds.PlayerShipId].Itinerary);
        Assert.False(final.Ships[WorldIds.PlayerShipId].AutomationEnabled);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(42)]
    public void ActualDifferentiatedCommandJourneysWinWithPaidInputsAndExactSaveContinuation(
        int seed)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var world = GrandCommissionWitness.Victory(content, seed);
        var final = world.Snapshot();
        Assert.InRange(final.Day, 1, GrandCommission.DeadlineDay);
        Assert.True(final.Companies[WorldIds.PlayerOwnerId].Cash >= GrandCommission.MinimumCash);
        Assert.True(final.Accounting.Companies[WorldIds.PlayerOwnerId].WagesPaid > 0);
        Assert.True(new RunResults(final).TotalScore >= 1500);
        Assert.InRange(final.Ports.Count, 8, 12);
        Assert.Equal(final.Ports.Count, final.CommissionHistory!.Ships[0].Visits.Count);
        Assert.Equal(4, GrandCommission.Progress(final).CurrentlyMetCount);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Same(final, world.Snapshot());
        var retry = RunScenario.Create(content, final.RunIdentity!);
        Assert.Equal(0, retry.Snapshot().Day);
        Assert.Equal(final.RunIdentity, retry.Snapshot().RunIdentity);
        Assert.Equal(ChallengeStatus.Active, retry.Snapshot().Challenge!.Status);
    }
}
