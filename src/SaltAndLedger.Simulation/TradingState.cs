namespace SaltAndLedger.Simulation
{
    // Private working accounts for a single step; never exposed by the world.
    internal sealed class TradingState
    {
        private readonly TradingSnapshot before;
        private readonly Inventory traderInventory;
        private long marketCash;
        private long traderCash;

        internal TradingState(ContentCatalog content, TradingSnapshot before)
        {
            this.before = before;
            traderInventory = new Inventory(content, before.TraderInventory);
            marketCash = before.MarketCash;
            traderCash = before.TraderCash;
        }

        internal TradeAttempt Execute(Inventory marketInventory, TradeCommand command)
        {
            var attempt = TradeCalculator.Preview(before.Quotes[command.GoodId], command,
                marketInventory.GetQuantity(command.GoodId), traderInventory.GetQuantity(command.GoodId), marketCash, traderCash);
            TradeCalculator.Apply(attempt, marketInventory, traderInventory);
            marketCash = attempt.MarketCashChange.After;
            traderCash = attempt.TraderCashChange.After;
            return attempt;
        }

        internal TradingSnapshot Snapshot() => new TradingSnapshot(before.Quotes, marketCash,
            before.TraderId, traderCash, traderInventory.Snapshot());
    }
}
