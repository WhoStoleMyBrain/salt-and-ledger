using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class CommandDeckExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            saveDirectory = Path.Combine(Path.GetTempPath(), "SaltAndLedger-CommandDeck-" + Guid.NewGuid().ToString("N"));
            typeof(ArchipelagoLabController).GetField("saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new LocalRunSaveStore(saveDirectory));
        }

        [TearDown]
        public void RemoveOwnTestSlots()
        {
            if (saveDirectory != null && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator InitialMapChooserSelectionCollapseAndOverlayDoNotMutateTheWorld()
        {
            controller.BeginRun(Modern());
            yield return null;
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Assert.That(controller.Presentation.HasSelection, Is.False);
            Assert.That(root.Q("deckBody").ClassListContains("hidden"), Is.True);
            Assert.That(controller.Map.VisibleRouteCount, Is.Zero);
            Assert.That(root.Q<Button>("deckCollapseButton").enabledSelf, Is.False);
            Click("modeTrade");
            Assert.That(root.Q("commandChooser").ClassListContains("hidden"), Is.False);
            Assert.That(root.Q<Button>("modeTrade").ClassListContains("sl-selected"), Is.True);
            var port = before.Ports[controller.Session.Player.DockedPortId];
            Click("chooseCommand_" + port.Id);
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(root.Q<Label>("tradeTitle").text, Does.Contain(port.Name));
            Click("closeTradeButton");
            Assert.That(controller.Presentation.HasSelection, Is.True);
            Assert.That(root.Q("deckBody").ClassListContains("hidden"), Is.False);
            Click("deckCollapseButton");
            Assert.That(root.Q("deckBody").ClassListContains("hidden"), Is.True);
            Click("deckCollapseButton");
            Assert.That(root.Q("deckBody").ClassListContains("hidden"), Is.False);
            root.Q<Toggle>("mapRoutesToggle").value = true;
            Assert.That(controller.Map.VisibleRouteCount, Is.EqualTo(6));
            root.Q<Toggle>("mapRoutesToggle").value = false;
            Assert.That(controller.Map.VisibleRouteCount, Is.EqualTo(3));
            AssertUnchanged(before, saved);
        }

        [UnityTest]
        public IEnumerator TownEconomyBindsExactSignalsAndHarborIndustryAndDetailReturnsPreserveContext()
        {
            controller.BeginRun(Modern(RunScenario.IndustrialCharter));
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            var need = EconomicSignals.NextEssentialNeed(before, portId);
            Assert.That(root.Q<Label>("deckFact0").text, Does.Contain("D" + need.NextDueDay)
                .And.Contain(Q(need.MarketStock)).And.Contain(Q(need.DesiredQuantity)));
            Click("cityEconomyButton");
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Assert.That(root.Q<Label>("economyFoodFact").text, Does.Contain(Q(need.MarketStock) + " stock / " + Q(need.DesiredQuantity) + " need"));
            Assert.That(root.Q<Label>("economyCashFact").text, Does.Contain(Money(before.Ports[portId].MarketCash))
                .And.Contain(Money(before.Ports[portId].City.HouseholdCash)));
            Assert.That(root.Q<Label>("economyNeedDetail").text, Does.Contain("Awaiting first need")
                .And.Contain(Q(need.AffordableQuantity.Value)).And.Contain(Money(need.CurrentPayment.Value))
                .And.Contain(need.DemandRemainder + " → " + need.NextDemandRemainder));
            foreach (var facility in before.Ports[portId].Facilities)
                AssertProductionCard(EconomicSignals.PublicProduction(before, controller.Session.Content, portId, facility.Configuration.Instance.Id));
            foreach (var facility in before.PrivateFacilities.Values.Where(f => f.PortId == portId && f.OwnerId == controller.Session.Player.OwnerId))
                AssertProductionCard(EconomicSignals.PrivateProduction(before, controller.Session.Content, facility.Id));
            Click("economyDetailsButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.CityEconomy));
            Click("cityEconomyBackButton");
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Click("economyHarborButton");
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(root.Q("commandModeNav").enabledInHierarchy, Is.False);
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q<Button>("economyHarborButton")));
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Click("modeIndustry");
            Assert.That(controller.IndustryIsOpen, Is.True);
            Cancel();
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            Cancel();
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Map));
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(portId));
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProductionCardModalReturnsToRebuiltSourceAndFallsBackWhenSourceIsGone()
        {
            controller.BeginRun(Modern(RunScenario.IndustrialCharter));
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.First(f => f.OwnerId == controller.Session.Player.OwnerId);
            Click("port_" + facility.PortId); Click("modeEconomy");
            var sourceName = "productionAct_" + facility.Id;
            var originalSource = root.Q<Button>(sourceName);
            Click(sourceName);
            root.Q<Foldout>("industryAdvancedFoldout").value = true;
            root.Q<Foldout>("industryDetailsFoldout").value = true;
            root.Q<Toggle>("facilityEnabled").value = !facility.Enabled;
            var before = controller.Session.Snapshot;
            Click("applyIndustryPolicy");
            Assert.That(controller.Session.Snapshot, Is.Not.SameAs(before));
            Assert.That(controller.Session.Snapshot.PrivateFacilities[facility.Id].Enabled, Is.EqualTo(!facility.Enabled));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(originalSource.panel, Is.Null, "The changed snapshot rebuilds the Economy card behind the modal.");
            Cancel();
            var rebuiltSource = root.Q<Button>(sourceName);
            Assert.That(rebuiltSource, Is.Not.SameAs(originalSource));
            Assert.That(root.focusController.focusedElement, Is.SameAs(rebuiltSource));

            var publicFacility = controller.Session.Snapshot.Ports[facility.PortId].Facilities.First();
            var publicName = "productionAct_" + publicFacility.Configuration.Instance.Id;
            var originalPublicSource = root.Q<Button>(publicName);
            Click(publicName);
            root.Q<DropdownField>("harborTradeTarget").value = "Company warehouse";
            root.Q<DropdownField>("goodDropdown").value = "Grain";
            root.Q<TextField>("quantityField").value = "1";
            Click("buyButton");
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(10000));
            Assert.That(originalPublicSource.panel, Is.Null);
            Cancel();
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q<Button>(publicName)));

            var committed = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Click(sourceName);
            root.Q<Button>(sourceName).RemoveFromHierarchy();
            Cancel();
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q<Button>("modeEconomy")),
                "An absent source falls back to a visible control in the same mode.");
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Economy));
            AssertUnchanged(committed, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NoticesEscapeClosesOnlyNoticesAndKeepsTheCurrentWorkspace()
        {
            controller.BeginRun(Modern());
            Click("ship_" + controller.Session.Player.Id);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            foreach (var mode in new[] { CommandMode.Fleet, CommandMode.Economy })
            {
                Click("mode" + mode); Click("noticesButton");
                Assert.That(root.Q("commandNotices").ClassListContains("hidden"), Is.False);
                Cancel();
                Assert.That(root.Q("commandNotices").ClassListContains("hidden"), Is.True);
                Assert.That(controller.Presentation.Mode, Is.EqualTo(mode));
                Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q<Button>("noticesButton")));
                Assert.That(controller.Session.IsPlaying, Is.False);
            }
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealHarborPurchaseFundsPooledMillAndProductionSignalsTrackCommittedBatches()
        {
            controller.BeginRun(Modern(RunScenario.IndustrialCharter));
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId); Click("modeIndustry");
            root.Q<DropdownField>("blueprintDropdown").value = "Mill";
            Click("constructButton");
            var mill = controller.Session.Snapshot.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "mill");
            var blocked = EconomicSignals.PrivateProduction(controller.Session.Snapshot, controller.Session.Content, mill.Id);
            Assert.That(blocked.CurrentReadiness, Is.EqualTo(ScheduledProductionOutcome.InputShortage));
            Click("industryHarborButton");
            root.Q<DropdownField>("harborTradeTarget").value = "Company warehouse";
            root.Q<DropdownField>("goodDropdown").value = "Grain";
            root.Q<TextField>("quantityField").value = "2";
            var before = controller.Session.Snapshot;
            var preview = before.PreviewWarehouseTrade(mill.OwnerId, mill.Id, new TradeCommand(TradeDirection.Buy, "grain", 20000));
            Assert.That(preview.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Click("buyButton");
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(preview.TraderCashChange.After));
            Assert.That(controller.Session.Snapshot.GetFacilityStorage(mill.Id).Inventory["grain"], Is.EqualTo(20000));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Click("closeTradeButton"); Click("modeEconomy");
            var ready = EconomicSignals.PrivateProduction(controller.Session.Snapshot, controller.Session.Content, mill.Id);
            Assert.That(ready.CurrentReadiness, Is.EqualTo(ScheduledProductionOutcome.Produced));
            AssertProductionCard(ready);
            Assert.That(AllText(root.Q("productionSignal_" + mill.Id)), Does.Contain("Ready from current state")
                .And.Contain("Shared inputs may be used by another local facility"));
            Click("modeMap");
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            var after = controller.Session.Snapshot;
            Assert.That(after.PrivateFacilities[mill.Id].BatchesProduced, Is.EqualTo(1));
            var bakery = after.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "bakery");
            Assert.That(bakery.BatchesProduced, Is.EqualTo(2));
            Assert.That(after.PrivateFacilities[mill.Id].WagesPaid, Is.GreaterThan(0));
            Assert.That(after.GetFacilityStorage(mill.Id), Is.SameAs(after.GetFacilityStorage(bakery.Id)));
            Click("modeEconomy");
            AssertProductionCard(EconomicSignals.PrivateProduction(after, controller.Session.Content, mill.Id));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ModernLocalStockHarborKeepsBothOrdersAndExactFeePreviewInTheInitialDefaultViewport()
        {
            root.style.flexGrow = 0; root.style.flexShrink = 0;
            root.style.width = 1366; root.style.height = 768;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            var draft = GrandCommissionDraft.Generate(99);
            var commission = draft.Resolve(draft.Rounds.Select(round => round.Offers[0].TemplateId));
            controller.BeginRun(new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter,
                RunWorldMode.ExpandingEconomy, 99, commission: commission, companyStorageRules: CompanyStorageRules.Pooled,
                itineraryVersion: 1, economicRulesVersion: 1));
            var portId = LivingCityGeneration.Generate(99).ProcessingPortId;
            Click("port_" + portId); Click("modeIndustry");
            root.Q<DropdownField>("blueprintDropdown").value = "Mill";
            Click("constructButton");
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "mill");
            var preparation = controller.Session.Snapshot;
            var purchase = preparation.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                new TradeCommand(TradeDirection.Buy, "grain", 10000));
            Assert.That(purchase.Outcome, Is.EqualTo(TradeOutcome.Traded));
            controller.Session.WarehouseTrade(facility.Id, TradeDirection.Buy, "grain", 10000, preparation);
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(purchase.TraderCashChange.After));
            Click("closeIndustryButton"); Click("openTradeButton");
            root.Q<DropdownField>("harborTradeTarget").value = "Company warehouse";
            root.Q<DropdownField>("goodDropdown").value = "Grain";
            root.Q<TextField>("quantityField").value = "1";
            yield return null; yield return null;

            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Assert.That(saved, Does.Contain("\"SchemaVersion\":5"));
            var buy = before.PreviewWarehouseTrade(facility.OwnerId, facility.Id, new TradeCommand(TradeDirection.Buy, "grain", 10000));
            var sell = before.PreviewWarehouseTrade(facility.OwnerId, facility.Id, new TradeCommand(TradeDirection.Sell, "grain", 10000));
            Assert.That(buy.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(sell.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(sell.ServiceFee, Is.GreaterThan(0), "The witness must include the extra v3 service-fee lines.");
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("Pay " + Money(buy.TotalAmount)));
            Assert.That(root.Q<Label>("sellPreviewLabel").text, Does.Contain("Gross " + Money(sell.TotalAmount))
                .And.Contain("Fee " + Money(sell.ServiceFee))
                .And.Contain("Net " + Money(sell.TraderCashChange.Delta)));
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.True);
            Assert.That(root.Q<Button>("sellButton").enabledInHierarchy, Is.True);
            var scroll = root.Q<ScrollView>("tradeContentScroll");
            Assert.That(scroll.scrollOffset, Is.EqualTo(Vector2.zero), "Both orders must fit on first opening, before any scrolling.");
            var viewport = scroll.contentViewport.worldBound;
            foreach (var id in new[] { "goodDropdown", "harborTradeTarget", "quantityField", "buyPreviewLabel", "sellPreviewLabel", "buyButton", "sellButton" })
            {
                var element = root.Q(id);
                var bounds = element.worldBound;
                Assert.That(element.resolvedStyle.fontSize, Is.EqualTo(17).Within(0.1f), id + " must retain Default body text size.");
                Assert.That(bounds.width, Is.GreaterThan(0), id);
                Assert.That(bounds.height, Is.GreaterThan(0), id);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(Math.Max(root.worldBound.xMin, viewport.xMin) - 1), id);
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(Math.Max(root.worldBound.yMin, viewport.yMin) - 1), id);
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(Math.Min(root.worldBound.xMax, viewport.xMax) + 1), id);
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(Math.Min(root.worldBound.yMax, viewport.yMax) + 1), id);
            }
            root.Q<Foldout>("harborCalculations").value = true;
            Assert.That(root.Q<Label>("buyCalculation").text, Does.Contain("exact total " + Money(buy.TotalAmount))
                .And.Contain("average " + ArchipelagoLabController.AverageExecutionPrice(buy)));
            Assert.That(root.Q<Label>("sellCalculation").text, Does.Contain("exact total " + Money(sell.TotalAmount))
                .And.Contain("average " + ArchipelagoLabController.AverageExecutionPrice(sell))
                .And.Contain("Service fee " + Money(sell.ServiceFee))
                .And.Contain("net receipts " + Money(sell.TraderCashChange.Delta))
                .And.Contain("Fee to market " + Money(sell.MarketServiceFee))
                .And.Contain("local workers " + Money(sell.HouseholdServiceFee)));
            Cancel();
            AssertUnchanged(before, saved);
        }

        [UnityTest]
        public IEnumerator FleetPreservesTheChosenSecondShipThroughPlannerHarborAndMapAndNpcIsReadOnly()
        {
            controller.BeginRun(Modern());
            controller.Session.SelectPort(controller.Session.Snapshot.Economy.Configuration.ShipwrightPortId);
            Assert.That(controller.Session.CommissionShip().Development.Single().Outcome, Is.EqualTo(DevelopmentOutcome.Started));
            for (var day = 0; day < 4; day++) controller.Session.NextDay();
            Click("modeFleet");
            var second = controller.Session.Snapshot.Ships.Values.Single(s => s.IsPlayer && s.Id != WorldIds.PlayerShipId);
            Click("fleetSelect_" + second.Id);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Assert.That(root.Q<Label>("fleetCommandTitle").text, Is.EqualTo(second.Name));
            Assert.That(root.Q<Label>("fleetCommandLocation").text, Does.Contain(before.Ports[second.DockedPortId].Name));
            Click("fleetEditRouteButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(controller.Session.Player.Id, Is.EqualTo(second.Id));
            Click("itineraryHarborButton");
            Assert.That(controller.TradeIsOpen, Is.True);
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(controller.Session.Player.Id, Is.EqualTo(second.Id));
            Assert.That(root.focusController.focusedElement, Is.SameAs(root.Q<Button>("itineraryHarborButton")));
            Click("itineraryBackButton");
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Fleet));
            Assert.That(controller.Session.SelectedShipId, Is.EqualTo(second.Id));
            Click("fleetHarborButton");
            Assert.That(controller.TradeIsOpen, Is.True);
            Cancel(); Click("fleetBackButton");
            Assert.That(controller.Presentation.Mode, Is.EqualTo(CommandMode.Map));
            Assert.That(controller.Session.SelectedShipId, Is.EqualTo(second.Id));
            var npc = before.Ships.Values.First(s => !s.IsPlayer && s.DockedPortId != null);
            Click("ship_" + npc.Id);
            Assert.That(root.Q("deckRouteButton").ClassListContains("hidden"), Is.True);
            Assert.That(root.Q("deckFleetButton").ClassListContains("hidden"), Is.True);
            Assert.That(controller.Session.Player.Id, Is.EqualTo(second.Id), "Inspecting an NPC must not replace the controlled ship.");
            Click("openTradeButton");
            Assert.That(root.Q<Label>("tradeAvailabilityLabel").text, Does.Contain("NPC inspection only"));
            foreach (var id in new[] { "buyButton", "sellButton", "harborLoadButton", "harborUnloadButton" })
                Assert.That(root.Q<Button>(id).enabledSelf, Is.False, id);
            Cancel();
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FleetShowsActualItineraryWaitCursorAndSelectedRouteWithoutExecutingIt()
        {
            controller.BeginRun(Modern());
            var first = controller.Session.Player.DockedPortId;
            var other = controller.Session.Snapshot.Ports.Keys.First(id => id != first);
            controller.Session.ApplyItinerary(new CargoItineraryPolicy("command_deck_wait", new[]
            {
                new CargoItineraryStop(first, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket,
                    "grain", 10000, shortagePolicy: CargoItineraryShortagePolicy.Wait, marginalPriceLimit: 1) }),
                new CargoItineraryStop(other, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", 10000) })
            }));
            controller.Session.SetAutomationEnabled(true);
            Click("ship_" + controller.Session.Player.Id); Click("modeFleet");
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            var decision = CargoItineraryEvaluator.Evaluate(before, controller.Session.Player.Id);
            Assert.That(decision.Kind, Is.EqualTo(CargoItineraryDecisionKind.Wait));
            Assert.That(root.Q<Label>("fleetCommandNext").text, Does.Contain(decision.Reason).And.Contain("Grain"));
            Assert.That(root.Q<Label>("fleetCommandLast").text, Does.Contain("stop 1, order 1").And.Contain("0/8"));
            Assert.That(controller.Map.HighlightedRoutePortIds, Is.EqualTo(new[] { first, other }));
            Assert.That(controller.Map.VisibleRouteCount, Is.EqualTo(1));
            root.Q<Toggle>("mapRoutesToggle").value = true;
            root.Q<Toggle>("mapRoutesToggle").value = false;
            Click("fleetEditRouteButton"); Cancel();
            Assert.That(controller.Session.Player.AutomationEnabled, Is.True);
            AssertUnchanged(before, saved);
            yield return null;
        }


        [UnityTest]
        public IEnumerator MidVoyageSchemaFiveSaveLoadsThroughSlotsAndInspectionRetainsExactBytes()
        {
            controller.BeginRun(Modern());
            var destination = controller.Session.Snapshot.Ports.Keys.Where(id => id != controller.Session.Player.DockedPortId)
                .OrderByDescending(id => controller.Session.Snapshot.TravelDays(controller.Session.Player.Id, id)).First();
            Click("ship_" + controller.Session.Player.Id); Click("port_" + destination); Click("sailButton");
            Click("nextDayButton");
            Assert.That(controller.Session.Player.DockedPortId, Is.Null);
            var saved = controller.Session.SaveText();
            Click("menuButton"); Click("pauseSavesButton"); Click("saveSlot1"); Click("loadSlot1");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Presentation.HasSelection, Is.False);
            var before = controller.Session.Snapshot;
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            Click("modeFleet");
            Assert.That(root.Q<Label>("fleetCommandLocation").text, Does.Contain(before.Ports[destination].Name)
                .And.Contain(controller.Session.Player.RemainingTravelDays + " days"));
            Assert.That(root.Q<Button>("fleetHarborButton").enabledSelf, Is.False);
            Click("modeEconomy"); Click("modeMap");
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator JournalBindsAuthoritativeGoalsAndReturnsPausedToTheSameTown()
        {
            var draft = GrandCommissionDraft.Generate(99);
            var commission = draft.Resolve(draft.Rounds.Select(round => round.Offers[0].TemplateId));
            controller.BeginRun(new RunIdentity(GrandCommission.ChallengeId, RunScenario.MerchantCharter,
                RunWorldMode.ExpandingEconomy, 99, commission: commission, companyStorageRules: CompanyStorageRules.Pooled,
                itineraryVersion: 1, economicRulesVersion: 1));
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Click("playButton");
            Assert.That(controller.Session.IsPlaying, Is.True);
            Click("modeObjectives");
            Assert.That(controller.Session.IsPlaying, Is.False);
            var progress = GrandCommission.Progress(before);
            Assert.That(root.Q<Label>("objectivesOverview").text, Does.Contain(progress.CurrentlyMetCount + "/4"));
            foreach (var goal in progress.Goals)
                Assert.That(root.Q<ProgressBar>("objectiveBar_" + goal.Id).value, Is.EqualTo(goal.Percent));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(portId));
            AssertUnchanged(before, saved);
            yield return null;
        }

        private void AssertProductionCard(ProductionOpportunitySignal signal)
        {
            var card = root.Q("productionSignal_" + signal.FacilityId);
            Assert.That(card, Is.Not.Null, signal.FacilityId);
            var text = AllText(card);
            Assert.That(text, Does.Contain(controller.Session.Content.GetBuilding(signal.BuildingId).Name)
                .And.Contain("D" + signal.NextEligibleDay).And.Contain(Money(signal.EmployerCash)).And.Contain(Money(signal.WagePerBatch)));
            foreach (var input in signal.Inputs)
                Assert.That(text, Does.Contain(Q(input.Quantity) + " " + controller.Session.Content.GetGood(input.GoodId).Name)
                    .And.Contain("stock " + Q(input.CurrentStock)));
            foreach (var output in signal.Outputs)
                Assert.That(text, Does.Contain(Q(output.Quantity) + " " + controller.Session.Content.GetGood(output.GoodId).Name));
            Assert.That(text, Does.Contain("Last committed outcome").And.Contain("Would produce only if"));
        }

        private void AssertUnchanged(RegionalWorldSnapshot before, string saved)
        {
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
        }

        private static RunIdentity Modern(string charter = RunScenario.MerchantCharter) => new RunIdentity(null, charter,
            RunWorldMode.ExpandingEconomy, 42, companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1, economicRulesVersion: 1);

        private string Q(long value) => ((decimal)value / controller.Session.Snapshot.QuantityScale).ToString("0.####", CultureInfo.InvariantCulture);
        private string Money(long value) => ((decimal)value / controller.Session.Snapshot.MoneyScale).ToString("0.####", CultureInfo.InvariantCulture);
        private static string AllText(VisualElement element) => string.Join("\n", element.Query<Label>().ToList().Select(label => label.text));

        private void Click(string name)
        {
            var button = root.Q<Button>(name);
            Assert.That(button, Is.Not.Null, name);
            Assert.That(button.enabledInHierarchy, Is.True, name);
            for (var parent = (VisualElement)button; parent != null; parent = parent.parent)
                Assert.That(parent.ClassListContains("hidden"), Is.False, name + " is hidden by " + parent.name);
            button.Focus();
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
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
