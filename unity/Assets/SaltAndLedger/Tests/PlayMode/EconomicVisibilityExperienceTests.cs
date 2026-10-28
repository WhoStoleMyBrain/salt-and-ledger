using System.Collections;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class EconomicVisibilityExperienceTests
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
            controller.BeginRun(new RunIdentity(null, RunScenario.IndustrialCharter,
                RunWorldMode.ExpandingEconomy, 42));
        }

        [UnityTest]
        public IEnumerator CommodityRowsBindPublicNeedsAndPrivateInputsWithoutChangingTheTradeTargetOrEconomy()
        {
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId);
            Click("openTradeButton");
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            var ownerId = controller.Session.Player.OwnerId;
            root.Q<DropdownField>("harborTradeTarget").index = 1;

            foreach (var scope in new[] { 0, 1, 0 })
            {
                root.Q<DropdownField>("harborFlowScope").index = scope;
                Assert.That(root.Q<DropdownField>("harborTradeTarget").index, Is.EqualTo(1),
                    "Information scope must not silently redirect the paid order.");
                foreach (var good in controller.Session.Content.Goods)
                {
                    var signal = EconomicSignals.CommodityFlow(before, controller.Session.Content, portId,
                        good.Id, scope == 0 ? null : ownerId);
                    AssertRow(signal);
                }
            }

            var publicBread = EconomicSignals.CommodityFlow(before, controller.Session.Content, portId, "bread");
            var privateBread = EconomicSignals.CommodityFlow(before, controller.Session.Content, portId, "bread", ownerId);
            var privateFlour = EconomicSignals.CommodityFlow(before, controller.Session.Content, portId, "flour", ownerId);
            Assert.That(publicBread.ConsumptionNeedQuantity, Is.GreaterThan(0), "The witness includes real household demand.");
            Assert.That(privateBread.EnabledProductionQuantity, Is.GreaterThan(0), "The charter has an operational Bakery.");
            Assert.That(privateBread.ConsumptionNeedQuantity, Is.Zero, "Household demand belongs only to the public scope.");
            Assert.That(privateFlour.InputUsageQuantity, Is.GreaterThan(0));
            Assert.That(privateFlour.CurrentStock, Is.GreaterThan(0), "The private pool has actual starter ingredients.");
            Click("tradeGood_planks");
            root.Q<Foldout>("harborFlowDetails").value = true;
            var municipalDetails = AllText(root.Q("harborFlowBreakdown"));
            Assert.That(municipalDetails, Does.Contain("Municipal").And.Contain("available for own-use"));
            Assert.That(municipalDetails, Does.Not.Contain("purchasable now").And.Not.Contain("funds short"));
            AssertUnchanged(before, saved);

            Click("closeTradeButton");
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            var committed = controller.Session.Snapshot;
            var bakery = committed.PrivateFacilities.Values.Single(f => f.OwnerId == ownerId);
            Assert.That(bakery.BatchesProduced, Is.GreaterThan(0));
            Assert.That(bakery.WagesPaid, Is.GreaterThan(0));
            var observedBread = EconomicSignals.CommodityFlow(committed, controller.Session.Content, portId, "bread");
            Assert.That(observedBread.Consumption.Any(need => need.HasObservation), Is.True);
            Assert.That(observedBread.FirstDay, Is.EqualTo(committed.Day + 1));
            Assert.That(observedBread.LastDay, Is.EqualTo(committed.Day + 12));
            Click("openTradeButton");
            var committedSave = controller.Session.SaveText();
            foreach (var scope in new[] { 0, 1 })
            {
                root.Q<DropdownField>("harborFlowScope").index = scope;
                foreach (var good in controller.Session.Content.Goods)
                    AssertRow(EconomicSignals.CommodityFlow(committed, controller.Session.Content, portId,
                        good.Id, scope == 0 ? null : ownerId));
            }
            AssertUnchanged(committed, committedSave);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SelectedGoodComparisonUsesActualTownQuotesAndReturnsThroughModalBackPassively()
        {
            var originalPortId = controller.Session.Player.DockedPortId;
            Click("port_" + originalPortId);
            Click("modeEconomy");
            Click("economyHarborButton");
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Click("tradeGood_bread");
            root.Q<Foldout>("harborFlowDetails").value = true;
            Assert.That(root.Q<DropdownField>("goodDropdown").value,
                Is.EqualTo(controller.Session.Content.GetGood("bread").Name));
            var details = root.Q<Foldout>("harborFlowDetails");
            Assert.That(details.text + AllText(details), Does.Contain("Bread"));
            var bread = EconomicSignals.CommodityFlow(before, controller.Session.Content, originalPortId, "bread");
            AssertQuantity(details.Q<Label>("selectedFlowProduction"), bread.EnabledProductionQuantity);
            AssertQuantity(details.Q<Label>("selectedFlowUsage"), -bread.UsageQuantity);
            AssertQuantity(details.Q<Label>("selectedFlowNet"), bread.NetQuantity);
            root.Q<Foldout>("harborComparison").value = true;
            yield return null;
            yield return null;

            foreach (var port in before.Ports.Values)
            {
                var row = root.Q<Button>("compareTown_" + port.Id);
                Assert.That(row, Is.Not.Null, port.Id);
                var quote = before.GetMarketQuote(port.Id, "bread");
                Assert.That(row.Q<Label>("stock").text, Is.EqualTo(Q(port.Inventory["bread"])));
                Assert.That(row.Q<Label>("bid").text, Is.EqualTo(Price(quote.Bid)));
                Assert.That(row.Q<Label>("ask").text, Is.EqualTo(Price(quote.Ask)));
                Assert.That(row.Q<Label>("cash").text, Is.EqualTo(Money(port.MarketCash)));
                foreach (var label in row.Query<Label>().ToList()) AssertReadable(label, row);
            }

            var otherPortId = before.Ports.Keys.First(id => id != originalPortId);
            Click("compareTown_" + otherPortId);
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(otherPortId));
            Assert.That(root.Q<DropdownField>("goodDropdown").value, Is.EqualTo("Bread"));
            Assert.That(root.Q<Button>("tradeGood_bread").ClassListContains("selected-good"), Is.True);
            Assert.That(root.Q("mainLayout").enabledInHierarchy, Is.False, "Comparison must retain the input-blocking modal.");
            Assert.That(root.Q<Label>("tradeTitle").text, Does.Contain(before.Ports[otherPortId].Name));
            AssertRow(EconomicSignals.CommodityFlow(before, controller.Session.Content, otherPortId, "bread"));
            foreach (var id in new[] { "buyButton", "sellButton", "harborLoadButton", "harborUnloadButton" })
                Assert.That(root.Q<Button>(id).enabledSelf, Is.False, id + " cannot move remote ship cargo.");

            root.Q<TextField>("quantityField").Focus();
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(otherPortId));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Cancel();
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Map));
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectionalManualOrdersMatchExactPaidPreviewsAndNoCashTransfersWithoutAdvancingDays()
        {
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId);
            Click("openTradeButton");
            Click("tradeGood_grain");
            var initial = controller.Session.Snapshot;
            var facility = initial.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);

            ExecutePaidOrder(false, TradeDirection.Buy, 10000, facility.Id);
            ExecutePaidOrder(false, TradeDirection.Sell, 10000, facility.Id);
            ExecutePaidOrder(true, TradeDirection.Buy, 15000, facility.Id);
            ExecutePaidOrder(true, TradeDirection.Sell, 5000, facility.Id);
            root.Q<TextField>("quantityField").value = "0.5";
            Assert.That(root.Q<Button>("harborLoadButton").enabledSelf, Is.False,
                "Fractional market retail must not grant fractional ship orders.");
            ExecuteTransfer(TransferDirection.WarehouseToShip, facility.Id);
            ExecuteTransfer(TransferDirection.ShipToWarehouse, facility.Id);

            var after = controller.Session.Snapshot;
            Assert.That(after.Day, Is.EqualTo(initial.Day));
            Assert.That(after.TotalCash, Is.EqualTo(initial.TotalCash));
            Assert.That(after.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(10000));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.Zero);
            Assert.That(after.Accounting.Companies[facility.OwnerId].FeesPaid,
                Is.GreaterThan(initial.Accounting.Companies[facility.OwnerId].FeesPaid));
            root.Q<DropdownField>("harborFlowScope").index = 1;
            AssertRow(EconomicSignals.CommodityFlow(after, controller.Session.Content, portId, "grain", facility.OwnerId));
            var saved = controller.Session.SaveText();
            root.Q<DropdownField>("harborFlowScope").index = 0;
            root.Q<Foldout>("harborFlowDetails").value = true;
            root.Q<Foldout>("harborComparison").value = true;
            AssertUnchanged(after, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator JournalSummariesKeepExactProgressAndRevealFullBlockersWithoutChangingTheRun()
        {
            controller.BeginRun(CurrentPlayTestRun.Commission(RunScenario.IndustrialCharter));
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            var progress = GrandCommission.Progress(before);
            Assert.That(progress.Goals.SelectMany(goal => goal.Conditions).Any(condition =>
                condition.Semantic == CommissionSemantic.Current), Is.True);
            Assert.That(progress.Goals.SelectMany(goal => goal.Conditions).Any(condition =>
                condition.Semantic == CommissionSemantic.Cumulative), Is.True);
            Click("modeObjectives");
            yield return null;
            yield return null;

            foreach (var goal in progress.Goals)
            {
                var card = root.Q("objectiveCards").Q("objective_" + goal.Id);
                var details = card.Q<Foldout>("objectiveDetails_" + goal.Id);
                Assert.That(details, Is.Not.Null, goal.Id);
                Assert.That(details.value, Is.False, "Full explanations should start disclosed only on demand.");
                var bar = card.Q<ProgressBar>("objectiveBar_" + goal.Id);
                Assert.That(bar.value, Is.EqualTo(goal.Percent));
                var title = bar.Q<Label>(className: "unity-progress-bar__title");
                AssertReadable(title, bar.Q(className: "unity-progress-bar__background"));
                AssertReadable(title, bar.Q(className: "unity-progress-bar__progress"));
                foreach (var condition in goal.Conditions)
                {
                    var summary = card.Q<Label>("objectiveCondition_" + condition.Id);
                    Assert.That(summary.text, Does.Contain(condition.CurrentText + " / " + condition.TargetText)
                        .And.Contain(condition.Semantic == CommissionSemantic.Cumulative ? "Cumulative" : "Current"));
                    Assert.That(summary.tooltip, Does.Contain(condition.Label).And.Contain(condition.Reason));
                }

                Submit(details.Q<Toggle>());
                Assert.That(details.value, Is.True, "Keyboard submit opens the full explanation.");
                var expandedText = AllText(details);
                Assert.That(expandedText, Does.Contain(goal.Explanation));
                foreach (var condition in goal.Conditions)
                    Assert.That(expandedText, Does.Contain(condition.Label).And.Contain(condition.Reason));
                AssertUnchanged(before, saved);
                Submit(details.Q<Toggle>());
                Assert.That(details.value, Is.False, "Keyboard submit collapses the explanation again.");
            }

            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(portId));
            Assert.That(controller.Session.IsPlaying, Is.False);
            AssertUnchanged(before, saved);
            yield return null;
        }

        private void ExecutePaidOrder(bool warehouse, TradeDirection direction, int quantity, string facilityId)
        {
            root.Q<DropdownField>("harborTradeTarget").index = warehouse ? 1 : 0;
            root.Q<TextField>("quantityField").value = Q(quantity);
            var before = controller.Session.Snapshot;
            var ownerId = controller.Session.Player.OwnerId;
            var portId = controller.Session.SelectedPortId;
            var command = new TradeCommand(direction, "grain", quantity);
            var preview = warehouse ? before.PreviewWarehouseTrade(ownerId, facilityId, command) :
                before.PreviewTrade(controller.Session.Player.Id, portId, command);
            Assert.That(preview.Outcome, Is.EqualTo(TradeOutcome.Traded));
            var prefix = direction == TradeDirection.Buy ? "buy" : "sell";
            Assert.That(root.Q<Label>(prefix + "PreviewLabel").text, Does.Contain(Money(preview.TotalAmount)));
            Click(prefix + "Button");
            var after = controller.Session.Snapshot;
            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(after.Day, Is.EqualTo(before.Day));
            Assert.That(after.Companies[ownerId].Cash, Is.EqualTo(preview.TraderCashChange.After));
            Assert.That(after.Ports[portId].MarketCash, Is.EqualTo(preview.MarketCashChange.After));
            Assert.That(after.Ports[portId].City.HouseholdCash,
                Is.EqualTo(before.Ports[portId].City.HouseholdCash + preview.HouseholdServiceFee));
            Assert.That(after.Ports[portId].Inventory["grain"], Is.EqualTo(preview.MarketInventoryChange.After));
            Assert.That(warehouse ? after.GetFacilityStorage(facilityId).Inventory["grain"] : controller.Session.Player.Cargo["grain"],
                Is.EqualTo(preview.TraderInventoryChange.After));
            Assert.That(after.TotalCash, Is.EqualTo(before.TotalCash));
            AssertRow(EconomicSignals.CommodityFlow(after, controller.Session.Content, portId, "grain"));
        }

        private void ExecuteTransfer(TransferDirection direction, string facilityId)
        {
            root.Q<TextField>("quantityField").value = "1";
            var before = controller.Session.Snapshot;
            var shipId = controller.Session.Player.Id;
            var ownerId = controller.Session.Player.OwnerId;
            var portId = controller.Session.SelectedPortId;
            var preview = before.PreviewTransferGoods(ownerId, shipId, facilityId, direction, "grain", 10000);
            Assert.That(preview.Outcome, Is.EqualTo(TransferOutcome.Transferred));
            var loading = direction == TransferDirection.WarehouseToShip;
            Click(loading ? "harborLoadButton" : "harborUnloadButton");
            var after = controller.Session.Snapshot;
            var cargoDelta = loading ? preview.TransferredQuantity : -preview.TransferredQuantity;
            Assert.That(after.Ships[shipId].Cargo["grain"], Is.EqualTo(before.Ships[shipId].Cargo["grain"] + cargoDelta));
            Assert.That(after.GetFacilityStorage(facilityId).Inventory["grain"],
                Is.EqualTo(before.GetFacilityStorage(facilityId).Inventory["grain"] - cargoDelta));
            Assert.That(after.Companies[ownerId].Cash, Is.EqualTo(before.Companies[ownerId].Cash));
            Assert.That(after.Ports[portId].Inventory, Is.EquivalentTo(before.Ports[portId].Inventory));
            Assert.That(after.Ports[portId].MarketCash, Is.EqualTo(before.Ports[portId].MarketCash));
            Assert.That(after.Ports[portId].City.HouseholdCash, Is.EqualTo(before.Ports[portId].City.HouseholdCash));
            Assert.That(after.Accounting.Companies[ownerId].HeldInventoryCost,
                Is.EqualTo(before.Accounting.Companies[ownerId].HeldInventoryCost));
            Assert.That(after.Day, Is.EqualTo(before.Day));
        }

        private void AssertRow(CommodityFlowSignal signal)
        {
            var row = root.Q<Button>("tradeGood_" + signal.GoodId);
            Assert.That(row, Is.Not.Null, signal.GoodId);
            Assert.That(row.Q<Label>("town").text, Is.EqualTo(Q(signal.CurrentStock)));
            AssertQuantity(row.Q<Label>("production"), signal.EnabledProductionQuantity);
            AssertQuantity(row.Q<Label>("usage"), -signal.UsageQuantity);
            AssertQuantity(row.Q<Label>("net"), signal.NetQuantity);
            var quote = controller.Session.Snapshot.GetMarketQuote(signal.PortId, signal.GoodId);
            Assert.That(row.Q<Label>("bid").text, Is.EqualTo(Price(quote.Bid)));
            Assert.That(row.Q<Label>("ask").text, Is.EqualTo(Price(quote.Ask)));
        }

        private void AssertQuantity(Label cell, long subunits)
        {
            Assert.That(cell, Is.Not.Null);
            if (subunits == 0 && cell.text == "—") return;
            var numeric = Regex.Match(cell.text.Replace('−', '-'), @"^[+-]?\d+(?:\.\d+)?");
            Assert.That(numeric.Success, Is.True, cell.name + ": " + cell.text);
            Assert.That(decimal.Parse(numeric.Value, CultureInfo.InvariantCulture),
                Is.EqualTo((decimal)subunits / controller.Session.Snapshot.QuantityScale), cell.name);
        }

        private void AssertUnchanged(RegionalWorldSnapshot before, string saved)
        {
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
        }

        private static void AssertReadable(Label label, VisualElement background)
        {
            Assert.That(label, Is.Not.Null);
            Assert.That(background, Is.Not.Null);
            var ink = label.resolvedStyle.color;
            var paper = background.resolvedStyle.backgroundColor;
            Assert.That(ink.a, Is.GreaterThanOrEqualTo(0.99f), label.name + " needs opaque text for this contrast check.");
            Assert.That(paper.a, Is.GreaterThanOrEqualTo(0.99f), background.name + " needs an actual opaque background.");
            var foreground = Luminance(ink);
            var behind = Luminance(paper);
            var contrast = (Mathf.Max(foreground, behind) + 0.05f) / (Mathf.Min(foreground, behind) + 0.05f);
            Assert.That(contrast, Is.GreaterThanOrEqualTo(4.5f),
                label.text + " must remain legible against " + background.name + " (" + ink + " / " + paper + ").");
        }

        private static float Luminance(Color color) =>
            0.2126f * LinearChannel(color.r) + 0.7152f * LinearChannel(color.g) + 0.0722f * LinearChannel(color.b);

        private static float LinearChannel(float channel) => channel <= 0.04045f ? channel / 12.92f :
            Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        private string Q(long value) => ((decimal)value / controller.Session.Snapshot.QuantityScale).ToString("0.####", CultureInfo.InvariantCulture);
        private string Money(long value) => ((decimal)value / controller.Session.Snapshot.MoneyScale).ToString("0.####", CultureInfo.InvariantCulture);
        private static string Price(long? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "—";
        private static string AllText(VisualElement element) => string.Join("\n", element.Query<Label>().ToList().Select(label => label.text));

        private void Click(string id)
        {
            var button = root.Q<Button>(id);
            Assert.That(button, Is.Not.Null, id);
            Assert.That(button.enabledInHierarchy, Is.True, id);
            for (var parent = (VisualElement)button; parent != null; parent = parent.parent)
                Assert.That(parent.ClassListContains("hidden"), Is.False, id + " is hidden by " + parent.name);
            Submit(button);
        }

        private static void Submit(VisualElement element)
        {
            Assert.That(element, Is.Not.Null);
            element.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = element; element.SendEvent(evt); }
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled())
            {
                evt.target = root.focusController.focusedElement as VisualElement ?? root;
                ((VisualElement)evt.target).SendEvent(evt);
            }
        }
    }
}
