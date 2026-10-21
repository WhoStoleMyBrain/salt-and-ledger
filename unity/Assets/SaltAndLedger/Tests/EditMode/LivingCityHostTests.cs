using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class LivingCityHostTests
    {
        [TestCase(RunScenario.MerchantCharter)]
        [TestCase(RunScenario.IndustrialCharter)]
        [TestCase(RunScenario.IndependentCharter)]
        public void RetryRetainsLivingIdentityAndRestoresHouseholdsNeedsAndHostState(string charter)
        {
            var content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
            var session = new ArchipelagoLabSession(content);
            var identity = new RunIdentity(null, charter, RunWorldMode.LivingCities, -42,
                RunDifficulty.SlowFoundations, livingPricingMode: LivingPricingMode.Coverage);
            session.StartRun(identity);
            var initial = Fingerprint(session.Snapshot);
            var totalCash = TotalCash(session.Snapshot);
            for (var day = 0; day < 40; day++) session.NextDay();
            Assert.That(TotalCash(session.Snapshot), Is.EqualTo(totalCash));
            Assert.That(session.Snapshot.Ports.Values.Sum(p => p.City.TotalWagesReceived), Is.GreaterThan(0));
            Assert.That(session.Snapshot.Ports.Values.Sum(p => p.City.TotalRetailSpend), Is.GreaterThan(0));
            Assert.That(session.Activity.Count, Is.LessThanOrEqualTo(ArchipelagoLabSession.MaximumActivity));
            session.SetPlaybackSpeed(10);
            session.Play();
            session.Reset();
            Assert.That(session.Identity, Is.SameAs(identity));
            Assert.That(Fingerprint(session.Snapshot), Is.EqualTo(initial));
            Assert.That(session.Journal, Is.Empty);
            Assert.That(session.PlayerInputs, Is.Zero);
            Assert.That(session.AutomatedActions, Is.Zero);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.PlaybackSpeed, Is.EqualTo(1));
            Assert.That(session.Activity.Count, Is.EqualTo(1));
        }

        [Test]
        public void RecommendedMerchantRouteUsesGeneratedRolesAndHistoricalRunsRemainExplicit()
        {
            var content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
            var session = new ArchipelagoLabSession(content);
            foreach (var seed in new[] { -42, 0, 42, int.MinValue })
            {
                session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, seed));
                var generation = LivingCityGeneration.Generate(seed);
                Assert.That(session.RecommendedMerchantPolicy().OriginPortId, Is.EqualTo(generation.FoodSourcePortId));
                Assert.That(session.RecommendedMerchantPolicy().DestinationPortId, Is.EqualTo(generation.RecoveryPortId));
                Assert.That(session.Player.DockedPortId, Is.EqualTo(generation.FoodSourcePortId));
            }
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.Seeded, 42));
            Assert.That(session.Snapshot.Ports.Values.All(p => p.City == null), Is.True);
            session.StartRun(null, RunScenario.MerchantCharter);
            Assert.That(session.Identity.WorldMode, Is.EqualTo(RunWorldMode.Classic));
            Assert.That(session.Snapshot.Ports.Values.All(p => p.City == null), Is.True);
        }

        private static long TotalCash(RegionalWorldSnapshot snapshot) => snapshot.Companies.Values.Sum(c => c.Cash) +
            snapshot.Ports.Values.Sum(p => p.MarketCash + (p.City?.HouseholdCash ?? 0));

        private static string Fingerprint(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
        {
            snapshot.Day, snapshot.Revision, snapshot.RunIdentity, snapshot.Companies, snapshot.Accounting,
            snapshot.PrivateFacilities, snapshot.Challenge,
            Ports = snapshot.Ports.Values.Select(p => new
            {
                p.Id, p.City, p.MarketCash, p.Inventory, p.Facilities, p.Consumption,
                Prices = p.Quotes.Keys.Select(g => snapshot.GetMarketQuote(p.Id, g))
            })
        });
    }
}
