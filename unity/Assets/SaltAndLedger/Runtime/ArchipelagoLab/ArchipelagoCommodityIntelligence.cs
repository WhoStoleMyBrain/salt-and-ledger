using System;
using System.Collections.Generic;
using System.Linq;
using SaltAndLedger.Simulation;
using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private RegionalWorldSnapshot flowSnapshot;
        private readonly Dictionary<(string, string, string), CommodityFlowSignal> commodityFlows =
            new Dictionary<(string, string, string), CommodityFlowSignal>();

        private CommodityFlowSignal HarborFlow(string portId, string goodId)
        {
            if (!ReferenceEquals(flowSnapshot, tradeSnapshot))
            {
                commodityFlows.Clear();
                flowSnapshot = tradeSnapshot;
            }
            var owner = Element<DropdownField>("harborFlowScope").index == 1 ? Session.Player.OwnerId : null;
            var key = (portId, goodId, owner);
            if (!commodityFlows.TryGetValue(key, out var signal))
            {
                signal = EconomicSignals.CommodityFlow(tradeSnapshot, Session.Content, portId, goodId, owner);
                commodityFlows.Add(key, signal);
            }
            return signal;
        }

        private string SignedQuantity(long quantity) => (quantity > 0 ? "+" : quantity < 0 ? "−" : "") + Q(Math.Abs(quantity));

        private static string FlowRole(CommodityFlowSignal flow) => flow.Produces && flow.Uses ? "Makes + uses" :
            flow.Produces ? "Produces" : flow.Uses ? "Uses" : "Trade only";

        private static string FlowState(CommodityFlowSignal flow)
        {
            if (!flow.HasStore) return "No warehouse";
            var statuses = new List<string>();
            if (flow.Production.Any(p => p.IsActive && !p.Opportunity.ReadyFromCurrentState)) statuses.Add("Blocked");
            if (flow.Production.Any(p => p.Opportunity.UnderConstruction)) statuses.Add("Building");
            if (flow.Production.Any(p => !p.Opportunity.Enabled)) statuses.Add("Disabled");
            if (flow.UsageQuantity > flow.CurrentStock) statuses.Add("Stock < need");
            return string.Join(" · ", statuses);
        }

        private void RefreshHarborRows(RegionalShipSnapshot ship, RegionalPortSnapshot port, CompanyStockpileSnapshot stock, bool local)
        {
            var rows = Element<VisualElement>("tradeGoodsRows");
            if (rows.childCount == 0)
                foreach (var goodId in goodIds)
                {
                    var captured = goodId;
                    var row = new Button(() => { SetIndex("goodDropdown", goodIds.IndexOf(captured)); RefreshHarbor(); })
                        { name = "tradeGood_" + goodId };
                    row.AddToClassList("trade-goods-row");
                    var glyph = new MaritimeGlyph(goodId); glyph.AddToClassList("sl-glyph-small"); row.Add(glyph);
                    var name = new Label { name = "name", pickingMode = PickingMode.Ignore };
                    name.AddToClassList("trade-good-name"); row.Add(name);
                    foreach (var field in new[] { "town", "ship", "production", "usage", "net", "bid", "ask" })
                    {
                        var cell = new Label { name = field, pickingMode = PickingMode.Ignore };
                        cell.AddToClassList("trade-cell"); row.Add(cell);
                    }
                    rows.Add(row);
                }
            foreach (var goodId in goodIds)
            {
                var row = rows.Q<Button>("tradeGood_" + goodId);
                var flow = HarborFlow(port.Id, goodId);
                var quote = tradeSnapshot.GetMarketQuote(port.Id, goodId);
                var status = FlowState(flow);
                row.Q<Label>("name").text = Session.Content.GetGood(goodId).Name + "\n" + FlowRole(flow) +
                    (status.Length == 0 ? "" : " · " + status);
                row.Q<Label>("town").text = flow.HasStore ? Q(flow.CurrentStock) : "—";
                row.Q<Label>("ship").text = local ? Q(ship.Cargo[goodId]) : "—";
                row.Q<Label>("production").text = SignedQuantity(flow.EnabledProductionQuantity);
                row.Q<Label>("usage").text = SignedQuantity(-flow.UsageQuantity);
                row.Q<Label>("net").text = SignedQuantity(flow.NetQuantity);
                row.Q<Label>("bid").text = quote.Bid.HasValue ? Number(quote.Bid.Value) : "—";
                row.Q<Label>("ask").text = quote.Ask.HasValue ? Number(quote.Ask.Value) : "—";
                row.tooltip = FlowRole(flow) + ". " + FlowSummary(flow) + "\nSelect for breakdown and other towns. Market quotes remain public in both views.";
                row.EnableInClassList("stock-short", flow.CurrentStock < flow.UsageQuantity);
                row.EnableInClassList("stock-adequate", flow.UsageQuantity > 0 && flow.CurrentStock >= flow.UsageQuantity);
                row.EnableInClassList("selected-good", goodId == SelectedGoodId());
            }
        }

        private string FlowSummary(CommodityFlowSignal flow) => "Days " + flow.FirstDay + "–" + flow.LastDay +
            ": enabled output " + Q(flow.EnabledProductionQuantity) + "; inputs " + Q(flow.InputUsageQuantity) +
            "; consumption need " + Q(flow.ConsumptionNeedQuantity) + ". Conditional on successful batches and purchases, not future stock.";

        private void RefreshCommodityIntelligence(RegionalShipSnapshot ship, RegionalPortSnapshot port)
        {
            var goodId = SelectedGoodId();
            var goodName = Session.Content.GetGood(goodId).Name;
            var flow = HarborFlow(port.Id, goodId);
            Element<Foldout>("harborFlowDetails").text = goodName + " · breakdown";
            SetText("selectedFlowProduction", SignedQuantity(flow.EnabledProductionQuantity) + " output");
            SetText("selectedFlowUsage", SignedQuantity(-flow.UsageQuantity) + " need");
            SetText("selectedFlowNet", SignedQuantity(flow.NetQuantity) + " net");
            var details = Element<VisualElement>("harborFlowBreakdown");
            details.Clear();
            AddFlowDetail(details, FlowSummary(flow));
            AddFlowDetail(details, "Installed output " + Q(flow.InstalledProductionQuantity) + " · enabled output " +
                Q(flow.EnabledProductionQuantity) + ". Disabled / building sites excluded from enabled totals. No trade or transport included.");
            foreach (var production in flow.Production)
            {
                var signal = production.Opportunity;
                var state = signal.UnderConstruction ? "Building" : !signal.Enabled ? "Disabled" : !signal.HasNumericHeadroom ? "Numeric capacity blocked" :
                    ProductionRemedy(signal.CurrentReadiness);
                var text = Session.Content.GetBuilding(signal.BuildingId).Name + " · " + state +
                    "\n" + SignedQuantity(production.OutputPerBatch) + " output / " + SignedQuantity(-production.InputPerBatch) +
                    " input per batch · every " + production.IntervalDays + "d · next eligible D" + signal.NextEligibleDay +
                    "\n" + signal.OperatingRate + "% · " + signal.Priority + " priority · " + production.Opportunities + " rate-admitted batches · " + SignedQuantity(production.OutputQuantity) +
                    " / " + SignedQuantity(-production.InputQuantity) + " in window · wages " + Money(signal.WagePerBatch) +
                    " · payer cash " + Money(signal.EmployerCash);
                foreach (var input in signal.Inputs)
                    text += "\n" + Session.Content.GetGood(input.GoodId).Name + ": " + Q(input.CurrentStock) + " held / " +
                        Q(input.Quantity) + " required" + (input.Shortfall == 0 ? "" : " · short " + Q(input.Shortfall));
                if (signal.StorageCapacity.HasValue) text += "\nWarehouse " + Q(signal.StoredUnits) + "/" + Q(signal.StorageCapacity.Value);
                if (signal.OutputStockLimit.HasValue) text += " · output ceiling " + Q(signal.OutputStockLimit.Value);
                text += "\nLast outcome: " + ArchipelagoLabSession.FacilityStatus(signal.LastOutcome) + " · " + signal.FacilityId;
                AddFlowDetail(details, text);
            }
            foreach (var need in flow.Consumption)
            {
                var municipal = need.Kind == ConsumptionKind.Municipal;
                var text = need.Kind + " · " + (need.IsEligible ? Q(need.NeedQuantity) + " needed / 12d" : "Currently ineligible") +
                    "\nEvery " + need.IntervalDays + "d · next D" + need.NextDueDay + ": " + Q(need.NextDesiredQuantity) +
                    " needed · current stock short " + Q(need.NextStockShortfall);
                if (need.PreviewAvailable) text += municipal ? " · available for own-use " + Q(need.NextAffordableQuantity.Value) :
                    " · purchasable now " + Q(need.NextAffordableQuantity.Value) + " · funds short " + Q(need.NextAffordabilityShortfall.Value);
                if (need.NumericCapacityBlocked) text += " · monetary numeric limit";
                text += need.HasObservation ? "\nLast actual: " + Q(need.LastConsumedQuantity) + " consumed / " +
                    Q(need.LastDesiredQuantity) + " desired · stock miss " + Q(need.LastUnmetStock) +
                    (municipal ? " · own-use, no payment" : " · paid " + Money(need.LastPayment) + " · funds miss " + Q(need.LastUnmetAffordability)) : "\nNo consumption observation yet";
                text += municipal ? "\nFixed municipal use · no household purchase." :
                    "\nCurrent population / eligibility held fixed; fractional demand carry " + need.DemandRemainder + ".";
                AddFlowDetail(details, text);
            }
            if (flow.Production.Count == 0 && flow.Consumption.Count == 0)
                AddFlowDetail(details, flow.HasStore ? "No installed local production or configured need for this good." : "No company warehouse here.");
            RefreshCommodityComparison(ship, goodId, goodName);
        }

        private static void AddFlowDetail(VisualElement parent, string text)
        {
            var label = new Label(text); label.AddToClassList("help"); label.AddToClassList("flow-detail"); parent.Add(label);
        }

        private void RefreshCommodityComparison(RegionalShipSnapshot ship, string goodId, string goodName)
        {
            Element<Foldout>("harborComparison").text = goodName + " · compare towns";
            var rows = Element<VisualElement>("harborComparisonRows");
            if (rows.childCount == 0)
                foreach (var port in tradeSnapshot.Ports.Values.OrderBy(p => p.Id, StringComparer.Ordinal))
                {
                    var captured = port.Id;
                    var row = new Button(() => OpenTrade(captured)) { name = "compareTown_" + port.Id };
                    row.AddToClassList("commodity-comparison");
                    var title = new Label { name = "townName", pickingMode = PickingMode.Ignore };
                    title.AddToClassList("comparison-town"); row.Add(title);
                    var values = new VisualElement(); values.AddToClassList("comparison-values");
                    foreach (var field in new[] { "stock", "bid", "ask", "cash" })
                    {
                        var group = new VisualElement(); group.AddToClassList("comparison-group");
                        group.Add(new Label(field == "stock" ? "STOCK" : field == "cash" ? "BUYING POWER" : field.ToUpperInvariant()) { pickingMode = PickingMode.Ignore });
                        group.Add(new Label { name = field, pickingMode = PickingMode.Ignore }); values.Add(group);
                    }
                    row.Add(values); rows.Add(row);
                }
            foreach (var port in tradeSnapshot.Ports.Values)
            {
                var row = rows.Q<Button>("compareTown_" + port.Id);
                var quote = tradeSnapshot.GetMarketQuote(port.Id, goodId);
                var travel = ship.DockedPortId == port.Id ? " · Docked" : ship.DockedPortId != null && Session.HasRoute(ship.DockedPortId, port.Id) ?
                    " · " + tradeSnapshot.TravelDays(ship.Id, port.Id) + "d from ship" : " · Cargo remote";
                row.Q<Label>("townName").text = port.Name + travel;
                row.Q<Label>("stock").text = Q(port.Inventory[goodId]);
                row.Q<Label>("bid").text = quote.Bid.HasValue ? Number(quote.Bid.Value) : "—";
                row.Q<Label>("ask").text = quote.Ask.HasValue ? Number(quote.Ask.Value) : "—";
                row.Q<Label>("cash").text = Money(port.MarketCash);
                row.tooltip = "Inspect " + goodName + " at " + port.Name + ". Quotes are current marginal coins per good, not a shipment's execution price. Cargo needs a local dock and whole quantities.";
                row.EnableInClassList("selected-good", port.Id == Session.SelectedPortId);
            }
        }
    }
}
