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
        private string tradeShipId;
        private RegionalWorldSnapshot tradeSnapshot;
        private VisualElement returnFocus;
        public bool TradeIsOpen => !Element<VisualElement>("tradeBackdrop").ClassListContains("hidden");
        public bool IndustryIsOpen => !Element<VisualElement>("industryBackdrop").ClassListContains("hidden");

        private void BindOverlays()
        {
            Click("openTradeButton", () => OpenTrade(Session.SelectedPortId));
            Click("closeTradeButton", CloseModals);
            Click("tradeSailButton", () => Run(() => { Session.SailTo(Session.SelectedPortId); CloseModals(); }));
            Click("quantityOne", () => SetQuantity(1));
            Click("quantityFive", () => SetQuantity(5));
            Click("quantityMinus", () => AdjustQuantity(-1));
            Click("quantityPlus", () => AdjustQuantity(1));
            Click("quantityMaxBuy", () => SetMaximum(TradeDirection.Buy));
            Click("quantityMaxSell", () => SetMaximum(TradeDirection.Sell));
            Click("startChallengeButton", () => StartScenario(true));
            Click("startSandboxButton", () => StartScenario(false));
            BindIndustry();
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || !TradeIsOpen && !IndustryIsOpen) return;
                evt.StopImmediatePropagation();
                CloseModals();
            }, TrickleDown.TrickleDown);
            // Unity 6's Input System routes the UI Cancel action (including Escape) as navigation.
            Root.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                if (!TradeIsOpen && !IndustryIsOpen) return;
                evt.StopImmediatePropagation();
                CloseModals();
            }, TrickleDown.TrickleDown);
            foreach (var id in new[] { "tradeBackdrop", "industryBackdrop" })
            {
                var backdrop = Element<VisualElement>(id);
                backdrop.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.target == backdrop && evt.button == 0) CloseModals();
                    evt.StopPropagation();
                });
                backdrop.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            }
        }

        private void StartScenario(bool challenge) => Run(() =>
        {
            CloseModals();
            Session.StartScenario(challenge);
            LoadPolicy();
            Element<DropdownField>("speedDropdown").SetValueWithoutNotify("1×");
        });

        public void OpenTrade(string portId)
        {
            Run(() =>
            {
                var selectedShip = Session.SelectedShipId;
                tradeShipId = selectedShip ?? Session.Player.Id;
                Session.InspectTradePort(portId);
                ShowModal("tradeBackdrop", "closeTradeButton");
                if (string.IsNullOrEmpty(Element<DropdownField>("goodDropdown").value)) SetIndex("goodDropdown", goodIds.IndexOf("logs"));
            });
        }

        private void ShowModal(string backdropId, string focusId)
        {
            if (!TradeIsOpen && !IndustryIsOpen) returnFocus = Root.focusController?.focusedElement as VisualElement;
            Element<VisualElement>("tradeBackdrop").EnableInClassList("hidden", backdropId != "tradeBackdrop");
            Element<VisualElement>("industryBackdrop").EnableInClassList("hidden", backdropId != "industryBackdrop");
            Element<VisualElement>("topBar").SetEnabled(false);
            Element<VisualElement>("mainLayout").SetEnabled(false);
            Element<Button>(focusId).Focus();
        }

        public void CloseModals()
        {
            Element<VisualElement>("tradeBackdrop").AddToClassList("hidden");
            Element<VisualElement>("industryBackdrop").AddToClassList("hidden");
            Element<VisualElement>("topBar").SetEnabled(true);
            Element<VisualElement>("mainLayout").SetEnabled(true);
            returnFocus?.Focus();
            returnFocus = null;
        }

        private void SetQuantity(int quantity)
        {
            Element<TextField>("quantityField").value = Number(quantity);
        }

        private void AdjustQuantity(int delta)
        {
            int.TryParse(Element<TextField>("quantityField").value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
            SetQuantity((int)Math.Max(1, Math.Min(int.MaxValue, (long)value + delta)));
        }

        private void SetMaximum(TradeDirection direction)
        {
            if (tradeShipId == null) return;
            var maximum = Session.Snapshot.MaximumTradeQuantity(tradeShipId, Session.SelectedPortId, direction, SelectedGoodId());
            SetQuantity(maximum);
            if (maximum == 0) Validation("No executable " + (direction == TradeDirection.Buy ? "purchase" : "sale") +
                " is available with the current stock, cash and capacity.");
        }

        private void RefreshTradeOverlay()
        {
            if (tradeShipId == null) tradeShipId = Session.Player.Id;
            tradeSnapshot = Session.Snapshot;
            var ship = tradeSnapshot.Ships[tradeShipId];
            var port = tradeSnapshot.Ports[Session.SelectedPortId];
            var local = ship.DockedPortId == port.Id;
            var canControl = ship.IsPlayer && !ship.AutomationEnabled && !Session.IsChallengeEnded;
            SetText("tradeTitle", port.Name + "  /  " + ship.Name);
            SetText("tradeDay", "DAY " + Number(tradeSnapshot.Day));
            SetText("tradeHeader", (local ? "DOCKED" : "REMOTE MARKET") + "   ·   Company cash " + Number(ship.Cash) +
                "   ·   Town liquidity " + Number(port.MarketCash) + "   ·   Cargo " + ship.CargoUnits + "/" + ship.CargoCapacity +
                " (" + (ship.CargoCapacity - ship.CargoUnits) + " free)");
            SetText("tradeAvailabilityLabel", !ship.IsPlayer ? "NPC inspection only. Select your ship to trade or sail." :
                Session.IsChallengeEnded ? "The charter has ended. Reset or start a new sandbox to continue." :
                ship.AutomationEnabled ? "Disable your trade route before placing manual orders." :
                local ? "Trades are instant and all-or-nothing. Playback stays paused when you close the exchange." :
                "Remote information only. Dock here to trade. Prices may change before your arrival.");
            var sail = Element<Button>("tradeSailButton");
            sail.EnableInClassList("hidden", local || !ship.IsPlayer);
            sail.SetEnabled(canControl && ship.DockedPortId != null && !local && Session.HasRoute(ship.DockedPortId, port.Id));
            sail.text = ship.DockedPortId != null && !local && Session.HasRoute(ship.DockedPortId, port.Id)
                ? "Sail here · " + tradeSnapshot.TravelDays(ship.Id, port.Id) + " days" : "Sailing · await arrival";
            var rows = Element<VisualElement>("tradeGoodsRows");
            // Reuse rows while quantity changes so keyboard focus is never removed by a refresh.
            if (rows.childCount == 0)
                foreach (var goodId in goodIds)
                {
                    var captured = goodId;
                    var row = new Button(() => { SetIndex("goodDropdown", goodIds.IndexOf(captured)); RefreshTradeOverlay(); })
                        { name = "tradeGood_" + goodId };
                    row.AddToClassList("trade-goods-row");
                    row.Add(new Label(Session.Content.GetGood(goodId).Name) { name = "name", pickingMode = PickingMode.Ignore });
                    row[0].AddToClassList("trade-good-name");
                    foreach (var field in new[] { "town", "ship", "bid", "ask" })
                    {
                        var cell = new Label { name = field, pickingMode = PickingMode.Ignore };
                        cell.AddToClassList("trade-cell"); row.Add(cell);
                    }
                    rows.Add(row);
                }
            foreach (var goodId in goodIds)
            {
                var row = rows.Q<Button>("tradeGood_" + goodId);
                var quote = tradeSnapshot.GetMarketQuote(port.Id, goodId);
                row.Q<Label>("town").text = Number(port.Inventory[goodId]);
                row.Q<Label>("ship").text = Number(ship.Cargo[goodId]);
                row.Q<Label>("bid").text = quote.Bid.HasValue ? Number(quote.Bid.Value) : "—";
                row.Q<Label>("ask").text = quote.Ask.HasValue ? Number(quote.Ask.Value) : "—";
                row.EnableInClassList("selected-good", goodId == SelectedGoodId());
            }
            SetText("tradeGoodDescription", Session.Content.GetGood(SelectedGoodId()).Description);
            var valid = int.TryParse(Element<TextField>("quantityField").value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var quantity) && quantity > 0;
            foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
            {
                var prefix = direction == TradeDirection.Buy ? "buy" : "sell";
                TradeAttempt preview = null;
                string text;
                try
                {
                    preview = valid ? tradeSnapshot.PreviewTrade(ship.Id, port.Id, new TradeCommand(direction, SelectedGoodId(), quantity)) : null;
                    text = preview == null ? "Enter a positive whole quantity." : DetailedTradeText(preview, ship.Cash, ship.CargoUnits, ship.CargoCapacity);
                }
                catch (OverflowException) { text = "This quantity exceeds the supported financial bounds. Choose a smaller order."; }
                SetText(prefix + "PreviewLabel", text);
                Element<Button>(prefix + "Button").SetEnabled(TradeIsOpen && canControl && local && preview?.Outcome == TradeOutcome.Traded);
            }
            Element<Button>("quantityMaxBuy").SetEnabled(canControl && local);
            Element<Button>("quantityMaxSell").SetEnabled(canControl && local);
            RefreshModalStatus();
        }

        public static string AverageExecutionPrice(TradeAttempt preview) => preview.Price == null || preview.Quantity <= 0
            ? "—" : ((decimal)preview.TotalAmount / preview.Quantity).ToString("0.00", CultureInfo.InvariantCulture);

        private static string DetailedTradeText(TradeAttempt preview, long cash, long units, int capacity)
        {
            if (preview.Price == null) return TradingLabText.Outcome(preview.Outcome) + ".";
            var text = "Total " + Number(preview.TotalAmount) + "   ·   Average " + AverageExecutionPrice(preview) + " / unit" +
                "\nFirst " + Number(preview.Price.FirstMarginalPrice) + "  →  last " + Number(preview.Price.LastMarginalPrice);
            if (preview.Outcome != TradeOutcome.Traded) return text + "\n" + TradingLabText.Outcome(preview.Outcome);
            var buying = preview.Direction == TradeDirection.Buy;
            return text + "\nAfter: cash " + Number(buying ? checked(cash - preview.TotalAmount) : checked(cash + preview.TotalAmount)) +
                "   ·   cargo " + (buying ? units + preview.Quantity : units - preview.Quantity) + "/" + capacity;
        }

        private void RefreshModalStatus()
        {
            foreach (var name in new[] { "tradeStatusLabel", "industryStatusLabel" })
            {
                var label = Root.Q<Label>(name);
                if (label == null) continue;
                label.text = status ?? Session.LastStatus;
                label.EnableInClassList("error", statusIsError);
            }
        }

        private void RefreshChallenge()
        {
            var challenge = Session.Snapshot.Challenge;
            if (challenge == null)
            {
                SetText("challengeLabel", "SANDBOX · Build your enterprise at your own pace. No deadline.");
                return;
            }
            var goal = challenge.Configuration;
            SetText("challengeLabel", goal.Title + " · " + challenge.Status + "\n" + challenge.DeliveredQuantity + "/" + goal.RequiredQuantity +
                " net " + Session.Content.GetGood(goal.GoodId).Name + " supplied to " + PortName(goal.PortId) +
                " · " + Math.Max(0, goal.DeadlineDay - Session.Snapshot.Day) + " days left · retain cash ≥ " + Number(goal.MinimumCash));
        }
    }
}
