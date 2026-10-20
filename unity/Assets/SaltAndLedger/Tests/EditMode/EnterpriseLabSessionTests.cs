using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class EnterpriseLabSessionTests
    {
        private ArchipelagoLabSession session;

        [SetUp]
        public void SetUp() => session = new ArchipelagoLabSession(ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text));

        [Test]
        public void DefaultWorldUsesExpandedCanonicalScenarioAndSeparateCompanyCash()
        {
            Assert.That(session.Content.Goods.Count, Is.EqualTo(6));
            Assert.That(session.Snapshot.Ships.Count, Is.EqualTo(4));
            Assert.That(session.Player.Cash, Is.EqualTo(EnterpriseScenario.InitialPlayerCash));
            Assert.That(session.Snapshot.Companies[session.Player.OwnerId].Cash, Is.EqualTo(session.Player.Cash));
            Assert.That(session.Snapshot.PrivateFacilities, Is.Empty);
            Assert.That(session.Snapshot.Challenge, Is.Null);
        }

        [Test]
        public void AverageExecutionPriceUsesExactTotalAndRequestedQuantityWithoutChangingTheWorld()
        {
            var before = session.Snapshot;
            var preview = session.PreviewTrade(TradeDirection.Buy, "logs", 6);
            Assert.That(preview.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(preview.TotalAmount, Is.Not.EqualTo(preview.Price.FirstMarginalPrice * 6));
            Assert.That(ArchipelagoLabController.AverageExecutionPrice(preview),
                Is.EqualTo(((decimal)preview.TotalAmount / 6).ToString("0.00", CultureInfo.InvariantCulture)));
            Assert.That(session.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void StaleManualTradeCannotExecuteAfterAnotherSameDayCommit()
        {
            var before = session.Snapshot;
            session.Trade(TradeDirection.Buy, "logs", 1, before);
            var after = session.Snapshot;
            Assert.Throws<InvalidOperationException>(() => session.Trade(TradeDirection.Buy, "logs", 1, before));
            Assert.That(session.Snapshot, Is.SameAs(after));
            Assert.That(session.Snapshot.Day, Is.Zero);
        }

        [Test]
        public void InspectingRemoteTradePausesWithoutArmingOrExecutingAVoyage()
        {
            session.SelectShip(session.Player.Id);
            session.Play();
            var before = session.Snapshot;
            session.InspectTradePort(EnterpriseScenario.East);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.PendingDestinationId, Is.Null);
            Assert.That(session.Player.DockedPortId, Is.EqualTo(EnterpriseScenario.North));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.Throws<InvalidOperationException>(() => session.Trade(TradeDirection.Buy, "bread", 1, before));
        }

        [Test]
        public void PrivateFacilityTransfersAndProductionNeverMergeIntoPublicStock()
        {
            session.Trade(TradeDirection.Buy, "logs", 2);
            session.Construct(EnterpriseScenario.SawmillBlueprint);
            var facility = session.Snapshot.PrivateFacilities.Values.Single();
            var cash = session.Player.Cash;
            var publicLogs = session.Snapshot.Ports[EnterpriseScenario.North].Inventory["logs"];
            session.Transfer(facility.Id, TransferDirection.ShipToWarehouse, "logs", 2);
            Assert.That(session.Player.Cash, Is.EqualTo(cash));
            Assert.That(session.Snapshot.Ports[EnterpriseScenario.North].Inventory["logs"], Is.EqualTo(publicLogs));
            for (var day = 0; day < facility.Blueprint.IntervalDays; day++) session.NextDay();
            facility = session.Snapshot.PrivateFacilities[facility.Id];
            Assert.That(facility.Inventory["planks"], Is.EqualTo(1));
            session.Transfer(facility.Id, TransferDirection.WarehouseToShip, "planks", 1);
            Assert.That(session.Player.Cargo["planks"], Is.EqualTo(1));
            Assert.That(session.ClosedCashProfit, Is.Null, "Capital and unsold assets prevent shipping-only profit claims");
        }

        [Test]
        public void SharedOperatingPresetKeepsProducingWithoutRepeatedHostActions()
        {
            session.SelectPort(EnterpriseScenario.West);
            session.Construct(EnterpriseScenario.BakeryBlueprint);
            var facility = session.Snapshot.PrivateFacilities.Values.Single();
            session.ConfigureFacility(facility.Id, EnterpriseScenario.DefaultIndustryPolicy(EnterpriseScenario.BakeryBlueprint), true);
            for (var day = 0; day < 12; day++) session.NextDay();
            facility = session.Snapshot.PrivateFacilities[facility.Id];
            Assert.That(facility.BatchesProduced, Is.GreaterThan(1));
            Assert.That(facility.ProcurementCost, Is.GreaterThan(0));
            Assert.That(facility.SalesRevenue, Is.GreaterThan(0));
        }

        [Test]
        public void IdleChallengeEndsPlaybackAndResetPreservesChosenMode()
        {
            session.StartScenario(true);
            session.SetPlaybackSpeed(10);
            session.Play();
            for (var frame = 0; frame < 100 && !session.IsChallengeEnded; frame++) session.AdvancePlayback(1000);
            Assert.That(session.Snapshot.Day, Is.EqualTo(EnterpriseScenario.CharterDeadline));
            Assert.That(session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Failed));
            Assert.That(session.IsPlaying, Is.False);
            Assert.Throws<InvalidOperationException>(() => session.Play());
            session.Reset();
            Assert.That(session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Active));
            Assert.That(session.Snapshot.Challenge.DeliveredQuantity, Is.Zero);
            Assert.That(session.Player.Cash, Is.EqualTo(EnterpriseScenario.InitialPlayerCash));
            session.StartScenario(false);
            for (var day = 0; day <= EnterpriseScenario.CharterDeadline; day++) session.NextDay();
            Assert.That(session.Snapshot.Challenge, Is.Null);
            Assert.That(session.Snapshot.Day, Is.GreaterThan(EnterpriseScenario.CharterDeadline));
        }
    }
}
