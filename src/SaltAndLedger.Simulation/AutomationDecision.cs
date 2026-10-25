namespace SaltAndLedger.Simulation
{
    public enum AutomationStage
    {
        Suspended,
        InTransit,
        OutboundLoading,
        OutboundSale,
        ReturnLoading,
        ReturnSale
    }

    public enum AutomationAction
    {
        BuyAndDepart,
        Sell,
        Depart,
        ContinueSailing,
        Wait,
        Pause,
        LoadAndDepart,
        Unload,
        AdvanceStage,
        ExecuteItinerary
    }

    public enum AutomationReason
    {
        PurchaseReady,
        SaleReady,
        ShipmentReady,
        Sailing,
        EmptyReturnAllowed,
        Disabled,
        UnexpectedCargo,
        MixedCargo,
        MinimumExceedsCapacity,
        InsufficientInventory,
        BuyPriceLimit,
        SellPriceLimit,
        InsufficientTraderFunds,
        CashReserve,
        InsufficientMarketFunds,
        MarketInventoryFull,
        PurchaseExpenditureLimit,
        SourceStockThreshold
    }

    /// <summary>A non-mutating policy evaluation tied to one exact immutable committed snapshot.</summary>
    public sealed class AutomationDecision
    {
        public MaritimeWorldSnapshot Before { get; }
        public long Tick => Before.Tick;
        public AutomationStage Stage { get; }
        public AutomationAction Action { get; }
        public AutomationReason Reason { get; }
        public PortTradeCommand? Trade { get; }
        public SailCommand? Departure { get; }
        public TradeAttempt? Preview { get; }
        public int SelectedQuantity => Trade?.Trade.Quantity ?? 0;

        internal AutomationDecision(MaritimeWorldSnapshot before, AutomationStage stage,
            AutomationAction action, AutomationReason reason, PortTradeCommand? trade = null,
            SailCommand? departure = null, TradeAttempt? preview = null)
        {
            Before = before;
            Stage = stage;
            Action = action;
            Reason = reason;
            Trade = trade;
            Departure = departure;
            Preview = preview;
        }
    }

    /// <summary>The policy explanation and the ordinary committed maritime step.</summary>
    public sealed class AutomationStep
    {
        public AutomationDecision Decision { get; }
        public MaritimeStepResult Result { get; }

        internal AutomationStep(AutomationDecision decision, MaritimeStepResult result)
        {
            Decision = decision;
            Result = result;
        }
    }
}
