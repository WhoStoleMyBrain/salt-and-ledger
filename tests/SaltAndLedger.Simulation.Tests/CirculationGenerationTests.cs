using System;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class CirculationGenerationTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());

    internal static RunIdentity Identity(int version = 1, int seed = 42,
        LivingPricingMode pricing = LivingPricingMode.Coverage, bool commission = false)
    {
        var draft = GrandCommissionDraft.Generate(seed);
        return new RunIdentity(commission ? GrandCommission.ChallengeId : null, RunScenario.IndustrialCharter,
            RunWorldMode.ExpandingEconomy, seed, RunDifficulty.SlowFoundations, livingPricingMode: pricing,
            commission: commission ? draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId)) : null,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1, economicRulesVersion: version);
    }

    [Fact]
    public void EconomicRevisionIsExplicitAndCannotReinterpretHistoricalIdentities()
    {
        var historical = Identity(0);
        var current = Identity();
        Assert.DoesNotContain("EconomicRulesVersion", JsonConvert.SerializeObject(historical));
        Assert.EndsWith("/economy-v1", current.ComparisonGroup);
        Assert.NotEqual(historical, current);
        Assert.Equal(historical.GeneratorVersion, current.GeneratorVersion);
        Assert.Equal(historical.ContentVersion, current.ContentVersion);
        Assert.Equal(historical.ValuationVersion, current.ValuationVersion);
        Assert.Throws<ArgumentOutOfRangeException>(() => Identity(2));
        Assert.Equal(current.EconomicRulesVersion, new RunIdentity(null, RunScenario.MerchantCharter).EconomicRulesVersion);
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            companyStorageRules: CompanyStorageRules.Legacy));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            itineraryVersion: 0));
        Assert.Null(RunScenario.Create(Content(), historical).Snapshot().Circulation);
        Assert.Equal(CirculationMode.Combined, RunScenario.Create(Content(), current).Snapshot().Circulation!.Configuration.Mode);
        Assert.Equal(Identity(0, commission: true).Commission!.Signature, Identity(1, commission: true).Commission!.Signature);
    }

    [Fact]
    public void ExplicitFiniteOpeningStockAndCashAreIndependentOfCirculationTreatment()
    {
        foreach (var seed in Enumerable.Range(0, 64).Select(i => unchecked(i * 104729 * (i % 2 == 0 ? 1 : -1)))
            .Concat(new[] { int.MinValue, int.MaxValue, -734282136 }))
        {
            var control = ExpandingEconomyGeneration.Generate(seed, economicRulesVersion: 0);
            var current = ExpandingEconomyGeneration.Generate(seed, economicRulesVersion: 1);
            Assert.Equal(control.Geography.Signature, current.Geography.Signature);
            Assert.Equal(JsonConvert.SerializeObject(control.Blueprints), JsonConvert.SerializeObject(current.Blueprints));
            Assert.Equal(control.Configuration.Fingerprint, current.Configuration.Fingerprint);
            Assert.NotEqual(control.Signature, current.Signature);
            foreach (var port in current.Ports)
            {
                var previous = control.Ports.Single(p => p.Id == port.Id);
                Assert.Equal(previous.Market.InitialMarketCash, port.Market.InitialMarketCash);
                Assert.Equal(previous.Settlement!.InitialHouseholdCash, port.Settlement!.InitialHouseholdCash);
                Assert.Equal(JsonConvert.SerializeObject(previous.Facilities), JsonConvert.SerializeObject(port.Facilities));
                Assert.Equal(JsonConvert.SerializeObject(previous.Consumption), JsonConvert.SerializeObject(port.Consumption));
                Assert.Equal(previous.Market.InitialQuantities, port.Market.InitialQuantities);
                foreach (var good in port.Market.InitialQuantities)
                {
                    var range = current.Configuration.StartingInventories[good.Key];
                    Assert.InRange(good.Value, EconomicUnits.FromWhole(range.Minimum), EconomicUnits.FromWhole(range.Maximum));
                }
                Assert.InRange(port.Market.InitialMarketCash, EconomicUnits.Coins(current.Configuration.MarketCash.Minimum),
                    EconomicUnits.Coins(current.Configuration.MarketCash.Maximum));
            }
            foreach (var charter in RunScenario.Charters)
                Assert.Equal(JsonConvert.SerializeObject(ExpandingEconomyScenario.GetCharter(control, charter.Id)),
                    JsonConvert.SerializeObject(ExpandingEconomyScenario.GetCharter(current, charter.Id)));
        }
    }

    [Fact]
    public void CoverageLuxuryFloorIsAnAuthoredStockTargetNotEligibilityOrUniformPricing()
    {
        var old = RunScenario.Create(Content(), Identity(0)).Snapshot();
        var current = RunScenario.Create(Content(), Identity()).Snapshot();
        foreach (var town in current.Ports.Values)
        {
            Assert.Equal(old.Ports[town.Id].City!.LuxuryEnabled, town.City!.LuxuryEnabled);
            Assert.Equal(Math.Max(20000, old.Ports[town.Id].Quotes["furniture"].Pricing!.TargetStock),
                town.Quotes["furniture"].Pricing!.TargetStock);
            Assert.Equal(town.Configuration.Market.Quotes["furniture"].Pricing!.ReferencePrice,
                town.Quotes["furniture"].Pricing!.ReferencePrice);
            var derived = LivingCityPricing.Derive(town.Configuration, town.City, 1);
            Assert.Equal(town.Quotes["furniture"].Pricing!.TargetStock, derived["furniture"].Pricing!.TargetStock);
            var quote = town.Quotes["furniture"];
            foreach (var stock in new[] { 0, 1, 19999, 20000, 20001, 80000, 1000000 })
            {
                var sell = quote.Price(new TradeCommand(TradeDirection.Sell, "furniture", 10000), stock);
                var splitSell = quote.Price(new TradeCommand(TradeDirection.Sell, "furniture", 3333), stock).TotalAmount +
                    quote.Price(new TradeCommand(TradeDirection.Sell, "furniture", 6667), stock + 3333).TotalAmount;
                Assert.Equal(sell.TotalAmount, splitSell);
                var buy = quote.Price(new TradeCommand(TradeDirection.Buy, "furniture", 10000), stock + 10000);
                var splitBuy = quote.Price(new TradeCommand(TradeDirection.Buy, "furniture", 6667), stock + 10000).TotalAmount +
                    quote.Price(new TradeCommand(TradeDirection.Buy, "furniture", 3333), stock + 3333).TotalAmount;
                Assert.Equal(buy.TotalAmount, splitBuy);
                Assert.True(buy.TotalAmount >= sell.TotalAmount);
            }
        }
        Assert.True(current.Ports.Values.Select(p => p.Quotes["furniture"].Pricing!.ReferencePrice).Distinct().Count() > 1);
        var stocked = RunScenario.Create(Content(), Identity(pricing: LivingPricingMode.StockOnly)).Snapshot();
        Assert.All(stocked.Ports.Values, p => Assert.Same(p.Configuration.Market.Quotes["furniture"], p.Quotes["furniture"]));
    }

    [Theory]
    [InlineData(CirculationMode.None)]
    [InlineData(CirculationMode.FeesOnly)]
    [InlineData(CirculationMode.MunicipalOnly)]
    public void DiagnosticTreatmentsShareGenerationButCannotPretendToBeDurableCombinedRuns(CirculationMode mode)
    {
        var diagnostic = ExpandingEconomyScenario.Create(Content(), Identity(), circulation: new CirculationConfiguration(mode: mode));
        var combined = RunScenario.Create(Content(), Identity()).Snapshot();
        foreach (var port in diagnostic.Snapshot().Ports.Values)
            Assert.Equal(combined.Ports[port.Id].Inventory, port.Inventory);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Save(diagnostic));
        Assert.Throws<ArgumentException>(() => ExpandingEconomyScenario.Create(Content(), Identity(0),
            circulation: new CirculationConfiguration(mode: mode)));
    }
}
