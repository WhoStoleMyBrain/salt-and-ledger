namespace SaltAndLedger.Simulation
{
    /// <summary>
    /// An immutable runtime description of one facility. Its definition and recipe
    /// remain shared catalog content; a world replaces this value when controls change.
    /// </summary>
    public sealed class BuildingInstance
    {
        public string Id { get; }
        public string BuildingId { get; }
        public string RecipeId { get; }
        public int Priority { get; }
        public bool ProductionEnabled { get; }

        public BuildingInstance(string id, string buildingId, string recipeId,
            int priority = 0, bool productionEnabled = true)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(buildingId, nameof(buildingId));
            RuntimeIdentifier.Validate(recipeId, nameof(recipeId));
            Id = id;
            BuildingId = buildingId;
            RecipeId = recipeId;
            Priority = priority;
            ProductionEnabled = productionEnabled;
        }

        internal BuildingInstance WithProductionEnabled(bool enabled) =>
            enabled == ProductionEnabled ? this : new BuildingInstance(Id, BuildingId, RecipeId, Priority, enabled);
    }
}
