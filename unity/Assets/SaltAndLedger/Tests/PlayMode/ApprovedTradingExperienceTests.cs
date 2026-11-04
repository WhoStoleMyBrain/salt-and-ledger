using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost.Tests
{
    public sealed class ApprovedTradingExperienceTests
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
        public IEnumerator PairedArrowsExecuteSixExactFlowsAndRetainFractionalStockAndFifoThroughSaveLoad()
        {
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId); Click("openTradeButton");
            var initial = controller.Session.Snapshot;
            var facility = initial.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);

            ExecutePaid(HarborPair.MarketShip, TradeDirection.Buy, 10000, facility.Id);
            ExecutePaid(HarborPair.MarketShip, TradeDirection.Sell, 10000, facility.Id);
            ExecutePaid(HarborPair.MarketWarehouse, TradeDirection.Buy, 15000, facility.Id);
            ExecutePaid(HarborPair.MarketWarehouse, TradeDirection.Sell, 5000, facility.Id);
            Click("pairWarehouseShip");
            Quantity("0.5");
            Assert.That(Arrow("grain", true).enabledSelf, Is.False);
            Assert.That(Arrow("grain", true).tooltip, Does.Contain("whole goods"));
            Quantity("1");
            ExecuteTransfer(true, facility.Id);
            ExecuteTransfer(false, facility.Id);

            var after = controller.Session.Snapshot;
            Assert.That(after.Day, Is.EqualTo(initial.Day));
            Assert.That(after.TotalCash, Is.EqualTo(initial.TotalCash));
            Assert.That(after.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(10000));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.Zero);
            Assert.That(after.Accounting.Companies[facility.OwnerId].FeesPaid,
                Is.GreaterThan(initial.Accounting.Companies[facility.OwnerId].FeesPaid));
            Click("closeTradeButton");
            var saved = controller.Session.SaveText();
            controller.Session.LoadSaveText(saved);
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved), "Save/load preserves exact paid lots and all accounts.");
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(initial.Day));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisplayedArrowRejectsAStaleSameDayProposalInsteadOfSilentlyRepricingIt()
        {
            controller.OpenTrade(controller.Session.Player.DockedPortId);
            Click("pairMarketShip"); Quantity("1");
            var displayed = controller.Session.Snapshot;
            var facility = displayed.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);
            Assert.That(Arrow("grain", true).enabledInHierarchy, Is.True);
            controller.Session.WarehouseTrade(facility.Id, TradeDirection.Buy, "grain", 5000, displayed);
            var current = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Assert.That(current.Day, Is.EqualTo(displayed.Day));
            Assert.That(current, Is.Not.SameAs(displayed));

            // No frame or UI edit refreshes the displayed order between the external commit and this click.
            Submit(Arrow("grain", true));
            Assert.That(root.Q<Label>("tradeStatusLabel").text, Does.Contain("stale"));
            AssertUnchanged(current, saved);
            Assert.That(controller.Session.Player.Cargo["grain"], Is.Zero);

            Quantity("2");
            var fresh = current.PreviewTrade(controller.Session.Player.Id, controller.Session.SelectedPortId,
                new TradeCommand(TradeDirection.Buy, "grain", 20000));
            Assert.That(fresh.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Submit(Arrow("grain", true));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(fresh.TraderCashChange.After));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(20000));
            yield return null;
        }

        [UnityTest]
        public IEnumerator IndustryManualTradeKeepsPairedArrowsAndExactOrderOnTheSameWarehouse()
        {
            var portId = controller.Session.Player.DockedPortId;
            Click("port_" + portId); Click("openIndustryButton");
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            var facility = before.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);
            Click("industryHarborButton");
            Assert.That(controller.IndustryIsOpen, Is.False);
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(controller.TradingPair, Is.EqualTo(HarborPair.MarketWarehouse));
            Assert.That(root.Q<DropdownField>("harborTradeTarget").index, Is.EqualTo(1));
            Good("grain"); Quantity("0.5");
            var preview = before.PreviewWarehouseTrade(facility.OwnerId, facility.Id,
                new TradeCommand(TradeDirection.Buy, "grain", 5000));
            Assert.That(preview.Outcome, Is.EqualTo(TradeOutcome.Traded));
            Assert.That(root.Q<Button>("buyButton").text, Does.Contain("Warehouse"));
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain(Money(preview.TotalAmount)));
            Assert.That(Arrow("grain", true).tooltip, Does.Contain("Market → Warehouse").And.Contain(Money(preview.TotalAmount)));
            AssertUnchanged(before, saved);

            Submit(Arrow("grain", true));
            var after = controller.Session.Snapshot;
            Assert.That(after.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(preview.TraderInventoryChange.After));
            Assert.That(controller.Session.Player.Cargo, Is.EquivalentTo(before.Ships[controller.Session.Player.Id].Cargo));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(preview.TraderCashChange.After));
            Assert.That(after.TotalCash, Is.EqualTo(before.TotalCash));
            Assert.That(after.Day, Is.EqualTo(before.Day));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlannerHarborNavigationOpensTheChosenMapOrEconomyWorkspace()
        {
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            foreach (var mode in new[] { CommandMode.Map, CommandMode.Economy })
            {
                Click("modeMap"); Click("ship_" + controller.Session.Player.Id); Click("deckRouteButton");
                Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
                root.Q<Foldout>("routePlannerTools").value = true;
                Click("itineraryHarborButton");
                Assert.That(controller.TradeIsOpen, Is.True);
                Click("mode" + mode);

                Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
                Assert.That(controller.Presentation.Mode, Is.EqualTo(mode));
                Assert.That(controller.TradeIsOpen, Is.False);
                Assert.That(controller.IndustryIsOpen, Is.False);
                Assert.That(root.Q("economyWorkspace").ClassListContains("hidden"), Is.EqualTo(mode != CommandMode.Economy));
                Assert.That(controller.Map.enabledSelf, Is.True);
                AssertUnchanged(before, saved);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BakeryPurchaseCanOpenPlannerAndReturnWithoutAHiddenFactoryModal()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            var blueprint = controller.Session.Snapshot.IndustryBlueprints.Values.Single(b => b.BuildingId == "bakery");
            var town = blueprint.EligiblePortIds.First();
            Click("port_" + town); Click("openIndustryButton"); Click("construct_" + blueprint.Id);
            Assert.That(controller.IndustryIsOpen, Is.True);
            Assert.That(root.Q("factoryInput_flour").ClassListContains("industry-v3-blocked"), Is.True);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Click("factoryShortagePurchase");
            Assert.That(controller.TradingPair, Is.EqualTo(HarborPair.MarketWarehouse));
            Assert.That(controller.IndustryIsOpen, Is.False);
            Click("harborRoutePlannerButton");

            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(controller.IndustryIsOpen, Is.False, "Opening the planner must not reopen its purchase's parent factory.");
            yield return null;
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.TradeIsOpen, Is.True, "Back returns to the Harbor that opened this planner.");
            Assert.That(controller.IndustryIsOpen, Is.False);
            Assert.That(controller.Session.SelectedPortId, Is.EqualTo(town));
            Click("closeTradeButton");
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(controller.IndustryIsOpen, Is.False, "The abandoned purchase return must not reopen on a later close.");
            Assert.That(controller.Map.enabledInHierarchy, Is.True);
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator NpcHarborDetailsDismissesTheInsetAndEscapeReturnsDirectlyToGameplay()
        {
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            var ownedShipId = controller.Session.Player.Id;
            var npc = before.Ships.Values.First(ship => !ship.IsPlayer && ship.DockedPortId != null);
            Click("ship_" + npc.Id); Click("openTradeButton");
            Assert.That(controller.TradeIsOpen, Is.True);
            Click("deckInspectButton");

            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Inspector));
            Assert.That(controller.TradeIsOpen, Is.False, "Secondary screens must not retain an invisible input-catching Harbor.");
            Assert.That(controller.IndustryIsOpen, Is.False);
            AssertUnchanged(before, saved);
            yield return null;
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay), "One Escape returns from Details.");
            Assert.That(controller.Session.SelectedShipId, Is.EqualTo(npc.Id));
            Assert.That(controller.Session.Player.Id, Is.EqualTo(ownedShipId));
            Assert.That(controller.Map.enabledInHierarchy, Is.True);
            AssertUnchanged(before, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FreshRunDismissesFactoryPurchaseAndPlannerHarborReturnContexts()
        {
            var identity = controller.Session.Identity;
            foreach (var fromFactory in new[] { true, false })
            {
                var before = controller.Session.Snapshot;
                if (fromFactory)
                {
                    var facility = before.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);
                    Click("port_" + facility.PortId); Click("openIndustryButton"); Click("manageFactory_" + facility.Id);
                    Click("factoryWarehouseButton");
                    Assert.That(controller.TradingPair, Is.EqualTo(HarborPair.MarketWarehouse));
                }
                else
                {
                    Click("ship_" + controller.Session.Player.Id); Click("deckRouteButton");
                    root.Q<Foldout>("routePlannerTools").value = true;
                    Click("itineraryHarborButton");
                }
                Assert.That(controller.TradeIsOpen, Is.True);
                Assert.That(controller.Session.Snapshot, Is.SameAs(before));
                controller.BeginRun(identity);

                Assert.That(controller.Session.Snapshot, Is.Not.SameAs(before));
                Assert.That(controller.Session.Snapshot.Day, Is.Zero);
                Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
                Assert.That(controller.TradeIsOpen, Is.False);
                Assert.That(controller.IndustryIsOpen, Is.False, "Replacing a run cannot reopen an old parent factory.");
                Assert.That(controller.Presentation.HasSelection, Is.False);
                Assert.That(controller.Session.IsPlaying, Is.False);
                Assert.That(controller.Map.enabledInHierarchy, Is.True);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TerminalResultsDismissFactoryPurchaseBeforeFrozenInspectionAndRetry()
        {
            controller.BeginRun(CurrentPlayTestRun.Commission(RunScenario.IndustrialCharter));
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);
            Click("port_" + facility.PortId); Click("openIndustryButton"); Click("manageFactory_" + facility.Id);
            Click("factoryWarehouseButton");
            Assert.That(controller.TradeIsOpen, Is.True);
            controller.Session.AbandonRun();
            var terminal = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Assert.That(terminal.Challenge.Status, Is.EqualTo(ChallengeStatus.Abandoned));

            // A normal bound-field refresh observes the terminal world without first dismissing its modal.
            Quantity("0.5");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(controller.IndustryIsOpen, Is.False, "Results must not follow the purchase's Back-to-factory path.");
            AssertUnchanged(terminal, saved);
            Click("resultsInspectButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.IndustryIsOpen, Is.False);
            Assert.That(controller.Map.enabledInHierarchy, Is.True);
            AssertUnchanged(terminal, saved);
            Click("menuButton"); Click("retryRunButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            Assert.That(controller.Session.IsChallengeEnded, Is.False);
            Assert.That(controller.TradeIsOpen, Is.False);
            Assert.That(controller.IndustryIsOpen, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CargoRequiresManualControlAndActualDockWhileCompanyStockRemainsIndependent()
        {
            var origin = controller.Session.Player.DockedPortId;
            var other = controller.Session.Snapshot.Ports.Keys.First(id => id != origin && controller.Session.HasRoute(origin, id));
            controller.Session.ApplyItinerary(new CargoItineraryPolicy("approved_manual_guard", new[]
            {
                new CargoItineraryStop(origin, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket,
                    "grain", 10000, shortagePolicy: CargoItineraryShortagePolicy.Wait, marginalPriceLimit: 1) }),
                new CargoItineraryStop(other, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", 10000) })
            }));
            controller.Session.SetAutomationEnabled(true);
            controller.OpenTrade(origin); Quantity("1");
            foreach (var pair in new[] { HarborPair.MarketShip, HarborPair.WarehouseShip })
            {
                Click("pair" + pair);
                AssertBlocked("grain", "automation");
            }
            var facility = controller.Session.Snapshot.PrivateFacilities.Values.Single(f => f.OwnerId == controller.Session.Player.OwnerId);
            ExecutePaid(HarborPair.MarketWarehouse, TradeDirection.Buy, 10000, facility.Id);
            Assert.That(controller.Session.Player.AutomationEnabled, Is.True);
            Good("grain");
            Click("harborDisableRoute");
            Assert.That(controller.Session.Player.AutomationEnabled, Is.False);
            Click("pairWarehouseShip");
            ExecuteTransfer(true, facility.Id);
            Click("closeTradeButton");

            controller.Session.SelectShip(controller.Session.Player.Id);
            controller.Session.SelectPort(other);
            controller.Session.Sail();
            controller.OpenTrade(origin);
            var sailing = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            foreach (var pair in new[] { HarborPair.MarketShip, HarborPair.WarehouseShip })
            {
                Click("pair" + pair);
                AssertBlocked("grain", "Dock at this town");
            }
            Click("pairMarketWarehouse");
            Assert.That(Arrow("grain", true).enabledInHierarchy, Is.True, "Owned local stock can trade while the ship is at sea.");
            AssertUnchanged(sailing, saved);
            Click("closeTradeButton");

            var controlledShip = controller.Session.Player.Id;
            var npc = sailing.Ships.Values.First(s => !s.IsPlayer && s.DockedPortId != null);
            controller.SelectCommandShip(npc.Id);
            controller.OpenTrade(npc.DockedPortId);
            foreach (var pair in new[] { HarborPair.MarketShip, HarborPair.WarehouseShip })
            {
                Click("pair" + pair);
                AssertBlocked("grain", "NPC inspection only");
            }
            Assert.That(controller.Session.Player.Id, Is.EqualTo(controlledShip));
            AssertUnchanged(sailing, saved);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FleetHarborArrowsTradeOnlyTheSelectedSecondShipsPhysicalHold()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            controller.Session.SelectPort(controller.Session.Snapshot.Economy.Configuration.ShipwrightPortId);
            Assert.That(controller.Session.CommissionShip().Development.Single().Outcome, Is.EqualTo(DevelopmentOutcome.Started));
            for (var day = 0; day < 4; day++) controller.Session.NextDay();
            Click("modeFleet");
            var second = controller.Session.Snapshot.Ships.Values.Single(s => s.IsPlayer && s.Id != WorldIds.PlayerShipId);
            Click("fleetSelect_" + second.Id); Click("fleetHarborButton"); Click("pairMarketShip"); Quantity("1");
            var before = controller.Session.Snapshot;
            var good = controller.Session.Content.Goods.First(g =>
                before.MaximumTradeQuantity(second.Id, second.DockedPortId, TradeDirection.Buy, g.Id) >= before.QuantityScale);
            var preview = before.PreviewTrade(second.Id, second.DockedPortId, new TradeCommand(TradeDirection.Buy, good.Id, before.QuantityScale));
            Submit(Arrow(good.Id, true));
            var after = controller.Session.Snapshot;
            Assert.That(controller.Session.Player.Id, Is.EqualTo(second.Id));
            Assert.That(after.Ships[second.Id].Cargo[good.Id], Is.EqualTo(preview.TraderInventoryChange.After));
            Assert.That(after.Ships[WorldIds.PlayerShipId].Cargo, Is.EquivalentTo(before.Ships[WorldIds.PlayerShipId].Cargo));
            Assert.That(after.Companies[second.OwnerId].Cash, Is.EqualTo(preview.TraderCashChange.After));
            Assert.That(after.Day, Is.EqualTo(before.Day));
            Assert.That(after.TotalCash, Is.EqualTo(before.TotalCash));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InspectorPairsDetailsAndTextScalesRemainPassiveAndTerminalArrowsStayDisabled()
        {
            controller.BeginRun(CurrentPlayTestRun.Commission(RunScenario.IndustrialCharter));
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Assert.That(controller.Presentation.HasSelection, Is.False);
            Assert.That(root.Q("commandDeck").ClassListContains("hidden"), Is.True);
            foreach (var width in new[] { 1366, 1920 })
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = width; root.style.height = width == 1366 ? 768 : 1080;
                foreach (var scale in new[] { ArchipelagoUiScale.Compact, ArchipelagoUiScale.Default,
                    ArchipelagoUiScale.Large, ArchipelagoUiScale.ExtraLarge })
                {
                    controller.TextScale.Apply(scale, false);
                    Click("port_" + controller.Session.Player.DockedPortId);
                    Assert.That(root.Q("approvedPublicIndustries"), Is.Not.Null);
                    Click("openTradeButton");
                    foreach (var pair in new[] { HarborPair.MarketShip, HarborPair.MarketWarehouse, HarborPair.WarehouseShip })
                    {
                        Click("pair" + pair);
                        Assert.That(controller.TradingPair, Is.EqualTo(pair));
                    }
                    Good("bread");
                    root.Q<Foldout>("harborComparison").value = true;
                    Assert.That(root.Q("compareTown_" + before.Ports.Keys.First()), Is.Not.Null);
                    Assert.That(controller.Map.enabledInHierarchy, Is.False);
                    yield return null;
                    Cancel();
                    Assert.That(controller.TradeIsOpen, Is.False);
                    Click("ship_" + controller.Session.Player.Id);
                    Assert.That(root.Q("approvedShipCargo"), Is.Not.Null);
                    Click("clearSelectionButton");
                    Assert.That(root.Q("commandDeck").ClassListContains("hidden"), Is.True);
                    AssertUnchanged(before, saved);
                }
            }

            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            controller.Session.AbandonRun();
            Click("modeMap");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Results));
            Click("resultsInspectButton");
            Click("port_" + controller.Session.Player.DockedPortId); Click("openTradeButton");
            var terminal = controller.Session.Snapshot;
            var terminalSave = controller.Session.SaveText();
            foreach (var pair in new[] { HarborPair.MarketShip, HarborPair.MarketWarehouse, HarborPair.WarehouseShip })
            {
                Click("pair" + pair);
                AssertBlocked("bread", "Run concluded");
            }
            AssertUnchanged(terminal, terminalSave);
            yield return null;
        }

        private void ExecutePaid(HarborPair pair, TradeDirection direction, int quantity, string facilityId)
        {
            Click("pair" + pair); Quantity(Q(quantity));
            var before = controller.Session.Snapshot;
            var ownerId = controller.Session.Player.OwnerId;
            var portId = controller.Session.SelectedPortId;
            var command = new TradeCommand(direction, "grain", quantity);
            var warehouse = pair == HarborPair.MarketWarehouse;
            var preview = warehouse ? before.PreviewWarehouseTrade(ownerId, facilityId, command) :
                before.PreviewTrade(controller.Session.Player.Id, portId, command);
            Assert.That(preview.Outcome, Is.EqualTo(TradeOutcome.Traded));
            var arrow = Arrow("grain", direction == TradeDirection.Buy);
            Assert.That(arrow.tooltip, Does.Contain(Money(preview.TotalAmount)));
            if (direction == TradeDirection.Sell)
                Assert.That(arrow.tooltip, Does.Contain("fee " + Money(preview.ServiceFee)).And.Contain("net " + Money(preview.TraderCashChange.Delta)));
            Submit(arrow);
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
        }

        private void ExecuteTransfer(bool loading, string facilityId)
        {
            Quantity("1");
            var before = controller.Session.Snapshot;
            var ownerId = controller.Session.Player.OwnerId;
            var shipId = controller.Session.Player.Id;
            var direction = loading ? TransferDirection.WarehouseToShip : TransferDirection.ShipToWarehouse;
            var preview = before.PreviewTransferGoods(ownerId, shipId, facilityId, direction, "grain", 10000);
            Assert.That(preview.Outcome, Is.EqualTo(TransferOutcome.Transferred));
            Assert.That(Arrow("grain", loading).tooltip, Does.Contain("no payment"));
            Submit(Arrow("grain", loading));
            var after = controller.Session.Snapshot;
            var cargoDelta = loading ? preview.TransferredQuantity : -preview.TransferredQuantity;
            Assert.That(after.Ships[shipId].Cargo["grain"], Is.EqualTo(before.Ships[shipId].Cargo["grain"] + cargoDelta));
            Assert.That(after.GetFacilityStorage(facilityId).Inventory["grain"],
                Is.EqualTo(before.GetFacilityStorage(facilityId).Inventory["grain"] - cargoDelta));
            Assert.That(after.Companies[ownerId].Cash, Is.EqualTo(before.Companies[ownerId].Cash));
            Assert.That(after.Accounting.Companies[ownerId].HeldInventoryCost, Is.EqualTo(before.Accounting.Companies[ownerId].HeldInventoryCost));
            foreach (var port in before.Ports.Values)
            {
                Assert.That(after.Ports[port.Id].Inventory, Is.EquivalentTo(port.Inventory));
                Assert.That(after.Ports[port.Id].MarketCash, Is.EqualTo(port.MarketCash));
                Assert.That(after.Ports[port.Id].City.HouseholdCash, Is.EqualTo(port.City.HouseholdCash));
            }
            Assert.That(after.Day, Is.EqualTo(before.Day));
        }

        private void AssertBlocked(string goodId, string reason)
        {
            foreach (var right in new[] { false, true })
            {
                Assert.That(Arrow(goodId, right).enabledSelf, Is.False);
                Assert.That(Arrow(goodId, right).tooltip, Does.Contain(reason));
            }
        }

        private void AssertUnchanged(RegionalWorldSnapshot before, string saved)
        {
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
        }

        private Button Arrow(string goodId, bool right) => root.Q("tradeGood_" + goodId).Q<Button>(right ? "rightArrow" : "leftArrow");
        private void Good(string goodId) => Submit(root.Q("tradeGood_" + goodId).Q<Button>("good"));
        private void Quantity(string value) => root.Q<TextField>("quantityField").value = value;
        private string Q(long value) => ((decimal)value / controller.Session.Snapshot.QuantityScale).ToString("0.####", CultureInfo.InvariantCulture);
        private string Money(long value) => ((decimal)value / controller.Session.Snapshot.MoneyScale).ToString("0.####", CultureInfo.InvariantCulture);
        private void Click(string id) => Submit(root.Q<Button>(id));

        private static void Submit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledInHierarchy, Is.True, button.name);
            for (var element = (VisualElement)button; element != null; element = element.parent)
                Assert.That(element.ClassListContains("hidden"), Is.False, button.name + " hidden by " + element.name);
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
