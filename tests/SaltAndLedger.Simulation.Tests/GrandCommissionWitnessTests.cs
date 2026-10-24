using System;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionWitnessTests
{
    [Theory]
    [InlineData(99, 18, 6486668, 240000, 2164)]
    [InlineData(42, 13, 2956668, 420000, 2129)]
    public void ActualDifferentiatedCommandJourneysWinWithPaidInputsAndExactSaveContinuation(
        int seed, int day, long cash, long wages, int score)
    {
        var content = ContentLoader.Load(ContentLoaderTests.CanonicalJson());
        var world = GrandCommissionWitness.Victory(content, seed);
        var final = world.Snapshot();
        Assert.Equal(day, final.Day);
        Assert.Equal(cash, final.Companies[EnterpriseScenario.PlayerOwnerId].Cash);
        Assert.Equal(wages, final.Accounting.Companies[EnterpriseScenario.PlayerOwnerId].WagesPaid);
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
