using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class GoodDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        internal GoodDefinition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    public sealed class BuildingDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        internal BuildingDefinition(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    public sealed class RecipeQuantity
    {
        public string GoodId { get; }
        public int Quantity { get; }

        internal RecipeQuantity(string goodId, int quantity)
        {
            GoodId = goodId;
            Quantity = quantity;
        }
    }

    public enum RecipeKind
    {
        Transformation,
        Extraction
    }

    public sealed class RecipeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string BuildingId { get; }
        public RecipeKind Kind { get; }
        public IReadOnlyList<RecipeQuantity> Inputs { get; }
        public IReadOnlyList<RecipeQuantity> Outputs { get; }

        internal RecipeDefinition(string id, string name, string description,
            string buildingId, RecipeKind kind, IEnumerable<RecipeQuantity> inputs, IEnumerable<RecipeQuantity> outputs)
        {
            Id = id;
            Name = name;
            Description = description;
            BuildingId = buildingId;
            Kind = kind;
            Inputs = Array.AsReadOnly(inputs.ToArray());
            Outputs = Array.AsReadOnly(outputs.ToArray());
        }
    }

    public sealed class ContentCatalog
    {
        private readonly Dictionary<string, GoodDefinition> goodsById;
        private readonly Dictionary<string, BuildingDefinition> buildingsById;
        private readonly Dictionary<string, RecipeDefinition> recipesById;

        public int SchemaVersion => 1;
        public IReadOnlyList<GoodDefinition> Goods { get; }
        public IReadOnlyList<BuildingDefinition> Buildings { get; }
        public IReadOnlyList<RecipeDefinition> Recipes { get; }

        internal ContentCatalog(IEnumerable<GoodDefinition> goods, IEnumerable<BuildingDefinition> buildings,
            IEnumerable<RecipeDefinition> recipes)
        {
            Goods = Array.AsReadOnly(goods.ToArray());
            Buildings = Array.AsReadOnly(buildings.ToArray());
            Recipes = Array.AsReadOnly(recipes.ToArray());
            goodsById = Goods.ToDictionary(good => good.Id, StringComparer.Ordinal);
            buildingsById = Buildings.ToDictionary(building => building.Id, StringComparer.Ordinal);
            recipesById = Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);
        }

        public GoodDefinition GetGood(string id) => goodsById[id];
        public BuildingDefinition GetBuilding(string id) => buildingsById[id];
        public RecipeDefinition GetRecipe(string id) => recipesById[id];
    }
}
