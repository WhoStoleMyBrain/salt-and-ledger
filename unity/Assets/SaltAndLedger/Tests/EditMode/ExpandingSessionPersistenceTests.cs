using System;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ExpandingSessionPersistenceTests
    {
        private ArchipelagoLabSession Session()
        {
            var session = new ArchipelagoLabSession(ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text));
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            return session;
        }

        [Test]
        public void SaveLoadRestoresExactEconomyAndClearsTransientPlayback()
        {
            var session = Session();
            session.NextDay();
            var saved = session.SaveText();
            Assert.That(session.HasUnsavedChanges, Is.True);
            session.MarkSaved();
            Assert.That(session.HasUnsavedChanges, Is.False);
            session.NextDay();
            session.Play();
            Assert.That(session.HasUnsavedChanges, Is.True);
            session.LoadSaveText(saved);
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.HasUnsavedChanges, Is.False);
            Assert.That(session.Journal, Is.Empty);
            Assert.That(session.PlayerInputs, Is.Zero);
            Assert.That(session.Content.Goods.Count, Is.EqualTo(7));
            var identity = session.Identity;
            session.Reset();
            Assert.That(session.Identity, Is.EqualTo(identity));
            Assert.That(session.Snapshot.Day, Is.Zero);
            Assert.That(session.HasUnsavedChanges, Is.True);
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter));
            Assert.That(session.Content.Goods.Count, Is.EqualTo(6));
            Assert.That(session.Snapshot.Economy, Is.Null);
        }

        [Test]
        public void CorruptLoadPreservesCurrentSnapshotSelectionAndPlayback()
        {
            var session = Session();
            session.SelectShip(EnterpriseScenario.PlayerShipId);
            session.Play();
            var before = session.Snapshot;
            var selection = session.SelectedShipId;
            Assert.Throws<WorldSaveException>(() => session.LoadSaveText("{invalid"));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.SelectedShipId, Is.EqualTo(selection));
            Assert.That(session.IsPlaying, Is.True);
        }

        [Test]
        public void CommissionedShipsHaveIndependentTargetsAndPolicies()
        {
            var session = Session();
            var economy = session.Snapshot.Economy;
            session.SelectPort(economy.Configuration.ShipwrightPortId);
            Assert.That(session.CommissionShip().Development.Single().Outcome, Is.EqualTo(DevelopmentOutcome.Started));
            for (var day = 0; day < economy.Configuration.ShipConstructionDays; day++) session.NextDay();
            Assert.That(session.OwnedShips.Count, Is.EqualTo(2));
            session.SelectShip("player_ship_2");
            var starter = session.Snapshot.Ships[EnterpriseScenario.PlayerShipId];
            var route = new TradeRoutePolicy(economy.Configuration.ShipwrightPortId, economy.Configuration.SilverPortId,
                new RouteLeg("planks", 20000, 10000, 100, 1), allowEmptyReturn: true);
            session.ApplyPolicy(route);
            session.SetAutomationEnabled(true);
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            Assert.That(session.Snapshot.Ships[EnterpriseScenario.PlayerShipId].Policy, Is.SameAs(starter.Policy));
            Assert.That(session.Snapshot.Ships[EnterpriseScenario.PlayerShipId].AutomationEnabled, Is.False);
            session.SelectShip(ArchipelagoScenario.TimberShipId);
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            session.SelectPort(economy.Configuration.ShipwrightPortId);
            session.SetAutomationEnabled(false);
            Assert.That(session.Snapshot.Ships["player_ship_2"].AutomationEnabled, Is.False);
            Assert.That(session.Snapshot.Ships[ArchipelagoScenario.TimberShipId].AutomationEnabled, Is.True);
            var saved = session.SaveText();
            session.LoadSaveText(saved);
            Assert.That(session.OwnedShips.Count, Is.EqualTo(2));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
        }

        [Test]
        public void RepeatedMillConstructionAndFractionalFormattingUseAuthoritativeUnits()
        {
            var session = Session();
            var blueprint = session.Snapshot.IndustryBlueprints.Values.Single(b => b.BuildingId == "mill");
            session.SelectPort(blueprint.EligiblePortIds[0]);
            session.Construct(blueprint.Id);
            session.Construct(blueprint.Id);
            Assert.That(session.Snapshot.PrivateFacilities.Values.Count(f => f.Blueprint.Id == blueprint.Id), Is.EqualTo(2));
            Assert.That(session.Quantity(7500), Is.EqualTo("0.75"));
            Assert.That(session.Coins(15001), Is.EqualTo("1.5001"));
            Assert.That(session.WholeOrderQuantity(2), Is.EqualTo(20000));
            Assert.That(session.RecommendedMerchantPolicy().Outbound.MaxPurchaseQuantity % 10000, Is.Zero);
        }

        [Test]
        public void V3NeverLabelsCashChangeAsClosedShippingProfitAfterTradeOrShipSelection()
        {
            var session = Session();
            session.Trade(TradeDirection.Buy, "bread", 10000);
            session.Trade(TradeDirection.Sell, "bread", 10000);
            Assert.That(session.Player.CargoUnits, Is.Zero);
            Assert.That(session.Snapshot.Accounting.Companies[session.Player.OwnerId].SalesRevenue, Is.GreaterThan(0));
            Assert.That(session.ClosedCashProfit, Is.Null, "V3 uses attributed finance even after a closed cargo cycle.");
            session.Trade(TradeDirection.Buy, "bread", 10000);
            var starterId = session.Player.Id;
            session.SelectPort(session.Snapshot.Economy.Configuration.ShipwrightPortId);
            session.CommissionShip();
            for (var day = 0; day < 4; day++) session.NextDay();
            session.SelectShip("player_ship_2");
            Assert.That(session.Player.CargoUnits, Is.Zero);
            Assert.That(session.Snapshot.Ships[starterId].CargoUnits, Is.EqualTo(10000));
            Assert.That(session.ClosedCashProfit, Is.Null, "An empty selected ship cannot close another ship's held inventory.");
        }
    }
}
