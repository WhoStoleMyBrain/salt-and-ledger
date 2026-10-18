using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Immutable setup for one local market and its market-owned production.</summary>
    public sealed class PortConfiguration
    {
        public string Id { get; }
        public IReadOnlyDictionary<string, MarketQuote> Quotes { get; }
        public long InitialMarketCash { get; }
        public IReadOnlyList<BuildingInstance> Buildings { get; }
        public IReadOnlyDictionary<string, int> InitialQuantities { get; }

        public PortConfiguration(string id, IEnumerable<MarketQuote> quotes, long initialMarketCash,
            IEnumerable<BuildingInstance> buildings, IReadOnlyDictionary<string, int>? initialQuantities = null)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            if (quotes == null) throw new ArgumentNullException(nameof(quotes));
            if (buildings == null) throw new ArgumentNullException(nameof(buildings));
            if (initialMarketCash < 0) throw new ArgumentOutOfRangeException(nameof(initialMarketCash));
            var prices = new SortedDictionary<string, MarketQuote>(StringComparer.Ordinal);
            foreach (var quote in quotes)
            {
                if (quote == null) throw new ArgumentException("Quotes cannot contain null.", nameof(quotes));
                prices.Add(quote.GoodId, quote);
            }
            var instances = new List<BuildingInstance>();
            foreach (var building in buildings)
            {
                if (building == null) throw new ArgumentException("Buildings cannot contain null.", nameof(buildings));
                instances.Add(building);
            }
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (initialQuantities != null)
            {
                foreach (var quantity in initialQuantities)
                {
                    RuntimeIdentifier.Validate(quantity.Key, nameof(initialQuantities));
                    if (quantity.Value < 0) throw new ArgumentOutOfRangeException(nameof(initialQuantities));
                    quantities.Add(quantity.Key, quantity.Value);
                }
            }
            Id = id;
            InitialMarketCash = initialMarketCash;
            Quotes = new ReadOnlyDictionary<string, MarketQuote>(prices);
            Buildings = Array.AsReadOnly(instances.OrderBy(building => building.Priority)
                .ThenBy(building => building.Id, StringComparer.Ordinal).ToArray());
            InitialQuantities = new ReadOnlyDictionary<string, int>(quantities);
        }

        internal PortSnapshot CreateSnapshot(ContentCatalog content)
        {
            foreach (var goodId in Quotes.Keys) content.GetGood(goodId);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in Buildings)
            {
                if (!instanceIds.Add(building.Id))
                    throw new ArgumentException("Duplicate building instance ID '" + building.Id + "' in port '" + Id + "'.");
                content.GetBuilding(building.BuildingId);
                var recipe = content.GetRecipe(building.RecipeId);
                if (!string.Equals(recipe.BuildingId, building.BuildingId, StringComparison.Ordinal))
                    throw new ArgumentException("Recipe '" + building.RecipeId + "' does not belong to building '" + building.BuildingId + "'.");
            }
            var inventory = new Inventory(content, InitialQuantities);
            return new PortSnapshot(Id, inventory.Snapshot(), InitialMarketCash, Quotes, Buildings);
        }
    }
}
