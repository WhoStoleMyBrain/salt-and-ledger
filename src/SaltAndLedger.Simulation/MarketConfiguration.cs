using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        public long Bid => IsFixed ? fixedBid : throw new InvalidOperationException("Dynamic bids require market stock; use WorldSnapshot.GetMarketQuote.");
        public long Ask => IsFixed ? fixedAsk : throw new InvalidOperationException("Dynamic asks require market stock; use WorldSnapshot.GetMarketQuote.");

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

    /// <summary>Immutable initial accounts and pricing configurations for one local market and one trader.</summary>
    public sealed class MarketConfiguration
    {
        public IReadOnlyDictionary<string, MarketQuote> Quotes { get; }
        public long InitialMarketCash { get; }
        public string TraderId { get; }
        public long InitialTraderCash { get; }
        public IReadOnlyDictionary<string, int> InitialTraderInventory { get; }

        public MarketConfiguration(IEnumerable<MarketQuote> quotes, long initialMarketCash,
            string traderId, long initialTraderCash, IReadOnlyDictionary<string, int>? initialTraderInventory = null)
        {
            if (quotes == null) throw new ArgumentNullException(nameof(quotes));
            RuntimeIdentifier.Validate(traderId, nameof(traderId));
            if (initialMarketCash < 0) throw new ArgumentOutOfRangeException(nameof(initialMarketCash));
            if (initialTraderCash < 0) throw new ArgumentOutOfRangeException(nameof(initialTraderCash));
            var prices = new SortedDictionary<string, MarketQuote>(StringComparer.Ordinal);
            foreach (var quote in quotes)
            {
                if (quote == null) throw new ArgumentException("Quotes cannot contain null.", nameof(quotes));
                prices.Add(quote.GoodId, quote);
            }
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (initialTraderInventory != null)
            {
                foreach (var quantity in initialTraderInventory)
                {
                    RuntimeIdentifier.Validate(quantity.Key, nameof(initialTraderInventory));
                    if (quantity.Value < 0) throw new ArgumentOutOfRangeException(nameof(initialTraderInventory));
                    quantities.Add(quantity.Key, quantity.Value);
                }
            }
            Quotes = new ReadOnlyDictionary<string, MarketQuote>(prices);
            InitialMarketCash = initialMarketCash;
            TraderId = traderId;
            InitialTraderCash = initialTraderCash;
            InitialTraderInventory = new ReadOnlyDictionary<string, int>(quantities);
        }

        internal TradingSnapshot CreateSnapshot(ContentCatalog content)
        {
            foreach (var goodId in Quotes.Keys) content.GetGood(goodId);
            var inventory = new Inventory(content, InitialTraderInventory);
            return new TradingSnapshot(Quotes, InitialMarketCash, TraderId, InitialTraderCash, inventory.Snapshot());
        }
    }
}
