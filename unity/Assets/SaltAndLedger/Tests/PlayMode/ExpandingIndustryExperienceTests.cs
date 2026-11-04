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
            var blueprint = controller.Session.Snapshot.IndustryBlueprints.Values.Single(b => b.BuildingId == "mill" &&
                b.EligiblePortIds.Contains(controller.Session.SelectedPortId));
            Click("construct_" + blueprint.Id);
            Click("industryBackToOverview");
            Assert.That(root.Q<Button>("construct_" + blueprint.Id).enabledInHierarchy, Is.True);
            Click("construct_" + blueprint.Id);
            var mills = controller.Session.Snapshot.PrivateFacilities.Values.Where(f => f.Blueprint.BuildingId == "mill").ToArray();
            Assert.That(mills.Length, Is.EqualTo(2));
            Click("industryBackToOverview");
            foreach (var mill in mills) Assert.That(root.Q<Button>("manageFactory_" + mill.Id), Is.Not.Null);
            Assert.That(root.Q<Label>("construction_" + blueprint.Id).text, Does.Contain("260 coins"));
            var stocked = mills.Last();
            Click("manageFactory_" + stocked.Id);
            Click("factoryWarehouseButton");
            root.Q<TextField>("quantityField").value = "0.75";
            var before = controller.Session.Snapshot;
            var buy = root.Q("tradeGood_grain").Q<Button>("rightArrow");
            Assert.That(buy.enabledInHierarchy, Is.True);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = buy; buy.SendEvent(evt); }
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(stocked.Id).Inventory["grain"], Is.EqualTo(7500));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Click("pairWarehouseShip");
            Assert.That(root.Q("tradeGood_grain").Q<Button>("rightArrow").enabledInHierarchy, Is.False);
            Click("closeTradeButton");
            Assert.That(root.Q<Label>("facilityInputStatusLabel").text, Does.Contain("warehouse 0.75/2").And.Contain("missing 1.25"));
            Click("factoryAutomationButton");
            root.Q<Toggle>("warehouseBuyEnabled_grain").value = true;
            root.Q<TextField>("warehouseBuyTarget_grain").value = "2.5";
            root.Q<TextField>("warehouseBuyPrice_grain").value = "24";
            Click("editWarehouseCashReserve");
            root.Q<TextField>("warehouseCashReserve").value = "80.25";
            Click("closeWarehouseCashReserve");
            var applied = controller.Session.Snapshot.GetFacilityStorage(stocked.Id).Policy;
            Assert.That(applied.Rules.Single(rule => rule.GoodId == "grain").BuyTarget, Is.EqualTo(25000));
            Assert.That(applied.CashReserve, Is.EqualTo(802500));
            Assert.That(applied.Rules.Single(rule => rule.GoodId == "grain").MaximumBuyPrice, Is.EqualTo(24));
            var passive = controller.Session.Snapshot;
            Click("warehouseSellTab"); Click("warehouseBuyTab");
            Assert.That(controller.Session.Snapshot, Is.SameAs(passive));
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
            Click("routePlannerButton");
            var selector = root.Q<DropdownField>("itineraryShip");
            Assert.That(selector.choices.Count, Is.EqualTo(2));
            var passive = session.Snapshot;
            var secondShip = session.OwnedShips.Single(ship => ship.Id == "player_ship_2");
            selector.value = selector.choices.Single(value => value == secondShip.Name);
            Assert.That(session.Snapshot, Is.SameAs(passive));
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            Assert.That(session.Snapshot.Ships[WorldIds.PlayerShipId].AutomationEnabled, Is.False);
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
            Click("manageFactory_" + facility.Id);
            var prediction = EconomicSignals.PrivateProduction(session.Snapshot, session.Content, facility.Id).CurrentReadiness;
            var status = root.Q<Label>("facilityStateLabel").text;
            Assert.That(root.Q<Label>("facilityStateLabel").tooltip, Does.Contain("Last attempt:"));
            Assert.That(status, Does.Contain(prediction == ScheduledProductionOutcome.Produced
                ? "Ready with current inputs, cash and space" : ArchipelagoLabSession.FacilityStatus(prediction)));
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
