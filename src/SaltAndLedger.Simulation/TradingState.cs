using System.Collections.Generic;

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
            var buy = command.Direction == TradeDirection.Buy;
            var quote = before.Quotes[command.GoodId];
            var price = buy ? quote.Ask : quote.Bid;
            var total = checked(command.Quantity * price);
            var marketQuantity = marketInventory.GetQuantity(command.GoodId);
            var traderQuantity = traderInventory.GetQuantity(command.GoodId);
            var outcome = TradeOutcome.Traded;
            if (buy && marketQuantity < command.Quantity) outcome = TradeOutcome.InsufficientMarketInventory;
            else if (!buy && traderQuantity < command.Quantity) outcome = TradeOutcome.InsufficientTraderInventory;
            else if (buy && traderCash < total) outcome = TradeOutcome.InsufficientTraderFunds;
            else if (!buy && marketCash < total) outcome = TradeOutcome.InsufficientMarketFunds;

            var nextMarketQuantity = marketQuantity;
            var nextTraderQuantity = traderQuantity;
            var nextMarketCash = marketCash;
            var nextTraderCash = traderCash;
            if (outcome == TradeOutcome.Traded)
            {
                // Check every destination before applying any part of the transfer.
                checked
                {
                    nextMarketQuantity = buy ? marketQuantity - command.Quantity : marketQuantity + command.Quantity;
                    nextTraderQuantity = buy ? traderQuantity + command.Quantity : traderQuantity - command.Quantity;
                    nextMarketCash = buy ? marketCash + total : marketCash - total;
                    nextTraderCash = buy ? traderCash - total : traderCash + total;
                }
            }

            var attempt = new TradeAttempt(command, price, total, outcome,
                new InventoryChange(command.GoodId, marketQuantity, nextMarketQuantity),
                new InventoryChange(command.GoodId, traderQuantity, nextTraderQuantity),
                new CashChange(marketCash, nextMarketCash), new CashChange(traderCash, nextTraderCash));
            if (outcome == TradeOutcome.Traded)
            {
                var marketUpdate = new Dictionary<string, int> { [command.GoodId] = nextMarketQuantity };
                var traderUpdate = new Dictionary<string, int> { [command.GoodId] = nextTraderQuantity };
                marketInventory.Commit(marketUpdate);
                traderInventory.Commit(traderUpdate);
                marketCash = nextMarketCash;
                traderCash = nextTraderCash;
            }
            return attempt;
        }

        internal TradingSnapshot Snapshot() => new TradingSnapshot(before.Quotes, marketCash,
            before.TraderId, traderCash, traderInventory.Snapshot());
    }
}
