using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaltAndLedger.Simulation
{
    public sealed class Inventory
    {
        private readonly Dictionary<string, int> quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        internal ContentCatalog Content { get; }
        public int QuantityScale { get; }

        public Inventory(ContentCatalog content, IReadOnlyDictionary<string, int>? initialQuantities = null,
            int quantityScale = 1)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            EconomicUnits.ValidateQuantityScale(quantityScale);
            QuantityScale = quantityScale;
            foreach (var good in content.Goods) quantities.Add(good.Id, 0);
            if (initialQuantities == null) return;
            foreach (var item in initialQuantities)
            {
                content.GetGood(item.Key);
                if (item.Value < 0) throw new ArgumentOutOfRangeException(nameof(initialQuantities), "Stock cannot be negative.");
                quantities[item.Key] = item.Value;
            }
        }

        public int GetQuantity(string goodId) => quantities[goodId];

        public IReadOnlyDictionary<string, int> Snapshot() =>
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(quantities, StringComparer.Ordinal));

        internal void Commit(IReadOnlyDictionary<string, int> updatedQuantities)
        {
            foreach (var item in updatedQuantities) quantities[item.Key] = item.Value;
        }
    }
}
