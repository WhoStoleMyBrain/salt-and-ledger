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
            CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryCommandKind.SellToMarket,
            CargoItineraryCommandKind.LoadFromCompanyTownStock, CargoItineraryCommandKind.BuyFromMarket
        };

        private void RenderSelectedItineraryStop()
        {
            var parent = Element<VisualElement>("itineraryStopEditor"); parent.Clear();
            var draft = ActiveItineraryDraft;
            var index = selectedItineraryStop;
            var stop = draft.Stops[index];
            var heading = Panel(parent, null, "row itinerary-stop-heading");
            Text(heading, null, "STOP " + (index + 1), "run-eyebrow");
            Dropdown(heading, "itineraryPort" + index, "Town", portIds.Select(PortName).ToList());
            var picker = Element<DropdownField>("itineraryPort" + index);
            picker.SetValueWithoutNotify(PortName(stop.PortId));
            picker.RegisterValueChangedCallback(change =>
            {
                if (change.target != picker || picker.index < 0) return;
                stop.PortId = portIds[picker.index]; ItineraryEdited();
            });
            var tools = Panel(parent, null, "row itinerary-order-tools");
            Button(tools, "itineraryStopUp" + index, "Move earlier", () =>
            {
                draft.Stops.RemoveAt(index); draft.Stops.Insert(index - 1, stop);
                selectedItineraryStop = index - 1; RebuildChangedItinerary();
            }).SetEnabled(index > 0);
            Button(tools, "itineraryStopDown" + index, "Move later", () =>
            {
                draft.Stops.RemoveAt(index); draft.Stops.Insert(index + 1, stop);
                selectedItineraryStop = index + 1; RebuildChangedItinerary();
            }).SetEnabled(index + 1 < draft.Stops.Count);
            Button(tools, "itineraryRemoveStop" + index, "Remove stop", () =>
            { draft.Stops.RemoveAt(index); RebuildChangedItinerary(); }).SetEnabled(draft.Stops.Count > 2);
            Button(tools, "itineraryHarbor" + index, "Inspect harbor", () => OpenPlannerHarbor(stop.PortId));
            Text(parent, null, "Maximum whole units / visit · blank or 0 removes an order", "run-footnote itinerary-grid-help");
            var header = Panel(parent, null, "itinerary-grid-row itinerary-grid-heading");
            Text(header, null, "Commodity", "itinerary-grid-good");
            foreach (var kind in ItineraryKinds)
            {
                var label = Text(header, null, ItineraryVerb(kind) + "\n" + ItineraryFlow(kind), "itinerary-grid-column");
                label.tooltip = ItineraryFlowDescription(kind);
            }
            foreach (var goodId in goodIds)
            {
                var row = Panel(parent, "itineraryCommodity" + index + "_" + goodId, "itinerary-grid-row");
                var good = Panel(row, null, "itinerary-grid-good");
                var icon = new MaritimeGlyph(goodId); icon.AddToClassList("sl-glyph-small"); good.Add(icon);
                Text(good, null, Session.Content.GetGood(goodId).Name, "itinerary-good-name");
                foreach (var kind in ItineraryKinds)
                {
                    var field = new TextField { name = ItineraryCellName(index, goodId, kind) };
                    field.AddToClassList("itinerary-quantity");
                    field.tooltip = Session.Content.GetGood(goodId).Name + " · " + ItineraryFlowDescription(kind) +
                        ". Maximum whole units per visit; blank or 0 removes the order. Minimum defaults to 1, shortage to Skip. Advanced keeps individual limits.";
                    row.Add(field);
                    field.RegisterValueChangedCallback(change =>
                    {
                        if (change.target != field) return;
                        EditItineraryCell(stop, goodId, kind, change.newValue);
                    });
                }
            }
            Text(parent, "itineraryOrderNote", "", "run-footnote");
            var advanced = new Foldout { name = "itineraryAdvanced" + index, text = "Advanced limits & execution order", value = stop.AdvancedOpen };
            advanced.AddToClassList("itinerary-advanced"); parent.Add(advanced);
            advanced.RegisterValueChangedCallback(change =>
            {
                if (change.target != advanced) return;
                stop.AdvancedOpen = change.newValue;
                if (!change.newValue) plannerStop = plannerAction = -1;
            });
            var orderTools = Panel(advanced, null, "row itinerary-order-tools");
            Button(orderTools, "itineraryDefaultOrder" + index, "Use unload / sell / load / buy order", () =>
            {
                SortItineraryActions(stop); stop.PreserveOrder = false;
                RebuildChangedItinerary();
            }).tooltip = "Explicitly reorder this stop. All existing instructions and their limits are retained, including repeated goods.";
            Text(advanced, null, "Instructions run top to bottom, once each. Repeated good/action pairs remain separate here. Quotes use current holdings; earlier draft instructions are not simulated.", "run-footnote");
            Panel(advanced, "itineraryOrderedActions", "itinerary-ordered-actions");
            RenderItineraryAdvancedActions();
        }

        private void RenderItineraryAdvancedActions()
        {
            var parent = Element<VisualElement>("itineraryOrderedActions"); parent.Clear();
            var stop = ActiveItineraryDraft.Stops[selectedItineraryStop];
            for (var i = 0; i < stop.Actions.Count; i++)
                RenderItineraryAction(parent, selectedItineraryStop, i, stop.Actions[i]);
            Button(parent, "itineraryAddAction" + selectedItineraryStop, "Add ordered instruction", () =>
            {
                stop.Actions.Add(new ItineraryActionDraft()); stop.RetainedCellOrder.Clear(); stop.PreserveOrder = true;
                RebuildChangedItinerary();
            }).SetEnabled(stop.Actions.Count < 8 && ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count) < 24);
        }

        private void EditItineraryCell(ItineraryStopDraft stop, string goodId, CargoItineraryCommandKind kind, string value)
        {
            var matches = stop.Actions.Where(action => action.GoodId == goodId && action.Kind == kind).ToList();
            if (matches.Count > 1) return;
            var key = goodId + ":" + (int)kind;
            var empty = string.IsNullOrWhiteSpace(value) || long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number == 0;
            if (empty)
            {
                if (matches.Count == 1)
                {
                    if (stop.PreserveOrder && stop.RetainedCellOrder.Count == 0)
                        stop.RetainedCellOrder.AddRange(stop.Actions);
                    stop.RemovedCells[key] = matches[0];
                    stop.Actions.Remove(matches[0]);
                }
            }
            else if (matches.Count == 1) matches[0].Maximum = value;
            else
            {
                if (!stop.RemovedCells.TryGetValue(key, out var action))
                    action = new ItineraryActionDraft { GoodId = goodId, Kind = kind };
                action.Maximum = value;
                var position = stop.RetainedCellOrder.IndexOf(action);
                var successor = stop.PreserveOrder && position >= 0
                    ? stop.RetainedCellOrder.Skip(position + 1).FirstOrDefault(candidate => stop.Actions.Contains(candidate)) : null;
                if (successor != null) stop.Actions.Insert(stop.Actions.IndexOf(successor), action);
                else stop.Actions.Add(action);
                if (stop.RetainedCellOrder.Count > 0 && position < 0) stop.RetainedCellOrder.Add(action);
                stop.RemovedCells.Remove(key);
                if (!stop.PreserveOrder) SortItineraryActions(stop);
            }
            plannerStop = plannerAction = -1;
            RenderItineraryAdvancedActions();
            ItineraryEdited();
        }

        private static void SortItineraryActions(ItineraryStopDraft stop)
        {
            stop.RetainedCellOrder.Clear();
            var sorted = stop.Actions.OrderBy(action => Array.IndexOf(ItineraryKinds, action.Kind))
                .ThenBy(action => action.GoodId, StringComparer.Ordinal).ToArray();
            stop.Actions.Clear(); stop.Actions.AddRange(sorted);
        }

        private void RefreshItineraryGrid()
        {
            var stop = ActiveItineraryDraft.Stops[selectedItineraryStop];
            var total = ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count);
            Element<Button>("itineraryAddStop").SetEnabled(ActiveItineraryDraft.Stops.Count < 4 && total < 24);
            foreach (var goodId in goodIds)
                foreach (var kind in ItineraryKinds)
                {
                    var field = Root.Q<TextField>(ItineraryCellName(selectedItineraryStop, goodId, kind));
                    if (field == null) continue;
                    var matches = stop.Actions.Where(action => action.Kind == kind && action.GoodId == goodId).ToList();
                    field.SetValueWithoutNotify(matches.Count > 1 ? "ordered" : matches.Count == 0 ? "" : matches[0].Maximum);
                    field.SetEnabled(matches.Count <= 1 && (matches.Count == 1 || stop.Actions.Count < 8 && total < 24));
                    field.EnableInClassList("itinerary-cell-active", matches.Count != 0);
                    if (matches.Count > 1) field.tooltip = "Repeated instructions: inspect or edit each one in Advanced limits & execution order. No quantities or limits have been merged.";
                    else if (matches.Count == 0 && !field.enabledSelf) field.tooltip = "Route limit reached: 8 instructions per stop, 24 in total. Remove an existing quantity to make room.";
                    else field.tooltip = Session.Content.GetGood(goodId).Name + " · " + ItineraryFlowDescription(kind) +
                        ". Maximum whole units per visit; blank or 0 removes the order. Individual constraints are in Advanced.";
                }
            SetText("itineraryOrderNote", stop.Actions.Count + "/8 instructions · " + (stop.PreserveOrder
                ? "Existing order retained; new goods append. Inspect or change order in Advanced."
                : "Default order: Unload → Sell → Load → Buy, then commodity ID. Advanced can override it."));
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
                default: return "Ship → Warehouse";
            }
        }

        private static string ItineraryFlowDescription(CargoItineraryCommandKind kind) =>
            ItineraryVerb(kind) + ": " + ItineraryFlow(kind) +
            (kind == CargoItineraryCommandKind.BuyFromMarket || kind == CargoItineraryCommandKind.SellToMarket ? " · paid market trade" : " · no payment");
    }
}
