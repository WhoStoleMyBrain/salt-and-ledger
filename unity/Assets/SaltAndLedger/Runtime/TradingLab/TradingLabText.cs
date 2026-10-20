using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Player-facing explanations; no economic calculations.</summary>
    public static class TradingLabText
    {
        public static string PortName(string portId)
        {
            return portId == TradingLabScenarios.North ? "Northport" :
                portId == TradingLabScenarios.South ? "Southhaven" : portId;
        }

        public static string Stage(AutomationStage stage)
        {
            switch (stage)
            {
                case AutomationStage.Suspended: return "Paused";
                case AutomationStage.InTransit: return "In transit";
                case AutomationStage.OutboundLoading: return "Loading outbound cargo";
                case AutomationStage.OutboundSale: return "Selling outbound cargo";
                case AutomationStage.ReturnLoading: return "Loading return cargo";
                case AutomationStage.ReturnSale: return "Selling return cargo";
                default: return stage.ToString();
            }
        }

        public static string Action(AutomationAction action)
        {
            switch (action)
            {
                case AutomationAction.BuyAndDepart: return "Buy and depart";
                case AutomationAction.Sell: return "Sell cargo";
                case AutomationAction.Depart: return "Depart";
                case AutomationAction.ContinueSailing: return "Continue sailing";
                case AutomationAction.Wait: return "Waiting";
                case AutomationAction.Pause: return "Paused - intervention needed";
                default: return action.ToString();
            }
        }

        public static string Reason(AutomationReason reason)
        {
            switch (reason)
            {
                case AutomationReason.PurchaseReady: return "A qualifying shipment is affordable; buy and depart on the next tick.";
                case AutomationReason.SaleReady: return "The market can buy this cargo at your minimum selling price.";
                case AutomationReason.ShipmentReady: return "Cargo already meets the departure minimum.";
                case AutomationReason.Sailing: return "Sailing; each explicit tick advances the voyage. Trade after arrival.";
                case AutomationReason.EmptyReturnAllowed: return "No qualifying return purchase; this policy allows an empty return.";
                case AutomationReason.Disabled: return "Automation is disabled. Manual controls are available; enabling does not advance time.";
                case AutomationReason.UnexpectedCargo: return "Cargo includes a good outside this policy. Pause and trade it manually or change the policy.";
                case AutomationReason.MixedCargo: return "Outbound and return cargo are mixed. Pause and clear one commodity manually.";
                case AutomationReason.MinimumExceedsCapacity: return "The minimum shipment exceeds ship capacity. Lower the minimum and apply.";
                case AutomationReason.InsufficientInventory: return "The market lacks enough stock for the minimum shipment. Waiting does not replenish it.";
                case AutomationReason.BuyPriceLimit: return "The buying limit prevents the minimum shipment. Raise the maximum buying price and apply to reconsider.";
                case AutomationReason.SellPriceLimit: return "The current bid is below your minimum selling price. Lower the limit and apply to reconsider.";
                case AutomationReason.InsufficientTraderFunds: return "Trader cash cannot fund a qualifying shipment.";
                case AutomationReason.CashReserve: return "The cash reserve prevents a qualifying purchase. Lower the reserve and apply to reconsider.";
                case AutomationReason.InsufficientMarketFunds: return "The selected market cannot pay for any eligible cargo. Cargo stays aboard; changing a price limit creates no cash.";
                case AutomationReason.MarketInventoryFull: return "The receiving market has no inventory space. Cargo stays aboard.";
                default: return reason.ToString();
            }
        }

        public static string Outcome(TradeOutcome outcome)
        {
            switch (outcome)
            {
                case TradeOutcome.Traded: return "Executable";
                case TradeOutcome.InsufficientMarketInventory: return "Market does not have enough stock";
                case TradeOutcome.InsufficientTraderInventory: return "Ship does not carry enough of this good";
                case TradeOutcome.InsufficientTraderFunds: return "Trader does not have enough cash";
                case TradeOutcome.InsufficientMarketFunds: return "Market does not have enough cash";
                case TradeOutcome.InsufficientCargoCapacity: return "Purchase exceeds free cargo capacity";
                default: return outcome.ToString();
            }
        }
    }
}
