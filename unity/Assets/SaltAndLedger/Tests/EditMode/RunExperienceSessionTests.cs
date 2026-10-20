using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class RunExperienceSessionTests
    {
        private ArchipelagoLabSession session;

        [SetUp]
        public void SetUp()
        {
            var canonical = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(canonical, Is.Not.Null);
            session = new ArchipelagoLabSession(ContentLoader.Load(canonical.text));
        }

        [TestCase(RunScenario.MerchantCharter, 600, 10, EnterpriseScenario.North, 0)]
        [TestCase(RunScenario.IndustrialCharter, 300, 8, EnterpriseScenario.West, 1)]
        [TestCase(RunScenario.IndependentCharter, 480, 12, EnterpriseScenario.North, 0)]
        public void ConfiguredRunUsesExactlyTheSelectedCharter(string charterId, long cash, int capacity,
            string portId, int factories)
        {
            session.StartRun(RunScenario.CoastalChallenge, charterId);
            Assert.That(session.HasConfiguredRun, Is.True);
            Assert.That(session.CharterId, Is.EqualTo(charterId));
            Assert.That(session.ChallengeId, Is.EqualTo(RunScenario.CoastalChallenge));
            Assert.That(session.Snapshot.Challenge.Configuration.Id, Is.EqualTo(RunScenario.CoastalChallenge));
            Assert.That(session.Player.Cash, Is.EqualTo(cash));
            Assert.That(session.Player.CargoCapacity, Is.EqualTo(capacity));
            Assert.That(session.Player.DockedPortId, Is.EqualTo(portId));
            Assert.That(session.Snapshot.PrivateFacilities.Count, Is.EqualTo(factories));
            Assert.That(session.Snapshot.Accounting.Companies[session.Player.OwnerId].InventoryPurchases, Is.Zero);
            Assert.That(session.Snapshot.Accounting.Companies[session.Player.OwnerId].ConstructionExpenditure, Is.Zero);
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Snapshot.Revision, Is.Zero);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.PlayerInputs, Is.Zero);
            Assert.That(session.Journal, Is.Empty);
        }

        [Test]
        public void RetryRestoresSelectedAssetsAndClearsHostHistoryAndClockBacklog()
        {
            session.StartRun(RunScenario.CoastalChallenge, RunScenario.IndustrialCharter);
            var initial = Fingerprint(session.Snapshot);
            session.ConfigureFacility(RunScenario.StarterBakery, EnterpriseScenario.BakeryPolicy(), true);
            session.ApplyIndustrialPolicy(RunScenario.BakeryShippingPolicy(), true);
            session.SetPlaybackSpeed(10);
            session.Play();
            for (var frame = 0; frame < 5; frame++) session.AdvancePlayback(session.PlaybackInterval);
            Assert.That(session.Journal, Is.Not.Empty);
            Assert.That(session.PlayerInputs, Is.GreaterThan(0));
            session.Reset();
            Assert.That(Fingerprint(session.Snapshot), Is.EqualTo(initial));
            Assert.That(session.CharterId, Is.EqualTo(RunScenario.IndustrialCharter));
            Assert.That(session.ChallengeId, Is.EqualTo(RunScenario.CoastalChallenge));
            Assert.That(session.PlayerInputs, Is.Zero);
            Assert.That(session.AutomatedActions, Is.Zero);
            Assert.That(session.Journal, Is.Empty);
            Assert.That(session.ShipDeliveries, Is.Empty);
            Assert.That(session.PlaybackSpeed, Is.EqualTo(1));
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.AdvancePlayback(1000), Is.Zero);
            Assert.That(session.Activity.Count, Is.EqualTo(1));
        }

        [Test]
        public void InvalidRunSelectionPreservesCurrentConfiguredSession()
        {
            session.StartRun(RunScenario.ReliefChallenge, RunScenario.IndependentCharter);
            var before = session.Snapshot;
            Assert.Throws<ArgumentException>(() => session.StartRun(RunScenario.CoastalChallenge, "missing_charter"));
            Assert.Throws<ArgumentException>(() => session.StartRun("missing_challenge", RunScenario.MerchantCharter));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.CharterId, Is.EqualTo(RunScenario.IndependentCharter));
            Assert.That(session.ChallengeId, Is.EqualTo(RunScenario.ReliefChallenge));
        }

        [Test]
        public void AbandonmentPausesPreservesAccountsAndFreezesFurtherEconomicWork()
        {
            session.StartRun(RunScenario.CoastalChallenge, RunScenario.MerchantCharter);
            session.Trade(TradeDirection.Buy, "logs", 2);
            session.Play();
            var before = session.Snapshot;
            var journalCount = session.Journal.Count;
            var abandoned = session.AbandonRun();
            Assert.That(abandoned.After.Challenge.Status, Is.EqualTo(ChallengeStatus.Abandoned));
            Assert.That(abandoned.After.Challenge.CompletedDay, Is.EqualTo(before.Day));
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.IsChallengeEnded, Is.True);
            Assert.That(session.Player.Cash, Is.EqualTo(before.Ships[session.Player.Id].Cash));
            Assert.That(session.Player.Cargo["logs"], Is.EqualTo(2));
            Assert.That(session.Journal.Count, Is.EqualTo(journalCount));
            Assert.That(session.AdvancePlayback(1000), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => session.Play());
            Assert.Throws<InvalidOperationException>(() => session.NextDay());
            Assert.Throws<InvalidOperationException>(() => session.Trade(TradeDirection.Sell, "logs", 1));
            Assert.That(session.Snapshot, Is.SameAs(abandoned.After));
            var result = new RunResults(session.Snapshot, session.CharterId);
            Assert.That(result.VictoryScore, Is.Zero);
            Assert.That(result.TimeScore, Is.Zero);
        }

        [Test]
        public void IdleDeadlineStopsPlaybackAndSandboxHasNoTerminalDeadline()
        {
            session.StartRun(RunScenario.CoastalChallenge, RunScenario.MerchantCharter);
            for (var day = 0; day < 59; day++) session.NextDay();
            session.Play();
            Assert.That(session.AdvancePlayback(1000), Is.EqualTo(1));
            Assert.That(session.Snapshot.Day, Is.EqualTo(60));
            Assert.That(session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Failed));
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.AdvancePlayback(1000), Is.Zero);
            session.StartRun(null, RunScenario.IndependentCharter);
            for (var day = 0; day < 65; day++) session.NextDay();
            Assert.That(session.Snapshot.Challenge, Is.Null);
            Assert.That(session.ChallengeMode, Is.False);
            Assert.That(session.IsChallengeEnded, Is.False);
            Assert.That(session.Snapshot.Day, Is.EqualTo(65));
            Assert.That(session.CharterId, Is.EqualTo(RunScenario.IndependentCharter));
        }

        [Test]
        public void JournalIsBoundedNewestFirstWithoutLosingAuthoritativeFinancialTotals()
        {
            session.StartRun(null, RunScenario.MerchantCharter);
            var initialCash = session.Player.Cash;
            long purchases = 0, revenue = 0;
            for (var transaction = 0; transaction < 105; transaction++)
            {
                purchases += session.Trade(TradeDirection.Buy, "logs", 1).Trades.Single().Trade.ExecutedAmount;
                revenue += session.Trade(TradeDirection.Sell, "logs", 1).Trades.Single().Trade.ExecutedAmount;
            }
            Assert.That(session.Journal.Count, Is.EqualTo(ArchipelagoLabSession.MaximumJournalEntries));
            Assert.That(session.Journal.First().Revision, Is.EqualTo(210));
            Assert.That(session.Journal.Last().Revision, Is.EqualTo(11));
            Assert.That(session.Journal.All(e => e.CompanyId == session.Player.OwnerId), Is.True);
            Assert.That(session.Journal.Select(e => e.Revision), Is.Ordered.Descending);
            var finance = session.Snapshot.Accounting.Companies[session.Player.OwnerId];
            Assert.That(finance.InventoryPurchases, Is.EqualTo(purchases));
            Assert.That(finance.SalesRevenue, Is.EqualTo(revenue));
            Assert.That(finance.CostOfGoodsSold, Is.EqualTo(purchases));
            Assert.That(finance.HeldInventoryCost, Is.Zero);
            Assert.That(session.Player.Cash, Is.EqualTo(initialCash - purchases + revenue));
            Assert.That(session.ShipDeliveries["logs"], Is.EqualTo(105), "This host counter is gross sales, not challenge net supply.");
            Assert.That(session.PlayerInputs, Is.EqualTo(210));
            Assert.That(session.AutomatedActions, Is.Zero);
            Assert.That(session.Snapshot.Day, Is.Zero);
        }

        [Test]
        public void InputCountersExcludePassiveInspectionRejectionsWaitsAndNpcWork()
        {
            session.StartRun(null, RunScenario.MerchantCharter);
            session.SelectPort(EnterpriseScenario.North);
            session.SelectShip(session.Player.Id);
            session.Play();
            session.Pause();
            session.Trade(TradeDirection.Buy, "logs", 100);
            Assert.That(session.PlayerInputs, Is.Zero);
            Assert.That(session.AutomatedActions, Is.Zero);
            session.SetAutomationEnabled(true);
            Assert.That(session.PlayerInputs, Is.EqualTo(1));
            session.SetAutomationEnabled(true);
            Assert.That(session.PlayerInputs, Is.EqualTo(1), "Unchanged enablement is not another committed mutation.");
            session.RunDockside();
            Assert.That(session.AutomatedActions, Is.EqualTo(1), "Buying and departing is one selected automatic action.");
            Assert.That(session.PlayerInputs, Is.EqualTo(1));
            session.RunDockside();
            session.NextDay();
            Assert.That(session.AutomatedActions, Is.EqualTo(1), "In-transit evaluation and NPC transactions are excluded.");
            Assert.That(session.PlayerInputs, Is.EqualTo(1));
        }

        [Test]
        public void IndustrialCharterCompletesThroughConfiguredPhysicalShippingAndStopsPlayback()
        {
            session.StartRun(RunScenario.CoastalChallenge, RunScenario.IndustrialCharter);
            session.ConfigureFacility(RunScenario.StarterBakery, EnterpriseScenario.BakeryPolicy(), true);
            session.ApplyIndustrialPolicy(RunScenario.BakeryShippingPolicy(), true);
            session.Play();
            for (var frame = 0; frame < 60 && !session.IsChallengeEnded; frame++)
                session.AdvancePlayback(session.PlaybackInterval);
            Assert.That(session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Succeeded));
            Assert.That(session.Snapshot.Day, Is.EqualTo(39));
            Assert.That(session.Player.Cash, Is.EqualTo(1027));
            Assert.That(session.Snapshot.Challenge.NetDeliveredQuantity, Is.EqualTo(24));
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.AutomatedActions, Is.GreaterThan(3));
            Assert.That(session.Journal.Any(e => e.Kind == EconomicEventKind.Transfer), Is.True);
            Assert.That(session.Journal.Any(e => e.Kind == EconomicEventKind.Production), Is.True);
            Assert.That(session.ShipDeliveries["bread"], Is.EqualTo(24));
            var before = session.Snapshot;
            Assert.That(session.AdvancePlayback(1000), Is.Zero);
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        private static string Fingerprint(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
        {
            snapshot.Day, snapshot.Revision, snapshot.Companies, snapshot.Accounting,
            snapshot.Challenge, snapshot.Routes, snapshot.IndustryBlueprints, snapshot.PrivateFacilities,
            Ports = snapshot.Ports.Values.Select(port => new
            {
                port.Id, port.Name, port.Role, port.X, port.Y, port.Inventory, port.MarketCash,
                port.Facilities, port.Consumption,
                Prices = port.Quotes.Keys.Select(good => snapshot.GetMarketQuote(port.Id, good)),
                Pricing = port.Quotes.Select(quote => new { quote.Key, quote.Value.Pricing })
            }),
            Ships = snapshot.Ships.Values.Select(ship => new
            {
                ship.Configuration, ship.Ship, ship.Cash, ship.Policy, ship.IndustrialPolicy,
                ship.IndustrialStage, ship.AutomationEnabled, ship.VoyageDurationDays
            })
        });
    }
}
