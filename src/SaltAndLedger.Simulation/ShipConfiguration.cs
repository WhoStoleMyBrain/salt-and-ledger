using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaltAndLedger.Simulation
{
    /// <summary>One initially docked ship; each item occupies one cargo-capacity unit.</summary>
    public sealed class ShipConfiguration
    {
        public string Id { get; }
        public int CargoCapacity { get; }
        public string InitialPortId { get; }
        public IReadOnlyDictionary<string, int> InitialCargo { get; }

        public ShipConfiguration(string id, int cargoCapacity, string initialPortId,
            IReadOnlyDictionary<string, int>? initialCargo = null)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(initialPortId, nameof(initialPortId));
            if (cargoCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(cargoCapacity));
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            long total = 0;
            if (initialCargo != null)
            {
                foreach (var quantity in initialCargo)
                {
                    RuntimeIdentifier.Validate(quantity.Key, nameof(initialCargo));
                    if (quantity.Value < 0) throw new ArgumentOutOfRangeException(nameof(initialCargo));
                    total = checked(total + quantity.Value);
                    quantities.Add(quantity.Key, quantity.Value);
                }
            }
            if (total > cargoCapacity)
                throw new ArgumentOutOfRangeException(nameof(initialCargo), "Initial cargo exceeds ship capacity.");
            Id = id;
            CargoCapacity = cargoCapacity;
            InitialPortId = initialPortId;
            InitialCargo = new ReadOnlyDictionary<string, int>(quantities);
        }
    }
}
