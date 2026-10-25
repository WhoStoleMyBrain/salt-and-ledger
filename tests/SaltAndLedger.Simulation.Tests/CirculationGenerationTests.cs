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
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 42, economicRulesVersion: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 42, companyStorageRules: CompanyStorageRules.Pooled,
            economicRulesVersion: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunIdentity(null, RunScenario.MerchantCharter,
            economicRulesVersion: 1));
        Assert.Null(RunScenario.Create(Content(), historical).Snapshot().Circulation);
        Assert.Equal(CirculationMode.Combined, RunScenario.Create(Content(), current).Snapshot().Circulation!.Configuration.Mode);
        Assert.Equal(Identity(0, commission: true).Commission!.Signature, Identity(1, commission: true).Commission!.Signature);
    }

    [Fact]
    public void FiniteOpeningBuffersPreserveEveryGeographyRoleCashAndCapabilityAcrossBroadSeeds()
    {
        foreach (var seed in Enumerable.Range(0, 256).Select(i => unchecked(i * 104729 * (i % 2 == 0 ? 1 : -1)))
            .Concat(new[] { int.MinValue, int.MaxValue, -734282136 }))
        {
            var old = ExpandingEconomyGeneration.Generate(seed);
            var current = ExpandingEconomyGeneration.Generate(seed, economicRulesVersion: 1);
            Assert.Equal(old.Geography.Signature, current.Geography.Signature);
            Assert.Equal(old.SourceRoles.Signature, current.SourceRoles.Signature);
            Assert.Equal(JsonConvert.SerializeObject(old.Blueprints), JsonConvert.SerializeObject(current.Blueprints));
            Assert.StartsWith(old.Signature + "|economy-v1:", current.Signature);
            foreach (var port in current.Ports)
            {
                var previous = old.Ports.Single(p => p.Id == port.Id);
                Assert.Equal(previous.Market.InitialMarketCash, port.Market.InitialMarketCash);
                Assert.Equal(previous.Settlement!.InitialHouseholdCash, port.Settlement!.InitialHouseholdCash);
                Assert.Equal(JsonConvert.SerializeObject(previous.Facilities), JsonConvert.SerializeObject(port.Facilities));
                Assert.Equal(JsonConvert.SerializeObject(previous.Consumption), JsonConvert.SerializeObject(port.Consumption));
                var need = port.Consumption.Single(n => n.Kind == ConsumptionKind.Essential);
                var fourMeals = checked((int)((long)need.Quantity * port.Settlement.InitialPopulation * 4 / need.PopulationPerUnit));
                Assert.Equal(Math.Max(previous.Market.InitialQuantities["bread"], fourMeals), port.Market.InitialQuantities["bread"]);
                Assert.InRange(port.Market.InitialQuantities["bread"], 10000, 160000);
                foreach (var good in previous.Market.InitialQuantities.Keys.Where(g => g != "bread"))
                {
                    var imports = port.Id == current.FoodSourcePortId && good == "flour" ? 2 :
                        port.Id == current.ProcessingPortId && (good == "logs" || good == "grain") ? 2 :
                        port.Id == current.FurniturePortId && good == "planks" ? 2 :
                        port.Id == current.RecoveryPortId && (good == "flour" || good == "planks") ? 1 : 0;
                    Assert.Equal(previous.Market.InitialQuantities[good] + imports * 10000, port.Market.InitialQuantities[good]);
                }
            }
            foreach (var charter in RunScenario.Charters)
                Assert.Equal(JsonConvert.SerializeObject(ExpandingEconomyScenario.GetCharter(old, charter.Id)),
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
            Assert.False(town.City!.LuxuryEnabled);
            Assert.Equal(1, old.Ports[town.Id].Quotes["furniture"].Pricing!.TargetStock);
            Assert.Equal(20000, town.Quotes["furniture"].Pricing!.TargetStock);
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
