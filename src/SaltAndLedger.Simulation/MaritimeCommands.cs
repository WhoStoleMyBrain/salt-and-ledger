namespace SaltAndLedger.Simulation
{
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

    /// <summary>Depart for the identified port through the regional command path.</summary>
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
