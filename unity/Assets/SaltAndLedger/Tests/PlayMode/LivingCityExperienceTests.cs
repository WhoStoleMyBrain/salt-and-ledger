using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class LivingCityExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadTitle()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            Assert.That(controller, Is.Not.Null);
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
        }

        [UnityTest]
        public IEnumerator SetupShowsCanonicalRolesAndStartsTheExactLivingIdentity()
        {
            var before = controller.Session.Snapshot;
            Click("newRunButton");
            var mode = root.Q<DropdownField>("worldModePicker");
            Assert.That(mode.choices.Count, Is.EqualTo(4));
            mode.value = mode.choices[2];
            root.Q<TextField>("seedField").value = "-42";
            var pricing = root.Q<DropdownField>("livingPricingPicker"); pricing.value = pricing.choices[1];
            var ascension = root.Q<DropdownField>("difficultyPicker"); ascension.value = ascension.choices[1];
            root.Q<DropdownField>("charterPicker").value = RunScenario.GetCharter(RunScenario.IndustrialCharter).Name;
            var expected = controller.SetupIdentity;
            Assert.That(expected.WorldMode, Is.EqualTo(RunWorldMode.LivingCities));
            Assert.That(expected.GeneratorVersion, Is.EqualTo(2));
            Assert.That(expected.LivingPricingMode, Is.EqualTo(LivingPricingMode.Coverage));
            Assert.That(expected.ChallengeId, Is.EqualTo(RunScenario.RecoveryChallenge));
            Assert.That(root.Q<Label>("setupWorldIdentity").text, Does.Contain("Living Cities").And.Contain("Generator v2"));
            var generation = LivingCityGeneration.Generate(-42, 2, LivingPricingMode.Coverage);
            foreach (var port in generation.Ports)
            {
                var preview = root.Q<Label>("setupCapabilities_" + port.Id).text;
                foreach (var facility in port.Facilities)
                    Assert.That(preview, Does.Contain(controller.Session.Content.GetBuilding(facility.Instance.BuildingId).Name));
            }
            Assert.That(root.Q<Label>("setupAssets").text, Does.Contain(LivingCityGeneration.PortName(generation.ProcessingPortId)));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("startRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(expected));
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(generation.ProcessingPortId));
            foreach (var port in generation.Ports)
            {
                var actual = controller.Session.Snapshot.Ports[port.Id];
                Assert.That(actual.City.Population, Is.EqualTo(port.Settlement.InitialPopulation));
                Assert.That(actual.City.HouseholdCash, Is.EqualTo(port.Settlement.InitialHouseholdCash));
                Assert.That(actual.Facilities.Select(f => f.Configuration.Instance.BuildingId), Is.EqualTo(port.Facilities.Select(f => f.Instance.BuildingId)));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityAccountsNeedsAndConstructionEligibilityReflectTheCommittedWorld()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, 42));
            var generation = LivingCityGeneration.Generate(42);
            Click("port_" + generation.FurniturePortId);
            var before = controller.Session.Snapshot;
            Click("cityEconomyButton");
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(generation.FurniturePortId));
            Click("economyDetailsButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CityEconomy));
            Assert.That(controller.Session.IsPlaying, Is.False);
            var port = before.Ports[generation.FurniturePortId];
            Assert.That(root.Q<Label>("cityMarketAccount").text, Does.Contain(port.MarketCash.ToString()));
            Assert.That(root.Q<Label>("cityHouseholdAccount").text, Does.Contain(port.City.HouseholdCash.ToString()));
            Assert.That(root.Q<Label>("cityProsperityDetail").text, Does.Contain("Purchasing power: awaiting first food observation."));
            Click("cityIndustryTab");
            Assert.That(root.Q<Label>("cityCapabilities").text, Does.Contain("Lumber Camp").And.Contain("Furniture Workshop").And.Contain("NOT ELIGIBLE HERE"));
            Assert.That(root.Q<Label>("cityIndustriesDetail").text, Does.Contain("wage"));
            Click("cityNeedsTab");
            Assert.That(root.Q<Label>("cityNeedsDetail").text, Does.Contain("Essential household purchase").And.Contain("Municipal own-use; no self-payment"));
            Click("cityEconomyHelpButton");
            Assert.That(root.Q<Label>("encyclopediaEntryTitle").text, Is.EqualTo("Living city accounts and wages"));
            Click("related_help_city_needs");
            Assert.That(root.Q<Label>("encyclopediaEntryTitle").text, Is.EqualTo("Food, furniture and municipal use"));
            Cancel();
            Assert.That(root.Q<Label>("encyclopediaEntryTitle").text, Is.EqualTo("Living city accounts and wages"));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CityEconomy));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("modeIndustry");
            Assert.That(root.Q<DropdownField>("blueprintDropdown").choices, Does.Not.Contain("Sawmill"));
            Assert.Throws<InvalidOperationException>(() => controller.Session.Construct(EnterpriseScenario.SawmillBlueprint));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("closeIndustryButton");
            Click("modeMap");
            for (var day = 0; day < 12; day++) Click("nextDayButton");
            Click("cityEconomyButton");
            Click("economyDetailsButton");
            port = controller.Session.Snapshot.Ports[generation.FurniturePortId];
            Assert.That(root.Q<Label>("cityPopulationDetail").text, Does.Contain("Population " + port.City.Population));
            Assert.That(root.Q<Label>("cityProsperityDetail").text, Does.Contain(port.City.ProsperityReason));
            Assert.That(root.Q<Label>("cityProsperityDetail").text, Does.Contain("Purchasing power at last food observation: " + port.City.PurchasingPowerPercent));
            Assert.That(root.Q<Label>("cityCashFlows").text, Does.Contain(port.City.TotalWagesReceived + " total"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoreSummaryAndMarketFitDefaultAndCriticalActionsRemainReachableAtEveryScale()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, -42));
            Click("port_" + controller.Session.Player.DockedPortId);
            var before = controller.Session.Snapshot;
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            foreach (ArchipelagoUiScale scale in Enum.GetValues(typeof(ArchipelagoUiScale)))
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = size.x; root.style.height = size.y;
                controller.TextScale.Apply(scale, false);
                yield return null; yield return null;
                Visible("openTradeButton"); Visible("openIndustryButton"); Visible("cityEconomyButton");
                if (scale == ArchipelagoUiScale.Default)
                {
                    Visible("deckFact0"); Visible("deckFact1"); Visible("deckFact2"); Visible("deckReason");
                    Assert.That(root.Q("deckBody").GetFirstAncestorOfType<ScrollView>(), Is.Null,
                        "The default primary deck must fit without an internal scrollbar at " + size);
                    Assert.That(controller.Map.worldBound.Overlaps(root.Q("commandDeck").worldBound), Is.False,
                        "The command deck must not cover map markers at " + size);
                }
                Click("mapDetailsButton");
                yield return Reachable("contextOverviewTab");
                Click("contextOverviewTab");
                yield return Reachable("cityFundsLabel");
                yield return Reachable("contextMarketTab");
                Click("contextMarketTab");
                yield return Reachable("marketCashLabel");
                Click("inspectorBackButton");
                Click("cityEconomyButton");
                yield return null; yield return null;
                Visible("economyDetailsButton"); Visible("economyBackButton");
                Click("economyDetailsButton");
                yield return null; yield return null;
                Visible("cityOverviewTab"); Visible("cityNeedsTab"); Visible("cityIndustryTab"); Visible("cityEconomyBackButton");
                Click("cityEconomyBackButton");
                Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
                Click("economyBackButton");
                Click("openTradeButton");
                yield return null; yield return null;
                if (scale == ArchipelagoUiScale.Default)
                { Visible("quantityField"); Visible("buyButton"); Visible("sellButton"); }
                yield return Reachable("buyButton"); yield return Reachable("sellButton");
                Visible("closeTradeButton"); Cancel();
                Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            }
        }

        [UnityTest]
        public IEnumerator RealHouseholdPurchaseRestoresMarketLiquidityAndEnablesAnExactPlayerSale()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.LivingCities, 42));
            var recoveryPortId = LivingCityGeneration.Generate(42).RecoveryPortId;
            Click("contextControlsTab");
            root.Q<Foldout>("automationFoldout").value = true;
            root.Q<Toggle>("automationToggle").value = true;
            for (var day = 0; day < 7; day++) Click("nextDayButton");
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(recoveryPortId));
            Assert.That(controller.Session.Player.Cargo["bread"], Is.EqualTo(2));
            root.Q<Toggle>("automationToggle").value = false;
            Click("port_" + recoveryPortId);
            var blocked = controller.Session.Snapshot;
            Assert.That(blocked.Ports[recoveryPortId].MarketCash, Is.EqualTo(2));
            Assert.That(blocked.Ports[recoveryPortId].City.HouseholdCash, Is.EqualTo(464));
            Click("openTradeButton");
            Click("tradeGood_bread");
            root.Q<TextField>("quantityField").value = "1";
            Assert.That(controller.Session.PreviewTrade(TradeDirection.Sell, "bread", 1).Outcome,
                Is.EqualTo(TradeOutcome.InsufficientMarketFunds));
            Assert.That(root.Q<Label>("sellPreviewLabel").text, Does.Contain("Market does not have enough cash"));
            Assert.That(root.Q<Button>("sellButton").enabledInHierarchy, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(blocked));
            Click("closeTradeButton");
            Click("nextDayButton");
            var funded = controller.Session.Snapshot;
            var city = funded.Ports[recoveryPortId];
            Assert.That(funded.Day, Is.EqualTo(8));
            Assert.That(city.MarketCash, Is.EqualTo(41));
            Assert.That(city.City.HouseholdCash, Is.EqualTo(425));
            Assert.That(city.City.LastRetailSpend, Is.EqualTo(39));
            Assert.That(city.Inventory["bread"], Is.EqualTo(7));
            Click("openTradeButton");
            var preview = controller.Session.PreviewTrade(TradeDirection.Sell, "bread", 1);
            Assert.That(preview.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(preview.TotalAmount, Is.EqualTo(37));
            Assert.That(root.Q<Label>("sellPreviewLabel").text, Does.Contain("Total 37"));
            var companyCash = controller.Session.Player.Cash;
            Click("sellButton");
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(8));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(companyCash + preview.TotalAmount));
            Assert.That(controller.Session.Player.Cargo["bread"], Is.EqualTo(1));
            Assert.That(controller.Session.Snapshot.Ports[recoveryPortId].MarketCash, Is.EqualTo(4));
            Assert.That(controller.Session.Snapshot.Ports[recoveryPortId].City.HouseholdCash, Is.EqualTo(425));
            Assert.That(controller.Session.Snapshot.Ports[recoveryPortId].Inventory["bread"], Is.EqualTo(8));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedMerchantActionsReachGenuineRecoveryVictoryAndFreezeTheRun()
        {
            var identity = new RunIdentity(RunScenario.RecoveryChallenge, RunScenario.MerchantCharter,
                RunWorldMode.LivingCities, 42, RunDifficulty.SlowFoundations);
            controller.BeginRun(identity);
            Click("contextControlsTab");
            root.Q<Foldout>("automationFoldout").value = true;
            var before = controller.Session.Snapshot;
            root.Q<Toggle>("automationToggle").value = true;
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Companies[controller.Session.Player.OwnerId].Cash));
            for (var day = 0; day < 90 && !controller.Session.IsChallengeEnded; day++) Click("nextDayButton");
            var final = controller.Session.Snapshot;
            Assert.That(final.Challenge.Status, Is.EqualTo(ChallengeStatus.Succeeded));
            Assert.That(final.Day, Is.EqualTo(28));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(664));
            Assert.That(final.Challenge.Recovery.Qualified, Is.True);
            Assert.That(final.Challenge.Recovery.Observations, Is.EqualTo(6));
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Completion));
            Click("completionResultsButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(root.Q<Label>("resultOutcome").text, Is.EqualTo("VICTORY"));
            Assert.That(root.Q<Label>("resultObjective").text, Does.Contain("Paid food").And.Contain("Population"));
            Assert.That(root.Q<Button>("nextDayButton").enabledInHierarchy, Is.False);
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(final));
            Click("retryRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(identity));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Snapshot.Challenge.Recovery.Observations, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RecoveryDeadlineAndRetryShowRealProgressAndResetTheExactIdentity()
        {
            var identity = new RunIdentity(RunScenario.RecoveryChallenge, RunScenario.MerchantCharter,
                RunWorldMode.LivingCities, 42, RunDifficulty.SlowFoundations);
            controller.BeginRun(identity);
            var initialCash = controller.Session.Player.Cash;
            for (var day = 0; day < 90 && !controller.Session.IsChallengeEnded; day++) Click("nextDayButton");
            Assert.That(controller.Session.Snapshot.Challenge.Status, Is.EqualTo(ChallengeStatus.Failed));
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(root.Q<Label>("resultObjective").text, Does.Contain("Paid food").And.Contain("observations").And.Contain("Population"));
            Assert.That(root.Q<Label>("resultIdentity").text, Does.Contain("Living Cities").And.Contain("Generator v2").And.Contain("Ascension 1"));
            Assert.That(root.Q<Label>("resultFinances").text, Does.Contain("Paid wages (operating expense)"));
            var terminal = controller.Session.Snapshot;
            yield return null;
            Assert.That(controller.Session.Snapshot, Is.SameAs(terminal));
            Click("retryRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(identity));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(initialCash));
            Assert.That(controller.Session.Snapshot.Ports.Values.All(p => p.City.FoodWindow.Count == 0), Is.True);
        }

        private IEnumerator Reachable(string name)
        {
            var item = root.Q(name);
            item.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(item);
            yield return null; yield return null;
            Visible(name);
        }

        private void Visible(string name)
        {
            var item = root.Q(name);
            var viewport = item.GetFirstAncestorOfType<ScrollView>()?.contentViewport.worldBound ?? root.worldBound;
            Assert.That(item.worldBound.width, Is.GreaterThan(0), name);
            Assert.That(item.worldBound.height, Is.GreaterThan(0), name);
            Assert.That(item.worldBound.xMin, Is.GreaterThanOrEqualTo(Math.Max(root.worldBound.xMin, viewport.xMin) - 1), name);
            Assert.That(item.worldBound.yMin, Is.GreaterThanOrEqualTo(Math.Max(root.worldBound.yMin, viewport.yMin) - 1), name);
            Assert.That(item.worldBound.xMax, Is.LessThanOrEqualTo(Math.Min(root.worldBound.xMax, viewport.xMax) + 1), name);
            Assert.That(item.worldBound.yMax, Is.LessThanOrEqualTo(Math.Min(root.worldBound.yMax, viewport.yMax) + 1), name);
        }

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled())
            {
                var target = root.focusController.focusedElement as VisualElement ?? root;
                evt.target = target; target.SendEvent(evt);
            }
        }
    }
}
