using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Explicit UI actions and snapshot presentation; the session owns the simulation boundary.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TradingLabController : MonoBehaviour
    {
        private static readonly List<string> ScenarioNames = new List<string>
        {
            "A · Standard trading", "B · Restrictive automation", "C · No destination cash"
        };

        private readonly List<string> goodIds = new List<string>();
        private string selectedPort = TradingLabScenarios.North;
        private string status = "Select a port, preview an order, then choose when time advances.";
        private bool statusIsError;

        public TradingLabSession Session { get; private set; }
        public VisualElement Root { get; private set; }

        private void Start()
        {
            Root = GetComponent<UIDocument>().rootVisualElement;
            try
            {
                var contentAsset = Resources.Load<TextAsset>("CanonicalContent");
                if (contentAsset == null)
                {
                    throw new InvalidOperationException(
                        "CanonicalContent is missing. Run python tools/prepare_unity.py from the repository root, then import the generated assets.");
                }

                Session = new TradingLabSession(ContentLoader.Load(contentAsset.text));
                BindControls();
                LoadPolicyFields();
                Refresh();
                Debug.Log("Salt & Ledger Trading Lab ready: tick " + Number(Session.Snapshot.Tick) +
                    ", cash " + Number(Session.Snapshot.TraderCash) + ", cargo " +
                    Number(Session.Snapshot.Ship.CargoUnits) + "/" + Number(Session.Snapshot.Ship.CargoCapacity) + ".");
            }
            catch (Exception exception)
            {
                ReportException(exception);
            }
        }

        private void BindControls()
        {
            var scenarios = Element<DropdownField>("scenarioDropdown");
            scenarios.choices = ScenarioNames;
            scenarios.SetValueWithoutNotify(ScenarioNames[0]);
            var goods = Session.Content.Goods.OrderBy(good => good.Id, StringComparer.Ordinal).ToArray();
            goodIds.AddRange(goods.Select(good => good.Id));
            var goodDropdown = Element<DropdownField>("goodDropdown");
            goodDropdown.choices = goods.Select(good => good.Name).ToList();
            goodDropdown.SetValueWithoutNotify(goodDropdown.choices[0]);
            goodDropdown.RegisterValueChangedCallback(_ => Run(RefreshPreviews, false));
            Element<TextField>("quantityField").RegisterValueChangedCallback(_ => Run(RefreshPreviews, false));
            Click("northportButton", () => SelectPort(TradingLabScenarios.North));
            Click("southhavenButton", () => SelectPort(TradingLabScenarios.South));
            Click("buyButton", () => ExecuteTrade(TradeDirection.Buy, false));
            Click("sellButton", () => ExecuteTrade(TradeDirection.Sell, false));
            Click("buyDepartButton", () => ExecuteTrade(TradeDirection.Buy, true));
            Click("sailButton", () => Run(() => Session.Sail(OtherPort(Session.Snapshot.Ship.DockedPortId))));
            Click("nextTickButton", () => Run(() => Session.NextTick()));
            Click("advanceTicksButton", () => Run(() => Session.AdvanceTicks(3)));
            Click("automationTickButton", () => Run(() => Session.NextTick()));
            Click("automationRunButton", () => Run(() => Session.AdvanceAutomation(6)));
            Click("resetButton", ResetScenario);
            Click("fullPresetButton", () => ApplyPreset(false));
            Click("restrictivePresetButton", () => ApplyPreset(true));
            Click("applyPolicyButton", ApplyPolicy);
            Element<Toggle>("automationToggle").RegisterValueChangedCallback(change =>
                Run(() => Session.SetAutomationEnabled(change.newValue)));

            foreach (var name in new[] { "maxBuyField", "minSellField", "maxShipmentField", "minShipmentField", "cashReserveField" })
            {
                Element<TextField>(name).RegisterValueChangedCallback(_ =>
                    SetText("policyDraftLabel", "Draft changed. Apply policy to use these values; no tick will pass."));
            }
        }

        private void SelectPort(string portId)
        {
            Run(() => selectedPort = portId, false);
        }

        private void ResetScenario()
        {
            Run(() =>
            {
                var selected = Element<DropdownField>("scenarioDropdown").index;
                Session.Reset((TradingLabScenario)Math.Max(0, selected));
                selectedPort = TradingLabScenarios.North;
                Element<DropdownField>("goodDropdown").SetValueWithoutNotify(
                    Element<DropdownField>("goodDropdown").choices[0]);
                Element<TextField>("quantityField").SetValueWithoutNotify("6");
                LoadPolicyFields();
                foreach (var name in new[] { "marketScroll", "manualScroll", "automationScroll" })
                    Element<ScrollView>(name).scrollOffset = Vector2.zero;
            });
        }

        private void ApplyPreset(bool restrictive)
        {
            Run(() =>
            {
                Session.ApplyPolicy(TradingLabScenarios.CreatePolicy(restrictive));
                LoadPolicyFields();
            });
        }

        private void LoadPolicyFields()
        {
            var policy = Session.Policy;
            Element<TextField>("maxBuyField").SetValueWithoutNotify(Number(policy.Outbound.MaxBuyPrice));
            Element<TextField>("minSellField").SetValueWithoutNotify(Number(policy.Outbound.MinSellPrice));
            Element<TextField>("maxShipmentField").SetValueWithoutNotify(Number(policy.Outbound.MaxPurchaseQuantity));
            Element<TextField>("minShipmentField").SetValueWithoutNotify(Number(policy.Outbound.MinShipmentQuantity));
            Element<TextField>("cashReserveField").SetValueWithoutNotify(Number(policy.CashReserve));
            SetText("policyDraftLabel", "Applied policy. Price limits constrain the worst unit in an order.");
        }

        private void ApplyPolicy()
        {
            if (!PositiveLong("maxBuyField", out var maxBuy) || !PositiveLong("minSellField", out var minSell))
            {
                Validation("Buy and sell limits must be positive whole numbers within the 64-bit range.");
                return;
            }
            if (!PositiveInt("maxShipmentField", out var maximum) || !PositiveInt("minShipmentField", out var minimum) || minimum > maximum)
            {
                Validation("Shipment quantities must be positive whole numbers; minimum cannot exceed target.");
                return;
            }
            if (!long.TryParse(Element<TextField>("cashReserveField").value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var reserve) || reserve < 0)
            {
                Validation("Cash reserve must be a nonnegative whole number within the 64-bit range.");
                return;
            }

            Run(() =>
            {
                var current = Session.Policy;
                var policy = new TradeRoutePolicy(current.OriginPortId, current.DestinationPortId,
                    new RouteLeg(current.Outbound.GoodId, maximum, minimum, maxBuy, minSell),
                    current.ReturnLeg, reserve, current.AllowEmptyReturn);
                Session.ApplyPolicy(policy);
                LoadPolicyFields();
            });
        }

        private void ExecuteTrade(TradeDirection direction, bool depart)
        {
            if (!PositiveInt("quantityField", out var quantity))
            {
                Validation("Quantity must be a positive whole number from 1 to 2,147,483,647.");
                return;
            }
            Run(() => Session.ExecuteTrade(selectedPort, direction, SelectedGoodId(), quantity, depart));
        }

        /// <summary>Refreshes only from committed state; no simulation time is advanced.</summary>
        public void Refresh()
        {
            if (Session == null) return;
            var snapshot = Session.Snapshot;
            var ship = snapshot.Ship;
            SetText("tickLabel", "TICK " + Number(snapshot.Tick));
            SetText("cashLabel", Number(snapshot.TraderCash));
            SetText("cashChangeLabel", "Cash change since reset: " + Signed(Session.CashChange));
            SetText("cargoLabel", Number(ship.CargoUnits) + " / " + Number(ship.CargoCapacity) + " units");
            SetText("cargoContentsLabel", string.Join("  ·  ", Session.Content.Goods.Select(good =>
                good.Name + " " + Number(ship.Cargo[good.Id]))));
            SetText("profitLabel", Session.RealizedGrossProfit.HasValue
                ? Signed(Session.RealizedGrossProfit.Value) + " realized gross profit"
                : "No closed cash-flow result");
            RefreshMap(snapshot);
            RefreshMarket(snapshot);
            RefreshPreviews();
            RefreshAutomation();
            RefreshActivity();
            Element<Toggle>("automationToggle").SetValueWithoutNotify(Session.AutomationEnabled);
            Element<Button>("automationTickButton").SetEnabled(Session.AutomationEnabled);
            Element<Button>("automationRunButton").SetEnabled(Session.AutomationEnabled);
            Element<Button>("advanceTicksButton").text = Session.AutomationEnabled ? "Advance up to 3 ticks" : "Advance 3 ticks";
            SetText("timeModeLabel", Session.AutomationEnabled
                ? "Time buttons evaluate the applied policy. One tick always advances once; bounded runs can stop early."
                : "Time buttons advance production and travel without placing a trade.");
            SetText("statusLabel", status);
            Element<Label>("statusLabel").EnableInClassList("error", statusIsError);
        }

        private void RefreshMap(MaritimeWorldSnapshot snapshot)
        {
            var ship = snapshot.Ship;
            Element<Button>("northportButton").EnableInClassList("selected-port", selectedPort == TradingLabScenarios.North);
            Element<Button>("southhavenButton").EnableInClassList("selected-port", selectedPort == TradingLabScenarios.South);
            var progress = Element<ProgressBar>("shipProgress");
            if (ship.DockedPortId != null)
            {
                progress.value = ship.DockedPortId == TradingLabScenarios.North ? 0 : 100;
                progress.title = "Ship at " + TradingLabText.PortName(ship.DockedPortId);
                SetText("shipStatusLabel", "Docked at " + TradingLabText.PortName(ship.DockedPortId) + ". Trade on the next step.");
            }
            else
            {
                var fraction = (snapshot.TravelTicks - ship.RemainingTravelTicks) / (float)snapshot.TravelTicks;
                progress.value = 100 * (ship.OriginPortId == TradingLabScenarios.North ? fraction : 1 - fraction);
                progress.title = "Ship sailing · " + Number(ship.RemainingTravelTicks) + " tick remaining";
                SetText("shipStatusLabel", "Sailing to " + TradingLabText.PortName(ship.DestinationPortId) +
                    " · " + Number(ship.RemainingTravelTicks) + " tick(s) until arrival. Trading unavailable.");
            }
            var sail = Element<Button>("sailButton");
            sail.SetEnabled(!Session.AutomationEnabled && ship.DockedPortId != null);
            sail.text = ship.DockedPortId == null ? "Sailing · advance time to arrive"
                : "Sail to " + TradingLabText.PortName(OtherPort(ship.DockedPortId)) + " (1 tick)";
        }

        private void RefreshMarket(MaritimeWorldSnapshot snapshot)
        {
            var port = snapshot.Ports[selectedPort];
            SetText("marketTitle", TradingLabText.PortName(selectedPort) + " market");
            SetText("marketCashLabel", "Market cash: " + Number(port.MarketCash));
            var rows = Element<VisualElement>("marketRows");
            rows.Clear();
            foreach (var good in Session.Content.Goods.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                var quote = snapshot.GetMarketQuote(selectedPort, good.Id);
                var row = new VisualElement();
                row.AddToClassList("market-row");
                var name = new Label(good.Name) { tooltip = good.Description };
                name.AddToClassList("market-good");
                name.AddToClassList("market-good-name");
                row.Add(name);
                AddCell(row, Number(port.Inventory[good.Id]));
                AddCell(row, quote.Bid.HasValue ? Number(quote.Bid.Value) : "—");
                AddCell(row, quote.Ask.HasValue ? Number(quote.Ask.Value) : "—");
                rows.Add(row);
            }
            var production = new List<string>();
            foreach (var building in port.Buildings)
            {
                var recipe = Session.Content.GetRecipe(building.RecipeId);
                production.Add(Session.Content.GetBuilding(building.BuildingId).Name +
                    (building.ProductionEnabled ? " · active each tick\n" : " · disabled\n") +
                    Quantities(recipe.Inputs) + " → " + Quantities(recipe.Outputs));
            }
            SetText("productionLabel", production.Count == 0 ? "No local production." : string.Join("\n", production) +
                "\nUses this market's stock; a shortage skips the batch.");
            var parameters = new List<string>();
            foreach (var quote in port.Quotes.Values)
            {
                var pricing = quote.Pricing;
                parameters.Add(Session.Content.GetGood(quote.GoodId).Name + (pricing == null
                    ? ": fixed bid " + Number(quote.Bid) + ", ask " + Number(quote.Ask)
                    : ": " + pricing.Model + " · reference " + Number(pricing.ReferencePrice) +
                    ", target stock " + Number(pricing.TargetStock) + ", sensitivity " + Number(pricing.Sensitivity) +
                    ", bounds " + Number(pricing.MinPrice) + "–" + Number(pricing.MaxPrice) +
                    ", spread " + Number(pricing.Spread)));
            }
            SetText("pricingLabel", string.Join("\n\n", parameters) + "\n\nTarget stock is a pricing parameter, not automatic replenishment.");
        }

        private void RefreshPreviews()
        {
            var goodId = SelectedGoodId();
            SetText("goodDescriptionLabel", Session.Content.GetGood(goodId).Description);
            var snapshot = Session.Snapshot;
            var local = snapshot.Ship.DockedPortId == selectedPort;
            var canExecute = local && !Session.AutomationEnabled;
            SetText("tradeAvailabilityLabel", Session.AutomationEnabled
                ? "Manual orders disabled. Pause automation to take control."
                : local ? "Docked here · orders below execute on the next step."
                : "Inspection only · hypothetical previews as if docked here. Sail here before trading.");
            if (!PositiveInt("quantityField", out var quantity))
            {
                const string invalid = "Enter a positive whole quantity (1–2,147,483,647). No order has been placed.";
                SetText("buyPreviewLabel", invalid);
                SetText("sellPreviewLabel", invalid);
                foreach (var name in new[] { "buyButton", "sellButton", "buyDepartButton" }) Element<Button>(name).SetEnabled(false);
                return;
            }
            var buy = Session.PreviewTrade(selectedPort, TradeDirection.Buy, goodId, quantity);
            var sell = Session.PreviewTrade(selectedPort, TradeDirection.Sell, goodId, quantity);
            SetText("buyPreviewLabel", PreviewText(buy));
            SetText("sellPreviewLabel", PreviewText(sell));
            Element<Label>("buyPreviewLabel").EnableInClassList("warning", buy.Outcome != TradeOutcome.Traded);
            Element<Label>("sellPreviewLabel").EnableInClassList("warning", sell.Outcome != TradeOutcome.Traded);
            Element<Button>("buyButton").SetEnabled(canExecute && buy.Outcome == TradeOutcome.Traded);
            Element<Button>("buyDepartButton").SetEnabled(canExecute && buy.Outcome == TradeOutcome.Traded);
            Element<Button>("sellButton").SetEnabled(canExecute && sell.Outcome == TradeOutcome.Traded);
        }

        private void RefreshAutomation()
        {
            var policy = Session.Policy;
            SetText("outboundLabel", "Outbound · " + Session.Content.GetGood(policy.Outbound.GoodId).Name +
                " to " + TradingLabText.PortName(policy.DestinationPortId));
            var back = policy.ReturnLeg;
            SetText("returnPolicyLabel", back == null ? "Return: empty sailing permitted."
                : "Return: " + Session.Content.GetGood(back.GoodId).Name + " · target " + Number(back.MaxPurchaseQuantity) +
                  ", minimum " + Number(back.MinShipmentQuantity) + ", buy ≤ " + Number(back.MaxBuyPrice) +
                  ", sell ≥ " + Number(back.MinSellPrice) + (policy.AllowEmptyReturn
                      ? ". Empty return permitted." : ". Empty return disabled."));
            var decision = Session.CurrentDecision;
            var text = (decision.Reason == AutomationReason.Disabled ? "Manual control"
                : TradingLabText.Stage(decision.Stage) + " · " + TradingLabText.Action(decision.Action)) +
                "\n" + TradingLabText.Reason(decision.Reason);
            if (decision.Trade != null)
                text += "\nSelected: " + Number(decision.SelectedQuantity) + " " + Session.Content.GetGood(decision.Trade.Trade.GoodId).Name;
            if (decision.Preview != null) text += "\n" + PriceText(decision.Preview);
            text += decision.Departure == null ? "\nNo departure proposed."
                : "\nReady to depart for " + TradingLabText.PortName(decision.Departure.DestinationPortId) + ".";
            SetText("decisionLabel", text);
            Element<Label>("decisionLabel").EnableInClassList("warning", decision.Action == AutomationAction.Wait ||
                decision.Action == AutomationAction.Pause && Session.AutomationEnabled);
        }

        private void RefreshActivity()
        {
            var entries = Element<VisualElement>("activityEntries");
            entries.Clear();
            foreach (var activity in Session.Activity)
            {
                var label = new Label("T" + Number(activity.Tick) + " · " + activity.Kind + "\n" + activity.Message);
                label.AddToClassList("activity-entry");
                entries.Add(label);
            }
        }

        private static string PreviewText(TradeAttempt preview)
        {
            return PriceText(preview) + "\n" + (preview.Outcome == TradeOutcome.Traded
                ? "Economically executable if docked here."
                : TradingLabText.Outcome(preview.Outcome));
        }

        private static string PriceText(TradeAttempt preview)
        {
            return preview.Price == null ? "Total unavailable for this quantity."
                : Number(preview.Quantity) + " units · total " + Number(preview.Price.TotalAmount) +
                  "\nFirst unit " + Number(preview.Price.FirstMarginalPrice) + " · last unit " + Number(preview.Price.LastMarginalPrice);
        }

        private string Quantities(IEnumerable<RecipeQuantity> quantities)
        {
            return string.Join(" + ", quantities.Select(quantity =>
                Number(quantity.Quantity) + " " + Session.Content.GetGood(quantity.GoodId).Name));
        }

        private void Run(Action action, bool useSessionStatus = true)
        {
            try
            {
                action();
                if (useSessionStatus)
                {
                    status = Session.LastStatus;
                    statusIsError = false;
                }
                Refresh();
            }
            catch (Exception exception)
            {
                ReportException(exception);
            }
        }

        private void ReportException(Exception exception)
        {
            status = "Action failed: " + exception.Message;
            statusIsError = true;
            var label = Root?.Q<Label>("statusLabel");
            if (label != null)
            {
                label.text = status;
                label.AddToClassList("error");
            }
            Debug.LogException(exception, this);
        }

        private void Validation(string message)
        {
            status = message;
            statusIsError = true;
            SetText("statusLabel", message);
            Element<Label>("statusLabel").AddToClassList("error");
        }

        private bool PositiveInt(string name, out int value) =>
            int.TryParse(Element<TextField>(name).value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
        private bool PositiveLong(string name, out long value) =>
            long.TryParse(Element<TextField>(name).value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
        private string SelectedGoodId() => goodIds[Math.Max(0, Element<DropdownField>("goodDropdown").index)];
        private static string OtherPort(string portId) => portId == TradingLabScenarios.North ? TradingLabScenarios.South : TradingLabScenarios.North;
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Signed(long value) => (value > 0 ? "+" : "") + Number(value);
        private T Element<T>(string name) where T : VisualElement => Root.Q<T>(name) ??
            throw new InvalidOperationException("TradingLab UI element is missing: " + name);
        private void SetText(string name, string value) => Element<Label>(name).text = value;
        private void Click(string name, Action action) => Element<Button>(name).clicked += action;
        private static void AddCell(VisualElement row, string value)
        {
            var label = new Label(value);
            label.AddToClassList("market-cell");
            row.Add(label);
        }
    }
}
