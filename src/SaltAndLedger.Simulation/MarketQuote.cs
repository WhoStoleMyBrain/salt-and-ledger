using System;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    /// <summary>Immutable fixed or inventory-sensitive pricing configuration for one good.</summary>
    public sealed class MarketQuote
    {
        private readonly long fixedBid;
        private readonly long fixedAsk;
        public string GoodId { get; }
        public InventoryPricing? Pricing { get; }
        public bool IsFixed => Pricing == null;
        public long Bid => IsFixed ? fixedBid : throw new InvalidOperationException("Dynamic bids require market stock; use RegionalWorldSnapshot.GetMarketQuote.");
        public long Ask => IsFixed ? fixedAsk : throw new InvalidOperationException("Dynamic asks require market stock; use RegionalWorldSnapshot.GetMarketQuote.");

        public MarketQuote(string goodId, long bid, long ask)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (bid <= 0) throw new ArgumentOutOfRangeException(nameof(bid), "Bid must be positive.");
            if (ask < bid) throw new ArgumentOutOfRangeException(nameof(ask), "Ask must be at least bid.");
            GoodId = goodId;
            fixedBid = bid;
            fixedAsk = ask;
        }

        public MarketQuote(string goodId, InventoryPricing pricing)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            GoodId = goodId;
            Pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        }

        internal TradePrice Price(TradeCommand command, int marketStock)
        {
            if (Pricing != null) return Pricing.Quote(command.Direction, marketStock, command.Quantity);
            var price = command.Direction == TradeDirection.Buy ? fixedAsk : fixedBid;
            return new TradePrice(command.Quantity, price, price, checked(command.Quantity * price));
        }

        internal bool FitsBudget(TradeDirection direction, int marketStock, int quantity, long budget)
        {
            var total = Pricing != null ? Pricing.CalculateTotal(direction, marketStock, quantity) :
                (BigInteger)quantity * (direction == TradeDirection.Buy ? fixedAsk : fixedBid);
            return total <= budget;
        }

        internal MarketPrice AtStock(int marketStock)
        {
            // Null means the market-side stock boundary makes a one-unit movement impossible.
            long? bid = marketStock == int.MaxValue ? (long?)null :
                Pricing?.PriceAtEdge(TradeDirection.Sell, checked(marketStock + 1)) ?? fixedBid;
            long? ask = marketStock == 0 ? (long?)null :
                Pricing?.PriceAtEdge(TradeDirection.Buy, marketStock) ?? fixedAsk;
            return new MarketPrice(GoodId, marketStock, bid, ask);
        }
    }

}
