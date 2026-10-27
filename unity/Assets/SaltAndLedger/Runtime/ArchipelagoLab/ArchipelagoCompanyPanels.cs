using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private void BuildLedger()
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
