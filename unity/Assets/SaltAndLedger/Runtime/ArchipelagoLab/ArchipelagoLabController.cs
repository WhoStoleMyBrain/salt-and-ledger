using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class ArchipelagoLabController : MonoBehaviour
    {
        private readonly List<string> goodIds = new List<string>();
        private readonly List<string> portIds = new List<string>();
        private readonly List<string> shipIds = new List<string>();
        private string status;
        private bool statusIsError;
        private string policyShipId;

        public ArchipelagoLabSession Session { get; private set; }
        public VisualElement Root { get; private set; }
        public ArchipelagoMap Map { get; private set; }
        public ArchipelagoTextScale TextScale { get; private set; }

        private void Start()
        {
            Root = GetComponent<UIDocument>().rootVisualElement;
            TextScale = new ArchipelagoTextScale(Root);
            try
            {
                var canonical = Resources.Load<TextAsset>("CanonicalContent");
                if (canonical == null)
                    throw new InvalidOperationException("Canonical content is missing. Run python tools/prepare_unity.py.");
                Session = new ArchipelagoLabSession(ContentLoader.Load(canonical.text));
                Map = new ArchipelagoMap();
                Element<VisualElement>("mapContainer").Add(Map);
                Map.PortSelected += id => Run(() => Session.SelectPort(id));
                Map.ShipSelected += id => Run(() => Session.SelectShip(id));
                Map.PortTradeRequested += OpenTrade;
                Bind();
                BindRunExperience();
                LoadPolicy();
                Refresh();
                ShowScreen(ArchipelagoScreen.MainMenu);
                Debug.Log("Salt & Ledger Archipelago Lab ready: day 0, " + Session.Snapshot.Ports.Count + " ports, " + Session.Snapshot.Ships.Count + " ships.");
            }
            catch (Exception exception) { ReportException(exception); }
        }

        private void Update()
        {
            if (Session == null || Map == null) return;
            try
            {
                if (Session.AdvancePlayback(Time.unscaledDeltaTime) > 0)
                {
                    status = Session.LastStatus;
                    Refresh();
                }
                Map.AdvanceVisual(Time.unscaledDeltaTime, Session.IsPlaying && SmoothMovement);
            }
            catch (Exception exception)
            {
                Session.Pause();
                ReportException(exception);
            }
        }

        private void Bind()
        {
            var goods = Session.Content.Goods.OrderBy(good => good.Id, StringComparer.Ordinal).ToArray();
            goodIds.AddRange(goods.Select(good => good.Id));
            portIds.AddRange(Session.Snapshot.Ports.Keys.OrderBy(id => id, StringComparer.Ordinal));
            shipIds.AddRange(Session.Snapshot.Ships.Keys.OrderBy(id => id, StringComparer.Ordinal));
            SetChoices("shipPicker", new[] { "Inspect ship…" }.Concat(shipIds.Select(id => ShipChoice(Session.Snapshot.Ships[id]))).ToList());
            Element<DropdownField>("shipPicker").RegisterValueChangedCallback(_ =>
            {
                var index = Element<DropdownField>("shipPicker").index;
                if (index > 0) Run(() => Session.SelectShip(shipIds[index - 1]));
            });
            var goodNames = goods.Select(good => good.Name).ToList();
            foreach (var name in new[] { "goodDropdown", "outGoodDropdown" })
                SetChoices(name, goodNames);
            SetChoices("returnGoodDropdown", new[] { "None" }.Concat(goodNames).ToList());
            var portNames = portIds.Select(id => Session.Snapshot.Ports[id].Name).ToList();
            SetChoices("routeOriginDropdown", portNames);
            SetChoices("routeDestinationDropdown", portNames);
            SetChoices("speedDropdown", new List<string> { "1×", "3×", "10×" });
            Element<DropdownField>("speedDropdown").RegisterValueChangedCallback(_ =>
                Run(() => Session.SetPlaybackSpeed(new[] { 1, 3, 10 }[Math.Max(0, Element<DropdownField>("speedDropdown").index)])));
            Element<DropdownField>("goodDropdown").RegisterValueChangedCallback(_ => Run(RefreshPreviews, false));
            Element<TextField>("quantityField").RegisterValueChangedCallback(_ => Run(RefreshPreviews, false));
            Click("playButton", () => Run(Session.Play));
            Click("pauseButton", () => Run(Session.Pause));
            Click("nextDayButton", () => Run(() => Session.NextDay()));
            Click("playerShipButton", () => Run(() => Session.SelectShip(Session.Player.Id)));
            Click("resetButton", RequestRestart);
            Click("sailButton", () => Run(() => Session.Sail()));
            Click("buyButton", () => Trade(TradeDirection.Buy));
            Click("sellButton", () => Trade(TradeDirection.Sell));
            Click("applyPolicyButton", ApplyPolicy);
            Click("presetButton", () => Run(() => { Session.ApplyPolicy(Session.RecommendedMerchantPolicy()); LoadPolicy(); }));
            Click("runDocksideButton", () => Run(() => Session.RunDockside()));
            Element<Toggle>("automationToggle").RegisterValueChangedCallback(change =>
                Run(() => Session.SetAutomationEnabled(change.newValue)));
            BindOverlays();
            SetIndex("goodDropdown", goodIds.IndexOf("logs"));
            foreach (var name in new[] { "routeOriginDropdown", "routeDestinationDropdown", "outGoodDropdown", "returnGoodDropdown" })
                Element<DropdownField>(name).RegisterValueChangedCallback(_ => DraftChanged());
            foreach (var prefix in new[] { "out", "return" })
                foreach (var suffix in new[] { "MaxQty", "MinQty", "Buy", "Sell", "Budget" })
                    Element<TextField>(prefix + suffix).RegisterValueChangedCallback(_ => DraftChanged());
            Element<TextField>("cashReserveField").RegisterValueChangedCallback(_ => DraftChanged());
            Element<Toggle>("emptyReturnToggle").RegisterValueChangedCallback(_ => DraftChanged());
        }

        private void SetChoices(string name, List<string> choices)
        {
            var field = Element<DropdownField>(name);
            field.choices = choices;
            field.SetValueWithoutNotify(choices[0]);
        }

        private void DraftChanged()
        {
            SetText("policyDraftLabel", "Draft changed. Apply to use these settings; editing spends no day.");
            Element<VisualElement>("returnFields").SetEnabled(Element<DropdownField>("returnGoodDropdown").index > 0);
        }

        private void LoadPolicy()
        {
            policyShipId = Session.Player.Id;
            var policy = Session.Policy;
            SetIndex("routeOriginDropdown", portIds.IndexOf(policy.OriginPortId));
            SetIndex("routeDestinationDropdown", portIds.IndexOf(policy.DestinationPortId));
            SetIndex("outGoodDropdown", goodIds.IndexOf(policy.Outbound.GoodId));
            LoadLeg("out", policy.Outbound);
            SetIndex("returnGoodDropdown", policy.ReturnLeg == null ? 0 : goodIds.IndexOf(policy.ReturnLeg.GoodId) + 1);
            LoadLeg("return", policy.ReturnLeg ?? new RouteLeg(goodIds.First(id => id != policy.Outbound.GoodId), Session.Snapshot.QuantityScale, Session.Snapshot.QuantityScale, 1, 1));
            Element<VisualElement>("returnFields").SetEnabled(policy.ReturnLeg != null);
            Element<TextField>("cashReserveField").SetValueWithoutNotify(Money(policy.CashReserve));
            Element<Toggle>("emptyReturnToggle").SetValueWithoutNotify(policy.AllowEmptyReturn);
            SetText("policyDraftLabel", "Applied route. Limits use the worst marginal unit and the exact full purchase total.");
        }

        private void LoadLeg(string prefix, RouteLeg leg)
        {
            Element<TextField>(prefix + "MaxQty").SetValueWithoutNotify(Q(leg.MaxPurchaseQuantity));
            Element<TextField>(prefix + "MinQty").SetValueWithoutNotify(Q(leg.MinShipmentQuantity));
            Element<TextField>(prefix + "Buy").SetValueWithoutNotify(Number(leg.MaxBuyPrice));
            Element<TextField>(prefix + "Sell").SetValueWithoutNotify(Number(leg.MinSellPrice));
            Element<TextField>(prefix + "Budget").SetValueWithoutNotify(
                leg.MaxPurchaseExpenditure == long.MaxValue ? "" : Money(leg.MaxPurchaseExpenditure));
        }

        private void SetIndex(string name, int index)
        {
            var field = Element<DropdownField>(name);
            field.SetValueWithoutNotify(field.choices[Math.Max(0, index)]);
        }

        private void ApplyPolicy()
        {
            try
            {
                var origin = portIds[Element<DropdownField>("routeOriginDropdown").index];
                var destination = portIds[Element<DropdownField>("routeDestinationDropdown").index];
                if (origin == destination || !Session.HasRoute(origin, destination))
                    throw new ArgumentException("Choose two different ports joined by a sea route.");
                var outbound = ReadLeg("out", goodIds[Element<DropdownField>("outGoodDropdown").index]);
                var returnIndex = Element<DropdownField>("returnGoodDropdown").index;
                var returnLeg = returnIndex == 0 ? null : ReadLeg("return", goodIds[returnIndex - 1]);
                if (returnLeg != null && returnLeg.GoodId == outbound.GoodId)
                    throw new ArgumentException("Choose different outbound and return goods, or choose None for return.");
                var emptyReturn = Element<Toggle>("emptyReturnToggle").value;
                if (returnLeg == null && !emptyReturn)
                    throw new ArgumentException("A route without return purchases must allow an empty return.");
                var reserve = checked(ReadLong("cashReserveField", "Cash reserve", true) * Session.Snapshot.MoneyScale);
                Run(() =>
                {
                    Session.ApplyPolicy(new TradeRoutePolicy(origin, destination, outbound, returnLeg, reserve, emptyReturn));
                    LoadPolicy();
                });
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException) { Validation(exception.Message); }
        }

        private RouteLeg ReadLeg(string prefix, string goodId)
        {
            var maximum = ReadLong(prefix + "MaxQty", "Maximum shipment", false);
            var minimum = ReadLong(prefix + "MinQty", "Minimum shipment", false);
            if (maximum > int.MaxValue || minimum > maximum)
                throw new ArgumentException("Shipment sizes must fit a positive integer; minimum cannot exceed maximum.");
            var buy = ReadLong(prefix + "Buy", "Maximum buying price", false);
            var sell = ReadLong(prefix + "Sell", "Minimum selling price", false);
            var budget = string.IsNullOrWhiteSpace(Element<TextField>(prefix + "Budget").value)
                ? long.MaxValue : checked(ReadLong(prefix + "Budget", "Purchase expenditure cap", false) * Session.Snapshot.MoneyScale);
            return new RouteLeg(goodId, Session.WholeOrderQuantity((int)maximum), Session.WholeOrderQuantity((int)minimum), buy, sell, budget);
        }

        private long ReadLong(string name, string label, bool allowZero)
        {
            if (!long.TryParse(Element<TextField>(name).value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
                value < (allowZero ? 0 : 1))
                throw new ArgumentException(label + " must be a " + (allowZero ? "nonnegative" : "positive") + " whole number.");
            return value;
        }

        private void Trade(TradeDirection direction)
        {
            Run(() =>
            {
                var amount = ReadIndustryQuantity("quantityField", "Quantity", false);
                if (HarborUsesLocalStock)
                {
                    if (harborFacilityId == null) throw new InvalidOperationException("Construct a local facility to create company storage.");
                    Session.WarehouseTrade(harborFacilityId, direction, SelectedGoodId(), amount, tradeSnapshot);
                }
                else
                {
                    if (tradeShipId != Session.Player.Id) throw new InvalidOperationException("Select an owned ship to trade its cargo.");
                    if (amount % Session.Snapshot.QuantityScale != 0) throw new ArgumentException("Ship orders require whole goods. Use company local stock for fractional trades.");
                    Session.Trade(direction, SelectedGoodId(), amount, tradeSnapshot);
                }
            });
            RefreshTradeOverlay();
        }

        private void Refresh()
        {
            RefreshRunChoices();
            var snapshot = Session.Snapshot;
            var player = Session.Player;
            if (policyShipId != player.Id) LoadPolicy();
            SetText("dayLabel", "DAY " + Number(snapshot.Day));
            SetText("cashLabel", "Cash " + Money(player.Cash) + "  ·  cargo " + Q(player.CargoUnits) + "/" + Q(player.CargoCapacity));
            if (snapshot.Economy != null)
            {
                var value = snapshot.CompanyValue(player.OwnerId);
                SetText("cashLabel", "Cash " + Money(player.Cash) + " · Value " + Money(value.Total));
                Element<Label>("cashLabel").tooltip = "Reference Company Value; not liquidation proceeds. Change " + SignedMoney(value.Change) +
                    " · surplus after wages" + (snapshot.Circulation == null ? " " : " and fees ") +
                    SignedMoney(snapshot.Circulation == null ? snapshot.Accounting.Companies[player.OwnerId].OperatingSurplusAfterWages :
                        snapshot.Accounting.Companies[player.OwnerId].OperatingSurplusAfterExpenses) + ". Open Company or Ledger for the breakdown.";
            }
            else Element<Label>("cashLabel").tooltip = "";
            SetText("playbackLabel", Session.IsPlaying ? "Playing " + Session.PlaybackSpeed + "×" : "Paused");
            Element<Button>("playButton").SetEnabled(!Session.IsPlaying && !Session.IsChallengeEnded);
            Element<Button>("nextDayButton").SetEnabled(!Session.IsChallengeEnded);
            Element<Button>("pauseButton").SetEnabled(Session.IsPlaying);
            Map.Present(snapshot, Session.SelectedPortId, Session.SelectedShipId, Session.IsPlaying && SmoothMovement, Session.PlaybackInterval);
            Map.AdvanceVisual(0, Session.IsPlaying && SmoothMovement);
            SetIndex("shipPicker", Session.SelectedShipId == null ? 0 : shipIds.IndexOf(Session.SelectedShipId) + 1);
            SetText("mapHint", Session.NavigationArmed
                ? "Your ship selected · right-click a town to trade; left-click to plan a voyage."
                : "Select an island to inspect its economy. Select a ship to inspect cargo and voyages.");
            var isShip = Session.SelectedShipId != null;
            Element<VisualElement>("portContext").EnableInClassList("hidden", isShip);
            Element<VisualElement>("shipContext").EnableInClassList("hidden", !isShip);

            RefreshPort();
            if (isShip) RefreshShip(snapshot.Ships[Session.SelectedShipId]);
            RefreshPreviews();
            RefreshIndustry();
            RefreshChallenge();
            RefreshNavigation();
            RefreshPolicy();
            RefreshActivity();
            SetText("statusLabel", status ?? Session.LastStatus);
            Element<Label>("statusLabel").EnableInClassList("error", statusIsError);
            RefreshModalStatus();
            RefreshRunExperience();
            RefreshCityEconomy();
            RefreshContextLayout();
            RefreshDevelopment();
        }

        private void RefreshNavigation()
        {
            var ship = Session.Player;
            var destination = Session.PendingDestinationId;
            var text = ship.DockedPortId != null ? "Your ship: docked at " + PortName(ship.DockedPortId) :
                "Your ship: sailing to " + PortName(ship.DestinationPortId) + " · " +
                ship.RemainingTravelDays + "/" + ship.VoyageDurationDays + " days remain";
            if (destination != null && ship.DockedPortId != null)
            {
                var route = Session.Snapshot.GetRoute(ship.DockedPortId, destination);
                text += "\nProposed: " + PortName(destination) + " · " + route.Distance + " distance units · " +
                    Session.Snapshot.TravelDays(ship.Id, destination) + " days at speed " + ship.Configuration.Speed + ".";
            }
            SetText("voyageLabel", text);
            var sail = Element<Button>("sailButton");
            sail.text = destination == null ? "Confirm voyage" : "Sail to " + PortName(destination);
            sail.SetEnabled(destination != null && ship.DockedPortId != null &&
                destination != ship.DockedPortId && !ship.AutomationEnabled && !Session.IsChallengeEnded);
            sail.EnableInClassList("hidden", destination == null);
        }

        private void RefreshPort()
        {
            var port = Session.Snapshot.Ports[Session.SelectedPortId];
            SetText("selectionTitle", port.Name);
            SetText("selectionSubtitle", port.Role);
            SetText("marketCashLabel", "Market cash " + Money(port.MarketCash));
            var rows = Element<VisualElement>("marketRows");
            rows.Clear();
            foreach (var goodId in goodIds)
            {
                var price = Session.Snapshot.GetMarketQuote(port.Id, goodId);
                var row = new VisualElement();
                row.AddToClassList("commodity-row");
                var name = new Label(Session.Content.GetGood(goodId).Name) { tooltip = Session.Content.GetGood(goodId).Description };
                name.AddToClassList("commodity-name");
                row.Add(name);
                AddCell(row, Q(port.Inventory[goodId]));
                AddCell(row, price.Bid.HasValue ? Number(price.Bid.Value) : "—");
                AddCell(row, price.Ask.HasValue ? Number(price.Ask.Value) : "—");
                rows.Add(row);
            }
            var docked = Session.Snapshot.Ships.Values.Where(ship => ship.DockedPortId == port.Id).ToArray();
            SetText("dockedLabel", docked.Length == 0 ? "No ships docked." : "Docked: " +
                string.Join(", ", docked.Select(ship => ship.Name + (ship.IsPlayer ? " (you)" : " (NPC)"))));
            var industries = new List<string>();
            foreach (var facility in port.Facilities)
            {
                var configuration = facility.Configuration;
                var building = configuration.Instance;
                var recipe = Session.Content.GetRecipe(building.RecipeId);
                var inputs = recipe.Inputs.Count == 0 ? "Natural extraction; no tradeable inputs" : Quantities(recipe.Inputs);
                var availability = recipe.Inputs.Count == 0 ? "" : "\nInputs now: " +
                    string.Join(", ", recipe.Inputs.Select(input => Q(port.Inventory[input.GoodId]) + "/" + input.Quantity +
                        " " + Session.Content.GetGood(input.GoodId).Name));
                industries.Add(Session.Content.GetBuilding(building.BuildingId).Name + " · " + recipe.Name + "\n" +
                    inputs + " → " + Quantities(recipe.Outputs) + "\nEvery " + configuration.IntervalDays +
                    " days · " + (facility.NextDueDay <= Session.Snapshot.Day
                        ? "due since day " + facility.NextDueDay + "; retries on the next day when enabled"
                        : "next opportunity day " + facility.NextDueDay) +
                    (configuration.OutputStockLimit.HasValue ? " · output ceiling " + Q(configuration.OutputStockLimit.Value) : "") +
                    "\n" + (facility.Enabled ? ArchipelagoLabSession.FacilityStatus(facility.LastOutcome) : "Disabled") + availability);
            }
            SetText("productionLabel", industries.Count == 0 ? "No production facilities." : string.Join("\n\n", industries));
            var consumption = port.Consumption.Select(rule => Q(rule.Configuration.PreviewDemand(port.City?.Population ?? 0, rule.DemandRemainder).Quantity) + " " +
                Session.Content.GetGood(rule.Configuration.GoodId).Name + " every " + rule.Configuration.IntervalDays +
                " days · next day " + rule.NextDueDay + "\nLast unmet " + Q(rule.LastUnmetQuantity) +
                " · cumulative unmet " + Q(rule.TotalUnmetQuantity)).ToArray();
            SetText("consumptionLabel", consumption.Length == 0 ? "No recurring local consumption." : string.Join("\n\n", consumption) +
                (port.City == null ? "\nHistorical physical consumption removes stock; household payments are not modeled."
                    : "\nHousehold Bread/Furniture needs use real retail purchases. Municipal own-use removes market stock without self-payment. Inspect Needs & coverage for exact amounts and causes."));
            SetText("economySummaryLabel", port.Facilities.Count + " public industries · " +
                Session.Snapshot.PrivateFacilities.Values.Count(f => f.OwnerId == Session.Player.OwnerId && f.PortId == port.Id) +
                " private industries · " + port.Consumption.Count +
                " consumption rules" + (port.Consumption.Any(rule => rule.LastUnmetQuantity > 0) ? "\nRecent demand unmet. Inspect economy details." : ""));
            SetText("pricingLabel", string.Join("\n\n", port.Quotes.Values.Select(quote =>
            {
                var pricing = quote.Pricing;
                return Session.Content.GetGood(quote.GoodId).Name + (pricing == null ? ": fixed quotes" :
                    ": " + pricing.Model + ", reference " + pricing.ReferencePrice + ", target stock " + Q(pricing.TargetStock) +
                    ", sensitivity " + pricing.Sensitivity + ", bounds " + pricing.MinPrice + "–" + pricing.MaxPrice +
                    ", spread " + pricing.Spread);
            })) + "\nTarget stock is a pricing parameter. It does not refill the market.");
            var deliveries = Session.Activity.Where(item => item.PortId == port.Id && item.ShipId != null).Take(3).ToArray();
            SetText("deliveriesLabel", deliveries.Length == 0 ? "No recent ship activity here." :
                string.Join("\n", deliveries.Select(item => "D" + item.Day + " · " + item.Message)));
        }

        private void RefreshShip(RegionalShipSnapshot ship)
        {
            SetText("selectionTitle", ship.Name);
            SetText("selectionSubtitle", ship.IsPlayer ? "Player ship · " + ship.OwnerId : "Independent NPC merchant · " + ship.OwnerId);
            SetText("shipStateLabel", ship.DockedPortId != null ? "Docked at " + PortName(ship.DockedPortId) :
                "Sailing " + PortName(ship.OriginPortId) + " → " + PortName(ship.DestinationPortId) +
                "\n" + ship.RemainingTravelDays + " of " + ship.VoyageDurationDays + " days remaining");
            SetText("shipCargoLabel", "Cargo " + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) + "\n" +
                string.Join(", ", goodIds.Select(id => Q(ship.Cargo[id]) + " " + Session.Content.GetGood(id).Name)) +
                "\nShared company cash " + Money(ship.Cash));
            var policy = ship.Policy;
            SetText("shipPolicyLabel", policy == null ? "No route policy." : PortName(policy.OriginPortId) + " → " +
                PortName(policy.DestinationPortId) + "\nOutbound " + LegText(policy.Outbound) +
                "\nReturn " + (policy.ReturnLeg == null ? "none" : LegText(policy.ReturnLeg)) +
                "\nMinimum source stock: " + Q(ship.Configuration.MinimumOriginStock) +
                "; return source stock: " + Q(ship.Configuration.MinimumReturnStock) +
                "\nReserve " + Money(policy.CashReserve) + (policy.AllowEmptyReturn ? " · empty return allowed" : " · loaded return only"));
            if (ship.Itinerary != null)
                SetText("shipPolicyLabel", "Ordered itinerary · " + string.Join(" → ", ship.Itinerary.Stops.Select(stop => PortName(stop.PortId))) +
                    " → loop\n" + ship.Itinerary.Stops.Sum(stop => stop.Commands.Count) + " actions · mixed cargo retained · open Route planner to inspect each action.");
            else if (UsesItineraries && ship.IsPlayer) SetText("shipPolicyLabel", "No itinerary assigned. Open Fleet / Route planner to draft this ship's ordered work.");
            var decision = Session.DecisionFor(ship.Id);
            SetText("shipDecisionLabel", DecisionText(decision));
            SetText("shipControlLabel", ship.IsPlayer ? "You may disable your route and trade or plan a voyage manually." :
                "Inspection only. This merchant obeys the same market cash, cargo and voyage rules.");
        }

        private void RefreshPreviews() => RefreshTradeOverlay();

        private void RefreshPolicy()
        {
            Element<Toggle>("automationToggle").SetValueWithoutNotify(Session.Player.AutomationEnabled);
            SetText("decisionLabel", DecisionText(Session.CurrentDecision));
            SetText("activeRouteLabel", Session.Player.Name + " · " + (Session.Player.IndustrialPolicy != null ? "Industrial shipping · " : "") +
                PortName(Session.Player.IndustrialPolicy?.OriginPortId ?? Session.Policy.OriginPortId) + " ↔ " +
                PortName(Session.Player.IndustrialPolicy?.DestinationPortId ?? Session.Policy.DestinationPortId) +
                " · " + (Session.Player.AutomationEnabled ? "enabled" : "manual control"));
            Element<Button>("runDocksideButton").SetEnabled(Session.Player.AutomationEnabled && !Session.IsChallengeEnded);
            var finance = Session.Snapshot.Accounting.Companies[Session.Player.OwnerId];
            SetText("profitLabel", "Cash change " + SignedMoney(Session.CashChange) +
                "\nRealized material gross margin " + SignedMoney(finance.RealizedGrossMargin) +
                " · held material basis " + Money(finance.HeldInventoryCost) +
                "\nGrants have zero paid basis. Construction and unmodeled operating costs are separate; see the Ledger.");
        }

        private void RefreshActivity()
        {
            SetText("latestEventLabel", Session.Activity.Count == 0 ? "" : "D" + Session.Activity[0].Day + " · " + Session.Activity[0].Message);
            var container = Element<VisualElement>("activityEntries");
            container.Clear();
            foreach (var entry in Session.Activity)
            {
                var label = new Label("D" + entry.Day + " · " + entry.Message);
                label.AddToClassList("activity-entry");
                container.Add(label);
            }
        }

        private string DecisionText(RegionalDecision decision)
        {
            var actor = decision.Before.Ships[decision.ShipId];
            if (UsesItineraries && actor.IsPlayer && actor.Itinerary == null) return "Manual control · no itinerary assigned. Draft one in Fleet / Route planner.";
            if (decision.ItineraryDetail != null)
                return "Itinerary · " + decision.ItineraryDetail.Kind + "\n" + decision.Reason;
            if (decision.IndustrialDetail != null)
                return actor.IndustrialStage + " · " + decision.IndustrialDetail.Action + "\n" + decision.Reason;
            var text = !actor.AutomationEnabled
                ? (actor.IsPlayer ? "Manual control" : "Automation disabled") + "\n" + decision.Reason
                : TradingLabText.Stage(decision.Stage) + " · " + TradingLabText.Action(decision.Action) + "\n" + decision.Reason;
            if (decision.Preview != null)
                text += "\n" + Q(decision.SelectedQuantity) + " " + Session.Content.GetGood(decision.Preview.GoodId).Name +
                    " · " + PreviewText(decision.Preview) + "\n" + QuantityExplanation(decision);
            if (decision.Detail?.Departure != null)
                text += "\nDestination: " + PortName(decision.Detail.Departure.DestinationPortId) + ".";
            return text;
        }

        private string QuantityExplanation(RegionalDecision decision)
        {
            var ship = decision.Before.Ships[decision.ShipId];
            var preview = decision.Preview;
            if (ship.Policy == null || preview == null || decision.Detail?.Trade == null) return "";
            var leg = decision.Stage == AutomationStage.ReturnLoading || decision.Stage == AutomationStage.ReturnSale
                ? ship.Policy.ReturnLeg : ship.Policy.Outbound;
            if (leg == null) return "";
            var portId = decision.Detail.Trade.PortId;
            var port = decision.Before.Ports[portId];
            if (preview.Direction == TradeDirection.Buy)
            {
                if (preview.Quantity + (long)ship.Cargo[preview.GoodId] >= leg.MaxPurchaseQuantity)
                    return "This fills the configured shipment target.";
                if (preview.Quantity >= ship.CargoCapacity - ship.CargoUnits)
                    return "A larger purchase would exceed free cargo capacity.";
                if (preview.Quantity >= port.Inventory[preview.GoodId])
                    return "This uses all currently available source stock.";
            }
            else if (preview.Quantity >= ship.Cargo[preview.GoodId])
                return "This sells all carried units of this commodity.";
            if (preview.Quantity > int.MaxValue - Session.Snapshot.QuantityScale) return "The public quantity bound has been reached.";
            // Explain one larger proposal using the authoritative preview, never a price formula.
            var larger = decision.Before.PreviewTrade(ship.Id, portId,
                new TradeCommand(preview.Direction, preview.GoodId, preview.Quantity + Session.Snapshot.QuantityScale));
            if (larger.Price != null)
            {
                if (preview.Direction == TradeDirection.Buy && larger.Price.LastMarginalPrice > leg.MaxBuyPrice)
                    return "One more unit would cost up to " + Number(larger.Price.LastMarginalPrice) +
                        ", above the buy limit " + Number(leg.MaxBuyPrice) + ".";
                if (preview.Direction == TradeDirection.Sell && larger.Price.LastMarginalPrice < leg.MinSellPrice)
                    return "One more unit would lower the final bid to " + Number(larger.Price.LastMarginalPrice) +
                        ", below the sell limit " + Number(leg.MinSellPrice) + ".";
                if (preview.Direction == TradeDirection.Buy && larger.TotalAmount > leg.MaxPurchaseExpenditure)
                    return "One more unit would exceed the total purchase expenditure cap.";
                if (preview.Direction == TradeDirection.Buy && larger.TotalAmount > Math.Max(0, ship.Cash - ship.Policy.CashReserve))
                    return "One more unit would exceed cash available after the reserve.";
            }
            return larger.Outcome == TradeOutcome.Traded ? "The route uses the largest qualifying exact order." :
                "A larger order is blocked: " + TradingLabText.Outcome(larger.Outcome) + ".";
        }

        private string LegText(RouteLeg leg) => Session.Content.GetGood(leg.GoodId).Name + " " +
            Q(leg.MinShipmentQuantity) + "–" + Q(leg.MaxPurchaseQuantity) + " · buy ≤ " + leg.MaxBuyPrice +
            " · sell ≥ " + leg.MinSellPrice + (leg.MaxPurchaseExpenditure == long.MaxValue ? "" : " · spend ≤ " + Money(leg.MaxPurchaseExpenditure));
        private string Quantities(IEnumerable<RecipeQuantity> values) =>
            string.Join(", ", values.Select(value => value.Quantity + " " + Session.Content.GetGood(value.GoodId).Name));
        private string PreviewText(TradeAttempt preview) => preview.Price == null ?
            TradingLabText.Outcome(preview.Outcome) + "." :
            "Exact total " + Money(preview.TotalAmount) + " · first " + Number(preview.Price.FirstMarginalPrice) +
            " / last " + Number(preview.Price.LastMarginalPrice) +
            (preview.Outcome == TradeOutcome.Traded ? "" : "\n" + TradingLabText.Outcome(preview.Outcome));
        private string SelectedGoodId() => goodIds[Math.Max(0, Element<DropdownField>("goodDropdown").index)];
        private static string ShipChoice(RegionalShipSnapshot ship) => ship.Name + (ship.IsPlayer ? " (you)" : " (NPC)");
        private string PortName(string id) => Session.Snapshot.Ports[id].Name;
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Signed(long value) => (value > 0 ? "+" : "") + Number(value);
        private string Q(long value) => Session.Quantity(value);
        private string Money(long value) => Session.Coins(value);
        private string Money(System.Numerics.BigInteger value) => ((decimal)value / Session.Snapshot.MoneyScale).ToString("0.####", CultureInfo.InvariantCulture);
        private string SignedMoney(long value) => (value > 0 ? "+" : "") + Money(value);
        private T Element<T>(string name) where T : VisualElement => Root.Q<T>(name) ??
            throw new InvalidOperationException("Archipelago UI element missing: " + name);
        private void SetText(string name, string value) => Element<Label>(name).text = value;
        private void Click(string name, Action action) => Element<Button>(name).clicked += action;
        private static void AddCell(VisualElement row, string value)
        {
            var cell = new Label(value);
            cell.AddToClassList("commodity-cell");
            row.Add(cell);
        }

        private void Run(Action action, bool useSessionStatus = true)
        {
            try
            {
                action();
                if (useSessionStatus) { status = Session.LastStatus; statusIsError = false; }
                Refresh();
            }
            catch (ArgumentException exception) { Validation(exception.Message); }
            catch (InvalidOperationException exception) { Validation(exception.Message); }
            catch (Exception exception) when (exception is WorldSaveException || exception is System.IO.IOException ||
                exception is UnauthorizedAccessException || exception is OverflowException) { Validation(exception.Message); }
            catch (Exception exception) { ReportException(exception); }
        }

        private void Validation(string message)
        {
            status = message;
            statusIsError = true;
            SetText("statusLabel", message);
            Element<Label>("statusLabel").AddToClassList("error");
            RefreshModalStatus();
        }

        private void ReportException(Exception exception)
        {
            status = "Action failed: " + exception.Message;
            statusIsError = true;
            var label = Root?.Q<Label>("statusLabel");
            if (label != null) { label.text = status; label.AddToClassList("error"); }
            if (Root != null) RefreshModalStatus();
            Debug.LogException(exception, this);
        }
    }
}
