using System;
using System.Collections.Generic;

namespace SaltAndLedger.Simulation
{
    /// <summary>Stateless, bounded decisions over the existing maritime quotes and commands.</summary>
    public static class RoutePolicyEvaluator
    {
        public static AutomationDecision Evaluate(MaritimeWorldSnapshot snapshot, TradeRoutePolicy policy,
            bool enabled = true)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            ValidateReferences(snapshot, policy);
            if (!enabled) return Pause(snapshot, AutomationReason.Disabled);
            if (policy.Outbound.MinShipmentQuantity > snapshot.Ship.CargoCapacity ||
                (policy.ReturnLeg != null && policy.ReturnLeg.MinShipmentQuantity > snapshot.Ship.CargoCapacity))
                return Pause(snapshot, AutomationReason.MinimumExceedsCapacity);

            foreach (var item in snapshot.Ship.Cargo)
            {
                if (item.Value > 0 && !string.Equals(item.Key, policy.Outbound.GoodId, StringComparison.Ordinal) &&
                    !string.Equals(item.Key, policy.ReturnLeg?.GoodId, StringComparison.Ordinal))
                    return Pause(snapshot, AutomationReason.UnexpectedCargo);
            }
            var outboundCargo = snapshot.Ship.Cargo[policy.Outbound.GoodId];
            var returnCargo = policy.ReturnLeg == null ? 0 : snapshot.Ship.Cargo[policy.ReturnLeg.GoodId];
            if (outboundCargo > 0 && returnCargo > 0) return Pause(snapshot, AutomationReason.MixedCargo);
            if (snapshot.Ship.DockedPortId == null)
                return new AutomationDecision(snapshot, AutomationStage.InTransit,
                    AutomationAction.ContinueSailing, AutomationReason.Sailing);

            if (string.Equals(snapshot.Ship.DockedPortId, policy.OriginPortId, StringComparison.Ordinal))
            {
                if (returnCargo > 0)
                    return Sell(snapshot, policy.ReturnLeg!, policy.OriginPortId, AutomationStage.ReturnSale);
                return Load(snapshot, policy, policy.Outbound, policy.OriginPortId,
                    policy.DestinationPortId, AutomationStage.OutboundLoading);
            }

            if (outboundCargo > 0)
                return Sell(snapshot, policy.Outbound, policy.DestinationPortId, AutomationStage.OutboundSale);
            if (policy.ReturnLeg != null)
            {
                var loading = Load(snapshot, policy, policy.ReturnLeg, policy.DestinationPortId,
                    policy.OriginPortId, AutomationStage.ReturnLoading);
                if (loading.Action != AutomationAction.Wait || !policy.AllowEmptyReturn || snapshot.Ship.CargoUnits != 0)
                    return loading;
            }
            return new AutomationDecision(snapshot, AutomationStage.ReturnLoading, AutomationAction.Depart,
                AutomationReason.EmptyReturnAllowed, departure: new SailCommand(policy.OriginPortId));
        }

        private static AutomationDecision Load(MaritimeWorldSnapshot snapshot, TradeRoutePolicy policy,
            RouteLeg leg, string portId, string destinationPortId, AutomationStage stage)
        {
            var port = snapshot.Ports[portId];
            var carried = snapshot.Ship.Cargo[leg.GoodId];
            var upper = Math.Min(Math.Max(0, leg.MaxPurchaseQuantity - carried),
                Math.Min(port.Inventory[leg.GoodId], snapshot.Ship.CargoCapacity - snapshot.Ship.CargoUnits));
            var budget = snapshot.TraderCash > policy.CashReserve ? snapshot.TraderCash - policy.CashReserve : 0;
            var quantity = SelectQuantity(port.Quotes[leg.GoodId], TradeDirection.Buy,
                port.Inventory[leg.GoodId], upper, leg.MaxBuyPrice, budget);
            if (quantity > 0 && checked(carried + quantity) >= leg.MinShipmentQuantity)
            {
                var command = new PortTradeCommand(portId, TradeDirection.Buy, leg.GoodId, quantity);
                var preview = SuccessfulPreview(snapshot, command);
                return new AutomationDecision(snapshot, stage, AutomationAction.BuyAndDepart,
                    AutomationReason.PurchaseReady, command, new SailCommand(destinationPortId), preview);
            }
            if (carried >= leg.MinShipmentQuantity)
                return new AutomationDecision(snapshot, stage, AutomationAction.Depart,
                    AutomationReason.ShipmentReady, departure: new SailCommand(destinationPortId));

            var needed = leg.MinShipmentQuantity - carried;
            var reason = PurchaseWaitReason(port, leg, needed, budget, snapshot.TraderCash);
            return new AutomationDecision(snapshot, stage, AutomationAction.Wait, reason);
        }

        private static AutomationDecision Sell(MaritimeWorldSnapshot snapshot, RouteLeg leg,
            string portId, AutomationStage stage)
        {
            var port = snapshot.Ports[portId];
            var stock = port.Inventory[leg.GoodId];
            var upper = Math.Min(snapshot.Ship.Cargo[leg.GoodId], int.MaxValue - stock);
            var quote = port.Quotes[leg.GoodId];
            var quantity = SelectQuantity(quote, TradeDirection.Sell, stock, upper, leg.MinSellPrice, port.MarketCash);
            if (quantity > 0)
            {
                var command = new PortTradeCommand(portId, TradeDirection.Sell, leg.GoodId, quantity);
                return new AutomationDecision(snapshot, stage, AutomationAction.Sell, AutomationReason.SaleReady,
                    command, preview: SuccessfulPreview(snapshot, command));
            }
            var reason = upper == 0 ? AutomationReason.MarketInventoryFull :
                !MeetsPriceLimit(quote, TradeDirection.Sell, stock, 1, leg.MinSellPrice) ? AutomationReason.SellPriceLimit :
                AutomationReason.InsufficientMarketFunds;
            return new AutomationDecision(snapshot, stage, AutomationAction.Wait, reason);
        }

        private static int SelectQuantity(MarketQuote quote, TradeDirection direction, int stock,
            int upper, long marginalLimit, long budget)
        {
            var lower = 0;
            // Positive prices and monotone marginal curves make feasibility a prefix.
            // At most 31 probes are needed for the public int quantity range.
            while (lower < upper)
            {
                var middle = lower + (int)(((long)upper - lower + 1) / 2);
                if (MeetsPriceLimit(quote, direction, stock, middle, marginalLimit) &&
                    quote.FitsBudget(direction, stock, middle, budget))
                    lower = middle;
                else upper = middle - 1;
            }
            return lower;
        }

        private static bool MeetsPriceLimit(MarketQuote quote, TradeDirection direction,
            int stock, int quantity, long limit)
        {
            var lastEdge = direction == TradeDirection.Buy ? checked(stock - quantity + 1) : checked(stock + quantity);
            var price = quote.Pricing != null ? quote.Pricing.PriceAtEdge(direction, lastEdge) :
                direction == TradeDirection.Buy ? quote.Ask : quote.Bid;
            return direction == TradeDirection.Buy ? price <= limit : price >= limit;
        }

        private static AutomationReason PurchaseWaitReason(PortSnapshot port, RouteLeg leg,
            int needed, long budget, long traderCash)
        {
            var stock = port.Inventory[leg.GoodId];
            if (stock < needed) return AutomationReason.InsufficientInventory;
            var quote = port.Quotes[leg.GoodId];
            if (!MeetsPriceLimit(quote, TradeDirection.Buy, stock, needed, leg.MaxBuyPrice))
                return AutomationReason.BuyPriceLimit;
            return quote.FitsBudget(TradeDirection.Buy, stock, needed, traderCash) && budget < traderCash ?
                AutomationReason.CashReserve : AutomationReason.InsufficientTraderFunds;
        }

        private static TradeAttempt SuccessfulPreview(MaritimeWorldSnapshot snapshot, PortTradeCommand command)
        {
            var preview = snapshot.PreviewTrade(command.PortId, command.Trade);
            if (preview.Outcome != TradeOutcome.Traded)
                throw new InvalidOperationException("A selected automation trade must pass its authoritative preview.");
            return preview;
        }

        private static AutomationDecision Pause(MaritimeWorldSnapshot snapshot, AutomationReason reason) =>
            new AutomationDecision(snapshot, AutomationStage.Suspended, AutomationAction.Pause, reason);

        private static void ValidateReferences(MaritimeWorldSnapshot snapshot, TradeRoutePolicy policy)
        {
            var origin = snapshot.Ports[policy.OriginPortId];
            var destination = snapshot.Ports[policy.DestinationPortId];
            ValidateLeg(origin, destination, policy.Outbound);
            if (policy.ReturnLeg != null) ValidateLeg(origin, destination, policy.ReturnLeg);
        }

        private static void ValidateLeg(PortSnapshot origin, PortSnapshot destination, RouteLeg leg)
        {
            if (!origin.Inventory.ContainsKey(leg.GoodId) || !destination.Inventory.ContainsKey(leg.GoodId))
                throw new KeyNotFoundException("Unknown route commodity '" + leg.GoodId + "'.");
            if (!origin.Quotes.ContainsKey(leg.GoodId) || !destination.Quotes.ContainsKey(leg.GoodId))
                throw new KeyNotFoundException("Both route ports require a quote for '" + leg.GoodId + "'.");
        }
    }
}
