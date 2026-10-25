using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private readonly List<string> logisticsFacilities = new List<string>();
        private readonly List<string> logisticsShips = new List<string>();

        private void BuildLedgerAndLogistics()
        {
            var ledger = CreateScreen(ArchipelagoScreen.Ledger, "Company accounts",
                "Actual money paid, historical material basis, and held physical assets. Market quotes are not asset valuations.");
            var scroll = new ScrollView { name = "ledgerScroll" };
            scroll.AddToClassList("run-content-scroll"); ledger.Add(scroll);
            Text(scroll, "ledgerFinances", "", "run-objective");
            Text(scroll, "ledgerAssets", "", "run-copy");
            Text(scroll, "ledgerOperations", "", "run-copy");
            Text(scroll, null, "FIFO costing follows arrival at each stockpile. Partial lots retain exact integer remainders. Transfers keep basis and ownership; manufacture carries consumed material expenditure into output. Starter grants have zero paid basis. Living wages and modern port handling fees are separate operating expenses. Wages include work whose output is still held. V3 Company Value uses stable reference values, not promised liquidation proceeds. Voyage costs and depreciation are not modeled.", "run-footnote");
            var history = new Foldout { name = "ledgerHistory", text = "This session's recent financial events · 200 maximum", value = false };
            scroll.Add(history);
            Panel(history, "ledgerEvents", "");
            Button(ledger, "ledgerBackButton", "Back", () => ShowScreen(secondaryParent));
            AddDiagnosticExport(ledger, "ledgerExportButton");

            var logistics = CreateScreen(ArchipelagoScreen.Logistics, "Industrial shipping",
                "Connect private stock to your ship and a destination. A route never moves goods remotely or overrides cash, prices or capacity.");
            var body = new ScrollView { name = "logisticsScroll" };
            body.AddToClassList("run-content-scroll"); logistics.Add(body);
            Text(body, "logisticsShipLabel", "", "run-objective");
            Dropdown(body, "logisticsShip", "Assigned player ship", new List<string> { Session.Player.Name });
            Element<DropdownField>("logisticsShip").RegisterValueChangedCallback(_ => Run(() =>
            {
                var index = Element<DropdownField>("logisticsShip").index;
                if (index < 0 || index >= logisticsShips.Count) return;
                Session.SelectShip(logisticsShips[index]);
                LoadPolicy();
                LoadSelectedShipLogistics();
            }));
            var columns = Panel(body, null, "run-columns");
            var source = Panel(columns, null, "run-column run-card");
            Dropdown(source, "logisticsFacility", "Source warehouse", new List<string> { "No owned warehouses" });
            Dropdown(source, "logisticsGood", "Cargo", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            Field(source, "logisticsQuantity", "Load up to", "8");
            Field(source, "logisticsReserve", "Leave in source storage", "0");
            Button(source, "logisticsPresetButton", "Bakery shipping preset", LoadLogisticsPreset);
            var procurementPreview = RecommendedIndustryPolicy(EnterpriseScenario.BakeryBlueprint);
            Text(source, "logisticsProcurementPreview", "INPUT PRESET BEFORE APPLYING\n" +
                string.Join("\n", procurementPreview.Inputs.Select(rule => "Buy " + Session.Content.GetGood(rule.GoodId).Name +
                    " toward " + Q(rule.TargetQuantity) + " units, worst marginal price at most " + Number(rule.MaxBuyPrice))) +
                "\nRetain company cash " + Money(procurementPreview.CashReserve) +
                ". Local output sales stay off. Applying authorizes future daily purchases; it places no order now.", "run-footnote");
            Button(source, "logisticsProcurementButton", "Apply bakery input procurement", () => Run(() =>
            {
                var facility = SelectedLogisticsFacility();
                if (facility.Blueprint.Id != EnterpriseScenario.BakeryBlueprint)
                    throw new InvalidOperationException("This preset is for a bakery. Configure other recipes in Your industries.");
                Session.ConfigureFacility(facility.Id, RecommendedIndustryPolicy(EnterpriseScenario.BakeryBlueprint), true);
                SetText("logisticsDraft", "Bakery input procurement applied. Automatic local output sales are off, retaining bread for shipping.");
            }));
            Text(source, null, "Procurement and shipping are separate decisions. Configure precise input ceilings and factory output reserves in Your industries.", "run-footnote");
            var destination = Panel(columns, null, "run-column run-card");
            Dropdown(destination, "logisticsDestination", "Destination", portIds.Select(PortName).ToList());
            Dropdown(destination, "logisticsDeliveryKind", "Deliver to", new List<string> { "Public market sale", "Owned private warehouse" });
            Dropdown(destination, "logisticsDestinationFacility", "Destination warehouse", new List<string> { "No owned warehouses" });
            Field(destination, "logisticsSellFloor", "Minimum marginal sale price", "24");
            Dropdown(destination, "logisticsReturnMode", "Return journey", new List<string>
            {
                "Return empty", "Buy goods into source warehouse", "Buy goods for source market",
                "Load destination warehouse into source warehouse", "Load destination warehouse for source market"
            });
            var advanced = new Foldout { name = "logisticsAdvanced", text = "Advanced thresholds and return cargo", value = false };
            body.Add(advanced);
            var advancedColumns = Panel(advanced, null, "run-columns");
            var limits = Panel(advancedColumns, null, "run-column");
            Field(limits, "logisticsMinimum", "Minimum shipment", "4");
            Field(limits, "logisticsThreshold", "Wait until source stock reaches", "8");
            Field(limits, "logisticsCashReserve", "Company cash reserve", "100");
            var empty = new Toggle("Allow empty return when no return purchase is available") { name = "logisticsAllowEmpty", value = true };
            limits.Add(empty);
            var back = Panel(advancedColumns, null, "run-column");
            Dropdown(back, "logisticsReturnGood", "Return good", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            Field(back, "logisticsReturnQuantity", "Return quantity", "2");
            Field(back, "logisticsReturnBuy", "Return purchase ceiling", "30");
            Field(back, "logisticsReturnSell", "Return sale floor", "1");
            Field(back, "logisticsReturnBudget", "Return purchase expenditure cap", "100");
            Field(back, "logisticsReturnReserve", "Destination warehouse reserve", "0");
            Text(body, "logisticsDraft", "Editing is passive. Apply assigns the configured route; enablement controls whether it acts.", "run-copy");
            Text(body, "logisticsStatus", "", "run-objective");
            var enabled = new Toggle("Enable this ship's automation") { name = "logisticsEnabled" };
            body.Add(enabled);
            enabled.RegisterValueChangedCallback(change => Run(() => Session.SetAutomationEnabled(change.newValue)));
            var actions = Panel(logistics, null, "run-actions row");
            Button(actions, "logisticsBackButton", "Back", () => ShowScreen(secondaryParent));
            Button(actions, "applyLogisticsButton", "Apply shipping policy", ApplyLogistics, "primary");
            Button(actions, "logisticsRunButton", "Run dockside actions", () => Run(() => Session.RunDockside()));
            Element<DropdownField>("logisticsFacility").RegisterValueChangedCallback(_ => RefreshLogisticsStatus());
            Element<DropdownField>("logisticsDeliveryKind").RegisterValueChangedCallback(_ => RefreshLogisticsStatus());
            Element<DropdownField>("logisticsReturnMode").RegisterValueChangedCallback(_ => RefreshLogisticsStatus());
        }

        private string FinancialText()
        {
            var snapshot = Session.Snapshot;
            var finance = snapshot.Accounting.Companies[Session.Player.OwnerId];
            var living = snapshot.Ports.Values.Any(port => port.City != null);
            var value = snapshot.Economy == null ? null : snapshot.CompanyValue(Session.Player.OwnerId);
            return "LIQUID CASH  " + Money(Session.Player.Cash) +
                (value == null ? "" : "   |   COMPANY VALUE  " + Money(value.Total) +
                    "   |   CHANGE  " + SignedMoney(value.Change)) + "\n" +
                "Sale revenue  " + Money(finance.SalesRevenue) + "   |   Inventory purchases  " + Money(finance.InventoryPurchases) +
                "\nCost of goods sold  " + Money(finance.CostOfGoodsSold) +
                (living ? "   |   Realized material margin  " : "   |   Realized gross margin  ") + SignedMoney(finance.RealizedGrossMargin) +
                "\nHeld inventory material basis  " + Money(finance.HeldInventoryCost) +
                "   |   Construction expenditure  " + Money(finance.ConstructionExpenditure) +
                (living ? "\nPaid wages (operating expense)  " + Money(finance.WagesPaid) +
                    "\nOPERATING SURPLUS AFTER WAGES  " + SignedMoney(finance.OperatingSurplusAfterWages) +
                    "\nThis subtracts wages for unsold output too; capital and unmodeled costs remain separate." : "") +
                (snapshot.Circulation == null ? "" : "\nPort service fees paid  " + Money(finance.FeesPaid) +
                    "\nSURPLUS AFTER WAGES AND FEES  " + SignedMoney(finance.OperatingSurplusAfterExpenses) +
                    "\nFees are operating expenses, separate from material FIFO, capital and Mint receipts.") +
                (value == null ? "" : "\nMint supplier receipts  " + Money(finance.MintReceipts) +
                    "   |   Surrendered Silver material basis  " + Money(finance.MintMaterialCost) +
                    "\nPublic Mint commissioning paid  " + Money(finance.PublicCommissionExpenditure) +
                    ". Issuance receipts are separate from trading margin and operating surplus.") +
                "\nStarting cash  " + Money(finance.InitialCash) + "   |   Cash change  " + SignedMoney(Session.CashChange);
        }
        private string ValueText()
        {
            if (Session.Snapshot.Economy == null) return "";
            var value = Session.Snapshot.CompanyValue(Session.Player.OwnerId);
            return "REFERENCE COMPANY VALUE\nCash " + Money(value.Cash) + " + owned goods " + Money(value.Goods) +
                " + completed ships " + Money(value.Ships) + " + operational factories " + Money(value.Facilities) +
                " + paid construction in progress " + Money(value.PendingCapital) + " = " + Money(value.Total) +
                "\nStarting value " + Money(value.InitialValue) + " | change " + SignedMoney(value.Change) +
                "\nStable reference prices; buyers and liquidation proceeds are not guaranteed. Starter gifts count at reference value with zero paid basis. Public Mint, treasury, markets, households and other companies are excluded. FIFO paid basis is not added again.\n\n";
        }
        private string AssetText()
        {
            var snapshot = Session.Snapshot;
            var finance = snapshot.Accounting.Companies[Session.Player.OwnerId];
            var stores = snapshot.Accounting.Stores.Values.Where(s => s.OwnerId == Session.Player.OwnerId);
            var facilities = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId).ToArray();
            return ValueText() + "HELD GOODS\n" + QuantityTotals(finance.HeldInventoryQuantities) +
                "\n\nSTOCKPILES\n" + string.Join("\n", stores.Select(s =>
                    (s.StoreId.StartsWith("ship:", StringComparison.Ordinal) ? "Ship " + snapshot.Ships[s.StoreId.Substring(5)].Name :
                        snapshot.CompanyStockpiles.ContainsKey(s.StoreId) ? "Company stock at " + PortName(snapshot.CompanyStockpiles[s.StoreId].PortId) :
                        "Private " + Session.Content.GetBuilding(snapshot.PrivateFacilities[s.StoreId.Substring(9)].Blueprint.BuildingId).Name +
                        " at " + PortName(snapshot.PrivateFacilities[s.StoreId.Substring(9)].PortId)) +
                    " · " + Q(s.TotalUnits) + " units · basis " + Money(s.TotalCostBasis) +
                    "\n" + (s.Goods.Count == 0 ? "Empty" : string.Join("; ", s.Goods.Values.Select(g =>
                        Q(g.Quantity) + " " + Session.Content.GetGood(g.GoodId).Name + " · basis " + Money(g.CostBasis)))))) +
                "\n\nOWNED FACTORIES  " + facilities.Length + "\n" + (facilities.Length == 0 ? "None" :
                    string.Join("\n", facilities.Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " [" + f.Id + "] at " +
                        PortName(f.PortId) + (f.IsStarterGrant ? " · operational starter grant" :
                            " · ordered day " + f.OrderedDay + (f.IsOperational ? " · completed day " + f.ConstructionCompletedDay :
                            " · under construction, completes day " + f.ConstructionCompletionDay)) +
                        " · first eligible day " + f.FirstProductionDay + " · " + f.BatchesProduced + " batches"))) +
                "\n\nSTARTER GOODS GRANTED\n" + QuantityTotals(finance.GrantedQuantities);
        }
        private string OperationsText()
        {
            var finance = Session.Snapshot.Accounting.Companies[Session.Player.OwnerId];
            return "PRODUCTION\n" + QuantityTotals(finance.ProducedQuantities) +
                "\nInputs consumed: " + QuantityTotals(finance.ConsumedQuantities) +
                "\nGoods sold to markets or authorized procurement: " + QuantityTotals(finance.SoldQuantities) +
                "\n\nSINCE START / LOAD OF THIS SESSION\nShip deliveries to public markets: " + QuantityTotals(Session.ShipDeliveries) +
                "\nCommitted instant player commands: " + Session.PlayerInputs + " · Automated player trade/transfer/departure decisions: " + Session.AutomatedActions +
                "\n\n" + ObjectiveText();
        }
        private void RefreshLedger()
        {
            SetText("ledgerFinances", FinancialText());
            SetText("ledgerAssets", AssetText());
            SetText("ledgerOperations", OperationsText());
            var list = Element<VisualElement>("ledgerEvents"); list.Clear();
            foreach (var item in Session.Journal)
                Text(list, null, "Day " + item.Day + " / revision " + item.Revision + " · " + item.Kind +
                    (item.GoodId == null ? item.RecipeId == null ? "" : " · " + Session.Content.GetRecipe(item.RecipeId).Name :
                        " · " + Q(item.Quantity) + " " + Session.Content.GetGood(item.GoodId).Name) +
                    "\nCash " + SignedMoney(item.CompanyCashDelta) + " · attributed basis " + Money(item.AllocatedCost) +
                    (item.PortId == null ? "" : " · " + PortName(item.PortId)) +
                    "\n" + (item.SourceStoreId ?? "Grant") + " → " + (item.DestinationStoreId ?? "—"), "ledger-event");
            if (Session.Journal.Count == 0) Text(list, null, "No financial events recorded since this session started or loaded. Lifetime totals and starter grants remain in the summary.", "run-copy");
        }

        private void OpenLogistics()
        {
            secondaryParents[ArchipelagoScreen.Logistics] = Screen;
            secondaryParent = Screen;
            Session.Pause();
            var owned = Session.Snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId).ToArray();
            logisticsFacilities.Clear(); logisticsFacilities.AddRange(owned.Select(f => f.Id));
            var names = owned.Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " [" + f.Id + "] at " + PortName(f.PortId)).ToList();
            if (names.Count == 0) names.Add("Construct a private facility first");
            SetChoices("logisticsFacility", names);
            SetChoices("logisticsDestinationFacility", names);
            LoadSelectedShipLogistics();
            ShowScreen(ArchipelagoScreen.Logistics);
            RefreshLogisticsStatus();
        }

        private void LoadSelectedShipLogistics()
        {
            var applied = Session.Player.IndustrialPolicy;
            if (applied != null && applied.Outbound.SourceKind == IndustrialEndpointKind.Warehouse &&
                logisticsFacilities.Contains(applied.Outbound.SourceFacilityId))
                LoadAppliedLogistics(applied);
            else LoadLogisticsPreset();
            RefreshLogisticsStatus();
        }
        private PrivateFacilitySnapshot SelectedLogisticsFacility()
        {
            var index = Element<DropdownField>("logisticsFacility").index;
            if (index < 0 || index >= logisticsFacilities.Count) throw new InvalidOperationException("Construct or select an owned source warehouse first.");
            return Session.Snapshot.PrivateFacilities[logisticsFacilities[index]];
        }
        private void LoadLogisticsPreset()
        {
            if (logisticsFacilities.Count == 0) { RefreshLogisticsStatus(); return; }
            var facility = SelectedLogisticsFacility();
            if (Session.Snapshot.Economy != null && facility.Blueprint.Id == EnterpriseScenario.BakeryBlueprint)
            {
                var generation = ExpandingEconomyGeneration.Generate(Session.Identity.Seed.Value, pricingMode: Session.Identity.LivingPricingMode);
                if (facility.PortId != generation.RecoveryPortId)
                {
                    LoadAppliedLogistics(ExpandingEconomyScenario.IndustrialPolicy(generation, facility.Id, facility.PortId));
                    SetText("logisticsDraft", "V3 draft: ship whole units of private Bread to the recovery town. Apply to this selected ship only; future sales still need real buyer funds.");
                    RefreshLogisticsStatus();
                    return;
                }
            }
            if (Session.Identity?.WorldMode == RunWorldMode.LivingCities && facility.Blueprint.Id == EnterpriseScenario.BakeryBlueprint)
            {
                var generation = LivingCityGeneration.Generate(Session.Identity.Seed.Value, Session.Identity.GeneratorVersion, Session.Identity.LivingPricingMode);
                if (facility.PortId != generation.RecoveryPortId)
                {
                    LoadAppliedLogistics(LivingCityScenario.IndustrialPolicy(generation, facility.Id, facility.PortId));
                    SetText("logisticsDraft", "Living Cities draft: ship private Bread to the vulnerable town using the scenario's stated limits. Apply to commit; current demand is not a promise of payment.");
                    RefreshLogisticsStatus();
                    return;
                }
            }
            var output = Session.Content.GetRecipe(facility.Blueprint.RecipeId).Outputs.First().GoodId;
            var quantity = Math.Min(8, Session.Player.CargoCapacity / Session.Snapshot.QuantityScale);
            SetIndex("logisticsGood", goodIds.IndexOf(output));
            SetIndex("logisticsReturnGood", goodIds.IndexOf("flour"));
            SetIndex("logisticsDestination", portIds.IndexOf(facility.PortId == EnterpriseScenario.East ? EnterpriseScenario.West : EnterpriseScenario.East));
            SetIndex("logisticsDeliveryKind", 0); SetIndex("logisticsReturnMode", 0);
            Element<TextField>("logisticsQuantity").SetValueWithoutNotify(Number(quantity));
            Element<TextField>("logisticsMinimum").SetValueWithoutNotify(Number(Math.Min(4, quantity)));
            Element<TextField>("logisticsThreshold").SetValueWithoutNotify(Number(quantity));
            Element<TextField>("logisticsReserve").SetValueWithoutNotify("0");
            Element<TextField>("logisticsSellFloor").SetValueWithoutNotify(output == "bread" ? "24" : "1");
            SetText("logisticsDraft", "Preset prepared: retain private output, load a shipment, sell at the destination, and return empty. Apply to commit.");
            RefreshLogisticsStatus();
        }
        private void LoadAppliedLogistics(IndustrialRoutePolicy policy)
        {
            var leg = policy.Outbound;
            SetIndex("logisticsFacility", logisticsFacilities.IndexOf(leg.SourceFacilityId));
            SetIndex("logisticsGood", goodIds.IndexOf(leg.GoodId));
            SetIndex("logisticsDestination", portIds.IndexOf(policy.DestinationPortId));
            SetIndex("logisticsDeliveryKind", leg.DestinationKind == IndustrialEndpointKind.Warehouse ? 1 : 0);
            var destinationFacility = leg.DestinationFacilityId ?? policy.ReturnLeg?.SourceFacilityId;
            SetIndex("logisticsDestinationFacility", logisticsFacilities.IndexOf(destinationFacility));
            SetQuantityField("logisticsQuantity", leg.MaximumShipmentQuantity);
            SetQuantityField("logisticsMinimum", leg.MinimumShipmentQuantity);
            SetQuantityField("logisticsReserve", leg.WarehouseReserve);
            SetQuantityField("logisticsThreshold", leg.MinimumSourceStock);
            SetField("logisticsSellFloor", leg.MinimumSellPrice);
            SetMoneyField("logisticsCashReserve", policy.CashReserve);
            Element<Toggle>("logisticsAllowEmpty").SetValueWithoutNotify(policy.AllowEmptyReturn);
            var back = policy.ReturnLeg;
            SetIndex("logisticsReturnMode", back == null ? 0 : back.SourceKind == IndustrialEndpointKind.Warehouse
                ? back.DestinationKind == IndustrialEndpointKind.Warehouse ? 3 : 4
                : back.DestinationKind == IndustrialEndpointKind.Warehouse ? 1 : 2);
            if (back != null)
            {
                SetIndex("logisticsReturnGood", goodIds.IndexOf(back.GoodId));
                SetQuantityField("logisticsReturnQuantity", back.MaximumShipmentQuantity);
                SetField("logisticsReturnBuy", back.MaximumBuyPrice);
                SetField("logisticsReturnSell", back.MinimumSellPrice);
                SetMoneyField("logisticsReturnBudget", back.MaximumPurchaseExpenditure);
                SetQuantityField("logisticsReturnReserve", back.WarehouseReserve);
            }
            SetText("logisticsDraft", "Showing the applied industrial route. Edit and Apply to replace it; opening this panel changes nothing.");
        }
        private void ApplyLogistics() => Run(() =>
        {
            var source = SelectedLogisticsFacility();
            var destination = portIds[Element<DropdownField>("logisticsDestination").index];
            var destinationWarehouse = Element<DropdownField>("logisticsDeliveryKind").index == 1;
            var destinationFacility = logisticsFacilities[Math.Max(0, Element<DropdownField>("logisticsDestinationFacility").index)];
            var outbound = new IndustrialRouteLeg(goodIds[Element<DropdownField>("logisticsGood").index],
                ReadWholeCargo("logisticsQuantity", "Shipment quantity", false), ReadWholeCargo("logisticsMinimum", "Minimum shipment", false),
                IndustrialEndpointKind.Warehouse, destinationWarehouse ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market,
                source.Id, destinationWarehouse ? destinationFacility : null,
                ReadWholeCargo("logisticsThreshold", "Source threshold", true), ReadWholeCargo("logisticsReserve", "Warehouse reserve", true),
                minimumSellPrice: ReadLong("logisticsSellFloor", "Sale floor", false));
            var mode = Element<DropdownField>("logisticsReturnMode").index;
            IndustrialRouteLeg returnLeg = null;
            if (mode > 0)
            {
                var fromWarehouse = mode >= 3;
                var toWarehouse = mode == 1 || mode == 3;
                returnLeg = new IndustrialRouteLeg(goodIds[Element<DropdownField>("logisticsReturnGood").index],
                    ReadWholeCargo("logisticsReturnQuantity", "Return quantity", false), Session.Snapshot.QuantityScale,
                    fromWarehouse ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market,
                    toWarehouse ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market,
                    fromWarehouse ? destinationFacility : null, toWarehouse ? source.Id : null,
                    warehouseReserve: fromWarehouse ? ReadWholeCargo("logisticsReturnReserve", "Return reserve", true) : 0,
                    maximumBuyPrice: ReadLong("logisticsReturnBuy", "Return buy ceiling", false),
                    minimumSellPrice: ReadLong("logisticsReturnSell", "Return sale floor", false),
                    maximumPurchaseExpenditure: ReadIndustryMoney("logisticsReturnBudget", "Return purchase cap", false));
            }
            Session.ApplyIndustrialPolicy(new IndustrialRoutePolicy(source.PortId, destination, outbound, returnLeg,
                ReadIndustryMoney("logisticsCashReserve", "Cash reserve", true), Element<Toggle>("logisticsAllowEmpty").value),
                Session.Player.AutomationEnabled);
            SetText("logisticsDraft", "Shipping policy applied. Enable automation to execute at day boundaries, or run dockside actions explicitly.");
        });
        private void RefreshLogisticsStatus()
        {
            if (Root.Q("logisticsStatus") == null) return;
            var procurement = RecommendedIndustryPolicy(EnterpriseScenario.BakeryBlueprint);
            SetText("logisticsProcurementPreview", "INPUT PRESET BEFORE APPLYING\n" +
                string.Join("\n", procurement.Inputs.Select(rule => "Buy " + Session.Content.GetGood(rule.GoodId).Name +
                    " toward " + Q(rule.TargetQuantity) + " units, worst marginal price at most " + Number(rule.MaxBuyPrice))) +
                "\nRetain company cash " + Money(procurement.CashReserve) +
                ". Local output sales stay off. Applying authorizes future daily purchases; it places no order now.");
            var ship = Session.Player;
            var ownedShips = Session.Snapshot.Ships.Values.Where(item => item.OwnerId == ship.OwnerId)
                .OrderBy(item => item.Id, StringComparer.Ordinal).ToArray();
            logisticsShips.Clear(); logisticsShips.AddRange(ownedShips.Select(item => item.Id));
            var selector = Element<DropdownField>("logisticsShip");
            selector.choices = ownedShips.Select(item => item.Name + " [" + item.Id + "]").ToList();
            selector.SetValueWithoutNotify(selector.choices[logisticsShips.IndexOf(ship.Id)]);
            SetText("logisticsShipLabel", "Assigned ship: " + ship.Name + " · " + Q(ship.CargoCapacity) +
                " cargo units · " + (ship.DockedPortId == null ? "sailing" : "docked at " + PortName(ship.DockedPortId)));
            var decision = Session.CurrentDecision;
            SetText("logisticsStatus", (ship.IndustrialPolicy == null ? "No industrial shipping policy assigned." :
                "Stage: " + ship.IndustrialStage + " · " + (ship.AutomationEnabled ? "enabled" : "disabled")) +
                "\n" + decision.Reason + "\nApply requires an empty ship docked at either route port. Existing cargo is never discarded.");
            Element<Button>("applyLogisticsButton").SetEnabled(logisticsFacilities.Count > 0 && !Session.IsChallengeEnded);
            Element<Button>("logisticsPresetButton").SetEnabled(logisticsFacilities.Count > 0);
            Element<Button>("logisticsProcurementButton").SetEnabled(logisticsFacilities.Count > 0 && !Session.IsChallengeEnded &&
                SelectedLogisticsFacility().Blueprint.Id == EnterpriseScenario.BakeryBlueprint);
            Element<Toggle>("logisticsEnabled").SetValueWithoutNotify(ship.AutomationEnabled);
            Element<Toggle>("logisticsEnabled").SetEnabled(!Session.IsChallengeEnded && ship.IndustrialPolicy != null);
            Element<Button>("logisticsRunButton").SetEnabled(ship.AutomationEnabled && ship.IndustrialPolicy != null && !Session.IsChallengeEnded);
            Element<DropdownField>("logisticsDestinationFacility").SetEnabled(logisticsFacilities.Count > 0 &&
                (Element<DropdownField>("logisticsDeliveryKind").index == 1 || Element<DropdownField>("logisticsReturnMode").index >= 3));
        }
        private static void Field(VisualElement parent, string name, string label, string value) =>
            parent.Add(new TextField(label) { name = name, value = value });
        private void SetQuantityField(string name, long value) => Element<TextField>(name).SetValueWithoutNotify(Q(value));
        private void SetMoneyField(string name, long value) => Element<TextField>(name).SetValueWithoutNotify(Money(value));
        private void SetField(string name, long value) => Element<TextField>(name).SetValueWithoutNotify(Number(value));
        private static void Dropdown(VisualElement parent, string name, string label, List<string> choices)
        {
            var field = new DropdownField(label, choices, 0) { name = name };
            parent.Add(field);
        }
    }
}
