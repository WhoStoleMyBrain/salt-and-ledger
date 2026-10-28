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
            SetChoices("harborTradeTarget", new List<string> { "Ship cargo", "Company warehouse" });
            Element<DropdownField>("harborTradeTarget").RegisterValueChangedCallback(_ => RefreshHarbor());
            SetChoices("harborFlowScope", new List<string> { "Town market", "Company warehouse" });
            Element<DropdownField>("harborFlowScope").RegisterValueChangedCallback(_ => RefreshHarbor());
            Click("harborUnloadButton", () => HarborTransfer(TransferDirection.ShipToWarehouse));
            Click("harborLoadButton", () => HarborTransfer(TransferDirection.WarehouseToShip));
            Click("harborMaxUnload", () => MaximumHarborTransfer(TransferDirection.ShipToWarehouse));
            Click("harborMaxLoad", () => MaximumHarborTransfer(TransferDirection.WarehouseToShip));
            Click("harborIndustryButton", () => OpenIndustryFromHarbor());
            Click("harborWarehouseButton", () => OpenIndustryFromHarbor(true));
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
        }

        private void OpenIndustryFromHarbor(bool warehouse = false)
        {
            if (selectedFacilityId != harborFacilityId) loadedPolicyFacilityId = null;
            selectedFacilityId = harborFacilityId;
            SelectIndustryPane(warehouse);
            if (!warehouse) Element<ScrollView>("industryPolicyScroll").scrollOffset = UnityEngine.Vector2.zero;
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
            SetText("tradeHeader", "Company " + Money(Session.Player.Cash) + " · Market buying power " + Money(port.MarketCash) +
                " · " + ship.Name + (local ? " · Docked · cargo " + Q(ship.CargoUnits) + "/" + Q(ship.CargoCapacity) : " · Cargo remote") +
                " · Warehouse " + (stock == null ? "none" : Q(stock.StoredUnits) + "/" + Q(stock.Capacity)));
            SetText("tradeAvailabilityLabel", !ownedShip ? "NPC inspection only" :
                Session.IsChallengeEnded ? "Run concluded · stocks frozen" :
                ship.AutomationEnabled ? "Pause ship automation for cargo orders. Warehouse trades remain available." :
                !local ? "Cargo remote · dock here to load or trade ship goods" : "Docked · instant orders · no day advance");
            var sail = Element<Button>("tradeSailButton");
            sail.EnableInClassList("hidden", local || !ownedShip);
            sail.SetEnabled(manual && ship.DockedPortId != null && Session.HasRoute(ship.DockedPortId, port.Id));
            sail.text = ship.DockedPortId != null && !local && Session.HasRoute(ship.DockedPortId, port.Id)
                ? "Sail here · " + tradeSnapshot.TravelDays(ship.Id, port.Id) + " days" : "At sea · await arrival";
            Element<Button>("harborDisableRoute").EnableInClassList("hidden", !ownedShip || !ship.AutomationEnabled || Session.IsChallengeEnded);
            Element<Button>("harborRoutePlannerButton").EnableInClassList("hidden", !UsesItineraries);
            Element<Button>("harborRoutePlannerButton").SetEnabled(ownedShip);
            RefreshHarborRows(ship, port, stock, local);
            RefreshCommodityIntelligence(ship, port);
            SetText("harborSelectedHoldings", "Ship " + (local ? Q(ship.Cargo[SelectedGoodId()]) : "remote") + " · Warehouse " +
                (stock == null ? "none" : Q(stock.Inventory[SelectedGoodId()])));
            SetText("tradeGoodDescription", Session.Content.GetGood(SelectedGoodId()).Description);
            SetText("harborStockExplanation", stock == null ? "Construct an eligible local facility to gain finite company storage." :
                "One town stockpile. All your local facilities draw inputs and deposit output here; capacity includes paid pending sites.");
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
                        text = preview == null ? "No company warehouse" :
                            (direction == TradeDirection.Buy ? "Pay " : "Gross ") + Money(preview.TotalAmount) +
                            (preview.Outcome == TradeOutcome.Traded ? direction == TradeDirection.Sell ?
                                "\nFee " + Money(preview.ServiceFee) + " · Net " + Money(preview.TraderCashChange.Delta) : "" :
                                "\n" + TradeRemedy(preview.Outcome, HarborUsesLocalStock));
                    }
                    else if (valid) text = "Whole cargo only · use Warehouse for fractional trades.";
                }
                catch (OverflowException) { text = "Numeric limit exceeded. Reduce this order."; }
                SetText(prefix + "PreviewLabel", text);
                SetText(prefix + "Calculation", preview?.Price == null ? "No executable priced order." :
                    "First marginal " + Number(preview.Price.FirstMarginalPrice) + " → last " + Number(preview.Price.LastMarginalPrice) +
                    "; exact total " + Money(preview.TotalAmount) + "; average " + AverageExecutionPrice(preview) + "." + ServiceFeeText(preview));
                Element<Button>(prefix + "Button").text = direction == TradeDirection.Buy ? "Market → " + (HarborUsesLocalStock ? "Warehouse" : "Ship") :
                    (HarborUsesLocalStock ? "Warehouse" : "Ship") + " → Market";
                Element<Button>(prefix + "Button").SetEnabled(TradeIsOpen && canTrade && preview?.Outcome == TradeOutcome.Traded && (HarborUsesLocalStock || whole));
            }
            Element<Button>("quantityMaxBuy").SetEnabled(canTrade);
            Element<Button>("quantityMaxSell").SetEnabled(canTrade);
            RefreshHarborTransfers(ship, local, stock != null, manual, valid, whole, amount);
            RefreshModalStatus();
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
                        reason = executable ? "Ready · no cash" : preview.Outcome == TransferOutcome.InsufficientInventory ? "Source stock short" :
                            "Destination full";
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
                default: return EconomicText.Outcome(outcome) + ". Adjust the good or order quantity.";
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
