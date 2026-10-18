using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaltAndLedger.Simulation
{
    /// <summary>Fixed prices in indivisible currency units, from the trader's perspective.</summary>
    public sealed class MarketQuote
    {
        public string GoodId { get; }
        public long Bid { get; }
        public long Ask { get; }

        public MarketQuote(string goodId, long bid, long ask)
        {
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (bid <= 0) throw new ArgumentOutOfRangeException(nameof(bid), "Bid must be positive.");
            if (ask < bid) throw new ArgumentOutOfRangeException(nameof(ask), "Ask must be at least bid.");
            GoodId = goodId;
            Bid = bid;
            Ask = ask;
        }
    }

    /// <summary>Immutable initial accounts and fixed quotes for one local market and one trader.</summary>
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
