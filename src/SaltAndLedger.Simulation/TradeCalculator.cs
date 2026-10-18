using System.Collections.Generic;

namespace SaltAndLedger.Simulation
{
    // Shared accounting for single-location and maritime worlds. No state ownership or location policy.
    internal static class TradeCalculator
    {
        internal static TradeAttempt Preview(MarketQuote quote, TradeCommand command,
            int marketQuantity, int traderQuantity, long marketCash, long traderCash,
            int? availableCargoCapacity = null)
        {
            var buy = command.Direction == TradeDirection.Buy;
            // Preserve fixed-mode multiplication-first precedence. Dynamic requests need
            // sufficient seller stock and cargo room before calculating an execution path.
            TradePrice? price = quote.IsFixed ? quote.Price(command, marketQuantity) : null;
            var outcome = TradeOutcome.Traded;
            if (buy && marketQuantity < command.Quantity) outcome = TradeOutcome.InsufficientMarketInventory;
            else if (!buy && traderQuantity < command.Quantity) outcome = TradeOutcome.InsufficientTraderInventory;
            else if (buy && availableCargoCapacity.HasValue && command.Quantity > availableCargoCapacity.Value)
                outcome = TradeOutcome.InsufficientCargoCapacity;
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

        // Callers supply only privately staged inventories and publish their complete state atomically.
        internal static void Apply(TradeAttempt attempt, Inventory marketInventory, Inventory traderInventory)
        {
            if (attempt.Outcome != TradeOutcome.Traded) return;
            var marketUpdate = new Dictionary<string, int> { [attempt.GoodId] = attempt.MarketInventoryChange.After };
            var traderUpdate = new Dictionary<string, int> { [attempt.GoodId] = attempt.TraderInventoryChange.After };
            marketInventory.Commit(marketUpdate);
            traderInventory.Commit(traderUpdate);
        }
    }
}
