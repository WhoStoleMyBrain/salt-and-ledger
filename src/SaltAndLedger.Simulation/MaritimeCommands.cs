namespace SaltAndLedger.Simulation
{
    /// <summary>A local production control, qualified by port and building instance.</summary>
    public sealed class PortProductionCommand
    {
        public string PortId { get; }
        public string BuildingInstanceId { get; }
        public bool Enabled { get; }

        public PortProductionCommand(string portId, string buildingInstanceId, bool enabled)
        {
            RuntimeIdentifier.Validate(portId, nameof(portId));
            RuntimeIdentifier.Validate(buildingInstanceId, nameof(buildingInstanceId));
            PortId = portId;
            BuildingInstanceId = buildingInstanceId;
            Enabled = enabled;
        }
    }

    /// <summary>A trade at the identified port; execution requires the ship to start there.</summary>
    public sealed class PortTradeCommand
    {
        public string PortId { get; }
        public TradeCommand Trade { get; }

        public PortTradeCommand(string portId, TradeDirection direction, string goodId, int quantity)
        {
            RuntimeIdentifier.Validate(portId, nameof(portId));
            PortId = portId;
            Trade = new TradeCommand(direction, goodId, quantity);
        }
    }

    /// <summary>Depart the starting port for the other port after this step's trades.</summary>
    public sealed class SailCommand
    {
        public string DestinationPortId { get; }

        public SailCommand(string destinationPortId)
        {
            RuntimeIdentifier.Validate(destinationPortId, nameof(destinationPortId));
            DestinationPortId = destinationPortId;
        }
    }
}
