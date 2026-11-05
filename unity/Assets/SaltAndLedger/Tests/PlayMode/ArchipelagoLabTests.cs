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
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            Assert.That(root.panel, Is.Not.Null);
            controller.BeginRun(null, RunScenario.MerchantCharter);
            Click("port_" + controller.Session.Player.DockedPortId);
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
                    Assert.That(marker.yMin - port.yMax, Is.EqualTo(8).Within(1), ship.Id);
                    Assert.That(marker.Overlaps(port), Is.False, ship.Id);
                    Assert.That(controller.Map.worldBound.Contains(marker.min) &&
                        controller.Map.worldBound.Contains(marker.max), Is.True,
                        ship.Id + " at " + size + "; map=" + controller.Map.worldBound + "; marker=" + marker);
                }
            }
        }

        [UnityTest]
        public IEnumerator FleetPickerSelectsEveryShipWhenVoyageMarkersOverlap()
        {
            Click("openTradeButton");
            root.Q<TextField>("quantityField").value = "2";
            Arrow("logs", true);
            if (controller.TradeIsOpen) Click("closeTradeButton");
            Click("ship_" + controller.Session.Player.Id);
            Click("port_" + GeneratedWorldFixture.Destination(controller.Session.Snapshot));
            Click("sailButton");
            Click("nextDayButton");
            var snapshot = controller.Session.Snapshot;
            Assert.That(controller.Map.DisplayedShipPosition(WorldIds.PlayerShipId),
                Is.EqualTo(controller.Map.DisplayedShipPosition(GeneratedWorldFixture.Npc(controller.Session.Snapshot))));
            Click("deckInspectButton");
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
            Assert.That(controller.Map.PortMarkerCount, Is.EqualTo(controller.Session.Snapshot.Ports.Count));
            Assert.That(controller.Map.ShipMarkerCount, Is.EqualTo(controller.Session.Snapshot.Ships.Count));
            var before = controller.Session.Snapshot;
            Click("port_" + GeneratedWorldFixture.Destination(controller.Session.Snapshot));
            Assert.That(root.Q<Label>("selectionTitle").text, Is.EqualTo("Westhaven"));
            Assert.That(root.Q<Label>("productionLabel").text, Does.Contain("Lumber Camp"));
            Click("ship_" + GeneratedWorldFixture.Npc(controller.Session.Snapshot));
            Assert.That(root.Q<Label>("selectionSubtitle").text, Does.Contain("NPC"));
            Assert.That(root.Q<Label>("shipCargoLabel").text, Does.Contain("Shared company cash"));
            Click("openTradeButton");
            Assert.That(root.Q<Label>("tradeAvailabilityLabel").text, Does.Contain("NPC inspection"));
            Assert.That(Row("logs").Q<Button>("rightArrow").enabledInHierarchy, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InstantDocksideControlsUpdatePricesAndRejectInvalidInputs()
        {
            Click("openTradeButton");
            root.Q<TextField>("quantityField").value = "2";
            var oldPreview = Row("logs").Q<Button>("rightArrow").tooltip;
            Arrow("logs", true);
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(20000));
            Assert.That(Row("logs").Q<Button>("rightArrow").tooltip, Is.Not.EqualTo(oldPreview));
            root.Q<TextField>("quantityField").value = "1";
            Arrow("logs", false);
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(10000));
            var before = controller.Session.Snapshot;
            foreach (var text in new[] { "0", "-1", "abc", "2147483648", "" })
            {
                root.Q<TextField>("quantityField").value = text;
                Assert.That(Row("logs").Q<Button>("rightArrow").enabledInHierarchy, Is.False);
            }
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapVoyageRequiresConfirmationAndMovesOnlyDuringWholeDays()
        {
            Click("openTradeButton");
            Arrow("logs", true);
            var days = controller.Session.Snapshot.TravelDays(WorldIds.PlayerShipId, GeneratedWorldFixture.Destination(controller.Session.Snapshot));
            if (controller.TradeIsOpen) Click("closeTradeButton");
            Click("ship_" + controller.Session.Player.Id);
            Click("port_" + GeneratedWorldFixture.Destination(controller.Session.Snapshot));
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(ExpandingEconomyGeneration.Generate(42).FoodSourcePortId));
            Assert.That(root.Q<Label>("voyageLabel").text, Does.Contain(days + " days"));
            Click("sailButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(Row("logs").Q<Button>("leftArrow").enabledInHierarchy, Is.False);
            var start = controller.Map.DisplayedShipPosition(WorldIds.PlayerShipId);
            Click("nextDayButton");
            Assert.That(controller.Map.DisplayedShipPosition(WorldIds.PlayerShipId), Is.Not.EqualTo(start));
            for (var day = 1; day < days; day++) Click("nextDayButton");
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(GeneratedWorldFixture.Destination(controller.Session.Snapshot)));
            Click("openTradeButton");
            Arrow("logs", false);
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(days));
            yield return null;
        }


        [UnityTest]
        public IEnumerator PauseSpeedTradeOpeningAndResetUseThePlaybackControls()
        {
            var before = controller.Session.Snapshot;
            Click("approvedSpeed10");
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
            Click("deckInspectButton"); Click("resetButton");
            Click("confirmAcceptButton");
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.PlaybackSpeed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ScheduledEconomyAndNpcMarkersAreVisible()
        {
            var initial = controller.Map.DisplayedShipPosition(GeneratedWorldFixture.Npc(controller.Session.Snapshot));
            Click("nextDayButton");
            Assert.That(controller.Map.DisplayedShipPosition(GeneratedWorldFixture.Npc(controller.Session.Snapshot)), Is.Not.EqualTo(initial));
            var north = controller.Session.Snapshot.Ports[GeneratedWorldFixture.Port(controller.Session.Snapshot, 0)];
            Assert.That(root.Q<Button>("port_" + north.Id).text, Is.EqualTo(north.Name + "\n" + ArchipelagoMap.ShortRole(north.Role)));
            Assert.That(root.Q<Button>("ship_" + controller.Session.Player.Id).tooltip, Does.Contain("Docked at " + controller.Session.Snapshot.Ports[controller.Session.Player.DockedPortId].Name));
            Assert.That(root.Q<Button>("ship_" + GeneratedWorldFixture.Npc(controller.Session.Snapshot)).ClassListContains("npc-marker"), Is.True);
            for (var day = 1; day < 6; day++) Click("nextDayButton");
            Click("port_" + GeneratedWorldFixture.Port(controller.Session.Snapshot, 0));
            Assert.That(root.Q<Label>("consumptionLabel").text, Does.Contain("cumulative unmet"));
            Assert.That(controller.Session.Activity.Any(item => item.Message.Contains("Harvest") || item.Message.Contains("consumed") ||
                item.Message.Contains("unmet demand")), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RightClickOpensLocalAndRemoteMarketsAndEscapeRestoresFocus()
        {
            Click("ship_" + controller.Session.Player.Id);
            var before = controller.Session.Snapshot;
            var focus = root.focusController.focusedElement;
            RightClick("port_" + controller.Session.Player.DockedPortId);
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(root.Q<Label>("tradeHeader").text, Does.Contain("Docked"));
            Assert.That(Row("logs").Q<Button>("rightArrow").enabledInHierarchy, Is.True);
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False, "Modal blocks underlying clock");
            Escape();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(root.focusController.focusedElement, Is.SameAs(focus));
            RightClick("port_" + GeneratedWorldFixture.Port(controller.Session.Snapshot, 2));
            Assert.That(root.Q<Label>("tradeHeader").text, Does.Contain("Cargo remote"));
            Assert.That(Row("logs").Q<Button>("rightArrow").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Button>("tradeSailButton").enabledInHierarchy, Is.True);
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(ExpandingEconomyGeneration.Generate(42).FoodSourcePortId));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Escape();
            yield return null;
        }

        [UnityTest]
        public IEnumerator NavigationCancelClosesFocusedTradeAndIndustryWithoutAnEconomicCommand()
        {
            Click("ship_" + controller.Session.Player.Id);
            var before = controller.Session.Snapshot;
            var focus = root.focusController.focusedElement;
            RightClick("port_" + controller.Session.Player.DockedPortId);
            root.Q<TextField>("quantityField").Focus();
            CancelNavigation();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(root.focusController.focusedElement, Is.SameAs(focus));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("openIndustryButton");
            root.Q<Button>("industryWarehouseTab").Focus();
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
                Good(good.Id);
                Assert.That(root.Q<DropdownField>("goodDropdown").value, Is.EqualTo(good.Name));
            }
            Good("logs");
            Set("quantityField", "2");
            var preview = controller.Session.PreviewTrade(TradeDirection.Buy, "logs", 20000);
            Assert.That(Row("logs").Q<Button>("rightArrow").tooltip, Does.Contain("exact payment " + controller.Session.Coins(preview.TotalAmount)));
            root.Q<Foldout>("harborCalculations").value = true;
            Assert.That(root.Q<Label>("buyCalculation").text, Does.Contain("exact total " + controller.Session.Coins(preview.TotalAmount))
                .And.Contain("average " + ArchipelagoLabController.AverageExecutionPrice(preview))
                .And.Contain("First marginal " + preview.Price.FirstMarginalPrice + " → last " + preview.Price.LastMarginalPrice));
            root.Q<Foldout>("harborCalculations").value = false;
            var max = controller.Session.Snapshot.MaximumTradeQuantity(controller.Session.Player.Id, controller.Session.Player.DockedPortId, TradeDirection.Buy, "logs");
            Click("quantityMaxBuy");
            Assert.That(root.Q<TextField>("quantityField").value, Is.EqualTo(controller.Session.Quantity(max)));
            Click("quantityOne");
            Arrow("logs", true);
            Arrow("logs", true);
            Arrow("logs", false);
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(10000));
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
                foreach (var name in new[] { "approvedQuantityBar", "quantityField", "closeTradeButton", "tradeStatusLabel" })
                {
                    var bounds = root.Q(name).worldBound;
                    Assert.That(dialog.Contains(bounds.min) && dialog.Contains(bounds.max), Is.True, name + " at " + size);
                }
                var town = root.Q<Button>("port_" + GeneratedWorldFixture.Port(controller.Session.Snapshot, 0));
                Assert.That(town.enabledInHierarchy, Is.False);
                Assert.That(root.panel.Pick(town.worldBound.center), Is.Not.SameAs(town));
            }
        }

        [UnityTest]
        public IEnumerator PrivateConstructionPoliciesProductionAndShipTransfersUseTheControls()
        {
            Click("openTradeButton");
            Set("quantityField", "2"); Arrow("logs", true); Click("closeTradeButton");
            var processing = ExpandingEconomyGeneration.Generate(42).ProcessingPortId;
            Click("ship_" + controller.Session.Player.Id); Click("port_" + processing); Click("sailButton");
            while (controller.Session.Player.DockedPortId == null) Click("nextDayButton");
            var cash = controller.Session.Player.Cash;
            Click("openIndustryButton");
            var blueprint = controller.Session.Snapshot.IndustryBlueprints.Values.Single(value => value.BuildingId == "sawmill");
            Click("construct_" + blueprint.Id);
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(cash - facility.ConstructionCost));
            Assert.That(facility.StoredUnits, Is.Zero);
            Click("facilityPriorityHigh"); Click("applyIndustryPolicy");
            Assert.That(controller.Session.Snapshot.PrivateFacilities[facility.Id].Priority, Is.EqualTo(ProductionPriority.High));
            Click("industryHarborButton"); Click("pairWarehouseShip");
            Set("quantityField", "2");
            var before = controller.Session.Snapshot;
            Arrow("logs", false);
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Ships[controller.Session.Player.Id].Cash));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["logs"], Is.EqualTo(20000));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Policy.Rules, Is.Empty);
            Click("closeTradeButton");
            for (var day = 0; day < facility.Blueprint.IntervalDays; day++) Click("nextDayButton");
            facility = controller.Session.Snapshot.PrivateFacilities[facility.Id];
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["planks"], Is.EqualTo(10000));
            Assert.That(facility.BatchesProduced, Is.EqualTo(1));
            controller.OpenTrade(processing); Click("pairWarehouseShip"); Set("quantityField", "1");
            Arrow("planks", true);
            Assert.That(controller.Session.Player.Cargo["planks"], Is.EqualTo(10000));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["planks"], Is.Zero);
            yield return null;
        }


        private VisualElement Row(string id) => root.Q("tradeGood_" + id);
        private void Good(string id) => Submit(Row(id).Q<Button>("good"));
        private void Arrow(string id, bool right) => Submit(Row(id).Q<Button>(right ? "rightArrow" : "leftArrow"));
        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null); Assert.That(button.enabledInHierarchy, Is.True, button.name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
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
