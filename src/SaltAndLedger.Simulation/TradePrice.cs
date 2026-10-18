namespace SaltAndLedger.Simulation
{
    /// <summary>An exact proposed execution price, before stock ownership and cash checks.</summary>
    public sealed class TradePrice
    {
        public int Quantity { get; }
        public long FirstMarginalPrice { get; }
        public long LastMarginalPrice { get; }
        public long TotalAmount { get; }
        public bool IsUniform => FirstMarginalPrice == LastMarginalPrice;

        internal TradePrice(int quantity, long firstMarginalPrice, long lastMarginalPrice, long totalAmount)
        {
            Quantity = quantity;
            FirstMarginalPrice = firstMarginalPrice;
            LastMarginalPrice = lastMarginalPrice;
            TotalAmount = totalAmount;
        }
    }
}
