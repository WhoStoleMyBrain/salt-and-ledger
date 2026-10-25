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
    public sealed class RunExperienceTests
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
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Session, Is.Not.Null);
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            Assert.That(root.panel, Is.Not.Null);
        }

        [TearDown]
        public void RestorePreference()
        {
            if (hadPreference) PlayerPrefs.SetInt(ArchipelagoTextScale.PreferenceKey, originalPreference);
            else PlayerPrefs.DeleteKey(ArchipelagoTextScale.PreferenceKey);
            PlayerPrefs.Save();
        }

        [UnityTest]
        public IEnumerator StartupShowsTitleAndSelectedCharterCreatesAuthoritativeAssets()
        {
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.MainMenu));
            Assert.That(controller.HasActiveRun, Is.False);
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(root.Q<Button>("playButton").enabledInHierarchy, Is.False);
            Click("newRunButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Setup));
            root.Q<Foldout>("advancedWorldFoldout").value = true;
            root.Q<DropdownField>("worldModePicker").value = root.Q<DropdownField>("worldModePicker").choices[0];
            Assert.That(root.Q<DropdownField>("charterPicker").choices.Count, Is.EqualTo(3));
            Assert.That(root.Q<DropdownField>("challengePicker").choices.Count, Is.EqualTo(4));
            root.Q<DropdownField>("charterPicker").value = RunScenario.GetCharter(RunScenario.IndustrialCharter).Name;
            Assert.That(root.Q<Label>("setupAssets").text, Does.Contain("300"));
            Assert.That(root.Q<Label>("setupTradeoff").text, Does.Contain("8-unit"));
            Assert.That(root.Q<Label>("setupObjective").text, Does.Contain("24"));
            Click("startRunButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.CharterId, Is.EqualTo(RunScenario.IndustrialCharter));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(300));
            Assert.That(controller.Session.Player.CargoCapacity, Is.EqualTo(8));
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(EnterpriseScenario.West));
            Assert.That(controller.Session.Snapshot.PrivateFacilities.ContainsKey(RunScenario.StarterBakery), Is.True);
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeClosesTradeThenPausesAndConfirmationCancelsOnlyItself()
        {
            BeginMerchant();
            Click("playButton");
            Click("openTradeButton");
            var before = controller.Session.Snapshot;
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Pause));
            Click("restartRunButton");
            Assert.That(controller.ConfirmationIsOpen, Is.True);
            Cancel();
            Assert.That(controller.ConfirmationIsOpen, Is.False);
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Pause));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseHelpReturnsToPauseAndResumeRestoresPreviousPlayback()
        {
            BeginMerchant();
            Click("playButton");
            var before = controller.Session.Snapshot;
            Cancel();
            Assert.That(controller.Session.IsPlaying, Is.False);
            Click("pauseEncyclopediaButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Encyclopedia));
            Click("encyclopediaCloseButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Pause));
            Cancel();
            Assert.That(controller.Session.IsPlaying, Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            controller.Session.Pause();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EncyclopediaSearchCategoriesRelatedLinksAndBackUseGeneratedEntries()
        {
            Click("menuEncyclopediaButton");
            root.Q<TextField>("encyclopediaSearch").value = "bread";
            Assert.That(root.Q<Button>("reference_bread"), Is.Not.Null);
            Click("reference_bread");
            Assert.That(root.Q<Label>("encyclopediaEntryTitle").text, Is.EqualTo("Bread"));
            Click("related_bake_bread");
            Assert.That(root.Q<Label>("encyclopediaEntryTitle").text, Is.EqualTo("Bake bread"));
            Cancel();
            Assert.That(root.Q<Label>("encyclopediaEntryTitle").text, Is.EqualTo("Bread"));
            root.Q<DropdownField>("encyclopediaCategory").value = "Mechanics";
            root.Q<TextField>("encyclopediaSearch").value = "cost";
            Assert.That(root.Q<ScrollView>("encyclopediaEntries").Query<Button>().ToList().Count, Is.GreaterThan(0));
            Assert.That(root.Q<Button>("reference_bread"), Is.Null);
            Click("encyclopediaCloseButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.MainMenu));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IndustrialPanelConfiguresAnAutomaticSupplyVictoryAndReloadsAppliedDraft()
        {
            controller.BeginRun(RunScenario.CoastalChallenge, RunScenario.IndustrialCharter);
            Click("contextControlsTab");
            Click("logisticsButton");
            Assert.That(root.Q<Toggle>("logisticsEnabled").enabledInHierarchy, Is.False);
            Click("logisticsProcurementButton");
            root.Q<TextField>("logisticsQuantity").value = "6";
            root.Q<TextField>("logisticsThreshold").value = "6";
            Click("applyLogisticsButton");
            Assert.That(controller.Session.Player.IndustrialPolicy.Outbound.MaximumShipmentQuantity, Is.EqualTo(6));
            Assert.That(root.Q<Toggle>("logisticsEnabled").enabledInHierarchy, Is.True);
            Click("logisticsBackButton");
            Click("logisticsButton");
            Assert.That(root.Q<TextField>("logisticsQuantity").value, Is.EqualTo("6"));
            Assert.That(root.Q<Label>("logisticsDraft").text, Does.Contain("applied"));
            root.Q<Toggle>("logisticsEnabled").value = true;
            Click("logisticsBackButton");
            for (var day = 0; day < 60 && !controller.Session.IsChallengeEnded; day++) Click("nextDayButton");
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Succeeded));
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Completion));
            Click("completionResultsButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(root.Q<Label>("resultOutcome").text, Is.EqualTo("VICTORY"));
            Assert.That(root.Q<Label>("resultFinances").text, Does.Contain("Cost of goods sold"));
            Assert.That(controller.Session.AutomatedActions, Is.GreaterThan(0));
            Assert.That(controller.Session.ShipDeliveries["bread"], Is.GreaterThanOrEqualTo(24));
            Assert.That(controller.Session.IsPlaying, Is.False);
            var terminal = controller.Session.Snapshot;
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(terminal));
            Click("retryRunButton");
            Assert.That(controller.Session.CharterId, Is.EqualTo(RunScenario.IndustrialCharter));
            Assert.That(controller.Session.ChallengeId, Is.EqualTo(RunScenario.CoastalChallenge));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(300));
        }

        [UnityTest]
        public IEnumerator DeadlineFailureShowsResultsAndCanStartAnotherCompanyWithoutReloadingScene()
        {
            BeginMerchant();
            for (var day = 0; day < 60; day++) Click("nextDayButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(root.Q<Label>("resultOutcome").text, Is.EqualTo("DEFEAT"));
            Assert.That(root.Q<Label>("resultScore").text, Does.Contain("Victory 0"));
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False);
            Click("resultsNewRunButton");
            root.Q<DropdownField>("charterPicker").value = RunScenario.GetCharter(RunScenario.IndependentCharter).Name;
            root.Q<DropdownField>("challengePicker").value = "Unlimited sandbox";
            Click("startRunButton");
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(480));
            Assert.That(controller.Session.Player.CargoUnits, Is.EqualTo(6));
            Assert.That(controller.Session.Snapshot.Challenge, Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AbandonmentConfirmationProducesHonestResultsAndMenuDiscardsSession()
        {
            BeginMerchant();
            Click("nextDayButton");
            Click("menuButton");
            Click("abandonRunButton");
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Active));
            Click("confirmAcceptButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Abandoned));
            Assert.That(root.Q<Label>("resultOutcome").text, Is.EqualTo("RUN ABANDONED"));
            Click("resultsMenuButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.MainMenu));
            Assert.That(controller.HasActiveRun, Is.False);
            Assert.That(root.Q<Button>("continueButton"), Is.Null);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StarterCargoSalesShowAttributedMaterialMarginInsteadOfClosedCashProfit()
        {
            controller.BeginRun(null, RunScenario.IndependentCharter);
            Click("openTradeButton");
            Click("tradeGood_grain");
            root.Q<TextField>("quantityField").value = "4";
            Click("sellButton");
            Click("tradeGood_logs");
            root.Q<TextField>("quantityField").value = "2";
            Click("sellButton");
            Click("closeTradeButton");
            var finance = controller.Session.Snapshot.Accounting.Companies[controller.Session.Player.OwnerId];
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            Assert.That(finance.SalesRevenue, Is.GreaterThan(0));
            Assert.That(finance.CostOfGoodsSold, Is.Zero, "Granted starter cargo has no paid material basis.");
            Assert.That(finance.RealizedGrossMargin, Is.EqualTo(finance.SalesRevenue));
            var text = root.Q<Label>("profitLabel").text;
            Assert.That(text, Does.Contain("Realized material gross margin +" + finance.RealizedGrossMargin));
            Assert.That(text, Does.Contain("held material basis 0"));
            Assert.That(text, Does.Contain("Grants have zero paid basis"));
            Assert.That(text, Does.Not.Contain("closed cash profit"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsAndLedgerAreFunctionalAndPassive()
        {
            root.Q<DropdownField>("textScaleDropdown").value = "Large";
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            BeginMerchant();
            Click("ledgerButton");
            Assert.That(root.Q<Label>("ledgerFinances").text, Does.Contain("600"));
            Assert.That(root.Q<Label>("ledgerAssets").text, Does.Contain("HELD GOODS"));
            Click("ledgerBackButton");
            Click("menuButton");
            Click("pauseSettingsButton");
            Assert.That(root.Q<DropdownField>("textScaleDropdown").value, Is.EqualTo("Default"));
            var before = controller.Session.Snapshot;
            root.Q<Toggle>("smoothMovementToggle").value = false;
            root.Q<DropdownField>("textScaleDropdown").value = "Large";
            Assert.That(controller.SmoothMovement, Is.False);
            Assert.That(controller.TextScale.Current, Is.EqualTo(ArchipelagoUiScale.Large));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Pause));
            Click("pauseExitButton");
            Assert.That(controller.ConfirmationIsOpen, Is.True);
            Click("confirmCancelButton");
            Assert.That(controller.ConfirmationIsOpen, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MainSetupAndReferenceKeepPrimaryControlsInsideBothSupportedViewports()
        {
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0; root.style.width = size.x; root.style.height = size.y;
                yield return null; yield return null;
                Contained("newRunButton"); Contained("menuExitButton");
                Click("newRunButton");
                yield return null;
                Contained("setupModeHeadline"); Contained("startRunButton");
                foreach (var name in new[] { "challengePicker", "charterPicker", "setupAssets" })
                {
                    root.Q<ScrollView>("setupScroll").ScrollTo(root.Q(name));
                    yield return null;
                    Contained(name);
                }
                Click("setupHelpButton");
                yield return null;
                Contained("encyclopediaSearch"); Contained("encyclopediaEntries"); Contained("encyclopediaDetail"); Contained("encyclopediaCloseButton");
                Click("encyclopediaCloseButton");
                Click("setupBackButton");
            }
        }

        private void BeginMerchant() => controller.BeginRun(RunScenario.CoastalChallenge, RunScenario.MerchantCharter);
        private void Contained(string name)
        {
            var bounds = root.Q(name).worldBound;
            Assert.That(bounds.width, Is.GreaterThan(0), name);
            Assert.That(bounds.height, Is.GreaterThan(0), name);
            Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(root.worldBound.xMin - 1), name);
            Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(root.worldBound.yMin - 1), name);
            Assert.That(bounds.xMax, Is.LessThanOrEqualTo(root.worldBound.xMax + 1), name);
            Assert.That(bounds.yMax, Is.LessThanOrEqualTo(root.worldBound.yMax + 1), name);
        }
        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled())
            {
                var target = root.focusController.focusedElement as VisualElement ?? root;
                evt.target = target;
                target.SendEvent(evt);
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
                evt.target = button;
                button.SendEvent(evt);
            }
        }
    }
}
