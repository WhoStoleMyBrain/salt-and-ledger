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
    public sealed class IndustryUsabilityTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            Assert.That(controller, Is.Not.Null);
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            root = controller.Root;
        }

        [UnityTest]
        public IEnumerator RecommendedDraftIsPassiveUntilExplicitApplyAndNeverPurchasesImmediately()
        {
            controller.BeginRun(null, RunScenario.MerchantCharter);
            OpenBakeryInvestment(); Click("constructButton");
            var before = controller.Session.Snapshot;
            var facility = before.PrivateFacilities.Values.Single();
            Assert.That(facility.Policy.ProcurementEnabled, Is.False);
            Click("recommendIndustryPresetButton");
            Assert.That(root.Q<Label>("industryPolicyDraftLabel").text, Does.Contain("draft only"));
            Assert.That(root.Q<Label>("industryPolicyPreviewLabel").text, Does.Contain("Retain company cash 100"));
            Assert.That(root.Q<Label>("industryPolicyPreviewLabel").text, Does.Contain("Automatic local sales OFF"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            root.Q<Foldout>("industryAdvancedFoldout").value = true;
            root.Q<Foldout>("industryDetailsFoldout").value = true;
            root.Q<TextField>("inputPrice_flour").value = "0";
            Assert.That(root.Q<Label>("industryPolicyPreviewLabel").text, Does.Contain("Incomplete draft"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("recommendIndustryPresetButton"); Click("applyIndustryPolicy");
            facility = controller.Session.Snapshot.PrivateFacilities[facility.Id];
            Assert.That(facility.Policy.ProcurementEnabled, Is.True);
            Assert.That(facility.Policy.SalesEnabled, Is.False);
            Assert.That(facility.Policy.Inputs.Single(rule => rule.GoodId == "flour").MaxBuyPrice, Is.EqualTo(24));
            Assert.That(facility.StoredUnits, Is.Zero);
            Assert.That(facility.ProcurementCost, Is.Zero);
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Companies[facility.OwnerId].Cash));
            Assert.That(root.Q<Label>("industryAppliedPolicyLabel").text, Does.Contain("input buying on"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingInputsUseLocalQuotesAndFinancialFeedbackUsesCommittedAmounts()
        {
            controller.BeginRun(null, RunScenario.MerchantCharter);
            OpenBakeryInvestment(); Click("constructButton");
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            var preview = controller.Session.Snapshot.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                new TradeCommand(TradeDirection.Buy, "flour", 1));
            var ask = controller.Session.Snapshot.GetMarketQuote(facility.PortId, "flour").Ask.Value;
            Assert.That(root.Q<Label>("facilityStateLabel").text, Does.Contain("INPUT-STARVED"));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("next buy " + ask));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("exact " + preview.TotalAmount + " for 1"));
            root.Q<DropdownField>("storageGoodDropdown").value = "Flour";
            root.Q<TextField>("storageQuantity").value = "1";
            Click("warehouseBuy");
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Local inventory purchases " + preview.TotalAmount));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Capital paid " + facility.ConstructionCost));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("not profit"));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("Flour: private 1/1 · missing 0"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConstructionControlsShowExactTimelineAndNoCompletionDayBatch()
        {
            foreach (var difficulty in new[] { RunDifficulty.Normal, RunDifficulty.SlowFoundations })
            {
                controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, difficulty: difficulty));
                OpenBakeryInvestment();
                var schedule = controller.Session.Snapshot.PreviewConstructionSchedule(controller.Session.Player.OwnerId,
                    EnterpriseScenario.BakeryBlueprint);
                Assert.That(root.Q<Label>("constructionLabel").text, Does.Contain("first batch eligible day " + schedule.FirstProductionDay));
                Click("constructButton");
                var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
                Assert.That(facility.IsOperational, Is.EqualTo(difficulty == RunDifficulty.Normal));
                var capital = controller.Session.Snapshot.Accounting.Companies[facility.OwnerId].ConstructionExpenditure;
                foreach (var good in new[] { "Flour", "Logs" })
                {
                    root.Q<DropdownField>("storageGoodDropdown").value = good;
                    root.Q<TextField>("storageQuantity").value = "1";
                    Click("warehouseBuy");
                }
                Click("recommendIndustryPresetButton"); Click("applyIndustryPolicy");
                var manualPurchases = controller.Session.Snapshot.PrivateFacilities[facility.Id].ProcurementCost;
                if (difficulty == RunDifficulty.SlowFoundations)
                {
                    Assert.That(root.Q<Label>("facilityStateLabel").text, Does.Contain("2 days remaining"));
                    Assert.That(root.Q<Label>("facilityScheduleLabel").text, Does.Contain("completion day 2"));
                }
                for (var day = 1; day <= schedule.FirstProductionDay; day++)
                {
                    Click("closeIndustryButton"); Click("nextDayButton"); Click("openIndustryButton");
                    facility = controller.Session.Snapshot.PrivateFacilities[facility.Id];
                    Assert.That(facility.BatchesProduced, day < schedule.FirstProductionDay ? Is.Zero : Is.EqualTo(1));
                    Assert.That(controller.Session.Snapshot.Accounting.Companies[facility.OwnerId].ConstructionExpenditure, Is.EqualTo(capital));
                    if (difficulty == RunDifficulty.SlowFoundations && day <= 2)
                    {
                        Assert.That(facility.ProcurementCost, Is.EqualTo(manualPurchases));
                        Assert.That(facility.ConstructionDaysRemaining, Is.EqualTo(2 - day));
                        Assert.That(facility.IsOperational, Is.EqualTo(day == 2));
                    }
                    yield return null;
                }
                Click("closeIndustryButton");
            }
        }

        [UnityTest]
        public IEnumerator StarterGrantAndPhysicalShippingLinkAreTransparentAndOpeningIsPassive()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.IndustrialCharter, difficulty: RunDifficulty.SlowFoundations));
            Click("openIndustryButton");
            Assert.That(root.Q<Label>("facilityScheduleLabel").text, Does.Contain("Already-built starter grant"));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Capital paid 0"));
            Assert.That(root.Q<Label>("industryShippingLabel").text, Does.Contain("NO PHYSICAL SHIPPING LINK"));
            var before = controller.Session.Snapshot;
            root.Q<Foldout>("industryAdvancedFoldout").value = true;
            root.Q<Foldout>("industryShippingFoldout").value = true;
            Click("industryShippingButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Logistics));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("applyLogisticsButton");
            root.Q<Toggle>("logisticsEnabled").value = true;
            Click("logisticsBackButton"); Click("openIndustryButton");
            Assert.That(root.Q<Label>("industryShippingLabel").text, Does.Contain("Enabled"));
            Assert.That(root.Q<Label>("industryShippingLabel").text, Does.Contain(controller.Session.CurrentDecision.Reason));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlockedInputPricesAndUnsoldOutputRemainExplainable()
        {
            controller.BeginRun(null, RunScenario.MerchantCharter);
            OpenBakeryInvestment(); Click("constructButton");
            Click("recommendIndustryPresetButton");
            root.Q<Foldout>("industryAdvancedFoldout").value = true;
            root.Q<Foldout>("industryDetailsFoldout").value = true;
            root.Q<TextField>("inputPrice_flour").value = "1";
            root.Q<TextField>("inputPrice_logs").value = "1";
            Click("applyIndustryPolicy"); Click("closeIndustryButton"); Click("nextDayButton"); Click("openIndustryButton");
            Assert.That(root.Q<Label>("facilityStateLabel").text, Does.Contain("PRICE-BLOCKED"));
            controller.BeginRun(null, RunScenario.IndustrialCharter);
            Click("openIndustryButton");
            root.Q<Foldout>("industryAdvancedFoldout").value = true;
            root.Q<Foldout>("industryDetailsFoldout").value = true;
            root.Q<Toggle>("procurementEnabled").value = false;
            root.Q<Toggle>("salesEnabled").value = true;
            root.Q<TextField>("outputPrice_bread").value = "999";
            Click("applyIndustryPolicy"); Click("closeIndustryButton");
            Click("nextDayButton"); Click("nextDayButton"); Click("openIndustryButton");
            var facility = controller.Session.Snapshot.PrivateFacilities[RunScenario.StarterBakery];
            Assert.That(facility.Inventory["bread"], Is.GreaterThan(0));
            Assert.That(facility.SalesRevenue, Is.Zero);
            Assert.That(root.Q<Label>("industryAppliedPolicyLabel").text, Does.Contain("sell price limit"));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("actual local sale revenue 0"));
            yield return null;
        }

        private void OpenBakeryInvestment()
        {
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Bakery";
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
