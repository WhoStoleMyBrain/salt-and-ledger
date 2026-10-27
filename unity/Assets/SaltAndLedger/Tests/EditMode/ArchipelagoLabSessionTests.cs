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
            session.SelectPort(WorldIds.East);
            session.SelectShip(WorldIds.TimberShipId);
            session.SetPlaybackSpeed(10);
            session.Play();
            session.Pause();
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.Snapshot.Ports.Count, Is.EqualTo(4));
            Assert.That(session.Snapshot.Ships.Count, Is.EqualTo(4));
            Assert.That(session.IsPlaying, Is.False);
        }

        [Test]
        public void InstantTradingUpdatesStateAndPricesWithoutProductionOrDays()
        {
            var before = session.Snapshot;
            var port = session.Player.DockedPortId;
            var quote = before.GetMarketQuote(port, "logs");
            session.Play();
            session.Trade(TradeDirection.Buy, "logs", 20000);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(session.Player.Cargo["logs"], Is.EqualTo(20000));
            Assert.That(session.Snapshot.GetMarketQuote(port, "logs").Ask, Is.GreaterThanOrEqualTo(quote.Ask));
            session.Trade(TradeDirection.Sell, "logs", 10000);
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Snapshot.Revision, Is.EqualTo(2));
            Assert.That(session.Player.Cargo["logs"], Is.EqualTo(10000));
            Assert.That(session.Snapshot.Ports[WorldIds.North].Facilities[0].NextDueDay,
                Is.EqualTo(before.Ports[WorldIds.North].Facilities[0].NextDueDay));
        }

        [Test]
        public void RejectedAndInvalidTradesPreserveTheSnapshot()
        {
            var before = session.Snapshot;
            var rejected = session.Trade(TradeDirection.Buy, "logs", 1000000);
            Assert.That(rejected.Trades[0].Trade.Outcome, Is.Not.EqualTo(TradeOutcome.Traded));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Trade(TradeDirection.Buy, "logs", 0));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.SelectPort(session.Snapshot.Ports.Keys.First(id => id != session.Player.DockedPortId));
            Assert.Throws<InvalidOperationException>(() => session.Trade(TradeDirection.Buy, "planks", 10000));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void VoyageNeedsPlayerSelectionAndConfirmation()
        {
            var before = session.Snapshot;
            session.SelectShip(WorldIds.TimberShipId);
            session.SelectPort(WorldIds.South);
            Assert.That(session.PendingDestinationId, Is.Null);
            Assert.Throws<InvalidOperationException>(() => session.Sail());
            session.SelectShip(WorldIds.PlayerShipId);
            session.SelectPort(WorldIds.South);
            Assert.That(session.PendingDestinationId, Is.EqualTo(WorldIds.South));
            Assert.That(session.Snapshot, Is.SameAs(before));
            session.Sail();
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.Player.DockedPortId, Is.Null);
        }

        [Test]
        public void ConfiguredVoyageArrivesAfterWholeDaysAndAllowsImmediateTrade()
        {
            session.Trade(TradeDirection.Buy, "logs", 20000);
            var days = session.Snapshot.TravelDays(session.Player.Id, WorldIds.South);
            session.SelectShip(session.Player.Id);
            session.SelectPort(WorldIds.South);
            session.Sail();
            Assert.That(session.Player.RemainingTravelDays, Is.EqualTo(days));
            for (var day = 1; day <= days; day++)
            {
                session.NextDay();
                Assert.That(session.Player.RemainingTravelDays, Is.EqualTo(days - day));
            }
            Assert.That(session.Player.DockedPortId, Is.EqualTo(WorldIds.South));
            session.Trade(TradeDirection.Sell, "logs", 20000);
            Assert.That(session.Snapshot.Day, Is.EqualTo(days));
            Assert.That(session.Player.CargoUnits, Is.Zero);
        }

        [Test]
        public void AutomatedTravelClearsAnEarlierUnconfirmedManualDestination()
        {
            session.ApplyItinerary(new CargoItineraryPolicy("travel", new[] {
                new CargoItineraryStop(session.Player.DockedPortId, new[] {
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "bread", 10000) }),
                new CargoItineraryStop(WorldIds.South, new[] {
                    new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "bread", 10000) }) }));
            session.SelectShip(session.Player.Id);
            session.SelectPort(WorldIds.South);
            Assert.That(session.PendingDestinationId, Is.EqualTo(WorldIds.South));
            session.SetAutomationEnabled(true);
            session.RunDockside();
            Assert.That(session.PendingDestinationId, Is.Null);
            var duration = session.Player.RemainingTravelDays;
            for (var day = 0; day < duration; day++) session.NextDay();
            session.SetAutomationEnabled(false);
            Assert.That(session.Player.DockedPortId, Is.EqualTo(WorldIds.South));
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
        public void ScheduledEconomyAndNpcMovementAreObservedFromStructuredResults()
        {
            var first = session.NextDay();
            Assert.That(first.Movements.Any(movement => movement.ShipId == WorldIds.TimberShipId &&
                movement.Kind == ShipMovementKind.Departure), Is.True);
            Assert.That(session.Snapshot.DockedShips(session.Player.DockedPortId).Any(s => s.Id == session.Player.Id), Is.True);
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
            Assert.That(session.CashChange, Is.Zero);
            Assert.That(session.Player.Cash, Is.EqualTo(session.Snapshot.Accounting.Companies[session.Player.OwnerId].InitialCash));
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
