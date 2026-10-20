using System.Collections;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class TradingLabTests
    {
        private TradingLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("TradingLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<TradingLabController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Session, Is.Not.Null);
            root = controller.Root;
            Assert.That(root.panel, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator InspectionAndInvalidInputDoNotMutateTheWorld()
        {
            var before = controller.Session.Snapshot;
            Assert.That(root.Q<Label>("cashLabel").text, Does.Contain("100"));
            Assert.That(root.Q<Label>("cargoLabel").text, Does.Contain("6"));
            Click("southhavenButton");
            Assert.That(root.Q<Label>("marketTitle").text, Does.Contain("Southhaven"));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            Click("northportButton");
            root.Q<TextField>("quantityField").value = "6";
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("54"));
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("10"));
            foreach (var input in new[] { "0", "-1", "abc", "2147483648", "" })
            {
                root.Q<TextField>("quantityField").value = input;
                Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False, input);
                Assert.That(root.Q<Button>("buyDepartButton").enabledInHierarchy, Is.False, input);
            }
            root.Q<TextField>("maxBuyField").value = "0";
            Click("applyPolicyButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.Policy.Outbound.MaxBuyPrice, Is.EqualTo(10));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ManualControlsCompleteTheSixTickExpedition()
        {
            root.Q<TextField>("quantityField").value = "6";
            Click("buyDepartButton");
            Assert.That(controller.Session.Snapshot.Tick, Is.EqualTo(1));
            Assert.That(controller.Session.Snapshot.TraderCash, Is.EqualTo(46));
            Assert.That(controller.Session.Snapshot.Ship.RemainingTravelTicks, Is.EqualTo(1));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("shipStatusLabel").text, Does.Contain("Southhaven"));
            Click("nextTickButton");
            Assert.That(controller.Session.Snapshot.Ship.DockedPortId, Is.EqualTo("southhaven"));
            Click("southhavenButton");
            Assert.That(root.Q<Label>("sellPreviewLabel").text, Does.Contain("144"));
            Click("sellButton");
            var goods = root.Q<DropdownField>("goodDropdown");
            goods.index = 1;
            root.Q<TextField>("quantityField").value = "2";
            Click("buyDepartButton");
            Click("nextTickButton");
            Click("northportButton");
            Click("sellButton");
            Assert.That(controller.Session.Snapshot.Tick, Is.EqualTo(6));
            Assert.That(controller.Session.Snapshot.TraderCash, Is.EqualTo(219));
            Assert.That(controller.Session.Snapshot.Ship.CargoUnits, Is.Zero);
            Assert.That(root.Q<Label>("profitLabel").text, Does.Contain("119"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutomationControlsPauseApplyRecoverAndReset()
        {
            var before = controller.Session.Snapshot;
            root.Q<TextField>("maxBuyField").value = "7";
            Click("applyPolicyButton");
            root.Q<Toggle>("automationToggle").value = true;
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.BuyPriceLimit));
            Assert.That(root.Q<Label>("decisionLabel").text, Does.Contain("buying limit"));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            root.Q<TextField>("maxBuyField").value = "10";
            Click("applyPolicyButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("automationTickButton");
            Assert.That(controller.Session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(6));
            var sailing = controller.Session.Snapshot;
            root.Q<Toggle>("automationToggle").value = false;
            Assert.That(controller.Session.Snapshot, Is.SameAs(sailing));
            root.Q<Toggle>("automationToggle").value = true;
            for (var i = 0; i < 5; i++) Click("automationTickButton");
            Assert.That(controller.Session.Snapshot.TraderCash, Is.EqualTo(219));
            Click("resetButton");
            Assert.That(controller.Session.Snapshot.Tick, Is.Zero);
            Assert.That(controller.Session.Snapshot.TraderCash, Is.EqualTo(100));
            Assert.That(controller.Session.AutomationEnabled, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PresetsAndBlockedScenarioUseTheActualControls()
        {
            root.Q<DropdownField>("scenarioDropdown").index = 1;
            Click("resetButton");
            root.Q<Toggle>("automationToggle").value = true;
            Click("automationRunButton");
            Assert.That(controller.Session.Snapshot.Tick, Is.EqualTo(6));
            Assert.That(controller.Session.Snapshot.TraderCash, Is.EqualTo(169));
            Assert.That(root.Q<Label>("profitLabel").text, Does.Contain("69"));
            root.Q<DropdownField>("scenarioDropdown").index = 2;
            Click("resetButton");
            root.Q<Toggle>("automationToggle").value = true;
            Click("automationRunButton");
            Assert.That(controller.Session.Snapshot.Tick, Is.EqualTo(5));
            Assert.That(controller.Session.Snapshot.Ship.Cargo["logs"], Is.EqualTo(6));
            Assert.That(controller.Session.Snapshot.TraderCash, Is.EqualTo(46));
            Assert.That(controller.Session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.InsufficientMarketFunds));
            Assert.That(root.Q<Label>("decisionLabel").text, Does.Contain("cannot pay"));
            Assert.That(controller.Session.RealizedGrossProfit, Is.Null);
            var blocked = controller.Session.Snapshot;
            root.Q<TextField>("minSellField").value = "1";
            Click("applyPolicyButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(blocked));
            Assert.That(controller.Session.CurrentDecision.Reason, Is.EqualTo(AutomationReason.InsufficientMarketFunds));
            root.Q<Toggle>("automationToggle").value = false;
            Click("southhavenButton");
            Assert.That(root.Q<Button>("sailButton").enabledInHierarchy, Is.True);
            yield return null;
        }

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
