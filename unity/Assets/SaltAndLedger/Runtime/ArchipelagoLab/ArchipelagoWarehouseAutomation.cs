using System;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private string loadedWarehousePortId;
        private RegionalWorldSnapshot warehousePolicySnapshot;

        private void BindWarehouseAutomation()
        {
            Click("warehouseBuyTab", () => SelectWarehouseDirection(false));
            Click("warehouseSellTab", () => SelectWarehouseDirection(true));
            Click("reloadWarehousePolicy", () => { loadedWarehousePortId = null; RefreshWarehouseAutomation(); });
            Element<TextField>("warehouseCashReserve").RegisterValueChangedCallback(_ => WarehouseDraftChanged());
            Click("applyWarehousePolicy", () => Run(() =>
            {
                Session.ConfigureWarehouse(Session.SelectedPortId, ReadWarehouseDraft(), warehousePolicySnapshot);
                SetText("warehousePolicyDraftLabel", "Warehouse rules applied. Purchases precede production; sales follow it on future days.");
            }));
            SelectWarehouseDirection(false);
        }

        private void SelectWarehouseDirection(bool sell)
        {
            Element<VisualElement>("warehouseBuyRows").EnableInClassList("hidden", sell);
            Element<VisualElement>("warehouseSellRows").EnableInClassList("hidden", !sell);
            Element<Button>("warehouseBuyTab").EnableInClassList("selected-tab", !sell);
            Element<Button>("warehouseSellTab").EnableInClassList("selected-tab", sell);
        }

        private CompanyStockpileSnapshot LocalWarehouse() => Session.Snapshot.CompanyStockpiles.Values.FirstOrDefault(
            pool => pool.OwnerId == Session.Player.OwnerId && pool.PortId == Session.SelectedPortId);

        private void RefreshWarehouseAutomation()
        {
            warehousePolicySnapshot = Session.Snapshot;
            var pool = LocalWarehouse();
            SetText("warehouseAutomationSummary", pool == null ? "No company warehouse here. Construct a facility to create real storage." :
                PortName(pool.PortId) + " · shared stock " + Q(pool.StoredUnits) + " / " + Q(pool.Capacity));
            SetText("warehouseAutomationAccounting", pool == null ? "" : "Actual purchases " + Money(pool.ProcurementCost) +
                " · gross sales " + Money(pool.SalesRevenue) + " · held material basis " + Money(warehousePolicySnapshot.Accounting.Stores[pool.Id].TotalCostBasis) +
                "\nLast buy: " + WaitText(pool.ProcurementWaitReason) + " · last sale: " + WaitText(pool.SalesWaitReason));
            Element<ScrollView>("warehouseAutomationScroll").EnableInClassList("hidden", pool == null);
            Element<TextField>("warehouseCashReserve").SetEnabled(pool != null && !Session.IsChallengeEnded);
            Element<Button>("reloadWarehousePolicy").SetEnabled(pool != null);
            if (pool == null)
            {
                loadedWarehousePortId = null;
                SetText("warehousePolicyDraftLabel", "Warehouse rules do not create storage or goods.");
                Element<Button>("applyWarehousePolicy").SetEnabled(false);
                return;
            }
            if (loadedWarehousePortId != pool.PortId)
            {
                Element<TextField>("warehouseCashReserve").SetValueWithoutNotify(Money(pool.Policy.CashReserve));
                BuildWarehouseRows(pool.Policy, false);
                BuildWarehouseRows(pool.Policy, true);
                loadedWarehousePortId = pool.PortId;
                SetText("warehousePolicyDraftLabel", "Applied rules · edits require Apply. Price 0 means no limit.");
            }
            foreach (var goodId in goodIds)
            {
                var flow = EconomicSignals.CommodityFlow(warehousePolicySnapshot, Session.Content, pool.PortId, goodId, pool.OwnerId);
                foreach (var direction in new[] { "Buy", "Sell" })
                {
                    var row = Element<VisualElement>("warehouse" + direction + "Row_" + goodId);
                    row.Q<Label>("flow").text = Session.Content.GetGood(goodId).Name + " · stock " + Q(flow.CurrentStock) +
                        " · " + SignedQuantity(flow.EnabledProductionQuantity) + " output / " + SignedQuantity(-flow.InputUsageQuantity) + " inputs / 12d";
                    row.Q<Label>("flow").tooltip = "Installed output " + Q(flow.InstalledProductionQuantity) + " / 12d. " + FlowSummary(flow) +
                        "\n" + FlowState(flow) + "\n" + string.Join("\n", flow.Production.Select(item => item.Opportunity.FacilityId +
                            ": " + item.Opportunity.OperatingRate + "% · " + item.Opportunity.Priority + " · " +
                            ProductionRemedy(item.Opportunity.CurrentReadiness)));
                }
            }
            RefreshWarehouseDraftPreviews();
        }

        private void BuildWarehouseRows(WarehousePolicy policy, bool sell)
        {
            var prefix = "warehouse" + (sell ? "Sell" : "Buy");
            var rows = Element<VisualElement>(prefix + "Rows"); rows.Clear();
            foreach (var goodId in goodIds)
            {
                var rule = policy.Rules.FirstOrDefault(item => item.GoodId == goodId);
                var row = new VisualElement { name = prefix + "Row_" + goodId }; row.AddToClassList("warehouse-rule");
                var heading = new VisualElement(); heading.AddToClassList("warehouse-rule-heading");
                var glyph = new MaritimeGlyph(goodId); glyph.AddToClassList("sl-glyph-small"); heading.Add(glyph);
                heading.Add(new Label { name = "flow" }); row.Add(heading);
                var fields = new VisualElement(); fields.AddToClassList("warehouse-rule-fields");
                var enabled = new Toggle(sell ? "Sell" : "Buy") { name = prefix + "Enabled_" + goodId,
                    value = sell ? rule?.SellEnabled ?? false : rule?.BuyEnabled ?? false };
                var target = new TextField(sell ? "Retain stock" : "Target stock") { name = prefix + "Target_" + goodId,
                    value = Q(sell ? rule?.SellReserve ?? 0 : rule?.BuyTarget ?? 0) };
                var price = new TextField(sell ? "Min bid (0: any)" : "Max ask (0: any)") { name = prefix + "Price_" + goodId,
                    value = Number(sell ? rule?.MinimumSellPrice ?? 0 : rule?.MaximumBuyPrice ?? 0),
                    tooltip = "Worst marginal unit price, not the order average. 0 removes this limit." };
                enabled.RegisterValueChangedCallback(_ => WarehouseDraftChanged());
                target.RegisterValueChangedCallback(_ => WarehouseDraftChanged());
                price.RegisterValueChangedCallback(_ => WarehouseDraftChanged());
                fields.Add(enabled); fields.Add(target); fields.Add(price); row.Add(fields);
                var preview = new Label { name = prefix + "Preview_" + goodId }; preview.AddToClassList("warehouse-rule-preview"); row.Add(preview);
                rows.Add(row);
            }
        }

        private WarehousePolicy ReadWarehouseDraft() => new WarehousePolicy(
            ReadIndustryMoney("warehouseCashReserve", "Company cash reserve", true), goodIds.Select(goodId =>
                new WarehouseCommodityRule(goodId,
                    Element<Toggle>("warehouseBuyEnabled_" + goodId).value,
                    ReadIndustryQuantity("warehouseBuyTarget_" + goodId, Session.Content.GetGood(goodId).Name + " buy target", true),
                    ReadLong("warehouseBuyPrice_" + goodId, "Maximum ask", true),
                    Element<Toggle>("warehouseSellEnabled_" + goodId).value,
                    ReadIndustryQuantity("warehouseSellTarget_" + goodId, Session.Content.GetGood(goodId).Name + " retain stock", true),
                    ReadLong("warehouseSellPrice_" + goodId, "Minimum bid", true))));

        private void WarehouseDraftChanged()
        {
            SetText("warehousePolicyDraftLabel", "Unapplied warehouse rules · one shared policy for every local facility");
            RefreshWarehouseDraftPreviews();
        }

        private void RefreshWarehouseDraftPreviews()
        {
            if (loadedWarehousePortId == null) return;
            var valid = false;
            try
            {
                var draft = ReadWarehouseDraft();
                foreach (var goodId in goodIds)
                    foreach (var direction in new[] { TradeDirection.Buy, TradeDirection.Sell })
                    {
                        var preview = warehousePolicySnapshot.PreviewWarehouseAutomation(Session.Player.OwnerId, Session.SelectedPortId, direction, goodId, draft);
                        SetText("warehouse" + direction + "Preview_" + goodId, WarehousePreviewText(preview));
                    }
                valid = true;
            }
            catch (ArgumentException exception) { SetText("warehousePolicyDraftLabel", "Invalid draft: " + exception.Message); }
            catch (InvalidOperationException exception) { SetText("warehousePolicyDraftLabel", "Invalid draft: " + exception.Message); }
            catch (OverflowException) { SetText("warehousePolicyDraftLabel", "Invalid draft: quantity or cash exceeds its supported bound."); }
            if (!valid)
                foreach (var goodId in goodIds)
                    foreach (var direction in new[] { "Buy", "Sell" })
                        SetText("warehouse" + direction + "Preview_" + goodId, "Invalid draft · review the message below");
            Element<Button>("applyWarehousePolicy").SetEnabled(valid && !Session.IsChallengeEnded);
        }

        private string WarehousePreviewText(WarehouseTradePreview preview) => preview.Quantity == 0 ?
            preview.Reason == IndustryWaitReason.Disabled ? "Off" :
            preview.Reason == IndustryWaitReason.TargetSatisfied ? "Target met" :
            preview.Reason == IndustryWaitReason.ReserveRetained ? "Retained stock protected" : WaitText(preview.Reason) :
            "Now: " + Q(preview.Quantity) + " for " + Money(preview.TotalAmount) +
            (preview.MarginalPrice > 0 ? " · last marginal " + Number(preview.MarginalPrice) : "") +
            (preview.Trade == null ? "" : ServiceFeeText(preview.Trade)) +
            (preview.Reason == IndustryWaitReason.None ? " · eligible" : " · partial: " + WaitText(preview.Reason)) +
            ". Snapshot preview; other rules share stock/cash.";
    }
}
