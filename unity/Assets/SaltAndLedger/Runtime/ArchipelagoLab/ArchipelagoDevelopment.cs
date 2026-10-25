using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private LocalRunSaveStore saveStore;
        private readonly List<string> royalContractIds = new List<string>();
        private int developmentTab;

        private void RefreshRunChoices()
        {
            var goods = Session.Content.Goods.OrderBy(g => g.Id, StringComparer.Ordinal).ToArray();
            if (!goodIds.SequenceEqual(goods.Select(g => g.Id)))
            {
                goodIds.Clear(); goodIds.AddRange(goods.Select(g => g.Id));
                foreach (var name in new[] { "goodDropdown", "outGoodDropdown", "storageGoodDropdown", "logisticsGood", "logisticsReturnGood" })
                    if (Root.Q<DropdownField>(name) != null) SetChoices(name, goods.Select(g => g.Name).ToList());
                SetChoices("returnGoodDropdown", new[] { "None" }.Concat(goods.Select(g => g.Name)).ToList());
                Element<VisualElement>("tradeGoodsRows").Clear();
            }
            var ships = Session.Snapshot.Ships.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (!shipIds.SequenceEqual(ships))
            {
                shipIds.Clear(); shipIds.AddRange(ships);
                SetChoices("shipPicker", new[] { "Inspect ship…" }.Concat(shipIds.Select(id => ShipChoice(Session.Snapshot.Ships[id]))).ToList());
            }
        }

        private void BuildDevelopment()
        {
            saveStore = new LocalRunSaveStore(Path.Combine(Application.persistentDataPath, "Runs"));
            var screen = CreateScreen(ArchipelagoScreen.Development, "Company & royal services",
                "Choose a ship explicitly. Public services use finite accounts and real deliveries; the Mint belongs to the region.");
            Text(screen, "companyHeadline", "", "run-objective");
            var tabs = Panel(screen, null, "row run-actions");
            var names = new[] { "Fleet", "Royal Mint", "External contracts", "Public works" };
            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                Button(tabs, "developmentTab" + i, names[i], () => { developmentTab = index; RefreshDevelopment(); });
            }
            var scroll = new ScrollView { name = "developmentScroll" };
            scroll.AddToClassList("run-content-scroll"); screen.Add(scroll);
            for (var i = 0; i < names.Length; i++) Panel(scroll, "developmentPane" + i, "run-card");
            var fleet = Element<VisualElement>("developmentPane0");
            Panel(fleet, "ownedFleetRows", "run-columns");
            Text(fleet, "shipwrightQuote", "", "run-copy");
            Button(fleet, "commissionShipButton", "Commission ship", () => Run(() =>
            {
                Session.SelectPort(Session.Snapshot.Economy.Configuration.ShipwrightPortId);
                Session.CommissionShip();
            }), "primary");
            Text(fleet, "shipOrders", "", "run-copy");
            var mint = Element<VisualElement>("developmentPane1");
            Text(mint, "mintSummary", "", "run-copy");
            Button(mint, "commissionMintButton", "Commission public Mint", () => Run(() => Session.CommissionMint()), "primary");
            Text(mint, "mintExchangeQuote", "", "run-copy");
            Button(mint, "mintSilverButton", "Surrender 1 Silver · issue authorized coins", () => Run(() => Session.MintSilver()), "primary");
            var external = Element<VisualElement>("developmentPane2");
            var contract = new DropdownField("Royal contract", new List<string> { "No contracts" }, 0) { name = "royalContractPicker" };
            external.Add(contract);
            contract.RegisterValueChangedCallback(_ => RefreshDevelopment());
            Field(external, "exportQuantity", "Whole units to deliver", "1");
            Element<TextField>("exportQuantity").RegisterValueChangedCallback(_ => RefreshDevelopment());
            Text(external, "externalSummary", "", "run-copy");
            Text(external, "exportQuote", "", "run-copy");
            Button(external, "fulfillContractButton", "Deliver owned cargo", () => Run(() => Session.FulfillContract(
                royalContractIds[Element<DropdownField>("royalContractPicker").index], RoyalQuantity("exportQuantity"))), "primary");
            var works = Element<VisualElement>("developmentPane3");
            Text(works, "treasurySummary", "", "run-copy");
            Field(works, "publicWorksQuantity", "Whole Planks to deliver", "1");
            Element<TextField>("publicWorksQuantity").RegisterValueChangedCallback(_ => RefreshDevelopment());
            Text(works, "publicWorksQuote", "", "run-copy");
            Button(works, "publicWorksButton", "Deliver Planks for funded public works", () => Run(() =>
                Session.SupplyPublicWorks(RoyalQuantity("publicWorksQuantity"))), "primary");
            var actions = Panel(screen, null, "row run-actions");
            Button(actions, "developmentBackButton", "Back", () => ShowScreen(secondaryParent));
            Button(actions, "developmentLedgerButton", "Value & accounting", () => OpenSecondary(ArchipelagoScreen.Ledger));
            Button(actions, "developmentSaveButton", "Save / Load", OpenSaveSlots);
            BuildSaveSlots();
        }

        private int RoyalQuantity(string field)
        {
            var value = ReadLong(field, "Delivery", false);
            return Session.WholeOrderQuantity(checked((int)value));
        }

        private void RefreshDevelopment()
        {
            if (saveStore == null) return;
            Root.Q<Button>("royalServicesButton")?.EnableInClassList("hidden", Session.Snapshot.Economy == null);
            if (Screen != ArchipelagoScreen.Development) return;
            var world = Session.Snapshot; var economy = world.Economy;
            if (economy == null) { SetText("companyHeadline", "Royal services require an Expanding Economy v3 run."); return; }
            var value = world.CompanyValue(Session.Player.OwnerId);
            var finance = world.Accounting.Companies[Session.Player.OwnerId];
            SetText("companyHeadline", "Cash " + Money(value.Cash) + " · Company Value " + Money(value.Total) +
                " (" + SignedMoney(value.Change) + ") · operating surplus after wages " + SignedMoney(finance.OperatingSurplusAfterWages) +
                "\nSelected ship: " + Session.Player.Name + " · " + Session.Player.Id + " · regional money " + Money(world.RegionalCash));
            for (var i = 0; i < 4; i++)
            {
                Element<VisualElement>("developmentPane" + i).EnableInClassList("hidden", i != developmentTab);
                Element<Button>("developmentTab" + i).EnableInClassList("primary", i == developmentTab);
            }
            var rows = Element<VisualElement>("ownedFleetRows"); rows.Clear();
            foreach (var ship in Session.OwnedShips)
            {
                var id = ship.Id;
                var row = Panel(rows, null, "run-column run-card");
                var location = ship.DockedPortId == null ? "Sailing to " + PortName(ship.DestinationPortId) + " · " + ship.RemainingTravelDays + " days" : "Docked at " + PortName(ship.DockedPortId);
                Text(row, null, ship.Name + " · " + id + "\n" + location + " · cargo " + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) +
                    "\n" + (ship.AutomationEnabled ? Session.DecisionFor(id).Reason : "Manual control") +
                    (ship.IndustrialPolicy == null ? "" : " · " + ship.IndustrialStage), "run-copy");
                var buttons = Panel(row, null, "row");
                Button(buttons, "selectOwned_" + id, id == Session.Player.Id ? "Selected" : "Select ship", () => Run(() => Session.SelectShip(id)));
                Button(buttons, "routeOwned_" + id, UsesItineraries ? "Route planner" : "Legacy route", () =>
                {
                    if (UsesItineraries) { OpenRoutePlanner(id); return; }
                    Session.SelectShip(id); LoadPolicy(); ShowScreen(ArchipelagoScreen.Gameplay);
                    SelectContextTab(2); Element<Foldout>("automationFoldout").SetValueWithoutNotify(true); Refresh();
                });
            }
            var config = economy.Configuration;
            var shipQuote = world.PreviewShipCommission(Session.Player.OwnerId, config.ShipwrightPortId);
            SetText("shipwrightQuote", "Shipwright: " + PortName(config.ShipwrightPortId) + " · " + Money(shipQuote.Payment) +
                " coins · " + Q(shipQuote.Quantity) + " market Planks · household labor " + Money(shipQuote.HouseholdAmount) +
                "\nEmpty ship due day " + shipQuote.CompletionDay + " · capacity " + Q(config.ShipCapacity) + " · cap 3 including orders.\n" + shipQuote.Reason);
            Element<Button>("commissionShipButton").SetEnabled(!Session.IsChallengeEnded && shipQuote.Outcome == DevelopmentOutcome.Started);
            SetText("shipOrders", economy.ShipOrders.Count == 0 ? "No pending ships." : string.Join("\n", economy.ShipOrders.Select(o =>
                o.ShipId + " · paid " + Money(o.PaidCapital) + " · completes day " + o.CompletionDay)));
            var mintQuote = world.PreviewMintCommission(Session.Player.OwnerId);
            SetText("mintSummary", "Public Royal Mint · " + PortName(config.MintPortId) + " · " + economy.MintStatus +
                "\nCommission " + Money(config.MintCommissionCost) + " coins into the treasury · " + config.MintConstructionDays + " days" +
                (economy.MintCompletionDay.HasValue ? " · completion day " + economy.MintCompletionDay : "") +
                "\nSilver source: " + PortName(config.SilverPortId) + ". Physically mine and transport it here." +
                "\nReserve " + Q(economy.SilverReserve) + " Silver · batches " + economy.MintBatches + "/" + config.MaximumMintBatches +
                "\nIssued " + Money(economy.MintedCurrency) + " coins = suppliers " + Money(economy.SupplierIssuance) +
                " + treasury " + Money(economy.TreasuryIssuance) +
                (economy.MintStatus == MintStatus.Active ? ". Next batch date " + economy.NextMintDay + " (requires delivered Silver and remaining authorization)." :
                    ". Issuance requires the Mint to become active.") +
                "\nPublic property is excluded from Company Value. " + mintQuote.Reason);
            Element<Button>("commissionMintButton").SetEnabled(!Session.IsChallengeEnded && mintQuote.Outcome == DevelopmentOutcome.Started);
            PreviewRoyal("mintExchangeQuote", "mintSilverButton", () => world.PreviewMintExchange(Session.Player.Id));
            var ids = economy.Contracts.Select(c => c.Configuration.Id).ToArray();
            if (!royalContractIds.SequenceEqual(ids))
            {
                royalContractIds.Clear(); royalContractIds.AddRange(ids);
                SetChoices("royalContractPicker", economy.Contracts.Select(c => Session.Content.GetGood(c.Configuration.GoodId).Name + " · " + c.Configuration.Id).ToList());
            }
            var chosen = economy.Contracts[Element<DropdownField>("royalContractPicker").index];
            SetText("externalSummary", "External funds remaining " + Money(economy.ExternalCash) + " · transferred into region " + Money(economy.ExternalNetInflow) +
                "\nDeliver at " + PortName(chosen.Configuration.PortId) + " · " + Q(chosen.RemainingQuantity) + " units remaining at " + chosen.Configuration.UnitPrice +
                " coins/unit · days " + chosen.Configuration.OpensDay + "–" + chosen.Configuration.DeadlineDay +
                "\nExported goods leave the region. Existing external money is transferred, never minted or automatically refilled.");
            PreviewRoyal("exportQuote", "fulfillContractButton", () => world.PreviewExternalContract(Session.Player.Id, chosen.Configuration.Id, RoyalQuantity("exportQuantity")));
            SetText("treasurySummary", "Treasury " + Money(economy.TreasuryCash) + " coins · public works spending " + Money(economy.TreasuryExpenditure) +
                "\nDelivered " + Q(economy.TreasuryPlanks) + "/" + Q(config.TreasuryPlankRequirement) + " Planks · " + config.TreasuryPlankPrice +
                " coins/unit at " + PortName(config.MintPortId) + ". Only actual delivered cargo is paid and consumed.");
            PreviewRoyal("publicWorksQuote", "publicWorksButton", () => world.PreviewTreasuryProcurement(Session.Player.Id, RoyalQuantity("publicWorksQuantity")));
        }

        private void PreviewRoyal(string label, string button, Func<DevelopmentAttempt> preview)
        {
            try
            {
                var attempt = preview();
                SetText(label, attempt.Reason + "\nSupplier payment " + Money(attempt.Payment) + " coins" +
                    (attempt.Kind == DevelopmentKind.MintExchange ? " · new treasury coins " + Money(attempt.TreasuryAmount) : "") +
                    (Session.Player.AutomationEnabled ? "\nPause this ship's automation before a manual delivery." : ""));
                Element<Button>(button).SetEnabled(attempt.Outcome == DevelopmentOutcome.Completed && !Session.Player.AutomationEnabled && !Session.IsChallengeEnded);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException || exception is OverflowException)
            { SetText(label, exception.Message); Element<Button>(button).SetEnabled(false); }
        }

        private void BuildSaveSlots()
        {
            var screen = CreateScreen(ArchipelagoScreen.Saves, "Local voyages", "Three local slots on this device. Load validates the entire save before replacing the current session. Retry creates a fresh world.");
            var scroll = new ScrollView { name = "saveSlotsScroll" }; scroll.AddToClassList("run-content-scroll"); screen.Add(scroll);
            Panel(scroll, "saveSlotRows", "");
            Button(screen, "savesBackButton", "Back", () => ShowScreen(secondaryParent));
            AddDiagnosticExport(screen, "savesExportButton");
        }

        private void AddDiagnosticExport(VisualElement parent, string id)
        {
            if (!Application.isEditor && !Debug.isDebugBuild) return;
            Button(parent, id, "Export Run Report · diagnostics", ExportRunReport);
            Text(parent, id + "Status", "", "run-footnote");
        }

        private void ExportRunReport()
        {
            if (!HasActiveRun || Session.Identity == null) { Validation("Start or load a named run before exporting diagnostics."); return; }
            try
            {
                var store = new RunReportExportStore(Path.Combine(Application.persistentDataPath, "Exports"), Session.Content);
                var result = store.Export(Session.SaveText(), Session.Snapshot,
                    Session.Activity.Select(entry => "Day " + entry.Day + " · " + entry.Message));
                var message = "Exact save: " + result.SavePath + "\nDiagnostic report: " + result.ReportPath;
                foreach (var id in new[] { "savesExportButton", "resultsExportButton", "ledgerExportButton" })
                    if (Root.Q<Label>(id + "Status") is Label label) label.text = message;
                Debug.Log("Salt & Ledger diagnostic export\n" + message);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                exception is ArgumentException || exception is InvalidOperationException || exception is WorldSaveException)
            { Validation("Diagnostic export failed: " + exception.Message); }
        }
        private void OpenSaveSlots()
        {
            OpenSecondary(ArchipelagoScreen.Saves);
            RefreshSaveSlots();
        }
        private void RefreshSaveSlots()
        {
            var rows = Element<VisualElement>("saveSlotRows"); rows.Clear();
            foreach (var slot in saveStore.List(Session.CanonicalContent))
            {
                var number = slot.Number;
                var card = Panel(rows, null, "run-card");
                Text(card, null, "Slot " + number + "\n" + slot.Description, "run-copy");
                var buttons = Panel(card, null, "row");
                Button(buttons, "saveSlot" + number, slot.Exists ? "Replace with current run" : "Save current run", () =>
                {
                    void Save() => Run(() => { saveStore.SaveText(number, Session.SaveText()); Session.MarkSaved(); status = "Saved slot " + number + "."; RefreshSaveSlots(); });
                    if (slot.Exists) Confirm("Replace slot " + number + "?", "The previous local save in this slot will be replaced only after the new save is written successfully.", Save);
                    else Save();
                }).SetEnabled(HasActiveRun && Session.Identity != null);
                Button(buttons, "loadSlot" + number, "Load", () => RequestLoadSlot(number)).SetEnabled(slot.IsCompatible);
            }
        }
        private void RequestLoadSlot(int slot)
        {
            if (HasActiveRun && Session.HasUnsavedChanges) Confirm("Load another voyage?", "Unsaved changes in the current run will be replaced after the selected save passes validation.", () => LoadSlot(slot));
            else LoadSlot(slot);
        }
        private void LoadSlot(int slot) => Run(() =>
        {
            Session.LoadSaveText(saveStore.ReadText(slot));
            ClearItineraryDrafts();
            CloseModals(); RefreshRunChoices(); LoadSetupIdentity(Session.Identity);
            HasActiveRun = true; presentedResult = null; tradeShipId = Session.Player.Id; selectedFacilityId = null;
            LoadPolicy(); ShowScreen(ArchipelagoScreen.Gameplay); status = "Loaded slot " + slot + "; paused at the exact saved revision.";
            if (Session.IsChallengeEnded) PresentResults();
        });
        private void ContinueSavedRun()
        {
            var slot = saveStore.LatestCompatibleSlot(Session.CanonicalContent);
            if (slot.HasValue) RequestLoadSlot(slot.Value); else OpenSaveSlots();
        }
        private string LeaveProgressText => Session.HasUnsavedChanges
            ? "Unsaved progress will be discarded. Existing local save slots remain available."
            : "This run has been saved at its current revision. Continue is available from its local save slot.";
    }
}
