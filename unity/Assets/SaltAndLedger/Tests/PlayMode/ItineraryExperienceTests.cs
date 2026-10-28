using System;
using System.Collections;
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
    public sealed class ItineraryExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>(); root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            saveDirectory = Path.Combine(Path.GetTempPath(), "SaltAndLedger-Itinerary-" + Guid.NewGuid().ToString("N"));
            typeof(ArchipelagoLabController).GetField("saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new LocalRunSaveStore(saveDirectory));
        }

        [TearDown]
        public void RemoveOnlyTestSlots()
        {
            if (saveDirectory != null && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator FreshSetupAndFleetOpenThePassiveBoundedPlanner()
        {
            Click("sandboxMenuButton");
            Assert.That(controller.SetupIdentity.ItineraryVersion, Is.EqualTo(1));
            Click("startRunButton");
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Click("royalServicesButton"); Click("routeOwned_" + controller.Session.Player.Id);
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.RoutePlanner));
            Assert.That(controller.Session.IsPlaying, Is.False);
            Click("itineraryAddStop"); Click("itineraryAddStop");
            Assert.That(root.Q<Button>("itineraryAddStop").enabledSelf, Is.False);
            SelectStop(3); Click("itineraryStopUp3");
            SelectStop(0); Click("itineraryStopDown0");
            SelectStop(3); Click("itineraryRemoveStop3");
            SelectStop(0);
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "3");
            Grid(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "2");
            SelectStop(1);
            Assert.That(root.Q<TextField>(GridName(0, "grain", CargoItineraryCommandKind.BuyFromMarket)), Is.Null,
                "Only the selected stop has a commodity editor.");
            SelectStop(0);
            Assert.That(root.Q<TextField>(GridName(0, "grain", CargoItineraryCommandKind.BuyFromMarket)).value, Is.EqualTo("3"));
            Assert.That(root.Q<TextField>(GridName(0, "logs", CargoItineraryCommandKind.BuyFromMarket)).value, Is.EqualTo("2"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            Assert.That(root.Q<Label>("itineraryDraftStatus").text, Does.Contain("UNAPPLIED DRAFT"));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Development));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            yield return null;
        }


        [UnityTest]
        public IEnumerator TwoPaidActionsThenWaitPreserveTheCursorAndManualControl()
        {
            BeginIndependent(); OpenPlanner();
            SelectStop(0);
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1");
            Grid(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "1");
            Grid(0, "silver", CargoItineraryCommandKind.SellToMarket, "2");
            Advanced(0, 0);
            Click("itineraryActionDown0_0");
            Advanced(0, 1);
            Click("itineraryActionDown0_1");
            Advanced(0, 2);
            Choose("itineraryShortage0_2", 1);
            var before = controller.Session.Snapshot;
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.Itinerary.Id, Is.EqualTo("route_" + controller.Session.Player.Id));
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(controller.Session.Player.AutomationEnabled, Is.False);
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Click("itineraryRunButton");
            var waited = controller.Session.Snapshot;
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(2));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(before.Ships[controller.Session.Player.Id].Cargo["grain"] + 10000));
            Assert.That(controller.Session.Player.Cargo["logs"], Is.EqualTo(before.Ships[controller.Session.Player.Id].Cargo["logs"] + 10000));
            Assert.That(root.Q<Label>("itineraryLiveStatus").text, Does.Contain("Waiting"));
            Click("itineraryRunButton");
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(waited.Companies[controller.Session.Player.OwnerId].Cash));
            Assert.That(controller.Session.Player.Cargo["grain"], Is.EqualTo(waited.Ships[controller.Session.Player.Id].Cargo["grain"]));
            var progress = controller.Session.Player.ItineraryProgress;
            root.Q<Toggle>("itineraryManualToggle").value = true;
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(progress.ActionIndex));
            Assert.That(controller.Session.Player.ItineraryProgress.Cycle, Is.EqualTo(progress.Cycle));
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True, "Equivalent reapplication preserves the partial visit.");
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.EqualTo(progress.ActionIndex));
            Advanced(0, 2);
            root.Q<TextField>("itineraryMax0_2").value = "3";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("partial visit"));
            Assert.That(controller.Session.Player.Itinerary.Stops[0].Commands[2].MaximumQuantity, Is.EqualTo(20000));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletedStopCannotReplaceOrdersToResetTodaysBudget()
        {
            BeginIndependent(); OpenPlanner();
            SelectStop(0);
            for (var i = 0; i < 8; i++)
            {
                Advanced(0);
                if (i > 0) Click("itineraryAddAction0");
                var key = "0_" + i;
                Advanced(0, i);
                Choose("itineraryKind" + key, 1);
                root.Q<DropdownField>("itineraryGood" + key).value = controller.Session.Content.GetGood("silver").Name;
            }
            Click("itineraryApplyButton");
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Click("itineraryRunButton");
            var progress = controller.Session.Player.ItineraryProgress;
            Assert.That(progress.ActionIndex, Is.EqualTo(8)); Assert.That(progress.OperationsUsed, Is.EqualTo(8));
            root.Q<Toggle>("itineraryManualToggle").value = true;
            var saved = controller.Session.SaveText();
            Click("itineraryBackButton"); Click("developmentSaveButton"); Click("saveSlot1"); Click("loadSlot1");
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            Assert.That(controller.Session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(8));
            OpenPlanner(); SelectStop(0);
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True);
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(8));
            Advanced(0, 0);
            root.Q<TextField>("itineraryMax0_0").value = "3";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("Advance a day").And.Contain("operation budget"));
            Click("itineraryNextDayButton");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True);
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.ItineraryProgress.ActionIndex, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DraftValidationAndRemoteQuotesNeverExecuteAndHarborLinksReturn()
        {
            BeginIndependent();
            controller.OpenTrade(controller.Session.Player.DockedPortId);
            var before = controller.Session.Snapshot;
            Click("harborRoutePlannerButton");
            SelectStop(0);
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "1.5");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("whole number"));
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "0");
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False,
                "An empty stop remains an invalid draft; no placeholder order is silently added.");
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "2");
            Advanced(0, 0);
            root.Q<TextField>("itineraryCashReserve0_0").value = "10.00001";
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.False);
            root.Q<TextField>("itineraryCashReserve0_0").value = "10.0001";
            SelectStop(1); Advanced(1, 0);
            Assert.That(root.Q<Label>("itineraryQuote1_0").text, Does.Contain("REMOTE / HYPOTHETICAL").And.Contain("Market budget"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.True);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Cancel();
            Assert.That(controller.TradeIsOpen, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectedRouteEnableRestoresManualToggleAndExplainsTheLocation()
        {
            BeginIndependent(); OpenPlanner(); Click("itineraryApplyButton");
            var policy = controller.Session.Player.Itinerary;
            var outside = controller.Session.Snapshot.Ports.Keys.First(port => policy.Stops.All(stop => stop.PortId != port));
            Click("itineraryBackButton"); Click("developmentBackButton");
            controller.OpenTrade(outside); Click("tradeSailButton");
            Assert.That(controller.Session.Player.DestinationPortId, Is.EqualTo(outside));
            OpenPlanner();
            var before = controller.Session.Snapshot;
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.Player.AutomationEnabled, Is.False);
            Assert.That(root.Q<Toggle>("itineraryManualToggle").value, Is.True);
            Assert.That(root.Q<Label>("itineraryValidation").text, Does.Contain("Return to the itinerary's next stop"));
            Assert.That(controller.Session.Player.Itinerary, Is.SameAs(policy));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExampleIsOnlyADraftAndExplainsMissingCompanyStock()
        {
            BeginIndependent(); OpenPlanner();
            var before = controller.Session.Snapshot;
            Click("itineraryExampleButton");
            Assert.That(controller.Session.Player.Itinerary, Is.Null);
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(root.Q<Label>("itineraryDraftStatus").text, Does.Contain("3 stops"));
            Assert.That(root.Q<Label>("itineraryStopWarning0").text, Does.Contain("warehouse").IgnoreCase);
            Assert.That(root.Q<Button>("itineraryApplyButton").enabledSelf, Is.True,
                "Planning company-stock work does not require an existing warehouse.");
            Click("itineraryApplyButton");
            Assert.That(controller.Session.Player.Itinerary.Stops.Count, Is.EqualTo(3));
            Assert.That(controller.Session.Snapshot.PrivateFacilities.Count, Is.Zero);
            Assert.That(controller.Session.Snapshot.CompanyStockpiles.Count, Is.Zero);
            SelectStop(0); Advanced(0, 0);
            yield return null;
            var button = root.Q<Button>("itineraryBackButton");
            Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(20));
            Assert.That(root.worldBound.Contains(button.worldBound.center), Is.True);
        }

        [UnityTest]
        public IEnumerator CommodityGridCreatesSeveralOrdersWithDisposalFirstAndZeroRemovesAnOrder()
        {
            BeginIndependent(); OpenPlanner(); SelectStop(0);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Grid(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "2");
            Grid(0, "flour", CargoItineraryCommandKind.LoadFromCompanyTownStock, "2");
            Grid(0, "bread", CargoItineraryCommandKind.SellToMarket, "3");
            Grid(0, "logs", CargoItineraryCommandKind.UnloadToCompanyTownStock, "1");
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "0");
            Grid(0, "silver", CargoItineraryCommandKind.BuyFromMarket, "1");
            Grid(0, "silver", CargoItineraryCommandKind.BuyFromMarket, "");
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "4");
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            Assert.That(root.Q<Foldout>("itineraryAdvanced0").value, Is.False,
                "Multiple commodities need only quantity cells, not individual advanced action forms.");
            Click("itineraryApplyButton");
            var commands = controller.Session.Player.Itinerary.Stops[0].Commands;
            Assert.That(commands.Select(command => command.Kind), Is.EqualTo(new[]
            {
                CargoItineraryCommandKind.UnloadToCompanyTownStock, CargoItineraryCommandKind.SellToMarket,
                CargoItineraryCommandKind.LoadFromCompanyTownStock, CargoItineraryCommandKind.BuyFromMarket,
                CargoItineraryCommandKind.BuyFromMarket
            }));
            Assert.That(commands.Select(command => command.GoodId), Is.EqualTo(new[] { "logs", "bread", "flour", "grain", "logs" }));
            Assert.That(commands.Select(command => command.MaximumQuantity), Is.EqualTo(new[] { 10000, 30000, 20000, 40000, 20000 }));
            Assert.That(commands.All(command => command.MinimumQuantity == 10000 && command.ShortagePolicy == CargoItineraryShortagePolicy.Skip), Is.True);
            Assert.That(controller.Session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(before.Ships[controller.Session.Player.Id].Cash));
            Assert.That(controller.Session.Player.Cargo, Is.EqualTo(before.Ships[controller.Session.Player.Id].Cargo));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReopenedCustomRouteRetainsDuplicateOrdersExactLimitsAndIdentity()
        {
            BeginIndependent();
            var session = controller.Session;
            var dock = session.Player.DockedPortId;
            var remote = session.Snapshot.Ports.Keys.First(port => port != dock);
            var policy = new CargoItineraryPolicy("custom_order", new[]
            {
                new CargoItineraryStop(dock, new[]
                {
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", 10000, 10000,
                        CargoItineraryShortagePolicy.Wait, 99, 100001, 300002),
                    new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", 20000, marginalPriceLimit: 10),
                    new CargoItineraryCommand(CargoItineraryCommandKind.BuyFromMarket, "grain", 20000, 10000,
                        CargoItineraryShortagePolicy.Skip, 100, 100002, 300003),
                    new CargoItineraryCommand(CargoItineraryCommandKind.LoadFromCompanyTownStock, "flour", 20000,
                        warehouseReserve: 12345)
                }),
                new CargoItineraryStop(remote, new[] { new CargoItineraryCommand(CargoItineraryCommandKind.SellToMarket, "grain", 10000) })
            });
            session.ApplyItinerary(policy, session.Snapshot);
            var before = session.Snapshot;
            var saved = session.SaveText();
            OpenPlanner(); SelectStop(0);
            Assert.That(root.Q<TextField>(GridName(0, "grain", CargoItineraryCommandKind.BuyFromMarket)).enabledSelf, Is.False,
                "Two independently constrained orders must not be collapsed into one grid quantity.");
            foreach (var restoreWarehouseFirst in new[] { false, true })
            {
                Grid(0, "grain", CargoItineraryCommandKind.SellToMarket, "");
                Grid(0, "flour", CargoItineraryCommandKind.LoadFromCompanyTownStock, "");
                if (restoreWarehouseFirst)
                {
                    Grid(0, "flour", CargoItineraryCommandKind.LoadFromCompanyTownStock, "2");
                    Grid(0, "grain", CargoItineraryCommandKind.SellToMarket, "2");
                }
                else
                {
                    Grid(0, "grain", CargoItineraryCommandKind.SellToMarket, "2");
                    Grid(0, "flour", CargoItineraryCommandKind.LoadFromCompanyTownStock, "2");
                }
                Assert.That(session.Snapshot, Is.SameAs(before));
                Assert.That(session.SaveText(), Is.EqualTo(saved));
                Click("itineraryApplyButton");
                Assert.That(session.Player.Itinerary.EquivalentTo(policy), Is.True,
                    "Clearing several quantities then retyping in either order must retain exact instruction order and limits.");
                Assert.That(session.Snapshot, Is.SameAs(before));
            }
            Advanced(0, 0);
            Assert.That(root.Q<TextField>("itineraryCashReserve0_0").value, Is.EqualTo("10.0001"));
            Assert.That(root.Q<TextField>("itineraryBudget0_0").value, Is.EqualTo("30.0002"));
            Advanced(0, 3);
            Assert.That(root.Q<TextField>("itineraryWarehouseReserve0_3").value, Is.EqualTo("1.2345"));
            Click("itineraryReloadButton"); SelectStop(0); Click("itineraryApplyButton");
            Assert.That(session.Player.Itinerary.EquivalentTo(policy), Is.True);
            Assert.That(session.Snapshot, Is.SameAs(before));
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Grid(0, "logs", CargoItineraryCommandKind.BuyFromMarket, "3");
            Click("itineraryApplyButton");
            var applied = session.Player.Itinerary;
            Assert.That(applied.Id, Is.EqualTo("custom_order"));
            Assert.That(applied.Stops[0].Commands.Count, Is.EqualTo(5));
            Assert.That(applied.Stops[0].Commands.Select(command => command.GoodId),
                Is.EqualTo(new[] { "grain", "grain", "grain", "flour", "logs" }));
            var unchangedPrefix = new CargoItineraryPolicy(policy.Id, new[]
            {
                new CargoItineraryStop(dock, applied.Stops[0].Commands.Take(4)), applied.Stops[1]
            });
            Assert.That(unchangedPrefix.EquivalentTo(policy), Is.True,
                "Adding a grid cell to an imported route preserves all prior ordering and exact constraints.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator RestoredGridCellCannotAliasAnotherCommodityAfterAdvancedIdentityChange()
        {
            BeginIndependent(); OpenPlanner(); SelectStop(0);
            var before = controller.Session.Snapshot;
            var saved = controller.Session.SaveText();
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "");
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "2");
            Advanced(0, 0);
            root.Q<DropdownField>("itineraryGood0_0").value = controller.Session.Content.GetGood("logs").Name;
            root.Q<TextField>("itineraryCashReserve0_0").value = "10.0001";
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "3");
            Assert.That(root.Q<TextField>(GridName(0, "logs", CargoItineraryCommandKind.BuyFromMarket)).value, Is.EqualTo("2"));
            Assert.That(root.Q<TextField>(GridName(0, "grain", CargoItineraryCommandKind.BuyFromMarket)).value, Is.EqualTo("3"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Assert.That(controller.Session.SaveText(), Is.EqualTo(saved));
            Click("itineraryApplyButton");
            var commands = controller.Session.Player.Itinerary.Stops[0].Commands;
            Assert.That(commands.Select(command => command.GoodId), Is.EqualTo(new[] { "logs", "grain" }));
            Assert.That(commands.Select(command => command.MaximumQuantity), Is.EqualTo(new[] { 20000, 30000 }));
            Assert.That(commands[0].CashReserve, Is.EqualTo(100001));
            Assert.That(commands[1].CashReserve, Is.Zero,
                "Adding the original good creates a fresh instruction, not an alias of its renamed predecessor.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingWarehouseWaitSurvivesSlotLoadAndConstructionMakesTheSameRouteWork()
        {
            BeginIndependent(); OpenPlanner(); SelectStop(0);
            var session = controller.Session;
            var dock = session.Player.DockedPortId;
            Grid(0, "grain", CargoItineraryCommandKind.BuyFromMarket, "0");
            Grid(0, "grain", CargoItineraryCommandKind.UnloadToCompanyTownStock, "2");
            Advanced(0, 0); Choose("itineraryShortage0_0", 1);
            Assert.That(root.Q<Label>("itineraryStopWarning0").text, Does.Contain("warehouse").IgnoreCase);
            Click("itineraryApplyButton");
            var policy = session.Player.Itinerary;
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Click("itineraryRunButton");
            Assert.That(session.Player.ItineraryProgress.LastReason, Is.EqualTo(CargoItineraryBlockReason.MissingCompanyStock));
            Assert.That(session.Player.ItineraryProgress.ActionIndex, Is.Zero);
            Assert.That(session.Player.ItineraryProgress.OperationsUsed, Is.Zero);
            var waiting = session.Snapshot;
            Click("itineraryRunButton");
            Assert.That(session.Snapshot, Is.SameAs(waiting), "Repeated waiting neither transfers cargo nor consumes an operation.");
            root.Q<Toggle>("itineraryManualToggle").value = true;
            var saved = session.SaveText();
            Click("itineraryBackButton"); Click("developmentSaveButton"); Click("saveSlot1"); Click("loadSlot1");
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Assert.That(session.Player.Itinerary.EquivalentTo(policy), Is.True);
            Assert.That(session.Player.ItineraryProgress.LastReason, Is.EqualTo(CargoItineraryBlockReason.MissingCompanyStock));
            Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Farm";
            var cash = session.Player.Cash;
            Click("constructButton");
            var facility = session.Snapshot.PrivateFacilities.Values.Single(f => f.OwnerId == session.Player.OwnerId && f.PortId == dock);
            Assert.That(session.Player.Cash, Is.EqualTo(cash - facility.ConstructionCost));
            Assert.That(session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.Zero,
                "Planning created no warehouse or stock; construction creates only the real empty pool.");
            Click("closeIndustryButton"); OpenPlanner();
            root.Q<Toggle>("itineraryManualToggle").value = false;
            Click("itineraryRunButton");
            Assert.That(session.Player.Itinerary.EquivalentTo(policy), Is.True);
            Assert.That(session.Snapshot.GetFacilityStorage(facility.Id).Inventory["grain"], Is.EqualTo(20000));
            Assert.That(session.Player.Cargo["grain"], Is.EqualTo(20000));
            Assert.That(session.Player.ItineraryProgress.StopIndex, Is.EqualTo(1));
            Assert.That(session.Player.ItineraryProgress.OperationsUsed, Is.EqualTo(2), "One unload and one physical departure.");
            Assert.That(session.Player.DockedPortId, Is.Null);
            Assert.That(session.Snapshot.Day, Is.Zero);
            yield return null;
        }

        private void BeginIndependent() => controller.BeginRun(new RunIdentity(null,
            RunScenario.IndependentCharter, RunWorldMode.ExpandingEconomy, 42,
            companyStorageRules: CompanyStorageRules.Pooled, itineraryVersion: 1));

        private void OpenPlanner()
        {
            Click("royalServicesButton"); Click("routeOwned_" + controller.Session.Player.Id);
        }

        private void SelectStop(int index) => Click("itinerarySelectStop" + index);

        private static string GridName(int stop, string good, CargoItineraryCommandKind kind) =>
            "itineraryQuantity" + stop + "_" + good + "_" + (int)kind;

        private void Grid(int stop, string good, CargoItineraryCommandKind kind, string quantity)
        {
            var field = root.Q<TextField>(GridName(stop, good, kind));
            Assert.That(field, Is.Not.Null, GridName(stop, good, kind));
            Assert.That(field.enabledInHierarchy, Is.True);
            field.value = quantity;
        }

        private void Advanced(int stop, int action = -1)
        {
            root.Q<Foldout>("itineraryAdvanced" + stop).value = true;
            if (action >= 0) root.Q<Foldout>("itineraryAction" + stop + "_" + action).value = true;
        }

        private void Choose(string id, int index)
        {
            var field = root.Q<DropdownField>(id); field.value = field.choices[index];
        }

        private void Click(string id)
        {
            var button = root.Q<Button>(id); Assert.That(button, Is.Not.Null, id);
            Assert.That(button.enabledInHierarchy, Is.True, id);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled()) { evt.target = root; root.SendEvent(evt); }
        }
    }
}
