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
            Assert.That(session.Content.Goods.Count, Is.EqualTo(7));
            Assert.That(session.Snapshot.Economy, Is.Not.Null);
        }

        [Test]
        public void CorruptLoadPreservesCurrentSnapshotSelectionAndPlayback()
        {
            var session = Session();
            session.SelectShip(WorldIds.PlayerShipId);
            session.Play();
            var before = session.Snapshot;
            var selection = session.SelectedShipId;
            Assert.Throws<WorldSaveException>(() => session.LoadSaveText("{invalid"));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.SelectedShipId, Is.EqualTo(selection));
            Assert.That(session.IsPlaying, Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void UnsupportedSchemaPreservesTheActiveSession(int schema)
        {
            var session = Session();
            var saved = Newtonsoft.Json.Linq.JObject.Parse(session.SaveText());
            saved["SchemaVersion"] = schema;
            session.SelectShip(session.Player.Id);
            session.Play();
            var before = session.Snapshot;
            Assert.Throws<WorldSaveException>(() => session.LoadSaveText(saved.ToString()));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.SelectedShipId, Is.EqualTo(session.Player.Id));
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
            var starter = session.Snapshot.Ships[WorldIds.PlayerShipId];
            var route = new CargoItineraryPolicy("second_ship", new[] {
                new CargoItineraryStop(economy.Configuration.ShipwrightPortId, new[] {
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "planks", 20000) }),
                new CargoItineraryStop(economy.Configuration.SilverPortId, new[] {
                    new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "planks", 20000) }) });
            session.ApplyItinerary(route);
            session.SetAutomationEnabled(true);
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            Assert.That(session.Snapshot.Ships[WorldIds.PlayerShipId].Policy, Is.SameAs(starter.Policy));
            Assert.That(session.Snapshot.Ships[WorldIds.PlayerShipId].AutomationEnabled, Is.False);
            session.SelectShip(WorldIds.TimberShipId);
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            session.SelectPort(economy.Configuration.ShipwrightPortId);
            session.SetAutomationEnabled(false);
            Assert.That(session.Snapshot.Ships["player_ship_2"].AutomationEnabled, Is.False);
            Assert.That(session.Snapshot.Ships[WorldIds.TimberShipId].AutomationEnabled, Is.True);
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
        }

    }
}
