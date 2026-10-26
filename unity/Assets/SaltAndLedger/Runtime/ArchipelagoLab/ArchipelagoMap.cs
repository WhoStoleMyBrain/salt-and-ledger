using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly Dictionary<string, Rect> portLabelBounds = new Dictionary<string, Rect>(StringComparer.Ordinal);
        private readonly Dictionary<string, Rect> shipMarkerBounds = new Dictionary<string, Rect>(StringComparer.Ordinal);
        private readonly List<Label> distanceLabels = new List<Label>();
        private readonly List<Vector2> captionCenters = new List<Vector2>();
        private readonly Dictionary<string, RegionalDecision> decisions = new Dictionary<string, RegionalDecision>(StringComparer.Ordinal);
        private readonly List<string> highlightedRoute = new List<string>();
        private RegionalWorldSnapshot snapshot;
        private string selectedPortId;
        private string selectedShipId;
        private float interpolation = 1;
        private float interval = 1;
        private bool showAllRoutes;

        public event Action<string> PortSelected;
        public event Action<string> PortTradeRequested;
        public event Action<string> ShipSelected;
        public RegionalWorldSnapshot Snapshot => snapshot;
        public int PortMarkerCount => ports.Count;
        public int ShipMarkerCount => ships.Count;
        public MapViewport Viewport { get; private set; }
        public IReadOnlyList<string> HighlightedRoutePortIds => highlightedRoute.AsReadOnly();
        public int VisibleRouteCount => snapshot == null ? 0 : snapshot.Routes.Count(RouteIsVisible);
        public bool ShowAllRoutes
        {
            get => showAllRoutes;
            set
            {
                if (showAllRoutes == value) return;
                showAllRoutes = value;
                RefreshRouteVisibility();
                ArrangeMarkers();
            }
        }

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
                SynchronizeShipMarkers();
                RefreshViewport();
                decisions.Clear();
                foreach (var ship in snapshot.Ships.Values) decisions.Add(ship.Id, RegionalAutomation.Evaluate(snapshot, ship.Id));
                foreach (var ship in snapshot.Ships.Values)
                {
                    var point = ShipPosition(ship);
                    fromPositions[ship.Id] = displayedPositions.TryGetValue(ship.Id, out var old) ? old : point;
                    toPositions[ship.Id] = point;
                }
                interpolation = animate && previous != null && next.Day > previous.Day ? 0 : 1;
                interval = Mathf.Max(0.02f, (float)seconds);
            }
            RefreshHighlightedRoute();
            foreach (var port in snapshot.Ports.Values)
            {
                var stop = selectedShipId != null && snapshot.Ships[selectedShipId].Itinerary != null
                    ? highlightedRoute.IndexOf(port.Id) : -1;
                ports[port.Id].text = port.Name + "\n" + (stop < 0 ? ShortRole(port) : "Stop " + (stop + 1));
                ports[port.Id].EnableInClassList("selected-marker", selectedShipId == null && port.Id == selectedPortId);
                ports[port.Id].tooltip = port.Role + "\nLeft-click to inspect. Right-click to open the market exchange.";
            }
            foreach (var ship in snapshot.Ships.Values)
            {
                var decision = decisions[ship.Id];
                var blocked = ship.AutomationEnabled && ship.DockedPortId != null &&
                    (decision.Action == AutomationAction.Wait || decision.Action == AutomationAction.Pause);
                ships[ship.Id].EnableInClassList("selected-marker", ship.Id == selectedShipId);
                ships[ship.Id].EnableInClassList("sl-marker-blocked", blocked);
                ships[ship.Id].EnableInClassList("sl-marker-docked", ship.DockedPortId != null);
                ships[ship.Id].EnableInClassList("sl-marker-sailing", ship.DockedPortId == null);
                ships[ship.Id].EnableInClassList("sl-marker-active", ship.AutomationEnabled && !blocked);
                ships[ship.Id].Q<Label>(className: "sl-map-blocked-badge").EnableInClassList("hidden", !blocked);
                ships[ship.Id].tooltip = ship.Name + " · " + (ship.IsPlayer ? "Your ship" : "NPC merchant") +
                    "\n" + Quantity(ship.CargoUnits) + "/" + Quantity(ship.CargoCapacity) + " cargo" +
                    (ship.DockedPortId != null ? "\nDocked at " + snapshot.Ports[ship.DockedPortId].Name :
                        "\nTo " + snapshot.Ports[ship.DestinationPortId].Name + " · " + ship.RemainingTravelDays + " days remain") +
                    "\n" + (blocked ? "Waiting: " : ship.AutomationEnabled ? "Active: " : "Manual: ") + decision.Reason;
            }
            var player = selectedShipId != null && snapshot.Ships.TryGetValue(selectedShipId, out var selected)
                ? selected : snapshot.Ships.Values.Where(ship => ship.IsPlayer).OrderBy(ship => ship.Id, StringComparer.Ordinal).First();
            for (var index = 0; index < snapshot.Routes.Count; index++)
            {
                var route = snapshot.Routes[index];
                distanceLabels[index].text = route.Distance + " units · " + route.DurationDays(player.Configuration.Speed) + "d";
                distanceLabels[index].tooltip = snapshot.Ports[route.OriginPortId].Name + " — " +
                    snapshot.Ports[route.DestinationPortId].Name + ": " + route.Distance + " abstract distance units; " +
                    route.DurationDays(player.Configuration.Speed) + " days in " + player.Name + ". Direct sea connection; no coastline pathfinding.";
            }
            RefreshRouteVisibility();
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
        public Rect DisplayedShipMarkerBounds(string shipId) => shipMarkerBounds[shipId];
        public Vector2 WorldToViewport(Vector2 world) => Viewport.WorldToViewport(world);
        public Vector2 ViewportToWorld(Vector2 point) => Viewport.ViewportToWorld(point);

        private void RefreshHighlightedRoute()
        {
            highlightedRoute.Clear();
            if (selectedShipId == null || !snapshot.Ships.TryGetValue(selectedShipId, out var ship)) return;
            if (ship.Itinerary != null) highlightedRoute.AddRange(ship.Itinerary.Stops.Select(stop => stop.PortId));
            else if (ship.IsPlayer && snapshot.RunIdentity?.ItineraryVersion == 1)
            {
                // Modern manual ships retain a compatibility merchant policy in the snapshot.
                // It is not an assigned itinerary; only their real current voyage may be drawn.
                if (ship.DockedPortId == null)
                { highlightedRoute.Add(ship.OriginPortId); highlightedRoute.Add(ship.DestinationPortId); }
            }
            else if (ship.IndustrialPolicy != null)
            { highlightedRoute.Add(ship.IndustrialPolicy.OriginPortId); highlightedRoute.Add(ship.IndustrialPolicy.DestinationPortId); }
            else if (ship.Policy != null)
            { highlightedRoute.Add(ship.Policy.OriginPortId); highlightedRoute.Add(ship.Policy.DestinationPortId); }
            else if (ship.DockedPortId == null)
            { highlightedRoute.Add(ship.OriginPortId); highlightedRoute.Add(ship.DestinationPortId); }
        }

        private bool RouteIsHighlighted(SeaRoute route)
        {
            for (var index = 0; index < highlightedRoute.Count; index++)
                if (Joins(route, highlightedRoute[index], highlightedRoute[(index + 1) % highlightedRoute.Count])) return true;
            return false;
        }

        private bool RouteIsVisible(SeaRoute route) => ShowAllRoutes ||
            (selectedShipId != null ? RouteIsHighlighted(route) :
                selectedPortId != null && (route.OriginPortId == selectedPortId || route.DestinationPortId == selectedPortId));

        private static bool Joins(SeaRoute route, string first, string second) =>
            route.OriginPortId == first && route.DestinationPortId == second ||
            route.OriginPortId == second && route.DestinationPortId == first;

        private void RefreshRouteVisibility()
        {
            if (snapshot == null) return;
            for (var index = 0; index < snapshot.Routes.Count; index++)
                distanceLabels[index].EnableInClassList("hidden", !RouteIsVisible(snapshot.Routes[index]));
        }

        private static string ShortRole(RegionalPortSnapshot port) => ShortRole(port.Role);

        public static string ShortRole(string description)
        {
            var role = (description ?? "").Split(':', ';')[0].ToLowerInvariant();
            return role.Contains("process") || role.Contains("sawmill") ? "Processing" :
                role.Contains("coast") ? "Coastal" : role.Contains("food") || role.Contains("grain") ? "Food source" :
                role.Contains("forest") || role.Contains("timber") || role.Contains("lumber") ? "Forest" : "Harbor";
        }

        private void CreateMarkers()
        {
            foreach (var port in snapshot.Ports.Values.OrderBy(port => port.Id, StringComparer.Ordinal))
            {
                var id = port.Id;
                var button = new Button(() => PortSelected?.Invoke(id)) { name = "port_" + id };
                button.AddToClassList("port-marker");
                button.RegisterCallback<GeometryChangedEvent>(_ => ArrangeMarkers());
                button.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1) return;
                    evt.StopImmediatePropagation();
                    PortTradeRequested?.Invoke(id);
                }, TrickleDown.TrickleDown);
                ports.Add(id, button);
                Add(button);
            }
            foreach (var route in snapshot.Routes)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("route-distance");
                label.RegisterCallback<GeometryChangedEvent>(_ => ArrangeMarkers());
                distanceLabels.Add(label);
                captionCenters.Add(Vector2.zero);
                Insert(0, label);
            }
        }

        private void SynchronizeShipMarkers()
        {
            foreach (var id in ships.Keys.Where(id => !snapshot.Ships.ContainsKey(id)).ToArray())
            {
                ships[id].RemoveFromHierarchy(); ships.Remove(id);
                fromPositions.Remove(id); toPositions.Remove(id); displayedPositions.Remove(id);
            }
            var npcIndex = 0;
            var playerIndex = 0;
            foreach (var ship in snapshot.Ships.Values.OrderBy(ship => ship.Id, StringComparer.Ordinal))
            {
                var id = ship.Id;
                if (!ships.TryGetValue(id, out var button))
                {
                    button = new Button(() => ShipSelected?.Invoke(id)) { name = "ship_" + id };
                    button.AddToClassList("ship-marker");
                    var glyph = new MaritimeGlyph("ship");
                    glyph.AddToClassList("sl-map-ship-glyph");
                    button.Add(glyph);
                    var label = new Label { pickingMode = PickingMode.Ignore };
                    label.AddToClassList("sl-map-ship-label");
                    button.Add(label);
                    var badge = new Label("!") { pickingMode = PickingMode.Ignore, tooltip = "Route is waiting; inspect this ship for the current cause." };
                    badge.AddToClassList("sl-map-blocked-badge");
                    badge.AddToClassList("hidden");
                    button.Add(badge);
                    button.RegisterCallback<GeometryChangedEvent>(_ => ArrangeMarkers());
                    ships.Add(id, button); Add(button);
                }
                button.EnableInClassList("player-marker", ship.IsPlayer);
                button.EnableInClassList("npc-marker", !ship.IsPlayer);
                button.Q<Label>(className: "sl-map-ship-label").text = ship.IsPlayer
                    ? "P" + (++playerIndex).ToString(CultureInfo.InvariantCulture) : "N" + (++npcIndex).ToString(CultureInfo.InvariantCulture);
            }
        }

        private string Quantity(int quantity) => ((decimal)quantity / snapshot.QuantityScale).ToString("0.####", CultureInfo.InvariantCulture);

        private Vector2 ShipPosition(RegionalShipSnapshot ship)
        {
            if (ship.DockedPortId != null)
            {
                var port = snapshot.Ports[ship.DockedPortId];
                var docked = snapshot.Ships.Values.Where(value => value.DockedPortId == ship.DockedPortId)
                    .OrderBy(value => value.Id, StringComparer.Ordinal).Select(value => value.Id).ToArray();
                var slot = Array.IndexOf(docked, ship.Id);
                var markerWidth = docked.Max(id => ElementWidth(ships[id], 44));
                var markerHeight = docked.Max(id => ElementHeight(ships[id], 32));
                var groupWidth = docked.Length * (markerWidth + 6) - 6;
                var portPoint = PortPoint(port.Id);
                var vertical = ElementHeight(ports[port.Id], 48) / 2 + markerHeight / 2 + 8;
                var horizontal = ElementWidth(ports[port.Id], 132) / 2 + groupWidth / 2 + 8;
                var offset = new Vector2(0, vertical);
                // Prefer the historical berth below the town, but keep the entire group
                // clear of every town's hit target on dense charts with large text.
                foreach (var candidate in new[] { new Vector2(0, vertical), new Vector2(0, -vertical),
                    new Vector2(-horizontal, 0), new Vector2(horizontal, 0) })
                {
                    var center = portPoint + candidate;
                    var bounds = new Rect(center.x - groupWidth / 2, center.y - markerHeight / 2,
                        groupWidth, markerHeight);
                    if (bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > contentRect.width ||
                        bounds.yMax > contentRect.height) continue;
                    var overlapsTown = snapshot.Ports.Keys.Any(id =>
                    {
                        var point = PortPoint(id);
                        var width = ElementWidth(ports[id], 132) + 8;
                        var height = ElementHeight(ports[id], 48) + 8;
                        return bounds.Overlaps(new Rect(point.x - width / 2, point.y - height / 2, width, height));
                    });
                    if (overlapsTown) continue;
                    offset = candidate;
                    break;
                }
                return Viewport.ViewportToWorld(portPoint + offset +
                    new Vector2((slot - (docked.Length - 1) / 2f) * (markerWidth + 6), 0));
            }
            var origin = snapshot.Ports[ship.OriginPortId];
            var destination = snapshot.Ports[ship.DestinationPortId];
            var progress = (ship.VoyageDurationDays - ship.RemainingTravelDays) / (float)ship.VoyageDurationDays;
            return Vector2.Lerp(new Vector2(origin.X, origin.Y), new Vector2(destination.X, destination.Y), progress);
        }

        private void RefreshViewport()
        {
            if (snapshot == null) return;
            var minimum = new Vector2(snapshot.Ports.Values.Min(port => port.X), snapshot.Ports.Values.Min(port => port.Y));
            var maximum = new Vector2(snapshot.Ports.Values.Max(port => port.X), snapshot.Ports.Values.Max(port => port.Y));
            var width = float.IsNaN(contentRect.width) || float.IsInfinity(contentRect.width) || contentRect.width < 1 ? 1000 : contentRect.width;
            var height = float.IsNaN(contentRect.height) || float.IsInfinity(contentRect.height) || contentRect.height < 1 ? 700 : contentRect.height;
            Viewport = MapViewport.Fit(new Rect(minimum, maximum - minimum), new Rect(0, 0, width, height),
                new Vector4(PaddingX, PaddingTop, PaddingX, PaddingBottom));
        }

        private Vector2 ChartPoint(Vector2 position) => Viewport.WorldToViewport(position);

        private float PaddingX => ports.Count == 0 ? 80 : ports.Values.Max(port => ElementWidth(port, 132)) / 2 + 22;
        private float PaddingTop => ports.Count == 0 ? 56 : ports.Values.Max(port => ElementHeight(port, 48)) / 2 + 42;
        private float PaddingBottom => PaddingTop +
            (ships.Count == 0 ? 32 : ships.Values.Max(ship => ElementHeight(ship, 32))) + 8;
        private static float ElementWidth(VisualElement element, float fallback) =>
            float.IsNaN(element.resolvedStyle.width) || element.resolvedStyle.width < 1 ? fallback : element.resolvedStyle.width;
        private static float ElementHeight(VisualElement element, float fallback) =>
            float.IsNaN(element.resolvedStyle.height) || element.resolvedStyle.height < 1 ? fallback : element.resolvedStyle.height;

        private Vector2 PortPoint(string portId)
        {
            var port = snapshot.Ports[portId];
            return ChartPoint(new Vector2(port.X, port.Y));
        }

        private void ArrangeMarkers()
        {
            if (snapshot == null) return;
            RefreshViewport();
            foreach (var ship in snapshot.Ships.Values)
            {
                if (ship.DockedPortId != null) toPositions[ship.Id] = ShipPosition(ship);
                var chartPosition = Vector2.Lerp(fromPositions[ship.Id], toPositions[ship.Id], interpolation);
                displayedPositions[ship.Id] = chartPosition;
            }
            portLabelBounds.Clear();
            foreach (var id in snapshot.Ports.Keys)
            {
                var point = PortPoint(id);
                var size = new Vector2(ElementWidth(ports[id], 132), ElementHeight(ports[id], 48));
                portLabelBounds.Add(id, new Rect(point - size / 2, size));
            }
            var shipBounds = new List<Rect>();
            shipMarkerBounds.Clear();
            foreach (var ship in snapshot.Ships.Values.OrderByDescending(ship => ship.IsPlayer).ThenBy(ship => ship.Id, StringComparer.Ordinal))
            {
                var center = ChartPoint(displayedPositions[ship.Id]);
                var size = new Vector2(ElementWidth(ships[ship.Id], 44), ElementHeight(ships[ship.Id], 32));
                var bounds = PlacePortLabel(center, size, Viewport.ViewportBounds, shipBounds);
                shipMarkerBounds.Add(ship.Id, bounds);
                shipBounds.Add(bounds);
                ships[ship.Id].style.translate = new Translate(bounds.x, bounds.y);
            }
            foreach (var id in snapshot.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal))
            {
                var obstacles = shipBounds.Concat(portLabelBounds.Where(pair => pair.Key != id).Select(pair => pair.Value)).ToArray();
                var bounds = PlacePortLabel(PortPoint(id), portLabelBounds[id].size, Viewport.ViewportBounds, obstacles);
                portLabelBounds[id] = bounds;
                ports[id].style.translate = new Translate(bounds.x, bounds.y);
            }
            var occupiedCaptions = portLabelBounds.Values.Concat(shipBounds).Select(bounds =>
                new Rect(bounds.x - 4, bounds.y - 4, bounds.width + 8, bounds.height + 8)).ToList();
            for (var i = 0; i < distanceLabels.Count; i++)
            {
                var route = snapshot.Routes[i];
                if (!RouteIsVisible(route)) continue;
                var start = PortPoint(route.OriginPortId);
                var end = PortPoint(route.DestinationPortId);
                var direction = (end - start).normalized;
                var normal = new Vector2(-direction.y, direction.x);
                var point = (start + end) / 2 + normal * 12;
                var labelWidth = ElementWidth(distanceLabels[i], 120);
                var labelHeight = ElementHeight(distanceLabels[i], 24);
                var placed = false;
                // Bounded presentation-only search around the actual sea connection.
                foreach (var offset in new[] { 12f, -12f, 36f, -36f, 60f, -60f, 84f, -84f, 108f, -108f, 132f, -132f, 156f, -156f })
                {
                    foreach (var fraction in new[] { 0.5f, 0.35f, 0.65f, 0.25f, 0.75f })
                    {
                        var candidate = Vector2.Lerp(start, end, fraction) + normal * offset;
                        var bounds = new Rect(candidate.x - labelWidth / 2 - 4, candidate.y - labelHeight / 2 - 4,
                            labelWidth + 8, labelHeight + 8);
                        if (bounds.xMin < 0 || bounds.yMin < 0 || bounds.xMax > contentRect.width ||
                            bounds.yMax > contentRect.height || occupiedCaptions.Any(occupied => occupied.Overlaps(bounds))) continue;
                        point = candidate;
                        placed = true;
                        break;
                    }
                    if (placed) break;
                }
                if (!placed)
                {
                    // Dense charts may have no space beside the connection. Search the whole
                    // chart on a fixed grid, keeping a leader line to the authoritative route.
                    // This is bounded presentation work, never a change to geographic positions.
                    var bestDistance = float.PositiveInfinity;
                    for (var row = 0; row < 24; row++)
                    for (var column = 0; column < 32; column++)
                    {
                        var candidate = new Vector2(labelWidth / 2 + 6 + column *
                            Mathf.Max(0, contentRect.width - labelWidth - 12) / 31,
                            labelHeight / 2 + 6 + row * Mathf.Max(0, contentRect.height - labelHeight - 12) / 23);
                        var bounds = new Rect(candidate.x - labelWidth / 2 - 4, candidate.y - labelHeight / 2 - 4,
                            labelWidth + 8, labelHeight + 8);
                        if (bounds.xMax > contentRect.width || bounds.yMax > contentRect.height ||
                            occupiedCaptions.Any(occupied => occupied.Overlaps(bounds))) continue;
                        var distance = (candidate - (start + end) / 2).sqrMagnitude;
                        if (distance >= bestDistance) continue;
                        point = candidate;
                        bestDistance = distance;
                    }
                }
                occupiedCaptions.Add(new Rect(point.x - labelWidth / 2 - 4, point.y - labelHeight / 2 - 4,
                    labelWidth + 8, labelHeight + 8));
                captionCenters[i] = point;
                distanceLabels[i].style.translate = new Translate(point.x - labelWidth / 2, point.y - labelHeight / 2);
            }
            MarkDirtyRepaint();
        }

        public static Rect PlacePortLabel(Vector2 anchor, Vector2 size, Rect viewport, IReadOnlyList<Rect> obstacles)
        {
            var best = new Rect(anchor - size / 2, size);
            var bestDistance = float.PositiveInfinity;
            void Consider(Vector2 center)
            {
                center.x = Mathf.Clamp(center.x, viewport.xMin + size.x / 2 + 4, viewport.xMax - size.x / 2 - 4);
                center.y = Mathf.Clamp(center.y, viewport.yMin + size.y / 2 + 4, viewport.yMax - size.y / 2 - 4);
                var bounds = new Rect(center - size / 2, size);
                var padded = new Rect(bounds.x - 4, bounds.y - 4, bounds.width + 8, bounds.height + 8);
                if (padded.xMin < viewport.xMin || padded.yMin < viewport.yMin || padded.xMax > viewport.xMax || padded.yMax > viewport.yMax ||
                    obstacles.Any(obstacle => obstacle.Overlaps(padded))) return;
                var distance = (center - anchor).sqrMagnitude;
                if (distance >= bestDistance) return;
                best = bounds; bestDistance = distance;
            }
            Consider(anchor);
            if (bestDistance == 0) return best;
            foreach (var obstacle in obstacles)
            {
                var left = obstacle.xMin - size.x / 2 - 6;
                var right = obstacle.xMax + size.x / 2 + 6;
                var above = obstacle.yMin - size.y / 2 - 6;
                var below = obstacle.yMax + size.y / 2 + 6;
                Consider(new Vector2(left, anchor.y)); Consider(new Vector2(right, anchor.y));
                Consider(new Vector2(anchor.x, above)); Consider(new Vector2(anchor.x, below));
                Consider(new Vector2(left, above)); Consider(new Vector2(right, above));
                Consider(new Vector2(left, below)); Consider(new Vector2(right, below));
            }
            if (float.IsPositiveInfinity(bestDistance))
                for (var row = 0; row < 24; row++)
                for (var column = 0; column < 32; column++)
                    Consider(new Vector2(viewport.xMin + size.x / 2 + 4 + column * Mathf.Max(0, viewport.width - size.x - 8) / 31,
                        viewport.yMin + size.y / 2 + 4 + row * Mathf.Max(0, viewport.height - size.y - 8) / 23));
            return best;
        }

        private void DrawChart(MeshGenerationContext context)
        {
            if (snapshot == null || contentRect.width < 1 || contentRect.height < 1) return;
            var painter = context.painter2D;
            painter.fillGradient = FillGradient.MakeLinearGradient(new Color(0.063f, 0.23f, 0.27f),
                new Color(0.12f, 0.34f, 0.37f), Vector2.zero, new Vector2(contentRect.width, contentRect.height), AddressMode.Clamp);
            painter.BeginPath();
            painter.MoveTo(Vector2.zero); painter.LineTo(new Vector2(contentRect.width, 0));
            painter.LineTo(new Vector2(contentRect.width, contentRect.height)); painter.LineTo(new Vector2(0, contentRect.height));
            painter.ClosePath(); painter.Fill();
            painter.fillGradient = default;
            painter.lineWidth = 1;
            painter.strokeColor = new Color(0.51f, 0.73f, 0.71f, 0.12f);
            for (var row = 0; row < 10; row++)
            {
                var y = 25 + row * contentRect.height / 10;
                for (var column = 0; column < 14; column++)
                {
                    var x = 12 + column * contentRect.width / 14 + (row % 2) * 15;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, y));
                    painter.BezierCurveTo(new Vector2(x + 8, y - 3), new Vector2(x + 16, y + 3), new Vector2(x + 26, y));
                    painter.Stroke();
                }
            }
            DrawCompass(painter, new Vector2(contentRect.width - 38, 40), 24);
            painter.lineWidth = 1.5f;
            foreach (var route in snapshot.Routes)
            {
                if (!RouteIsVisible(route)) continue;
                painter.strokeColor = RouteIsHighlighted(route) ? new Color(0.91f, 0.76f, 0.45f, 0.9f) :
                    new Color(0.62f, 0.82f, 0.79f, 0.65f);
                painter.lineWidth = RouteIsHighlighted(route) ? 2.5f : 1.5f;
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
            if (highlightedRoute.Count > 2)
            {
                painter.strokeColor = new Color(0.96f, 0.82f, 0.54f, 0.95f);
                painter.lineWidth = 2.5f;
                for (var index = 0; index < highlightedRoute.Count; index++)
                {
                    var from = PortPoint(highlightedRoute[index]);
                    var to = PortPoint(highlightedRoute[(index + 1) % highlightedRoute.Count]);
                    var direction = (to - from).normalized;
                    var normal = new Vector2(-direction.y, direction.x);
                    var tip = Vector2.Lerp(from, to, 0.66f);
                    painter.BeginPath(); painter.MoveTo(tip - direction * 10 + normal * 5);
                    painter.LineTo(tip); painter.LineTo(tip - direction * 10 - normal * 5); painter.Stroke();
                }
            }
            painter.lineWidth = 1;
            painter.strokeColor = new Color(0.65f, 0.79f, 0.79f, 0.5f);
            for (var index = 0; index < captionCenters.Count; index++)
            {
                var route = snapshot.Routes[index];
                if (!RouteIsVisible(route)) continue;
                var midpoint = (PortPoint(route.OriginPortId) + PortPoint(route.DestinationPortId)) / 2;
                if ((captionCenters[index] - midpoint).sqrMagnitude < 36 * 36) continue;
                painter.BeginPath();
                painter.MoveTo(midpoint);
                painter.LineTo(captionCenters[index]);
                painter.Stroke();
            }
            var islandColors = new[]
            {
                new Color(0.37f, 0.49f, 0.34f), new Color(0.29f, 0.44f, 0.34f),
                new Color(0.46f, 0.49f, 0.32f), new Color(0.34f, 0.48f, 0.37f)
            };
            var islandIndex = 0;
            foreach (var port in snapshot.Ports.Values.OrderBy(port => port.Id, StringComparer.Ordinal))
            {
                var point = PortPoint(port.Id);
                var scale = IslandDecorationScale(port.Id);
                var selected = selectedShipId == null && port.Id == selectedPortId;
                painter.fillColor = new Color(0.28f, 0.57f, 0.55f, 0.28f);
                painter.strokeColor = new Color(0.51f, 0.73f, 0.65f, 0.3f);
                painter.lineWidth = 1;
                IslandPath(painter, point, scale * 1.20f, islandIndex); painter.Fill(); painter.Stroke();
                painter.fillColor = new Color(0.71f, 0.67f, 0.44f);
                painter.strokeColor = selected ? new Color(0.98f, 0.86f, 0.57f) : new Color(0.80f, 0.76f, 0.55f);
                painter.lineWidth = selected ? 3 : 1.5f;
                IslandPath(painter, point, scale * 1.06f, islandIndex); painter.Fill(); painter.Stroke();
                painter.fillColor = islandColors[islandIndex % islandColors.Length];
                IslandPath(painter, point, scale, islandIndex); painter.Fill();
                painter.strokeColor = new Color(0.73f, 0.77f, 0.49f, 0.6f);
                painter.lineWidth = 1;
                IslandPath(painter, point, scale * 0.82f, islandIndex); painter.Stroke();
                // Authored chart hatching and small role silhouettes dress the town's real coordinate.
                for (var hatch = 0; hatch < 5; hatch++)
                {
                    var origin = point + new Vector2(-80 + hatch * 9, -22 - hatch * 3) * scale;
                    painter.BeginPath(); painter.MoveTo(origin); painter.LineTo(origin + new Vector2(8, -9) * scale); painter.Stroke();
                }
                var role = ShortRole(port);
                var symbol = role == "Forest" ? "forest" : role == "Food source" ? "farm" :
                    role == "Processing" ? "mill" : "lighthouse";
                MaritimeGlyph.Draw(painter, symbol, new Rect(point.x - 20 * scale,
                    point.y - ElementHeight(ports[port.Id], 48) / 2 - 39 * scale, 40 * scale, 38 * scale));
                islandIndex++;
            }
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.91f, 0.82f, 0.62f, 0.9f);
            foreach (var pair in portLabelBounds)
            {
                var anchor = PortPoint(pair.Key);
                if ((pair.Value.center - anchor).sqrMagnitude < 1) continue;
                var edge = new Vector2(Mathf.Clamp(anchor.x, pair.Value.xMin, pair.Value.xMax),
                    Mathf.Clamp(anchor.y, pair.Value.yMin, pair.Value.yMax));
                painter.BeginPath(); painter.MoveTo(anchor); painter.LineTo(edge); painter.Stroke();
                painter.BeginPath(); painter.Arc(anchor, 3, Angle.Degrees(0), Angle.Degrees(360)); painter.Stroke();
            }
            foreach (var pair in shipMarkerBounds)
            {
                var anchor = ChartPoint(displayedPositions[pair.Key]);
                if ((pair.Value.center - anchor).sqrMagnitude < 1) continue;
                var edge = new Vector2(Mathf.Clamp(anchor.x, pair.Value.xMin, pair.Value.xMax),
                    Mathf.Clamp(anchor.y, pair.Value.yMin, pair.Value.yMax));
                painter.strokeColor = snapshot.Ships[pair.Key].IsPlayer ? new Color(0.98f, 0.86f, 0.57f) : new Color(0.70f, 0.87f, 0.84f);
                painter.BeginPath(); painter.MoveTo(anchor); painter.LineTo(edge); painter.Stroke();
                painter.BeginPath(); painter.Arc(anchor, 3, Angle.Degrees(0), Angle.Degrees(360)); painter.Stroke();
            }
        }

        private static readonly Vector2[] Coast =
        {
            new Vector2(-112, -4), new Vector2(-94, -27), new Vector2(-79, -45), new Vector2(-51, -43),
            new Vector2(-31, -62), new Vector2(-3, -69), new Vector2(24, -53), new Vector2(52, -58),
            new Vector2(71, -37), new Vector2(100, -26), new Vector2(109, -3), new Vector2(88, 18),
            new Vector2(64, 33), new Vector2(32, 29), new Vector2(9, 42), new Vector2(-20, 35),
            new Vector2(-42, 45), new Vector2(-64, 29), new Vector2(-92, 24)
        };

        private float IslandDecorationScale(string portId)
        {
            var point = PortPoint(portId);
            var scale = Mathf.Min(Mathf.Clamp(contentRect.width / 1000, 0.8f, 1.2f), contentRect.height / 450);
            // The outer surf contour includes the widest authored coast variant. Decorative
            // extents fit the chart and nearby islands without changing any world positions.
            scale = Mathf.Min(scale, (point.x - 4) / 135, (contentRect.width - point.x - 4) / 135,
                (point.y - 4) / 96, (contentRect.height - point.y - 4) / 63,
                (point.y - ElementHeight(ports[portId], 48) / 2 - 4) / 39);
            foreach (var otherId in snapshot.Ports.Keys)
            {
                if (otherId == portId) continue;
                var separation = PortPoint(otherId) - point;
                scale = Mathf.Min(scale, Mathf.Max(Mathf.Abs(separation.x) / 282, Mathf.Abs(separation.y) / 171));
            }
            return Mathf.Max(0, scale);
        }

        private static void IslandPath(Painter2D painter, Vector2 center, float scale, int variant)
        {
            painter.BeginPath();
            for (var index = 0; index < Coast.Length; index++)
            {
                var vertex = Coast[index];
                var x = vertex.x * (variant % 2 == 0 ? 1 : -1);
                var y = vertex.y * (1 + variant * 0.05f);
                var point = center + new Vector2(x, y) * scale;
                if (index == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.ClosePath();
        }

        private static void DrawCompass(Painter2D painter, Vector2 center, float radius)
        {
            painter.strokeColor = new Color(0.77f, 0.66f, 0.44f, 0.36f);
            painter.lineWidth = 1;
            painter.BeginPath(); painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360)); painter.Stroke();
            for (var index = 0; index < 8; index++)
            {
                var angle = index * Mathf.PI / 4;
                var direction = new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle));
                var normal = new Vector2(-direction.y, direction.x);
                var length = index % 2 == 0 ? radius : radius * 0.7f;
                painter.fillColor = index % 2 == 0 ? new Color(0.77f, 0.66f, 0.44f, 0.30f) : new Color(0.4f, 0.64f, 0.63f, 0.3f);
                painter.BeginPath(); painter.MoveTo(center + direction * length);
                painter.LineTo(center + normal * 3); painter.LineTo(center - direction * 5);
                painter.ClosePath(); painter.Fill(); painter.Stroke();
            }
        }
    }
}
