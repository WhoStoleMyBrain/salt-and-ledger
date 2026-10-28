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
    public sealed class ExpandingRunExperienceTests
    {
        private ArchipelagoLabController controller;
        private VisualElement root;
        private string saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadHost()
        {
            yield return SceneManager.LoadSceneAsync("ArchipelagoLab", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<ArchipelagoLabController>();
            Assert.That(controller, Is.Not.Null);
            root = controller.Root;
            controller.TextScale.Apply(ArchipelagoUiScale.Default, false);
            // Exercise the actual slot UI without reading, overwriting or deleting the user's local saves.
            saveDirectory = Path.Combine(Path.GetTempPath(), "SaltAndLedger-UI-" + Guid.NewGuid().ToString("N"));
            typeof(ArchipelagoLabController).GetField("saveStore", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new LocalRunSaveStore(saveDirectory));
        }

        [TearDown]
        public void RemoveOnlyTestSlots()
        {
            if (saveDirectory != null && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator V3SetupShowsVersionedRolesAndExactStartWithoutChangingThePreviewedWorld()
        {
            var before = controller.Session.Snapshot;
            Click("newRunButton");
            root.Q<TextField>("seedField").value = "-2147483648";
            var difficulty = root.Q<DropdownField>("difficultyPicker"); difficulty.value = difficulty.choices[1];
            var pricing = root.Q<DropdownField>("livingPricingPicker"); pricing.value = pricing.choices[1];
            root.Q<DropdownField>("charterPicker").value = RunScenario.GetCharter(RunScenario.MerchantCharter).Name;
            root.Q<DropdownField>("challengePicker").value = "Unlimited sandbox";
            var identity = controller.SetupIdentity;
            Assert.That(identity.WorldMode, Is.EqualTo(RunWorldMode.ExpandingEconomy));
            Assert.That(identity.GeneratorVersion, Is.EqualTo(3));
            Assert.That(identity.ContentVersion, Is.EqualTo(3));
            Assert.That(identity.ValuationVersion, Is.EqualTo(1));
            Assert.That(identity.Ascension, Is.EqualTo(1));
            Assert.That(identity.LivingPricingMode, Is.EqualTo(LivingPricingMode.Coverage));
            Assert.That(root.Q<Label>("setupWorldIdentity").text, Does.Contain("Expanding Economy"));
            Assert.That(root.Q<Label>("setupAssets").text, Does.Contain("1800 coins"));
            var generation = ExpandingEconomyGeneration.Generate(identity.Seed.Value, pricingMode: identity.LivingPricingMode);
            var cards = string.Join("\n", generation.Ports.Select(p => root.Q<Label>("setupCapabilities_" + p.Id).text));
            foreach (var blueprint in generation.Blueprints)
                Assert.That(cards, Does.Contain(controller.Session.CanonicalContent.ForExpandingEconomy().GetBuilding(blueprint.BuildingId).Name));
            Assert.That(root.Q<Label>("setupCapabilities_" + generation.MintPortId).text, Does.Contain("Public Mint and shipwright"));
            Assert.That(controller.Session.Snapshot, Is.SameAs(before));
            Click("startRunButton");
            Assert.That(controller.Session.Identity, Is.EqualTo(identity));
            Assert.That(controller.Session.Player.Cash, Is.EqualTo(18000000));
            Assert.That(controller.Session.Content.Goods.Count, Is.EqualTo(7));
            Assert.That(controller.Session.Snapshot.Day, Is.Zero);
            foreach (var position in SeededArchipelago.Generate(identity.Seed.Value).Positions.Values)
            {
                var port = controller.Session.Snapshot.Ports[position.PortId];
                Assert.That(port.X, Is.EqualTo(position.X)); Assert.That(port.Y, Is.EqualTo(position.Y));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RoyalControlsRequireRealSilverAndDockingAndCommitOnlyOneAuthorizedIssuance()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            var session = controller.Session;
            var config = session.Snapshot.Economy.Configuration;
            Click("royalServicesButton"); Click("developmentTab1");
            var before = session.Snapshot;
            Assert.That(root.Q<Button>("mintSilverButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("mintExchangeQuote").text, Does.Contain("physically"));
            Click("commissionMintButton");
            Assert.That(session.Snapshot.Economy.MintStatus, Is.EqualTo(MintStatus.UnderConstruction));
            Assert.That(session.Snapshot.TotalCash, Is.EqualTo(before.TotalCash));
            Assert.That(session.Snapshot.Economy.TreasuryCash, Is.EqualTo(config.MintCommissionCost));
            Click("developmentTab0"); Click("commissionShipButton");
            Assert.That(session.OwnedShips.Count, Is.EqualTo(1));
            Assert.That(session.Snapshot.Economy.ShipOrders.Count, Is.EqualTo(1));
            Click("developmentBackButton");
            Click("port_" + config.SilverPortId); Click("openIndustryButton");
            root.Q<DropdownField>("blueprintDropdown").value = "Silver Mine";
            Click("constructButton"); Click("closeIndustryButton");
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            var mine = session.Snapshot.PrivateFacilities.Values.Single(f => f.Blueprint.BuildingId == "silver_mine");
            Assert.That(session.Snapshot.GetFacilityStorage(mine.Id).Inventory["silver"], Is.EqualTo(10000));
            Assert.That(mine.WagesPaid, Is.GreaterThan(0));
            Assert.That(session.Snapshot.Economy.MintStatus, Is.EqualTo(MintStatus.Active));
            Click("royalServicesButton"); Click("developmentTab0"); Click("selectOwned_player_ship_2");
            Click("developmentTab1");
            Assert.That(root.Q<Button>("mintSilverButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("mintExchangeQuote").text, Does.Contain("Silver aboard this ship"));
            Click("developmentBackButton");
            SailTo(config.SilverPortId);
            Click("openIndustryButton");
            root.Q<DropdownField>("storageGoodDropdown").value = "Silver";
            root.Q<TextField>("storageQuantity").value = "1";
            Click("warehouseToShip"); Click("closeIndustryButton");
            Assert.That(session.Player.Cargo["silver"], Is.EqualTo(10000));
            Click("royalServicesButton"); Click("developmentTab1");
            Assert.That(root.Q<Button>("mintSilverButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("mintExchangeQuote").text, Does.Contain("physically"));
            Click("developmentBackButton");
            SailTo(config.MintPortId);
            Click("royalServicesButton"); Click("developmentTab1");
            var supplied = session.Snapshot;
            Click("mintSilverButton");
            Assert.That(session.Player.Cargo["silver"], Is.Zero);
            Assert.That(session.Snapshot.Economy.SilverReserve, Is.EqualTo(10000));
            Assert.That(session.Snapshot.Economy.MintBatches, Is.EqualTo(1));
            Assert.That(session.Snapshot.Economy.MintedCurrency, Is.EqualTo(800000));
            Assert.That(session.Player.Cash - supplied.Companies[session.Player.OwnerId].Cash, Is.EqualTo(600000));
            Assert.That(session.Snapshot.Economy.TreasuryCash - supplied.Economy.TreasuryCash, Is.EqualTo(200000));
            Assert.That(session.Snapshot.TotalCash - supplied.TotalCash, Is.EqualTo(new System.Numerics.BigInteger(800000)));
            Assert.That(root.Q<Label>("mintSummary").text, Does.Contain("Issued 80 coins = suppliers 60 + treasury 20"));
            Assert.That(root.Q<Button>("mintSilverButton").enabledInHierarchy, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChosenOwnedShipReceivesOnlyWholeTradeOrdersAndNpcRemainsInspectionOnly()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            var session = controller.Session;
            Click("royalServicesButton"); Click("commissionShipButton"); Click("developmentBackButton");
            for (var day = 0; day < 4; day++) Click("nextDayButton");
            Click("royalServicesButton"); Click("selectOwned_player_ship_2"); Click("developmentBackButton");
            var ship = session.Player;
            var portId = ship.DockedPortId;
            var goodId = session.Content.Goods.Select(g => g.Id).First(id => session.Snapshot.PreviewTrade(ship.Id, portId,
                new TradeCommand(TradeDirection.Buy, id, 10000)).Outcome == TradeOutcome.Traded);
            controller.OpenTrade(portId);
            root.Q<DropdownField>("goodDropdown").value = session.Content.GetGood(goodId).Name;
            root.Q<TextField>("quantityField").value = "0.75";
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("Whole cargo only"));
            var before = session.Snapshot;
            root.Q<TextField>("quantityField").value = "1";
            var preview = before.PreviewTrade(ship.Id, portId, new TradeCommand(TradeDirection.Buy, goodId, 10000));
            Assert.That(root.Q<Label>("buyPreviewLabel").text, Does.Contain("Pay " + session.Coins(preview.TotalAmount)));
            Assert.That(root.Q<Label>("tradeHeader").text, Does.Contain("cargo " + session.Quantity(ship.CargoUnits) + "/" + session.Quantity(ship.CargoCapacity)));
            Click("buyButton");
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            Assert.That(session.Player.Cargo[goodId], Is.EqualTo(10000));
            Assert.That(session.Snapshot.Ships[WorldIds.PlayerShipId].Cargo, Is.EqualTo(before.Ships[WorldIds.PlayerShipId].Cargo));
            Assert.That(session.Snapshot.Day, Is.EqualTo(before.Day));
            Assert.That(session.Player.Cash, Is.EqualTo(before.Companies[ship.OwnerId].Cash - preview.TotalAmount));
            Click("closeTradeButton");
            session.SelectShip(WorldIds.TimberShipId);
            controller.OpenTrade(portId);
            Assert.That(root.Q<Button>("buyButton").enabledInHierarchy, Is.False);
            Assert.That(root.Q<Label>("tradeAvailabilityLabel").text, Does.Contain("NPC inspection only"));
            Assert.That(session.Player.Id, Is.EqualTo("player_ship_2"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalSaveFlowRestoresPendingTravelAndProjectsRejectsCorruptionAndReturnsThroughNestedScreens()
        {
            controller.BeginRun(new RunIdentity(null, RunScenario.MerchantCharter, RunWorldMode.ExpandingEconomy, 42));
            var session = controller.Session;
            var mintPort = session.Snapshot.Economy.Configuration.MintPortId;
            Click("royalServicesButton"); Click("commissionShipButton"); Click("developmentTab1"); Click("commissionMintButton");
            Click("developmentBackButton");
            Click("playerShipButton"); Click("port_" + mintPort); Click("sailButton"); Click("nextDayButton");
            var saved = session.SaveText();
            Click("royalServicesButton"); Click("developmentSaveButton");
            Click("saveSlot1");
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "slot-1.json")), Is.EqualTo(saved));
            Assert.That(session.HasUnsavedChanges, Is.False);
            Click("savesBackButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Development));
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            Click("nextDayButton");
            Click("menuButton"); Click("pauseSavesButton");
            var advanced = session.Snapshot;
            Click("loadSlot1");
            Assert.That(controller.ConfirmationIsOpen, Is.True);
            Cancel();
            Assert.That(session.Snapshot, Is.SameAs(advanced));
            Click("loadSlot1"); Click("confirmAcceptButton");
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Assert.That(session.HasUnsavedChanges, Is.False);
            Assert.That(session.IsPlaying, Is.False);
            Assert.That(session.Snapshot.Economy.ShipOrders.Count, Is.EqualTo(1));
            Assert.That(session.Snapshot.Economy.MintStatus, Is.EqualTo(MintStatus.UnderConstruction));
            Click("menuButton"); Click("pauseSavesButton");
            var preserved = session.Snapshot;
            File.WriteAllText(Path.Combine(saveDirectory, "slot-1.json"), "{invalid");
            Click("loadSlot1");
            Assert.That(session.Snapshot, Is.SameAs(preserved));
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Saves));
            Assert.That(root.Q<Label>("navigationStatus").text, Is.Not.Empty);
            Click("saveSlot2");
            Assert.That(root.Q<Button>("loadSlot1").enabledInHierarchy, Is.False);
            Cancel();
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Pause));
            Click("returnMenuButton");
            Assert.That(root.Q<Label>("confirmationMessage").text, Does.Contain("saved at its current revision"));
            Click("confirmAcceptButton");
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.MainMenu));
            Click("continueRunButton");
            Assert.That(session.SaveText(), Is.EqualTo(saved));
            Assert.That(session.Snapshot.Challenge, Is.Null);
            Assert.That(controller.Screen, Is.EqualTo(ArchipelagoScreen.Gameplay));
            yield return null;
        }

        private void SailTo(string portId)
        {
            Click("playerShipButton"); Click("port_" + portId); Click("sailButton");
            for (var day = 0; day < 20 && controller.Session.Player.DockedPortId == null; day++) Click("nextDayButton");
            Assert.That(controller.Session.Player.DockedPortId, Is.EqualTo(portId));
        }

        private void Cancel()
        {
            using (var evt = NavigationCancelEvent.GetPooled()) { evt.target = root; root.SendEvent(evt); }
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
