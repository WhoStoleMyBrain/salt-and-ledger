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
    public sealed class ItineraryExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>(); root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
        }

        [UnityTest]
        public IEnumerator FreshSetupAndFleetOpenThePassiveBoundedPlanner()
        {
            Click("sandboxMenuButton");
            Assert.That(controller.SetupIdentity.ItineraryVersion, Is.EqualTo(1));
            Click("startRunButton");
            var before = controller.Session.Snapshot;
            Click("royalServicesButton"); Click("routeOwned_" + controller.Session.Player.Id);
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Click("itineraryAddStop"); Click("itineraryAddStop");
            Assert.That(root.Q<Button>("itineraryAddStop").enabledSelf, Is.False);
            Click("itineraryStopUp3"); Click("itineraryStopDown0"); Click("itineraryRemoveStop3");
            root.Q<Foldout>("itineraryAction0_0").value = true;
            root.Q<TextField>("itineraryMax0_0").value = "3";
            Click("itineraryAddAction0");
            root.Q<Foldout>("itineraryAction0_1").value = true;
            Assert.That(root.Q<Foldout>("itineraryAction0_0").value, Is.False, "One expanded action keeps the planner compact.");
            Choose("itineraryKind0_1", 1);
            Click("itineraryActionUp0_1");
            Assert.That(root.Q<DropdownField>("itineraryKind0_0").index, Is.EqualTo(1));
            root.Q<Foldout>("itineraryAction0_0").value = true;
            Click("itineraryRemoveAction0_0");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(root.Q<Label>("itineraryDraftStatus").text, Does.Contain("UNAPPLIED DRAFT"));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Development));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }


        [UnityTest]
        public IEnumerator TwoPaidActionsThenWaitPreserveTheCursorAndManualControl()
        {
            BeginIndependent(); OpenPlanner();
            root.Q<Foldout>("itineraryAction0_0").value = true;
            root.Q<TextField>("itineraryMax0_0").value = "1";
            Click("itineraryAddAction0"); root.Q<Foldout>("itineraryAction0_1").value = true;
            root.Q<DropdownField>("itineraryGood0_1").value = controller.Session.Content.GetGood("logs").Name;
            root.Q<TextField>("itineraryMax0_1").value = "1";
            Click("itineraryAddAction0"); root.Q<Foldout>("itineraryAction0_2").value = true;
            Choose("itineraryKind0_2", 1);
            root.Q<DropdownField>("itineraryGood0_2").value = controller.Session.Content.GetGood("silver").Name;
            Choose("itineraryShortage0_2", 1);
            var before = controller.Session.Snapshot;
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.Itinerary.Id, Is.EqualTo("route_" + controller.Session.Player.Id));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(controller.Session.Player.AutomationEnabled, Is.False);
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Click("itineraryRunButton");
            var waited = controller.Session.Snapshot;
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(2));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(before.Ships[controller.Session.Player.Id].Cargo["grain"] + 10000));
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(before.Ships[controller.Session.Player.Id].Cargo["logs"] + 10000));
            Assert.That(root.Q<Label>("itineraryLiveStatus").text, Does.Contain("Waiting"));
            Click("itineraryRunButton");
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(waited.Companies[controller.Session.Player.OwnerId].Cash));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(waited.Ships[controller.Session.Player.Id].Cargo["grain"]));
            var progress = controller.Session.Player.ItineraryProgress;
            root.Q<Toggle>("itineraryManualToggle").value = true;
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(progress.ActionIndex));
            Assert.That(controller.Session.Player.ItineraryProgress.Cycle, Is.EqualTo(progress.Cycle));
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True, "Equivalent reapplication preserves the partial visit.");
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(progress.ActionIndex));
            root.Q<TextField>("itineraryMax0_2").value = "3";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("partial visit"));
            Assert.That(controller.Session.Player.Itinerary.Stops[0].Commands[2].MaximumQuantity, Is.EqualTo(20000));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletedStopCannotReplaceOrdersToResetTodaysBudget()
        {
            BeginIndependent(); OpenPlanner();
            for (var i = 0; i < 8; i++)
            {
                if (i > 0) Click("itineraryAddAction0");
                var key = "0_" + i;
                root.Q<Foldout>("itineraryAction" + key).value = true;
                Choose("itineraryKind" + key, 1);
                root.Q<DropdownField>("itineraryGood" + key).value = controller.Session.Content.GetGood("silver").Name;
            }
            Click("itineraryApplyButton");
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Click("itineraryRunButton");
            var progress = controller.Session.Player.ItineraryProgress;
            Assert.That(progress.ActionIndex, Is.EqualTo(8)); Assert.That(progress.OperationsUsed, Is.EqualTo(8));
            root.Q<Toggle>("itineraryManualToggle").value = true;
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True);
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(8));
            root.Q<Foldout>("itineraryAction0_0").value = true;
            root.Q<TextField>("itineraryMax0_0").value = "3";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("Advance a day").And.Contain("operation budget"));
            Click("itineraryNextDayButton");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True);
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraftValidationAndRemoteQuotesNeverExecuteAndHarborLinksReturn()
        {
            BeginIndependent();
            controller.OpenTrade(controller.Session.Player.DockedPortId);
            var before = controller.Session.Snapshot;
            Click("harborRoutePlannerButton");
            root.Q<Foldout>("itineraryAction0_0").value = true;
            root.Q<TextField>("itineraryMax0_0").value = "1.5";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("whole number"));
            root.Q<TextField>("itineraryMax0_0").value = "2";
            root.Q<TextField>("itineraryCashReserve0_0").value = "10.00001";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            root.Q<TextField>("itineraryCashReserve0_0").value = "10.0001";
            root.Q<Foldout>("itineraryAction1_0").value = true;
            Assert.That(root.Q<Label>("itineraryQuote1_0").text, Does.Contain("REMOTE / HYPOTHETICAL").And.Contain("Market budget"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectedRouteEnableRestoresManualToggleAndExplainsTheLocation()
        {
            BeginIndependent(); OpenPlanner(); Click("itineraryApplyButton");
            var policy = controller.Session.Player.Itinerary;
            var outside = controller.Session.Snapshot.Ports.Keys.First(port => policy.Stops.All(stop => stop.PortId != port));
            Click("itineraryBackButton"); Click("developmentBackButton");
            controller.OpenTrade(outside); Click("tradeSailButton");
            Assert.That(controller.Session.Player.DestinationPortId, Is.EqualTo(outside));
            OpenPlanner();
            var before = controller.Session.Snapshot;
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.Player.AutomationEnabled, Is.False);
            Assert.That(root.Q<Toggle>("itineraryManualToggle").value, Is.True);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("Return to the itinerary's next stop"));
            Assert.That(controller.Session.Player.Itinerary, Is.SameAs(policy));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExampleIsOnlyADraftAndExplainsMissingCompanyStock()
        {
            BeginIndependent(); OpenPlanner();
            var before = controller.Session.Snapshot;
            Click("itineraryExampleButton");
            Assert.That(controller.Session.Player.Itinerary, Is.Null);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(root.Q<Label>("itineraryDraftStatus").text, Does.Contain("3 stops"));
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("owned industry"));
            root.Q<Foldout>("itineraryAction0_0").value = true;
            yield return null;
            var button = root.Q<Button>("itineraryBackButton");
            Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(20));
            Assert.That(root.worldBound.Contains(button.worldBound.center), Is.True);
        }

        private void BeginIndependent() => controller.BeginRun(new RunIdentity(null,
            RunScenario.IndependentCharter, RunWorldMode.ExpandingEconomy, 42,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1));

        private void OpenPlanner()
        {
            Click("royalServicesButton"); Click("routeOwned_" + controller.Session.Player.Id);
        }

        private void Choose(string id, int index)
        {
            var field = root.Q<DropdownField>(id); field.value = field.choices[index];
        }

        private void Click(string id)
        {
            var button = root.Q<Button>(id); Assert.That(button, Is.Not.Null, id);
            Assert.That(button.enabledInHierarchy, Is.True, id);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled()) { evt.target = root; root.SendEvent(evt); }
        }
    }
}
