using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum ProductionOutcome
    {
        Produced,
        ResourceShortage
    }

    public sealed class InventoryChange
    {
        public string GoodId { get; }
        public int Before { get; }
        public int After { get; }
        public int Delta => checked(After - Before);

        internal InventoryChange(string goodId, int before, int after)
        {
            GoodId = goodId;
            Before = before;
            After = after;
        }
    }

    public sealed class ProductionAttempt
    {
        public string BuildingInstanceId { get; }
        public string BuildingId { get; }
        public string RecipeId { get; }
        public ProductionOutcome Outcome { get; }
        public IReadOnlyList<InventoryChange> InventoryChanges { get; }

        internal ProductionAttempt(BuildingInstance building, ProductionOutcome outcome,
            IEnumerable<InventoryChange> inventoryChanges)
        {
            BuildingInstanceId = building.Id;
            BuildingId = building.BuildingId;
            RecipeId = building.RecipeId;
            Outcome = outcome;
            InventoryChanges = Array.AsReadOnly(inventoryChanges.ToArray());
        }
    }

    /// <summary>
    /// A completed step: Tick identifies its starting state, and After contains Tick + 1.
    /// The caller owns retention; the world does not accumulate a history of results.
    /// </summary>
    public sealed class StepResult
    {
        public long Tick { get; }
        public WorldSnapshot After { get; }
        public IReadOnlyList<ProductionAttempt> ProductionAttempts { get; }

        internal StepResult(long tick, WorldSnapshot after, IEnumerable<ProductionAttempt> productionAttempts)
        {
            Tick = tick;
            After = after;
            ProductionAttempts = Array.AsReadOnly(productionAttempts.ToArray());
        }
    }
}
