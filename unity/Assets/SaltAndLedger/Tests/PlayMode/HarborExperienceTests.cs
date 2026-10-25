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
            Assert.That(root.Q<DropdownField>("worldModePicker").index, Is.EqualTo(3));
            Assert.That(root.Q<DropdownField>("challengePicker").value, Is.EqualTo(RunScenario.GetChallenge(GrandCommission.ChallengeId).Name));
            Assert.That(controller.SetupIdentity.CompanyStorageRules, Is.EqualTo(CompanyStorageRules.Pooled));
            Assert.That(root.Q<Foldout>("advancedWorldFoldout").value, Is.False);
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
            Click("buyButton");
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Companies[facility.OwnerId].Cash - quote.TotalAmount));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(15000));
            Quantity("0.5"); Assert.That(root.Q<Button>("harborLoadButton").enabledSelf, Is.False);
            Quantity("1"); var paid = controller.Session.Snapshot;
            Click("harborLoadButton");
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(10000));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(paid.Companies[facility.OwnerId].Cash));
            Click("harborUnloadButton");
            Assert.That(controller.Session.Player.Cargo["grain"], Is.Zero);
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(15000));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Click("closeTradeButton");
            var remote = controller.Session.Snapshot.Ports.Keys.First(id => id != controller.Session.Player.DockedPortId);
            controller.OpenTrade(remote);
            Assert.That(root.Q<Button>("harborLoadButton").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("harborUnloadButton").enabledSelf, Is.False);
            Assert.That(root.Q<Button>("tradeGood_grain").Q<Label>("ship").text, Is.EqualTo("—"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MillAndBakeryConsumeTheTownPoolThroughRealFundedDays()
        {
            BeginIndustrial(CompanyStorageRules.Pooled);
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Mill"; Click("constructButton");
            Click("industryHarborButton"); LocalStock(); Good("grain"); Quantity("2"); Click("buyButton");
            Assert.That(root.Q<DropdownField>("harborLegacyStore").ClassListContains("hidden"), Is.True);
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
            Assert.That(root.Q<Label>("harborStockExplanation").text, Does.Contain("One town stockpile"));
            Assert.That(root.Q<Label>("harborProductionLabel").text, Does.Contain("Mill").And.Contain("Bakery"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RetryPreservesLegacyStorageButChangeSetupStartsTheNewStorageRules()
        {
            BeginIndustrial(CompanyStorageRules.Legacy);
            var legacy = controller.Session.Identity;
            Click("nextDayButton"); Click("menuButton"); Click("restartRunButton"); Click("confirmAcceptButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(legacy));
            Assert.That(controller.Session.Snapshot.UsesPooledCompanyStorage, Is.False);
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Click("menuButton"); Click("changeSetupButton"); Click("confirmAcceptButton");
            Assert.That(controller.SetupIdentity.Seed, Is.EqualTo(legacy.Seed));
            Assert.That(controller.SetupIdentity.CharterId, Is.EqualTo(legacy.CharterId));
            Assert.That(controller.SetupIdentity.CompanyStorageRules, Is.EqualTo(CompanyStorageRules.Pooled));
            Click("startRunButton");
            Assert.That(controller.Session.Snapshot.UsesPooledCompanyStorage, Is.True);
            Assert.That(controller.Session.Identity.Seed, Is.EqualTo(legacy.Seed));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LegacyHarborNamesTheActualSeparateStoreAndNpcCannotAct()
        {
            BeginIndustrial(CompanyStorageRules.Legacy);
            controller.OpenTrade(controller.Session.SelectedPortId);
            Assert.That(root.Q<DropdownField>("harborLegacyStore").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q<Label>("harborStockExplanation").text, Does.Contain("no inventories have been merged"));
            Assert.That(controller.Session.Snapshot.CompanyStockpiles.Count, Is.Zero);
            Click("closeTradeButton");
            var npc = controller.Session.Snapshot.Ships.Values.First(ship => !ship.IsPlayer);
            controller.Session.SelectShip(npc.Id); controller.OpenTrade(npc.DockedPortId);
            Assert.That(root.Q<Label>("tradeAvailabilityLabel").text, Does.Contain("NPC inspection only"));
            foreach (var id in new[] { "buyButton", "sellButton", "harborLoadButton", "harborUnloadButton" })
                Assert.That(root.Q<Button>(id).enabledSelf, Is.False, id);
            yield return null;
        }

        private void BeginIndustrial(CompanyStorageRules storage) => controller.BeginRun(new RunIdentity(null,
            RunScenario.IndustrialCharter, RunWorldMode.ExpandingEconomy, 42, companyStorageRules: storage));
        private void LocalStock() => root.Q<DropdownField>("harborTradeTarget").value = "Company local stock";
        private void Good(string id) => root.Q<DropdownField>("goodDropdown").value = controller.Session.Content.GetGood(id).Name;
        private void Quantity(string value) => root.Q<TextField>("quantityField").value = value;
        private void Click(string id, VisualElement parent = null)
        {
            var button = (parent ?? root).Q<Button>(id); Assert.That(button, Is.Not.Null, id);
            Assert.That(button.enabledInHierarchy, Is.True, id);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
