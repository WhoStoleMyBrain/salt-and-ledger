using System;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private bool approvedShellBuilt;
        private VisualElement approvedContextOverride;
        private RegionalWorldSnapshot approvedActivitySnapshot;

        private void BuildApprovedShell()
        {
            var root = Element<VisualElement>("archipelagoLab");
            root.AddToClassList("ux-shell");
            var main = Element<VisualElement>("mainLayout");
            var mapSide = Root.Q(className: "map-side");
            mapSide.name = "approvedMapWorkspace";
            var deck = Element<VisualElement>("commandDeck");
            main.Add(deck);
            deck.AddToClassList("ux-inspector");
            var body = Element<VisualElement>("deckBody");
            var scroll = new ScrollView { name = "approvedInspectorScroll" };
            scroll.AddToClassList("ux-scroll");
            deck.Insert(0, scroll);
            scroll.Add(body);
            scroll.Add(Element<Label>("deckReason"));
            scroll.Add(Element<VisualElement>("voyageBar"));
            var content = Panel(body, "approvedContextFacts", "ux-context-facts");
            body.Insert(body.IndexOf(Element<VisualElement>("deckActions")), content);
            var facts = new Foldout { name = "approvedContextDetails", text = "Accounts & current readiness", value = false };
            body.Insert(body.IndexOf(Element<VisualElement>("deckActions")), facts);
            facts.Add(Element<VisualElement>("deckFacts"));
            var overrideHost = Panel(deck, "approvedContextOverride", "ux-context-override hidden");
            var activity = Panel(mapSide, "approvedActivity", "ux-activity");
            var activityHeading = Panel(activity, null, "ux-activity-heading");
            Text(activityHeading, null, "ACTIVITY & ATTENTION", "ux-eyebrow");
            Button(activityHeading, "approvedActivityLog", "View log →", () => OpenSecondary(ArchipelagoScreen.Inspector));
            Panel(activity, "approvedActivityCards", "ux-activity-cards");
            var controls = Root.Q(className: "map-footer");
            controls.AddToClassList("ux-map-tools");
            Button(controls, "fitMapButton", "Fit map", Map.FitToWorld);
            Button(controls, "clearSelectionButton", "Clear selection", () =>
            {
                Presentation.HasSelection = false;
                observedPort = Session.SelectedPortId; observedShip = Session.SelectedShipId;
                Refresh();
            });
            // Preserve the vessel picker as a real accessible detail control, including co-located NPCs.
            Element<VisualElement>("shipContext").Insert(0, Element<DropdownField>("shipPicker"));
            Element<VisualElement>("shipContext").Insert(1, Element<Button>("playerShipButton"));
            Element<VisualElement>("contextScroll").Add(Element<Button>("mapDetailsButton"));
            Element<VisualElement>("contextScroll").Add(Element<Button>("resetButton"));
            activityHeading.Add(Element<Button>("noticesButton"));
            var nav = Element<VisualElement>("commandModeNav");
            foreach (var pair in modeButtons)
            {
                pair.Value.Clear(); pair.Value.text = "";
                Text(pair.Value, null, ((int)pair.Key + 1).ToString("00", CultureInfo.InvariantCulture), "ux-nav-number");
                Text(pair.Value, null, pair.Key == CommandMode.Trade ? "Harbor" : pair.Key == CommandMode.Objectives ? "Goals" : pair.Key.ToString(), "ux-nav-label");
            }
            var speed = Panel(Element<VisualElement>("topBar"), "approvedSpeeds", "ux-speeds");
            Element<DropdownField>("speedDropdown").AddToClassList("hidden");
            foreach (var value in new[] { 1, 3, 10 })
            {
                var captured = value;
                Button(speed, "approvedSpeed" + value, value + "×", () =>
                {
                    SetIndex("speedDropdown", Array.IndexOf(new[] { 1, 3, 10 }, captured));
                    Run(() => Session.SetPlaybackSpeed(captured));
                });
            }
            Element<Label>("playbackLabel").AddToClassList("hidden");
            // The inset workspace lives beside the map rather than under a disabled ancestor.
            var frame = Root.Q(className: "map-frame");
            Text(frame, "approvedChartTitle", "ARCHIPELAGO / WORLD MAP", "ux-chart-title");
            frame.Add(Element<VisualElement>("tradeBackdrop"));
            frame.Add(Element<VisualElement>("industryBackdrop"));
            BuildApprovedTrading();
            approvedShellBuilt = true;
        }

        private void SetApprovedContext(VisualElement content)
        {
            approvedContextOverride = content;
            var host = Root.Q("approvedContextOverride");
            if (host == null) return;
            host.Clear(); if (content != null) host.Add(content);
            RefreshApprovedContext();
        }

        private void ClearApprovedContext() => SetApprovedContext(null);

        private void RefreshApprovedContext()
        {
            if (!approvedShellBuilt) return;
            var custom = approvedContextOverride != null && IndustryIsOpen;
            Element<ScrollView>("approvedInspectorScroll").EnableInClassList("hidden", custom);
            Element<VisualElement>("approvedContextOverride").EnableInClassList("hidden", !custom);
            Element<Button>("playButton").EnableInClassList("hidden", Session.IsPlaying);
            Element<Button>("pauseButton").EnableInClassList("hidden", !Session.IsPlaying);
            Element<Button>("playButton").text = "Ⅱ Paused";
            Element<Button>("pauseButton").text = "Ⅱ Pause";
            foreach (var speed in new[] { 1, 3, 10 }) Root.Q<Button>("approvedSpeed" + speed)?.EnableInClassList("sl-selected",
                Element<DropdownField>("speedDropdown").index == Array.IndexOf(new[] { 1, 3, 10 }, speed));
            var cards = Element<VisualElement>("approvedContextFacts");
            cards.Clear();
            if (Presentation.HasSelection)
            {
                if (Session.SelectedShipId == null) BuildApprovedTownFacts(cards, Session.Snapshot.Ports[Session.SelectedPortId]);
                else BuildApprovedShipFacts(cards, Session.Snapshot.Ships[Session.SelectedShipId]);
            }
            if (!ReferenceEquals(approvedActivitySnapshot, Session.Snapshot))
            {
                approvedActivitySnapshot = Session.Snapshot;
                RefreshApprovedActivity();
            }
        }

        private void BuildApprovedTownFacts(VisualElement parent, RegionalPortSnapshot port)
        {
            Text(parent, null, "PUBLIC PRODUCTION", "ux-eyebrow");
            var industries = Panel(parent, "approvedPublicIndustries", "ux-tiles");
            foreach (var facility in port.Facilities)
            {
                var building = Session.Content.GetBuilding(facility.Configuration.Instance.BuildingId);
                var card = Button(industries, "approvedPublic_" + facility.Configuration.Instance.Id, "", () =>
                    { SelectCommandPort(port.Id); SwitchCommandMode(CommandMode.Economy); }, "ux-production-tile");
                card.Add(new MaritimeGlyph(building.Id));
                Text(card, null, building.Name, "ux-small");
                card.tooltip = "Public production. Inspect current inputs, wages and readiness in Economy.";
            }
            var people = Panel(parent, null, "ux-tiles");
            Text(people, null, "POPULATION\n" + (port.City == null ? "—" : port.City.Population.ToString(CultureInfo.InvariantCulture)), "ux-stat");
            Text(people, null, "PROSPERITY\n" + (port.City == null ? "—" : port.City.Prosperity + "/100"), "ux-stat");
            Text(parent, null, "HOUSEHOLD NEEDS", "ux-eyebrow");
            foreach (var id in new[] { "bread", "furniture" })
            {
                var signal = EconomicSignals.CommodityFlow(Session.Snapshot, Session.Content, port.Id, id, null);
                var row = Panel(parent, "approvedNeed_" + id, "ux-need");
                row.Add(new MaritimeGlyph(id));
                Text(row, null, Session.Content.GetGood(id).Name, "ux-grow");
                Text(row, null, Q(signal.ConsumptionNeedQuantity) + " / 12d", "ux-metric");
                row.tooltip = "Need at current population and eligibility, carrying exact saved demand fractions. A need is not a completed purchase.";
            }
        }

        private void BuildApprovedShipFacts(VisualElement parent, RegionalShipSnapshot ship)
        {
            Text(parent, null, "CURRENT VOYAGE", "ux-eyebrow");
            Text(parent, null, ship.DockedPortId == null ? PortName(ship.OriginPortId) + " → " + PortName(ship.DestinationPortId) +
                "\n" + ship.RemainingTravelDays + " days remaining" : "Docked · " + PortName(ship.DockedPortId), "ux-state");
            Text(parent, null, "CARGO", "ux-eyebrow");
            var fill = new ProgressBar { lowValue = 0, highValue = ship.CargoCapacity, value = ship.CargoUnits,
                title = Q(ship.CargoUnits) + " / " + Q(ship.CargoCapacity) + " used" };
            parent.Add(fill);
            var cargo = Panel(parent, "approvedShipCargo", "ux-tiles");
            foreach (var id in goodIds.Where(id => ship.Cargo[id] > 0))
            {
                var tile = Panel(cargo, null, "ux-cargo-tile"); tile.Add(new MaritimeGlyph(id));
                Text(tile, null, Q(ship.Cargo[id]) + " " + Session.Content.GetGood(id).Name, "ux-small");
            }
            Text(parent, null, Q(ship.CargoCapacity - ship.CargoUnits) + " capacity available", "ux-muted");
            Text(parent, null, "AUTOMATION", "ux-eyebrow");
            Text(parent, null, ship.IsPlayer ? ship.AutomationEnabled ? "Route active" : "Manual control" : "NPC merchant · read only", "ux-state");
            if (ship.Itinerary != null) Text(parent, null, string.Join(" → ", ship.Itinerary.Stops.Select(s => PortName(s.PortId))), "ux-copy");
            Text(parent, null, "NEXT ORDER", "ux-eyebrow");
            Text(parent, null, ShipInstruction(ship), "ux-copy");
        }

        private void RefreshApprovedActivity()
        {
            var cards = Element<VisualElement>("approvedActivityCards"); cards.Clear();
            var problem = Session.Snapshot.Ports.Values.OrderBy(p => p.Id, StringComparer.Ordinal)
                .Select(p => new { Port = p, Need = EconomicSignals.NextEssentialNeed(Session.Snapshot, p.Id) })
                .FirstOrDefault(p => p.Need.StockShortfall > 0 || p.Need.AffordabilityShortfall > 0);
            if (problem != null)
            {
                var card = Button(cards, "approvedFoodAlert", "", () => { SelectCommandPort(problem.Port.Id); SwitchCommandMode(CommandMode.Economy); }, "ux-activity-card ux-warning");
                Text(card, null, "FOOD · " + problem.Port.Name, "ux-eyebrow");
                Text(card, null, problem.Need.StockShortfall > 0 ? "Bread stock short by " + Q(problem.Need.StockShortfall) : "Household purchasing power short", "ux-copy");
            }
            var ship = Session.Player;
            var fleet = Button(cards, "approvedFleetAlert", "", () => { SelectCommandShip(ship.Id); SwitchCommandMode(CommandMode.Fleet); }, "ux-activity-card");
            Text(fleet, null, "FLEET · " + ship.Name, "ux-eyebrow");
            Text(fleet, null, ship.DockedPortId == null ? "To " + PortName(ship.DestinationPortId) + " · " + ship.RemainingTravelDays + "d" :
                "Docked · " + PortName(ship.DockedPortId), "ux-copy");
            var goal = Button(cards, "approvedGoalAlert", "", OpenObjectives, "ux-activity-card");
            Text(goal, null, "COMMISSION", "ux-eyebrow"); Text(goal, null, ObjectiveText(), "ux-copy");
        }
    }
}
