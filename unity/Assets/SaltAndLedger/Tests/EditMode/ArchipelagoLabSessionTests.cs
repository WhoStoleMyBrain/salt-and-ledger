using System;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ArchipelagoLabSessionTests
    {
        private ArchipelagoLabSession session;

        [SetUp]
        public void SetUp()
        {
            var content = Resources.Load<TextAsset>("CanonicalContent");
            Assert.That(content, Is.Not.Null);
            session = new ArchipelagoLabSession(ContentLoader.Load(content.text));
        }

        [Test]
        public void SelectionPlaybackConfigurationAndPauseAreEconomicallyPassive()
        {
            var before = session.Snapshot;
            session.SelectPort(ArchipelagoScenario.East);
            session.SelectShip(ArchipelagoScenario.TimberShipId);
            session.SetPlaybackSpeed(10);
            session.Play();
            session.Pause();
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.Snapshot.Ports.Count, Is.EqualTo(4));
            Assert.That(session.Snapshot.Ships.Count, Is.EqualTo(3));
            Assert.That(session.IsPlaying, Is.False);
        }

        [Test]
        public void InstantTradingUpdatesStateAndPricesWithoutProductionOrDays()
        {
            var before = session.Snapshot;
            var quote = before.GetMarketQuote(ArchipelagoScenario.North, "logs");
            session.Play();
            session.Trade(TradeDirection.Buy, "logs", 2);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(session.Player.Cargo["logs"], Is.EqualTo(2));
            Assert.That(session.Snapshot.GetMarketQuote(ArchipelagoScenario.North, "logs").Ask, Is.GreaterThan(quote.Ask));
            session.Trade(TradeDirection.Sell, "logs", 1);
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Snapshot.Revision, Is.EqualTo(2));
            Assert.That(session.Player.Cargo["logs"], Is.EqualTo(1));
            Assert.That(session.Snapshot.Ports[ArchipelagoScenario.North].Facilities[0].NextDueDay,
                Is.EqualTo(before.Ports[ArchipelagoScenario.North].Facilities[0].NextDueDay));
            Assert.That(session.ClosedCashProfit, Is.Null);
        }

        [Test]
        public void RejectedAndInvalidTradesPreserveTheSnapshot()
        {
            var before = session.Snapshot;
            var rejected = session.Trade(TradeDirection.Buy, "logs", 100);
            Assert.That(rejected.Trades[0].Trade.Outcome, Is.Not.EqualTo(TradeOutcome.Traded));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Trade(TradeDirection.Buy, "logs", 0));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.SelectPort(ArchipelagoScenario.East);
            Assert.Throws<InvalidOperationException>(() => session.Trade(TradeDirection.Buy, "planks", 1));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void VoyageNeedsPlayerSelectionAndConfirmation()
        {
            var before = session.Snapshot;
            session.SelectShip(ArchipelagoScenario.TimberShipId);
            session.SelectPort(ArchipelagoScenario.South);
            Assert.That(session.PendingDestinationId, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Sail());
            session.SelectShip(ArchipelagoScenario.PlayerShipId);
            session.SelectPort(ArchipelagoScenario.South);
            Assert.That(session.PendingDestinationId, Is.EqualTo(ArchipelagoScenario.South));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.Sail();
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Player.DockedPortId, Is.Null);
        }

        [Test]
        public void ConfiguredVoyageArrivesAfterWholeDaysAndAllowsImmediateTrade()
        {
            session.Trade(TradeDirection.Buy, "logs", 2);
            var days = session.Snapshot.TravelDays(session.Player.Id, ArchipelagoScenario.South);
            session.SelectShip(session.Player.Id);
            session.SelectPort(ArchipelagoScenario.South);
            session.Sail();
            Assert.That(session.Player.RemainingTravelDays, Is.EqualTo(days));
            for (var day = 1; day <= days; day++)
            {
                session.NextDay();
                Assert.That(session.Player.RemainingTravelDays, Is.EqualTo(days - day));
            }
            Assert.That(session.Player.DockedPortId, Is.EqualTo(ArchipelagoScenario.South));
            session.Trade(TradeDirection.Sell, "logs", 2);
            Assert.That(session.Snapshot.Day, Is.EqualTo(days));
            Assert.That(session.Player.CargoUnits, Is.Zero);
            Assert.That(session.ClosedCashProfit, Is.Not.Null);
        }

        [Test]
        public void AutomatedTravelClearsAnEarlierUnconfirmedManualDestination()
        {
            session.SelectShip(session.Player.Id);
            session.SelectPort(ArchipelagoScenario.South);
            Assert.That(session.PendingDestinationId, Is.EqualTo(ArchipelagoScenario.South));
            session.SetAutomationEnabled(true);
            session.RunDockside();
            Assert.That(session.PendingDestinationId, Is.Null);
            var duration = session.Player.RemainingTravelDays;
            for (var day = 0; day < duration; day++) session.NextDay();
            session.SetAutomationEnabled(false);
            Assert.That(session.Player.DockedPortId, Is.EqualTo(ArchipelagoScenario.South));
            Assert.That(session.PendingDestinationId, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Sail());
        }

        [Test]
        public void PlaybackSpeedChangesOnlyHowFrequentlyWholeDaysAreRequested()
        {
            var fast = new ArchipelagoLabSession(session.Content);
            fast.SetPlaybackSpeed(10);
            session.Play();
            fast.Play();
            for (var day = 0; day < 12; day++)
            {
                Assert.That(session.AdvancePlayback(session.PlaybackInterval), Is.EqualTo(1));
                Assert.That(fast.AdvancePlayback(fast.PlaybackInterval), Is.EqualTo(1));
            }
            Assert.That(Fingerprint(fast.Snapshot), Is.EqualTo(Fingerprint(session.Snapshot)));
        }

        [Test]
        public void PauseDropsElapsedBacklogAndCatchUpIsBounded()
        {
            session.Play();
            session.AdvancePlayback(session.PlaybackInterval / 2);
            session.Pause();
            Assert.That(session.AdvancePlayback(100), Is.Zero);
            session.Play();
            Assert.That(session.AdvancePlayback(session.PlaybackInterval / 2), Is.Zero);
            Assert.That(session.AdvancePlayback(1000), Is.EqualTo(ArchipelagoLabSession.MaximumDaysPerFrame));
            Assert.That(session.Snapshot.Day, Is.EqualTo(ArchipelagoLabSession.MaximumDaysPerFrame));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvancePlayback(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SetPlaybackSpeed(7));
        }

        [Test]
        public void AutomationConfigurationAndDocksideChainSpendNoDay()
        {
            var before = session.Snapshot;
            session.ApplyPolicy(ArchipelagoScenario.PlayerPolicy(true));
            session.SetAutomationEnabled(true);
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Player.Cash, Is.EqualTo(before.Ships[session.Player.Id].Cash));
            Assert.That(session.Player.CargoUnits, Is.Zero);
            Assert.That(before.Ships[session.Player.Id].AutomationEnabled, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.Trade(TradeDirection.Buy, "logs", 1));
            session.ApplyPolicy(ArchipelagoScenario.PlayerPolicy());
            var result = session.RunDockside();
            Assert.That(result.After.Day, Is.Zero);
            Assert.That(result.Decisions.Count, Is.LessThanOrEqualTo(4));
            Assert.That(session.Player.CargoUnits, Is.GreaterThan(0));
            Assert.That(session.Player.DestinationPortId, Is.EqualTo(ArchipelagoScenario.South));
        }

        [Test]
        public void ScheduledEconomyAndNpcMovementAreObservedFromStructuredResults()
        {
            var first = session.NextDay();
            Assert.That(first.Movements.Any(movement => movement.ShipId == ArchipelagoScenario.TimberShipId &&
                movement.Kind == ShipMovementKind.Departure), Is.True);
            Assert.That(session.Snapshot.DockedShips(ArchipelagoScenario.North).Count, Is.EqualTo(1));
            var results = Enumerable.Range(0, 5).Select(_ => session.NextDay()).ToArray();
            Assert.That(results.Any(result => result.Production.Any(item => item.Outcome == ScheduledProductionOutcome.Produced)), Is.True);
            Assert.That(results.Any(result => result.Consumption.Count > 0), Is.True);
            Assert.That(session.Activity.Any(item => item.Message.Contains("consumed") || item.Message.Contains("unmet demand")), Is.True);
        }

        [Test]
        public void RecentActivityIsBoundedAndResetRestoresTheSharedScenario()
        {
            for (var day = 0; day < 120; day++) session.NextDay();
            Assert.That(session.Activity.Count, Is.LessThanOrEqualTo(ArchipelagoLabSession.MaximumActivity));
            session.Play();
            session.Reset();
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Snapshot.Revision, Is.Zero);
            Assert.That(session.Player.Cash, Is.EqualTo(session.Player.Configuration.InitialCash));
            Assert.That(session.Player.CargoUnits, Is.Zero);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.Player.AutomationEnabled, Is.False);
            Assert.That(session.Activity.Count, Is.EqualTo(1));
        }

        private static string Fingerprint(RegionalWorldSnapshot snapshot) => snapshot.Day + "|" +
            string.Join("|", snapshot.Ports.Values.Select(port => port.Id + ":" + port.MarketCash + ":" +
                string.Join(",", port.Inventory.Select(pair => pair.Key + "=" + pair.Value)))) + "|" +
            string.Join("|", snapshot.Ships.Values.Select(ship => ship.Id + ":" + ship.Cash + ":" +
                ship.DockedPortId + ":" + ship.DestinationPortId + ":" + ship.RemainingTravelDays + ":" +
                string.Join(",", ship.Cargo.Select(pair => pair.Key + "=" + pair.Value))));
    }
}
