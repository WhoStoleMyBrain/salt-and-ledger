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
    public sealed class CirculationExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
        }

        [UnityTest]
        public IEnumerator FreshMapPreviewUsesCirculationAndFiniteStartingFood()
        {
            Click("sandboxMenuButton");
            Assert.That(controller.SetupIdentity.EconomicRulesVersion, Is.EqualTo(1));
            var before = controller.Session.Snapshot;
            Click("previewWorldButton");
            var preview = controller.CommissionPreview;
            Assert.That(preview.Circulation, Is.Not.Null);
            Assert.That(preview.Ports.Values.All(p => p.Inventory["bread"] > 0), Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("commissionMapBackButton"); Click("startRunButton");
            Assert.That(controller.Session.Identity.EconomicRulesVersion, Is.EqualTo(1));
            foreach (var port in preview.Ports.Values)
                Assert.That(controller.Session.Snapshot.Ports[port.Id].Inventory["bread"], Is.EqualTo(port.Inventory["bread"]));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HarborShowsExactFeeAndLedgerSeparatesTheExpense()
        {
            controller.BeginRun(Modern());
            controller.OpenTrade(controller.Session.Player.DockedPortId);
            root.Q<DropdownField>("harborTradeTarget").value = "Company local stock";
            root.Q<DropdownField>("goodDropdown").value = controller.Session.Content.GetGood("grain").Name;
            root.Q<TextField>("quantityField").value = "0.5";
            Click("buyButton");
            var before = controller.Session.Snapshot;
            var facility = before.PrivateFacilities.Values.First();
            var preview = before.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                new TradeCommand(TradeDirection.Sell, "grain", 5000));
            Assert.That(preview.ServiceFee, Is.GreaterThan(0));
            Assert.That(root.Q<Label>("sellPreviewLabel").text, Does.Contain("Service fee").And.Contain("net receipts").And.Contain("local workers"));
            Click("sellButton");
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(preview.TraderCashChange.After));
            var after = controller.Session.Snapshot;
            Assert.That(after.Accounting.Companies[facility.OwnerId].FeesPaid, Is.EqualTo(preview.ServiceFee));
            Assert.That(after.TotalCash, Is.EqualTo(before.TotalCash));
            Assert.That(after.Day, Is.EqualTo(before.Day));
            Click("closeTradeButton"); Click("ledgerButton");
            Assert.That(root.Q<Label>("ledgerFinances").text, Does.Contain("Port service fees paid").And.Contain("SURPLUS AFTER WAGES AND FEES"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityExplainsRecordedReviewAndSaveDoesNotRepeatIt()
        {
            controller.BeginRun(Modern());
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            var saved = controller.Session.SaveText();
            controller.Session.LoadSaveText(saved);
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            yield return null;
            Click("cityEconomyButton");
            var text = root.Q<Label>("cityCirculationDetail").text;
            Assert.That(text, Does.Contain("Last review day 4").And.Contain("Next review day 8").And.Contain("payroll reserve"));
            Assert.That(controller.Session.IsPlaying, Is.False);
            controller.TextScale.Apply(ArchipelagoUiScale.Large, false);
            yield return null;
            Assert.That(root.Q<Button>("cityEconomyBackButton").enabledInHierarchy, Is.True);
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
        }


        private static RunIdentity Modern() => new RunIdentity(null, RunScenario.IndustrialCharter,
            RunWorldMode.ExpandingEconomy, 42, companyStorageRules: CompanyStorageRules.Pooled,
            itineraryVersion: 1, economicRulesVersion: 1);

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
