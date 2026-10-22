using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ExpandingMapTests
    {
        private static ArchipelagoLabSession CreateSession()
        {
            var session = new ArchipelagoLabSession(ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text));
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            return session;
        }

        private static void CompleteShip(ArchipelagoLabSession session)
        {
            session.SelectPort(session.Snapshot.Economy.Configuration.ShipwrightPortId);
            Assert.That(session.CommissionShip().Development.Single().Outcome, Is.EqualTo(DevelopmentOutcome.Started));
            for (var day = 0; day < 4; day++) session.NextDay();
        }

        [Test]
        public void CommissionCompletionAddsSelectableScaledMarkerAndRetryRemovesIt()
        {
            var session = CreateSession();
            var map = new ArchipelagoMap();
            map.Present(session.Snapshot, session.SelectedPortId, null, false, 1);
            Assert.That(map.ShipMarkerCount, Is.EqualTo(4));
            CompleteShip(session);
            var snapshot = session.Snapshot;
            map.Present(snapshot, session.SelectedPortId, "player_ship_2", true, 1);
            Assert.That(map.ShipMarkerCount, Is.EqualTo(5));
            var marker = map.Q<Button>("ship_player_ship_2");
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.ClassListContains("selected-marker"), Is.True);
            Assert.That(marker.tooltip, Does.Contain("0/10 cargo"));
            Assert.That(float.IsNaN(map.DisplayedShipPosition("player_ship_2").x), Is.False);
            Assert.That(float.IsInfinity(map.DisplayedShipPosition("player_ship_2").y), Is.False);
            Assert.That(session.Snapshot, Is.SameAs(snapshot), "Presentation must not mutate a newly completed ship.");
            session.Reset();
            map.Present(session.Snapshot, session.SelectedPortId, null, false, 1);
            Assert.That(map.ShipMarkerCount, Is.EqualTo(4));
            Assert.That(map.Q<Button>("ship_player_ship_2"), Is.Null);
        }

        [Test]
        public void InitialPresentationOfLoadedFleetAndNpcInspectionNeedNoSinglePlayerAssumption()
        {
            var session = CreateSession();
            CompleteShip(session);
            session.LoadSaveText(session.SaveText());
            var map = new ArchipelagoMap();
            map.Present(session.Snapshot, session.SelectedPortId, "player_ship_2", false, 1);
            Assert.That(map.ShipMarkerCount, Is.EqualTo(session.Snapshot.Ships.Count));
            Assert.That(map.Query<Label>(className: "route-distance").ToList().All(label =>
                label.tooltip.Contains(session.Snapshot.Ships["player_ship_2"].Name)), Is.True);
            map.Present(session.Snapshot, session.SelectedPortId, ArchipelagoScenario.TimberShipId, false, 1);
            Assert.That(map.Q<Button>("ship_" + ArchipelagoScenario.TimberShipId).ClassListContains("selected-marker"), Is.True);
            Assert.That(map.Query<Label>(className: "route-distance").ToList().All(label => label.text.Contains(" units")), Is.True);
        }
    }
}
