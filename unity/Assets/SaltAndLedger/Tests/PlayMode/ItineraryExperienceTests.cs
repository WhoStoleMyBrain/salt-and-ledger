using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SaltAndLedger.UnityHost.Tests
{
    /// <summary>Real V2 controls, real route execution; no hidden legacy editor controls.</summary>
    public sealed class ItineraryExperienceTests
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
            controller.BeginRun(new RunIdentity(null, RunScenario.IndependentCharter, RunWorldMode.ExpandingEconomy, 42,
                companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1));
        }

        [UnityTest]
        public IEnumerator ChartPlanningReorderingAndInlineCancellationArePassive()
        {
            var before = controller.Session.Snapshot; var saved = controller.Session.SaveText();
            controller.OpenRoutePlanner(controller.Session.Player.Id);
            var chart = root.Q<ArchipelagoMap>("routeCourseMap");
            Assert.That(chart.HighlightedRoutePortIds, Is.EqualTo(new[] { controller.Session.Player.DockedPortId }));
            Assert.That(root.Q<Button>("routeOrdersButton").enabledSelf, Is.False);
            foreach (var id in before.Ports.Keys.Where(id => id != controller.Session.Player.DockedPortId).Take(3))
                Click(chart.Q<Button>("port_" + id));
            Assert.That(chart.HighlightedRoutePortIds.Count, Is.EqualTo(4));
            Assert.That(root.Q<Button>("itineraryAddStop").enabledSelf, Is.False);
            Click("itineraryStopUp3"); Click("itineraryRemoveStop2");
            Assert.That(chart.HighlightedRoutePortIds.Count, Is.EqualTo(3));
            Click("routeOrdersButton"); Select(0);
            Click(Cell(0, "grain", CargoItineraryCommandKind.BuyFromMarket));
            Inline("itineraryMax").value = "4";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Cancel();
            Assert.That(root.Q<VisualElement>("routeInlineEditor"), Is.Null, "Escape cancels the local instruction buffer first.");
            Assert.That(root.Q<Button>(Cell(0, "grain", CargoItineraryCommandKind.BuyFromMarket)).text, Is.EqualTo("+"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CommodityCellsCreateDisposalFirstOrdersAndReviewSavesPaused()
        {
            OpenTwoStopDraft(); var before = controller.Session.Snapshot;
            AddOrder(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "2");
            AddOrder(0, "flour", CargoItineraryCommandKind.LoadFromCompanyTownStock, "2");
            AddOrder(0, "bread", CargoItineraryCommandKind.SellToMarket, "3");
            AddOrder(0, "logs", CargoItineraryCommandKind.UnloadToCompanyTownStock, "1");
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "4");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.Player.Itinerary, Is.Null);
            Apply();
            var commands = controller.Session.Player.Itinerary.Stops[0].Commands;
            Assert.That(commands.Select(command => command.Kind), Is.EqualTo(new[]
            {
                CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryCommandKind.SellToMarket,
                CargoItineraryCommandKind.LoadFromCompanyTownStock, CargoItineraryCommandKind.BuyFromMarket, CargoItineraryCommandKind.BuyFromMarket
            }));
            Assert.That(commands.Select(command => command.GoodId), Is.EqualTo(new[] { "logs", "bread", "flour", "grain", "logs" }));
            Assert.That(commands.Select(command => command.MaximumQuantity), Is.EqualTo(new[] { 10000, 30000, 20000, 40000, 20000 }));
            Assert.That(controller.Session.Player.AutomationEnabled, Is.False);
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Ships[controller.Session.Player.Id].Cash));
            Assert.That(controller.Session.Player.Cargo, Is.EqualTo(before.Ships[controller.Session.Player.Id].Cargo));
            Assert.That(root.Q<Button>("routeStartButton").ClassListContains("hidden"), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoPaidActionsThenWaitPreserveCursorAndRejectPartialReplacement()
        {
            OpenTwoStopDraft();
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(0, "silver", CargoItineraryCommandKind.SellToMarket, "2", wait: true);
            Click("itineraryActionDown0_0"); Click("itineraryActionDown0_1");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1");
            var before = controller.Session.Snapshot; Apply(); Click("routeStartButton"); Click("itineraryRunButton");
            var session = controller.Session; var waited = session.Snapshot;
            Assert.That(session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(2));
            Assert.That(session.Player.Cargo["grain"], Is.EqualTo(before.Ships[session.Player.Id].Cargo["grain"] + 10000));
            Assert.That(session.Player.Cargo["logs"], Is.EqualTo(before.Ships[session.Player.Id].Cargo["logs"] + 10000));
            Assert.That(root.Q<Label>("itineraryLiveStatus").text, Does.Contain("Waiting"));
            Click("itineraryRunButton");
            Assert.That(session.Player.Cargo, Is.EqualTo(waited.Ships[session.Player.Id].Cargo));
            Assert.That(session.Player.Cash, Is.EqualTo(waited.Ships[session.Player.Id].Cash));
            root.Q<Toggle>("itineraryManualToggle").value = true;
            var saved = session.SaveText(); session.LoadSaveText(saved);
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Assert.That(session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(2));
            controller.OpenRoutePlanner(session.Player.Id); Click("routeEditButton");
            AddOrder(0, "planks", CargoItineraryCommandKind.BuyFromMarket, "1");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("partial visit"));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            yield return null;
        }

        [UnityTest]
        public IEnumerator EightSkippedOrdersRetainDailyBudgetAcrossSaveAndEquivalentReapply()
        {
            OpenTwoStopDraft();
            for (var i = 0; i < 8; i++)
            {
                Select(0); root.Q<Foldout>("itineraryAdvanced0").value = true; Click("itineraryAddAction0");
                var action = root.Q<DropdownField>("itineraryKind0_-1"); action.value = action.choices[1];
                root.Q<DropdownField>("itineraryGood0_-1").value = controller.Session.Content.GetGood("silver").Name;
                Click("routeSaveOrder");
            }
            Assert.That(root.Q<Button>("itineraryAddAction0").enabledSelf, Is.False);
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1");
            Apply(); Click("routeStartButton"); Click("itineraryRunButton");
            var session = controller.Session;
            Assert.That(session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(8));
            Assert.That(session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(8));
            Assert.That(session.Player.DockedPortId, Is.Not.Null, "The ninth operation cannot depart today.");
            root.Q<Toggle>("itineraryManualToggle").value = true;
            var saved = session.SaveText(); session.LoadSaveText(saved); controller.OpenRoutePlanner(session.Player.Id);
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Click("routeEditButton"); Apply();
            Assert.That(session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(8));
            Click("routeEditButton"); Select(0); root.Q<Foldout>("itineraryAdvanced0").value = true;
            Click("routeEditOrder0_0"); Inline("itineraryMax").value = "3"; Click("routeSaveOrder");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("operation budget"));
            Click("itineraryNextDayButton");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True);
            Apply(); Assert.That(session.Player.ItineraryProgress.ActionIndex, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidInlineValuesAndRemoteQuotesArePassive()
        {
            OpenTwoStopDraft();
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1");
            var before = controller.Session.Snapshot; var saved = controller.Session.SaveText();
            Select(0); Click(Cell(0, "grain", CargoItineraryCommandKind.BuyFromMarket));
            Inline("itineraryMax").value = "1.5";
            Assert.That(root.Q<Button>("routeSaveOrder").enabledSelf, Is.False);
            Inline("itineraryMax").value = "2";
            root.Q<Toggle>("routeRequireMinimum").value = true;
            Inline("itineraryMin").value = "3";
            Assert.That(root.Q<Button>("routeSaveOrder").enabledSelf, Is.False, "Minimum cannot exceed maximum.");
            root.Q<Toggle>("routeRequireMinimum").value = false;
            Assert.That(Inline("itineraryMin").value, Is.EqualTo("1"));
            root.Q<Foldout>("routeTechnicalLimits").value = true;
            Inline("itineraryCashReserve").value = "10.00001";
            Assert.That(root.Q<Button>("routeSaveOrder").enabledSelf, Is.False);
            Inline("itineraryCashReserve").value = "10.0001"; Click("routeSaveOrder");
            Select(1); Click(Cell(1, "grain", CargoItineraryCommandKind.SellToMarket));
            root.Q<Foldout>("routeTechnicalLimits").value = true;
            Assert.That(root.Q<Label>("routeCurrentQuote").text, Does.Contain("REMOTE / HYPOTHETICAL").And.Contain("Town buying power"));
            Click("routeCancelOrder");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExistingDuplicateOrdersRetainIdentityAndAllExactLimits()
        {
            var session = controller.Session; var dock = session.Player.DockedPortId;
            var remote = session.Snapshot.Ports.Keys.First(port => port != dock);
            var policy = new CargoItineraryPolicy("custom_order", new[]
            {
                new CargoItineraryStop(dock, new[]
                {
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", 10000, 10000, CargoItineraryShortagePolicy.Wait, 99, 100001, 300002),
                    new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", 20000, marginalPriceLimit: 10),
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", 20000, 10000, CargoItineraryShortagePolicy.Skip, 100, 100002, 300003),
                    new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, "flour", 20000, warehouseReserve: 12345)
                }),
                new CargoItineraryStop(remote, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", 10000) })
            });
            session.ApplyItinerary(policy, session.Snapshot); var saved = session.SaveText();
            controller.OpenRoutePlanner(session.Player.Id); Click("routeEditButton"); Select(0);
            Assert.That(root.Q<Button>(Cell(0, "grain", CargoItineraryCommandKind.BuyFromMarket)).text, Is.EqualTo("Ordered…"));
            Click(Cell(0, "grain", CargoItineraryCommandKind.BuyFromMarket)); Click("routeEditOrder0_0");
            Assert.That(Inline("itineraryCashReserve").value, Is.EqualTo("10.0001"));
            Assert.That(Inline("itineraryBudget").value, Is.EqualTo("30.0002"));
            Click("routeCancelOrder"); Click("routeEditOrder0_3");
            Assert.That(Inline("itineraryWarehouseReserve").value, Is.EqualTo("1.2345"));
            Click("routeSaveOrder"); Apply();
            Assert.That(session.Player.Itinerary.EquivalentTo(policy), Is.True);
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Click("routeEditButton"); AddOrder(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "3"); Apply();
            Assert.That(session.Player.Itinerary.Id, Is.EqualTo("custom_order"));
            Assert.That(session.Player.Itinerary.Stops[0].Commands.Select(command => command.GoodId), Is.EqualTo(new[] { "grain", "grain", "grain", "flour", "logs" }));
            var prefix = new CargoItineraryPolicy(policy.Id, new[] { new CargoItineraryStop(dock, session.Player.Itinerary.Stops[0].Commands.Take(4)), policy.Stops[1] });
            Assert.That(prefix.EquivalentTo(policy), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FutureWarehouseWaitSurvivesLoadThenConstructionEnablesSameRoute()
        {
            OpenTwoStopDraft(); var session = controller.Session; var dock = session.Player.DockedPortId;
            AddOrder(0, "grain", CargoItineraryCommandKind.UnloadToCompanyTownStock, "2", wait: true);
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1");
            Assert.That(root.Q<Label>("itineraryStopWarning0").text, Does.Contain("Warehouse needed"));
            Apply(); var policy = session.Player.Itinerary;
            Assert.That(session.Snapshot.CompanyStockpiles.Count, Is.Zero);
            Click("routeStartButton"); Click("itineraryRunButton");
            Assert.That(session.Player.ItineraryProgress.LastReason, Is.EqualTo(CargoItineraryBlockReason.MissingCompanyStock));
            Assert.That(session.Player.ItineraryProgress.ActionIndex, Is.Zero);
            root.Q<Toggle>("itineraryManualToggle").value = true;
            var saved = session.SaveText(); session.LoadSaveText(saved);
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            var blueprint = session.Snapshot.IndustryBlueprints.Values.First(value => value.EligiblePortIds.Contains(dock));
            session.InspectTradePort(dock); session.Construct(blueprint.Id);
            var pool = session.Snapshot.CompanyStockpiles.Values.Single(value => value.OwnerId == session.Player.OwnerId && value.PortId == dock);
            Assert.That(pool.StoredUnits, Is.Zero);
            controller.OpenRoutePlanner(session.Player.Id); Click("routeStartButton"); Click("itineraryRunButton");
            pool = session.Snapshot.CompanyStockpiles[pool.Id];
            Assert.That(pool.Inventory["grain"], Is.EqualTo(20000));
            Assert.That(session.Player.Itinerary.EquivalentTo(policy), Is.True);
            Assert.That(session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(2));
            Assert.That(session.Player.DockedPortId, Is.Null);
            Assert.That(session.Snapshot.Day, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BreadPresetIsAnUnfundedDraftWithInfrastructureWarnings()
        {
            controller.OpenRoutePlanner(controller.Session.Player.Id);
            var before = controller.Session.Snapshot;
            root.Q<Foldout>("routePlannerTools").value = true; Click("itineraryExampleButton");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.Player.Itinerary, Is.Null);
            Assert.That(root.Q<Label>("itineraryDraftStatus").text, Does.Contain("3 stops"));
            Assert.That(root.Q<Label>("itineraryStopWarning0").text, Does.Contain("Warehouse needed"));
            Click("routeOrdersButton"); Apply();
            Assert.That(controller.Session.Player.Itinerary.Stops.Count, Is.EqualTo(3));
            Assert.That(controller.Session.Snapshot.PrivateFacilities.Count, Is.Zero);
            Assert.That(controller.Session.Snapshot.CompanyStockpiles.Count, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChangingAnInstructionIdentityCannotAliasTheOriginalGood()
        {
            OpenTwoStopDraft();
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "2");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1");
            Select(0); Click(Cell(0, "grain", CargoItineraryCommandKind.BuyFromMarket));
            root.Q<DropdownField>("itineraryGood0_0").value = controller.Session.Content.GetGood("logs").Name;
            Inline("itineraryCashReserve").value = "10.0001"; Click("routeSaveOrder");
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "3"); Apply();
            var commands = controller.Session.Player.Itinerary.Stops[0].Commands;
            Assert.That(commands.Single(command => command.GoodId == "logs").CashReserve, Is.EqualTo(100001));
            Assert.That(commands.Single(command => command.GoodId == "grain").CashReserve, Is.Zero);
            Assert.That(commands.Single(command => command.GoodId == "logs").MaximumQuantity, Is.EqualTo(20000));
            Assert.That(commands.Single(command => command.GoodId == "grain").MaximumQuantity, Is.EqualTo(30000));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectedOffRouteResumeRestoresPauseControlAndKeepsPolicy()
        {
            OpenTwoStopDraft();
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1"); Apply();
            var session = controller.Session; var policy = session.Player.Itinerary;
            var outside = session.Snapshot.Ports.Keys.First(id => policy.Stops.All(stop => stop.PortId != id));
            session.SailTo(outside); controller.OpenRoutePlanner(session.Player.Id);
            var before = session.Snapshot;
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.Player.Itinerary, Is.SameAs(policy));
            Assert.That(root.Q<Toggle>("itineraryManualToggle").value, Is.True);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("Return to the itinerary's next stop"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReopeningAnUnappliedEditNeverPresentsItAsTheOperatingRoute()
        {
            OpenTwoStopDraft();
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1"); Apply();
            var session = controller.Session; var applied = session.Player.Itinerary;
            var before = session.Snapshot; var saved = session.SaveText();
            Click("routeEditButton"); Click("routeCourseButton");
            var third = before.Ports.Keys.First(id => applied.Stops.All(stop => stop.PortId != id));
            Click(root.Q<ArchipelagoMap>("routeCourseMap").Q<Button>("port_" + third));
            Click("itineraryBackButton");
            controller.OpenRoutePlanner(session.Player.Id);
            Assert.That(root.Q<Label>("routeWorkspaceTitle").text, Is.EqualTo("Plot your course"));
            Assert.That(root.Q<Label>("itineraryDraftStatus").text, Does.Contain("UNAPPLIED DRAFT"));
            Assert.That(root.Q<Button>("routeStartButton").ClassListContains("hidden"), Is.True,
                "Starting the old applied route must not appear to start the edited chart.");
            Assert.That(root.Q<ArchipelagoMap>("routeCourseMap").HighlightedRoutePortIds, Does.Contain(third),
                "The user's unapplied draft remains available for completion.");
            Assert.That(session.Player.Itinerary, Is.SameAs(applied));
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CourseChartMarkersOfferHonestStopAndHarborActions()
        {
            OpenTwoStopDraft();
            AddOrder(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(1, "grain", CargoItineraryCommandKind.SellToMarket, "1"); Apply();
            var session = controller.Session; var saved = session.SaveText();
            var chart = root.Q<ArchipelagoMap>("routeCourseMap");
            foreach (var ship in session.Snapshot.Ships.Values)
            {
                var marker = chart.Q<Button>("ship_" + ship.Id);
                Assert.That(marker.enabledSelf, Is.False);
                Assert.That(marker.focusable, Is.False);
                Assert.That(marker.tooltip, Does.Contain("Position only"));
                Assert.That(controller.Map.Q<Button>("ship_" + ship.Id).enabledSelf, Is.True,
                    "The main gameplay map must retain its working vessel selection.");
            }
            var second = session.Player.Itinerary.Stops[1].PortId;
            Click(chart.Q<Button>("port_" + second));
            Assert.That(root.Q<Label>("routeWorkspaceTitle").text, Is.EqualTo("Stop orders"));
            Assert.That(root.Q<Label>("routeStopTown").text, Is.EqualTo(session.Snapshot.Ports[second].Name));
            Click("routeReviewButton");
            var outside = session.Snapshot.Ports.Keys.First(id => session.Player.Itinerary.Stops.All(stop => stop.PortId != id));
            var town = chart.Q<Button>("port_" + outside);
            Assert.That(town.tooltip, Does.Contain("inspect this town's Harbor"));
            using (var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 1 }))
            { evt.target = town; town.SendEvent(evt); }
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(session.SelectedPortId, Is.EqualTo(outside));
            Click("closeTradeButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Click(chart.Q<Button>("port_" + outside));
            Assert.That(controller.TradeIsOpen, Is.True, "An off-route marker inspects Harbor instead of silently doing nothing.");
            Click("closeTradeButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingCommodityCellsRevealsAndFocusesTheirInlineEditorWithoutManualScrolling()
        {
            OpenTwoStopDraft();
            AddOrder(0, "bread", CargoItineraryCommandKind.BuyFromMarket, "1");
            AddOrder(1, "bread", CargoItineraryCommandKind.SellToMarket, "1"); Select(0);
            var before = controller.Session.Snapshot; var saved = controller.Session.SaveText();
            var sizes = new[] { new Vector2(1920, 1080), new Vector2(1366, 768) };
            var scales = new[] { ArchipelagoUiScale.Default, ArchipelagoUiScale.Large };
            for (var index = 0; index < sizes.Length; index++)
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = sizes[index].x; root.style.height = sizes[index].y;
                controller.TextScale.Apply(scales[index], false);
                yield return null; yield return null; yield return null;
                var scroll = root.Q<ScrollView>("itineraryScroll");
                var existing = root.Q<Button>(Cell(0, "bread", CargoItineraryCommandKind.BuyFromMarket));
                AssertWithinViewport(existing, scroll);
                Click(existing);
                yield return null; yield return null; yield return null;
                AssertInlineEditorVisibleAndFocused(scroll);
                Inline("itineraryMax").value = "7";
                Click("routeCancelOrder");
                yield return null; yield return null; yield return null;
                Assert.That(root.Q<Button>(existing.name).text, Is.EqualTo("1"), "Cancel must discard the local value.");
                AssertWithinViewport(root.Q<Button>(existing.name), scroll);
                var good = index == 0 ? "flour" : "logs";
                var added = root.Q<Button>(Cell(0, good, CargoItineraryCommandKind.BuyFromMarket));
                AssertWithinViewport(added, scroll);
                Click(added);
                yield return null; yield return null; yield return null;
                AssertInlineEditorVisibleAndFocused(scroll);
                Assert.That(Inline("itineraryMax").name, Does.EndWith("_-1"), "New order focus must use the actual rendered key.");
                Inline("itineraryMax").value = "2";
                Click("routeSaveOrder");
                yield return null; yield return null; yield return null;
                Assert.That(root.Q<Button>(added.name).text, Is.EqualTo("2"));
                AssertWithinViewport(root.Q<Button>(added.name), scroll);
                Assert.That(root.Q<DropdownField>("itineraryShip").choices,
                    Is.EqualTo(controller.Session.OwnedShips.Select(ship => ship.Name).ToList()));
                Assert.That(controller.Session.Player.Itinerary, Is.Null, "Save order remains a draft until Apply route.");
                Assert.That(controller.Session.Snapshot, Is.SameAs(before));
                Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            }
        }

        private void AssertInlineEditorVisibleAndFocused(ScrollView scroll)
        {
            var maximum = Inline("itineraryMax");
            Assert.That(root.Q<Foldout>("routeTechnicalLimits").value, Is.False,
                "Advanced quote/price fields must remain progressive disclosure when an order first opens.");
            AssertWithinViewport(maximum, scroll);
            AssertWithinViewport(root.Q<Button>("routeSaveOrder"), scroll);
            AssertWithinViewport(root.Q<Button>("routeCancelOrder"), scroll);
            var focused = root.focusController.focusedElement as VisualElement;
            Assert.That(focused == maximum || maximum.Contains(focused), Is.True,
                "The quantity control must receive focus after the actual layout and reveal.");
        }

        private static void AssertWithinViewport(VisualElement element, ScrollView scroll)
        {
            Assert.That(element.worldBound.yMin, Is.GreaterThanOrEqualTo(scroll.contentViewport.worldBound.yMin - 0.5f), element.name);
            Assert.That(element.worldBound.yMax, Is.LessThanOrEqualTo(scroll.contentViewport.worldBound.yMax + 0.5f), element.name);
            Assert.That(scroll.contentViewport.worldBound.Contains(element.worldBound.center), Is.True, element.name);
        }

        [UnityTest]
        public IEnumerator RouteSurfacesKeepTheUnderlyingFleetHiddenAndPaperControlsReadable()
        {
            OpenTwoStopDraft(); Click("routeCourseButton");
            var before = controller.Session.Snapshot; var saved = controller.Session.SaveText();
            foreach (var size in new[] { new Vector2(1366, 768), new Vector2(1920, 1080) })
            foreach (ArchipelagoUiScale scale in Enum.GetValues(typeof(ArchipelagoUiScale)))
            {
                root.style.flexGrow = 0; root.style.flexShrink = 0;
                root.style.width = size.x; root.style.height = size.y;
                controller.TextScale.Apply(scale, false);
                yield return null; yield return null;
                var context = size + " / " + scale;
                var navigation = root.Q("runNavigation");
                var heading = root.Q(className: "route-v3-heading");
                var title = root.Q<Label>("routeWorkspaceTitle");
                Assert.That(navigation.resolvedStyle.backgroundColor.a, Is.GreaterThanOrEqualTo(0.99f),
                    "The mounted Fleet/map must not bleed through the planner: " + context);
                Assert.That(navigation.worldBound.yMin, Is.GreaterThanOrEqualTo(root.Q("topBar").worldBound.yMax - 0.5f),
                    "Planner heading must remain below the persistent HUD: " + context);
                Assert.That(title.worldBound.xMin, Is.GreaterThanOrEqualTo(navigation.worldBound.xMin + 8), context);
                Assert.That(title.worldBound.yMin, Is.GreaterThanOrEqualTo(navigation.worldBound.yMin + 8), context);
                Assert.That(root.Q(className: "route-v3-body").worldBound.yMin,
                    Is.GreaterThanOrEqualTo(heading.worldBound.yMax), "Heading must not overlap the chart: " + context);
                Assert.That(Contrast(title.resolvedStyle.color, navigation.resolvedStyle.backgroundColor),
                    Is.GreaterThanOrEqualTo(4.5), "Heading contrast: " + context);
                var contextScroll = root.Q<ScrollView>("routeContextScroll");
                AssertPaperSurface(contextScroll, context);
                var tools = root.Q<Foldout>("routePlannerTools");
                var toggle = tools.Q<Toggle>();
                var input = toggle.Q(className: "unity-base-field__input");
                var label = toggle.Q<Label>();
                Assert.That(input.resolvedStyle.backgroundColor.a, Is.GreaterThanOrEqualTo(0.99f), context);
                Assert.That(Contrast(label.resolvedStyle.color, input.resolvedStyle.backgroundColor),
                    Is.GreaterThanOrEqualTo(4.5), "Route tools must not use ink text on a navy input: " + context);
                Click("routeOrdersButton");
                yield return null; yield return null;
                AssertPaperSurface(root.Q<ScrollView>("itineraryScroll"), context);
                Click("routeCourseButton");
                yield return null;
                Assert.That(controller.Session.Snapshot, Is.SameAs(before));
                Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            }
        }

        private static void AssertPaperSurface(ScrollView scroll, string context)
        {
            Assert.That(scroll.contentViewport.resolvedStyle.backgroundColor.a, Is.GreaterThanOrEqualTo(0.99f),
                "Paper viewport must not expose the underlying chart: " + context);
            Assert.That(scroll.contentContainer.resolvedStyle.backgroundColor.a, Is.GreaterThanOrEqualTo(0.99f), context);
            Assert.That(Contrast(new Color32(35, 55, 68, 255), scroll.contentViewport.resolvedStyle.backgroundColor),
                Is.GreaterThanOrEqualTo(4.5), "Paper must support readable ink: " + context);
        }

        private static double Contrast(Color first, Color second)
        {
            double Channel(double value) => value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            double Luminance(Color value) => 0.2126 * Channel(value.r) + 0.7152 * Channel(value.g) + 0.0722 * Channel(value.b);
            var one = Luminance(first); var two = Luminance(second);
            return (Math.Max(one, two) + 0.05) / (Math.Min(one, two) + 0.05);
        }

        private void OpenTwoStopDraft()
        {
            controller.OpenRoutePlanner(controller.Session.Player.Id); Click("itineraryAddStop"); Click("routeOrdersButton"); Select(0);
        }

        private void Select(int index) => Click("routeStopTab" + index);
        private static string Cell(int stop, string good, CargoItineraryCommandKind kind) => "itineraryQuantity" + stop + "_" + good + "_" + (int)kind;
        private TextField Inline(string prefix) => root.Q<VisualElement>("routeInlineEditor").Query<TextField>().ToList().Single(field => field.name.StartsWith(prefix, StringComparison.Ordinal));

        private void AddOrder(int stop, string good, CargoItineraryCommandKind kind, string maximum, bool wait = false)
        {
            Select(stop); Click(Cell(stop, good, kind)); Inline("itineraryMax").value = maximum;
            root.Q<Toggle>("routeWaitIfShort").value = wait; Click("routeSaveOrder");
        }

        private void Apply() { Click("routeReviewButton"); Click("itineraryApplyButton"); }

        private void Click(string id) => Click(root.Q<Button>(id));
        private static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null); Assert.That(button.enabledInHierarchy, Is.True, button.name);
            Assert.That(button.ClassListContains("hidden"), Is.False, button.name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        private void Cancel() { using (var evt = NavigationCancelEvent.GetPooled()) { evt.target = root; root.SendEvent(evt); } }
    }
}
