using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaltAndLedger.Simulation
{
    /// <summary>Detached accounts. Market goods remain in WorldSnapshot.Inventory.</summary>
    public sealed class TradingSnapshot
    {
        public IReadOnlyDictionary<string, MarketQuote> Quotes { get; }
        public long MarketCash { get; }
        public string TraderId { get; }
        public long TraderCash { get; }
        public IReadOnlyDictionary<string, int> TraderInventory { get; }

        internal TradingSnapshot(IReadOnlyDictionary<string, MarketQuote> quotes, long marketCash,
            string traderId, long traderCash, IReadOnlyDictionary<string, int> traderInventory)
        {
            var prices = new SortedDictionary<string, MarketQuote>(StringComparer.Ordinal);
            foreach (var quote in quotes) prices.Add(quote.Key, quote.Value);
            Quotes = new ReadOnlyDictionary<string, MarketQuote>(prices);
            MarketCash = marketCash;
            TraderId = traderId;
            TraderCash = traderCash;
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var quantity in traderInventory) quantities.Add(quantity.Key, quantity.Value);
            TraderInventory = new ReadOnlyDictionary<string, int>(quantities);
        }
    }
}
