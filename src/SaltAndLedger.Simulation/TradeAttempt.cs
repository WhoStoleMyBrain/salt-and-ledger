namespace SaltAndLedger.Simulation
{
    public enum TradeOutcome
    {
        Traded,
        InsufficientMarketInventory,
        InsufficientTraderInventory,
        InsufficientTraderFunds,
        InsufficientMarketFunds
    }

    public sealed class CashChange
    {
        public long Before { get; }
        public long After { get; }
        public long Delta => checked(After - Before);

        internal CashChange(long before, long after)
        {
            Before = before;
            After = after;
        }
    }

    /// <summary>
    /// A proposed or completed attempt. Price describes the full requested trade, not a payment.
    /// Dynamic stock rejections have no Price. UnitPrice is the first marginal price, not an average.
    /// </summary>
    public sealed class TradeAttempt
    {
        public TradeDirection Direction { get; }
        public string GoodId { get; }
        public int Quantity { get; }
        public TradePrice? Price { get; }
        public long UnitPrice => Price?.FirstMarginalPrice ?? 0;
        public long TotalAmount => Price?.TotalAmount ?? 0;
        public int ExecutedQuantity => Outcome == TradeOutcome.Traded ? Quantity : 0;
        public long ExecutedAmount => Outcome == TradeOutcome.Traded ? TotalAmount : 0;
        public TradeOutcome Outcome { get; }
        public InventoryChange MarketInventoryChange { get; }
        public InventoryChange TraderInventoryChange { get; }
        public CashChange MarketCashChange { get; }
        public CashChange TraderCashChange { get; }

        internal TradeAttempt(TradeCommand command, TradePrice? price, TradeOutcome outcome,
            InventoryChange marketInventoryChange, InventoryChange traderInventoryChange,
            CashChange marketCashChange, CashChange traderCashChange)
        {
            Direction = command.Direction;
            GoodId = command.GoodId;
            Quantity = command.Quantity;
            Price = price;
            Outcome = outcome;
            MarketInventoryChange = marketInventoryChange;
            TraderInventoryChange = traderInventoryChange;
            MarketCashChange = marketCashChange;
            TraderCashChange = traderCashChange;
        }
    }
}
