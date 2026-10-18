namespace SaltAndLedger.Simulation
{
    /// <summary>Sets one facility's production flag at the start of a world step.</summary>
    public sealed class SetProductionEnabledCommand
    {
        public string BuildingInstanceId { get; }
        public bool Enabled { get; }

        public SetProductionEnabledCommand(string buildingInstanceId, bool enabled)
        {
            RuntimeIdentifier.Validate(buildingInstanceId, nameof(buildingInstanceId));
            BuildingInstanceId = buildingInstanceId;
            Enabled = enabled;
        }
    }
}
