using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>An immutable view of a complete world state, safe to retain across steps.</summary>
    public sealed class WorldSnapshot
    {
        public long Tick { get; }
        public string LocationId { get; }
        public IReadOnlyDictionary<string, int> Inventory { get; }
        public IReadOnlyList<BuildingInstance> Buildings { get; }
        public TradingSnapshot? Trading { get; }

        /// <summary>Derives stock-feasible marginal prices; cash is checked by PreviewTrade.</summary>
        public MarketPrice GetMarketQuote(string goodId)
        {
            if (goodId == null) throw new ArgumentNullException(nameof(goodId));
            if (Trading == null) throw new InvalidOperationException("Trading requires market configuration.");
            return Trading.Quotes[goodId].AtStock(Inventory[goodId]);
        }

        /// <summary>
        /// Read-only evaluation against this snapshot, using the execution calculator.
        /// Reported transfers are hypothetical until a matching command is stepped.
        /// </summary>
        public TradeAttempt PreviewTrade(TradeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (Trading == null) throw new InvalidOperationException("Trading requires market configuration.");
            return TradingState.Preview(Trading.Quotes[command.GoodId], command,
                Inventory[command.GoodId], Trading.TraderInventory[command.GoodId], Trading.MarketCash, Trading.TraderCash);
        }

        internal WorldSnapshot(long tick, string locationId, IReadOnlyDictionary<string, int> inventory,
            IEnumerable<BuildingInstance> buildings, TradingSnapshot? trading = null)
        {
            Tick = tick;
            LocationId = locationId;
            Trading = trading;
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var quantity in inventory) quantities.Add(quantity.Key, quantity.Value);
            Inventory = new ReadOnlyDictionary<string, int>(quantities);
            Buildings = Array.AsReadOnly(buildings.OrderBy(building => building.Priority)
                .ThenBy(building => building.Id, StringComparer.Ordinal).ToArray());
        }
    }
}
