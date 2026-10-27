using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class GrandCommissionExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            saveDirectory = Path.Combine(Path.GetTempPath(), "SaltAndLedger-Commission-UI-" + Guid.NewGuid().ToString("N"));
            typeof(ArchipelagoLabController).GetField("saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new LocalRunSaveStore(saveDirectory));
        }

        [TearDown]
        public void RemoveTestSlots()
        {
            if (saveDirectory != null && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator MapFirstFourRoundsBacktrackingAndReviewArePassiveUntilExactBegin()
        {
            var original = controller.Session.Snapshot;
            OpenMap(42);
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CommissionMap));
            Assert.That(root.Q<ArchipelagoMap>("commissionPreviewMap").PortMarkerCount, Is.EqualTo(4));
            Assert.That(controller.CommissionPreview.Day, Is.Zero);
            Assert.That(controller.SetupIdentity.Commission, Is.Null);
            Assert.That(root.Q<Button>("commissionBeginButton").enabledSelf, Is.False);
            var geography = RunScenario.PreviewGeography(controller.SetupIdentity);
            foreach (var position in geography.Positions.Values)
            {
                Assert.That(controller.CommissionPreview.Ports[position.PortId].X, Is.EqualTo(position.X));
                Assert.That(controller.CommissionPreview.Ports[position.PortId].Y, Is.EqualTo(position.Y));
                ClickIn(root.Q<ArchipelagoMap>("commissionPreviewMap"), "port_" + position.PortId);
                Assert.That(root.Q<Label>("commissionMapDetails").text, Does.Contain("YOU CAN BUILD"));
            }
            Click("commissionMapNextButton");
            var draft = GrandCommissionDraft.Generate(42);
            var chosen = new string[4];
            for (var round = 0; round < 4; round++)
            {
                Assert.That(root.Q<VisualElement>("commissionOfferCards").childCount, Is.EqualTo(3));
                Assert.That(root.Q<Button>("commissionNextButton").enabledSelf, Is.False);
                chosen[round] = draft.Rounds[round].Offers[0].TemplateId;
                Click("chooseCommission_" + chosen[round]);
                Assert.That(root.Q<VisualElement>("commissionOffer_" + chosen[round]).ClassListContains("commission-selected"), Is.True);
                Click("commissionNextButton");
            }
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CommissionReview));
            var identity = controller.SetupIdentity;
            Assert.That(identity.Commission.Signature, Is.EqualTo(draft.Resolve(chosen).Signature));
            Assert.That(root.Q<Label>("commissionReviewDeadline").text, Does.Contain("100").And.Contain("180"));
            Click("commissionEdit0");
            Assert.That(root.Q<VisualElement>("commissionOffer_" + chosen[0]).ClassListContains("commission-selected"), Is.True);
            foreach (var offer in draft.Rounds[0].Offers)
                Assert.That(root.Q<Button>("chooseCommission_" + offer.TemplateId), Is.Not.Null);
            chosen[0] = draft.Rounds[0].Offers[1].TemplateId;
            Click("chooseCommission_" + chosen[0]);
            for (var round = 0; round < 4; round++) Click("commissionNextButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(original));
            var preview = controller.CommissionPreview;
            Click("commissionBeginButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.Identity.Commission.Signature, Is.EqualTo(draft.Resolve(chosen).Signature));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            foreach (var port in preview.Ports.Values)
            {
                var actual = controller.Session.Snapshot.Ports[port.Id];
                Assert.That(actual.Inventory, Is.EqualTo(port.Inventory));
                Assert.That(actual.MarketCash, Is.EqualTo(port.MarketCash));
            }
            Assert.That(root.Q<Label>("challengeLabel").text.Split('\n').Length, Is.EqualTo(1));
            Assert.That(root.Q<Label>("challengeLabel").text, Does.Contain("Objectives").And.Contain("180 days left").And.Contain("100"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator JournalPausesReturnsToItsParentAndRetainsExactSavedVoyageAndRetryPlan()
        {
            DraftAndBegin(42);
            var identity = controller.Session.Identity;
            Click("playButton");
            Assert.That(controller.Session.IsPlaying, Is.True);
            var before = controller.Session.Snapshot;
            Click("objectivesButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Objectives));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(root.Q<VisualElement>("objectiveCards").childCount, Is.EqualTo(4));
            Assert.That(root.Q<Label>("objectivesDeadline").text, Does.Contain("Day 180").And.Contain("100"));
            foreach (var bar in root.Q<VisualElement>("objectiveCards").Query<ProgressBar>().ToList())
                Assert.That(bar.value, Is.InRange(0, 100));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.IsPlaying, Is.False, "Closing a management view must not silently restart playback.");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("playButton");
            Assert.That(controller.Session.IsPlaying, Is.True, "Playback resumes only on the explicit Play command.");
            Click("pauseButton");
            Click("menuButton"); Click("pauseObjectivesButton"); Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Pause));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Click("resumeButton");
            var destination = controller.Session.Snapshot.Ports.Keys.First(id => id != controller.Session.Player.DockedPortId);
            Click("playerShipButton"); Click("port_" + destination); Click("sailButton"); Click("nextDayButton");
            var saved = controller.Session.SaveText();
            Click("menuButton"); Click("pauseSavesButton"); Click("saveSlot1"); Click("savesBackButton"); Click("resumeButton");
            Click("nextDayButton"); Click("menuButton"); Click("pauseSavesButton"); Click("loadSlot1"); Click("confirmAcceptButton");
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            Click("objectivesButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(identity));
            Assert.That(root.Q<Label>("objectivesDeadline").text, Does.Contain("179 days remaining"));
            Click("objectivesBackButton"); Click("menuButton"); Click("restartRunButton"); Click("confirmAcceptButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(identity));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Snapshot.CommissionHistory.Ships[0].Visits.Count, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TerminalJournalShowsFourFrozenGoalsAndSeedEditsInvalidateTheUnfinishedDraft()
        {
            DraftAndBegin(-42);
            Click("menuButton"); Click("abandonRunButton"); Click("confirmAcceptButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(root.Q<VisualElement>("resultCommissionGoals").childCount, Is.EqualTo(4));
            Assert.That(root.Q<Label>("resultIdentity").text, Does.Contain("Mission draft v1"));
            Click("resultsObjectivesButton");
            var terminal = controller.Session.Snapshot;
            Assert.That(root.Q<Label>("objectivesOverview").text, Does.Contain("Abandoned"));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(controller.Session.Snapshot, Is.SameAs(terminal));
            Click("resultsNewRunButton");
            root.Q<TextField>("seedField").value = "99";
            Assert.That(controller.SetupIdentity.Commission, Is.Null);
            Assert.That(root.Q<Button>("commissionBeginButton").enabledSelf, Is.False);
            Click("startRunButton"); Click("commissionMapNextButton");
            Assert.That(root.Q<Button>("commissionNextButton").enabledSelf, Is.False);
            foreach (var offer in GrandCommissionDraft.Generate(99).Rounds[0].Offers)
                Assert.That(root.Q<Button>("chooseCommission_" + offer.TemplateId), Is.Not.Null);
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CommissionMap));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Setup));
            root.Q<TextField>("seedField").value = "invalid";
            Assert.That(root.Q<Button>("startRunButton").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("commissionBeginButton").enabledSelf, Is.False);
            yield return null;
        }

        private void OpenMap(int seed)
        {
            Click("newRunButton");
            Assert.That(root.Q<DropdownField>("challengePicker").value, Is.EqualTo(RunScenario.GetChallenge(GrandCommission.ChallengeId).Name));
            root.Q<TextField>("seedField").value = seed.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Click("startRunButton");
        }

        private void DraftAndBegin(int seed)
        {
            OpenMap(seed); Click("commissionMapNextButton");
            foreach (var round in GrandCommissionDraft.Generate(seed).Rounds)
            {
                Click("chooseCommission_" + round.Offers[0].TemplateId);
                Click("commissionNextButton");
            }
            Click("commissionBeginButton");
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled()) { evt.target = root; root.SendEvent(evt); }
        }
        private void Click(string name) => ClickIn(root, name);
        private static void ClickIn(VisualElement scope, string name)
        {
            var button = scope.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
