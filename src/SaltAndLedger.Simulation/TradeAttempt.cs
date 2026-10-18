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

    /// <summary>TotalAmount is the requested quote total, including for rejected trades.</summary>
    public sealed class TradeAttempt
    {
        public TradeDirection Direction { get; }
        public string GoodId { get; }
        public int Quantity { get; }
        public long UnitPrice { get; }
        public long TotalAmount { get; }
        public TradeOutcome Outcome { get; }
        public InventoryChange MarketInventoryChange { get; }
        public InventoryChange TraderInventoryChange { get; }
        public CashChange MarketCashChange { get; }
        public CashChange TraderCashChange { get; }

        internal TradeAttempt(TradeCommand command, long unitPrice, long totalAmount, TradeOutcome outcome,
            InventoryChange marketInventoryChange, InventoryChange traderInventoryChange,
            CashChange marketCashChange, CashChange traderCashChange)
        {
            Direction = command.Direction;
            GoodId = command.GoodId;
            Quantity = command.Quantity;
            UnitPrice = unitPrice;
            TotalAmount = totalAmount;
            Outcome = outcome;
            MarketInventoryChange = marketInventoryChange;
            TraderInventoryChange = traderInventoryChange;
            MarketCashChange = marketCashChange;
            TraderCashChange = traderCashChange;
        }
    }
}
