using System;
using System.Collections;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ArchipelagoReadabilityTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;
        private bool hadPreference;
        private int originalPreference;

        [UnitySetUp]
        public IEnumerator LoadTitle()
        {
            hadPreference = PlayerPrefs.HasKey(ArchipelagoTextScale.PreferenceKey);
            originalPreference = PlayerPrefs.GetInt(ArchipelagoTextScale.PreferenceKey);
            PlayerPrefs.SetInt(ArchipelagoTextScale.PreferenceKey, (int)ArchipelagoUiScale.Default);
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            Assert.That(controller, Is.Not.Null);
            root = controller.Root;
            Assert.That(controller.TextScale, Is.Not.Null);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (hadPreference) PlayerPrefs.SetInt(ArchipelagoTextScale.PreferenceKey, originalPreference);
            else PlayerPrefs.DeleteKey(ArchipelagoTextScale.PreferenceKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator ScaleChangesReachLiveAndNewControlsWithoutAnyEconomicMutation()
        {
            controller.BeginRun(null, RunScenario.IndustrialCharter);
            Click("port_" + controller.Session.Player.DockedPortId);
            var before = controller.Session.Snapshot;
            var dynamicCopy = new Label("Newly created account detail") { name = "readabilityProbe" };
            dynamicCopy.AddToClassList("run-copy");
            root.Q<ScrollView>("contextScroll").Add(dynamicCopy);
            var bodies = new[] { 14f, 17f, 21f, 25f };
            var captions = new[] { 12f, 15f, 19f, 23f };
            foreach (ArchipelagoUiScale scale in Enum.GetValues(typeof(ArchipelagoUiScale)))
            {
                Click("menuButton"); Click("pauseSettingsButton");
                var picker = root.Q<DropdownField>("textScaleDropdown");
                picker.value = picker.choices[(int)scale];
                Assert.That(controller.TextScale.Current, Is.EqualTo(scale));
                Cancel(); Click("resumeButton");
                yield return null; yield return null;
                Assert.That(dynamicCopy.resolvedStyle.fontSize, Is.EqualTo(bodies[(int)scale]).Within(0.1f));
                Assert.That(root.Q<Label>("challengeLabel").resolvedStyle.fontSize, Is.EqualTo(captions[(int)scale]).Within(0.1f));
                Assert.That(root.Q<Button>("playButton").resolvedStyle.fontSize, Is.EqualTo(bodies[(int)scale]).Within(0.1f));
                Assert.That(root.Q(className: "route-distance").resolvedStyle.fontSize, Is.EqualTo(captions[(int)scale]).Within(0.1f));
                Click("openTradeButton");
                yield return null;
                Assert.That(root.Q<TextField>("quantityField").resolvedStyle.fontSize, Is.EqualTo(bodies[(int)scale]).Within(0.1f));
                Assert.That(root.Q<DropdownField>("goodDropdown").resolvedStyle.fontSize, Is.EqualTo(bodies[(int)scale]).Within(0.1f));
                var quantity = root.Q<TextField>("quantityField");
                quantity.Focus(); quantity.value = "3";
                Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("Total"));
                Assert.That(root.focusController.focusedElement, Is.Not.Null);
                Cancel();
                Assert.That(controller.TradeIsOpen, Is.False);
                Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay), "Cancel closes the overlay before opening pause.");
                Click("menuButton"); Click("pauseEncyclopediaButton");
                yield return null;
                Assert.That(root.Q<Label>("encyclopediaEntryTitle").resolvedStyle.fontSize, Is.GreaterThan(bodies[(int)scale]));
                Cancel(); Click("resumeButton");
                Assert.That(controller.Session.Snapshot, Is.SameAs(before));
                Assert.That(controller.Session.Snapshot.Day, Is.Zero);
                Assert.That(controller.Session.Snapshot.Revision, Is.Zero);
                Assert.That(controller.Session.IsPlaying, Is.False);
            }
            dynamicCopy.RemoveFromHierarchy();
        }

        [UnityTest]
        public IEnumerator EveryScaleKeepsScreensAndScrollableActionsReachableAtBothResolutions()
        {
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            foreach (ArchipelagoUiScale scale in Enum.GetValues(typeof(ArchipelagoUiScale)))
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = size.x; root.style.height = size.y;
                controller.TextScale.Apply(scale, false);
                yield return null; yield return null;
                yield return Reachable("newRunButton");
                yield return Reachable("menuExitButton");
                Click("menuSettingsButton");
                yield return Reachable("textScaleDropdown");
                yield return Reachable("smoothMovementToggle");
                yield return Reachable("settingsBackButton");
                Click("settingsBackButton");
                Click("newRunButton");
                yield return Reachable("setupModeHeadline");
                yield return Reachable("seedField");
                yield return Reachable("randomizeSeedButton");
                yield return Reachable("copySeedButton");
                yield return Reachable("difficultyPicker");
                yield return Reachable("challengePicker");
                yield return Reachable("charterPicker");
                yield return Reachable("startRunButton");
                Click("setupHelpButton");
                yield return Reachable("encyclopediaSearch");
                yield return Reachable("encyclopediaCategory");
                yield return Reachable("reference_bread");
                Click("reference_bread");
                yield return Reachable("related_bake_bread");
                yield return Reachable("encyclopediaBackButton");
                yield return Reachable("encyclopediaCloseButton");
                Click("encyclopediaCloseButton");
                Click("setupBackButton");
                controller.BeginRun(null, RunScenario.IndustrialCharter);
                yield return null; yield return null;
                Assert.That(controller.Map.contentRect.height, Is.GreaterThanOrEqualTo(350), "The readable chart must not collapse at " + size + " / " + scale);
                foreach (var marker in controller.Map.Query<Button>(className: "port-marker").ToList())
                {
                    Assert.That(controller.Map.worldBound.Contains(marker.worldBound.min), Is.True, marker.name);
                    Assert.That(controller.Map.worldBound.Contains(marker.worldBound.max), Is.True, marker.name);
                }
                Assert.That(controller.Presentation.HasSelection, Is.False);
                Assert.That(root.Q("deckBody").ClassListContains("hidden"), Is.True);
                Click("port_" + controller.Session.Player.DockedPortId);
                yield return Reachable("openTradeButton");
                Click("openTradeButton");
                yield return null; yield return null;
                if (scale == ArchipelagoUiScale.Default)
                    foreach (var name in new[] { "tradeGood_bread", "quantityField", "buyPreviewLabel", "sellPreviewLabel", "buyButton", "sellButton" })
                        AssertVisible(name, "Default trade core should fit without scrolling.");
                yield return Reachable("tradeGood_bread");
                yield return Reachable("goodDropdown");
                yield return Reachable("quantityField");
                yield return Reachable("quantityMaxBuy");
                yield return Reachable("quantityMaxSell");
                yield return Reachable("buyButton");
                yield return Reachable("sellButton");
                yield return Reachable("closeTradeButton");
                yield return Reachable("tradeAvailabilityLabel");
                Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False);
                var town = root.Q<Button>("port_" + WorldIds.West);
                Assert.That(root.panel.Pick(town.worldBound.center), Is.Not.SameAs(town), "Trade backdrop blocks the town marker.");
                Cancel();
                Click("openIndustryButton");
                yield return Reachable("closeIndustryButton");
                yield return Reachable("constructButton");
                yield return Reachable("facilityDropdown");
                yield return Reachable("recommendIndustryPresetButton");
                yield return Reachable("applyIndustryPolicy");
                var industryBefore = controller.Session.Snapshot;
                root.Q<Foldout>("industryAdvancedFoldout").value = true;
                root.Q<Foldout>("industryDetailsFoldout").value = true;
                yield return Reachable("procurementEnabled");
                yield return Reachable("industryCashReserve");
                root.Q<Foldout>("industryDetailsFoldout").value = false;
                root.Q<Foldout>("industryShippingFoldout").value = true;
                yield return Reachable("industryShippingButton");
                root.Q<Foldout>("industryShippingFoldout").value = false;
                root.Q<Foldout>("constructionRulesFoldout").value = true;
                yield return Reachable("closeIndustryButton");
                root.Q<Foldout>("constructionRulesFoldout").value = false;
                Assert.That(controller.Session.Snapshot, Is.SameAs(industryBefore));
                yield return Reachable("storageQuantity");
                yield return Reachable("shipToWarehouse");
                yield return Reachable("warehouseToShip");
                yield return Reachable("warehouseBuy");
                yield return Reachable("warehouseSell");
                Assert.That(root.panel.Pick(town.worldBound.center), Is.Not.SameAs(town), "Industry backdrop blocks the town marker.");
                Cancel();
                Click("mapDetailsButton");
                yield return Reachable("contextControlsTab");
                Click("contextControlsTab");
                yield return Reachable("routePlannerButton");
                Click("routePlannerButton");
                yield return Reachable("itineraryApplyButton");
                yield return Reachable("itineraryShip");
                Click("itineraryBackButton");
                Click("ledgerButton");
                yield return Reachable("ledgerBackButton");
                Click("ledgerBackButton");
                Click("inspectorBackButton");
                Click("menuButton");
                yield return Reachable("resumeButton");
                yield return Reachable("restartRunButton");
                yield return Reachable("returnMenuButton");
                yield return Reachable("pauseExitButton");
                Click("pauseExitButton");
                yield return Reachable("confirmCancelButton");
                yield return Reachable("confirmAcceptButton");
                Cancel(); Click("resumeButton");
                controller.BeginRun(CurrentPlayTestRun.Commission(RunScenario.IndustrialCharter));
                Click("menuButton"); Click("abandonRunButton"); Click("confirmAcceptButton");
                Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
                yield return Reachable("retryRunButton");
                yield return Reachable("resultsMenuButton");
                Click("resultsMenuButton");
            }
        }

        [UnityTest]
        public IEnumerator TradeTableAndOrderFormNeverOverlapAtAnyScaleOrSupportedResolution()
        {
            controller.BeginRun(null, RunScenario.IndustrialCharter);
            Click("port_" + controller.Session.Player.DockedPortId);
            var before = controller.Session.Snapshot;
            Click("openTradeButton");
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            foreach (ArchipelagoUiScale scale in Enum.GetValues(typeof(ArchipelagoUiScale)))
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = size.x; root.style.height = size.y;
                controller.TextScale.Apply(scale, false);
                root.Q<ScrollView>("tradeContentScroll").scrollOffset = Vector2.zero;
                yield return null; yield return null;
                var market = root.Q(className: "trade-market").worldBound;
                var order = root.Q(className: "trade-order").worldBound;
                var rows = root.Q("tradeGoodsRows").worldBound;
                var description = root.Q("tradeGoodDescription").worldBound;
                var context = size + " / " + scale;
                Assert.That(market.height, Is.GreaterThan(0), context);
                Assert.That(order.height, Is.GreaterThan(0), context);
                Assert.That(market.Overlaps(order), Is.False, "Table and order sections: " + context);
                Assert.That(rows.Overlaps(order), Is.False, "Commodity rows and order form: " + context);
                Assert.That(rows.yMax, Is.LessThanOrEqualTo(market.yMax + 1), context);
                Assert.That(description.yMax, Is.LessThanOrEqualTo(market.yMax + 1), context);
                if (size.x == 1366 && (scale == ArchipelagoUiScale.Large || scale == ArchipelagoUiScale.ExtraLarge))
                    Assert.That(order.yMin - market.yMax, Is.GreaterThanOrEqualTo(11), "Stacked sections need a visible gap: " + context);
                yield return Reachable("tradeGood_logs");
                yield return Reachable("buyButton");
                yield return Reachable("sellButton");
                Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            }
        }

        private IEnumerator Reachable(string name)
        {
            yield return null;
            var element = root.Q(name);
            Assert.That(element, Is.Not.Null, name);
            var scroll = element.GetFirstAncestorOfType<ScrollView>();
            scroll?.ScrollTo(element);
            yield return null; yield return null;
            AssertVisible(name);
        }

        private void AssertVisible(string name, string reason = null)
        {
            var element = root.Q(name);
            var scroll = element.GetFirstAncestorOfType<ScrollView>();
            var viewport = scroll == null ? root.worldBound : scroll.contentViewport.worldBound;
            var bounds = element.worldBound;
            var message = name + " / " + controller.TextScale.Current + " / " + root.layout.size + ". " + reason;
            Assert.That(bounds.width, Is.GreaterThan(0), message);
            Assert.That(bounds.height, Is.GreaterThan(0), message);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(Math.Max(root.worldBound.xMin, viewport.xMin) - 1), message);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(Math.Max(root.worldBound.yMin, viewport.yMin) - 1), message);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Math.Min(root.worldBound.xMax, viewport.xMax) + 1), message);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Math.Min(root.worldBound.yMax, viewport.yMax) + 1), message);
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled())
            {
                var target = root.focusController.focusedElement as VisualElement ?? root;
                evt.target = target; target.SendEvent(evt);
            }
        }

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = button; button.SendEvent(evt);
            }
        }
    }
}
