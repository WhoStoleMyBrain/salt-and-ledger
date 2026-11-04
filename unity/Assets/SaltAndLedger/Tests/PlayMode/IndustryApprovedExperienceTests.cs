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
    public sealed class IndustryApprovedExperienceTests
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
            controller.BeginRun(null, RunScenario.IndustrialCharter);
            root = controller.Root;
        }

        [UnityTest]
        public IEnumerator BakeryShortagePurchaseDraftApplyAndRealProductionFormOneJourney()
        {
            controller.BeginRun(null, RunScenario.MerchantCharter);
            Click("openIndustryButton"); Click("construct_" + WorldIds.BakeryBlueprint);
            var bakery = controller.Session.Snapshot.PrivateFacilities.Values.Single();
            Assert.That(root.Q("factoryInput_flour").ClassListContains("industry-v3-blocked"), Is.True);
            var ordered = controller.Session.Snapshot;
            Click("factoryShortagePurchase");
            Assert.That(controller.TradingPair, Is.EqualTo(HarborPair.MarketWarehouse));
            Assert.That(root.Q<DropdownField>("goodDropdown").value, Is.EqualTo("Flour"));
            root.Q<TextField>("quantityField").value = "2";
            var quote = controller.Session.Snapshot.PreviewWarehouseTrade(bakery.OwnerId, bakery.Id,
                new TradeCommand(TradeDirection.Buy, "flour", 20000));
            Submit(root.Q("tradeGood_flour").Q<Button>("rightArrow"));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(ordered.Companies[bakery.OwnerId].Cash - quote.TotalAmount));
            Click("closeTradeButton");
            Assert.That(controller.IndustryIsOpen, Is.True);
            Assert.That(root.Q("industryPolicyScroll").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q("factoryInput_flour").ClassListContains("industry-v3-blocked"), Is.False);
            Click("factoryShortagePurchase");
            Assert.That(root.Q<DropdownField>("goodDropdown").value, Is.EqualTo("Logs"));
            Submit(root.Q("tradeGood_logs").Q<Button>("rightArrow"));
            Click("closeTradeButton");
            Assert.That(EconomicSignals.PrivateProduction(controller.Session.Snapshot, controller.Session.Content, bakery.Id).ReadyFromCurrentState, Is.True);
            var supplied = controller.Session.Snapshot;
            Click("facilityRate50"); Click("facilityPriorityHigh");
            Assert.That(controller.Session.Snapshot, Is.SameAs(supplied));
            Click("applyIndustryPolicy");
            var applied = controller.Session.Snapshot;
            Assert.That(applied.Revision, Is.EqualTo(supplied.Revision + 1));
            Assert.That(applied.PrivateFacilities[bakery.Id].OperatingRate, Is.EqualTo(50));
            Assert.That(applied.PrivateFacilities[bakery.Id].Priority, Is.EqualTo(ProductionPriority.High));
            Assert.That(applied.Day, Is.EqualTo(ordered.Day));
            var firstBatch = EconomicSignals.PrivateProduction(applied, controller.Session.Content, bakery.Id).NextRateAdmittedDay.Value;
            Click("closeIndustryButton");
            while (controller.Session.Snapshot.Day < firstBatch) Click("nextDayButton");
            bakery = controller.Session.Snapshot.PrivateFacilities[bakery.Id];
            Assert.That(bakery.BatchesProduced, Is.EqualTo(1));
            Assert.That(bakery.WagesPaid, Is.EqualTo(bakery.Blueprint.WagePerBatch));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(bakery.Id).Inventory["bread"], Is.GreaterThan(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidWarehouseEditRetainsLastPolicyWhileOtherGoodsSaveAndOppositeRuleRepairsIt()
        {
            Click("openIndustryButton"); Click("industryWarehouseTab");
            root.Q<TextField>("warehouseBuyTarget_bread").value = "2";
            root.Q<Toggle>("warehouseBuyEnabled_bread").value = true;
            Click("warehouseSellTab");
            root.Q<TextField>("warehouseSellTarget_bread").value = "4";
            root.Q<Toggle>("warehouseSellEnabled_bread").value = true;
            Click("warehouseBuyTab");
            var beforeInvalid = controller.Session.Snapshot;
            root.Q<TextField>("warehouseBuyTarget_bread").value = "10";
            Assert.That(controller.Session.Snapshot, Is.SameAs(beforeInvalid));
            Assert.That(root.Q<Label>("warehouseInspectorError").text, Does.Contain("Buy target cannot exceed sell reserve"));
            Assert.That(root.Q("warehouseBuyRow_bread").ClassListContains("industry-v3-invalid"), Is.True);
            root.Q<TextField>("warehouseBuyTarget_grain").value = "2.5";
            var afterGrain = controller.Session.Snapshot;
            Assert.That(afterGrain.Revision, Is.EqualTo(beforeInvalid.Revision + 1));
            Assert.That(Pool().Policy.Rules.Single(r => r.GoodId == "bread").BuyTarget, Is.EqualTo(20000));
            Assert.That(Pool().Policy.Rules.Single(r => r.GoodId == "grain").BuyTarget, Is.EqualTo(25000));
            root.Q<TextField>("warehouseBuyTarget_bread").Focus();
            Click("warehouseOppositeRule");
            Assert.That(root.Q("warehouseSellRows").ClassListContains("hidden"), Is.False);
            root.Q<TextField>("warehouseSellTarget_bread").value = "10";
            var repaired = Pool().Policy.Rules.Single(r => r.GoodId == "bread");
            Assert.That(repaired.BuyTarget, Is.EqualTo(100000));
            Assert.That(repaired.SellReserve, Is.EqualTo(100000));
            root.Q<Toggle>("warehouseSellEnabled_bread").value = false;
            Assert.That(Pool().Policy.Rules.Single(r => r.GoodId == "bread").SellReserve, Is.EqualTo(100000));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(beforeInvalid.Day));
            Assert.That(controller.Session.Snapshot.Companies[controller.Session.Player.OwnerId].Cash,
                Is.EqualTo(beforeInvalid.Companies[controller.Session.Player.OwnerId].Cash));
            CollectionAssert.AreEquivalent(beforeInvalid.CompanyStockpiles[Pool().Id].Inventory, Pool().Inventory);
            var save = controller.Session.SaveText();
            controller.Session.LoadSaveText(save);
            Assert.That(controller.Session.SaveText(), Is.EqualTo(save));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReservePopupUsesSameBuyOnlyReserveOnBothTabsAndCancelClosesItFirst()
        {
            Click("openIndustryButton"); Click("industryWarehouseTab"); Click("warehouseSellTab");
            var original = controller.Session.Snapshot;
            Click("editWarehouseCashReserve");
            Assert.That(controller.Session.Snapshot, Is.SameAs(original));
            root.Q<TextField>("warehouseCashReserve").value = "80.25";
            Assert.That(Pool().Policy.CashReserve, Is.EqualTo(802500));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(original.Day));
            var applied = controller.Session.Snapshot;
            root.Q<TextField>("warehouseCashReserve").value = "-1";
            Assert.That(controller.Session.Snapshot, Is.SameAs(applied));
            Assert.That(root.Q<Label>("warehouseReserveError").text, Is.Not.Empty);
            using (var evt = NavigationCancelEvent.GetPooled()) { evt.target = root.Q<TextField>("warehouseCashReserve"); root.SendEvent(evt); }
            Assert.That(controller.IndustryIsOpen, Is.True);
            Assert.That(root.Q("warehouseCashReservePopup").ClassListContains("hidden"), Is.True);
            Click("warehouseBuyTab");
            Assert.That(root.Q<Button>("editWarehouseCashReserve").text, Does.Contain("80.25"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(applied));
            Click("editWarehouseCashReserve");
            yield return null;
            Click("industryBackToOverview");
            Assert.That(root.Q("industryOverview").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q("industryWarehousePane").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("warehouseCashReservePopup").ClassListContains("hidden"), Is.True);
            yield return null;
            Click("industryWarehouseTab"); Click("editWarehouseCashReserve"); Click("closeIndustryButton");
            Assert.That(controller.IndustryIsOpen, Is.False);
            Click("openIndustryButton");
            yield return null;
            Click("industryWarehouseTab");
            Assert.That(root.Q("warehouseCashReservePopup").ClassListContains("hidden"), Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(applied));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublicIndustryInspectionAndPrivateDraftCancelArePassive()
        {
            var original = controller.Session.Snapshot;
            var publicId = original.Ports[controller.Session.SelectedPortId].Facilities.First().Configuration.Instance.Id;
            Click("openIndustryButton"); Click("manageFactory_" + publicId);
            Assert.That(root.Q<Label>("facilityOwnership").text, Does.Contain("TOWN"));
            Assert.That(root.Q("factorySettings").ClassListContains("hidden"), Is.True);
            yield return null;
            Click("industryBackToOverview");
            var privateId = original.PrivateFacilities.Values.Single().Id;
            Click("manageFactory_" + privateId);
            Click("facilityRate50"); Click("facilityPriorityHigh");
            Assert.That(controller.Session.Snapshot, Is.SameAs(original));
            Click("cancelIndustryPolicy");
            Assert.That(root.Q<TextField>("facilityOperatingRate").value, Is.EqualTo(original.PrivateFacilities[privateId].OperatingRate.ToString()));
            Assert.That(controller.Session.Snapshot, Is.SameAs(original));
            yield return null;
            Click("industryBackToOverview"); Click("closeIndustryButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(original));
            yield return null;
        }

        private CompanyStockpileSnapshot Pool() => controller.Session.Snapshot.CompanyStockpiles.Values.Single(p =>
            p.OwnerId == controller.Session.Player.OwnerId && p.PortId == controller.Session.SelectedPortId);

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            Assert.That(button.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None), name);
            Submit(button);
        }

        private void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledInHierarchy, Is.True, button.name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
