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
