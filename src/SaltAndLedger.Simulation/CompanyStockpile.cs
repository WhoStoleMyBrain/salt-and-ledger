using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;

namespace SaltAndLedger.Simulation
{
    /// <summary>One physical inventory and one FIFO store belonging to a company at a port.</summary>
    public sealed class CompanyStockpileSnapshot
    {
        public string Id { get; }
        public string OwnerId { get; }
        public string PortId { get; }
        public int Capacity { get; }
        public int StoredUnits { get; }
        public IReadOnlyDictionary<string, int> Inventory { get; }

        internal CompanyStockpileSnapshot(string id, string ownerId, string portId, int capacity,
            IReadOnlyDictionary<string, int> inventory)
        {
            Id = id; OwnerId = ownerId; PortId = portId; Capacity = capacity;
            var quantities = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in inventory)
            {
                if (entry.Value < 0) throw new ArgumentOutOfRangeException(nameof(inventory));
                quantities.Add(entry.Key, entry.Value);
            }
            Inventory = new ReadOnlyDictionary<string, int>(quantities);
            StoredUnits = quantities.Values.Aggregate(0, (sum, value) => checked(sum + value));
            if (capacity < StoredUnits) throw new ArgumentException("Company stock exceeds its constructed capacity.");
        }

        public static string StoreId(string ownerId, string portId)
        {
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            return "company:" + ownerId + ":" + portId;
        }
    }

    public sealed partial class RegionalWorldSnapshot
    {
        [JsonIgnore]
        public bool UsesPooledCompanyStorage => RunIdentity?.CompanyStorageRules == CompanyStorageRules.Pooled;
        public IReadOnlyDictionary<string, CompanyStockpileSnapshot> CompanyStockpiles { get; }
        public bool ShouldSerializeCompanyStockpiles() => UsesPooledCompanyStorage;

        /// <summary>The authoritative material store used by a facility. Legacy stores remain separate.</summary>
        public CompanyStockpileSnapshot GetFacilityStorage(string facilityId)
        {
            var facility = PrivateFacilities[facilityId];
            return UsesPooledCompanyStorage ? CompanyStockpiles[CompanyStockpileSnapshot.StoreId(facility.OwnerId, facility.PortId)] :
                new CompanyStockpileSnapshot("facility:" + facility.Id, facility.OwnerId, facility.PortId,
                    facility.Capacity, facility.Inventory);
        }

        /// <summary>Installed operational capacity for this exact recipe's outputs; pending sites add no ceiling.</summary>
        public int? FacilityOutputStockLimit(string facilityId)
        {
            var facility = PrivateFacilities[facilityId];
            if (!UsesPooledCompanyStorage) return facility.Blueprint.OutputStockLimit;
            return AggregateOutputLimit(PrivateFacilities.Values.Where(f => f.OwnerId == facility.OwnerId &&
                f.PortId == facility.PortId && f.Blueprint.RecipeId == facility.Blueprint.RecipeId && f.IsOperational)
                .Select(f => f.Blueprint.OutputStockLimit));
        }

        internal static int? AggregateOutputLimit(IEnumerable<int?> limits)
        {
            var total = 0L;
            foreach (var limit in limits)
            {
                if (!limit.HasValue) return null;
                total = checked(total + limit.Value);
            }
            // Physical quantities remain int-bounded even when installed output ceilings sum beyond that.
            return (int)Math.Min(int.MaxValue, total);
        }

        /// <summary>Exact dock-bound, no-cash transfer preview. Does not grant permission to automate a manual order.</summary>
        public RegionalTransfer PreviewTransferGoods(string ownerId, string shipId, string facilityId,
            TransferDirection direction, string goodId, int quantity)
        {
            ValidateWholeCargo(quantity);
            var store = TransferStorage(ownerId, shipId, facilityId, direction);
            var ship = Ships[shipId];
            var source = direction == TransferDirection.ShipToWarehouse ? ship.Cargo[goodId] : store.Inventory[goodId];
            var free = direction == TransferDirection.ShipToWarehouse ? store.Capacity - store.StoredUnits : ship.CargoCapacity - ship.CargoUnits;
            return new RegionalTransfer(ownerId, shipId, facilityId, direction, goodId, quantity,
                TransferOutcomeFor(source, free, quantity));
        }

        public int MaximumTransferQuantity(string ownerId, string shipId, string facilityId,
            TransferDirection direction, string goodId)
        {
            var store = TransferStorage(ownerId, shipId, facilityId, direction);
            var ship = Ships[shipId];
            var quantity = direction == TransferDirection.ShipToWarehouse
                ? Math.Min(ship.Cargo[goodId], store.Capacity - store.StoredUnits)
                : Math.Min(store.Inventory[goodId], ship.CargoCapacity - ship.CargoUnits);
            return quantity - quantity % QuantityScale;
        }

        private CompanyStockpileSnapshot TransferStorage(string ownerId, string shipId, string facilityId, TransferDirection direction)
        {
            if (!Enum.IsDefined(typeof(TransferDirection), direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            var facility = OwnedFacility(ownerId, facilityId);
            var ship = Ships[shipId];
            if (ship.OwnerId != ownerId) throw new InvalidOperationException("The ship belongs to another company.");
            if (ship.AutomationEnabled) throw new InvalidOperationException("Disable ship automation before a manual transfer.");
            if (ship.DockedPortId != facility.PortId) throw new InvalidOperationException("Dock this ship at the company's warehouse port first.");
            return GetFacilityStorage(facilityId);
        }

        internal static TransferOutcome TransferOutcomeFor(int source, int free, int quantity) =>
            source < quantity ? TransferOutcome.InsufficientInventory : free < quantity
                ? TransferOutcome.InsufficientCapacity : TransferOutcome.Transferred;
    }
}
