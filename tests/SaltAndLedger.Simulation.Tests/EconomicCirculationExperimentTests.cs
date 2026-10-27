using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SaltAndLedger.Simulation;
using Xunit;

public sealed class EconomicCirculationExperimentTests
{
    private static ContentCatalog Content() => ContentLoader.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content.json")));

    [Fact]
    public void MatchedArmsKeepInitialAccountsAndAuditRealProductionTradeAndContinuation()
    {
        var content = Content();
        var arms = new[] { CirculationMode.None, CirculationMode.FeesOnly, CirculationMode.MunicipalOnly, CirculationMode.Combined }
            .Select(mode => EconomicCirculationExperiments.RunCase(content, 42, LivingPricingMode.StockOnly, true, 1, mode, 60)).ToArray();
        Assert.Single(arms.Select(a => a.Initial.GlobalCash).Distinct());
        Assert.Single(arms.Select(a => a.Initial.PlayerCash).Distinct());
        Assert.Single(arms.Select(a => EconomicCirculationExperiments.Json(a.Initial.Towns)).Distinct());
        Assert.All(arms, arm =>
        {
            Assert.Equal(60, arm.Final.Day);
            Assert.Equal(arm.Initial.GlobalCash, arm.Final.GlobalCash);
            Assert.Equal(arm.Initial.RegionalCash, arm.Final.RegionalCash);
            Assert.Equal(0, arm.Final.Minted);
            Assert.Equal(0, arm.Final.ExternalNetInflow);
            Assert.True(arm.Final.PlayerWages > 0);
            Assert.True(arm.Flows["transformation_output/bread"] > 0);
            Assert.True(arm.Operations > 60);
        });
        Assert.Equal(0, arms[0].Final.NpcFees);
        Assert.Equal(0, arms[0].Final.PlayerFees);
        Assert.Equal(0, arms[2].Final.NpcFees);
        Assert.True(arms[1].Final.NpcFees > 0);
        Assert.True(arms[1].Final.PlayerFees > 0);
        Assert.True(arms[3].Final.PlayerFees > 0);
        Assert.Single(arms[3].SaveChecks);
        Assert.Contains(arms[3].AccountTransfers.Keys, key => key.StartsWith("ServiceFeeMarket/", StringComparison.Ordinal));
        Assert.Contains(arms[3].AccountTransfers.Keys, key => key.StartsWith("ServiceFeeHouseholds/", StringComparison.Ordinal));
    }

    [Fact]
    public void HistoricalControlStaysUntouchedAndReversedModernReplayMatches()
    {
        var content = Content();
        var old = EconomicCirculationExperiments.RunCase(content, -42, LivingPricingMode.Coverage, false, 0, CirculationMode.None, 30);
        Assert.Null(old.Initial.Circulation);
        Assert.Null(old.Final.Circulation);
        Assert.Equal(0, old.Final.PlayerFees);
        Assert.Equal(0, old.Final.NpcFees);
        var current = EconomicCirculationExperiments.RunCase(content, -42, LivingPricingMode.Coverage, false, 1, CirculationMode.Combined, 30);
        var reverse = EconomicCirculationExperiments.RunCase(content, -42, LivingPricingMode.Coverage, false, 1, CirculationMode.Combined, 30, true);
        Assert.Equal(EconomicCirculationExperiments.Json(current), EconomicCirculationExperiments.Json(reverse));
        Assert.Equal(old.Initial.GlobalCash, current.Initial.GlobalCash);
        Assert.NotEqual(old.Identity, current.Identity);
    }

}
