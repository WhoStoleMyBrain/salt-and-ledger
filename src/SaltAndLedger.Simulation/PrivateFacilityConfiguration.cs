using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SaltAndLedger.Simulation
{
    /// <summary>An explicit scenario endowment, never a construction or inventory purchase.</summary>
    public sealed class PrivateFacilityConfiguration
    {
        public string Id { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public string BlueprintId { get; }
        public string Provenance { get; }
        public IndustryPolicy Policy { get; }
        public bool Enabled { get; }
        public IReadOnlyDictionary<string, int> InitialInventory { get; }

        public PrivateFacilityConfiguration(string id, string ownerId, string portId, string blueprintId,
            IReadOnlyDictionary<string, int>? initialInventory = null, IndustryPolicy? policy = null,
            bool enabled = true, string provenance = "scenario_endowment")
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            RuntimeIdentifier.Validate(blueprintId, nameof(blueprintId));
            if (string.IsNullOrWhiteSpace(provenance)) throw new ArgumentException("Endowment provenance is required.");
            var stock = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (initialInventory != null)
                foreach (var entry in initialInventory)
                {
                    RuntimeIdentifier.Validate(entry.Key, nameof(initialInventory));
                    if (entry.Value < 0) throw new ArgumentOutOfRangeException(nameof(initialInventory));
                    stock.Add(entry.Key, entry.Value);
                }
            Id = id; OwnerId = ownerId; PortId = portId; BlueprintId = blueprintId;
            InitialInventory = new ReadOnlyDictionary<string, int>(stock);
            Policy = policy ?? new IndustryPolicy(); Enabled = enabled; Provenance = provenance;
        }
    }
}
