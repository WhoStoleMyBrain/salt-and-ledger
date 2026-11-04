using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private enum RouteWorkspaceStage { Course, Orders, Review, Applied }
        private RouteWorkspaceStage routeStage;
        private ArchipelagoMap routeChart;
        private ItineraryActionDraft routeEditingAction;
        private int routeEditingIndex = -1;
        private string routeEditingReturnCell;
        private bool routeRequiresMinimum;
        private RouteWorkspaceStage ReopenedRouteStage => Session.Player.Itinerary == null || ActiveItineraryDraft.Changed
            ? RouteWorkspaceStage.Course : RouteWorkspaceStage.Applied;

        private void ClearApprovedRouteEditor()
        {
            routeEditingAction = null; routeEditingIndex = -1; routeEditingReturnCell = null;
        }

        private void BuildApprovedRoutePlanner()
        {
            var screen = CreateScreen(ArchipelagoScreen.RoutePlanner, "Trade routes");
            screen.Clear(); screen.AddToClassList("route-v3-screen");
            var heading = Panel(screen, null, "route-v3-heading");
            var titles = Panel(heading, null, "route-v3-title-group");
            Text(titles, null, "FLEET  /  TRADE ROUTES", "run-eyebrow");
            Text(titles, "routeWorkspaceTitle", "Plot your course", "run-heading");
            Text(titles, "itineraryDraftStatus", "", "route-v3-subtitle");
            Dropdown(heading, "itineraryShip", "Ship", new List<string> { "Select ship" });
            var picker = Element<DropdownField>("itineraryShip");
            picker.RegisterValueChangedCallback(change =>
            {
                if (change.target != picker || picker.index < 0 || picker.index >= itineraryShipIds.Count) return;
                Session.SelectShip(itineraryShipIds[picker.index]); EnsureItineraryDraft(); selectedItineraryStop = 0;
                ClearApprovedRouteEditor(); routeStage = ReopenedRouteStage;
                RenderItineraryCards(); RefreshRoutePlannerStatus();
            });
            Button(heading, "routeCourseButton", "Course map", () => SetApprovedRouteStage(RouteWorkspaceStage.Course));
            var body = Panel(screen, null, "route-v3-body");
            var main = Panel(body, null, "route-v3-main");
            var chartPanel = Panel(main, "routeCoursePanel", "route-v3-chart-panel");
            routeChart = new ArchipelagoMap { name = "routeCourseMap", AllowShipSelection = false };
            chartPanel.Add(routeChart);
            routeChart.PortSelected += SelectApprovedRoutePort;
            routeChart.PortTradeRequested += OpenPlannerHarbor;
            routeChart.PortHovered += ShowApprovedRouteHover;
            var hover = Panel(chartPanel, "routeTownHover", "route-v3-town-hover hidden");
            hover.pickingMode = PickingMode.Ignore;
            Text(hover, "routeTownHoverTitle", "", "route-v3-context-title");
            Text(hover, "routeTownHoverDetail", "", "route-v3-copy");
            Text(main, "routePassageEstimate", "", "route-v3-passage");
            var editor = new ScrollView(ScrollViewMode.Vertical) { name = "itineraryScroll" };
            editor.AddToClassList("route-v3-editor"); main.Add(editor);
            Panel(editor, "routeStopTabs", "route-v3-stop-tabs");
            Panel(editor, "itineraryStopEditor", "route-v3-stop-editor");

            var right = new ScrollView(ScrollViewMode.Vertical) { name = "routeContextScroll" };
            right.AddToClassList("route-v3-context"); body.Add(right);
            var course = Panel(right, "routeCourseContext", "route-v3-context-section");
            Text(course, "routeCourseEyebrow", "SHIP ITINERARY", "route-v3-eyebrow");
            Text(course, "routeCourseTitle", "Sailing order", "route-v3-context-title");
            Panel(course, "itineraryStops", "route-v3-stops");
            Button(course, "itineraryAddStop", "+ Add town", AddItineraryStop);
            Text(course, "routeReturnPort", "", "route-v3-copy");
            var context = Panel(right, "routeStopContext", "route-v3-context-section");
            Text(context, "routeStopEyebrow", "STOP / CONTEXT", "route-v3-eyebrow");
            Text(context, "routeStopTown", "", "route-v3-context-title");
            Text(context, "routeStopRole", "", "route-v3-muted");
            Text(context, "routeStopInfrastructure", "", "route-v3-copy");
            Text(context, "routeStopStorageWarning", "", "route-v3-warning");
            Text(context, null, "LOCAL PRODUCTION", "route-v3-eyebrow");
            Panel(context, "routeStopSuggestions", "route-v3-suggestions");
            Text(context, "routeHoldSummary", "", "route-v3-hold");
            Text(context, null, "Current holdings only. Orders execute at arrival; stock and cargo are never reserved.", "route-v3-muted");
            Button(context, "routeNextStopButton", "Next stop", NextApprovedRouteStop, "primary");
            var tools = new Foldout { name = "routePlannerTools", text = "Route tools & details", value = false };
            right.Add(tools);
            Button(tools, "itineraryExampleButton", "Draft Grain–Flour–Bread loop", () => { DraftBreadItinerary(); SetApprovedRouteStage(RouteWorkspaceStage.Course); });
            Button(tools, "itineraryReloadButton", "Reload saved route", () =>
            {
                itineraryDrafts.Remove(Session.Player.Id); EnsureItineraryDraft(); ClearApprovedRouteEditor();
                RenderItineraryCards(); RefreshRoutePlannerStatus();
            });
            Text(tools, "itineraryExampleHelp", "", "route-v3-copy hidden");
            var evidence = new Foldout { name = "itineraryShipDetails", text = "Cargo, last outcome & daily budget" };
            tools.Add(evidence); Text(evidence, "itineraryCargo", "", "route-v3-copy");
            var rules = new Foldout { name = "itineraryRules", text = "How routes work" }; tools.Add(rules);
            Text(rules, null, "Whole units per visit. Each instruction executes once, even if partly filled. Skip continues; Wait holds below the minimum. Eight daily operations include skips and departures. Quotes are current, not forecasts.", "route-v3-copy");
            Button(tools, "itineraryHarborButton", "Inspect harbor", () => OpenPlannerHarbor(ActiveItineraryDraft.Stops[selectedItineraryStop].PortId));
            var manual = new Toggle("Pause route") { name = "itineraryManualToggle", tooltip = "Retains cargo, voyage, cursor and today's operation budget." };
            right.Add(manual);
            manual.RegisterValueChangedCallback(change =>
            {
                if (change.target != manual) return;
                itineraryApplyError = null; Run(() => Session.SetAutomationEnabled(!change.newValue));
                if (statusIsError) { itineraryApplyError = status; itineraryApplyErrorRevision = Session.Snapshot.Revision; }
                RefreshRoutePlannerStatus();
            });
            Text(right, "itineraryValidation", "", "route-v3-validation");
            Button(right, "routeOrdersButton", "Set stop orders →", () => SetApprovedRouteStage(RouteWorkspaceStage.Orders), "primary");
            Button(right, "routeReviewButton", "Review route →", () => SetApprovedRouteStage(RouteWorkspaceStage.Review), "primary");
            Button(right, "itineraryApplyButton", "Apply route", ApplyItineraryDraft, "primary");
            Button(right, "routeStartButton", "Start route →", () => Run(() => Session.SetAutomationEnabled(true)), "primary");
            Button(right, "routeEditButton", "Edit route", () => SetApprovedRouteStage(RouteWorkspaceStage.Orders));
            Button(right, "itineraryBackButton", "Back to fleet", CloseRoutePlanner);
            var activity = Panel(screen, null, "route-v3-activity");
            Text(activity, "itineraryLiveStatus", "", "route-v3-live");
            Button(activity, "itineraryRunButton", "Run dockside", () => Run(() => Session.RunDockside()));
            Button(activity, "itineraryNextDayButton", "Next day", () => Run(() => Session.NextDay()));
        }

        private void SetApprovedRouteStage(RouteWorkspaceStage stage)
        {
            if (routeEditingAction != null)
            {
                SetText("itineraryValidation", "Save or cancel the open instruction first.");
                return;
            }
            routeStage = stage; RenderItineraryCards(); RefreshRoutePlannerStatus();
        }

        private void SelectApprovedRoutePort(string portId)
        {
            if (routeEditingAction != null) return;
            var draft = ActiveItineraryDraft;
            var index = draft.Stops.FindIndex(stop => stop.PortId == portId);
            if (index >= 0)
            {
                selectedItineraryStop = index;
                if (routeStage == RouteWorkspaceStage.Review || routeStage == RouteWorkspaceStage.Applied) routeStage = RouteWorkspaceStage.Orders;
                RenderItineraryCards(); RefreshRoutePlannerStatus(); return;
            }
            if (routeStage != RouteWorkspaceStage.Course) { OpenPlannerHarbor(portId); return; }
            if (draft.Stops.Count >= 4) return;
            draft.Stops.Add(new ItineraryStopDraft { PortId = portId }); selectedItineraryStop = draft.Stops.Count - 1;
            RebuildChangedItinerary();
        }

        private void ShowApprovedRouteHover(string portId)
        {
            var hover = Element<VisualElement>("routeTownHover");
            hover.EnableInClassList("hidden", portId == null);
            if (portId == null) return;
            var port = Session.Snapshot.Ports[portId];
            SetText("routeTownHoverTitle", port.Name);
            var outputs = port.Facilities.SelectMany(building => Session.Content.GetRecipe(building.Configuration.Instance.RecipeId).Outputs.Select(quantity => quantity.GoodId))
                .Distinct().OrderBy(id => id, StringComparer.Ordinal).Select(id => Session.Content.GetGood(id).Name);
            SetText("routeTownHoverDetail", "Public output: " + string.Join(", ", outputs) + "\n" + RouteOwnedInfrastructure(portId));
        }

        private void RenderApprovedStopTools(VisualElement parent, int index)
        {
            var draft = ActiveItineraryDraft; var stop = draft.Stops[index];
            var tools = Panel(parent, null, "route-v3-stop-tools");
            Button(tools, "itineraryStopUp" + index, "↑", () =>
            {
                draft.Stops.RemoveAt(index); draft.Stops.Insert(index - 1, stop); selectedItineraryStop = index - 1; RebuildChangedItinerary();
            }).SetEnabled(index > 0);
            Button(tools, "itineraryStopDown" + index, "↓", () =>
            {
                draft.Stops.RemoveAt(index); draft.Stops.Insert(index + 1, stop); selectedItineraryStop = index + 1; RebuildChangedItinerary();
            }).SetEnabled(index + 1 < draft.Stops.Count);
            Button(tools, "itineraryRemoveStop" + index, "×", () =>
            { draft.Stops.RemoveAt(index); RebuildChangedItinerary(); }).SetEnabled(draft.Stops.Count > 1);
            tools.Children().ElementAt(0).tooltip = "Move town earlier";
            tools.Children().ElementAt(1).tooltip = "Move town later";
            tools.Children().ElementAt(2).tooltip = "Remove town from draft";
        }

        private string RouteOwnedInfrastructure(string portId)
        {
            var sites = Session.Snapshot.PrivateFacilities.Values.Where(site => site.OwnerId == Session.Player.OwnerId && site.PortId == portId)
                .OrderBy(site => site.Id, StringComparer.Ordinal).Select(site => Session.Content.GetBuilding(site.Blueprint.BuildingId).Name).ToArray();
            return sites.Length == 0 ? "No company industry" : "Company: " + string.Join(", ", sites);
        }

        private void RefreshApprovedRouteWorkspace()
        {
            if (Session == null || routeChart == null || !itineraryDrafts.ContainsKey(Session.Player.Id)) return;
            var draft = ActiveItineraryDraft; var ship = Session.Player;
            var orders = routeStage == RouteWorkspaceStage.Orders;
            var course = routeStage == RouteWorkspaceStage.Course;
            var review = routeStage == RouteWorkspaceStage.Review;
            SetText("routeWorkspaceTitle", orders ? "Stop orders" : review ? "Review sailing order" :
                routeStage == RouteWorkspaceStage.Applied ? ship.Name + " · " + (ship.AutomationEnabled ? "Under way" : "Route paused") : "Plot your course");
            Element<VisualElement>("routeCoursePanel").EnableInClassList("hidden", orders);
            Element<ScrollView>("itineraryScroll").EnableInClassList("hidden", !orders);
            Element<VisualElement>("routeCourseContext").EnableInClassList("hidden", orders);
            Element<VisualElement>("routeStopContext").EnableInClassList("hidden", !orders);
            Element<Button>("routeCourseButton").EnableInClassList("hidden", !orders);
            Element<Button>("itineraryAddStop").EnableInClassList("hidden", !course);
            Element<Button>("routeOrdersButton").EnableInClassList("hidden", !course);
            Element<Button>("routeOrdersButton").SetEnabled(draft.Stops.Count >= 2 && routeEditingAction == null);
            Element<Button>("routeReviewButton").EnableInClassList("hidden", !orders);
            Element<Button>("routeReviewButton").SetEnabled(routeEditingAction == null);
            Element<Button>("itineraryApplyButton").EnableInClassList("hidden", !review);
            Element<Button>("routeStartButton").EnableInClassList("hidden", routeStage != RouteWorkspaceStage.Applied || ship.AutomationEnabled);
            Element<Button>("routeStartButton").SetEnabled(ship.Itinerary != null && !Session.IsChallengeEnded);
            Element<Button>("routeEditButton").EnableInClassList("hidden", course || orders);
            Element<Toggle>("itineraryManualToggle").EnableInClassList("hidden", ship.Itinerary == null);
            Element<Button>("itineraryBackButton").SetEnabled(routeEditingAction == null);
            Element<DropdownField>("itineraryShip").SetEnabled(routeEditingAction == null);
            Element<Button>("itineraryReloadButton").SetEnabled(ship.Itinerary != null && routeEditingAction == null);
            Element<Button>("itineraryExampleButton").SetEnabled(routeEditingAction == null);
            SetText("routeReturnPort", "↻ Return to " + PortName(draft.Stops[0].PortId));
            foreach (var tools in Element<VisualElement>("itineraryStops").Query<VisualElement>(className: "route-v3-stop-tools").ToList())
                tools.EnableInClassList("hidden", !course);
            routeChart.SetDraftRoute(draft.Stops.Select(stop => stop.PortId).ToArray());
            routeChart.Present(Session.Snapshot, draft.Stops[selectedItineraryStop].PortId, ship.Id, false, 1);
            foreach (var port in Session.Snapshot.Ports.Values)
            {
                var included = draft.Stops.Any(item => item.PortId == port.Id);
                routeChart.Q<Button>("port_" + port.Id).tooltip = port.Role + "\n" + (course
                    ? included ? "Click to select this draft stop." : "Click to add this town to the draft."
                    : included ? "Click to edit this stop's orders." : "Click to inspect this town's Harbor.") +
                    " Right-click to inspect Harbor.";
            }
            var days = 0; var connected = true;
            if (draft.Stops.Count > 1)
                for (var i = 0; i < draft.Stops.Count; i++)
                {
                    var a = draft.Stops[i].PortId; var b = draft.Stops[(i + 1) % draft.Stops.Count].PortId;
                    var leg = Session.Snapshot.Routes.FirstOrDefault(route => route.OriginPortId == a && route.DestinationPortId == b || route.OriginPortId == b && route.DestinationPortId == a);
                    if (leg == null) connected = false; else days += leg.DurationDays(ship.Configuration.Speed);
                }
            SetText("routePassageEstimate", draft.Stops.Count < 2 ? "Select towns on the chart · 2–4 unique ports" : connected
                ? days + " sailing days per loop · port waits excluded" : "A required sea connection is missing");
            Element<Label>("routePassageEstimate").EnableInClassList("hidden", orders);
            RefreshApprovedStopTabs();
            var stop = draft.Stops[selectedItineraryStop]; var town = Session.Snapshot.Ports[stop.PortId];
            SetText("routeStopEyebrow", "STOP " + (selectedItineraryStop + 1).ToString("00", CultureInfo.InvariantCulture) + " / CONTEXT");
            SetText("routeStopTown", town.Name); SetText("routeStopRole", town.Role);
            SetText("routeStopInfrastructure", RouteOwnedInfrastructure(stop.PortId));
            var pool = Session.Snapshot.CompanyStockpiles.Values.FirstOrDefault(stock => stock.OwnerId == ship.OwnerId && stock.PortId == stop.PortId);
            SetText("routeStopStorageWarning", pool == null ? "Warehouse not built. Load / Unload orders remain valid; unavailable storage follows Skip / Wait." : "");
            Element<Label>("routeStopStorageWarning").EnableInClassList("hidden", pool != null);
            SetText("routeHoldSummary", "CURRENT HOLD  " + Q(ship.CargoUnits) + " / " + Q(ship.CargoCapacity) + "\n" +
                (ship.CargoUnits == 0 ? "Empty" : string.Join(" · ", ship.Cargo.Where(pair => pair.Value > 0).Select(pair => Q(pair.Value) + " " + Session.Content.GetGood(pair.Key).Name))));
            Element<Button>("routeNextStopButton").text = selectedItineraryStop + 1 < draft.Stops.Count ? "Next stop →" : "Review route →";
            Element<Button>("routeNextStopButton").SetEnabled(routeEditingAction == null);
            RenderApprovedSuggestions(stop);
        }

        private void RefreshApprovedStopTabs()
        {
            var tabs = Element<VisualElement>("routeStopTabs"); tabs.Clear();
            for (var i = 0; i < ActiveItineraryDraft.Stops.Count; i++)
            {
                var index = i;
                var button = Button(tabs, "routeStopTab" + i, (i + 1).ToString("00", CultureInfo.InvariantCulture) + "  " + PortName(ActiveItineraryDraft.Stops[i].PortId), () =>
                {
                    selectedItineraryStop = index; ClearApprovedRouteEditor(); RenderItineraryCards(); RefreshRoutePlannerStatus();
                });
                button.EnableInClassList("route-v3-selected-tab", i == selectedItineraryStop);
                button.SetEnabled(routeEditingAction == null);
            }
            Text(tabs, null, "↻ Repeat", "route-v3-muted");
        }

        private void NextApprovedRouteStop()
        {
            if (routeEditingAction != null) return;
            if (selectedItineraryStop + 1 < ActiveItineraryDraft.Stops.Count)
            { selectedItineraryStop++; RenderItineraryCards(); RefreshRoutePlannerStatus(); }
            else SetApprovedRouteStage(RouteWorkspaceStage.Review);
        }

        private void RenderApprovedSuggestions(ItineraryStopDraft stop)
        {
            var parent = Element<VisualElement>("routeStopSuggestions"); parent.Clear();
            var sites = Session.Snapshot.PrivateFacilities.Values.Where(site => site.OwnerId == Session.Player.OwnerId && site.PortId == stop.PortId).ToArray();
            var suggestions = new List<Tuple<string, CargoItineraryCommandKind>>();
            foreach (var site in sites)
            {
                var recipe = Session.Content.GetRecipe(site.Blueprint.RecipeId);
                suggestions.AddRange(recipe.Inputs.Select(quantity => Tuple.Create(quantity.GoodId, CargoItineraryCommandKind.UnloadToCompanyTownStock)));
                suggestions.AddRange(recipe.Outputs.Select(quantity => Tuple.Create(quantity.GoodId, CargoItineraryCommandKind.LoadFromCompanyTownStock)));
            }
            if (suggestions.Count == 0)
                suggestions.AddRange(Session.Snapshot.Ports[stop.PortId].Facilities.SelectMany(building => Session.Content.GetRecipe(building.Configuration.Instance.RecipeId).Outputs.Select(quantity => quantity.GoodId))
                    .Select(id => Tuple.Create(id, CargoItineraryCommandKind.BuyFromMarket)));
            foreach (var suggestion in suggestions.Distinct().OrderBy(item => item.Item1, StringComparer.Ordinal).ThenBy(item => item.Item2).Take(4))
            {
                var value = suggestion;
                Button(parent, "routeSuggest_" + value.Item1 + "_" + (int)value.Item2, "+ " + ItineraryVerb(value.Item2) + " " + Session.Content.GetGood(value.Item1).Name,
                    () => BeginApprovedRouteAction(-1, value.Item1, value.Item2)).SetEnabled(routeEditingAction == null && stop.Actions.Count < 8 && ActiveItineraryDraft.Stops.Sum(item => item.Actions.Count) < 24);
            }
            if (suggestions.Count == 0) Text(parent, null, "Choose goods in the order grid.", "route-v3-muted");
        }

        private static ItineraryActionDraft CopyApprovedAction(ItineraryActionDraft source) => new ItineraryActionDraft
        {
            Kind = source.Kind, GoodId = source.GoodId, Maximum = source.Maximum, Minimum = source.Minimum,
            Price = source.Price, CashReserve = source.CashReserve, Budget = source.Budget,
            WarehouseReserve = source.WarehouseReserve, Shortage = source.Shortage
        };

        private void BeginApprovedRouteAction(int index, string goodId = null, CargoItineraryCommandKind kind = CargoItineraryCommandKind.BuyFromMarket)
        {
            if (routeEditingAction != null) return;
            var stop = ActiveItineraryDraft.Stops[selectedItineraryStop];
            routeEditingIndex = index;
            routeEditingAction = index < 0 ? new ItineraryActionDraft { GoodId = goodId ?? goodIds[0], Kind = kind, Maximum = "1" } : CopyApprovedAction(stop.Actions[index]);
            routeEditingReturnCell = ItineraryCellName(selectedItineraryStop, routeEditingAction.GoodId, routeEditingAction.Kind);
            routeRequiresMinimum = routeEditingAction.Minimum != "1";
            stop.AdvancedOpen = true;
            RenderSelectedItineraryStop(); RefreshRoutePlannerStatus();
        }

        private void RenderApprovedRouteAction(VisualElement parent, int stopIndex, int actionIndex, ItineraryActionDraft action)
        {
            var key = stopIndex + "_" + actionIndex; var stop = ActiveItineraryDraft.Stops[stopIndex];
            var row = Panel(parent, "itineraryAction" + key, "route-v3-instruction");
            var title = Panel(row, null, "route-v3-instruction-title");
            Text(title, null, (actionIndex + 1).ToString("00", CultureInfo.InvariantCulture), "route-v3-order-number");
            var icon = new MaritimeGlyph(action.GoodId); icon.AddToClassList("sl-glyph-small"); title.Add(icon);
            var expand = Button(title, "routeEditOrder" + key, ItineraryVerb(action.Kind) + " " + Session.Content.GetGood(action.GoodId).Name,
                () => BeginApprovedRouteAction(actionIndex), "route-v3-order-title");
            expand.SetEnabled(routeEditingAction == null);
            var details = Text(title, null, "Up to " + action.Maximum + " · minimum " + action.Minimum + " · " + action.Shortage, "route-v3-muted route-v3-order-summary");
            details.tooltip = ItineraryFlowDescription(action.Kind);
            var tools = Panel(title, null, "route-v3-stop-tools");
            Button(tools, "itineraryActionUp" + key, "↑", () =>
            { stop.Actions.RemoveAt(actionIndex); stop.Actions.Insert(actionIndex - 1, action); stop.PreserveOrder = true; RebuildChangedItinerary(); }).SetEnabled(actionIndex > 0 && routeEditingAction == null);
            Button(tools, "itineraryActionDown" + key, "↓", () =>
            { stop.Actions.RemoveAt(actionIndex); stop.Actions.Insert(actionIndex + 1, action); stop.PreserveOrder = true; RebuildChangedItinerary(); }).SetEnabled(actionIndex + 1 < stop.Actions.Count && routeEditingAction == null);
            Button(tools, "itineraryRemoveAction" + key, "×", () =>
            { stop.Actions.RemoveAt(actionIndex); RebuildChangedItinerary(); }).SetEnabled(routeEditingAction == null);
            if (routeEditingAction != null && routeEditingIndex == actionIndex) RenderApprovedInlineEditor(row, key);
        }

        private void RenderApprovedInlineEditor(VisualElement parent, string key)
        {
            var action = routeEditingAction;
            var form = Panel(parent, "routeInlineEditor", "route-v3-inline-editor");
            Text(form, null, routeEditingIndex < 0 ? "NEW INSTRUCTION" : "EDIT INSTRUCTION", "route-v3-eyebrow");
            var primary = Panel(form, null, "route-v3-inline-fields");
            Dropdown(primary, "itineraryKind" + key, "Action", new List<string> { "Buy", "Sell", "Load", "Unload" });
            var kind = Element<DropdownField>("itineraryKind" + key); kind.SetValueWithoutNotify(kind.choices[(int)action.Kind]);
            Dropdown(primary, "itineraryGood" + key, "Good", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            var good = Element<DropdownField>("itineraryGood" + key); good.SetValueWithoutNotify(Session.Content.GetGood(action.GoodId).Name);
            good.RegisterValueChangedCallback(change => { if (change.target != good || good.index < 0) return; action.GoodId = goodIds[good.index]; ValidateApprovedInlineEditor(); });
            var maximum = ApprovedRouteField(primary, "itineraryMax" + key, "Up to / visit", action.Maximum, value => action.Maximum = value);
            var limits = Panel(form, null, "route-v3-inline-fields");
            var minimum = new Toggle("Require minimum") { name = "routeRequireMinimum", value = routeRequiresMinimum }; limits.Add(minimum);
            var minimumField = ApprovedRouteField(limits, "itineraryMin" + key, "At least", action.Minimum, value => action.Minimum = value);
            minimumField.SetEnabled(routeRequiresMinimum);
            minimum.RegisterValueChangedCallback(change =>
            {
                routeRequiresMinimum = change.newValue; minimumField.SetEnabled(change.newValue);
                if (!change.newValue) { action.Minimum = "1"; minimumField.SetValueWithoutNotify("1"); }
                ValidateApprovedInlineEditor();
            });
            var wait = new Toggle("Wait if short") { name = "routeWaitIfShort", value = action.Shortage == CargoItineraryShortagePolicy.Wait };
            limits.Add(wait); wait.tooltip = "When less than the minimum can execute, retain this instruction. Otherwise Skip advances without moving goods or money.";
            wait.RegisterValueChangedCallback(change => { action.Shortage = change.newValue ? CargoItineraryShortagePolicy.Wait : CargoItineraryShortagePolicy.Skip; ValidateApprovedInlineEditor(); });
            var advanced = new Foldout { name = "routeTechnicalLimits", text = "Price, cash and technical limits", value = false }; form.Add(advanced);
            ApprovedRouteField(advanced, "itineraryPrice" + key, "Marginal price · 0 unrestricted", action.Price, value => action.Price = value);
            ApprovedRouteField(advanced, "itineraryCashReserve" + key, "Keep company coins", action.CashReserve, value => action.CashReserve = value);
            ApprovedRouteField(advanced, "itineraryBudget" + key, "Spend at most · blank unrestricted", action.Budget, value => action.Budget = value);
            ApprovedRouteField(advanced, "itineraryWarehouseReserve" + key, "Keep warehouse units", action.WarehouseReserve, value => action.WarehouseReserve = value);
            kind.RegisterValueChangedCallback(change =>
            { if (change.target != kind || kind.index < 0) return; action.Kind = (CargoItineraryCommandKind)kind.index; RefreshItineraryFieldVisibility(key, action.Kind); ValidateApprovedInlineEditor(); });
            RefreshItineraryFieldVisibility(key, action.Kind);
            Text(advanced, "routeCurrentQuote", "", "route-v3-copy");
            Text(form, "routeInlineValidation", "", "route-v3-validation");
            var actions = Panel(form, null, "route-v3-inline-buttons");
            Button(actions, "routeCancelOrder", "Cancel", CancelApprovedRouteAction);
            Button(actions, "routeSaveOrder", "Save order", SaveApprovedRouteAction, "primary");
            ValidateApprovedInlineEditor();
            RevealApprovedRouteControl(form, maximum);
        }

        private void RevealApprovedRouteControl(VisualElement surface, VisualElement focus)
        {
            // New controls have no useful bounds until the retained layout has
            // settled. Reveal the complete compact form before focusing its
            // quantity, rather than focusing an off-screen input immediately.
            void Reveal()
            {
                if (Screen != ArchipelagoScreen.RoutePlanner || routeStage != RouteWorkspaceStage.Orders ||
                    surface.panel == null || focus.panel == null) return;
                Element<ScrollView>("itineraryScroll").ScrollTo(surface);
                focus.Focus();
                if (focus is TextField field) field.SelectAll();
            }
            EventCallback<GeometryChangedEvent> ready = null;
            ready = change =>
            {
                if (change.newRect.width <= 0 || change.newRect.height <= 0) return;
                surface.UnregisterCallback(ready);
                surface.schedule.Execute(Reveal).ExecuteLater(0);
            };
            surface.RegisterCallback(ready);
        }

        private void ReturnToApprovedRouteCell(string name)
        {
            var cell = name == null ? null : Root.Q<Button>(name);
            if (cell != null) RevealApprovedRouteControl(cell, cell);
        }

        private void CancelApprovedRouteAction()
        {
            var cell = routeEditingReturnCell;
            ClearApprovedRouteEditor(); RenderSelectedItineraryStop(); RefreshRoutePlannerStatus();
            ReturnToApprovedRouteCell(cell);
        }

        private TextField ApprovedRouteField(VisualElement parent, string name, string label, string value, Action<string> changed)
        {
            var field = new TextField(label) { name = name, value = value }; parent.Add(field);
            field.RegisterValueChangedCallback(change => { if (change.target != field) return; changed(change.newValue); ValidateApprovedInlineEditor(); });
            return field;
        }

        private void ValidateApprovedInlineEditor()
        {
            if (routeEditingAction == null || Root.Q<Button>("routeSaveOrder") == null) return;
            try
            {
                ReadItineraryAction(routeEditingAction);
                Element<Button>("routeSaveOrder").SetEnabled(true);
                SetText("routeInlineValidation", "Draft only · Apply route saves the full sailing order.");
                RefreshApprovedInlineQuote();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { Element<Button>("routeSaveOrder").SetEnabled(false); SetText("routeInlineValidation", exception.Message); }
        }

        private void RefreshApprovedInlineQuote()
        {
            var label = Root.Q<Label>("routeCurrentQuote");
            if (label == null) return;
            try
            {
                var stops = new List<CargoItineraryStop>();
                var chosenIndex = routeEditingIndex;
                for (var i = 0; i < ActiveItineraryDraft.Stops.Count; i++)
                {
                    var source = ActiveItineraryDraft.Stops[i];
                    var actions = source.Actions.Select(ReadItineraryAction).ToList();
                    if (i == selectedItineraryStop)
                    {
                        if (chosenIndex < 0) { chosenIndex = actions.Count; actions.Add(ReadItineraryAction(routeEditingAction)); }
                        else actions[chosenIndex] = ReadItineraryAction(routeEditingAction);
                    }
                    stops.Add(new CargoItineraryStop(source.PortId, actions));
                }
                var policy = new CargoItineraryPolicy(ActiveItineraryDraft.Id, stops);
                var decision = CargoItineraryEvaluator.PreviewCommand(Session.Snapshot, Session.Player.Id, policy, selectedItineraryStop, chosenIndex);
                var port = Session.Snapshot.Ports[stops[selectedItineraryStop].PortId];
                var stock = Session.Snapshot.CompanyStockpiles.Values.FirstOrDefault(value => value.OwnerId == Session.Player.OwnerId && value.PortId == port.Id);
                var good = routeEditingAction.GoodId;
                label.text = (Session.Player.DockedPortId == port.Id ? "CURRENT DOCK QUOTE" : "REMOTE / HYPOTHETICAL · actual arrival required") +
                    "\nMarket " + Q(port.Inventory[good]) + " · hold " + Q(Session.Player.Cargo[good]) + " · warehouse " + (stock == null ? "not built" : Q(stock.Inventory[good])) +
                    "\nTown buying power " + Money(port.MarketCash) + " coins · company " + Money(Session.Player.Cash) + "\n" + decision.Reason +
                    (decision.Preview == null ? "" : "\nExact total " + Money(decision.Preview.TotalAmount) + " coins" + ServiceFeeText(decision.Preview)) +
                    "\nCurrent holdings only; earlier draft orders are not simulated. Prices and availability can change.";
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { label.text = "Add valid orders at every stop to inspect an authoritative current quote. " + exception.Message; }
        }

        private void SaveApprovedRouteAction()
        {
            if (routeEditingAction == null) return;
            try
            {
                ReadItineraryAction(routeEditingAction);
                var stop = ActiveItineraryDraft.Stops[selectedItineraryStop];
                if (routeEditingIndex < 0)
                {
                    if (stop.Actions.Count >= 8 || ActiveItineraryDraft.Stops.Sum(item => item.Actions.Count) >= 24) throw new InvalidOperationException("Route instruction limit reached.");
                    stop.Actions.Add(routeEditingAction);
                    if (!stop.PreserveOrder) SortItineraryActions(stop);
                }
                else { stop.Actions[routeEditingIndex] = routeEditingAction; stop.PreserveOrder = true; }
                var cell = ItineraryCellName(selectedItineraryStop, routeEditingAction.GoodId, routeEditingAction.Kind);
                ClearApprovedRouteEditor(); RebuildChangedItinerary(); ReturnToApprovedRouteCell(cell);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { SetText("routeInlineValidation", exception.Message); }
        }
    }
}
