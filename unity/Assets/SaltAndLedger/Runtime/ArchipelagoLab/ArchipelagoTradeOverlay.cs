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
            Click("startChallengeButton", () => Confirm("Choose a new run?",
                "Current progress will be discarded. Choose the next challenge and starting charter in run setup.", () => OpenSetup(false)));
            Click("startSandboxButton", () => Confirm("Choose a new sandbox?",
                "Current progress will be discarded. Choose your next starting company in run setup.", () => OpenSetup(true)));
            BindIndustry();
            BindHarbor();
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
                harborFacilityId = null;
                Session.InspectTradePort(portId);
                ShowModal("tradeBackdrop", "closeTradeButton");
                RefreshHarborStorageSelection();
                SetIndex("harborTradeTarget", !HarborShipIsLocal() && harborFacilityId != null ? 1 : 0);
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
            decimal.TryParse(Element<TextField>("quantityField").value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value);
            Element<TextField>("quantityField").value = Math.Max(1, value + delta).ToString("0.####", CultureInfo.InvariantCulture);
        }

        private void SetMaximum(TradeDirection direction)
        {
            var snapshot = Session.Snapshot;
            var maximum = HarborUsesLocalStock
                ? harborFacilityId == null ? 0 : snapshot.MaximumWarehouseTradeQuantity(Session.Player.OwnerId, harborFacilityId, direction, SelectedGoodId())
                : tradeShipId == null ? 0 : snapshot.MaximumTradeQuantity(tradeShipId, Session.SelectedPortId, direction, SelectedGoodId());
            Element<TextField>("quantityField").value = Q(maximum);
        }

        private void RefreshTradeOverlay() => RefreshHarbor();

        public static string AverageExecutionPrice(TradeAttempt preview) => preview.Price == null || preview.Quantity <= 0
            ? "—" : ((decimal)preview.TotalAmount / preview.Quantity).ToString("0.00", CultureInfo.InvariantCulture);

        private string ServiceFeeText(TradeAttempt preview)
        {
            if (preview.Outcome != TradeOutcome.Traded || preview.Direction != TradeDirection.Sell || Session.Snapshot.Circulation == null)
                return "";
            return "\nService fee " + Money(preview.ServiceFee) + " · net receipts " + Money(preview.TraderCashChange.Delta) +
                "\nFee to market " + Money(preview.MarketServiceFee) + " · local workers " + Money(preview.HouseholdServiceFee);
        }

        private string DetailedTradeText(TradeAttempt preview, long cash, long units, int capacity)
        {
            if (preview.Price == null) return TradingLabText.Outcome(preview.Outcome) + ".";
            var text = "Total " + Money(preview.TotalAmount) + "   ·   Average " + AverageExecutionPrice(preview) + " / unit" +
                "\nFirst " + Number(preview.Price.FirstMarginalPrice) + "  →  last " + Number(preview.Price.LastMarginalPrice);
            if (preview.Outcome != TradeOutcome.Traded) return text + "\n" + TradingLabText.Outcome(preview.Outcome);
            var buying = preview.Direction == TradeDirection.Buy;
            return text + ServiceFeeText(preview) + "\nAfter: cash " + Money(preview.TraderCashChange.After) +
                "   ·   cargo " + Q(buying ? units + preview.Quantity : units - preview.Quantity) + "/" + Q(capacity);
        }

        private void RefreshModalStatus()
        {
            var navigationStatus = Root.Q<Label>("navigationStatus");
            if (navigationStatus != null) navigationStatus.text = statusIsError ? status : "";
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
                Element<Label>("challengeLabel").tooltip = "";
                return;
            }
            if (Session.Identity?.Commission != null)
            {
                SetText("challengeLabel", ObjectiveText());
                Element<Label>("challengeLabel").tooltip = "All four drafted objectives and the cash minimum must hold together. Open Objectives for exact conditions and blockers.";
                return;
            }
            if (Session.Snapshot.Economy != null)
            {
                var progress = ExpandingChallenges.Progress(Session.Snapshot);
                SetText("challengeLabel", challenge.Configuration.Title + " · " + challenge.Status + " · " + progress.Percent + "%\n" +
                    Math.Max(0, challenge.Configuration.DeadlineDay - Session.Snapshot.Day) + " days remaining · Cash " + Money(Session.Player.Cash));
                Element<Label>("challengeLabel").tooltip = ObjectiveText();
                return;
            }
            if (challenge.Recovery != null)
            {
                var recovery = challenge.Recovery;
                SetText("challengeLabel", "CITY RECOVERY · " + PortName(challenge.Configuration.PortId) + " · " + challenge.Status +
                    "\nPaid food " + recovery.FulfillmentPercent + "% / " + challenge.Configuration.RequiredQuantity + "% · " +
                    recovery.Observations + "/" + recovery.RequiredObservations + " observations · prosperity " + recovery.Prosperity + "/" + recovery.MinimumProsperity +
                    "\n" + Math.Max(0, challenge.Configuration.DeadlineDay - Session.Snapshot.Day) + " days left · Cash " + Session.Player.Cash + "/" + challenge.Configuration.MinimumCash);
                Element<Label>("challengeLabel").tooltip = ObjectiveText();
                return;
            }
            SetText("challengeLabel", ObjectiveText());
            Element<Label>("challengeLabel").tooltip = "";
        }
    }
}
