using System;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class MapViewportTests
    {
        [TestCase(1366, 500, 80, 60)]
        [TestCase(1920, 800, 104, 85)]
        [TestCase(520, 500, 110, 90)]
        [TestCase(220, 160, 160, 120)]
        public void FitAndInverseKeepWorldCornersInsideThePaddedViewport(int width, int height, int xPad, int yPad)
        {
            var world = new Rect(134, 96, 713, 498);
            var transform = MapViewport.Fit(world, new Rect(0, 0, width, height), new Vector4(xPad, yPad, xPad, yPad));
            foreach (var position in new[] { world.min, world.max, world.center, new Vector2(world.xMax, world.yMin) })
            {
                var view = transform.WorldToViewport(position);
                Assert.That(view.x, Is.InRange(transform.ContentBounds.xMin - 0.001f, transform.ContentBounds.xMax + 0.001f));
                Assert.That(view.y, Is.InRange(transform.ContentBounds.yMin - 0.001f, transform.ContentBounds.yMax + 0.001f));
                Assert.That(Vector2.Distance(transform.ViewportToWorld(view), position), Is.LessThan(0.001f));
            }
            Assert.That(transform.Scale, Is.GreaterThan(0));
            Assert.That(Vector2.Distance(transform.WorldToViewport(world.center), transform.ContentBounds.center), Is.LessThan(0.001f));
        }

        [Test]
        public void WorldInterpolationUsesTheSameTransformAsMarkersAtDifferentAspectRatios()
        {
            var origin = new Vector2(100, 130);
            var destination = new Vector2(810, 620);
            foreach (var size in new[] { new Vector2(1366, 450), new Vector2(1920, 820), new Vector2(550, 650) })
            {
                var transform = MapViewport.Fit(new Rect(origin, destination - origin), new Rect(Vector2.zero, size),
                    new Vector4(100, 75, 100, 85));
                var halfway = transform.WorldToViewport(Vector2.Lerp(origin, destination, 0.375f));
                var viewInterpolation = Vector2.Lerp(transform.WorldToViewport(origin), transform.WorldToViewport(destination), 0.375f);
                Assert.That(Vector2.Distance(halfway, viewInterpolation), Is.LessThan(0.001f));
            }
        }

        [Test]
        public void DegenerateWorldAndSmallViewportRemainInvertibleAndInvalidNumbersAreRejected()
        {
            var transform = MapViewport.Fit(new Rect(5, 9, 0, 0), new Rect(0, 0, 1, 1), new Vector4(100, 100, 100, 100));
            Assert.That(transform.Scale, Is.GreaterThan(0));
            Assert.That(Vector2.Distance(transform.ViewportToWorld(transform.WorldToViewport(new Vector2(5, 9))), new Vector2(5, 9)), Is.LessThan(0.001f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapViewport.Fit(new Rect(float.NaN, 0, 10, 10), new Rect(0, 0, 100, 100), Vector4.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapViewport.Fit(new Rect(0, 0, 10, 10), new Rect(0, 0, 0, 100), Vector4.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => MapViewport.Fit(new Rect(0, 0, 10, 10), new Rect(0, 0, 100, 100), new Vector4(float.PositiveInfinity, 0, 0, 0)));
        }

        [Test]
        public void ContextualRoutesAndExplicitOverlayAreReadOnlyAndKeepAllMarkers()
        {
            var session = CreateSession();
            var snapshot = session.Snapshot;
            var saved = session.SaveText();
            var map = new ArchipelagoMap();
            map.Present(snapshot, null, null, false, 1);
            Assert.That(map.VisibleRouteCount, Is.Zero);
            Assert.That(map.Query<Label>(className: "route-distance").ToList().All(label => label.ClassListContains("hidden")), Is.True);
            Assert.That(map.PortMarkerCount, Is.EqualTo(4));
            Assert.That(map.ShipMarkerCount, Is.EqualTo(snapshot.Ships.Count));
            map.Present(snapshot, snapshot.Ports.Keys.First(), null, false, 1);
            Assert.That(map.VisibleRouteCount, Is.EqualTo(3));
            map.ShowAllRoutes = true;
            Assert.That(map.VisibleRouteCount, Is.EqualTo(6));
            map.ShowAllRoutes = false;
            map.Present(snapshot, null, session.Player.Id, false, 1);
            Assert.That(session.Player.Policy, Is.Not.Null, "The compatibility policy remains in the authoritative snapshot.");
            Assert.That(map.VisibleRouteCount, Is.Zero, "An unassigned modern itinerary must not display a compatibility route.");
            Assert.That(map.HighlightedRoutePortIds, Is.Empty);
            Assert.That(map.Q<Button>("ship_" + session.Player.Id).focusable, Is.True);
            Assert.That(map.Query<Button>(className: "port-marker").ToList().All(marker => !marker.text.Contains("0 player")), Is.True);
            var npc = snapshot.Ships.Values.First(ship => !ship.IsPlayer && ship.Policy != null);
            map.Present(snapshot, null, npc.Id, false, 1);
            Assert.That(map.VisibleRouteCount, Is.EqualTo(1), "NPC merchant policy remains its genuine route.");
            Assert.That(session.Snapshot, Is.SameAs(snapshot));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
        }

        [TestCase("Processing: Mill and Sawmill; imports Grain, Logs and Bread", "Processing")]
        [TestCase("Vulnerable coast: Lumber Camp; no public food production; imports Bread and Planks", "Coastal")]
        [TestCase("Forest furniture: Lumber Camp and Furniture Workshop; no Sawmill; imports Planks and Bread", "Forest")]
        [TestCase("Food source: Farm, Lumber Camp and Bakery; imports Flour", "Food source")]
        public void ShortRoleUsesTheAuthoredRoleRatherThanItsImportOrMissingIndustryDescription(string description, string expected)
        {
            Assert.That(ArchipelagoMap.ShortRole(description), Is.EqualTo(expected));
        }

        [Test]
        public void ModernUnassignedShipDrawsOnlyItsActualManualVoyage()
        {
            var session = CreateSession();
            var origin = session.Player.DockedPortId;
            var destination = session.Snapshot.Ports.Keys.First(id => id != origin);
            session.SailTo(destination);
            var snapshot = session.Snapshot;
            var saved = session.SaveText();
            var map = new ArchipelagoMap();
            map.Present(snapshot, null, session.Player.Id, false, 1);
            Assert.That(map.VisibleRouteCount, Is.EqualTo(1));
            Assert.That(map.HighlightedRoutePortIds, Is.EqualTo(new[] { origin, destination }));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
        }

        [Test]
        public void ItineraryOverlayKeepsExactStopOrderAndInspectionDoesNotEnableIt()
        {
            var session = CreateSession();
            var first = session.Player.DockedPortId;
            var stops = new[] { first }.Concat(session.Snapshot.Ports.Keys.Where(id => id != first).Take(2)).ToArray();
            session.ApplyItinerary(new CargoItineraryPolicy("map_witness", stops.Select(id => new CargoItineraryStop(id,
                new[] { new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", EconomicUnits.QuantityScale) }))));
            var snapshot = session.Snapshot;
            var saved = session.SaveText();
            var map = new ArchipelagoMap();
            map.Present(snapshot, null, session.Player.Id, false, 1);
            Assert.That(map.HighlightedRoutePortIds, Is.EqualTo(stops));
            Assert.That(map.VisibleRouteCount, Is.EqualTo(3));
            for (var index = 0; index < stops.Length; index++)
                Assert.That(map.Q<Button>("port_" + stops[index]).text, Does.Contain("Stop " + (index + 1)));
            foreach (var label in map.Query<Label>(className: "route-distance").ToList())
                Assert.That(label.tooltip, Does.Contain(session.Player.Name));
            map.Present(snapshot, stops[1], null, false, 1);
            map.Present(snapshot, null, session.Player.Id, false, 1);
            Assert.That(session.Player.AutomationEnabled, Is.False);
            Assert.That(session.Snapshot, Is.SameAs(snapshot));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
        }

        [Test]
        public void Seed99DayOneLargeTownLabelsAvoidRealSailingMarkersWithoutChangingTheWorld()
        {
            var session = CreateSession();
            session.NextDay();
            var snapshot = session.Snapshot;
            var saved = session.SaveText();
            var minimum = new Vector2(snapshot.Ports.Values.Min(port => port.X), snapshot.Ports.Values.Min(port => port.Y));
            var maximum = new Vector2(snapshot.Ports.Values.Max(port => port.X), snapshot.Ports.Values.Max(port => port.Y));
            var viewport = new Rect(0, 0, 1348, 350);
            var transform = MapViewport.Fit(new Rect(minimum, maximum - minimum), viewport, new Vector4(114, 76, 114, 128));
            var labelSize = new Vector2(184, 68);
            var anchors = snapshot.Ports.Values.ToDictionary(port => port.Id,
                port => transform.WorldToViewport(new Vector2(port.X, port.Y)));
            var labels = anchors.ToDictionary(pair => pair.Key, pair => new Rect(pair.Value - labelSize / 2, labelSize));
            var sailing = snapshot.Ships.Values.Where(ship => ship.DockedPortId == null).Select(ship =>
            {
                var origin = snapshot.Ports[ship.OriginPortId];
                var destination = snapshot.Ports[ship.DestinationPortId];
                var progress = (ship.VoyageDurationDays - ship.RemainingTravelDays) / (float)ship.VoyageDurationDays;
                var world = Vector2.Lerp(new Vector2(origin.X, origin.Y), new Vector2(destination.X, destination.Y), progress);
                return new Rect(transform.WorldToViewport(world) - new Vector2(31, 22), new Vector2(62, 44));
            }).ToArray();
            Assert.That(sailing, Is.Not.Empty);
            Assert.That(labels.Values.Any(label => sailing.Any(ship => label.Overlaps(ship))), Is.True,
                "The witness must actually include the Large-scale label collision seen in the Editor.");
            foreach (var id in labels.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray())
                labels[id] = ArchipelagoMap.PlacePortLabel(anchors[id], labelSize, viewport,
                    sailing.Concat(labels.Where(pair => pair.Key != id).Select(pair => pair.Value)).ToArray());
            foreach (var pair in labels)
            {
                Assert.That(viewport.Contains(pair.Value.min) && viewport.Contains(pair.Value.max), Is.True, pair.Key);
                Assert.That(sailing.Any(ship => ship.Overlaps(pair.Value)), Is.False, pair.Key);
                Assert.That(labels.Any(other => other.Key != pair.Key && other.Value.Overlaps(pair.Value)), Is.False, pair.Key);
            }
            Assert.That(labels.Any(pair => pair.Value.center != anchors[pair.Key]), Is.True);
            Assert.That(session.Snapshot, Is.SameAs(snapshot));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            var free = ArchipelagoMap.PlacePortLabel(new Vector2(300, 150), labelSize, viewport, Array.Empty<Rect>());
            Assert.That(free.center, Is.EqualTo(new Vector2(300, 150)), "Unobstructed labels retain the exact port center.");
        }

        [Test]
        public void Seed99DayOneShipCalloutsSeparatePlayerAndNpcWhileKeepingExactVoyageAnchors()
        {
            var session = CreateSession();
            session.SailTo(WorldIds.West);
            session.NextDay();
            var snapshot = session.Snapshot;
            var saved = session.SaveText();
            var minimum = new Vector2(snapshot.Ports.Values.Min(port => port.X), snapshot.Ports.Values.Min(port => port.Y));
            var maximum = new Vector2(snapshot.Ports.Values.Max(port => port.X), snapshot.Ports.Values.Max(port => port.Y));
            var viewport = new Rect(0, 0, 1348, 350);
            var transform = MapViewport.Fit(new Rect(minimum, maximum - minimum), viewport, new Vector4(114, 76, 114, 128));
            Vector2 VoyagePoint(RegionalShipSnapshot ship)
            {
                var origin = snapshot.Ports[ship.OriginPortId];
                var destination = snapshot.Ports[ship.DestinationPortId];
                return Vector2.Lerp(new Vector2(origin.X, origin.Y), new Vector2(destination.X, destination.Y),
                    (ship.VoyageDurationDays - ship.RemainingTravelDays) / (float)ship.VoyageDurationDays);
            }
            var player = session.Player;
            var npc = snapshot.Ships[WorldIds.TimberShipId];
            Assert.That(player.DockedPortId, Is.Null);
            Assert.That(npc.DockedPortId, Is.Null);
            var playerAnchor = transform.WorldToViewport(VoyagePoint(player));
            var npcAnchor = transform.WorldToViewport(VoyagePoint(npc));
            var size = new Vector2(62, 44);
            Assert.That(new Rect(playerAnchor - size / 2, size).Overlaps(new Rect(npcAnchor - size / 2, size)), Is.True,
                "The witness must reproduce the P1/N3 Large-scale collision seen in the actual player.");
            var playerCallout = ArchipelagoMap.PlacePortLabel(playerAnchor, size, viewport, Array.Empty<Rect>());
            var npcCallout = ArchipelagoMap.PlacePortLabel(npcAnchor, size, viewport, new[] { playerCallout });
            Assert.That(playerCallout.center, Is.EqualTo(playerAnchor));
            Assert.That(npcCallout.Overlaps(playerCallout), Is.False);
            Assert.That(Vector2.Distance(npcCallout.center, npcAnchor), Is.InRange(1, 80));
            Assert.That(viewport.Contains(npcCallout.min) && viewport.Contains(npcCallout.max), Is.True);

            var map = new ArchipelagoMap();
            map.Present(snapshot, WorldIds.North, null, false, 1);
            map.ShowAllRoutes = true;
            foreach (var ship in snapshot.Ships.Values.Where(ship => ship.DockedPortId == null))
            {
                Assert.That(Vector2.Distance(map.DisplayedShipPosition(ship.Id), VoyagePoint(ship)), Is.LessThan(0.001f), ship.Id);
                Assert.That(map.Q<Button>("ship_" + ship.Id).focusable, Is.True);
                Assert.That(snapshot.Ships.Values.Any(other => other.Id != ship.Id &&
                    map.DisplayedShipMarkerBounds(other.Id).Overlaps(map.DisplayedShipMarkerBounds(ship.Id))), Is.False, ship.Id);
            }
            Assert.That(session.Snapshot, Is.SameAs(snapshot));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
        }

        [Test]
        public void SailingInterpolationDoesNotAlterVoyageOrSaveAndUsesWorldCoordinates()
        {
            var session = CreateSession();
            var origin = session.Player.DockedPortId;
            var destination = session.Snapshot.Ports.Keys.First(id => id != origin);
            session.SailTo(destination);
            var map = new ArchipelagoMap();
            map.Present(session.Snapshot, null, session.Player.Id, false, 1);
            var from = map.DisplayedShipPosition(session.Player.Id);
            session.NextDay();
            var snapshot = session.Snapshot;
            var saved = session.SaveText();
            map.Present(snapshot, null, session.Player.Id, true, 1);
            map.AdvanceVisual(0.5f, true);
            var middle = map.DisplayedShipPosition(session.Player.Id);
            map.AdvanceVisual(0.5f, true);
            var to = map.DisplayedShipPosition(session.Player.Id);
            Assert.That(Vector2.Distance(middle, Vector2.Lerp(from, to, 0.5f)), Is.LessThan(0.001f));
            Assert.That(Vector2.Distance(map.ViewportToWorld(map.WorldToViewport(middle)), middle), Is.LessThan(0.001f));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Assert.That(session.Snapshot, Is.SameAs(snapshot));
        }

        private static ArchipelagoLabSession CreateSession()
        {
            var session = new ArchipelagoLabSession(ContentLoader.Load(Resources.Load<TextAsset>("CanonicalContent").text));
            session.StartRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 99,
                companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1, economicRulesVersion: 1));
            return session;
        }
    }
}
