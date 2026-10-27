using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost
{
    /// <summary>Player-facing explanations; no economic calculations.</summary>
    public static class EconomicText
    {
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
