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
    public sealed class HarborExperienceTests
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
        public IEnumerator NewRunDefaultsToPooledCommissionAndMapInspectionIsPassive()
        {
            Click("newRunButton");
            Assert.That(root.Q<DropdownField>("challengePicker").value, Is.EqualTo(RunScenario.GetChallenge(GrandCommission.ChallengeId).Name));
            Assert.That(controller.SetupIdentity.CompanyStorageRules, Is.EqualTo(CompanyStorageRules.Pooled));
            var active = controller.Session.Snapshot;
            Click("previewWorldButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CommissionMap));
            var preview = controller.CommissionPreview;
            foreach (var port in preview.Ports.Values)
            {
                Click("port_" + port.Id, root.Q<ArchipelagoMap>("commissionPreviewMap"));
                Assert.That(root.Q<Label>("commissionMapSelection").text, Does.Contain(port.Name));
                Assert.That(root.Q<Label>("commissionMapDetails").text, Does.Contain("STARTING MARKET STOCK").And.Contain("YOU CAN BUILD"));
            }
            Assert.That(controller.Session.Snapshot, Is.SameAs(active));
            Click("commissionMapBackButton");
            root.Q<TextField>("seedField").value = "not-a-seed";
            Assert.That(root.Q<Button>("previewWorldButton").enabledSelf, Is.False);
            Assert.That(controller.CommissionPreview, Is.SameAs(preview));
            Assert.That(controller.Session.Snapshot, Is.SameAs(active));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HarborUsesOnePoolForFractionalPaidTradesAndWholeDockedTransfers()
        {
            BeginIndustrial(CompanyStorageRules.Pooled);
            controller.OpenTrade(controller.Session.SelectedPortId);
            LocalStock(); Good("grain"); Quantity("1.5");
            var before = controller.Session.Snapshot;
            var facility = before.PrivateFacilities.Values.First();
            var quote = before.PreviewWarehouseTrade(facility.OwnerId, facility.Id, new TradeCommand(TradeDirection.Buy, "grain", 15000));
            Arrow("grain", true);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Companies[facility.OwnerId].Cash - quote.TotalAmount));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(15000));
            Click("pairWarehouseShip");
            Quantity("0.5"); Assert.That(Row("grain").Q<Button>("rightArrow").enabledSelf, Is.False);
            Quantity("1"); var paid = controller.Session.Snapshot;
            Arrow("grain", true);
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(10000));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(paid.Companies[facility.OwnerId].Cash));
            Arrow("grain", false);
            Assert.That(controller.Session.Player.Cargo["grain"], Is.Zero);
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(15000));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Click("closeTradeButton");
            var remote = controller.Session.Snapshot.Ports.Keys.First(id => id != controller.Session.Player.DockedPortId);
            controller.OpenTrade(remote);
            Click("pairWarehouseShip");
            Assert.That(Row("grain").Q<Button>("rightArrow").enabledSelf, Is.False);
            Assert.That(Row("grain").Q<Button>("leftArrow").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("approvedRightOwner").text, Does.Contain("REMOTE"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MillAndBakeryConsumeTheTownPoolThroughRealFundedDays()
        {
            BeginIndustrial(CompanyStorageRules.Pooled);
            Click("port_" + controller.Session.Player.DockedPortId);
            Click("openIndustryButton");
            var blueprint = controller.Session.Snapshot.IndustryBlueprints.Values.Single(b => b.BuildingId == "mill");
            Click("construct_" + blueprint.Id);
            Click("industryHarborButton"); LocalStock(); Quantity("2"); Arrow("grain", true);
            Click("closeTradeButton");
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            var snapshot = controller.Session.Snapshot;
            var mill = snapshot.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "mill");
            var bakery = snapshot.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "bakery");
            Assert.That(mill.BatchesProduced, Is.EqualTo(1));
            Assert.That(bakery.BatchesProduced, Is.EqualTo(2));
            Assert.That(mill.WagesPaid, Is.GreaterThan(0)); Assert.That(bakery.WagesPaid, Is.GreaterThan(0));
            Assert.That(snapshot.CompanyStockpiles.Count, Is.EqualTo(1));
            Assert.That(snapshot.GetFacilityStorage(mill.Id), Is.SameAs(snapshot.GetFacilityStorage(bakery.Id)));
            Assert.That(mill.Inventory.Values.All(quantity => quantity == 0), Is.True);
            Assert.That(bakery.Inventory.Values.All(quantity => quantity == 0), Is.True);
            controller.OpenTrade(mill.PortId);
            Good("grain");
            root.Q<Foldout>("harborProduction").value = true;
            Assert.That(root.Q<Label>("harborStockExplanation").text, Does.Contain("One town stockpile"));
            Assert.That(root.Q<Label>("harborProductionLabel").text, Does.Contain("Mill").And.Contain("Bakery"));
            yield return null;
        }



        private void BeginIndustrial(CompanyStorageRules storage) => controller.BeginRun(new RunIdentity(null,
            RunScenario.IndustrialCharter, RunWorldMode.ExpandingEconomy, 42, companyStorageRules: storage));
        private void LocalStock() => Click("pairMarketWarehouse");
        private VisualElement Row(string id) => root.Q("tradeGood_" + id);
        private void Good(string id) => Click("good", Row(id));
        private void Arrow(string id, bool right) => Click(right ? "rightArrow" : "leftArrow", Row(id));
        private void Quantity(string value) => root.Q<TextField>("quantityField").value = value;
        private void Click(string id, VisualElement parent = null)
        {
            var button = (parent ?? root).Q<Button>(id); Assert.That(button, Is.Not.Null, id);
            Assert.That(button.enabledInHierarchy, Is.True, id);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
