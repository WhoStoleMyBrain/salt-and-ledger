using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private readonly List<string> harborFacilityIds = new List<string>();
        private string harborFacilityId;
        private bool HarborUsesLocalStock => Element<DropdownField>("harborTradeTarget").index == 1;

        private void BindHarbor()
        {
            SetChoices("harborTradeTarget", new List<string> { "Selected ship hold", "Company local stock" });
            Element<DropdownField>("harborTradeTarget").RegisterValueChangedCallback(_ => RefreshHarbor());
            Element<DropdownField>("harborLegacyStore").RegisterValueChangedCallback(_ =>
            {
                var index = Element<DropdownField>("harborLegacyStore").index;
                harborFacilityId = index < 0 || index >= harborFacilityIds.Count ? null : harborFacilityIds[index];
                RefreshHarbor();
            });
            Click("harborUnloadButton", () => HarborTransfer(TransferDirection.ShipToWarehouse));
            Click("harborLoadButton", () => HarborTransfer(TransferDirection.WarehouseToShip));
            Click("harborMaxUnload", () => MaximumHarborTransfer(TransferDirection.ShipToWarehouse));
            Click("harborMaxLoad", () => MaximumHarborTransfer(TransferDirection.WarehouseToShip));
            Click("harborIndustryButton", () => OpenIndustryFromHarbor());
            Click("harborDisableRoute", () => Run(() => Session.SetAutomationEnabled(false)));
            Click("harborRoutePlannerButton", () => OpenRoutePlanner(tradeShipId));
            Click("industryHarborButton", () => { var port = Session.SelectedPortId; CloseModals(); OpenTrade(port); SetIndex("harborTradeTarget", 1); RefreshHarbor(); });
        }

        private bool HarborShipIsLocal() => tradeShipId != null && Session.Snapshot.Ships.TryGetValue(tradeShipId, out var ship) &&
            ship.DockedPortId == Session.SelectedPortId;

        private void RefreshHarborStorageSelection()
        {
            var snapshot = Session.Snapshot;
            var owned = snapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId && f.PortId == Session.SelectedPortId)
                .OrderBy(f => f.Id, StringComparer.Ordinal).ToArray();
            harborFacilityIds.Clear(); harborFacilityIds.AddRange(owned.Select(f => f.Id));
            if (!harborFacilityIds.Contains(harborFacilityId)) harborFacilityId = harborFacilityIds.FirstOrDefault();
            var picker = Element<DropdownField>("harborLegacyStore");
            picker.choices = owned.Length == 0 ? new List<string> { "No company store here" } : owned.Select(f =>
                Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " [" + f.Id + "]").ToList();
            picker.SetValueWithoutNotify(picker.choices[Math.Max(0, harborFacilityIds.IndexOf(harborFacilityId))]);
            picker.EnableInClassList("hidden", snapshot.UsesPooledCompanyStorage || owned.Length == 0);
        }

        private void OpenIndustryFromHarbor()
        {
            selectedFacilityId = harborFacilityId; loadedPolicyFacilityId = null;
            ShowModal("industryBackdrop", "closeIndustryButton"); RefreshIndustry();
        }

        private void RefreshHarbor()
        {
            if (Root.Q<DropdownField>("harborTradeTarget") == null) return;
            tradeSnapshot = Session.Snapshot;
            if (tradeShipId == null || !tradeSnapshot.Ships.ContainsKey(tradeShipId)) tradeShipId = Session.Player.Id;
            RefreshHarborStorageSelection();
            var ship = tradeSnapshot.Ships[tradeShipId];
            var port = tradeSnapshot.Ports[Session.SelectedPortId];
            var local = ship.DockedPortId == port.Id;
            var ownedShip = ship.IsPlayer && ship.OwnerId == Session.Player.OwnerId;
            var manual = ownedShip && !ship.AutomationEnabled && !Session.IsChallengeEnded;
            var stock = harborFacilityId == null ? null : tradeSnapshot.GetFacilityStorage(harborFacilityId);
            SetText("tradeTitle", port.Name + " · Harbor");
            SetText("tradeDay", "DAY " + Number(tradeSnapshot.Day));
            SetText("tradeHeader", "Company cash " + Money(Session.Player.Cash) + " · Market cash " + Money(port.MarketCash) +
                "\n" + ship.Name + (local ? " · DOCKED · hold " + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) + " (" + Q(ship.CargoCapacity - ship.CargoUnits) + " free)" : " · REMOTE MARKET, cargo inaccessible") +
                " · " + (stock == null ? "No company store" : (tradeSnapshot.UsesPooledCompanyStorage ? "Company local stock " : "Selected legacy store ") +
                Q(stock.StoredUnits) + "/" + Q(stock.Capacity)));
            SetText("tradeAvailabilityLabel", !ownedShip ? "NPC inspection only. Select your ship for company actions." :
                Session.IsChallengeEnded ? "Run concluded. Stocks are frozen; use Menu to view results or save." :
                ship.AutomationEnabled ? "Ship route enabled: pause its automation for manual cargo orders. Local company stock can still trade." :
                !local ? "Dock at " + port.Name + " to use this ship's cargo. Local company stock can trade here remotely." :
                "Paid trades move cash. Load/unload moves owned goods only. Neither action advances a day.");
            var sail = Element<Button>("tradeSailButton");
            sail.EnableInClassList("hidden", local || !ownedShip);
            sail.SetEnabled(manual && ship.DockedPortId != null && Session.HasRoute(ship.DockedPortId, port.Id));
            sail.text = ship.DockedPortId != null && !local && Session.HasRoute(ship.DockedPortId, port.Id)
                ? "Sail here · " + tradeSnapshot.TravelDays(ship.Id, port.Id) + " days" : "At sea · await arrival";
            Element<Button>("harborDisableRoute").EnableInClassList("hidden", !ownedShip || !ship.AutomationEnabled || Session.IsChallengeEnded);
            Element<Button>("harborRoutePlannerButton").EnableInClassList("hidden", !UsesItineraries);
            Element<Button>("harborRoutePlannerButton").SetEnabled(ownedShip);
            RefreshHarborRows(ship, port, stock, local);
            SetText("tradeGoodDescription", Session.Content.GetGood(SelectedGoodId()).Description);
            SetText("harborStockExplanation", stock == null ? "Construct an eligible local facility to gain finite company storage." :
                tradeSnapshot.UsesPooledCompanyStorage ? "One town stockpile. All your local facilities draw inputs and deposit output here; capacity includes paid pending sites." :
                "Legacy run: each facility has its own stock and FIFO lots. Choose the actual store above; no inventories have been merged.");
            SetText("harborProductionLabel", string.Join("\n", tradeSnapshot.PrivateFacilities.Values.Where(f => f.OwnerId == Session.Player.OwnerId && f.PortId == port.Id)
                .OrderBy(f => f.Id, StringComparer.Ordinal).Select(f => Session.Content.GetBuilding(f.Blueprint.BuildingId).Name + " [" + f.Id + "] · " +
                    (f.IsOperational ? f.Enabled ? ProductionRemedy(tradeSnapshot.PreviewFacilityProduction(f.Id, Session.Content)) : "Disabled — enable its operating policy." :
                    "Construction — ready day " + f.ConstructionCompletionDay + ", first batch day " + f.FirstProductionDay) + " · " + f.BatchesProduced + " batches")));
            var valid = TryIndustryQuantity("quantityField", out var amount);
            var whole = valid && amount % tradeSnapshot.QuantityScale == 0;
            var canTrade = ownedShip && !Session.IsChallengeEnded && (HarborUsesLocalStock ? stock != null : manual && local);
            foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
            {
                var prefix = direction == TradeDirection.Buy ? "buy" : "sell";
                TradeAttempt preview = null;
                var text = "Enter a positive exact quantity.";
                try
                {
                    if (valid && (HarborUsesLocalStock || whole))
                    {
                        preview = HarborUsesLocalStock ? stock == null ? null : tradeSnapshot.PreviewWarehouseTrade(Session.Player.OwnerId, harborFacilityId,
                            new TradeCommand(direction, SelectedGoodId(), amount)) : tradeSnapshot.PreviewTrade(ship.Id, port.Id,
                            new TradeCommand(direction, SelectedGoodId(), amount));
                        text = preview == null ? "No company store. Construct a local facility." :
                            "Total " + Money(preview.TotalAmount) + " · Average " + AverageExecutionPrice(preview) + " / good\n" +
                            (preview.Outcome == TradeOutcome.Traded ? (direction == TradeDirection.Buy ? "Company pays market" : "Gross market payment") : TradeRemedy(preview.Outcome, HarborUsesLocalStock)) +
                            ServiceFeeText(preview);
                    }
                    else if (valid) text = "Ship cargo requires a positive whole quantity. Choose company local stock for fractional trades.";
                }
                catch (OverflowException) { text = "Numeric limit exceeded. Reduce this order."; }
                SetText(prefix + "PreviewLabel", text);
                SetText(prefix + "Calculation", preview?.Price == null ? "No executable priced order." :
                    "First marginal " + Number(preview.Price.FirstMarginalPrice) + " → last " + Number(preview.Price.LastMarginalPrice) +
                    "; exact total " + Money(preview.TotalAmount) + ". Average is display only; the order follows the stock-sensitive curve.");
                Element<Button>(prefix + "Button").text = direction == TradeDirection.Buy ? "Buy → " + (HarborUsesLocalStock ? "local stock" : "ship") :
                    "Sell " + (HarborUsesLocalStock ? "local stock" : "ship cargo");
                Element<Button>(prefix + "Button").SetEnabled(TradeIsOpen && canTrade && preview?.Outcome == TradeOutcome.Traded && (HarborUsesLocalStock || whole));
            }
            Element<Button>("quantityMaxBuy").SetEnabled(canTrade);
            Element<Button>("quantityMaxSell").SetEnabled(canTrade);
            RefreshHarborTransfers(ship, local, stock != null, manual, valid, whole, amount);
            RefreshModalStatus();
        }

        private void RefreshHarborRows(RegionalShipSnapshot ship, RegionalPortSnapshot port, CompanyStockpileSnapshot stock, bool local)
        {
            var rows = Element<VisualElement>("tradeGoodsRows");
            if (rows.childCount == 0)
                foreach (var goodId in goodIds)
                {
                    var captured = goodId;
                    var row = new Button(() => { SetIndex("goodDropdown", goodIds.IndexOf(captured)); RefreshHarbor(); }) { name = "tradeGood_" + goodId };
                    row.AddToClassList("trade-goods-row");
                    var glyph = new MaritimeGlyph(goodId); glyph.AddToClassList("sl-glyph-small"); row.Add(glyph);
                    var name = new Label { name = "name", pickingMode = PickingMode.Ignore }; name.AddToClassList("trade-good-name"); row.Add(name);
                    foreach (var field in new[] { "town", "ship", "local", "bid", "ask" })
                    { var cell = new Label { name = field, pickingMode = PickingMode.Ignore }; cell.AddToClassList("trade-cell"); row.Add(cell); }
                    rows.Add(row);
                }
            foreach (var goodId in goodIds)
            {
                var row = rows.Q<Button>("tradeGood_" + goodId);
                var quote = tradeSnapshot.GetMarketQuote(port.Id, goodId);
                var need = port.Consumption.Where(rule => rule.Configuration.GoodId == goodId)
                    .Sum(rule => port.City == null ? (long)rule.Configuration.PreviewDemand(0, rule.DemandRemainder).Quantity :
                        tradeSnapshot.PreviewHouseholdConsumption(port.Id, rule.Configuration.Id).RequestedQuantity);
                if (need == 0) need = port.Facilities.Where(f => f.Enabled).Sum(f => Session.Content.GetRecipe(f.Configuration.Instance.RecipeId).Inputs
                    .Where(input => input.GoodId == goodId).Sum(input => (long)input.Quantity * tradeSnapshot.QuantityScale));
                var quantity = port.Inventory[goodId];
                var condition = need == 0 ? "— Neutral" : quantity < need ? "! Short" : quantity == need ? "✓ Adequate" : "+ Surplus";
                row.Q<Label>("name").text = Session.Content.GetGood(goodId).Name + "\n" + condition;
                row.tooltip = need == 0 ? "No active household or enabled public-recipe need for this good." :
                    "Market stock " + Q(quantity) + " versus " + Q(need) + " for one configured household/municipal opportunity, or one batch per public facility. This is a stock indicator, not guaranteed affordability.";
                row.EnableInClassList("stock-short", need > 0 && quantity < need);
                row.EnableInClassList("stock-adequate", need > 0 && quantity >= need);
                row.Q<Label>("town").text = Q(quantity);
                row.Q<Label>("ship").text = local ? Q(ship.Cargo[goodId]) : "—";
                var localCell = row.Q<Label>("local");
                var localNeed = stock == null ? 0 : tradeSnapshot.PrivateFacilities.Values.Where(f => f.OwnerId == stock.OwnerId &&
                    f.PortId == stock.PortId && f.Enabled && f.IsOperational &&
                    (tradeSnapshot.UsesPooledCompanyStorage || f.Id == harborFacilityId)).Sum(f =>
                    Session.Content.GetRecipe(f.Blueprint.RecipeId).Inputs.Where(input => input.GoodId == goodId)
                        .Sum(input => (long)input.Quantity * tradeSnapshot.QuantityScale));
                localCell.text = stock == null ? "—" : Q(stock.Inventory[goodId]) + (localNeed == 0 ? "" :
                    stock.Inventory[goodId] < localNeed ? "\n! Short" : stock.Inventory[goodId] == localNeed ? "\n✓ Ready" : "\n+ Surplus");
                localCell.tooltip = localNeed == 0 ? "No enabled operational private recipe currently needs this input." :
                    Q(localNeed) + " needed for one batch of each enabled operational facility using this store. Payroll and deadlines still apply.";
                if (stock != null) row.tooltip += "\nCompany stock: " + Q(stock.Inventory[goodId]) + ". " + localCell.tooltip;
                localCell.EnableInClassList("stock-short", stock != null && localNeed > stock.Inventory[goodId]);
                localCell.EnableInClassList("stock-adequate", stock != null && localNeed > 0 && localNeed <= stock.Inventory[goodId]);
                row.Q<Label>("bid").text = quote.Bid.HasValue ? Number(quote.Bid.Value) : "—";
                row.Q<Label>("ask").text = quote.Ask.HasValue ? Number(quote.Ask.Value) : "—";
                row.EnableInClassList("selected-good", goodId == SelectedGoodId());
            }
        }

        private void RefreshHarborTransfers(RegionalShipSnapshot ship, bool local, bool hasStore, bool manual, bool valid, bool whole, int amount)
        {
            var reasons = new List<string>();
            foreach (var direction in new[] { TransferDirection.ShipToWarehouse, TransferDirection.WarehouseToShip })
            {
                var unloading = direction == TransferDirection.ShipToWarehouse;
                var executable = false;
                var reason = !hasStore ? "Construct a local facility for storage." : !local ? "Dock at this town to move cargo." :
                    !manual ? ship.AutomationEnabled ? "Pause ship automation for manual loading." : "This ship/run is read-only." :
                    !valid || !whole ? "Enter a positive whole cargo quantity." : null;
                if (reason == null)
                {
                    try
                    {
                        var preview = tradeSnapshot.PreviewTransferGoods(Session.Player.OwnerId, ship.Id, harborFacilityId, direction, SelectedGoodId(), amount);
                        executable = preview.Outcome == TransferOutcome.Transferred;
                        reason = executable ? "Ready · no cash" : preview.Outcome == TransferOutcome.InsufficientInventory ? "Not enough source stock. Buy, produce or unload this good first." :
                            "Destination is full. Sell or move goods to free capacity.";
                    }
                    catch (InvalidOperationException exception) { reason = exception.Message; }
                    catch (ArgumentException exception) { reason = exception.Message; }
                }
                Element<Button>(unloading ? "harborUnloadButton" : "harborLoadButton").SetEnabled(executable && TradeIsOpen);
                Element<Button>(unloading ? "harborMaxUnload" : "harborMaxLoad").SetEnabled(hasStore && local && manual);
                reasons.Add((unloading ? "Unload: " : "Load: ") + reason);
            }
            SetText("harborTransferPreview", string.Join("\n", reasons));
        }

        private void HarborTransfer(TransferDirection direction) => Run(() =>
        {
            if (tradeShipId != Session.Player.Id) throw new InvalidOperationException("Select an owned ship to move cargo.");
            Session.Transfer(harborFacilityId, direction, SelectedGoodId(), ReadIndustryQuantity("quantityField", "Cargo quantity", false), tradeSnapshot);
        });

        private void MaximumHarborTransfer(TransferDirection direction) => Run(() =>
        {
            var amount = Session.Snapshot.MaximumTransferQuantity(Session.Player.OwnerId, tradeShipId, harborFacilityId, direction, SelectedGoodId());
            Element<TextField>("quantityField").SetValueWithoutNotify(Q(amount));
        }, false);

        private static string TradeRemedy(TradeOutcome outcome, bool localStock)
        {
            switch (outcome)
            {
                case TradeOutcome.InsufficientMarketInventory: return "Market stock is short. Lower the quantity or deliver/produce this good.";
                case TradeOutcome.InsufficientTraderInventory: return (localStock ? "Local company stock" : "Ship cargo") + " is short. Buy, load or produce this good first.";
                case TradeOutcome.InsufficientTraderFunds: return "Company cash is too low. Reduce the order or earn cash from a funded sale.";
                case TradeOutcome.InsufficientMarketFunds: return "Market does not have enough cash. Reduce the sale or choose another funded customer.";
                case TradeOutcome.InsufficientCargoCapacity: return "Storage is full. Sell/unload goods or add real storage capacity.";
                default: return TradingLabText.Outcome(outcome) + ". Adjust the good or order quantity.";
            }
        }

        private static string ProductionRemedy(ScheduledProductionOutcome outcome)
        {
            switch (outcome)
            {
                case ScheduledProductionOutcome.Produced: return "Ready with current stock and payroll";
                case ScheduledProductionOutcome.InputShortage: return "Inputs short — buy or unload recipe inputs";
                case ScheduledProductionOutcome.PayrollShortage: return "Wages unfunded — earn company cash before the next batch";
                case ScheduledProductionOutcome.OutputLimit: return "Output ceiling — sell or ship output";
                case ScheduledProductionOutcome.WarehouseCapacity: return "Storage full — move goods or build capacity";
                case ScheduledProductionOutcome.Disabled: return "Disabled — enable the operating policy";
                default: return ArchipelagoLabSession.FacilityStatus(outcome);
            }
        }
    }
}
