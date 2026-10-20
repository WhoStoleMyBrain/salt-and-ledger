using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class SeededRunExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadTitle()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
        }

        [UnityTest]
        public IEnumerator SetupValidatesAndPreviewsTheExactWorldWithoutChangingTheCurrentEconomy()
        {
            var before = controller.Session.Snapshot;
            Click("newRunButton");
            var mode = root.Q<DropdownField>("worldModePicker"); mode.value = mode.choices[1];
            root.Q<TextField>("seedField").value = "2147483648";
            Assert.That(root.Q<Button>("startRunButton").enabledSelf, Is.False);
            Assert.That(controller.SetupIdentity, Is.Null);
            Assert.That(root.Q<Label>("seedValidation").text, Is.Not.Empty);
            root.Q<TextField>("seedField").value = "-42";
            var difficulty = root.Q<DropdownField>("difficultyPicker"); difficulty.value = difficulty.choices[1];
            root.Q<DropdownField>("charterPicker").value = RunScenario.GetCharter(RunScenario.IndustrialCharter).Name;
            var expected = controller.SetupIdentity;
            var geography = RunScenario.PreviewGeography(expected);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(root.Q<Label>("setupDifficulty").text, Does.Contain("already operational"));
            Click("copySeedButton");
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo("-42"));
            Click("startRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(expected));
            foreach (var position in geography.Positions.Values)
            {
                var port = controller.Session.Snapshot.Ports[position.PortId];
                Assert.That(port.X, Is.EqualTo(position.X)); Assert.That(port.Y, Is.EqualTo(position.Y));
            }
            Assert.That(controller.Map.Snapshot, Is.SameAs(controller.Session.Snapshot));
            var routeText = controller.Map.Query<Label>(className: "route-distance").ToList().Select(l => l.text).ToArray();
            foreach (var route in geography.Routes)
                Assert.That(routeText, Does.Contain(route.Distance + " units · " + route.DurationDays(controller.Session.Player.Configuration.Speed) + "d"));
            Assert.That(root.Q<Label>("runIdentityLabel").text, Does.Contain("Seed -42").And.Contain("Ascension 1"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetryAndRerollHaveDistinctExplicitLifecycles()
        {
            var identity = new RunIdentity(RunScenario.CoastalChallenge, RunScenario.IndustrialCharter,
                RunWorldMode.Seeded, 99, RunDifficulty.SlowFoundations);
            controller.BeginRun(identity);
            var initial = Fingerprint(controller.Session.Snapshot);
            Click("nextDayButton");
            Click("menuButton"); Click("restartRunButton");
            Assert.That(controller.ConfirmationIsOpen, Is.True);
            Click("confirmAcceptButton");
            Assert.That(Fingerprint(controller.Session.Snapshot), Is.EqualTo(initial));
            Click("menuButton"); Click("rerollWorldButton"); Click("confirmAcceptButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Setup));
            Assert.That(controller.HasActiveRun, Is.False);
            Assert.That(controller.Session.Identity, Is.EqualTo(identity), "A preview is not a new run.");
            var next = controller.SetupIdentity;
            Assert.That(next.Seed, Is.Not.EqualTo(identity.Seed));
            Assert.That(next.CharterId, Is.EqualTo(identity.CharterId));
            Assert.That(next.ChallengeId, Is.EqualTo(identity.ChallengeId));
            Assert.That(next.Difficulty, Is.EqualTo(identity.Difficulty));
            Click("setupHelpButton"); Click("encyclopediaCloseButton");
            Assert.That(controller.SetupIdentity, Is.EqualTo(next), "Reference navigation cannot reroll.");
            Click("startRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(next));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SeededChallengeReachesRealVictoryWithIdentityAndTerminalFreeze()
        {
            var identity = new RunIdentity(RunScenario.CoastalChallenge, RunScenario.IndustrialCharter,
                RunWorldMode.Seeded, -42, RunDifficulty.SlowFoundations);
            controller.BeginRun(identity);
            controller.Session.ConfigureFacility(RunScenario.StarterBakery, EnterpriseScenario.BakeryPolicy(), true);
            controller.Session.ApplyIndustrialPolicy(RunScenario.BakeryShippingPolicy(), true);
            for (var day = 0; day < 60 && !controller.Session.IsChallengeEnded; day++) Click("nextDayButton");
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Succeeded));
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(root.Q<Label>("resultIdentity").text, Does.Contain("Seed -42").And.Contain("Ascension 1"));
            var terminal = controller.Session.Snapshot;
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False);
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(terminal));
            Click("retryRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(identity));
            Assert.That(controller.Session.Journal, Is.Empty);
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AbandonedAscensionRunRetainsPendingConstructionAndPaidCapitalInResults()
        {
            var identity = new RunIdentity(RunScenario.CoastalChallenge, RunScenario.MerchantCharter,
                RunWorldMode.Seeded, 42, RunDifficulty.SlowFoundations);
            controller.BeginRun(identity);
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Bakery";
            Click("constructButton");
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            Assert.That(facility.PortId, Is.EqualTo(EnterpriseScenario.North));
            Assert.That(facility.IsOperational, Is.False);
            Assert.That(facility.OrderedDay, Is.Zero);
            Assert.That(facility.ConstructionCompletionDay, Is.EqualTo(2));
            Assert.That(facility.FirstProductionDay, Is.EqualTo(4));
            Assert.That(controller.Session.Snapshot.Accounting.Companies[facility.OwnerId].ConstructionExpenditure,
                Is.EqualTo(300));
            Click("closeIndustryButton"); Click("menuButton"); Click("abandonRunButton");
            var pending = controller.Session.Snapshot;
            Assert.That(controller.ConfirmationIsOpen, Is.True);
            Assert.That(pending.Challenge.Status, Is.EqualTo(ChallengeStatus.Active));
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(pending), "Confirmation is passive.");
            Click("confirmAcceptButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Abandoned));
            Assert.That(root.Q<Label>("resultAssets").text, Does.Contain("Bakery").And.Contain("ordered day 0")
                .And.Contain("under construction, completes day 2").And.Contain("first eligible day 4"));
            Assert.That(root.Q<Label>("resultFinances").text, Does.Contain("Construction expenditure  300"));
            var terminal = controller.Session.Snapshot;
            Assert.That(terminal.Day, Is.Zero);
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False);
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.AdvancePlayback(60), Is.Zero);
            Assert.Throws<System.InvalidOperationException>(() => controller.Session.NextDay());
            Assert.Throws<System.InvalidOperationException>(() => controller.Session.Play());
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(terminal));
            Assert.That(terminal.PrivateFacilities[facility.Id].ConstructionCompletedDay, Is.Null);
            Assert.That(terminal.PrivateFacilities[facility.Id].BatchesProduced, Is.Zero);
        }

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        // Project stock-sensitive quotes explicitly: their legacy Bid/Ask accessors require fixed pricing.
        private static string Fingerprint(RegionalWorldSnapshot snapshot) => JsonConvert.SerializeObject(new
        {
            snapshot.Day, snapshot.Revision, snapshot.Difficulty, snapshot.RunIdentity,
            snapshot.Companies, snapshot.Accounting, snapshot.Challenge, snapshot.Routes,
            snapshot.IndustryBlueprints, snapshot.PrivateFacilities,
            Ports = snapshot.Ports.Values.Select(port => new
            {
                port.Id, port.Name, port.Role, port.X, port.Y, port.Inventory, port.MarketCash,
                Buildings = port.Market.Buildings, port.Facilities, port.Consumption,
                Initial = new
                {
                    port.Configuration.Market.InitialMarketCash,
                    port.Configuration.Market.InitialQuantities,
                    port.Configuration.Market.Buildings,
                    port.Configuration.Facilities,
                    port.Configuration.Consumption
                },
                Prices = port.Quotes.Keys.Select(good => snapshot.GetMarketQuote(port.Id, good)),
                Pricing = port.Quotes.Select(quote => new
                {
                    quote.Key, quote.Value.IsFixed, quote.Value.Pricing,
                    FixedBid = quote.Value.IsFixed ? (long?)quote.Value.Bid : null,
                    FixedAsk = quote.Value.IsFixed ? (long?)quote.Value.Ask : null
                })
            }),
            Ships = snapshot.Ships.Values.Select(ship => new
            {
                ship.Configuration, ship.Ship, ship.OwnerId, ship.Name, ship.IsPlayer, ship.Cash,
                ship.Policy, ship.IndustrialPolicy, ship.IndustrialStage, ship.AutomationEnabled,
                ship.VoyageDurationDays
            })
        });
    }
}
