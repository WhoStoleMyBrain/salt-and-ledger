using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private readonly Dictionary<string, Button> fleetCommandRows = new Dictionary<string, Button>();
        private RegionalWorldSnapshot economyPresented, noticesPresented;
        private string economyPortPresented;

        private void BuildCommandWorkspaces(VisualElement main)
        {
            var fleetList = Panel(main, "fleetCommandList", "sl-paper command-fleet-list hidden");
            main.Insert(0, fleetList);
            Text(fleetList, null, "Your fleet", "sl-section-title");
            var listScroll = new ScrollView { name = "fleetCommandRows" };
            listScroll.AddToClassList("command-scroll"); fleetList.Add(listScroll);
            Button(fleetList, "fleetCompanyButton", "Company / shipwright", () => OpenSecondary(ArchipelagoScreen.Development));
            var inspector = Panel(main, "fleetCommandInspector", "sl-paper command-fleet-inspector hidden");
            var scroll = new ScrollView { name = "fleetCommandScroll" };
            scroll.AddToClassList("command-scroll"); inspector.Add(scroll);
            Text(scroll, "fleetCommandTitle", "", "sl-section-title");
            Text(scroll, "fleetCommandLocation", "", "sl-metric");
            Text(scroll, "fleetCommandCargo", "", "command-copy");
            Text(scroll, "fleetCommandStatus", "", "sl-problem");
            Text(scroll, "fleetCommandRoute", "", "command-copy");
            Text(scroll, "fleetCommandNext", "", "sl-card command-copy");
            Text(scroll, "fleetCommandLast", "", "command-muted");
            Button(inspector, "fleetEditRouteButton", "Inspect / edit route", OpenSelectedShipRoute, "primary");
            Button(inspector, "fleetHarborButton", "Open docked Harbor", () => OpenTrade(Session.Snapshot.Ships[Session.SelectedShipId ?? Session.Player.Id].DockedPortId));
            Button(inspector, "fleetBackButton", "Back to map", () => SwitchCommandMode(CommandMode.Map));

            var economy = Panel(main, "economyWorkspace", "sl-paper command-economy hidden");
            var heading = Panel(economy, null, "command-economy-heading");
            Text(heading, "economyWorkspaceTitle", "Town economy", "sl-section-title");
            var towns = new DropdownField("Town") { name = "economyTownPicker" };
            heading.Add(towns);
            towns.RegisterValueChangedCallback(change =>
            {
                if (change.target != towns || towns.index < 0 || towns.index >= portIds.Count) return;
                SelectCommandPort(portIds[towns.index]);
            });
            var economyScroll = new ScrollView { name = "economyWorkspaceScroll" };
            economyScroll.AddToClassList("command-scroll"); economy.Add(economyScroll);
            var metrics = Panel(economyScroll, null, "command-economy-metrics");
            Text(metrics, "economyFoodFact", "", "sl-metric command-economy-metric");
            Text(metrics, "economyCashFact", "", "sl-metric command-economy-metric");
            Text(metrics, "economyPeopleFact", "", "sl-metric command-economy-metric");
            Text(economyScroll, "economyNeedDetail", "", "sl-card command-copy");
            Text(economyScroll, null, "Next production opportunities", "sl-section-title");
            Text(economyScroll, null, "Current stock and funds, one canonical batch. Earlier ships, procurement and other facilities can change these facts before the attempt.", "command-muted");
            Panel(economyScroll, "economyProductionRows", "");
            var actions = Panel(economy, null, "row run-actions");
            Button(actions, "economyDetailsButton", "Full city accounts / pricing", () => OpenSecondary(ArchipelagoScreen.CityEconomy));
            Button(actions, "economyLedgerButton", "Company ledger", () => OpenSecondary(ArchipelagoScreen.Ledger));
            Button(actions, "economyHarborButton", "Harbor", () => OpenTrade(Session.SelectedPortId), "primary");
            Button(actions, "economyBackButton", "Back to map", () => SwitchCommandMode(CommandMode.Map));
        }

        private void RefreshCommandWorkspaces()
        {
            var snapshot = Session.Snapshot;
            foreach (var ship in snapshot.Ships.Values.Where(s => s.IsPlayer).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                if (!fleetCommandRows.TryGetValue(ship.Id, out var row))
                {
                    var id = ship.Id;
                    row = Button(Element<ScrollView>("fleetCommandRows"), "fleetSelect_" + id, "", () => SelectCommandShip(id), "sl-card command-fleet-row");
                    fleetCommandRows.Add(id, row);
                }
                row.text = ship.Name + "\n" + (ship.DockedPortId == null ? "To " + PortName(ship.DestinationPortId) + " · " + ship.RemainingTravelDays + "d" :
                    PortName(ship.DockedPortId)) + "\n" + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) + " cargo";
                row.EnableInClassList("sl-selected", ship.Id == Session.SelectedShipId);
            }
            foreach (var id in fleetCommandRows.Keys.Where(id => !snapshot.Ships.ContainsKey(id)).ToArray())
            { fleetCommandRows[id].RemoveFromHierarchy(); fleetCommandRows.Remove(id); }
            var selected = snapshot.Ships[Session.SelectedShipId ?? Session.Player.Id];
            SetText("fleetCommandTitle", selected.Name + (selected.IsPlayer ? "" : " · NPC"));
            SetText("fleetCommandLocation", selected.DockedPortId != null ? "Docked · " + PortName(selected.DockedPortId) :
                PortName(selected.OriginPortId) + " → " + PortName(selected.DestinationPortId) + "\n" + selected.RemainingTravelDays +
                " days · arrival D" + checked(snapshot.Day + selected.RemainingTravelDays));
            SetText("fleetCommandCargo", "Hold " + Q(selected.CargoUnits) + " / " + Q(selected.CargoCapacity) + "\n" +
                (selected.CargoUnits == 0 ? "Empty" : string.Join(" · ", goodIds.Where(id => selected.Cargo[id] > 0)
                    .Select(id => Session.Content.GetGood(id).Name + " " + Q(selected.Cargo[id])))));
            SetText("fleetCommandStatus", (selected.AutomationEnabled ? "Automation active" : "Manual control") +
                (selected.IsPlayer ? "" : " · inspection only"));
            SetText("fleetCommandNext", ShipInstruction(selected));
            Element<Label>("fleetCommandStatus").EnableInClassList("sl-warning", ShipBlocked(selected));
            var itinerary = selected.Itinerary;
            var progress = selected.ItineraryProgress;
            SetText("fleetCommandRoute", itinerary == null ? selected.IndustrialPolicy != null ? "Industrial route\n" +
                PortName(selected.IndustrialPolicy.OriginPortId) + " ↔ " + PortName(selected.IndustrialPolicy.DestinationPortId) :
                UsesItineraries && selected.IsPlayer ? "No itinerary assigned" : selected.Policy == null ? "No route assigned" :
                "Trade route\n" + PortName(selected.Policy.OriginPortId) + " ↔ " + PortName(selected.Policy.DestinationPortId) :
                string.Join("\n", itinerary.Stops.Select((stop, i) => (i == progress.StopIndex ? "› " : "") + (i + 1) + ". " +
                    PortName(stop.PortId) + " · " + stop.Commands.Count + " orders")));
            SetText("fleetCommandLast", progress == null ? "" : "Committed cursor: stop " + (progress.StopIndex + 1) +
                (progress.ActionIndex < itinerary.Stops[progress.StopIndex].Commands.Count ? ", order " + (progress.ActionIndex + 1) : ", departure pending") + " · pass " + progress.Cycle +
                "\nDaily work " + (progress.OperationsDay == snapshot.Day ? progress.OperationsUsed : 0) + "/8" +
                "\nLast committed: " + progress.LastDecision + " · " + progress.LastReason);
            Element<Button>("fleetEditRouteButton").SetEnabled(selected.IsPlayer);
            Element<Button>("fleetHarborButton").SetEnabled(selected.DockedPortId != null);
            if (ReferenceEquals(economyPresented, snapshot) && economyPortPresented == Session.SelectedPortId) return;
            economyPresented = snapshot; economyPortPresented = Session.SelectedPortId;
            RefreshEconomyWorkspace(snapshot.Ports[Session.SelectedPortId]);
        }

        private void RefreshEconomyWorkspace(RegionalPortSnapshot port)
        {
            var need = EconomicSignals.NextEssentialNeed(Session.Snapshot, port.Id);
            var city = port.City;
            SetText("economyWorkspaceTitle", port.Name + " · " + ShortRole(port.Role));
            var townPicker = Element<DropdownField>("economyTownPicker");
            townPicker.choices = portIds.Select(PortName).ToList();
            townPicker.SetValueWithoutNotify(PortName(port.Id));
            SetText("economyFoodFact", !need.Modeled ? "Essential need\nNot modeled" : !need.HasNeed ? "Essential need\nNone scheduled" :
                "Bread · due D" + need.NextDueDay + "\n" + Q(need.MarketStock) + " stock / " + Q(need.DesiredQuantity) + " need");
            SetText("economyCashFact", "Market cash " + Money(port.MarketCash) + "\nHousehold cash " +
                (city == null ? "Not modeled" : Money(city.HouseholdCash)));
            SetText("economyPeopleFact", city == null ? "People & prosperity\nNot modeled" : "Population " + city.Population +
                (city.PopulationDelta == 0 ? " · stable" : city.PopulationDelta > 0 ? " · growing" : " · declining") +
                "\nProsperity " + city.Prosperity + "/100");
            SetText("economyNeedDetail", !need.Modeled ? "This historical world retains its original physical demand. It has no household account." : !need.HasNeed ?
                "No essential household rule is configured." : (need.HasObservation ? "Next opportunity" : "Awaiting first need") +
                " · current stock shortfall " + Q(need.StockShortfall) + " Bread." +
                (need.PreviewAvailable ? "\nAt current quotes and funds, households could purchase " + Q(need.AffordableQuantity.Value) + " for " + Money(need.CurrentPayment.Value) +
                " coins. Available-stock affordability shortfall " + Q(need.AffordabilityShortfall.Value) + "." :
                "\nRetail preview unavailable: receiving account numeric limit. This is not household poverty.") +
                "\nAt current population " + need.Population + ", demand carry " + need.DemandRemainder + " → " + need.NextDemandRemainder +
                ". This is a current-state comparison; intervening production, purchases and price changes are not forecast.");
            var rows = Element<VisualElement>("economyProductionRows");
            rows.Clear();
            foreach (var production in TownProduction(port.Id))
            {
                var card = Panel(rows, "productionSignal_" + production.FacilityId, "sl-card command-production-card");
                Text(card, null, ProductionName(production) + (production.IsPrivate ? " · company stock" : " · public market"), "sl-section-title");
                Text(card, null, ReadinessLabel(production) + " · next eligible attempt D" + production.NextEligibleDay,
                    "sl-problem" + (IsProductionProblem(production) ? " sl-warning" : ""));
                Text(card, null, ProductionDetails(production), "command-copy");
                var id = production.FacilityId;
                Button(card, "productionAct_" + id, production.IsPrivate ? "Manage facility" : "Inspect public inputs in Harbor", () =>
                {
                    if (!production.IsPrivate) { OpenTrade(port.Id); return; }
                    selectedFacilityId = id; loadedPolicyFacilityId = null;
                    Session.Pause(); ShowModal("industryBackdrop", "closeIndustryButton"); RefreshIndustry();
                });
            }
            if (rows.childCount == 0) Text(rows, null, "No configured facilities at this town.", "command-copy");
        }

        private IEnumerable<ProductionOpportunitySignal> TownProduction(string portId)
        {
            var snapshot = Session.Snapshot;
            foreach (var facility in snapshot.Ports[portId].Facilities)
                yield return EconomicSignals.PublicProduction(snapshot, Session.Content, portId, facility.Configuration.Instance.Id);
            foreach (var facility in snapshot.PrivateFacilities.Values.Where(f => f.PortId == portId && f.OwnerId == Session.Player.OwnerId)
                .OrderBy(f => f.Id, StringComparer.Ordinal))
                yield return EconomicSignals.PrivateProduction(snapshot, Session.Content, facility.Id);
        }

        private ProductionOpportunitySignal PriorityProduction(string portId) => TownProduction(portId)
            .OrderByDescending(IsProductionProblem).ThenBy(p => p.NextEligibleDay).ThenBy(p => p.FacilityId, StringComparer.Ordinal).FirstOrDefault();

        private static bool IsProductionProblem(ProductionOpportunitySignal signal) => signal != null && signal.Enabled &&
            !signal.UnderConstruction && (signal.CurrentReadiness == ScheduledProductionOutcome.InputShortage ||
            signal.CurrentReadiness == ScheduledProductionOutcome.PayrollShortage || signal.CurrentReadiness == ScheduledProductionOutcome.WarehouseCapacity ||
            signal.CurrentReadiness == ScheduledProductionOutcome.OutputLimit);

        private string ProductionName(ProductionOpportunitySignal signal) => Session.Content.GetBuilding(signal.BuildingId).Name;

        private static string ReadinessLabel(ProductionOpportunitySignal signal)
        {
            if (signal.UnderConstruction) return "Under construction";
            if (!signal.Enabled) return "Disabled";
            if (!signal.HasNumericHeadroom) return "Numeric stock limit";
            switch (signal.CurrentReadiness)
            {
                case ScheduledProductionOutcome.Produced: return "Ready from current state";
                case ScheduledProductionOutcome.InputShortage: return "Missing inputs";
                case ScheduledProductionOutcome.PayrollShortage: return "Payroll short";
                case ScheduledProductionOutcome.OutputLimit: return "Output ceiling";
                case ScheduledProductionOutcome.WarehouseCapacity: return "Storage full";
                case ScheduledProductionOutcome.UnderConstruction: return "Under construction";
                case ScheduledProductionOutcome.Disabled: return "Disabled";
                default: return "Not due";
            }
        }

        private string ProductionDetails(ProductionOpportunitySignal signal) =>
            (signal.Inputs.Count == 0 ? "Extraction" : string.Join(" + ", signal.Inputs.Select(i => Q(i.Quantity) + " " +
                Session.Content.GetGood(i.GoodId).Name + " (stock " + Q(i.CurrentStock) + ")"))) + " → " +
            string.Join(" + ", signal.Outputs.Select(o => Q(o.Quantity) + " " + Session.Content.GetGood(o.GoodId).Name)) +
            "\nOne potential batch; employer cash " + Money(signal.EmployerCash) + " / wage " + Money(signal.WagePerBatch) +
            (signal.StorageCapacity.HasValue ? "; storage " + Q(signal.StoredUnits) + "/" + Q(signal.StorageCapacity.Value) : "") +
            (signal.OutputStockLimit.HasValue ? "; output ceiling " + Q(signal.OutputStockLimit.Value) : "") +
            ".\nLast committed outcome: " + ArchipelagoLabSession.FacilityStatus(signal.LastOutcome) + ". " +
            (signal.UsesSharedStock ? "Shared inputs may be used by another local facility. " : "") +
            "Would produce only if enabled, funded and inputs/space remain at the attempt.";

        private string TownReason(EssentialNeedSignal need, ProductionOpportunitySignal production)
        {
            if (need.StockShortfall > 0) return "Food stock short by " + Q(need.StockShortfall) + " Bread for D" + need.NextDueDay + " · inspect Harbor";
            if (need.AffordabilityShortfall > 0) return "Household funds short for " + Q(need.AffordabilityShortfall.Value) + " available Bread · inspect Economy";
            if (need.NumericCapacityBlocked) return "Retail preview unavailable · receiving account numeric limit";
            if (IsProductionProblem(production))
            {
                var missing = production.Inputs.FirstOrDefault(i => i.Shortfall > 0);
                return ProductionName(production) + (missing != null ? " needs " + Q(missing.Shortfall) + " " + Session.Content.GetGood(missing.GoodId).Name :
                    " · " + ReadinessLabel(production)) + (production.IsPrivate ? " · manage Industry" : " · inspect public supply in Harbor");
            }
            return need.Modeled && need.HasNeed && !need.HasObservation ? "Awaiting first need · current food stock covers the next request" :
                "No immediate stock or production blocker in the current state";
        }

        private string ShipInstruction(RegionalShipSnapshot ship)
        {
            if (ship.Itinerary != null)
            {
                var decision = CargoItineraryEvaluator.Evaluate(Session.Snapshot, ship.Id);
                var progress = ship.ItineraryProgress;
                var stop = ship.Itinerary.Stops[progress.StopIndex];
                var order = progress.ActionIndex < stop.Commands.Count ? stop.Commands[progress.ActionIndex] : null;
                var next = order == null ? "Depart after completed stop" : ItineraryVerb(order.Kind) + " up to " + Q(order.MaximumQuantity) + " " + Session.Content.GetGood(order.GoodId).Name;
                return "Next: " + next + " · " + PortName(stop.PortId) + "\n" + decision.Reason;
            }
            return UsesItineraries && ship.IsPlayer ? "Use Route planner to draft and apply an itinerary." : DecisionText(Session.DecisionFor(ship.Id));
        }

        private bool ShipBlocked(RegionalShipSnapshot ship)
        {
            if (!ship.AutomationEnabled || ship.DockedPortId == null) return false;
            if (ship.Itinerary != null)
            {
                var decision = CargoItineraryEvaluator.Evaluate(Session.Snapshot, ship.Id);
                return decision.Kind == CargoItineraryDecisionKind.Wait || decision.Kind == CargoItineraryDecisionKind.Paused;
            }
            return Session.DecisionFor(ship.Id).Action == AutomationAction.Wait;
        }

        private void ToggleCommandNotices()
        {
            Session.Pause();
            var panel = Element<VisualElement>("commandNotices");
            panel.EnableInClassList("hidden", !panel.ClassListContains("hidden"));
            if (!panel.ClassListContains("hidden")) Element<Button>("noticesCloseButton").Focus();
            else Element<Button>("noticesButton").Focus();
            RefreshCommandDeck();
        }

        private void RefreshCommandNotices()
        {
            if (ReferenceEquals(noticesPresented, Session.Snapshot)) return;
            noticesPresented = Session.Snapshot;
            var notices = new List<Tuple<string, Action>>();
            foreach (var port in Session.Snapshot.Ports.Values.OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                var need = EconomicSignals.NextEssentialNeed(Session.Snapshot, port.Id);
                var production = PriorityProduction(port.Id);
                if (need.StockShortfall <= 0 && !(need.AffordabilityShortfall > 0) && !IsProductionProblem(production)) continue;
                var id = port.Id;
                notices.Add(Tuple.Create(port.Name + " · " + TownReason(need, production), (Action)(() =>
                { SelectCommandPort(id); SwitchCommandMode(CommandMode.Economy); })));
            }
            foreach (var ship in Session.Snapshot.Ships.Values.Where(s => s.IsPlayer && ShipBlocked(s)))
            {
                var id = ship.Id;
                notices.Add(Tuple.Create(ship.Name + " · " + ShipInstruction(ship), (Action)(() =>
                { SelectCommandShip(id); SwitchCommandMode(CommandMode.Fleet); })));
            }
            Element<Button>("noticesButton").text = notices.Count == 0 ? "All quiet" : "Notices " + notices.Count;
            var rows = Element<VisualElement>("commandNoticeRows"); rows.Clear();
            for (var i = 0; i < notices.Count; i++)
            {
                var notice = Button(rows, "commandNotice" + i, "", notices[i].Item2, "sl-problem sl-warning row");
                var glyph = new MaritimeGlyph("attention"); glyph.AddToClassList("sl-glyph-small"); notice.Add(glyph);
                Text(notice, null, notices[i].Item1, "command-copy");
            }
            if (notices.Count == 0) Text(rows, null, "No current actionable food, production or owned-route blocker.", "command-copy");
        }
    }
}
