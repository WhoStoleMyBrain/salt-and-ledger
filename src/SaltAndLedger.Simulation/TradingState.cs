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
            var attempt = Preview(before.Quotes[command.GoodId], command,
                marketInventory.GetQuantity(command.GoodId), traderInventory.GetQuantity(command.GoodId), marketCash, traderCash);
            if (attempt.Outcome == TradeOutcome.Traded)
            {
                var marketUpdate = new Dictionary<string, int> { [command.GoodId] = attempt.MarketInventoryChange.After };
                var traderUpdate = new Dictionary<string, int> { [command.GoodId] = attempt.TraderInventoryChange.After };
                marketInventory.Commit(marketUpdate);
                traderInventory.Commit(traderUpdate);
                marketCash = attempt.MarketCashChange.After;
                traderCash = attempt.TraderCashChange.After;
            }
            return attempt;
        }

        internal static TradeAttempt Preview(MarketQuote quote, TradeCommand command,
            int marketQuantity, int traderQuantity, long marketCash, long traderCash)
        {
            var buy = command.Direction == TradeDirection.Buy;
            // Fixed mode retains Task 1.2's total-overflow-before-shortage precedence.
            // Dynamic mode prices only a feasible market inventory path, after seller-stock checks.
            TradePrice? price = quote.IsFixed ? quote.Price(command, marketQuantity) : null;
            var outcome = TradeOutcome.Traded;
            if (buy && marketQuantity < command.Quantity) outcome = TradeOutcome.InsufficientMarketInventory;
            else if (!buy && traderQuantity < command.Quantity) outcome = TradeOutcome.InsufficientTraderInventory;
            if (outcome == TradeOutcome.Traded)
            {
                price = price ?? quote.Price(command, marketQuantity);
                if (buy && traderCash < price.TotalAmount) outcome = TradeOutcome.InsufficientTraderFunds;
                else if (!buy && marketCash < price.TotalAmount) outcome = TradeOutcome.InsufficientMarketFunds;
            }

            var nextMarketQuantity = marketQuantity;
            var nextTraderQuantity = traderQuantity;
            var nextMarketCash = marketCash;
            var nextTraderCash = traderCash;
            if (outcome == TradeOutcome.Traded)
            {
                var total = price!.TotalAmount;
                // Check every destination before applying any part of the transfer.
                checked
                {
                    nextMarketQuantity = buy ? marketQuantity - command.Quantity : marketQuantity + command.Quantity;
                    nextTraderQuantity = buy ? traderQuantity + command.Quantity : traderQuantity - command.Quantity;
                    nextMarketCash = buy ? marketCash + total : marketCash - total;
                    nextTraderCash = buy ? traderCash - total : traderCash + total;
                }
            }

            return new TradeAttempt(command, price, outcome,
                new InventoryChange(command.GoodId, marketQuantity, nextMarketQuantity),
                new InventoryChange(command.GoodId, traderQuantity, nextTraderQuantity),
                new CashChange(marketCash, nextMarketCash), new CashChange(traderCash, nextTraderCash));
        }

        internal TradingSnapshot Snapshot() => new TradingSnapshot(before.Quotes, marketCash,
            before.TraderId, traderCash, traderInventory.Snapshot());
    }
}
