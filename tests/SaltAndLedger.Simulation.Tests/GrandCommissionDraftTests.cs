using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class GrandCommissionDraftTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());
    private static string Vector(GrandCommissionDraft draft) => string.Join("|", draft.Rounds.Select(r => string.Join(",", r.Offers.Select(o => o.TemplateId))));

    [Theory]
    [InlineData(42, "second_command,all_harbors,regional_survey|artisan_guild,timber_chain,breadworks|royal_exporter,flour_for_bakers,bread_for_coast|royal_institution,prosperous_enterprise,public_works")]
    [InlineData(-42, "open_routes,all_harbors,regional_survey|new_industry,timber_chain,breadworks|planks_for_forest,flour_for_bakers,bread_for_coast|public_works,prosperous_enterprise,city_back_in_trade")]
    [InlineData(99, "second_command,open_routes,all_harbors|artisan_guild,breadworks,new_industry|planks_for_forest,royal_exporter,flour_for_bakers|royal_institution,prosperous_enterprise,city_back_in_trade")]
    [InlineData(int.MinValue, "regional_survey,open_routes,second_command|new_industry,breadworks,artisan_guild|planks_for_forest,royal_exporter,bread_for_coast|royal_institution,prosperous_enterprise,city_back_in_trade")]
    [InlineData(int.MaxValue, "regional_survey,all_harbors,second_command|breadworks,timber_chain,new_industry|royal_exporter,flour_for_bakers,bread_for_coast|public_works,city_back_in_trade,prosperous_enterprise")]
    public void GoldenOffersHaveVersionedIndependentCategoryStreams(int seed, string expected)
    {
        Assert.Equal(expected, Vector(GrandCommissionDraft.Generate(seed, 1)));
        var before = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "tr-TR", "de-DE", "ar-SA" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.Equal(expected, Vector(GrandCommissionDraft.Generate(seed)));
            }
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    [Fact]
    public void FourDistinctPoolsOfferThreeKnownTemplatesAndOnlyAcceptOneFromEachRound()
    {
        var draft = GrandCommissionDraft.Generate(42);
        Assert.Equal(4, draft.Rounds.Count);
        Assert.All(draft.Rounds, round =>
        {
            Assert.Equal(4, round.Catalog.Count);
            Assert.Equal(3, round.Offers.Select(o => o.TemplateId).Distinct().Count());
            Assert.All(round.Offers, goal => { Assert.Equal(round.Category, goal.Category); Assert.Contains(goal, round.Catalog); Assert.NotEmpty(goal.Conditions); });
        });
        var ids = draft.Rounds.Select(r => r.Offers[0].TemplateId).ToArray();
        var plan = draft.Resolve(ids);
        Assert.Equal(ids, plan.Goals.Select(g => g.TemplateId));
        Assert.Equal(plan.Signature, GrandCommissionPlan.Restore(42, 1, ids, plan.Signature).Signature);
        Assert.Throws<ArgumentOutOfRangeException>(() => GrandCommissionDraft.Generate(42, 2));
        Assert.Throws<ArgumentException>(() => draft.Resolve(ids.Take(3)));
        Assert.Throws<ArgumentException>(() => draft.Resolve(ids.Concat(new[] { ids[0] })));
        Assert.Throws<ArgumentException>(() => draft.Resolve(Enumerable.Repeat(ids[0], 4)));
        Assert.Throws<ArgumentException>(() => draft.Resolve(new[] { "unknown", ids[1], ids[2], ids[3] }));
        Assert.Throws<ArgumentException>(() => draft.Resolve(new[] { draft.Rounds[0].Catalog.Single(c => !draft.Rounds[0].Offers.Contains(c)).TemplateId, ids[1], ids[2], ids[3] }));
        Assert.Throws<ArgumentException>(() => GrandCommissionPlan.Restore(42, 1, ids, "forged"));
        Assert.Throws<ArgumentException>(() => GrandCommissionPlan.Restore(99, 1, ids, plan.Signature));
    }

    [Fact]
    public void ChoicesChangeOnlyMissionIdentityNotWorldAndSurviveDifferentChartersDifficultyAndPricing()
    {
        var draft = GrandCommissionDraft.Generate(42);
        var a = draft.Resolve(draft.Rounds.Select(r => r.Offers[0].TemplateId));
        var b = draft.Resolve(draft.Rounds.Select(r => r.Offers[1].TemplateId));
        var first = new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42, commission: a);
        var second = new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42, commission: b);
        Assert.NotEqual(first.ComparisonGroup, second.ComparisonGroup);
        var one = RunScenario.Create(Content(), first).Snapshot();
        var two = RunScenario.Create(Content(), second).Snapshot();
        Assert.Equal(0, one.Day); Assert.Equal(0, one.Revision);
        string Ports(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(snapshot.Ports.Values.Select(p => new
        {
            p.Id, p.X, p.Y, p.Role, p.Inventory, p.MarketCash, p.City, p.Facilities, p.Consumption,
            Quotes = p.Quotes.Values.Select(q => new { q.GoodId, q.Pricing, Current = snapshot.GetMarketQuote(p.Id, q.GoodId) })
        }));
        Assert.Equal(Ports(one), Ports(two));
        Assert.Equal(JsonConvert.SerializeObject(one.Ships), JsonConvert.SerializeObject(two.Ships));
        Assert.Equal(JsonConvert.SerializeObject(one.Companies), JsonConvert.SerializeObject(two.Companies));
        Assert.Equal(JsonConvert.SerializeObject(one.Economy), JsonConvert.SerializeObject(two.Economy));
        foreach (var charter in RunScenario.Charters)
            foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
                foreach (var pricing in new[] { LivingPricingMode.StockOnly, LivingPricingMode.Coverage })
                {
                    var identity = new RunIdentity(GrandCommission.ChallengeId, charter.Id, RunWorldMode.ExpandingEconomy,
                        42, difficulty, livingPricingMode: pricing, commission: a);
                    Assert.Equal(a.Signature, identity.Commission!.Signature);
                    Assert.Equal(Vector(draft), Vector(GrandCommissionDraft.Generate(identity.Seed!.Value)));
                }
        Assert.Throws<ArgumentException>(() => new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter,
            RunWorldMode.ExpandingEconomy, 99, commission: a));
    }

    [Fact]
    public void BoundedSeedSampleKeepsAllResolvedTargetsAndRequiredFacilitiesStructurallyLegal()
    {
        var slates = new System.Collections.Generic.HashSet<string>();
        foreach (var seed in Enumerable.Range(-32, 65).Select(i => unchecked(i * 104729)).Concat(new[] { int.MinValue, int.MaxValue }))
        {
            var draft = GrandCommissionDraft.Generate(seed); var generation = ExpandingEconomyGeneration.Generate(seed);
            slates.Add(Vector(draft));
            Assert.Equal(6, generation.Geography.Routes.Count);
            foreach (var condition in draft.Rounds.SelectMany(r => r.Catalog).SelectMany(g => g.Conditions))
            {
                if (condition.PortId != null) Assert.Contains(generation.Ports, p => p.Id == condition.PortId);
                Assert.All(condition.PortIds, id => Assert.Contains(generation.Ports, p => p.Id == id));
                if (condition.RecipeId != null) Assert.Contains(generation.Blueprints, b => b.RecipeId == condition.RecipeId && b.EligiblePortIds.Count > 0);
                Assert.True(condition.Target > 0);
                if (condition.Role == "recovery") Assert.Equal(generation.RecoveryPortId, condition.PortId);
                if (condition.Role == "furniture") Assert.Equal(generation.FurniturePortId, condition.PortId);
                if (condition.Role == "food_source") Assert.Equal(generation.FoodSourcePortId, condition.PortId);
            }
            var mills = generation.Blueprints.Single(b => b.RecipeId == "mill_flour").EligiblePortIds;
            var bakeries = generation.Blueprints.Single(b => b.RecipeId == "bake_bread").EligiblePortIds;
            Assert.Contains(mills, mill => bakeries.Any(bakery => bakery != mill));
            Assert.True(bakeries.Count >= 2);
        }
        Assert.True(slates.Count > 4);
    }
}
