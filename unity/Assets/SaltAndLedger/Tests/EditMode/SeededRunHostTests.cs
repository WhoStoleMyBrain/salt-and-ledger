using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class SeededRunHostTests
    {
        [TestCase(RunScenario.MerchantCharter)]
        [TestCase(RunScenario.IndustrialCharter)]
        [TestCase(RunScenario.IndependentCharter)]
        public void RetryRestoresFullIdentityWorldAndAllHostState(string charter)
        {
            var content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
            var session = new ArchipelagoLabSession(content);
            var identity = new RunIdentity(RunScenario.CoastalChallenge, charter, RunWorldMode.Seeded,
                int.MinValue, RunDifficulty.SlowFoundations);
            session.StartRun(identity);
            var original = Fingerprint(session.Snapshot);
            // Every charter makes an actual acquisition and orders a delayed site before retry.
            // The Independent Traders' initial mixed cargo intentionally blocks the default route.
            session.Trade(TradeDirection.Buy, "logs", 1);
            session.Construct(EnterpriseScenario.SawmillBlueprint);
            session.SetAutomationEnabled(true);
            session.SetPlaybackSpeed(10);
            session.Play();
            for (var frame = 0; frame < 5; frame++) session.AdvancePlayback(session.PlaybackInterval);
            session.AbandonRun();
            var result = new RunResults(session.Snapshot);
            Assert.That(result.Identity, Is.EqualTo(identity));
            Assert.That(result.ComparisonGroup, Does.Contain("ascension1/seeded-v1-seed-2147483648"));
            Assert.That(session.Journal.Any(), Is.True);
            session.Reset();
            Assert.That(session.Identity, Is.SameAs(identity));
            Assert.That(Fingerprint(session.Snapshot), Is.EqualTo(original));
            Assert.That(session.Journal, Is.Empty);
            Assert.That(session.ShipDeliveries, Is.Empty);
            Assert.That(session.PlayerInputs, Is.Zero);
            Assert.That(session.AutomatedActions, Is.Zero);
            Assert.That(session.PlaybackSpeed, Is.EqualTo(1));
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.Activity.Count, Is.EqualTo(1));
            Assert.That(session.AdvancePlayback(1000), Is.Zero);
            Assert.That(session.PendingDestinationId, Is.Null);
            Assert.That(session.NavigationArmed, Is.False);
            // The next acquisition also verifies reset of the internal FIFO lot identity cursor.
            var fresh = new ArchipelagoLabSession(content);
            fresh.StartRun(identity);
            session.Trade(TradeDirection.Buy, "logs", 1);
            fresh.Trade(TradeDirection.Buy, "logs", 1);
            Assert.That(Fingerprint(session.Snapshot), Is.EqualTo(Fingerprint(fresh.Snapshot)));
            Assert.That(JsonConvert.SerializeObject(session.Journal), Is.EqualTo(JsonConvert.SerializeObject(fresh.Journal)));
        }

        [Test]
        public void ClassicSelectionAndRetryDoNotRetainAnEarlierSeedOrDifficulty()
        {
            var content = ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text);
            var session = new ArchipelagoLabSession(content);
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.Seeded, 42,
                RunDifficulty.SlowFoundations));
            session.StartRun(null, RunScenario.MerchantCharter);
            Assert.That(session.Identity.WorldMode, Is.EqualTo(RunWorldMode.Classic));
            Assert.That(session.Identity.Seed, Is.Null);
            Assert.That(session.Snapshot.Difficulty, Is.EqualTo(RunDifficulty.Normal));
            Assert.That(session.Snapshot.Routes.Select(r => r.Distance),
                Is.EqualTo(ArchipelagoGeography.Classic.Routes.Select(r => r.Distance)));
            session.Reset();
            Assert.That(session.Identity.WorldMode, Is.EqualTo(RunWorldMode.Classic));
        }
        // Project stock-sensitive quotes explicitly: their legacy Bid/Ask accessors require fixed pricing.
        private static string Fingerprint(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
        {
            snapshot.Day, snapshot.Revision, snapshot.Difficulty, snapshot.RunIdentity,
            snapshot.Companies, snapshot.Accounting, snapshot.Challenge, snapshot.Routes,
            snapshot.IndustryBlueprints, snapshot.PrivateFacilities,
            Ports = snapshot.Ports.Values.Select(port => new
            {
                port.Id, port.Name, port.Role, port.X, port.Y, port.Inventory, port.MarketCash,
                Buildings = port.Market.Buildings, port.Facilities, port.Consumption,
                Initial = new
                {
                    port.Configuration.Market.InitialMarketCash,
                    port.Configuration.Market.InitialQuantities,
                    port.Configuration.Market.Buildings,
                    port.Configuration.Facilities,
                    port.Configuration.Consumption
                },
                Prices = port.Quotes.Keys.Select(good => snapshot.GetMarketQuote(port.Id, good)),
                Pricing = port.Quotes.Select(quote => new
                {
                    quote.Key, quote.Value.IsFixed, quote.Value.Pricing,
                    FixedBid = quote.Value.IsFixed ? (long?)quote.Value.Bid : null,
                    FixedAsk = quote.Value.IsFixed ? (long?)quote.Value.Ask : null
                })
            }),
            Ships = snapshot.Ships.Values.Select(ship => new
            {
                ship.Configuration, ship.Ship, ship.OwnerId, ship.Name, ship.IsPlayer, ship.Cash,
                ship.Policy, ship.IndustrialPolicy, ship.IndustrialStage, ship.AutomationEnabled,
                ship.VoyageDurationDays
            })
        });
    }
}
