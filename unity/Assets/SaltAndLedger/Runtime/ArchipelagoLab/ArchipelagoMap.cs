using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Original vector chart and selectable markers. Positions never feed back into the simulation.</summary>
    public sealed class ArchipelagoMap : VisualElement
    {
        private readonly Dictionary<string, Button> ports = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> ships = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> fromPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> toPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector2> displayedPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal);
        private readonly List<Label> distanceLabels = new List<Label>();
        private RegionalWorldSnapshot snapshot;
        private string selectedPortId;
        private string selectedShipId;
        private float interpolation = 1;
        private float interval = 1;

        public event Action<string> PortSelected;
        public event Action<string> ShipSelected;
        public RegionalWorldSnapshot Snapshot => snapshot;
        public int PortMarkerCount => ports.Count;
        public int ShipMarkerCount => ships.Count;

        public ArchipelagoMap()
        {
            name = "archipelagoMap";
            AddToClassList("archipelago-map");
            generateVisualContent += DrawChart;
            RegisterCallback<GeometryChangedEvent>(_ => ArrangeMarkers());
        }

        public void Present(RegionalWorldSnapshot next, string portId, string shipId, bool animate, double seconds)
        {
            selectedPortId = portId;
            selectedShipId = shipId;
            if (!ReferenceEquals(snapshot, next))
            {
                var previous = snapshot;
                snapshot = next;
                if (ports.Count == 0) CreateMarkers();
                foreach (var ship in snapshot.Ships.Values)
                {
                    var point = ShipPosition(ship);
                    fromPositions[ship.Id] = displayedPositions.TryGetValue(ship.Id, out var old) ? old : point;
                    toPositions[ship.Id] = point;
                }
                interpolation = animate && previous != null && next.Day > previous.Day ? 0 : 1;
                interval = Mathf.Max(0.02f, (float)seconds);
            }
            foreach (var port in snapshot.Ports.Values)
            {
                var occupants = snapshot.Ships.Values.Where(ship => ship.DockedPortId == port.Id).ToArray();
                var playerCount = occupants.Count(ship => ship.IsPlayer);
                var npcCount = occupants.Length - playerCount;
                ports[port.Id].text = port.Name + "\n" + playerCount + " player · " + npcCount + " NPC";
                ports[port.Id].EnableInClassList("selected-marker", selectedShipId == null && port.Id == selectedPortId);
                ports[port.Id].tooltip = port.Role + "\nClick to inspect the finite market and local economy.";
            }
            foreach (var ship in snapshot.Ships.Values)
            {
                ships[ship.Id].EnableInClassList("selected-marker", ship.Id == selectedShipId);
                ships[ship.Id].tooltip = ship.Name + " · " + (ship.IsPlayer ? "Your ship" : "NPC merchant") +
                    "\n" + ship.CargoUnits + "/" + ship.CargoCapacity + " cargo" +
                    (ship.DockedPortId != null ? "\nDocked at " + snapshot.Ports[ship.DockedPortId].Name :
                        "\nTo " + snapshot.Ports[ship.DestinationPortId].Name + " · " + ship.RemainingTravelDays + " days remain");
            }
            ArrangeMarkers();
            MarkDirtyRepaint();
        }

        public void AdvanceVisual(float elapsed, bool playing)
        {
            if (snapshot == null) return;
            var before = interpolation;
            interpolation = playing ? Mathf.Clamp01(interpolation + elapsed / interval) : 1;
            if (before != interpolation) ArrangeMarkers();
        }

        public Vector2 DisplayedShipPosition(string shipId) => displayedPositions[shipId];

        private void CreateMarkers()
        {
            foreach (var port in snapshot.Ports.Values.OrderBy(port => port.Id, StringComparer.Ordinal))
            {
                var id = port.Id;
                var button = new Button(() => PortSelected?.Invoke(id)) { name = "port_" + id };
                button.AddToClassList("port-marker");
                ports.Add(id, button);
                Add(button);
            }
            var index = 0;
            foreach (var ship in snapshot.Ships.Values.OrderBy(ship => ship.Id, StringComparer.Ordinal))
            {
                var id = ship.Id;
                var button = new Button(() => ShipSelected?.Invoke(id))
                {
                    name = "ship_" + id,
                    text = ship.IsPlayer ? "▲ P" : "▲ " + (++index)
                };
                button.AddToClassList("ship-marker");
                button.AddToClassList(ship.IsPlayer ? "player-marker" : "npc-marker");
                ships.Add(id, button);
                Add(button);
            }
            foreach (var route in snapshot.Routes)
            {
                var player = snapshot.Ships.Values.Single(ship => ship.IsPlayer);
                var label = new Label(route.DurationDays(player.Configuration.Speed) + " days") { pickingMode = PickingMode.Ignore };
                label.AddToClassList("route-distance");
                distanceLabels.Add(label);
                Insert(0, label);
            }
        }

        private Vector2 ShipPosition(RegionalShipSnapshot ship)
        {
            if (ship.DockedPortId != null)
            {
                var port = snapshot.Ports[ship.DockedPortId];
                var docked = snapshot.Ships.Values.Where(value => value.DockedPortId == ship.DockedPortId)
                    .OrderBy(value => value.Id, StringComparer.Ordinal).Select(value => value.Id).ToArray();
                var slot = Array.IndexOf(docked, ship.Id);
                var chartWidth = ChartExtent(contentRect.width, 136, 1000);
                var chartHeight = ChartExtent(contentRect.height, 124, 700);
                // Keep docked groups below the town's label at every viewport size.
                return new Vector2(port.X + (20 + slot * 50) * 1000 / chartWidth,
                    port.Y + 52 * 700 / chartHeight);
            }
            var origin = snapshot.Ports[ship.OriginPortId];
            var destination = snapshot.Ports[ship.DestinationPortId];
            var progress = (ship.VoyageDurationDays - ship.RemainingTravelDays) / (float)ship.VoyageDurationDays;
            return Vector2.Lerp(new Vector2(origin.X, origin.Y), new Vector2(destination.X, destination.Y), progress);
        }

        private Vector2 ChartPoint(Vector2 position)
        {
            const float paddingX = 68;
            const float paddingY = 62;
            return new Vector2(paddingX + position.x / 1000f * ChartExtent(contentRect.width, 2 * paddingX, 1000),
                paddingY + position.y / 700f * ChartExtent(contentRect.height, 2 * paddingY, 700));
        }

        private static float ChartExtent(float size, float padding, float fallback)
        {
            // Present can run before UI Toolkit has resolved the first layout.
            return float.IsNaN(size) || float.IsInfinity(size) ? fallback : Mathf.Max(1, size - padding);
        }

        private Vector2 PortPoint(string portId)
        {
            var port = snapshot.Ports[portId];
            return ChartPoint(new Vector2(port.X, port.Y));
        }

        private void ArrangeMarkers()
        {
            if (snapshot == null) return;
            foreach (var port in snapshot.Ports.Values)
            {
                var point = PortPoint(port.Id);
                // Spatial transforms must follow live chart coordinates. Appearance stays in USS.
                ports[port.Id].style.translate = new Translate(point.x - 66, point.y - 24);
            }
            foreach (var ship in snapshot.Ships.Values)
            {
                if (ship.DockedPortId != null) toPositions[ship.Id] = ShipPosition(ship);
                var chartPosition = Vector2.Lerp(fromPositions[ship.Id], toPositions[ship.Id], interpolation);
                displayedPositions[ship.Id] = chartPosition;
                var point = ChartPoint(chartPosition);
                ships[ship.Id].style.translate = new Translate(point.x - 22, point.y - 16);
            }
            var occupiedCaptions = snapshot.Ports.Keys.Select(id =>
            {
                var point = PortPoint(id);
                return new Rect(point.x - 70, point.y - 28, 140, 56);
            }).ToList();
            for (var i = 0; i < distanceLabels.Count; i++)
            {
                var route = snapshot.Routes[i];
                var start = PortPoint(route.OriginPortId);
                var end = PortPoint(route.DestinationPortId);
                var direction = (end - start).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                var point = (start + end) / 2 + normal * 12;
                foreach (var fraction in new[] { 0.5f, 0.35f, 0.65f, 0.25f, 0.75f })
                {
                    var candidate = Vector2.Lerp(start, end, fraction) + normal * 12;
                    var bounds = new Rect(candidate.x - 39, candidate.y - 13, 78, 26);
                    if (occupiedCaptions.Any(occupied => occupied.Overlaps(bounds))) continue;
                    point = candidate;
                    break;
                }
                occupiedCaptions.Add(new Rect(point.x - 39, point.y - 13, 78, 26));
                distanceLabels[i].style.translate = new Translate(point.x - 35, point.y - 8);
            }
            MarkDirtyRepaint();
        }

        private void DrawChart(MeshGenerationContext context)
        {
            if (snapshot == null || contentRect.width < 1 || contentRect.height < 1) return;
            var painter = context.painter2D;
            painter.lineWidth = 1;
            painter.strokeColor = new Color(0.38f, 0.62f, 0.65f, 0.16f);
            for (var row = 0; row < 8; row++)
            {
                var y = 35 + row * contentRect.height / 8;
                for (var column = 0; column < 8; column++)
                {
                    var x = 22 + column * contentRect.width / 8 + (row % 2) * 15;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, y));
                    painter.BezierCurveTo(new Vector2(x + 8, y - 3), new Vector2(x + 16, y + 3), new Vector2(x + 26, y));
                    painter.Stroke();
                }
            }
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.49f, 0.72f, 0.73f, 0.42f);
            foreach (var route in snapshot.Routes)
            {
                var start = PortPoint(route.OriginPortId);
                var end = PortPoint(route.DestinationPortId);
                for (var part = 0; part < 15; part++)
                {
                    painter.BeginPath();
                    painter.MoveTo(Vector2.Lerp(start, end, part / 15f));
                    painter.LineTo(Vector2.Lerp(start, end, (part + 0.55f) / 15f));
                    painter.Stroke();
                }
            }
            var islandColors = new[]
            {
                new Color(0.30f, 0.43f, 0.35f), new Color(0.24f, 0.40f, 0.35f),
                new Color(0.40f, 0.41f, 0.30f), new Color(0.34f, 0.46f, 0.37f)
            };
            var shape = new[] { new Vector2(-95, -13), new Vector2(-74, -47), new Vector2(-34, -56),
                new Vector2(-7, -43), new Vector2(34, -51), new Vector2(84, -20), new Vector2(96, 10),
                new Vector2(63, 27), new Vector2(17, 23), new Vector2(-21, 34), new Vector2(-62, 20) };
            var islandIndex = 0;
            foreach (var port in snapshot.Ports.Values.OrderBy(port => port.Id, StringComparer.Ordinal))
            {
                var point = PortPoint(port.Id);
                var scale = Mathf.Clamp(contentRect.width / 1000, 0.65f, 1.2f);
                painter.fillColor = islandColors[islandIndex % islandColors.Length];
                painter.strokeColor = new Color(0.73f, 0.70f, 0.49f, 0.8f);
                painter.lineWidth = 3;
                painter.BeginPath();
                for (var vertex = 0; vertex < shape.Length; vertex++)
                {
                    var offset = shape[(vertex + islandIndex) % shape.Length];
                    var position = point + new Vector2(offset.x, offset.y * (1 + islandIndex * 0.12f)) * scale;
                    if (vertex == 0) painter.MoveTo(position); else painter.LineTo(position);
                }
                painter.ClosePath();
                painter.Fill();
                painter.Stroke();
                islandIndex++;
            }
        }
    }
}
