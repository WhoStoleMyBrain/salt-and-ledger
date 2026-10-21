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

        private void BuildLedgerAndLogistics()
        {
            var ledger = CreateScreen(ArchipelagoScreen.Ledger, "Company accounts",
                "Actual money paid, historical material basis, and held physical assets. Market quotes are not asset valuations.");
            var scroll = new ScrollView { name = "ledgerScroll" };
            scroll.AddToClassList("run-content-scroll"); ledger.Add(scroll);
            Text(scroll, "ledgerFinances", "", "run-objective");
            Text(scroll, "ledgerAssets", "", "run-copy");
            Text(scroll, "ledgerOperations", "", "run-copy");
            Text(scroll, null, "FIFO costing follows arrival at each stockpile. Partial lots retain exact integer remainders. Transfers keep basis and ownership; manufacture carries consumed material expenditure into output. Starter grants have zero paid basis. Living Cities wages are expensed separately when paid, including work whose output is still held. Transport expenses, depreciation and resale value are not modeled.", "run-footnote");
            var history = new Foldout { name = "ledgerHistory", text = "Recent committed financial events · 200 maximum", value = false };
            scroll.Add(history);
            Panel(history, "ledgerEvents", "");
            Button(ledger, "ledgerBackButton", "Back", () => ShowScreen(secondaryParent));

            var logistics = CreateScreen(ArchipelagoScreen.Logistics, "Industrial shipping",
                "Connect private stock to your ship and a destination. A route never moves goods remotely or overrides cash, prices or capacity.");
            var body = new ScrollView { name = "logisticsScroll" };
            body.AddToClassList("run-content-scroll"); logistics.Add(body);
            Text(body, "logisticsShipLabel", "", "run-objective");
            Dropdown(body, "logisticsShip", "Assigned player ship", new List<string> { Session.Player.Name });
            var columns = Panel(body, null, "run-columns");
            var source = Panel(columns, null, "run-column run-card");
            Dropdown(source, "logisticsFacility", "Source warehouse", new List<string> { "No owned warehouses" });
            Dropdown(source, "logisticsGood", "Cargo", goodIds.Select(id => Session.Content.GetGood(id).Name).ToList());
            Field(source, "logisticsQuantity", "Load up to", "8");
            Field(source, "logisticsReserve", "Leave in source storage", "0");
            Button(source, "logisticsPresetButton", "Bakery shipping preset", LoadLogisticsPreset);
            var procurementPreview = EnterpriseScenario.BakeryPolicy(false);
            Text(source, "logisticsProcurementPreview", "INPUT PRESET BEFORE APPLYING\n" +
                string.Join("\n", procurementPreview.Inputs.Select(rule => "Buy " + Session.Content.GetGood(rule.GoodId).Name +
                    " toward " + rule.TargetQuantity + " units, worst marginal price at most " + Number(rule.MaxBuyPrice))) +
                "\nRetain company cash " + Number(procurementPreview.CashReserve) +
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
            var finance = Session.Snapshot.Accounting.Companies[Session.Player.OwnerId];
            return "LIQUID CASH  " + Number(Session.Player.Cash) + "\n" +
                "Sale revenue  " + Number(finance.SalesRevenue) + "   ·   Inventory purchases  " + Number(finance.InventoryPurchases) +
                "\nCost of goods sold  " + Number(finance.CostOfGoodsSold) +
                (Session.Identity?.WorldMode == RunWorldMode.LivingCities ? "   ·   Realized material margin  " : "   ·   Realized gross margin  ") + Signed(finance.RealizedGrossMargin) +
                "\nHeld inventory material basis  " + Number(finance.HeldInventoryCost) +
                "   ·   Construction expenditure  " + Number(finance.ConstructionExpenditure) +
                (Session.Identity?.WorldMode == RunWorldMode.LivingCities
                    ? "\nPaid wages (operating expense)  " + Number(finance.WagesPaid) +
                      "\nMaterial margin less all paid wages  " + Signed(finance.OperatingSurplusAfterWages) +
                      "\nThis includes wages for unsold output; capital and unmodeled costs remain separate." : "") +
                "\nStarting cash  " + Number(finance.InitialCash) + "   ·   Cash change  " + Signed(Session.CashChange);
        }
        private string AssetText()
        {
            var snapshot = Session.Snapshot;
            var finance = snapshot.Accounting.Companies[Session.Player.OwnerId];
            var stores = snapshot.Accounting.Stores.Values.Where(s => s.OwnerId == Session.Player.OwnerId);
            var facilities = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId).ToArray();
            return "HELD GOODS\n" + QuantityTotals(finance.HeldInventoryQuantities) +
                "\n\nSTOCKPILES\n" + string.Join("\n", stores.Select(s =>
                    (s.StoreId.StartsWith("ship:", StringComparison.Ordinal) ? "Ship " + Session.Player.Name :
                        "Private " + Session.Content.GetBuilding(snapshot.PrivateFacilities[s.StoreId.Substring(9)].Blueprint.BuildingId).Name +
                        " at " + PortName(snapshot.PrivateFacilities[s.StoreId.Substring(9)].PortId)) +
                    " · " + s.TotalUnits + " units · basis " + Number(s.TotalCostBasis) +
                    "\n" + (s.Goods.Count == 0 ? "Empty" : string.Join("; ", s.Goods.Values.Select(g =>
                        g.Quantity + " " + Session.Content.GetGood(g.GoodId).Name + " · basis " + Number(g.CostBasis)))))) +
                "\n\nOWNED FACTORIES  " + facilities.Length + "\n" + (facilities.Length == 0 ? "None" :
                    string.Join("\n", facilities.Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " at " +
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
                "\nGoods sold to public markets (ship and warehouse): " + QuantityTotals(finance.SoldQuantities) +
                "\nShip deliveries to public markets: " + QuantityTotals(Session.ShipDeliveries) +
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
                        " · " + item.Quantity + " " + Session.Content.GetGood(item.GoodId).Name) +
                    "\nCash " + Signed(item.CompanyCashDelta) + " · attributed basis " + item.AllocatedCost +
                    (item.PortId == null ? "" : " · " + PortName(item.PortId)) +
                    "\n" + (item.SourceStoreId ?? "Grant") + " → " + (item.DestinationStoreId ?? "—"), "ledger-event");
            if (Session.Journal.Count == 0) Text(list, null, "No committed financial transactions in this run yet. Starter assets are recorded as grants in the summary.", "run-copy");
        }

        private void OpenLogistics()
        {
            secondaryParent = Screen;
            Session.Pause();
            var owned = Session.Snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId).ToArray();
            logisticsFacilities.Clear(); logisticsFacilities.AddRange(owned.Select(f => f.Id));
            var names = owned.Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " · " + PortName(f.PortId)).ToList();
            if (names.Count == 0) names.Add("Construct a private facility first");
            SetChoices("logisticsFacility", names);
            SetChoices("logisticsDestinationFacility", names);
            var applied = Session.Player.IndustrialPolicy;
            if (applied != null && applied.Outbound.SourceKind == IndustrialEndpointKind.Warehouse &&
                logisticsFacilities.Contains(applied.Outbound.SourceFacilityId))
                LoadAppliedLogistics(applied);
            else LoadLogisticsPreset();
            ShowScreen(ArchipelagoScreen.Logistics);
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
            var quantity = Math.Min(8, Session.Player.CargoCapacity);
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
            SetField("logisticsQuantity", leg.MaximumShipmentQuantity);
            SetField("logisticsMinimum", leg.MinimumShipmentQuantity);
            SetField("logisticsReserve", leg.WarehouseReserve);
            SetField("logisticsThreshold", leg.MinimumSourceStock);
            SetField("logisticsSellFloor", leg.MinimumSellPrice);
            SetField("logisticsCashReserve", policy.CashReserve);
            Element<Toggle>("logisticsAllowEmpty").SetValueWithoutNotify(policy.AllowEmptyReturn);
            var back = policy.ReturnLeg;
            SetIndex("logisticsReturnMode", back == null ? 0 : back.SourceKind == IndustrialEndpointKind.Warehouse
                ? back.DestinationKind == IndustrialEndpointKind.Warehouse ? 3 : 4
                : back.DestinationKind == IndustrialEndpointKind.Warehouse ? 1 : 2);
            if (back != null)
            {
                SetIndex("logisticsReturnGood", goodIds.IndexOf(back.GoodId));
                SetField("logisticsReturnQuantity", back.MaximumShipmentQuantity);
                SetField("logisticsReturnBuy", back.MaximumBuyPrice);
                SetField("logisticsReturnSell", back.MinimumSellPrice);
                SetField("logisticsReturnBudget", back.MaximumPurchaseExpenditure);
                SetField("logisticsReturnReserve", back.WarehouseReserve);
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
                ReadInt("logisticsQuantity", "Shipment quantity", false), ReadInt("logisticsMinimum", "Minimum shipment", false),
                IndustrialEndpointKind.Warehouse, destinationWarehouse ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market,
                source.Id, destinationWarehouse ? destinationFacility : null,
                ReadInt("logisticsThreshold", "Source threshold", true), ReadInt("logisticsReserve", "Warehouse reserve", true),
                minimumSellPrice: ReadLong("logisticsSellFloor", "Sale floor", false));
            var mode = Element<DropdownField>("logisticsReturnMode").index;
            IndustrialRouteLeg returnLeg = null;
            if (mode > 0)
            {
                var fromWarehouse = mode >= 3;
                var toWarehouse = mode == 1 || mode == 3;
                returnLeg = new IndustrialRouteLeg(goodIds[Element<DropdownField>("logisticsReturnGood").index],
                    ReadInt("logisticsReturnQuantity", "Return quantity", false), 1,
                    fromWarehouse ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market,
                    toWarehouse ? IndustrialEndpointKind.Warehouse : IndustrialEndpointKind.Market,
                    fromWarehouse ? destinationFacility : null, toWarehouse ? source.Id : null,
                    warehouseReserve: fromWarehouse ? ReadInt("logisticsReturnReserve", "Return reserve", true) : 0,
                    maximumBuyPrice: ReadLong("logisticsReturnBuy", "Return buy ceiling", false),
                    minimumSellPrice: ReadLong("logisticsReturnSell", "Return sale floor", false),
                    maximumPurchaseExpenditure: ReadLong("logisticsReturnBudget", "Return purchase cap", false));
            }
            Session.ApplyIndustrialPolicy(new IndustrialRoutePolicy(source.PortId, destination, outbound, returnLeg,
                ReadLong("logisticsCashReserve", "Cash reserve", true), Element<Toggle>("logisticsAllowEmpty").value),
                Session.Player.AutomationEnabled);
            SetText("logisticsDraft", "Shipping policy applied. Enable automation to execute at day boundaries, or run dockside actions explicitly.");
        });
        private void RefreshLogisticsStatus()
        {
            if (Root.Q("logisticsStatus") == null) return;
            var procurement = RecommendedIndustryPolicy(EnterpriseScenario.BakeryBlueprint);
            SetText("logisticsProcurementPreview", "INPUT PRESET BEFORE APPLYING\n" +
                string.Join("\n", procurement.Inputs.Select(rule => "Buy " + Session.Content.GetGood(rule.GoodId).Name +
                    " toward " + rule.TargetQuantity + " units, worst marginal price at most " + Number(rule.MaxBuyPrice))) +
                "\nRetain company cash " + Number(procurement.CashReserve) +
                ". Local output sales stay off. Applying authorizes future daily purchases; it places no order now.");
            var ship = Session.Player;
            SetText("logisticsShipLabel", "Assigned ship: " + ship.Name + " · " + ship.CargoCapacity +
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
        private void SetField(string name, long value) => Element<TextField>(name).SetValueWithoutNotify(Number(value));
        private static void Dropdown(VisualElement parent, string name, string label, List<string> choices)
        {
            var field = new DropdownField(label, choices, 0) { name = name };
            parent.Add(field);
        }
    }
}
