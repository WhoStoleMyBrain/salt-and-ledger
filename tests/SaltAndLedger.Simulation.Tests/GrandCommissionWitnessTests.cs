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
        Assert.Equal(6226936, final.Companies[WorldIds.PlayerOwnerId].Cash);
        Assert.Equal(18, final.Day);
        Assert.NotNull(final.Ships[WorldIds.PlayerShipId].Itinerary);
        Assert.False(final.Ships[WorldIds.PlayerShipId].AutomationEnabled);
    }

    [Theory]
    [InlineData(99, 18, 6226936, 240000, 2162)]
    [InlineData(42, 13, 2858935, 420000, 2128)]
    public void ActualDifferentiatedCommandJourneysWinWithPaidInputsAndExactSaveContinuation(
        int seed, int day, long cash, long wages, int score)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var world = GrandCommissionWitness.Victory(content, seed);
        var final = world.Snapshot();
        Assert.Equal(day, final.Day);
        Assert.Equal(cash, final.Companies[WorldIds.PlayerOwnerId].Cash);
        Assert.Equal(wages, final.Accounting.Companies[WorldIds.PlayerOwnerId].WagesPaid);
        Assert.Equal(score, new RunResults(final).TotalScore);
        Assert.Equal(4, GrandCommission.Progress(final).CurrentlyMetCount);
        Assert.Throws<InvalidOperationException>(() => world.AdvanceDay());
        Assert.Same(final, world.Snapshot());
        var retry = RunScenario.Create(content, final.RunIdentity!);
        Assert.Equal(0, retry.Snapshot().Day);
        Assert.Equal(final.RunIdentity, retry.Snapshot().RunIdentity);
        Assert.Equal(ChallengeStatus.Active, retry.Snapshot().Challenge!.Status);
    }
}
