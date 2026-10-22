using System;
using System.Collections.Generic;

namespace SaltAndLedger.Simulation
{
    public static class Production
    {
        /// <summary>
        /// Applies one complete recipe batch. A shortage returns false. Invalid IDs or
        /// building association and integer overflow throw; all leave stock unchanged.
        /// This command has no time, randomness, or implicit production loop.
        /// </summary>
        public static bool TryRunBatch(Inventory inventory, string buildingId, string recipeId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            inventory.Content.GetBuilding(buildingId);
            var recipe = inventory.Content.GetRecipe(recipeId);
            if (!string.Equals(recipe.BuildingId, buildingId, StringComparison.Ordinal))
                throw new ArgumentException("Recipe '" + recipeId + "' does not belong to building '" + buildingId + "'.", nameof(buildingId));

            foreach (var input in recipe.Inputs)
                if (inventory.GetQuantity(input.GoodId) < checked(input.Quantity * inventory.QuantityScale)) return false;

            // Stage every final balance before committing any change. Subtracting inputs
            // first also handles a good appearing on both sides without false overflow.
            var updated = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var input in recipe.Inputs)
                updated[input.GoodId] = inventory.GetQuantity(input.GoodId) - checked(input.Quantity * inventory.QuantityScale);
            foreach (var output in recipe.Outputs)
            {
                var current = updated.TryGetValue(output.GoodId, out var staged)
                    ? staged : inventory.GetQuantity(output.GoodId);
                updated[output.GoodId] = checked(current + checked(output.Quantity * inventory.QuantityScale));
            }
            inventory.Commit(updated);
            return true;
        }
    }
}
