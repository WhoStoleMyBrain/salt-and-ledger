namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// Marginal prices at one immutable stock state. Null means the market stock boundary
    /// prevents that direction; PreviewTrade also checks cash and trader inventory.
    /// </summary>
    public sealed class MarketPrice
    {
        public string GoodId { get; }
        public int MarketStock { get; }
        public long? Bid { get; }
        public long? Ask { get; }

        internal MarketPrice(string goodId, int marketStock, long? bid, long? ask)
        {
            GoodId = goodId;
            MarketStock = marketStock;
            Bid = bid;
            Ask = ask;
        }
    }
}
