using System;

namespace SaltAndLedger.Simulation
{
    public sealed class RegionalDecision
    {
        public RegionalWorldSnapshot Before { get; }
        public string ShipId { get; }
        public AutomationStage Stage { get; }
        public AutomationAction Action { get; }
        public string Reason { get; }
        public AutomationDecision? Detail { get; }
        public int SelectedQuantity => Detail?.SelectedQuantity ?? 0;
        public TradeAttempt? Preview => Detail?.Preview;
        internal RegionalDecision(RegionalWorldSnapshot before, string shipId, AutomationStage stage,
            AutomationAction action, string reason, AutomationDecision? detail = null)
        { Before = before; ShipId = shipId; Stage = stage; Action = action; Reason = reason; Detail = detail; }
    }

    /// <summary>Projects one actor's route into the proven evaluator; never owns economic state.</summary>
    public static class RegionalAutomation
    {
        public static RegionalDecision Evaluate(RegionalWorldSnapshot snapshot, string shipId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var ship = snapshot.Ships[shipId];
            if (!ship.AutomationEnabled || ship.Policy == null)
                return new RegionalDecision(snapshot, shipId, AutomationStage.Suspended, AutomationAction.Pause,
                    "Automation is paused; enable it to execute the configured route.");
            var policy = ship.Policy;
            ValidatePolicy(snapshot, shipId, policy);
            if (ship.DockedPortId != null && ship.DockedPortId != policy.OriginPortId &&
                ship.DockedPortId != policy.DestinationPortId)
                return new RegionalDecision(snapshot, shipId, AutomationStage.Suspended, AutomationAction.Pause,
                    "The ship is outside this route. Pause and sail to a route port, or apply a different route.");
            var projected = Project(snapshot, shipId, policy);
            var detail = RoutePolicyEvaluator.Evaluate(projected, policy, true,
                ship.Configuration.MinimumOriginStock, ship.Configuration.MinimumReturnStock);
            if (detail.Reason == AutomationReason.SourceStockThreshold)
            {
                var threshold = detail.Stage == AutomationStage.OutboundLoading
                    ? ship.Configuration.MinimumOriginStock : ship.Configuration.MinimumReturnStock;
                return new RegionalDecision(snapshot, shipId, detail.Stage, AutomationAction.Wait,
                    "Waiting for source stock to reach " + threshold + " units before loading.", detail);
            }
            return new RegionalDecision(snapshot, shipId, detail.Stage, detail.Action, Explain(detail.Reason), detail);
        }

        internal static void ValidatePolicy(RegionalWorldSnapshot snapshot, string shipId, TradeRoutePolicy policy)
        {
            snapshot.GetRoute(policy.OriginPortId, policy.DestinationPortId);
            // Disabled evaluation still validates canonical commodities and quotes.
            RoutePolicyEvaluator.Evaluate(Project(snapshot, shipId, policy), policy, false);
        }

        private static MaritimeWorldSnapshot Project(RegionalWorldSnapshot snapshot, string shipId, TradeRoutePolicy policy)
        {
            var ship = snapshot.Ships[shipId];
            return new MaritimeWorldSnapshot(snapshot.Day,
                new[] { snapshot.Ports[policy.OriginPortId].Market, snapshot.Ports[policy.DestinationPortId].Market },
                ship.OwnerId, ship.Cash, ship.Ship,
                snapshot.GetRoute(policy.OriginPortId, policy.DestinationPortId).DurationDays(ship.Configuration.Speed));
        }

        public static string Explain(AutomationReason reason)
        {
            switch (reason)
            {
                case AutomationReason.PurchaseReady: return "Buy the largest qualifying shipment within stock, cargo, marginal price, cash reserve and expenditure limits; then depart.";
                case AutomationReason.SaleReady: return "Sell eligible carried goods at the destination's current bids and finite cash.";
                case AutomationReason.ShipmentReady: return "Carried cargo already meets the departure minimum.";
                case AutomationReason.Sailing: return "Sailing; arrival requires the remaining economic days.";
                case AutomationReason.EmptyReturnAllowed: return "The configured route permits an empty return.";
                case AutomationReason.Disabled: return "Automation is paused.";
                case AutomationReason.UnexpectedCargo: return "Cargo is outside the configured commodities; pause and intervene manually.";
                case AutomationReason.MixedCargo: return "Mixed directional cargo requires manual intervention.";
                case AutomationReason.MinimumExceedsCapacity: return "The minimum shipment exceeds ship capacity.";
                case AutomationReason.InsufficientInventory: return "Source stock cannot supply the minimum shipment; inspect its production schedule.";
                case AutomationReason.BuyPriceLimit: return "The worst marginal purchase price exceeds the buying limit before the minimum shipment is reached.";
                case AutomationReason.SellPriceLimit: return "Current marginal bids are below the minimum selling price; cargo is retained.";
                case AutomationReason.InsufficientTraderFunds: return "Trader cash cannot fund a qualifying shipment.";
                case AutomationReason.CashReserve: return "The cash reserve prevents a qualifying purchase.";
                case AutomationReason.InsufficientMarketFunds: return "Destination market cash cannot fund eligible cargo; goods stay aboard.";
                case AutomationReason.MarketInventoryFull: return "The receiving market has no stock space.";
                case AutomationReason.PurchaseExpenditureLimit: return "The total purchase expenditure cap prevents the minimum shipment.";
                case AutomationReason.SourceStockThreshold: return "Waiting for the configured source stock threshold before loading.";
                default: throw new ArgumentOutOfRangeException(nameof(reason));
            }
        }
    }
}
