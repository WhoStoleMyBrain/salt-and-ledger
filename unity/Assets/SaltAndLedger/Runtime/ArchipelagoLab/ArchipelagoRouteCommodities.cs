using System;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private static readonly CargoItineraryCommandKind[] ItineraryKinds =
        {
            CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryCommandKind.DeliverContract, CargoItineraryCommandKind.SellToMarket,
            CargoItineraryCommandKind.LoadFromCompanyTownStock, CargoItineraryCommandKind.BuyFromMarket
        };

        private static readonly CargoItineraryCommandKind[] RouteGridKinds =
        {
            CargoItineraryCommandKind.SellToMarket, CargoItineraryCommandKind.BuyFromMarket,
            CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryCommandKind.LoadFromCompanyTownStock
        };

        private void RenderSelectedItineraryStop()
        {
            var parent = Element<VisualElement>("itineraryStopEditor"); parent.Clear();
            var index = selectedItineraryStop; var stop = ActiveItineraryDraft.Stops[index];
            var ownership = Panel(parent, null, "route-v3-grid-groups");
            Text(ownership, null, "COMMODITY", "route-v3-good-heading");
            Text(ownership, null, "TOWN MARKET\nPaid trades", "route-v3-owner-heading");
            Text(ownership, null, "COMPANY WAREHOUSE\nNo payment", "route-v3-owner-heading");
            var header = Panel(parent, null, "itinerary-grid-row route-v3-grid-heading");
            Text(header, null, "", "itinerary-grid-good");
            foreach (var kind in RouteGridKinds)
            {
                var label = Text(header, null, (kind == CargoItineraryCommandKind.SellToMarket || kind == CargoItineraryCommandKind.UnloadToCompanyTownStock ? "← " : "") +
                    ItineraryVerb(kind) + (kind == CargoItineraryCommandKind.BuyFromMarket || kind == CargoItineraryCommandKind.LoadFromCompanyTownStock ? " →" : ""), "itinerary-grid-column");
                label.tooltip = ItineraryFlowDescription(kind);
            }
            foreach (var goodId in new[] { "bread", "flour", "logs", "grain", "planks", "furniture", "silver" }.Where(goodIds.Contains))
            {
                var row = Panel(parent, "itineraryCommodity" + index + "_" + goodId, "itinerary-grid-row route-v3-good-row");
                var good = Panel(row, null, "itinerary-grid-good");
                var icon = new MaritimeGlyph(goodId); icon.AddToClassList("sl-glyph-small"); good.Add(icon);
                Text(good, null, Session.Content.GetGood(goodId).Name, "itinerary-good-name");
                foreach (var kind in RouteGridKinds)
                {
                    var selectedGood = goodId; var selectedKind = kind;
                    Button(row, ItineraryCellName(index, goodId, kind), "+", () =>
                    {
                        var matches = stop.Actions.Select((action, actionIndex) => new { action, actionIndex })
                            .Where(item => item.action.GoodId == selectedGood && item.action.Kind == selectedKind).ToArray();
                        if (matches.Length > 1)
                        {
                            stop.AdvancedOpen = true; RenderSelectedItineraryStop(); RefreshRoutePlannerStatus();
                        }
                        else BeginApprovedRouteAction(matches.Length == 0 ? -1 : matches[0].actionIndex, selectedGood, selectedKind);
                    }, "itinerary-quantity route-v3-grid-cell");
                }
            }
            Text(parent, "itineraryOrderNote", "", "route-v3-muted");
            Button(parent, "itineraryAddContract" + index, "+ Contract delivery", () =>
                BeginApprovedRouteAction(-1, kind: CargoItineraryCommandKind.DeliverContract))
                .SetEnabled(routeEditingAction == null && stop.Actions.Count < 8 && ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count) < 24 &&
                    Session.Snapshot.Opportunities != null && Session.Snapshot.Opportunities.Entries.Any(item => !item.IsProject && item.PortId == stop.PortId &&
                        (item.Status == OpportunityStatus.Available || item.Status == OpportunityStatus.Active)));
            var advanced = new Foldout { name = "itineraryAdvanced" + index, text = "ORDER SEQUENCE & LIMITS", value = stop.AdvancedOpen };
            advanced.AddToClassList("route-v3-sequence"); parent.Add(advanced);
            advanced.RegisterValueChangedCallback(change =>
            {
                if (change.target != advanced) return;
                if (!change.newValue && routeEditingAction != null) { advanced.SetValueWithoutNotify(true); return; }
                stop.AdvancedOpen = change.newValue;
            });
            Panel(advanced, "itineraryOrderedActions", "route-v3-ordered-actions");
            RenderItineraryAdvancedActions();
            Button(advanced, "itineraryDefaultOrder" + index, "Use Unload / Deliver / Sell / Load / Buy order", () =>
            { SortItineraryActions(stop); stop.PreserveOrder = false; RebuildChangedItinerary(); }).SetEnabled(routeEditingAction == null);
            RefreshItineraryGrid();
        }

        private void RenderItineraryAdvancedActions()
        {
            var parent = Element<VisualElement>("itineraryOrderedActions"); parent.Clear();
            var stop = ActiveItineraryDraft.Stops[selectedItineraryStop];
            for (var i = 0; i < stop.Actions.Count; i++) RenderItineraryAction(parent, selectedItineraryStop, i, stop.Actions[i]);
            if (routeEditingAction != null && routeEditingIndex < 0) RenderApprovedInlineEditor(parent, selectedItineraryStop + "_-1");
            Button(parent, "itineraryAddAction" + selectedItineraryStop, "+ Add an order", () => BeginApprovedRouteAction(-1))
                .SetEnabled(routeEditingAction == null && stop.Actions.Count < 8 && ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count) < 24);
        }

        private static void SortItineraryActions(ItineraryStopDraft stop)
        {
            var sorted = stop.Actions.OrderBy(action => Array.IndexOf(ItineraryKinds, action.Kind))
                .ThenBy(action => action.GoodId, StringComparer.Ordinal).ToArray();
            stop.Actions.Clear(); stop.Actions.AddRange(sorted);
        }

        private void RefreshItineraryGrid()
        {
            var stop = ActiveItineraryDraft.Stops[selectedItineraryStop];
            var total = ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count);
            Element<Button>("itineraryAddStop").SetEnabled(ActiveItineraryDraft.Stops.Count < 4 && total < 24 && routeEditingAction == null);
            foreach (var goodId in goodIds)
                foreach (var kind in RouteGridKinds)
                {
                    var cell = Root.Q<Button>(ItineraryCellName(selectedItineraryStop, goodId, kind));
                    if (cell == null) continue;
                    var matches = stop.Actions.Where(action => action.Kind == kind && action.GoodId == goodId).ToList();
                    cell.text = matches.Count > 1 ? "Ordered…" : matches.Count == 0 ? "+" : matches[0].Maximum;
                    cell.SetEnabled(routeEditingAction == null && (matches.Count > 0 || stop.Actions.Count < 8 && total < 24));
                    cell.EnableInClassList("itinerary-cell-active", matches.Count != 0);
                    cell.tooltip = Session.Content.GetGood(goodId).Name + " · " + ItineraryFlowDescription(kind) +
                        (matches.Count > 1 ? ". Repeated instructions remain separate in Order sequence & limits." : ". Edit maximum whole units per visit and optional limits.");
                }
            SetText("itineraryOrderNote", stop.Actions.Count + "/8 instructions · " + (stop.PreserveOrder ? "Custom execution order" : "Default: Unload → Deliver → Sell → Load → Buy"));
        }

        private bool MissingItineraryWarehouse(ItineraryStopDraft stop) =>
            stop.Actions.Any(action => action.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock || action.Kind == CargoItineraryCommandKind.UnloadToCompanyTownStock) &&
            !Session.Snapshot.CompanyStockpiles.Values.Any(stock => stock.OwnerId == Session.Player.OwnerId && stock.PortId == stop.PortId);

        private string ItineraryStopState(int index)
        {
            var ship = Session.Player;
            var progress = ship.ItineraryProgress;
            if (ActiveItineraryDraft.Changed || ship.Itinerary == null) return "Draft";
            if (progress.StopIndex != index) return "Planned";
            if (ship.DockedPortId == null) return ship.DestinationPortId == ship.Itinerary.Stops[index].PortId
                ? "Incoming · " + ship.RemainingTravelDays + "d" : "Paused · off route";
            if (ship.DockedPortId != ship.Itinerary.Stops[index].PortId) return "Paused · off route";
            var count = ship.Itinerary.Stops[index].Commands.Count;
            var completion = progress.ActionIndex + "/" + count + " done";
            if (progress.ActionIndex == count) return "Stop complete · " + completion;
            if (!ship.AutomationEnabled) return "Paused · " + completion;
            var current = CargoItineraryEvaluator.Evaluate(Session.Snapshot, ship.Id);
            if (current.Kind == CargoItineraryDecisionKind.Wait) return "Waiting · " + completion;
            if (progress.LastDecision == CargoItineraryDecisionKind.Skip) return "Skipped last · " + completion;
            return progress.ActionIndex > 0 ? "Partly done · " + completion : "Ready · " + completion;
        }

        private string ItineraryNextInstruction()
        {
            var ship = Session.Player;
            var progress = ship.ItineraryProgress;
            var stop = ship.Itinerary.Stops[progress.StopIndex];
            if (progress.ActionIndex == stop.Commands.Count)
                return "sail → " + PortName(ship.Itinerary.Stops[(progress.StopIndex + 1) % ship.Itinerary.Stops.Count].PortId);
            var command = stop.Commands[progress.ActionIndex];
            return ItineraryVerb(command.Kind) + " ≤" + Q(command.MaximumQuantity) + " " + Session.Content.GetGood(command.GoodId).Name + " · " + PortName(stop.PortId);
        }

        private static string ItineraryCellName(int stop, string good, CargoItineraryCommandKind kind) =>
            "itineraryQuantity" + stop + "_" + good + "_" + (int)kind;

        private static string ItineraryFlow(CargoItineraryCommandKind kind)
        {
            switch (kind)
            {
                case CargoItineraryCommandKind.BuyFromMarket: return "Market → Ship";
                case CargoItineraryCommandKind.SellToMarket: return "Ship → Market";
                case CargoItineraryCommandKind.LoadFromCompanyTownStock: return "Warehouse → Ship";
                case CargoItineraryCommandKind.DeliverContract: return "Ship → Contract recipient";
                default: return "Ship → Warehouse";
            }
        }

        private static string ItineraryFlowDescription(CargoItineraryCommandKind kind) =>
            ItineraryVerb(kind) + ": " + ItineraryFlow(kind) +
            (kind == CargoItineraryCommandKind.DeliverContract ? " · funded contract terms; accept on the board first" :
                kind == CargoItineraryCommandKind.BuyFromMarket || kind == CargoItineraryCommandKind.SellToMarket ? " · paid market trade" : " · no payment");
    }
}
