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
            public string Maximum = "2", Minimum = "1", Price = "0", CashReserve = "0", Budget = "", WarehouseReserve = "0";
            public CargoItineraryShortagePolicy Shortage;
        }

        private sealed class ItineraryStopDraft
        {
            public string PortId;
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
        private string itineraryApplyError;
        private long itineraryApplyErrorRevision = -1;
        private int plannerStop = -1, plannerAction = -1;
        private bool UsesItineraries => Session?.Identity?.ItineraryVersion == 1;
        private ItineraryDraft ActiveItineraryDraft => itineraryDrafts[Session.Player.Id];

        private void ClearItineraryDrafts()
        {
            itineraryDrafts.Clear(); plannerStop = -1; plannerAction = -1; plannerHarborReturn = null; plannerIndustryReturn = null;
            itineraryApplyError = null;
        }

        private void BuildRoutePlanner()
        {
            var screen = CreateScreen(ArchipelagoScreen.RoutePlanner, "Route planner",
                "Ordered work at real docks. Edit a passive draft; Apply assigns it to this ship. Different goods stay aboard until explicitly sold or unloaded.");
            var toolbar = Panel(screen, null, "itinerary-toolbar row");
            Dropdown(toolbar, "itineraryShip", "Owned ship", new List<string> { "Select ship" });
            Element<DropdownField>("itineraryShip").RegisterValueChangedCallback(change =>
            {
                if (change.target != Element<DropdownField>("itineraryShip")) return;
                var index = Element<DropdownField>("itineraryShip").index;
                if (index < 0 || index >= itineraryShipIds.Count) return;
                Session.SelectShip(itineraryShipIds[index]); EnsureItineraryDraft();
                plannerStop = plannerAction = -1; RenderItineraryCards(); RefreshRoutePlannerStatus();
            });
            var manual = new Toggle("Manual control · route actions paused") { name = "itineraryManualToggle" };
            toolbar.Add(manual);
            manual.RegisterValueChangedCallback(change =>
            {
                if (change.target != manual) return;
                itineraryApplyError = null;
                Run(() => Session.SetAutomationEnabled(!change.newValue));
                if (statusIsError)
                {
                    itineraryApplyError = status;
                    itineraryApplyErrorRevision = Session.Snapshot.Revision;
                }
                RefreshRoutePlannerStatus();
            });
            Text(screen, "itineraryLiveStatus", "", "run-copy itinerary-live");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "itineraryScroll" };
            scroll.AddToClassList("run-content-scroll"); screen.Add(scroll);
            var evidence = new Foldout { name = "itineraryShipDetails", text = "Cargo and committed route state", value = false };
            scroll.Add(evidence); Text(evidence, "itineraryCargo", "", "run-copy");
            var tools = Panel(scroll, null, "row run-actions");
            Button(tools, "itineraryExampleButton", "Draft Grain–Flour–Bread loop", DraftBreadItinerary);
            Button(tools, "itineraryReloadButton", "Reload applied route", () =>
            {
                itineraryDrafts.Remove(Session.Player.Id); EnsureItineraryDraft();
                plannerStop = plannerAction = -1; RenderItineraryCards(); RefreshRoutePlannerStatus();
            });
            Text(scroll, "itineraryDraftStatus", "", "run-copy");
            Text(scroll, "itineraryExampleHelp", "", "run-copy hidden");
            Text(scroll, null, "Each action executes once per visit, up to its maximum. Below its minimum: Skip continues; Wait keeps that exact action. Eight operations per ship per day include skips and departure. Quotes below describe current stock, not promised future prices.", "run-footnote");
            Panel(scroll, "itineraryStops", "itinerary-stops");
            Button(scroll, "itineraryAddStop", "Add stop", AddItineraryStop);
            Text(screen, "itineraryValidation", "", "run-copy itinerary-validation");
            var actions = Panel(screen, null, "row run-actions itinerary-footer");
            Button(actions, "itineraryBackButton", "Back", CloseRoutePlanner);
            Button(actions, "itineraryApplyButton", "Apply draft", ApplyItineraryDraft, "primary");
            Button(actions, "itineraryRunButton", "Run dockside actions", () => Run(() => Session.RunDockside()));
            Button(actions, "itineraryNextDayButton", "Next day", () => Run(() => Session.NextDay()));
            Button(actions, "itineraryHarborButton", "Open current harbor", () => OpenPlannerHarbor(Session.Player.DockedPortId ?? Session.Player.DestinationPortId));
        }

        public void OpenRoutePlanner(string shipId)
        {
            if (!UsesItineraries) { Validation("This saved world retains its legacy route controls. Start a new v3 run to use itineraries."); return; }
            if (!Session.Snapshot.Ships.TryGetValue(shipId, out var ship) || !ship.IsPlayer || ship.OwnerId != Session.Player.OwnerId)
            { Validation("Select an owned ship to plan its route."); return; }
            if (Screen != ArchipelagoScreen.RoutePlanner)
            {
                plannerParent = Screen;
                plannerReturnFocus = Root.focusController?.focusedElement as VisualElement;
                plannerHarborReturn = TradeIsOpen ? Session.SelectedPortId : null;
                plannerIndustryReturn = IndustryIsOpen ? Session.SelectedPortId : null;
            }
            CloseModals(); Session.Pause(); Session.SelectShip(shipId); EnsureItineraryDraft();
            itineraryApplyError = null;
            plannerStop = plannerAction = -1;
            ShowScreen(ArchipelagoScreen.RoutePlanner); RenderItineraryCards(); RefreshRoutePlannerStatus();
            Element<Button>("itineraryBackButton").Focus();
        }

        private void CloseRoutePlanner()
        {
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
        }

        private void EnsureItineraryDraft()
        {
            if (itineraryDrafts.ContainsKey(Session.Player.Id)) return;
            var applied = Session.Player.Itinerary;
            if (applied != null) { itineraryDrafts[Session.Player.Id] = CopyItinerary(applied); return; }
            var draft = new ItineraryDraft { Id = "route_" + Session.Player.Id, Changed = true };
            var first = Session.Player.DockedPortId ?? Session.Player.DestinationPortId ?? portIds[0];
            foreach (var port in new[] { first, portIds.First(id => id != first) })
            {
                var stop = new ItineraryStopDraft { PortId = port };
                stop.Actions.Add(new ItineraryActionDraft { Kind = draft.Stops.Count == 0
                    ? CargoItineraryCommandKind.BuyFromMarket : CargoItineraryCommandKind.SellToMarket });
                draft.Stops.Add(stop);
            }
            itineraryDrafts.Add(Session.Player.Id, draft);
        }

        private ItineraryDraft CopyItinerary(CargoItineraryPolicy policy)
        {
            var draft = new ItineraryDraft { Id = policy.Id };
            foreach (var source in policy.Stops)
            {
                var stop = new ItineraryStopDraft { PortId = source.PortId };
                foreach (var command in source.Commands) stop.Actions.Add(new ItineraryActionDraft
                {
                    Kind = command.Kind, GoodId = command.GoodId, Maximum = Q(command.MaximumQuantity), Minimum = Q(command.MinimumQuantity),
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
                plannerStop = plannerAction = -1; RenderItineraryCards(); RefreshRoutePlannerStatus();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { SetText("itineraryValidation", exception.Message); }
        }

        private void AddItineraryStop()
        {
            var draft = ActiveItineraryDraft;
            var port = portIds.FirstOrDefault(id => draft.Stops.All(stop => stop.PortId != id));
            if (port == null || draft.Stops.Count >= 4 || draft.Stops.Sum(s => s.Actions.Count) >= 24) return;
            var stop = new ItineraryStopDraft { PortId = port }; stop.Actions.Add(new ItineraryActionDraft()); draft.Stops.Add(stop);
            RebuildChangedItinerary();
        }

        private void RebuildChangedItinerary()
        {
            itineraryApplyError = null;
            ActiveItineraryDraft.Changed = true; plannerStop = plannerAction = -1;
            RenderItineraryCards(); RefreshRoutePlannerStatus();
        }

        private void RenderItineraryCards()
        {
            var parent = Element<VisualElement>("itineraryStops"); parent.Clear();
            var draft = ActiveItineraryDraft;
            for (var i = 0; i < draft.Stops.Count; i++)
            {
                var stopIndex = i; var stop = draft.Stops[i];
                var card = Panel(parent, "itineraryStop" + i, "run-card itinerary-stop");
                var top = Panel(card, null, "row itinerary-stop-heading");
                Text(top, null, "STOP " + (i + 1), "run-eyebrow");
                Dropdown(top, "itineraryPort" + i, "Town", portIds.Select(PortName).ToList());
                var picker = Element<DropdownField>("itineraryPort" + i);
                picker.SetValueWithoutNotify(PortName(stop.PortId));
                picker.RegisterValueChangedCallback(change =>
                { if (change.target != picker) return; stop.PortId = portIds[picker.index]; ItineraryEdited(); });
                var tools = Panel(card, null, "row itinerary-order-tools");
                Button(tools, "itineraryStopUp" + i, "Move stop up", () =>
                { draft.Stops.RemoveAt(stopIndex); draft.Stops.Insert(stopIndex - 1, stop); RebuildChangedItinerary(); }).SetEnabled(i > 0);
                Button(tools, "itineraryStopDown" + i, "Move stop down", () =>
                { draft.Stops.RemoveAt(stopIndex); draft.Stops.Insert(stopIndex + 1, stop); RebuildChangedItinerary(); }).SetEnabled(i + 1 < draft.Stops.Count);
                Button(tools, "itineraryRemoveStop" + i, "Remove stop", () =>
                { draft.Stops.RemoveAt(stopIndex); RebuildChangedItinerary(); }).SetEnabled(draft.Stops.Count > 2);
                Button(tools, "itineraryHarbor" + i, "Inspect harbor", () => OpenPlannerHarbor(stop.PortId));
                Text(card, "itineraryStopSummary" + i, "", "run-copy itinerary-stop-summary");
                for (var j = 0; j < stop.Actions.Count; j++) RenderItineraryAction(card, stopIndex, j, stop.Actions[j]);
                Button(card, "itineraryAddAction" + i, "Add action", () =>
                { stop.Actions.Add(new ItineraryActionDraft()); RebuildChangedItinerary(); })
                    .SetEnabled(stop.Actions.Count < 8 && draft.Stops.Sum(s => s.Actions.Count) < 24);
            }
            Element<Button>("itineraryAddStop").SetEnabled(draft.Stops.Count < 4 && draft.Stops.Sum(s => s.Actions.Count) < 24);
            UpdateItinerarySummaries();
        }

        private void RenderItineraryAction(VisualElement parent, int stopIndex, int actionIndex, ItineraryActionDraft action)
        {
            var key = stopIndex + "_" + actionIndex;
            var foldout = new Foldout { name = "itineraryAction" + key, text = "", value = false };
            foldout.AddToClassList("itinerary-action"); parent.Add(foldout);
            foldout.RegisterValueChangedCallback(change =>
            {
                if (change.target != foldout) return;
                if (change.newValue)
                {
                    plannerStop = stopIndex; plannerAction = actionIndex;
                    foreach (var other in Element<VisualElement>("itineraryStops").Query<Foldout>().ToList())
                        if (other != foldout) other.SetValueWithoutNotify(false);
                    RefreshRoutePlannerStatus();
                }
            });
            var tools = Panel(foldout, null, "row itinerary-order-tools");
            var actions = ActiveItineraryDraft.Stops[stopIndex].Actions;
            Button(tools, "itineraryActionUp" + key, "Move action up", () =>
            { actions.RemoveAt(actionIndex); actions.Insert(actionIndex - 1, action); RebuildChangedItinerary(); }).SetEnabled(actionIndex > 0);
            Button(tools, "itineraryActionDown" + key, "Move action down", () =>
            { actions.RemoveAt(actionIndex); actions.Insert(actionIndex + 1, action); RebuildChangedItinerary(); }).SetEnabled(actionIndex + 1 < actions.Count);
            Button(tools, "itineraryRemoveAction" + key, "Remove action", () =>
            { actions.RemoveAt(actionIndex); RebuildChangedItinerary(); }).SetEnabled(actions.Count > 1);
            var form = Panel(foldout, null, "itinerary-fields");
            Dropdown(form, "itineraryKind" + key, "Instruction", new List<string> { "Buy from public market", "Sell to public market", "Load from company town stock", "Unload to company town stock" });
            var kind = Element<DropdownField>("itineraryKind" + key); kind.SetValueWithoutNotify(kind.choices[(int)action.Kind]);
            Dropdown(form, "itineraryGood" + key, "Commodity", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            var good = Element<DropdownField>("itineraryGood" + key); good.SetValueWithoutNotify(Session.Content.GetGood(action.GoodId).Name);
            good.RegisterValueChangedCallback(change =>
            { if (change.target != good) return; action.GoodId = goodIds[good.index]; ItineraryEdited(); });
            ItineraryField(form, "itineraryMax" + key, "Maximum whole units / visit", action.Maximum, value => action.Maximum = value);
            ItineraryField(form, "itineraryMin" + key, "Minimum whole units", action.Minimum, value => action.Minimum = value);
            Dropdown(form, "itineraryShortage" + key, "Below minimum", new List<string> { "Skip for this visit", "Wait at this stop" });
            var shortage = Element<DropdownField>("itineraryShortage" + key); shortage.SetValueWithoutNotify(shortage.choices[(int)action.Shortage]);
            shortage.RegisterValueChangedCallback(change =>
            { if (change.target != shortage) return; action.Shortage = (CargoItineraryShortagePolicy)shortage.index; ItineraryEdited(); });
            ItineraryField(form, "itineraryPrice" + key, "Marginal price limit · 0 unrestricted", action.Price, value => action.Price = value);
            ItineraryField(form, "itineraryCashReserve" + key, "Keep company coins", action.CashReserve, value => action.CashReserve = value);
            ItineraryField(form, "itineraryBudget" + key, "Spend at most · blank unrestricted", action.Budget, value => action.Budget = value);
            ItineraryField(form, "itineraryWarehouseReserve" + key, "Keep units in town stock", action.WarehouseReserve, value => action.WarehouseReserve = value);
            kind.RegisterValueChangedCallback(change =>
            { if (change.target != kind) return; action.Kind = (CargoItineraryCommandKind)kind.index; RefreshItineraryFieldVisibility(key, action.Kind); ItineraryEdited(); });
            RefreshItineraryFieldVisibility(key, action.Kind);
            Text(foldout, "itineraryQuote" + key, "", "run-copy itinerary-quote");
        }

        private void ItineraryField(VisualElement parent, string id, string label, string value, Action<string> changed)
        {
            var field = new TextField(label) { name = id, value = value };
            parent.Add(field);
            field.RegisterValueChangedCallback(change =>
            { if (change.target != field) return; changed(change.newValue); ItineraryEdited(); });
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
        }

        private void ItineraryEdited()
        {
            itineraryApplyError = null;
            ActiveItineraryDraft.Changed = true; UpdateItinerarySummaries(); RefreshRoutePlannerStatus();
        }

        private void UpdateItinerarySummaries()
        {
            for (var i = 0; i < ActiveItineraryDraft.Stops.Count; i++)
            {
                var stop = ActiveItineraryDraft.Stops[i];
                SetText("itineraryStopSummary" + i, stop.Actions.Count + " ordered actions at " + PortName(stop.PortId) +
                    " → " + PortName(ActiveItineraryDraft.Stops[(i + 1) % ActiveItineraryDraft.Stops.Count].PortId));
                for (var j = 0; j < stop.Actions.Count; j++)
                {
                    var action = stop.Actions[j];
                    Element<Foldout>("itineraryAction" + i + "_" + j).text = (j + 1) + ". " + ItineraryVerb(action.Kind) + " " +
                        action.Minimum + "–" + action.Maximum + " " + Session.Content.GetGood(action.GoodId).Name + " · " + action.Shortage;
                }
            }
        }

        private static string ItineraryVerb(CargoItineraryCommandKind kind)
        {
            switch (kind)
            {
                case CargoItineraryCommandKind.BuyFromMarket: return "Buy";
                case CargoItineraryCommandKind.SellToMarket: return "Sell";
                case CargoItineraryCommandKind.LoadFromCompanyTownStock: return "Load";
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
                action.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock ? ItineraryStockReserve(action.WarehouseReserve) : 0);
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
                ActiveItineraryDraft.Changed = false; Refresh();
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
            Element<Foldout>("automationFoldout").EnableInClassList("hidden", UsesItineraries);
            Root.Q<Button>("logisticsButton")?.EnableInClassList("hidden", UsesItineraries);
            Root.Q<Button>("harborRoutePlannerButton")?.EnableInClassList("hidden", !UsesItineraries);
            Element<Button>("industryShippingButton").text = UsesItineraries ? "Connect / inspect Route planner" : "Connect / inspect industrial shipping";
        }

        private void RefreshRoutePlannerStatus()
        {
            if (Screen != ArchipelagoScreen.RoutePlanner || !UsesItineraries) return;
            EnsureItineraryDraft();
            var world = Session.Snapshot; var ship = Session.Player; var progress = ship.ItineraryProgress;
            if (itineraryApplyErrorRevision != world.Revision) itineraryApplyError = null;
            itineraryShipIds.Clear(); itineraryShipIds.AddRange(Session.OwnedShips.Select(s => s.Id));
            var picker = Element<DropdownField>("itineraryShip");
            picker.choices = Session.OwnedShips.Select(s => s.Name + " [" + s.Id + "]").ToList();
            picker.SetValueWithoutNotify(picker.choices[itineraryShipIds.IndexOf(ship.Id)]);
            var current = ship.Itinerary == null ? null : CargoItineraryEvaluator.Evaluate(world, ship.Id);
            SetText("itineraryLiveStatus", ship.Name + " · " + (ship.DockedPortId == null
                ? "Sailing to " + PortName(ship.DestinationPortId) + " · " + ship.RemainingTravelDays + " days remaining"
                : "Docked at " + PortName(ship.DockedPortId)) + " · cargo " + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) +
                (progress == null ? "\nNo itinerary assigned." : "\nPass " + (progress.Cycle + 1) + " · stop " + (progress.StopIndex + 1) + "/" + ship.Itinerary.Stops.Count +
                    " · next " + (progress.ActionIndex < ship.Itinerary.Stops[progress.StopIndex].Commands.Count ? "action " + (progress.ActionIndex + 1) : "departure") +
                    " at " + PortName(ship.Itinerary.Stops[progress.StopIndex].PortId) + " · " + (ship.AutomationEnabled ? "route enabled" : "manual control") +
                    "\n" + current.Reason));
            SetText("itineraryCargo", "Company cash " + Money(ship.Cash) + " coins · cargo: " +
                (ship.CargoUnits == 0 ? "empty" : string.Join(", ", ship.Cargo.Where(g => g.Value > 0).Select(g => Q(g.Value) + " " + Session.Content.GetGood(g.Key).Name))) +
                (progress == null ? "" : "\nCommitted: " + progress.LastDecision + " / " + progress.LastReason + " · " + Q(progress.LastQuantity) + " units. " +
                    (progress.OperationsDay == world.Day ? progress.OperationsUsed : 0) + "/8 operations used today. Pass count advances when leaving the last listed stop."));
            Element<Toggle>("itineraryManualToggle").SetValueWithoutNotify(!ship.AutomationEnabled);
            Element<Toggle>("itineraryManualToggle").SetEnabled(ship.Itinerary != null && !Session.IsChallengeEnded);
            Element<Button>("itineraryRunButton").SetEnabled(ship.Itinerary != null && ship.AutomationEnabled && ship.DockedPortId != null && !Session.IsChallengeEnded);
            Element<Button>("itineraryNextDayButton").SetEnabled(!Session.IsChallengeEnded);
            Element<Button>("itineraryReloadButton").SetEnabled(ship.Itinerary != null);
            SetText("itineraryDraftStatus", (ActiveItineraryDraft.Changed ? "UNAPPLIED DRAFT" : "APPLIED PLAN") + " · " + ActiveItineraryDraft.Stops.Count +
                " stops · " + ActiveItineraryDraft.Stops.Sum(s => s.Actions.Count) + "/24 actions. Viewing and editing spend no time or money.");
            var example = ActiveItineraryDraft.Id == "bread_loop";
            Element<Label>("itineraryExampleHelp").EnableInClassList("hidden", !example);
            if (example)
                SetText("itineraryExampleHelp", "Example intent: supply a funded Bakery with Flour and Logs, carry its Bread to the agricultural town, and bring Grain to processing for Flour. " +
                    "Build the Bakery and fund its wages yourself. This draft creates no facilities, goods or money; skips and future prices can change its outcome.");
            if (plannerStop >= 0 && plannerAction >= 0)
                SetText("itineraryQuote" + plannerStop + "_" + plannerAction, "Complete the draft fields to inspect an authoritative current quote.");
            try
            {
                var policy = ReadItineraryDraft();
                RefreshItineraryQuote(policy);
                CargoItineraryEvaluator.ValidatePolicy(world, ship.Id, policy);
                var unchanged = policy.EquivalentTo(ship.Itinerary);
                var partial = progress != null && progress.ActionIndex > 0 && progress.ActionIndex < ship.Itinerary.Stops[progress.StopIndex].Commands.Count;
                var usedToday = progress != null && progress.OperationsDay == world.Day && progress.OperationsUsed > 0;
                var message = Session.IsChallengeEnded ? "This run is frozen. You may inspect or draft, but cannot apply." : unchanged
                    ? "This plan is already assigned. Reapplying preserves its exact cursor and daily budget, and leaves route actions paused." : ship.AutomationEnabled
                    ? "Choose Manual control before applying. Pausing preserves cargo and the exact next action." : ship.DockedPortId == null
                    ? "Apply after this voyage arrives. The applied itinerary and cargo remain intact." : partial
                    ? "Finish or skip the remaining actions in this visit before replacing the route. Your partial visit is preserved."
                    : usedToday ? "Advance a day before replacing orders already executed today. Editing cannot reset the ship's daily operation budget."
                    : !policy.Stops.Any(s => s.PortId == ship.DockedPortId) ? "Include the actual dock among the new stops before applying."
                    : "Ready to apply. Cargo stays aboard. Apply leaves route actions paused; enable them explicitly.";
                var ready = !Session.IsChallengeEnded && (unchanged || !ship.AutomationEnabled && ship.DockedPortId != null && !partial && !usedToday && policy.Stops.Any(s => s.PortId == ship.DockedPortId));
                Element<Button>("itineraryApplyButton").SetEnabled(ready);
                SetText("itineraryValidation", itineraryApplyError == null ? message : itineraryApplyError + "\n" + message);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                Element<Button>("itineraryApplyButton").SetEnabled(false);
                SetText("itineraryValidation", "Draft needs attention: " + exception.Message);
            }
        }

        private void RefreshItineraryQuote(CargoItineraryPolicy policy)
        {
            if (plannerStop < 0 || plannerAction < 0) return;
            var stop = policy.Stops[plannerStop]; var command = stop.Commands[plannerAction];
            var decision = CargoItineraryEvaluator.PreviewCommand(Session.Snapshot, Session.Player.Id, policy, plannerStop, plannerAction);
            var world = Session.Snapshot; var ship = Session.Player; var port = world.Ports[stop.PortId];
            var stock = world.CompanyStockpiles.Values.FirstOrDefault(s => s.OwnerId == ship.OwnerId && s.PortId == stop.PortId);
            var label = Element<Label>("itineraryQuote" + plannerStop + "_" + plannerAction); label.SetEnabled(true);
            var local = ship.DockedPortId == port.Id;
            label.text = (local ? "CURRENT DOCK QUOTE" : "REMOTE / HYPOTHETICAL — actual arrival required; prices may change") +
                "\n" + Session.Content.GetGood(command.GoodId).Name + ": market " + Q(port.Inventory[command.GoodId]) +
                " · hold " + Q(ship.Cargo[command.GoodId]) + " · company town stock " + (stock == null ? "none" : Q(stock.Inventory[command.GoodId])) +
                "\nMarket budget " + Money(port.MarketCash) + " coins · company " + Money(ship.Cash) + " · keep " + Money(command.CashReserve) +
                (command.MaximumExpenditure == long.MaxValue ? " · no order spending cap" : " · spending cap " + Money(command.MaximumExpenditure)) +
                (command.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock ? "\nKeep " + Q(command.WarehouseReserve) + " units in the warehouse." : "") +
                "\n" + decision.Reason + "\nSelected " + Q(decision.SelectedQuantity) + " units" +
                (decision.Preview == null ? command.Kind == CargoItineraryCommandKind.LoadFromCompanyTownStock || command.Kind == CargoItineraryCommandKind.UnloadToCompanyTownStock
                    ? " · own-stock movement carries no cash." : " · no executable market order at these constraints." : " · exact total " + Money(decision.Preview.TotalAmount) + " coins" +
                    (decision.Preview.Price == null ? "" : " · last marginal price " + Number(decision.Preview.Price.LastMarginalPrice))) +
                "\nThis preview uses current holdings only; preceding draft actions have not been executed.";
        }
    }
}
