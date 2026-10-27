using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaltAndLedger.Simulation;
using Xunit;

namespace SaltAndLedger.Simulation.Tests;

public sealed class CirculationSaveTests
{
    private static ContentCatalog Content() => ContentLoader.Load(ContentLoaderTests.CanonicalJson());


    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewIdentitySelectsSchemaFiveAndRetryRetainsExactInitialRevision(bool commission)
    {
        var identity = CirculationGenerationTests.Identity(commission: commission);
        var world = RunScenario.Create(Content(), identity);
        var initial = WorldSaveCodec.Save(world);
        var tree = JObject.Parse(initial);
        Assert.Equal(5, (int)tree["SchemaVersion"]!);
        Assert.Equal(1, (int)tree["Identity"]!["EconomicRulesVersion"]!);
        Assert.NotNull(tree["Circulation"]);
        Assert.Equal(initial, WorldSaveCodec.Save(WorldSaveCodec.Load(Content(), initial)));
        for (var day = 0; day < 9; day++) world.AdvanceDay();
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.Equal(initial, WorldSaveCodec.Save(RunScenario.Create(Content(), restored.Snapshot().RunIdentity!)));
        if (commission)
        {
            restored.AbandonChallenge();
            var terminal = WorldSaveCodec.Save(restored);
            var frozen = WorldSaveCodec.Load(Content(), terminal);
            Assert.Throws<InvalidOperationException>(() => frozen.AdvanceDay());
            Assert.Equal(terminal, WorldSaveCodec.Save(frozen));
        }
    }

    [Fact]
    public void ExactFeePrefixesAndReviewDatesContinueAcrossEveryBoundaryCultureAndConfigurationOrder()
    {
        var identity = CirculationGenerationTests.Identity();
        var world = RunScenario.Create(Content(), identity);
        var reversed = RunScenario.Create(Content(), identity, reverseConfiguration: true);
        // Subunit warehouse proceeds exercise carried service rounding rather than whole-coin coincidence.
        foreach (var target in new[] { world, reversed })
            target.ExecuteWarehouseTrade(WorldIds.PlayerOwnerId, RunScenario.StarterBakery,
                new TradeCommand(TradeDirection.Sell, "flour", 3333));
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var restored = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(world));
            for (var day = 0; day < 20; day++)
            {
                Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(reversed));
                Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
                var events = Events(world.AdvanceDay());
                Assert.Equal(events, Events(reversed.AdvanceDay()));
                Assert.Equal(events, Events(restored.AdvanceDay()));
                restored = WorldSaveCodec.Load(Content(), WorldSaveCodec.Save(restored));
            }
            Assert.True(world.Snapshot().Circulation!.Commercial.Values.Sum(c => c.FeesPaid) > 0);
            Assert.All(restored.Snapshot().Circulation!.Towns.Values, t =>
            { Assert.Equal(20, t.LastReviewDay); Assert.Equal(24, t.NextReviewDay); });
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    [Fact]
    public void ActualNamedPayrollLevyRestoresWithoutRepeatedPaymentOrScheduleReset()
    {
        var identity = new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, -42,
            livingPricingMode: LivingPricingMode.StockOnly, companyStorageRules: CompanyStorageRules.Pooled,
            itineraryVersion: 1, economicRulesVersion: 1);
        var world = RunScenario.Create(Content(), identity);
        // A measured ordinary idle case, without changing public cash, schedules or inventory.
        for (var day = 0; day < 90; day++) world.AdvanceDay();
        Assert.True(world.Snapshot().Circulation!.Towns.Values.Sum(t => t.TotalLevy) > 0);
        var saved = WorldSaveCodec.Save(world);
        var restored = WorldSaveCodec.Load(Content(), saved);
        Assert.Equal(saved, WorldSaveCodec.Save(restored));
        Assert.All(restored.Snapshot().Circulation!.Towns.Values, t => Assert.Equal(92, t.NextReviewDay));
        for (var day = 0; day < 8; day++)
        {
            Assert.Equal(Events(world.AdvanceDay()), Events(restored.AdvanceDay()));
            Assert.Equal(WorldSaveCodec.Save(world), WorldSaveCodec.Save(restored));
        }
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("version_missing")]
    [InlineData("version_unknown")]
    [InlineData("circulation_missing")]
    [InlineData("mode")]
    [InlineData("state_version")]
    [InlineData("missing_town")]
    [InlineData("duplicate_town")]
    [InlineData("unknown_town")]
    [InlineData("missing_commercial")]
    [InlineData("duplicate_commercial")]
    [InlineData("unknown_company")]
    [InlineData("negative_gross")]
    [InlineData("gross_without_revenue")]
    [InlineData("fee_prefix")]
    [InlineData("market_prefix")]
    [InlineData("household_prefix")]
    [InlineData("company_fee")]
    [InlineData("next_review")]
    [InlineData("last_review")]
    [InlineData("review_reason")]
    [InlineData("unreviewed_reserve")]
    [InlineData("too_much_levy")]
    [InlineData("both_transfers")]
    [InlineData("due_above_reserve")]
    [InlineData("payroll_above_employers")]
    [InlineData("coverage_floor")]
    [InlineData("coverage_nonhousehold")]
    public void ResealedImpossibleCirculationStateIsRejectedWithoutChangingActiveRun(string corruption)
    {
        var world = RunScenario.Create(Content(), CirculationGenerationTests.Identity());
        var before = world.Snapshot();
        var saved = WorldSaveCodec.Save(world);
        var data = JObject.Parse(saved);
        var circulation = data["Circulation"]!;
        var rows = (JArray)circulation["Commercial"]!;
        var towns = (JArray)circulation["Towns"]!;
        var row = rows[0]!;
        var town = towns[0]!;
        switch (corruption)
        {
            case "schema": data["SchemaVersion"] = 4; break;
            case "version_missing": ((JObject)data["Identity"]!).Remove("EconomicRulesVersion"); break;
            case "version_unknown": data["Identity"]!["EconomicRulesVersion"] = 2; break;
            case "circulation_missing": ((JObject)data).Remove("Circulation"); break;
            case "mode": circulation["Mode"] = (int)CirculationMode.FeesOnly; break;
            case "state_version": circulation["Version"] = 2; break;
            case "missing_town": towns.RemoveAt(0); break;
            case "duplicate_town": towns[1] = town.DeepClone(); break;
            case "unknown_town": town["PortId"] = "unknown"; break;
            case "missing_commercial": rows.RemoveAt(0); break;
            case "duplicate_commercial": rows[1] = row.DeepClone(); break;
            case "unknown_company": row["OwnerId"] = "unknown"; break;
            case "negative_gross": row["GrossLocalSales"] = -1; break;
            case "gross_without_revenue": row["GrossLocalSales"] = 1; break;
            case "fee_prefix": row["FeesPaid"] = 1; break;
            case "market_prefix": row["MarketFees"] = 1; break;
            case "household_prefix": row["HouseholdFees"] = 1; break;
            case "company_fee": data["Companies"]![0]!["FeesPaid"] = 1; break;
            case "next_review": town["NextReviewDay"] = 8; break;
            case "last_review": town["LastReviewDay"] = 4; break;
            case "review_reason": town["LastReason"] = 99; break;
            case "unreviewed_reserve": town["FoodReserve"] = 1; break;
            case "too_much_levy": town["TotalLevy"] = 120001; break;
            case "both_transfers": town["LastLevy"] = 1; town["LastRelief"] = 1; break;
            case "due_above_reserve": town["DuePayroll"] = 1; break;
            case "payroll_above_employers": town["PayrollReserve"] = long.MaxValue; break;
            case "coverage_floor":
                data["Ports"]![0]!["Quotes"]!.Single(q => (string)q["GoodId"]! == "furniture")["Pricing"]!["TargetStock"] = 1; break;
            case "coverage_nonhousehold":
                data["Ports"]![0]!["Quotes"]!.Single(q => (string)q["GoodId"]! == "logs")["Pricing"]!["TargetStock"] = 1; break;
        }
        Rechecksum(data);
        Assert.Throws<WorldSaveException>(() => WorldSaveCodec.Load(Content(), data.ToString(Formatting.None)));
        Assert.Same(before, world.Snapshot());
        Assert.Equal(saved, WorldSaveCodec.Save(world));
    }

    private static string Events(RegionalResult result) => JsonConvert.SerializeObject(new
    {
        result.EconomicEvents, result.CashTransfers, result.CityChanges, result.Trades,
        result.WarehouseTrades, result.Production, result.Consumption, result.Movements,
        result.PrivateProduction, result.Construction, result.Transfers, result.Development
    });

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static void Rechecksum(JObject data)
    {
        data["StateFingerprint"] = "";
        static JToken Canonical(JToken token) => token switch
        {
            JObject obj => new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, Canonical(p.Value)))),
            JArray array => new JArray(array.Select(Canonical)),
            _ => token.DeepClone()
        };
        data["StateFingerprint"] = Hash(Canonical(data).ToString(Formatting.None));
    }
}
