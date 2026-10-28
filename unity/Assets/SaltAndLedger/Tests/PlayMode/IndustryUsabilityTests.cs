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
        public IEnumerator FactorySettingsCommitTogetherAndWarehouseDraftSurvivesTabChanges()
        {
            controller.BeginRun(null, RunScenario.IndustrialCharter);
            Click("openIndustryButton");
            var before = controller.Session.Snapshot;
            var facility = before.PrivateFacilities.Values.Single();
            root.Q<Toggle>("facilityEnabled").value = false;
            Click("facilityRate50");
            root.Q<DropdownField>("facilityPriority").value = "High";
            Click("industryWarehouseTab");
            Assert.That(root.Q("warehouseBuyRows").childCount, Is.EqualTo(7));
            Assert.That(root.Q("warehouseSellRows").childCount, Is.EqualTo(7));
            root.Q<Toggle>("warehouseBuyEnabled_grain").value = true;
            root.Q<TextField>("warehouseBuyTarget_grain").value = "2.5";
            root.Q<TextField>("warehouseCashReserve").value = "80.25";
            Click("warehouseSellTab"); Click("industryFactoriesTab");
            Assert.That(root.Q<TextField>("facilityOperatingRate").value, Is.EqualTo("50"));
            Assert.That(root.Q<DropdownField>("facilityPriority").value, Is.EqualTo("High"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("applyIndustryPolicy");
            var after = controller.Session.Snapshot;
            var applied = after.PrivateFacilities[facility.Id];
            Assert.That(after.Revision, Is.EqualTo(before.Revision + 1));
            Assert.That(applied.Enabled, Is.False);
            Assert.That(applied.OperatingRate, Is.EqualTo(50));
            Assert.That(applied.Priority, Is.EqualTo(ProductionPriority.High));
            Assert.That(applied.NextProductionDay, Is.EqualTo(facility.NextProductionDay));
            Assert.That(applied.ProductionCredit, Is.EqualTo(facility.ProductionCredit));
            Assert.That(root.Q<Label>("facilityFlowLabel").text, Does.Contain("0 Bread"), "Disabled factories have no enabled production potential.");
            Assert.That(after.Day, Is.EqualTo(before.Day));
            Assert.That(after.Companies[facility.OwnerId].Cash, Is.EqualTo(before.Companies[facility.OwnerId].Cash));
            Click("industryWarehouseTab"); Click("warehouseBuyTab");
            Assert.That(root.Q<TextField>("warehouseBuyTarget_grain").value, Is.EqualTo("2.5"));
            Assert.That(root.Q<TextField>("warehouseCashReserve").value, Is.EqualTo("80.25"));
            Click("applyWarehousePolicy");
            var pool = controller.Session.Snapshot.GetFacilityStorage(facility.Id);
            Assert.That(pool.Policy.Rules.Single(rule => rule.GoodId == "grain").BuyTarget, Is.EqualTo(25000));
            Assert.That(pool.Policy.CashReserve, Is.EqualTo(802500));
            Assert.That(pool.Inventory["grain"], Is.EqualTo(before.GetFacilityStorage(facility.Id).Inventory["grain"]));
            Click("industryHarborButton"); Click("harborWarehouseButton");
            Assert.That(root.Q("industryWarehousePane").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q<TextField>("warehouseBuyTarget_grain").value, Is.EqualTo("2.5"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidWarehouseTargetsAndFactoryRatesCannotApplyOrMutateState()
        {
            controller.BeginRun(null, RunScenario.IndustrialCharter);
            Click("openIndustryButton");
            var before = controller.Session.Snapshot;
            root.Q<TextField>("facilityOperatingRate").value = "101";
            Assert.That(root.Q<Button>("applyIndustryPolicy").enabledSelf, Is.False);
            Click("industryWarehouseTab");
            root.Q<Toggle>("warehouseBuyEnabled_logs").value = true;
            root.Q<TextField>("warehouseBuyTarget_logs").value = "3";
            Click("warehouseSellTab");
            root.Q<Toggle>("warehouseSellEnabled_logs").value = true;
            root.Q<TextField>("warehouseSellTarget_logs").value = "2";
            Assert.That(root.Q<Label>("warehousePolicyDraftLabel").text, Does.Contain("Buy target cannot exceed sell reserve"));
            Assert.That(root.Q<Button>("applyWarehousePolicy").enabledSelf, Is.False);
            root.Q<TextField>("warehouseSellTarget_logs").value = "3";
            Assert.That(root.Q<Button>("applyWarehousePolicy").enabledSelf, Is.True);
            root.Q<TextField>("warehouseSellTarget_logs").value = "1000";
            Assert.That(root.Q<Label>("warehousePolicyDraftLabel").text, Does.Contain("capacity"));
            Assert.That(root.Q<Button>("applyWarehousePolicy").enabledSelf, Is.False);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(root.Q<Label>("warehouseBuyPreview_logs").text, Does.StartWith("Invalid draft"));
            Click("reloadWarehousePolicy");
            Assert.That(root.Q<Toggle>("warehouseBuyEnabled_logs").value, Is.False);
            Assert.That(root.Q<Toggle>("warehouseSellEnabled_logs").value, Is.False);
            Assert.That(root.Q<Button>("applyWarehousePolicy").enabledSelf, Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingInputsUseLocalQuotesAndFinancialFeedbackUsesCommittedAmounts()
        {
            controller.BeginRun(null, RunScenario.MerchantCharter);
            OpenBakeryInvestment(); Click("constructButton");
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            var preview = controller.Session.Snapshot.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                new TradeCommand(TradeDirection.Buy, "flour", 10000));
            var ask = controller.Session.Snapshot.GetMarketQuote(facility.PortId, "flour").Ask.Value;
            Assert.That(root.Q<Label>("facilityStateLabel").text, Does.Contain("waiting for inputs"));
            Assert.That(root.Q<Label>("facilityInputsSummary").text, Does.Contain("Flour 0/1").And.Contain("Logs 0/1"));
            Assert.That(root.Q<Label>("facilityScheduleLabel").text, Does.Contain("full wage " + controller.Session.Coins(facility.Blueprint.WagePerBatch)));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("next buy " + ask));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("exact " + controller.Session.Coins(preview.TotalAmount) + " for 1"));
            root.Q<DropdownField>("storageGoodDropdown").value = "Flour";
            root.Q<TextField>("storageQuantity").value = "1";
            Click("warehouseBuy");
            Assert.That(root.Q<Label>("warehouseAutomationAccounting").text, Does.Contain("Actual purchases " + controller.Session.Coins(preview.TotalAmount)));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Capital paid " + controller.Session.Coins(facility.ConstructionCost)));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Warehouse purchases and sales appear on the Warehouse tab"));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Not.Contain("Inputs remain in this facility's separate store"));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Shared inputs can serve other local facilities"));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("Flour: warehouse 1/1 · missing 0"));
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
                    WorldIds.BakeryBlueprint);
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
                Click("applyIndustryPolicy");
                var manualPurchases = controller.Session.Snapshot.GetFacilityStorage(facility.Id).ProcurementCost;
                if (difficulty == RunDifficulty.SlowFoundations)
                {
                    Assert.That(root.Q<Label>("facilityStateLabel").text, Does.Contain("2 days remaining"));
                    Assert.That(root.Q<Label>("facilityScheduleDetail").text, Does.Contain("completion day 2"));
                    Assert.That(root.Q<Label>("facilityFlowLabel").text, Does.Contain("0 Bread"), "Pending factories have no enabled production potential.");
                }
                for (var day = 1; day <= schedule.FirstProductionDay; day++)
                {
                    Click("closeIndustryButton"); Click("nextDayButton"); Click("openIndustryButton");
                    facility = controller.Session.Snapshot.PrivateFacilities[facility.Id];
                    Assert.That(facility.BatchesProduced, day < schedule.FirstProductionDay ? Is.Zero : Is.EqualTo(1));
                    Assert.That(controller.Session.Snapshot.Accounting.Companies[facility.OwnerId].ConstructionExpenditure, Is.EqualTo(capital));
                    if (difficulty == RunDifficulty.SlowFoundations && day <= 2)
                    {
                        Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).ProcurementCost, Is.EqualTo(manualPurchases));
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
            Assert.That(root.Q<Label>("facilityScheduleDetail").text, Does.Contain("Already-built starter grant"));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("Capital paid 0"));
            Assert.That(root.Q<Label>("industryShippingLabel").text, Does.Contain("NO CONFIGURED TOWN-STOCK SHIPPING"));
            var before = controller.Session.Snapshot;
            root.Q<Foldout>("industryAdvancedFoldout").value = true;
            root.Q<Foldout>("industryShippingFoldout").value = true;
            Click("industryShippingButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("itineraryBackButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.Player.CargoUnits, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WarehouseBuysBeforeProductionAndRetainsUnsoldOutputWithExactFeedback()
        {
            controller.BeginRun(null, RunScenario.MerchantCharter);
            OpenBakeryInvestment(); Click("constructButton");
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            Click("industryWarehouseTab");
            foreach (var good in new[] { "flour", "logs" })
            {
                root.Q<Toggle>("warehouseBuyEnabled_" + good).value = true;
                root.Q<TextField>("warehouseBuyTarget_" + good).value = "3";
                root.Q<TextField>("warehouseBuyPrice_" + good).value = "1";
            }
            Click("applyWarehousePolicy"); Click("closeIndustryButton"); Click("nextDayButton"); Click("openIndustryButton");
            var pool = controller.Session.Snapshot.GetFacilityStorage(facility.Id);
            Assert.That(pool.ProcurementWaitReason, Is.EqualTo(IndustryWaitReason.BuyPriceLimit));
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("ask exceeds limit"));
            Click("industryWarehouseTab");
            foreach (var good in new[] { "flour", "logs" }) root.Q<TextField>("warehouseBuyPrice_" + good).value = "0";
            var expected = controller.Session.Snapshot.PreviewWarehouseAutomation(facility.OwnerId, facility.PortId, TradeDirection.Buy, "flour",
                new WarehousePolicy(0, new[] { new WarehouseCommodityRule("flour", true, 30000, 0, false, 0, 0) }));
            Assert.That(root.Q<Label>("warehouseBuyPreview_flour").text, Does.Contain("Now: " + controller.Session.Quantity(expected.Quantity)));
            Assert.That(root.Q<Label>("warehouseBuyPreview_flour").text, Does.Contain(controller.Session.Coins(expected.TotalAmount)));
            Click("warehouseSellTab");
            root.Q<Toggle>("warehouseSellEnabled_bread").value = true;
            root.Q<TextField>("warehouseSellPrice_bread").value = "999";
            Click("applyWarehousePolicy"); Click("closeIndustryButton"); Click("nextDayButton"); Click("openIndustryButton");
            facility = controller.Session.Snapshot.PrivateFacilities[facility.Id];
            pool = controller.Session.Snapshot.GetFacilityStorage(facility.Id);
            Assert.That(facility.BatchesProduced, Is.EqualTo(1));
            Assert.That(pool.Inventory["bread"], Is.GreaterThan(0));
            Assert.That(pool.ProcurementCost, Is.GreaterThan(0));
            Assert.That(pool.SalesRevenue, Is.Zero);
            Assert.That(pool.SalesWaitReason, Is.EqualTo(IndustryWaitReason.SellPriceLimit));
            Assert.That(root.Q<Label>("warehouseAutomationAccounting").text, Does.Contain("bid below limit"));
            Assert.That(root.Q<Label>("facilityStatsLabel").text, Does.Contain("paid wages " + controller.Session.Coins(facility.WagesPaid)));
            Click("industryWarehouseTab");
            var flow = EconomicSignals.CommodityFlow(controller.Session.Snapshot, controller.Session.Content, facility.PortId, "bread", facility.OwnerId);
            Assert.That(root.Q("warehouseSellRow_bread").Q<Label>("flow").text,
                Does.Contain(controller.Session.Quantity(flow.EnabledProductionQuantity) + " output"));
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
