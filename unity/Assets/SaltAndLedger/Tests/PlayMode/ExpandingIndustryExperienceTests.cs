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
    public sealed class ExpandingIndustryExperienceTests
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
            controller.BeginRun(new RunIdentity(null, RunScenario.IndustrialCharter, RunWorldMode.ExpandingEconomy, 42));
            root = controller.Root;
        }

        [UnityTest]
        public IEnumerator MultipleMillsAndFractionalWarehouseUseExactControlsWithoutFractionalCargo()
        {
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Mill";
            Click("constructButton");
            Assert.That(root.Q<Button>("constructButton").enabledInHierarchy, Is.True);
            Click("constructButton");
            var mills = controller.Session.Snapshot.PrivateFacilities.Values.Where(f => f.Blueprint.BuildingId == "mill").ToArray();
            Assert.That(mills.Length, Is.EqualTo(2));
            Assert.That(root.Q<DropdownField>("facilityDropdown").choices.Distinct().Count(), Is.EqualTo(3));
            Assert.That(root.Q<Label>("constructionLabel").text, Does.Contain("Pay 260 now"));
            root.Q<DropdownField>("storageGoodDropdown").value = "Grain";
            root.Q<TextField>("storageQuantity").value = "0.75";
            var before = controller.Session.Snapshot;
            Click("warehouseBuy");
            var stocked = controller.Session.Snapshot.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "mill" && f.Inventory["grain"] > 0);
            Assert.That(stocked.Inventory["grain"], Is.EqualTo(7500));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(root.Q<Button>("warehouseToShip").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("private 0.75/2").And.Contain("missing 1.25"));
            Click("recommendIndustryPresetButton");
            root.Q<TextField>("inputTarget_grain").value = "2.5";
            root.Q<TextField>("industryCashReserve").value = "80.25";
            var passive = controller.Session.Snapshot;
            Assert.That(root.Q<Label>("industryPolicyPreviewLabel").text, Does.Contain("toward 2.5").And.Contain("cash 80.25"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(passive));
            Click("applyIndustryPolicy");
            var applied = controller.Session.Snapshot.PrivateFacilities[stocked.Id].Policy;
            Assert.That(applied.Inputs.Single().TargetQuantity, Is.EqualTo(25000));
            Assert.That(applied.CashReserve, Is.EqualTo(802500));
            Assert.That(applied.Inputs.Single().MaxBuyPrice, Is.EqualTo(24));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FleetSelectorLoadsIndependentAppliedRoutesAndReferenceValueIncludesPendingShip()
        {
            var session = controller.Session;
            var owner = session.Player.OwnerId;
            var beforeValue = session.Snapshot.CompanyValue(owner).Total;
            Assert.That(session.CommissionShip().Development.Single().Outcome, Is.EqualTo(DevelopmentOutcome.Started));
            Click("ledgerButton");
            Assert.That(root.Q<Label>("ledgerAssets").text, Does.Contain("paid construction in progress 500").And.Contain("Public Mint"));
            Assert.That(root.Q<Label>("ledgerFinances").text, Does.Contain("COMPANY VALUE").And.Contain("OPERATING SURPLUS AFTER WAGES"));
            Assert.That(session.Snapshot.CompanyValue(owner).Total, Is.EqualTo(beforeValue));
            Click("ledgerBackButton");
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            Assert.That(session.OwnedShips.Count, Is.EqualTo(2));
            Click("logisticsButton");
            var selector = root.Q<DropdownField>("logisticsShip");
            Assert.That(selector.choices.Count, Is.EqualTo(2));
            var unchanged = session.Snapshot.Ships[EnterpriseScenario.PlayerShipId];
            var passive = session.Snapshot;
            selector.value = selector.choices.Single(value => value.Contains("[player_ship_2]"));
            Assert.That(session.Snapshot, Is.SameAs(passive));
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            root.Q<TextField>("logisticsQuantity").value = "3";
            root.Q<TextField>("logisticsMinimum").value = "1";
            root.Q<TextField>("logisticsThreshold").value = "2";
            Click("applyLogisticsButton");
            Assert.That(session.Player.IndustrialPolicy.Outbound.MaximumShipmentQuantity, Is.EqualTo(30000));
            Assert.That(session.Snapshot.Ships[EnterpriseScenario.PlayerShipId].IndustrialPolicy, Is.SameAs(unchanged.IndustrialPolicy));
            Assert.That(session.Snapshot.Ships[EnterpriseScenario.PlayerShipId].Policy, Is.SameAs(unchanged.Policy));
            selector.value = selector.choices.Single(value => value.Contains("[" + EnterpriseScenario.PlayerShipId + "]"));
            Assert.That(session.Player.Id, Is.EqualTo(EnterpriseScenario.PlayerShipId));
            selector.value = selector.choices.Single(value => value.Contains("[player_ship_2]"));
            Assert.That(root.Q<TextField>("logisticsQuantity").value, Is.EqualTo("3"));
            Assert.That(root.Q<TextField>("logisticsCashReserve").value, Is.EqualTo("80"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CityNeedAmountsAndCurrentProductionPredictionRemainAuthoritative()
        {
            var session = controller.Session;
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            var port = session.Snapshot.Ports[session.SelectedPortId];
            var need = port.Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential);
            Click("cityEconomyButton"); Click("cityNeedsTab");
            var text = root.Q<Label>("cityNeedsDetail").text;
            Assert.That(text, Does.Contain("desired " + session.Quantity(need.LastDesiredQuantity)));
            Assert.That(text, Does.Contain("paid " + session.Coins(need.LastPayment)));
            Click("cityEconomyBackButton");
            Click("openIndustryButton");
            var facility = session.Snapshot.PrivateFacilities[RunScenario.StarterBakery];
            var prediction = session.Snapshot.PreviewFacilityProduction(facility.Id, session.Content);
            var status = root.Q<Label>("facilityStateLabel").text;
            Assert.That(status, Does.Contain("Last attempt:"));
            Assert.That(status, Does.Contain(prediction == ScheduledProductionOutcome.Produced
                ? "Ready with current stocks/funds" : ArchipelagoLabSession.FacilityStatus(prediction)));
            yield return null;
        }

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
    }
}
