using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    /// <summary>Direct recipe dependencies only; inventories and indirect production chains are unrestricted.</summary>
    public static class ProductionSpecialization
    {
        public static bool AreIncompatible(RecipeDefinition first, RecipeDefinition second)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            if (first.Id == second.Id) return false;
            return first.Outputs.Any(output => second.Inputs.Any(input => input.GoodId == output.GoodId)) ||
                second.Outputs.Any(output => first.Inputs.Any(input => input.GoodId == output.GoodId));
        }

        public static string? Conflict(ContentCatalog content, IEnumerable<string> recipeIds)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (recipeIds == null) throw new ArgumentNullException(nameof(recipeIds));
            var recipes = recipeIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
                .Select(content.GetRecipe).ToArray();
            for (var first = 0; first < recipes.Length; first++)
                for (var second = first + 1; second < recipes.Length; second++)
                    if (AreIncompatible(recipes[first], recipes[second]))
                    {
                        var goods = recipes[first].Outputs.Where(output => recipes[second].Inputs.Any(input => input.GoodId == output.GoodId))
                            .Concat(recipes[second].Outputs.Where(output => recipes[first].Inputs.Any(input => input.GoodId == output.GoodId)))
                            .Select(output => output.GoodId).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
                        return "Recipes '" + recipes[first].Id + "' and '" + recipes[second].Id +
                            "' cannot share a town: a local producer supplies the other's input (" + string.Join(", ", goods) + ").";
                    }
            return null;
        }

        /// <summary>Royal services consume canonical materials even though their outputs are ships or currency.</summary>
        public static string? InstitutionConflict(ContentCatalog content, IEnumerable<string> recipeIds,
            string portId, ExpandingEconomyConfiguration economy)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (recipeIds == null) throw new ArgumentNullException(nameof(recipeIds));
            if (economy == null) throw new ArgumentNullException(nameof(economy));
            var recipes = recipeIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)
                .Select(content.GetRecipe).ToArray();
            string? ConsumedInput(string good, string institution)
            {
                var producer = recipes.FirstOrDefault(recipe => recipe.Outputs.Any(output => output.GoodId == good));
                return producer == null ? null : "Recipe '" + producer.Id + "' produces '" + good +
                    "', an input consumed by the local " + institution + ".";
            }
            if (portId == economy.MintPortId)
            {
                var conflict = ConsumedInput(ExpandingEconomyConfiguration.MintInputGoodId, "Royal Mint");
                if (conflict != null) return conflict;
            }
            return portId == economy.ShipwrightPortId
                ? ConsumedInput(ExpandingEconomyConfiguration.ShipMaterialGoodId, "Shipyard") : null;
        }
    }
}
