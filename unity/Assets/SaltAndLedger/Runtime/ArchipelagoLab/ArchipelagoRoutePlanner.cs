using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private sealed class ItineraryActionDraft
        {
            public CargoItineraryCommandKind Kind;
            public string GoodId = "grain";
            public string ContractId;
            public string Maximum = "2", Minimum = "1", Price = "0", CashReserve = "0", Budget = "", WarehouseReserve = "0";
            public CargoItineraryShortagePolicy Shortage;
        }

        private sealed class ItineraryStopDraft
        {
            public string PortId;
            public bool PreserveOrder;
            public bool AdvancedOpen;
            public readonly List<ItineraryActionDraft> Actions = new List<ItineraryActionDraft>();
        }

        private sealed class ItineraryDraft
        {
            public string Id;
            public bool Changed;
            public readonly List<ItineraryStopDraft> Stops = new List<ItineraryStopDraft>();
        }

        private readonly Dictionary<string, ItineraryDraft> itineraryDrafts = new Dictionary<string, ItineraryDraft>(StringComparer.Ordinal);
        private readonly List<string> itineraryShipIds = new List<string>();
        private ArchipelagoScreen plannerParent;
        private VisualElement plannerReturnFocus;
        private string plannerHarborReturn;
        private string plannerIndustryReturn;
        private bool harborReturnsToPlanner;
        private string itineraryApplyError;
        private long itineraryApplyErrorRevision = -1;
        private int selectedItineraryStop;
        private bool UsesItineraries => Session != null;
        private ItineraryDraft ActiveItineraryDraft => itineraryDrafts[Session.Player.Id];

        private void ClearItineraryDrafts()
        {
            itineraryDrafts.Clear(); plannerHarborReturn = null; plannerIndustryReturn = null;
            selectedItineraryStop = 0; ClearApprovedRouteEditor();
            harborReturnsToPlanner = false;
            itineraryApplyError = null;
        }

        private void BuildRoutePlanner() => BuildApprovedRoutePlanner();

        public void OpenRoutePlanner(string shipId)
        {
            if (!UsesItineraries) { Validation("Start a run before opening its route planner."); return; }
            if (!Session.Snapshot.Ships.TryGetValue(shipId, out var ship) || !ship.IsPlayer || ship.OwnerId != Session.Player.OwnerId)
            { Validation("Select an owned ship to plan its route."); return; }
            if (Screen != ArchipelagoScreen.RoutePlanner && !harborReturnsToPlanner)
            {
                plannerParent = Screen;
                plannerReturnFocus = Root.focusController?.focusedElement as VisualElement;
                plannerHarborReturn = TradeIsOpen ? Session.SelectedPortId : null;
                plannerIndustryReturn = IndustryIsOpen ? Session.SelectedPortId : null;
            }
            returnToFactory = false;
            harborReturnsToPlanner = false;
            CloseModals(); Session.Pause(); Session.SelectShip(shipId); EnsureItineraryDraft();
            itineraryApplyError = null;

            routeStage = ReopenedRouteStage;
            ClearApprovedRouteEditor();
            ShowScreen(ArchipelagoScreen.RoutePlanner); RenderItineraryCards(); RefreshRoutePlannerStatus();
            Element<Button>("itineraryBackButton").Focus();
        }

        private void CloseRoutePlanner()
        {
            if (routeEditingAction != null)
            {
                CancelApprovedRouteAction();
                return;
            }
            var harbor = plannerHarborReturn; plannerHarborReturn = null;
            var industry = plannerIndustryReturn; plannerIndustryReturn = null;
            ShowScreen(plannerParent); Refresh();
            if (harbor != null) OpenTrade(harbor);
            else if (industry != null) { Session.InspectTradePort(industry); ShowModal("industryBackdrop", "closeIndustryButton"); RefreshIndustry(); }
            else if (plannerReturnFocus?.panel != null && plannerReturnFocus.enabledInHierarchy) plannerReturnFocus.Focus();
        }

        private void OpenPlannerHarbor(string portId)
        {
            if (portId == null) return;
            ShowScreen(ArchipelagoScreen.Gameplay); Refresh(); OpenTrade(portId);
            harborReturnsToPlanner = TradeIsOpen;
        }

        private bool ReturnToPlannerAfterHarbor()
        {
            if (!harborReturnsToPlanner) return false;
            harborReturnsToPlanner = false;
            ShowScreen(ArchipelagoScreen.RoutePlanner);
            Refresh();
            Element<Button>("itineraryHarborButton").Focus();
            return true;
        }

        private void EnsureItineraryDraft()
        {
            if (itineraryDrafts.ContainsKey(Session.Player.Id)) return;
            var applied = Session.Player.Itinerary;
            if (applied != null) { itineraryDrafts[Session.Player.Id] = CopyItinerary(applied); return; }
            var draft = new ItineraryDraft { Id = "route_" + Session.Player.Id, Changed = true };
            var first = Session.Player.DockedPortId ?? Session.Player.DestinationPortId ?? portIds[0];
            draft.Stops.Add(new ItineraryStopDraft { PortId = first });
            itineraryDrafts.Add(Session.Player.Id, draft);
        }

        private ItineraryDraft CopyItinerary(CargoItineraryPolicy policy)
        {
            var draft = new ItineraryDraft { Id = policy.Id };
            foreach (var source in policy.Stops)
            {
                var stop = new ItineraryStopDraft { PortId = source.PortId, PreserveOrder = true };
                foreach (var command in source.Commands) stop.Actions.Add(new ItineraryActionDraft
                {
                    Kind = command.Kind, GoodId = command.GoodId, ContractId = command.ContractId, Maximum = Q(command.MaximumQuantity), Minimum = Q(command.MinimumQuantity),
                    Shortage = command.ShortagePolicy, Price = Number(command.MarginalPriceLimit), CashReserve = Money(command.CashReserve),
                    Budget = command.MaximumExpenditure == long.MaxValue ? "" : Money(command.MaximumExpenditure), WarehouseReserve = Q(command.WarehouseReserve)
                });
                draft.Stops.Add(stop);
            }
            return draft;
        }

        private void DraftBreadItinerary()
        {
            try
            {
                var draft = CopyItinerary(CargoItineraryPresets.BreadLoop(Session.Snapshot, Session.Player.Id));
                draft.Changed = true; itineraryDrafts[Session.Player.Id] = draft;
                selectedItineraryStop = 0; RenderItineraryCards(); RefreshRoutePlannerStatus();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { SetText("itineraryValidation", exception.Message); }
        }

        private void AddItineraryStop()
        {
            var draft = ActiveItineraryDraft;
            var port = portIds.FirstOrDefault(id => draft.Stops.All(stop => stop.PortId != id));
            if (port == null || draft.Stops.Count >= 4 || draft.Stops.Sum(s => s.Actions.Count) >= 24) return;
            var stop = new ItineraryStopDraft { PortId = port }; draft.Stops.Add(stop);
            selectedItineraryStop = draft.Stops.Count - 1;
            RebuildChangedItinerary();
        }

        private void RebuildChangedItinerary()
        {
            itineraryApplyError = null;
            ActiveItineraryDraft.Changed = true;
            ClearApprovedRouteEditor();
            RenderItineraryCards(); RefreshRoutePlannerStatus();
        }

        private void RenderItineraryCards()
        {
            var parent = Element<VisualElement>("itineraryStops"); parent.Clear();
            var draft = ActiveItineraryDraft;
            selectedItineraryStop = Math.Max(0, Math.Min(selectedItineraryStop, draft.Stops.Count - 1));
            for (var i = 0; i < draft.Stops.Count; i++)
            {
                var stopIndex = i;
                var card = Panel(parent, "itineraryStop" + i, "route-v3-stop");
                card.EnableInClassList("itinerary-selected", i == selectedItineraryStop);
                Button(card, "itinerarySelectStop" + i, "", () =>
                {
                    selectedItineraryStop = stopIndex; ClearApprovedRouteEditor();
                    RenderItineraryCards(); RefreshRoutePlannerStatus();
                    Element<Button>("itinerarySelectStop" + stopIndex).Focus();
                }, "itinerary-select-stop");
                Text(card, "itineraryStopSummary" + i, "", "run-copy itinerary-stop-summary");
                Text(card, "itineraryStopState" + i, "", "run-footnote itinerary-stop-state");
                Text(card, "itineraryStopWarning" + i, "", "run-footnote itinerary-stop-warning");
                RenderApprovedStopTools(card, i);
            }
            Element<Button>("itineraryAddStop").SetEnabled(draft.Stops.Count < 4 && draft.Stops.Sum(s => s.Actions.Count) < 24);
            RenderSelectedItineraryStop();
            UpdateItinerarySummaries();
            RefreshApprovedRouteWorkspace();
        }

        private void RenderItineraryAction(VisualElement parent, int stopIndex, int actionIndex, ItineraryActionDraft action)
        {
            RenderApprovedRouteAction(parent, stopIndex, actionIndex, action);
        }

        private void RefreshItineraryFieldVisibility(string key, CargoItineraryCommandKind kind)
        {
            var buy = kind == CargoItineraryCommandKind.BuyFromMarket;
            var sell = kind == CargoItineraryCommandKind.SellToMarket;
            var price = Element<TextField>("itineraryPrice" + key);
            price.EnableInClassList("hidden", !buy && !sell);
            price.label = (sell ? "Minimum marginal bid" : "Maximum marginal ask") + " · 0 unrestricted";
            Element<TextField>("itineraryCashReserve" + key).EnableInClassList("hidden", !buy);
            Element<TextField>("itineraryBudget" + key).EnableInClassList("hidden", !buy);
            Element<TextField>("itineraryWarehouseReserve" + key).EnableInClassList("hidden", kind != CargoItineraryCommandKind.LoadFromCompanyTownStock);
            Root.Q<DropdownField>("itineraryContract" + key)?.EnableInClassList("hidden", kind != CargoItineraryCommandKind.DeliverContract);
            Element<DropdownField>("itineraryGood" + key).SetEnabled(kind != CargoItineraryCommandKind.DeliverContract);
        }

        private void UpdateItinerarySummaries()
        {
            var draft = ActiveItineraryDraft;
            for (var i = 0; i < draft.Stops.Count; i++)
            {
                var stop = draft.Stops[i];
                Element<Button>("itinerarySelectStop" + i).text = (i + 1) + ". " + PortName(stop.PortId);
                SetText("itineraryStopSummary" + i, stop.Actions.Count == 0 ? "Choose goods below" :
                    string.Join("\n", ItineraryKinds.Where(kind => stop.Actions.Any(action => action.Kind == kind)).Select(kind =>
                        ItineraryVerb(kind) + " " + string.Join(", ", stop.Actions.Where(action => action.Kind == kind).Select(action =>
                            action.Maximum + " " + Session.Content.GetGood(action.GoodId).Name)))));
                var warning = MissingItineraryWarehouse(stop) ? "Warehouse needed \u00b7 orders follow Skip / Wait" : "";
                SetText("itineraryStopWarning" + i, warning);
                Element<Label>("itineraryStopWarning" + i).EnableInClassList("hidden", warning.Length == 0);
                SetText("itineraryStopState" + i, ItineraryStopState(i) + " \u2192 " + PortName(draft.Stops[(i + 1) % draft.Stops.Count].PortId));
                for (var j = 0; j < stop.Actions.Count; j++)
                {
                    var action = stop.Actions[j];
                    var foldout = Root.Q<Foldout>("itineraryAction" + i + "_" + j);
                    if (foldout != null) foldout.text = (j + 1) + ". " + ItineraryVerb(action.Kind) + " " +
                        action.Minimum + "\u2013" + action.Maximum + " " + Session.Content.GetGood(action.GoodId).Name + " \u00b7 " + action.Shortage;
                }
            }
            RefreshItineraryGrid();
        }

        private static string ItineraryVerb(CargoItineraryCommandKind kind)
        {
            switch (kind)
            {
                case CargoItineraryCommandKind.BuyFromMarket: return "Buy";
                case CargoItineraryCommandKind.SellToMarket: return "Sell";
                case CargoItineraryCommandKind.LoadFromCompanyTownStock: return "Load";
                case CargoItineraryCommandKind.DeliverContract: return "Deliver contract";
                default: return "Unload";
            }
        }

        private CargoItineraryPolicy ReadItineraryDraft()
        {
            var draft = ActiveItineraryDraft;
            var stops = new List<CargoItineraryStop>();
            foreach (var stop in draft.Stops)
                stops.Add(new CargoItineraryStop(stop.PortId, stop.Actions.Select(ReadItineraryAction)));
            return new CargoItineraryPolicy(draft.Id, stops);
        }

        private CargoItineraryCommand ReadItineraryAction(ItineraryActionDraft action)
        {
            var buy = action.Kind == CargoItineraryCommandKind.BuyFromMarket;
            var market = buy || action.Kind == CargoItineraryCommandKind.SellToMarket;
            return new CargoItineraryCommand(action.Kind, action.GoodId,
                ItineraryQuantity(action.Maximum, "Maximum quantity", false), ItineraryQuantity(action.Minimum, "Minimum quantity", false),
                action.Shortage, market ? ItineraryInteger(action.Price, "Marginal price", true) : 0,
                buy ? ItineraryMoney(action.CashReserve, "Company reserve", true) : 0,
                buy && !string.IsNullOrWhiteSpace(action.Budget) ? ItineraryMoney(action.Budget, "Expenditure cap", false) : long.MaxValue,
                action.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock ? ItineraryStockReserve(action.WarehouseReserve) : 0,
                action.Kind == CargoItineraryCommandKind.DeliverContract ? action.ContractId : null);
        }

        private static long ItineraryInteger(string value, string label, bool zero)
        {
            if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result < (zero ? 0 : 1))
                throw new ArgumentException(label + " must be a " + (zero ? "nonnegative" : "positive") + " whole number.");
            return result;
        }

        private int ItineraryQuantity(string value, string label, bool zero) => checked((int)(ItineraryInteger(value, label, zero) * Session.Snapshot.QuantityScale));

        private int ItineraryStockReserve(string value)
        {
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var units) || units < 0)
                throw new ArgumentException("Warehouse reserve requires nonnegative units, with at most four decimal places.");
            var exact = checked(units * Session.Snapshot.QuantityScale);
            if (exact != decimal.Truncate(exact)) throw new ArgumentException("Warehouse reserve supports at most four decimal places.");
            return checked((int)exact);
        }

        private long ItineraryMoney(string value, string label, bool zero)
        {
            if (!decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var coins) || coins < 0 || (!zero && coins == 0))
                throw new ArgumentException(label + " requires nonnegative coins, with at most four decimal places.");
            var exact = checked(coins * Session.Snapshot.MoneyScale);
            if (exact != decimal.Truncate(exact)) throw new ArgumentException(label + " supports at most four decimal places.");
            return checked((long)exact);
        }

        private void ApplyItineraryDraft()
        {
            try
            {
                var policy = ReadItineraryDraft();
                Session.ApplyItinerary(policy, Session.Snapshot);
                itineraryApplyError = null;
                foreach (var stop in ActiveItineraryDraft.Stops) stop.PreserveOrder = true;
                ActiveItineraryDraft.Changed = false; routeStage = RouteWorkspaceStage.Applied; ClearApprovedRouteEditor(); Refresh();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                itineraryApplyError = exception.Message; itineraryApplyErrorRevision = Session.Snapshot.Revision;
                SetText("itineraryValidation", exception.Message);
            }
        }

        private void RefreshItineraryAvailability()
        {
            Root.Q<Button>("routePlannerButton")?.EnableInClassList("hidden", !UsesItineraries);
            Root.Q<Button>("harborRoutePlannerButton")?.EnableInClassList("hidden", !UsesItineraries);
            var shipping = Root.Q<Button>("industryShippingButton");
            if (shipping != null) shipping.text = "Connect / inspect Route planner";
        }

        private void RefreshRoutePlannerStatus()
        {
            if (Screen != ArchipelagoScreen.RoutePlanner || !UsesItineraries) return;
            EnsureItineraryDraft();
            var world = Session.Snapshot; var ship = Session.Player; var progress = ship.ItineraryProgress;
            if (itineraryApplyErrorRevision != world.Revision) itineraryApplyError = null;
            itineraryShipIds.Clear(); itineraryShipIds.AddRange(Session.OwnedShips.Select(s => s.Id));
            var picker = Element<DropdownField>("itineraryShip");
            picker.choices = Session.OwnedShips.Select(s => s.Name).ToList();
            picker.SetValueWithoutNotify(picker.choices[itineraryShipIds.IndexOf(ship.Id)]);
            var current = ship.Itinerary == null ? null : CargoItineraryEvaluator.Evaluate(world, ship.Id);
            SetText("itineraryLiveStatus", ship.Name + " · " + (ship.DockedPortId == null
                ? "Sailing → " + PortName(ship.DestinationPortId) + " · " + ship.RemainingTravelDays + "d"
                : "Docked · " + PortName(ship.DockedPortId)) + " · hold " + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) +
                " · free " + Q(ship.CargoCapacity - ship.CargoUnits) +
                (progress == null ? "\nNo route assigned" : "\n" + (ship.AutomationEnabled ? "Active" : "Paused") + " · next " + ItineraryNextInstruction() + " · " + current.Reason));
            Element<Label>("itineraryLiveStatus").tooltip = progress == null ? "Apply a draft, then choose Start route." :
                "Pass " + (progress.Cycle + 1) + ", stop " + (progress.StopIndex + 1) + "/" + ship.Itinerary.Stops.Count +
                ". Current decision: " + current.Kind + ". " + current.Reason;
            SetText("itineraryCargo", "Company cash " + Money(ship.Cash) + " coins · cargo: " +
                (ship.CargoUnits == 0 ? "empty" : string.Join(", ", ship.Cargo.Where(g => g.Value > 0).Select(g => Q(g.Value) + " " + Session.Content.GetGood(g.Key).Name))) +
                (progress == null ? "" : "\nLast outcome: " + progress.LastDecision + " / " + progress.LastReason + " · " + Q(progress.LastQuantity) + " units. " +
                    (progress.OperationsDay == world.Day ? progress.OperationsUsed : 0) + "/8 operations used today. Pass count advances when leaving the last listed stop."));
            Element<Toggle>("itineraryManualToggle").SetValueWithoutNotify(!ship.AutomationEnabled);
            Element<Toggle>("itineraryManualToggle").SetEnabled(ship.Itinerary != null && !Session.IsChallengeEnded);
            Element<Button>("itineraryRunButton").SetEnabled(ship.Itinerary != null && ship.AutomationEnabled && ship.DockedPortId != null && !Session.IsChallengeEnded);
            Element<Button>("itineraryNextDayButton").SetEnabled(!Session.IsChallengeEnded);
            Element<Button>("itineraryReloadButton").SetEnabled(ship.Itinerary != null);
            SetText("itineraryDraftStatus", (ActiveItineraryDraft.Changed ? "UNAPPLIED DRAFT" : "APPLIED PLAN") + " · " + ActiveItineraryDraft.Stops.Count +
                " stops · " + ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count) + "/24 instructions · select a stop to edit goods");
            var example = ActiveItineraryDraft.Id == "bread_loop";
            Element<Label>("itineraryExampleHelp").EnableInClassList("hidden", !example);
            if (example)
                SetText("itineraryExampleHelp", "Bread loop: deliver Flour + Logs → owned Bakery; carry Bread → food source; Grain → processing. Plan now, build and fund the Bakery separately.");
            UpdateItinerarySummaries();
            try
            {
                var policy = ReadItineraryDraft();
                CargoItineraryEvaluator.ValidatePolicy(world, ship.Id, policy);
                var unchanged = policy.EquivalentTo(ship.Itinerary);
                var partial = progress != null && progress.ActionIndex > 0 && progress.ActionIndex < ship.Itinerary.Stops[progress.StopIndex].Commands.Count;
                var usedToday = progress != null && progress.OperationsDay == world.Day && progress.OperationsUsed > 0;
                var message = Session.IsChallengeEnded ? "This run is frozen. You may inspect or draft, but cannot apply." : unchanged
                    ? "Already assigned · reapply pauses; cursor and daily budget stay intact." : ship.AutomationEnabled
                    ? "Select Pause route before applying." : ship.DockedPortId == null
                    ? "Apply after arrival at a route stop." : partial
                    ? "Finish the remaining instructions before replacing this partial visit."
                    : usedToday ? "Advance a day before replacing orders: today's operation budget stays spent."
                    : !policy.Stops.Any(s => s.PortId == ship.DockedPortId) ? "Include the actual dock among the new stops before applying."
                    : "Ready to apply · cargo retained · route starts paused.";
                var missing = ActiveItineraryDraft.Stops.Where(MissingItineraryWarehouse).Select(stop => PortName(stop.PortId)).ToArray();
                if (missing.Length > 0) message += "\nWarehouse needed: " + string.Join(", ", missing) + ". Plan is valid; unavailable transfers follow Skip / Wait.";
                var ready = !Session.IsChallengeEnded && (unchanged || !ship.AutomationEnabled && ship.DockedPortId != null && !partial && !usedToday && policy.Stops.Any(s => s.PortId == ship.DockedPortId));
                Element<Button>("itineraryApplyButton").SetEnabled(ready);
                SetText("itineraryValidation", itineraryApplyError == null ? message : itineraryApplyError + "\n" + message);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                Element<Button>("itineraryApplyButton").SetEnabled(false);
                SetText("itineraryValidation", "Draft needs attention: " + exception.Message);
            }
            if (routeEditingAction != null) Element<Button>("itineraryApplyButton").SetEnabled(false);
            RefreshApprovedRouteWorkspace();
            if (routeEditingAction != null) ValidateApprovedInlineEditor();
        }

    }
}
