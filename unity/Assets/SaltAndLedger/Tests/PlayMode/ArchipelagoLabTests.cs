using System.Collections;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ArchipelagoLabTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Session, Is.Not.Null);
            root = controller.Root;
            Assert.That(root.panel, Is.Not.Null);
            controller.BeginRun(null, RunScenario.MerchantCharter);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialDockedMarkersStayFiniteAndClearOfTownLabels()
        {
            // Start has already presented before a deliberate viewport size is assigned.
            // The default batch GameView is only 640x480; exercise the supported layouts.
            foreach (var ship in controller.Session.Snapshot.Ships.Values)
            {
                var position = controller.Map.DisplayedShipPosition(ship.Id);
                Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x), Is.False, ship.Id);
                Assert.That(float.IsNaN(position.y) || float.IsInfinity(position.y), Is.False, ship.Id);
            }
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            {
                root.style.flexGrow = 0;
                root.style.flexShrink = 0;
                root.style.width = size.x;
                root.style.height = size.y;
                yield return null;
                yield return null;
                Assert.That(root.resolvedStyle.width, Is.EqualTo(size.x).Within(1));
                Assert.That(root.resolvedStyle.height, Is.EqualTo(size.y).Within(1));
                foreach (var ship in controller.Session.Snapshot.Ships.Values)
                {
                    var marker = root.Q<Button>("ship_" + ship.Id).worldBound;
                    var port = root.Q<Button>("port_" + ship.DockedPortId).worldBound;
                    Assert.That(marker.center.y - port.center.y, Is.EqualTo(52).Within(1), ship.Id);
                    Assert.That(marker.Overlaps(port), Is.False, ship.Id);
                    Assert.That(controller.Map.worldBound.Contains(marker.center), Is.True,
                        ship.Id + " at " + size + "; map=" + controller.Map.worldBound + "; marker=" + marker);
                }
            }
        }

        [UnityTest]
        public IEnumerator FleetPickerSelectsEveryShipWhenVoyageMarkersOverlap()
        {
            Click("openTradeButton");
            root.Q<TextField>("quantityField").value = "2";
            Click("buyButton");
            if (controller.TradeIsOpen) Click("closeTradeButton");
            Click("playerShipButton");
            Click("port_" + ArchipelagoScenario.South);
            Click("sailButton");
            Click("nextDayButton");
            var snapshot = controller.Session.Snapshot;
            Assert.That(controller.Map.DisplayedShipPosition(ArchipelagoScenario.PlayerShipId),
                Is.EqualTo(controller.Map.DisplayedShipPosition(ArchipelagoScenario.TimberShipId)));
            var picker = root.Q<DropdownField>("shipPicker");
            foreach (var ship in snapshot.Ships.Values)
            {
                picker.value = ship.Name + (ship.IsPlayer ? " (you)" : " (NPC)");
                Assert.That(controller.Session.SelectedShipId, Is.EqualTo(ship.Id));
                Assert.That(root.Q<Label>("selectionTitle").text, Is.EqualTo(ship.Name));
                Assert.That(controller.Session.Snapshot, Is.SameAs(snapshot));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapSelectionShowsPortsAndReadOnlyNpcOwnershipWithoutSpendingTime()
        {
            Assert.That(controller.Map.PortMarkerCount, Is.EqualTo(4));
            Assert.That(controller.Map.ShipMarkerCount, Is.EqualTo(4));
            var before = controller.Session.Snapshot;
            Click("port_" + ArchipelagoScenario.West);
            Assert.That(root.Q<Label>("selectionTitle").text, Is.EqualTo("Westhaven"));
            Assert.That(root.Q<Label>("productionLabel").text, Does.Contain("Lumber Camp"));
            Click("ship_" + ArchipelagoScenario.TimberShipId);
            Assert.That(root.Q<Label>("selectionSubtitle").text, Does.Contain("NPC"));
            Assert.That(root.Q<Label>("shipCargoLabel").text, Does.Contain("Trader cash"));
            Click("openTradeButton");
            Assert.That(root.Q<Label>("tradeAvailabilityLabel").text, Does.Contain("NPC inspection"));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InstantDocksideControlsUpdatePricesAndRejectInvalidInputs()
        {
            Click("openTradeButton");
            root.Q<TextField>("quantityField").value = "2";
            var oldPreview = root.Q<Label>("buyPreviewLabel").text;
            Click("buyButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(2));
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Is.Not.EqualTo(oldPreview));
            root.Q<TextField>("quantityField").value = "1";
            Click("sellButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(1));
            var before = controller.Session.Snapshot;
            foreach (var text in new[] { "0", "-1", "abc", "2147483648", "" })
            {
                root.Q<TextField>("quantityField").value = text;
                Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            }
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapVoyageRequiresConfirmationAndMovesOnlyDuringWholeDays()
        {
            Click("openTradeButton");
            Click("buyButton");
            var days = controller.Session.Snapshot.TravelDays(ArchipelagoScenario.PlayerShipId, ArchipelagoScenario.South);
            if (controller.TradeIsOpen) Click("closeTradeButton");
            Click("playerShipButton");
            Click("port_" + ArchipelagoScenario.South);
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(ArchipelagoScenario.North));
            Assert.That(root.Q<Label>("voyageLabel").text, Does.Contain(days + " days"));
            Click("sailButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(root.Q<Button>("sellButton").enabledInHierarchy, Is.False);
            var start = controller.Map.DisplayedShipPosition(ArchipelagoScenario.PlayerShipId);
            Click("nextDayButton");
            Assert.That(controller.Map.DisplayedShipPosition(ArchipelagoScenario.PlayerShipId), Is.Not.EqualTo(start));
            for (var day = 1; day < days; day++) Click("nextDayButton");
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(ArchipelagoScenario.South));
            Click("openTradeButton");
            Click("sellButton");
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(days));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BothLegsPortsCommoditiesPricesAndBudgetsAreAppliedFromTheControls()
        {
            var before = controller.Session.Snapshot;
            root.Q<Foldout>("automationFoldout").value = true;
            root.Q<DropdownField>("routeOriginDropdown").value = "Westhaven";
            root.Q<DropdownField>("routeDestinationDropdown").value = "Eastport";
            root.Q<DropdownField>("outGoodDropdown").value = "Planks";
            root.Q<DropdownField>("returnGoodDropdown").value = "Logs";
            Set("outMaxQty", "4"); Set("outMinQty", "2"); Set("outBuy", "23"); Set("outSell", "29"); Set("outBudget", "70");
            Set("returnMaxQty", "3"); Set("returnMinQty", "1"); Set("returnBuy", "13"); Set("returnSell", "17"); Set("returnBudget", "34");
            Set("cashReserveField", "11");
            root.Q<Toggle>("emptyReturnToggle").value = false;
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("applyPolicyButton");
            var policy = controller.Session.Policy;
            Assert.That(policy.OriginPortId, Is.EqualTo(ArchipelagoScenario.West));
            Assert.That(policy.DestinationPortId, Is.EqualTo(ArchipelagoScenario.East));
            Assert.That(policy.Outbound.GoodId, Is.EqualTo("planks"));
            Assert.That(policy.Outbound.MaxPurchaseQuantity, Is.EqualTo(4));
            Assert.That(policy.Outbound.MinShipmentQuantity, Is.EqualTo(2));
            Assert.That(policy.Outbound.MaxBuyPrice, Is.EqualTo(23));
            Assert.That(policy.Outbound.MinSellPrice, Is.EqualTo(29));
            Assert.That(policy.Outbound.MaxPurchaseExpenditure, Is.EqualTo(70));
            Assert.That(policy.ReturnLeg.GoodId, Is.EqualTo("logs"));
            Assert.That(policy.ReturnLeg.MaxPurchaseQuantity, Is.EqualTo(3));
            Assert.That(policy.ReturnLeg.MinShipmentQuantity, Is.EqualTo(1));
            Assert.That(policy.ReturnLeg.MaxBuyPrice, Is.EqualTo(13));
            Assert.That(policy.ReturnLeg.MinSellPrice, Is.EqualTo(17));
            Assert.That(policy.ReturnLeg.MaxPurchaseExpenditure, Is.EqualTo(34));
            Assert.That(policy.CashReserve, Is.EqualTo(11));
            Assert.That(policy.AllowEmptyReturn, Is.False);
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            root.Q<DropdownField>("returnGoodDropdown").value = "None";
            root.Q<Toggle>("emptyReturnToggle").value = true;
            Click("applyPolicyButton");
            Assert.That(controller.Session.Policy.ReturnLeg, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseSpeedTradeOpeningAndResetUseThePlaybackControls()
        {
            var before = controller.Session.Snapshot;
            root.Q<DropdownField>("speedDropdown").value = "10×";
            Click("playButton");
            Assert.That(controller.Session.IsPlaying, Is.True);
            Assert.That(controller.Session.PlaybackSpeed, Is.EqualTo(10));
            Click("openTradeButton");
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("closeTradeButton");
            Click("nextDayButton");
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(1));
            Click("playButton");
            yield return new WaitForSecondsRealtime(0.2f);
            Click("pauseButton");
            Assert.That(controller.Session.Snapshot.Day, Is.GreaterThan(1));
            var paused = controller.Session.Snapshot;
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(paused));
            Click("resetButton");
            Click("confirmAcceptButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.PlaybackSpeed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ScheduledEconomyNpcMarkersAndWaitingRouteAreVisible()
        {
            var initial = controller.Map.DisplayedShipPosition(ArchipelagoScenario.TimberShipId);
            Click("nextDayButton");
            Assert.That(controller.Map.DisplayedShipPosition(ArchipelagoScenario.TimberShipId), Is.Not.EqualTo(initial));
            Assert.That(root.Q<Button>("port_" + ArchipelagoScenario.North).text, Does.Contain("1 player · 0 NPC"));
            for (var day = 1; day < 6; day++) Click("nextDayButton");
            Click("port_" + ArchipelagoScenario.North);
            Assert.That(root.Q<Label>("consumptionLabel").text, Does.Contain("cumulative unmet"));
            Assert.That(controller.Session.Activity.Any(item => item.Message.Contains("Harvest") || item.Message.Contains("consumed") ||
                item.Message.Contains("unmet demand")), Is.True);
            root.Q<Foldout>("automationFoldout").value = true;
            Set("outBuy", "1");
            Click("applyPolicyButton");
            root.Q<Toggle>("automationToggle").value = true;
            var before = controller.Session.Snapshot;
            Click("runDocksideButton");
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            Assert.That(root.Q<Label>("decisionLabel").text.Length, Is.GreaterThan(20));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RightClickOpensLocalAndRemoteMarketsAndEscapeRestoresFocus()
        {
            Click("playerShipButton");
            var before = controller.Session.Snapshot;
            var focus = root.focusController.focusedElement;
            RightClick("port_" + EnterpriseScenario.North);
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(root.Q<Label>("tradeHeader").text, Does.Contain("DOCKED"));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.True);
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False, "Modal blocks underlying clock");
            Escape();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(root.focusController.focusedElement, Is.SameAs(focus));
            RightClick("port_" + EnterpriseScenario.East);
            Assert.That(root.Q<Label>("tradeHeader").text, Does.Contain("REMOTE MARKET"));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Button>("tradeSailButton").enabledInHierarchy, Is.True);
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(EnterpriseScenario.North));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Escape();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavigationCancelClosesFocusedTradeAndIndustryWithoutAnEconomicCommand()
        {
            Click("playerShipButton");
            var before = controller.Session.Snapshot;
            var focus = root.focusController.focusedElement;
            RightClick("port_" + EnterpriseScenario.North);
            root.Q<TextField>("quantityField").Focus();
            CancelNavigation();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(root.focusController.focusedElement, Is.SameAs(focus));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").Focus();
            CancelNavigation();
            Assert.That(controller.IndustryIsOpen, Is.False);
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        private void CancelNavigation()
        {
            using (var evt = NavigationCancelEvent.GetPooled())
            {
                evt.target = root.focusController.focusedElement as VisualElement ?? root;
                (evt.target as VisualElement).SendEvent(evt);
            }
        }

        [UnityTest]
        public IEnumerator EveryCommodityMaximumAndAverageUseFreshAuthoritativeQuotes()
        {
            Click("openTradeButton");
            foreach (var good in controller.Session.Content.Goods)
            {
                Click("tradeGood_" + good.Id);
                Assert.That(root.Q<DropdownField>("goodDropdown").value, Is.EqualTo(good.Name));
            }
            Click("tradeGood_logs");
            Set("quantityField", "2");
            var preview = controller.Session.PreviewTrade(TradeDirection.Buy, "logs", 2);
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("Total " + preview.TotalAmount));
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("Average " + ArchipelagoLabController.AverageExecutionPrice(preview)));
            var max = controller.Session.Snapshot.MaximumTradeQuantity(controller.Session.Player.Id, EnterpriseScenario.North, TradeDirection.Buy, "logs");
            Click("quantityMaxBuy");
            Assert.That(root.Q<TextField>("quantityField").value, Is.EqualTo(max.ToString()));
            Click("quantityOne");
            Click("buyButton");
            Click("buyButton");
            Click("sellButton");
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(1));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TradeControlsFitBothTargetResolutionsAndBackdropBlocksTheMap()
        {
            Click("openTradeButton");
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = size.x; root.style.height = size.y;
                yield return null; yield return null;
                var dialog = root.Q<VisualElement>("tradeDialog").worldBound;
                Assert.That(root.worldBound.Contains(dialog.min), Is.True);
                Assert.That(root.worldBound.Contains(dialog.max), Is.True);
                foreach (var name in new[] { "buyButton", "sellButton", "quantityField", "closeTradeButton", "tradeStatusLabel" })
                {
                    var bounds = root.Q(name).worldBound;
                    Assert.That(dialog.Contains(bounds.min) && dialog.Contains(bounds.max), Is.True, name + " at " + size);
                }
                var town = root.Q<Button>("port_" + EnterpriseScenario.North);
                Assert.That(town.enabledInHierarchy, Is.False);
                Assert.That(root.panel.Pick(town.worldBound.center), Is.Not.SameAs(town));
            }
        }

        [UnityTest]
        public IEnumerator PrivateConstructionPoliciesProductionAndShipTransfersUseTheControls()
        {
            Click("openTradeButton");
            Set("quantityField", "2"); Click("buyButton"); Click("closeTradeButton");
            var cash = controller.Session.Player.Cash;
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Sawmill";
            Click("constructButton");
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(cash - facility.ConstructionCost));
            Assert.That(facility.StoredUnits, Is.Zero);
            root.Q<DropdownField>("storageGoodDropdown").value = "Logs";
            Set("storageQuantity", "2");
            var before = controller.Session.Snapshot;
            Click("shipToWarehouse");
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Ships[controller.Session.Player.Id].Cash));
            Assert.That(controller.Session.Snapshot.PrivateFacilities[facility.Id].Inventory["logs"], Is.EqualTo(2));
            root.Q<Toggle>("procurementEnabled").value = false;
            root.Q<Toggle>("salesEnabled").value = false;
            Click("applyIndustryPolicy"); Click("closeIndustryButton");
            for (var day = 0; day < facility.Blueprint.IntervalDays; day++) Click("nextDayButton");
            facility = controller.Session.Snapshot.PrivateFacilities[facility.Id];
            Assert.That(facility.Inventory["planks"], Is.EqualTo(1));
            Assert.That(facility.BatchesProduced, Is.EqualTo(1));
            Click("openIndustryButton");
            root.Q<DropdownField>("storageGoodDropdown").value = "Planks"; Set("storageQuantity", "1");
            Click("warehouseToShip");
            Assert.That(controller.Session.Player.Cargo["planks"], Is.EqualTo(1));
            Assert.That(controller.Session.Snapshot.PrivateFacilities[facility.Id].Inventory["planks"], Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChallengeDisplayEndingAndResetUseTheSameEconomicDay()
        {
            Click("startChallengeButton");
            Click("confirmAcceptButton");
            Click("startRunButton");
            Assert.That(controller.Session.ChallengeMode, Is.True);
            Assert.That(root.Q<Label>("challengeLabel").text, Does.Contain("0 / " + EnterpriseScenario.CharterBread));
            for (var day = 0; day < EnterpriseScenario.CharterDeadline; day++) Click("nextDayButton");
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Failed));
            Assert.That(root.Q<Label>("challengeLabel").text, Does.Contain("Failed"));
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False);
            Click("retryRunButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Active));
            Click("startSandboxButton");
            Click("confirmAcceptButton");
            Click("startRunButton");
            Assert.That(controller.Session.Snapshot.Challenge, Is.Null);
            yield return null;
        }

        private void RightClick(string name)
        {
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 1 }))
            {
                evt.target = root.Q(name);
                root.Q(name).SendEvent(evt);
            }
        }

        private void Escape()
        {
            using (var evt = KeyDownEvent.GetPooled(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape }))
            {
                evt.target = root.focusController.focusedElement as VisualElement ?? root;
                (evt.target as VisualElement).SendEvent(evt);
            }
        }

        private void Set(string name, string value) => root.Q<TextField>(name).value = value;

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
