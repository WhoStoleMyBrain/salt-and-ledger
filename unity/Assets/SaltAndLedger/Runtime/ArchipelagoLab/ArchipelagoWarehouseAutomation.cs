using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private string loadedWarehousePortId;
        private string selectedWarehouseGood = "bread";
        private bool warehouseSellSelected;
        private string warehouseReserveError;
        private readonly Dictionary<string, string> warehouseDraftErrors = new Dictionary<string, string>(StringComparer.Ordinal);

        private void BindWarehouseAutomation()
        {
            Click("warehouseBuyTab", () => SelectWarehouseDirection(false));
            Click("warehouseSellTab", () => SelectWarehouseDirection(true));
            Click("reloadWarehousePolicy", () =>
            {
                loadedWarehousePortId = null;
                warehouseDraftErrors.Clear(); warehouseReserveError = null;
                RefreshWarehouseAutomation();
            });
            Click("editWarehouseCashReserve", () =>
            {
                Element<VisualElement>("warehouseCashReservePopup").RemoveFromClassList("hidden");
                Element<ScrollView>("warehouseCashReservePopup").scrollOffset = UnityEngine.Vector2.zero;
                Element<TextField>("warehouseCashReserve").Focus();
            });
            Click("closeWarehouseCashReserve", DismissWarehouseReserve);
            Element<TextField>("warehouseCashReserve").RegisterValueChangedCallback(_ => CommitWarehouseReserve());
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != UnityEngine.KeyCode.Escape || !CloseWarehouseReserveFirst()) return;
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(evt =>
            {
                if (CloseWarehouseReserveFirst()) evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            SelectWarehouseDirection(false);
        }

        private bool CloseWarehouseReserveFirst()
        {
            var popup = Element<VisualElement>("warehouseCashReservePopup");
            if (!IndustryIsOpen || popup.ClassListContains("hidden")) return false;
            DismissWarehouseReserve();
            Element<Button>("editWarehouseCashReserve").Focus();
            return true;
        }

        private void DismissWarehouseReserve() => Root.Q<VisualElement>("warehouseCashReservePopup")?.AddToClassList("hidden");

        private void SelectWarehouseDirection(bool sell)
        {
            warehouseSellSelected = sell;
            Element<VisualElement>("warehouseBuyRows").EnableInClassList("hidden", sell);
            Element<VisualElement>("warehouseSellRows").EnableInClassList("hidden", !sell);
            Element<Button>("warehouseBuyTab").EnableInClassList("selected-tab", !sell);
            Element<Button>("warehouseSellTab").EnableInClassList("selected-tab", sell);
            SetText("warehouseTargetHeading", sell ? "KEEP" : "BUY TO");
            SetText("warehousePriceHeading", sell ? "BID FLOOR" : "ASK CEILING");
            if (IndustryIsOpen && industryWarehouseSelected) RefreshWarehouseInspector();
        }

        private CompanyStockpileSnapshot LocalWarehouse() => Session.Snapshot.CompanyStockpiles.Values.FirstOrDefault(
            pool => pool.OwnerId == Session.Player.OwnerId && pool.PortId == Session.SelectedPortId);

        private void RefreshWarehouseAutomation()
        {
            var pool = LocalWarehouse();
            SetText("warehouseAutomationSummary", pool == null ? "No company storage here" : Q(pool.StoredUnits) + " / " + Q(pool.Capacity));
            Element<VisualElement>("warehouseAutomationScroll").EnableInClassList("hidden", pool == null);
            Element<Button>("editWarehouseCashReserve").SetEnabled(pool != null && !Session.IsChallengeEnded);
            Element<Button>("reloadWarehousePolicy").SetEnabled(pool != null);
            if (pool == null)
            {
                loadedWarehousePortId = null;
                warehouseDraftErrors.Clear(); warehouseReserveError = null;
                Element<VisualElement>("warehouseCashReservePopup").AddToClassList("hidden");
                SetText("warehousePolicyDraftLabel", "Construct an eligible facility to create real company storage.");
                SetText("warehouseSavedBadge", "Storage required"); SetText("warehouseRuleCounts", "0 buy · 0 sell");
                Element<Button>("editWarehouseCashReserve").text = "—";
                SetText("warehouseAutomationAccounting", "Warehouse rules never create storage or goods.");
                if (industryWarehouseSelected) ClearApprovedContext();
                return;
            }
            if (loadedWarehousePortId != pool.PortId)
            {
                warehouseDraftErrors.Clear(); warehouseReserveError = null;
                Element<TextField>("warehouseCashReserve").SetValueWithoutNotify(Money(pool.Policy.CashReserve));
                BuildWarehouseRows(pool.Policy, false);
                BuildWarehouseRows(pool.Policy, true);
                loadedWarehousePortId = pool.PortId;
                Element<ScrollView>("industryWarehousePane").scrollOffset = UnityEngine.Vector2.zero;
                Element<VisualElement>("warehouseCashReservePopup").AddToClassList("hidden");
            }
            SetText("warehouseRuleCounts", pool.Policy.Rules.Count(r => r.BuyEnabled) + " buy · " + pool.Policy.Rules.Count(r => r.SellEnabled) + " sell");
            Element<Button>("editWarehouseCashReserve").text = Money(pool.Policy.CashReserve) + " coins  ✎";
            SetText("warehouseReserveFunds", "Company cash " + Money(Session.Player.Cash) + " · above applied reserve " + Money(Math.Max(0, Session.Player.Cash - pool.Policy.CashReserve)));
            SetText("warehouseReserveError", warehouseReserveError ?? "");
            SetText("warehouseAutomationAccounting", "Actual purchases " + Money(pool.ProcurementCost) + " · gross sales " + Money(pool.SalesRevenue) +
                " · held material basis " + Money(Session.Snapshot.Accounting.Stores[pool.Id].TotalCostBasis) +
                "\nLast buy: " + WaitText(pool.ProcurementWaitReason) + " · last sale: " + WaitText(pool.SalesWaitReason));
            foreach (var good in goodIds)
                foreach (var sell in new[] { false, true }) RefreshWarehouseRow(good, sell);
            var invalid = warehouseDraftErrors.Count > 0 || warehouseReserveError != null;
            SetText("warehouseSavedBadge", invalid ? "NOT APPLIED\nReview edits" : "✓ APPLIED\nAutomatically");
            Element<Label>("warehouseSavedBadge").EnableInClassList("industry-v3-error", invalid);
            SetText("warehousePolicyDraftLabel", invalid ? (warehouseReserveError ?? warehouseDraftErrors.OrderBy(p => p.Key, StringComparer.Ordinal).First().Value) :
                "Valid changes apply immediately. Orders run on future days. Save your run separately.");
            Element<Label>("warehousePolicyDraftLabel").EnableInClassList("industry-v3-error", invalid);
            Element<TextField>("warehouseCashReserve").SetEnabled(!Session.IsChallengeEnded);
            if (industryWarehouseSelected) RefreshWarehouseInspector();
        }

        private void BuildWarehouseRows(WarehousePolicy policy, bool sell)
        {
            var prefix = "warehouse" + (sell ? "Sell" : "Buy");
            var rows = Element<VisualElement>(prefix + "Rows"); rows.Clear();
            var preferred = new[] { "bread", "flour", "grain", "logs", "planks", "furniture", "silver" };
            foreach (var good in preferred.Where(goodIds.Contains).Concat(goodIds.Where(g => !preferred.Contains(g))))
            {
                var goodId = good;
                var rule = policy.Rules.FirstOrDefault(item => item.GoodId == goodId);
                var row = Panel(rows, prefix + "Row_" + goodId, "industry-v3-rule-row");
                var line = Panel(row, null, "industry-v3-rule-line");
                var heading = Panel(line, null, "industry-v3-rule-name");
                var glyph = new MaritimeGlyph(goodId); glyph.AddToClassList("sl-glyph-small"); heading.Add(glyph);
                Text(heading, null, Session.Content.GetGood(goodId).Name, "");
                var enabled = new Toggle { name = prefix + "Enabled_" + goodId,
                    value = sell ? rule?.SellEnabled ?? false : rule?.BuyEnabled ?? false,
                    tooltip = "Pause this direction without losing its stock or price limits." };
                enabled.AddToClassList("industry-v3-rule-toggle"); heading.Add(enabled);
                Text(line, "townStock", "", "industry-v3-rule-stock");
                Text(line, "ownStock", "", "industry-v3-rule-stock industry-v3-rule-owned");
                var target = new TextField { name = prefix + "Target_" + goodId, isDelayed = true,
                    value = Q(sell ? rule?.SellReserve ?? 0 : rule?.BuyTarget ?? 0),
                    tooltip = sell ? "Keep this much company stock. Only stock above the reserve is offered." : "Buy toward this stock target; actual orders respect funds, price and free space." };
                target.AddToClassList("industry-v3-rule-value"); line.Add(target);
                var price = new TextField { name = prefix + "Price_" + goodId, isDelayed = true,
                    value = Number(sell ? rule?.MinimumSellPrice ?? 0 : rule?.MaximumBuyPrice ?? 0),
                    tooltip = "Worst marginal unit price, not the average. Zero means no price limit." };
                price.AddToClassList("industry-v3-rule-value"); line.Add(price);
                Text(row, prefix + "Validation_" + goodId, "", "industry-v3-rule-note hidden");
                row.RegisterCallback<PointerDownEvent>(_ => SelectWarehouseGood(goodId));
                row.RegisterCallback<FocusInEvent>(_ => SelectWarehouseGood(goodId));
                enabled.RegisterValueChangedCallback(_ => CommitWarehouseRule(goodId));
                target.RegisterValueChangedCallback(_ => CommitWarehouseRule(goodId));
                price.RegisterValueChangedCallback(_ => CommitWarehouseRule(goodId));
            }
        }

        private void RefreshWarehouseRow(string goodId, bool sell)
        {
            var pool = LocalWarehouse();
            var prefix = "warehouse" + (sell ? "Sell" : "Buy");
            var row = Element<VisualElement>(prefix + "Row_" + goodId);
            var enabled = Element<Toggle>(prefix + "Enabled_" + goodId);
            enabled.text = enabled.value ? "ON" : "OFF";
            row.EnableInClassList("industry-v3-off", !enabled.value);
            row.EnableInClassList("industry-v3-selected", selectedWarehouseGood == goodId);
            row.EnableInClassList("industry-v3-invalid", warehouseDraftErrors.ContainsKey(goodId));
            row.Q<Label>("townStock").text = Q(Session.Snapshot.Ports[pool.PortId].Inventory[goodId]);
            row.Q<Label>("ownStock").text = Q(pool.Inventory[goodId]);
            var flow = EconomicSignals.CommodityFlow(Session.Snapshot, Session.Content, pool.PortId, goodId, pool.OwnerId);
            row.tooltip = "Company stock only: " + SignedQuantity(flow.EnabledProductionQuantity) + " output / " + SignedQuantity(-flow.InputUsageQuantity) +
                " inputs / 12d. " + FlowSummary(flow) + "\n" + FlowState(flow);
            var preview = Session.Snapshot.PreviewWarehouseAutomation(pool.OwnerId, pool.PortId, sell ? TradeDirection.Sell : TradeDirection.Buy, goodId);
            var label = Element<Label>(prefix + "Validation_" + goodId);
            var invalid = warehouseDraftErrors.TryGetValue(goodId, out var error);
            label.text = invalid ? "Not applied: " + error : "";
            label.EnableInClassList("hidden", !invalid);
            row.tooltip = WarehousePreviewText(preview) + "\n" + row.tooltip;
            enabled.SetEnabled(!Session.IsChallengeEnded);
            Element<TextField>(prefix + "Target_" + goodId).SetEnabled(!Session.IsChallengeEnded);
            Element<TextField>(prefix + "Price_" + goodId).SetEnabled(!Session.IsChallengeEnded);
        }

        private WarehouseCommodityRule ReadWarehouseRule(string goodId) => new WarehouseCommodityRule(goodId,
            Element<Toggle>("warehouseBuyEnabled_" + goodId).value,
            ReadIndustryQuantity("warehouseBuyTarget_" + goodId, Session.Content.GetGood(goodId).Name + " buy target", true),
            ReadLong("warehouseBuyPrice_" + goodId, "Maximum ask", true),
            Element<Toggle>("warehouseSellEnabled_" + goodId).value,
            ReadIndustryQuantity("warehouseSellTarget_" + goodId, Session.Content.GetGood(goodId).Name + " retain stock", true),
            ReadLong("warehouseSellPrice_" + goodId, "Minimum bid", true));

        private WarehousePolicy WarehouseRuleProposal(string goodId)
        {
            var pool = LocalWarehouse();
            var changed = ReadWarehouseRule(goodId);
            if (!pool.Policy.Rules.Any(r => r.GoodId == goodId) && !changed.BuyEnabled && !changed.SellEnabled &&
                changed.BuyTarget == 0 && changed.SellReserve == 0 && changed.MaximumBuyPrice == 0 && changed.MinimumSellPrice == 0)
                return pool.Policy;
            return new WarehousePolicy(pool.Policy.CashReserve, pool.Policy.Rules.Where(r => r.GoodId != goodId).Concat(new[] { changed }));
        }

        private void CommitWarehouseRule(string goodId)
        {
            if (loadedWarehousePortId == null || Session.IsChallengeEnded) return;
            selectedWarehouseGood = goodId;
            try
            {
                var snapshot = Session.Snapshot;
                var proposal = WarehouseRuleProposal(goodId);
                snapshot.PreviewWarehouseAutomation(Session.Player.OwnerId, Session.SelectedPortId, TradeDirection.Buy, goodId, proposal);
                Session.ConfigureWarehouse(Session.SelectedPortId, proposal, snapshot);
                warehouseDraftErrors.Remove(goodId);
                status = Session.LastStatus; statusIsError = false;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { warehouseDraftErrors[goodId] = exception is OverflowException ? "Quantity or cash exceeds its supported bound." : exception.Message; }
            Refresh();
        }

        private void CommitWarehouseReserve()
        {
            if (loadedWarehousePortId == null || Session.IsChallengeEnded) return;
            try
            {
                var snapshot = Session.Snapshot;
                var policy = new WarehousePolicy(ReadIndustryMoney("warehouseCashReserve", "Buy cash reserve", true), LocalWarehouse().Policy.Rules);
                Session.ConfigureWarehouse(Session.SelectedPortId, policy, snapshot);
                warehouseReserveError = null;
                status = Session.LastStatus; statusIsError = false;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { warehouseReserveError = exception is OverflowException ? "Cash reserve exceeds its supported bound." : exception.Message; }
            Refresh();
        }

        private void SelectWarehouseGood(string goodId)
        {
            if (selectedWarehouseGood == goodId) return;
            selectedWarehouseGood = goodId;
            foreach (var good in goodIds)
                foreach (var direction in new[] { "Buy", "Sell" })
                    Element<VisualElement>("warehouse" + direction + "Row_" + good).EnableInClassList("industry-v3-selected", good == goodId);
            RefreshWarehouseInspector();
        }

        private void RefreshWarehouseInspector()
        {
            if (!IndustryIsOpen || !industryWarehouseSelected || loadedWarehousePortId == null) return;
            var pool = LocalWarehouse(); if (pool == null) return;
            if (!goodIds.Contains(selectedWarehouseGood)) selectedWarehouseGood = goodIds[0];
            var goodId = selectedWarehouseGood;
            var sell = warehouseSellSelected;
            var prefix = "warehouse" + (sell ? "Sell" : "Buy");
            var inspector = new ScrollView { name = "warehouseRuleInspector" }; inspector.AddToClassList("industry-v3-context");
            Text(inspector, null, warehouseDraftErrors.ContainsKey(goodId) ? "UNAPPLIED " + (sell ? "SELL" : "BUY") + " EDIT" : "SELECTED " + (sell ? "SELL" : "BUY") + " RULE", "industry-v3-eyebrow");
            var heading = Panel(inspector, null, "industry-v3-context-heading"); heading.Add(new MaritimeGlyph(goodId));
            Text(heading, null, Session.Content.GetGood(goodId).Name, "industry-v3-title");
            var flow = EconomicSignals.CommodityFlow(Session.Snapshot, Session.Content, pool.PortId, goodId, pool.OwnerId);
            Text(inspector, null, flow.Produces && flow.Uses ? "Produced and used by local company factories" : flow.Produces ? "Produced by local company factories" :
                flow.Uses ? "Input for local company factories" : "Company logistics stock", "industry-v3-caption");
            var metrics = Panel(inspector, null, "industry-v3-context-metrics");
            Text(metrics, null, "Our stock\n" + Q(pool.Inventory[goodId]), "industry-v3-metric");
            var targetText = Element<TextField>(prefix + "Target_" + goodId).value;
            Text(metrics, null, (sell ? "Keep\n" : "Buy to\n") + targetText, "industry-v3-metric");
            int threshold;
            try { threshold = ReadIndustryQuantity(prefix + "Target_" + goodId, "Stock limit", true); }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException) { threshold = 0; }
            var bar = new ProgressBar { lowValue = 0, highValue = Math.Max(1, Math.Max(threshold, pool.Inventory[goodId])), value = pool.Inventory[goodId],
                title = Q(pool.Inventory[goodId]) + " in shared stock" }; inspector.Add(bar);
            Text(inspector, null, sell ? "Minimum marginal bid" : "Maximum marginal ask", "industry-v3-eyebrow");
            var priceRow = Panel(inspector, null, "industry-v3-row");
            Button(priceRow, "warehouseInspectorPriceMinus", "−", () => StepWarehousePrice(-1)).SetEnabled(!Session.IsChallengeEnded);
            var price = new TextField { name = "warehouseInspectorPrice", isDelayed = true, value = Element<TextField>(prefix + "Price_" + goodId).value,
                tooltip = "Worst marginal price in coins per whole good. Zero means no price limit." };
            price.AddToClassList("industry-v3-rule-value"); priceRow.Add(price);
            price.SetEnabled(!Session.IsChallengeEnded);
            price.RegisterValueChangedCallback(evt => Element<TextField>(prefix + "Price_" + goodId).value = evt.newValue);
            Button(priceRow, "warehouseInspectorPricePlus", "+", () => StepWarehousePrice(1)).SetEnabled(!Session.IsChallengeEnded);
            if (warehouseDraftErrors.TryGetValue(goodId, out var error))
            {
                Text(inspector, "warehouseInspectorError", "Cannot apply this edit\n" + error, "industry-v3-error");
                Text(inspector, null, "The last applied rule remains active. Buy to must not exceed Keep when both directions are ON. Edit either limit or pause a direction.", "industry-v3-caption");
            }
            else
            {
                var preview = Session.Snapshot.PreviewWarehouseAutomation(pool.OwnerId, pool.PortId, sell ? TradeDirection.Sell : TradeDirection.Buy, goodId);
                Text(inspector, "warehouseInspectorPreview", WarehousePreviewText(preview), "industry-v3-metric");
            }
            var quote = Session.Snapshot.GetMarketQuote(pool.PortId, goodId);
            Text(inspector, null, "Town stock " + Q(Session.Snapshot.Ports[pool.PortId].Inventory[goodId]) + "\nCurrent " + (sell ? "bid " + (quote.Bid.HasValue ? Number(quote.Bid.Value) : "unavailable") : "ask " + (quote.Ask.HasValue ? Number(quote.Ask.Value) : "unavailable")) +
                " · market cash " + Money(Session.Snapshot.Ports[pool.PortId].MarketCash), "industry-v3-caption");
            Button(inspector, "warehouseOppositeRule", sell ? "View buy rule →" : "View sell rule →", () => SelectWarehouseDirection(!sell));
            foreach (var id in flow.Production.Select(p => p.Opportunity.FacilityId).Distinct(StringComparer.Ordinal))
            {
                var facility = Session.Snapshot.PrivateFacilities[id];
                Button(inspector, "warehouseViewFactory_" + id, "View " + Session.Content.GetBuilding(facility.Blueprint.BuildingId).Name + " →", () => OpenApprovedFactory(id));
            }
            Button(inspector, "warehouseViewShipRoute", "View ship route →", () => OpenRoutePlanner(Session.Player.Id));
            Text(inspector, null, "Purchases precede production; sales follow it. Current possibilities, not future-price promises.", "industry-v3-caption");
            SetApprovedContext(inspector);
        }

        private void StepWarehousePrice(int delta)
        {
            var name = "warehouse" + (warehouseSellSelected ? "Sell" : "Buy") + "Price_" + selectedWarehouseGood;
            if (!long.TryParse(Element<TextField>(name).value, out var value)) value = 0;
            if (delta > 0 && value == long.MaxValue) return;
            Element<TextField>(name).value = Number(Math.Max(0, value + delta));
        }

        private string WarehousePreviewText(WarehouseTradePreview preview) => preview.Quantity == 0 ?
            preview.Reason == IndustryWaitReason.Disabled ? "Off" :
            preview.Reason == IndustryWaitReason.TargetSatisfied ? "Target met" :
            preview.Reason == IndustryWaitReason.ReserveRetained ? "Retained stock protected" : WaitText(preview.Reason) :
            "Now: " + Q(preview.Quantity) + " for " + Money(preview.TotalAmount) +
            (preview.MarginalPrice > 0 ? " · last marginal " + Number(preview.MarginalPrice) : "") +
            (preview.Trade == null ? "" : ServiceFeeText(preview.Trade)) +
            (preview.Reason == IndustryWaitReason.None ? " · eligible" : " · partial: " + WaitText(preview.Reason)) +
            ". Current snapshot; other rules share stock/cash.";
    }
}
