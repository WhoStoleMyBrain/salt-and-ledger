using System;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public enum HarborPair { MarketShip, MarketWarehouse, WarehouseShip }

    public sealed partial class ArchipelagoLabController
    {
        public HarborPair TradingPair { get; private set; }
        private bool approvedTradingBuilt;
        private bool returnToFactory;
        private bool changingPair;

        private void BuildApprovedTrading()
        {
            var dialog = Element<VisualElement>("tradeDialog");
            dialog.AddToClassList("ux-trading");
            var heading = dialog.Q(className: "modal-heading");
            heading.Q(className: "eyebrow")?.RemoveFromHierarchy();
            Element<Label>("tradeDay").AddToClassList("hidden");
            var tabs = Panel(heading, "approvedTradeTabs", "ux-trade-tabs");
            heading.Insert(Math.Max(1, heading.childCount - 1), tabs);
            foreach (HarborPair pair in Enum.GetValues(typeof(HarborPair)))
            {
                var captured = pair;
                var button = Button(tabs, "pair" + pair, pair == HarborPair.MarketShip ? "Market ↔ Ship" :
                    pair == HarborPair.MarketWarehouse ? "Market ↔ Store" : "Store ↔ Ship", () => SelectTradingPair(captured));
                button.tooltip = pair == HarborPair.WarehouseShip ? "Move your goods at the actual dock. No payment." : "Exact paid market trades.";
            }
            var quantities = Panel(dialog, "approvedQuantityBar", "ux-quantity-bar");
            dialog.Insert(dialog.IndexOf(Element<Label>("tradeHeader")) + 1, quantities);
            Text(quantities, null, "ORDER QUANTITY", "ux-eyebrow");
            foreach (var id in new[] { "quantityMinus", "quantityField", "quantityPlus", "quantityOne", "quantityFive" }) quantities.Add(Root.Q(id));
            Element<TextField>("quantityField").label = "";
            Button(quantities, "quantityTen", "10", () => SetQuantity(10));
            quantities.tooltip = "Click a commodity arrow to execute this quantity. Each arrow's tooltip shows its exact payment or sale receipt.";
            var market = dialog.Q(className: "trade-market");
            market.Q(className: "trade-column-header")?.RemoveFromHierarchy();
            foreach (var child in market.Children().OfType<Label>().ToArray()) child.RemoveFromHierarchy();
            var scope = Element<DropdownField>("harborFlowScope");
            var goods = Element<VisualElement>("tradeGoodsRows"); goods.Clear();
            var caption = Panel(market, "approvedTradeColumns", "ux-trade-columns");
            market.Insert(market.IndexOf(goods), caption);
            Text(caption, "approvedLeftOwner", "", "ux-owner");
            Text(caption, null, "SELL / UNLOAD ←     COMMODITY     → BUY / LOAD", "ux-eyebrow");
            Text(caption, "approvedRightOwner", "", "ux-owner");
            var details = new Foldout { name = "approvedTradeDetails", text = "Selected commodity · exact order, economic details & comparison", value = false };
            market.Add(details);
            details.Add(Element<DropdownField>("goodDropdown"));
            details.Add(scope);
            foreach (var id in new[] { "harborFlowDetails", "harborComparison", "harborProduction" }) details.Add(Root.Q(id));
            var order = dialog.Q(className: "trade-order");
            details.Add(order);
            var actions = Panel(market, "approvedHarborActions", "ux-harbor-actions");
            foreach (var id in new[] { "harborIndustryButton", "harborWarehouseButton", "harborRoutePlannerButton" }) actions.Add(Root.Q(id));
            Element<Button>("closeTradeButton").text = "×";
            heading.Add(Element<Button>("closeTradeButton"));
            Element<DropdownField>("harborTradeTarget").RegisterValueChangedCallback(_ =>
            {
                if (!changingPair && TradingPair != HarborPair.WarehouseShip)
                { TradingPair = HarborUsesLocalStock ? HarborPair.MarketWarehouse : HarborPair.MarketShip; RefreshHarbor(); }
            });
            approvedTradingBuilt = true;
        }

        public void SelectTradingPair(HarborPair pair)
        {
            TradingPair = pair;
            changingPair = true;
            SetIndex("harborTradeTarget", pair == HarborPair.MarketWarehouse ? 1 : 0);
            SetIndex("harborFlowScope", pair == HarborPair.WarehouseShip ? 1 : 0);
            changingPair = false;
            RefreshHarbor();
        }

        private void OpenWarehousePurchase(string goodId, decimal? suggestedQuantity = null)
        {
            OpenTrade(Session.SelectedPortId);
            returnToFactory = true;
            SelectTradingPair(HarborPair.MarketWarehouse);
            SetIndex("goodDropdown", goodIds.IndexOf(goodId));
            if (suggestedQuantity.HasValue) Element<TextField>("quantityField").SetValueWithoutNotify(
                suggestedQuantity.Value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            Element<Button>("closeTradeButton").tooltip = "Back to factory · the purchase does not advance production time.";
            Refresh();
        }

        private bool ReturnToFactoryAfterPurchase()
        {
            if (!returnToFactory) return false;
            returnToFactory = false;
            SelectIndustryPane(false);
            ShowModal("industryBackdrop", "closeIndustryButton");
            RefreshIndustry();
            return true;
        }

        private void RefreshApprovedTradeRows(RegionalShipSnapshot ship, RegionalPortSnapshot port, CompanyStockpileSnapshot stock, bool local)
        {
            if (!approvedTradingBuilt) return;
            SetText("tradeTitle", TradingPair == HarborPair.MarketShip ? port.Name + " ↔ " + ship.Name :
                TradingPair == HarborPair.MarketWarehouse ? port.Name + " ↔ Warehouse" : "Warehouse ↔ " + ship.Name);
            SetText("approvedLeftOwner", TradingPair == HarborPair.WarehouseShip ? "COMPANY WAREHOUSE" : "TOWN MARKET");
            SetText("approvedRightOwner", TradingPair == HarborPair.MarketWarehouse ? "COMPANY WAREHOUSE" : ship.Name.ToUpperInvariant() + (local ? " · DOCKED" : " · REMOTE"));
            foreach (HarborPair pair in Enum.GetValues(typeof(HarborPair)))
                Element<Button>("pair" + pair).EnableInClassList("sl-selected", pair == TradingPair);
            var rows = Element<VisualElement>("tradeGoodsRows");
            var preferred = new[] { "bread", "flour", "grain", "logs", "planks", "furniture", "silver" };
            if (rows.childCount == 0) foreach (var id in preferred.Where(goodIds.Contains).Concat(goodIds.Where(id => !preferred.Contains(id))))
            {
                var goodId = id;
                var row = Panel(rows, "tradeGood_" + id, "ux-trade-row");
                Text(row, "left", "", "ux-stock");
                Button(row, "leftArrow", "", () => ExecuteApprovedArrow(goodId, false), "ux-trade-arrow");
                var good = Button(row, "good", "", () => { SetIndex("goodDropdown", goodIds.IndexOf(goodId));
                    RefreshHarbor();
                    Element<Foldout>("approvedTradeDetails").value = true; Element<Foldout>("harborFlowDetails").value = true; }, "ux-trade-good");
                good.Add(new MaritimeGlyph(id));
                var copy = Panel(good, null, "ux-trade-good-copy");
                Text(copy, "name", Session.Content.GetGood(id).Name, "ux-good-name");
                Text(copy, "flow", "", "ux-small");
                Button(row, "rightArrow", "", () => ExecuteApprovedArrow(goodId, true), "ux-trade-arrow");
                Text(row, "right", "", "ux-stock");
            }
            var valid = TryIndustryQuantity("quantityField", out var amount);
            var whole = valid && amount % tradeSnapshot.QuantityScale == 0;
            foreach (var id in goodIds)
            {
                var row = rows.Q("tradeGood_" + id);
                row.Q<Label>("left").text = TradingPair == HarborPair.WarehouseShip ? stock == null ? "—" : Q(stock.Inventory[id]) : Q(port.Inventory[id]);
                row.Q<Label>("right").text = TradingPair == HarborPair.MarketWarehouse ? stock == null ? "—" : Q(stock.Inventory[id]) : Q(ship.Cargo[id]);
                var flow = HarborFlow(port.Id, id);
                row.Q<Label>("flow").text = SignedQuantity(flow.EnabledProductionQuantity) + " / " + SignedQuantity(-flow.UsageQuantity) + " · 12d";
                row.Q<Button>("good").tooltip = FlowRole(flow) + " · " + FlowState(flow) + "\n" + FlowSummary(flow) + "\nSelect for economic details and comparison.";
                row.EnableInClassList("sl-selected", id == SelectedGoodId());
                foreach (var right in new[] { false, true })
                {
                    var button = row.Q<Button>(right ? "rightArrow" : "leftArrow");
                    var enabled = false;
                    var reason = "Enter a positive exact quantity.";
                    var quote = tradeSnapshot.GetMarketQuote(port.Id, id);
                    button.text = TradingPair == HarborPair.WarehouseShip ? right ? "Load →" : "← Unload" : right ?
                        (quote.Ask.HasValue ? Number(quote.Ask.Value) : "—") + " →" : "← " + (quote.Bid.HasValue ? Number(quote.Bid.Value) : "—");
                    if (Session.IsChallengeEnded) reason = "Run concluded · stocks frozen.";
                    else if (TradingPair != HarborPair.MarketWarehouse && (!ship.IsPlayer || ship.Id != Session.Player.Id)) reason = "NPC inspection only. Select your ship.";
                    else if (TradingPair != HarborPair.MarketShip && stock == null) reason = "Complete a depot project or construct local industry for company storage.";
                    else if (TradingPair != HarborPair.MarketWarehouse && !local) reason = "Dock at this town to move cargo.";
                    else if (TradingPair != HarborPair.MarketWarehouse && ship.AutomationEnabled) reason = "Pause ship automation for manual orders.";
                    else if (valid && TradingPair != HarborPair.MarketWarehouse && !whole) reason = "Ship cargo uses whole goods. Warehouse trades allow fractions.";
                    else if (valid)
                    {
                        try
                        {
                            if (TradingPair == HarborPair.WarehouseShip)
                            {
                                var preview = tradeSnapshot.PreviewTransferGoods(Session.Player.OwnerId, ship.Id, harborFacilityId,
                                    right ? TransferDirection.WarehouseToShip : TransferDirection.ShipToWarehouse, id, amount);
                                enabled = preview.Outcome == TransferOutcome.Transferred;
                                reason = enabled ? Q(amount) + " " + Session.Content.GetGood(id).Name + " · no payment · original FIFO basis retained" : preview.Outcome.ToString();
                            }
                            else
                            {
                                var command = new TradeCommand(right ? TradeDirection.Buy : TradeDirection.Sell, id, amount);
                                var preview = TradingPair == HarborPair.MarketWarehouse ? tradeSnapshot.PreviewWarehouseTrade(Session.Player.OwnerId, harborFacilityId, command) :
                                    tradeSnapshot.PreviewTrade(ship.Id, port.Id, command);
                                enabled = preview.Outcome == TradeOutcome.Traded;
                                reason = Q(amount) + " " + Session.Content.GetGood(id).Name + " · exact " + (right ? "payment " : "gross ") + Money(preview.TotalAmount) +
                                    (right ? "" : " · fee " + Money(preview.ServiceFee) + " · net " + Money(preview.TraderCashChange.Delta)) +
                                    (enabled ? "\nClick to execute. Displayed row price is the first marginal price, not the whole order." : "\n" + TradeRemedy(preview.Outcome, TradingPair == HarborPair.MarketWarehouse));
                            }
                        }
                        catch (OverflowException) { reason = "Numeric limit exceeded. Reduce the quantity."; }
                    }
                    button.SetEnabled(TradeIsOpen && enabled);
                    button.tooltip = (right ? TradingPair == HarborPair.WarehouseShip ? "Warehouse → Ship" : TradingPair == HarborPair.MarketWarehouse ? "Market → Warehouse" : "Market → Ship" :
                        TradingPair == HarborPair.WarehouseShip ? "Ship → Warehouse" : TradingPair == HarborPair.MarketWarehouse ? "Warehouse → Market" : "Ship → Market") + "\n" + reason;
                }
            }
        }

        private void ExecuteApprovedArrow(string goodId, bool right)
        {
            // Bind the exact displayed proposal; selecting a row must not refresh away staleness.
            var snapshot = tradeSnapshot;
            var pair = TradingPair;
            var facility = harborFacilityId;
            var amount = ReadIndustryQuantity("quantityField", "Order quantity", false);
            Run(() =>
            {
                if (pair == HarborPair.MarketWarehouse)
                    Session.WarehouseTrade(facility, right ? TradeDirection.Buy : TradeDirection.Sell, goodId, amount, snapshot);
                else
                {
                    if (tradeShipId != Session.Player.Id) throw new InvalidOperationException("Select an owned ship to move cargo.");
                    if (pair == HarborPair.WarehouseShip) Session.Transfer(facility, right ? TransferDirection.WarehouseToShip : TransferDirection.ShipToWarehouse, goodId, amount, snapshot);
                    else Session.Trade(right ? TradeDirection.Buy : TradeDirection.Sell, goodId, amount, snapshot);
                }
            });
        }
    }
}
