using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public sealed class ScheduledFacilityConfiguration
    {
        public BuildingInstance Instance { get; }
        public int IntervalDays { get; }
        public int? OutputStockLimit { get; }
        public long FirstDueDay { get; }

        public ScheduledFacilityConfiguration(BuildingInstance instance, int intervalDays,
            int? outputStockLimit = null, long? firstDueDay = null)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            if (intervalDays <= 0) throw new ArgumentOutOfRangeException(nameof(intervalDays));
            if (outputStockLimit < 0) throw new ArgumentOutOfRangeException(nameof(outputStockLimit));
            if (firstDueDay.HasValue && firstDueDay.Value < 1) throw new ArgumentOutOfRangeException(nameof(firstDueDay));
            IntervalDays = intervalDays;
            OutputStockLimit = outputStockLimit;
            FirstDueDay = firstDueDay ?? intervalDays;
        }
    }

    public sealed class ConsumptionConfiguration
    {
        public string Id { get; }
        public string GoodId { get; }
        public int Quantity { get; }
        public int IntervalDays { get; }
        public long FirstDueDay { get; }

        public ConsumptionConfiguration(string id, string goodId, int quantity, int intervalDays,
            long? firstDueDay = null)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (intervalDays <= 0) throw new ArgumentOutOfRangeException(nameof(intervalDays));
            if (firstDueDay.HasValue && firstDueDay.Value < 1) throw new ArgumentOutOfRangeException(nameof(firstDueDay));
            Id = id;
            GoodId = goodId;
            Quantity = quantity;
            IntervalDays = intervalDays;
            FirstDueDay = firstDueDay ?? intervalDays;
        }
    }

    public sealed class RegionalPortConfiguration
    {
        public PortConfiguration Market { get; }
        public string Id => Market.Id;
        public string Name { get; }
        public int X { get; }
        public int Y { get; }
        public string Role { get; }
        public IReadOnlyList<ScheduledFacilityConfiguration> Facilities { get; }
        public IReadOnlyList<ConsumptionConfiguration> Consumption { get; }

        public RegionalPortConfiguration(PortConfiguration market, string name, int x, int y,
            IEnumerable<ScheduledFacilityConfiguration> facilities,
            IEnumerable<ConsumptionConfiguration> consumption, string role = "")
        {
            Market = market ?? throw new ArgumentNullException(nameof(market));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A port needs a name.", nameof(name));
            if (facilities == null) throw new ArgumentNullException(nameof(facilities));
            if (consumption == null) throw new ArgumentNullException(nameof(consumption));
            var configured = facilities.ToArray();
            var demand = consumption.ToArray();
            if (configured.Any(f => f == null) || demand.Any(c => c == null))
                throw new ArgumentException("Configuration entries cannot be null.");
            if (configured.Select(f => f.Instance.Id).Distinct(StringComparer.Ordinal).Count() != configured.Length ||
                demand.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != demand.Length)
                throw new ArgumentException("Facility and consumption IDs must be unique within a port.");
            if (configured.Length != market.Buildings.Count ||
                configured.Any(f => !market.Buildings.Any(b => b.Id == f.Instance.Id &&
                    b.BuildingId == f.Instance.BuildingId && b.RecipeId == f.Instance.RecipeId &&
                    b.Priority == f.Instance.Priority && b.ProductionEnabled == f.Instance.ProductionEnabled)))
                throw new ArgumentException("Each market building needs exactly its matching schedule.");
            Name = name;
            X = x;
            Y = y;
            Role = role ?? throw new ArgumentNullException(nameof(role));
            Facilities = Array.AsReadOnly(configured.OrderBy(f => f.Instance.Priority)
                .ThenBy(f => f.Instance.Id, StringComparer.Ordinal).ToArray());
            Consumption = Array.AsReadOnly(demand.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class SeaRoute
    {
        public string OriginPortId { get; }
        public string DestinationPortId { get; }
        public int Distance { get; }

        public SeaRoute(string originPortId, string destinationPortId, int distance)
        {
            RuntimeIdentifier.Validate(originPortId, nameof(originPortId));
            RuntimeIdentifier.Validate(destinationPortId, nameof(destinationPortId));
            if (originPortId == destinationPortId) throw new ArgumentException("Route ports must differ.");
            if (distance <= 0) throw new ArgumentOutOfRangeException(nameof(distance));
            OriginPortId = originPortId;
            DestinationPortId = destinationPortId;
            Distance = distance;
        }

        public int DurationDays(int speed)
        {
            if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            return checked((int)(((long)Distance + speed - 1) / speed));
        }

        internal string Key => string.CompareOrdinal(OriginPortId, DestinationPortId) < 0
            ? OriginPortId + "/" + DestinationPortId : DestinationPortId + "/" + OriginPortId;
    }

    /// <summary>One explicitly owned vessel and its finite trader account.</summary>
    public sealed class RegionalShipConfiguration
    {
        public ShipConfiguration Ship { get; }
        public string OwnerId { get; }
        public string Name { get; }
        public long InitialCash { get; }
        public int Speed { get; }
        public int Priority { get; }
        public bool IsPlayer { get; }
        public TradeRoutePolicy? Policy { get; }
        public bool AutomationEnabled { get; }
        public int MinimumOriginStock { get; }
        public int MinimumReturnStock { get; }

        public RegionalShipConfiguration(ShipConfiguration ship, string ownerId, string name,
            long initialCash, int speed, int priority = 0, bool isPlayer = false,
            TradeRoutePolicy? policy = null, bool automationEnabled = false,
            int minimumOriginStock = 0, int minimumReturnStock = 0)
        {
            Ship = ship ?? throw new ArgumentNullException(nameof(ship));
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A ship needs a name.", nameof(name));
            if (initialCash < 0) throw new ArgumentOutOfRangeException(nameof(initialCash));
            if (speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (minimumOriginStock < 0 || minimumReturnStock < 0) throw new ArgumentOutOfRangeException(nameof(minimumOriginStock));
            if (automationEnabled && policy == null) throw new ArgumentException("Enabled automation needs a policy.");
            OwnerId = ownerId;
            Name = name;
            InitialCash = initialCash;
            Speed = speed;
            Priority = priority;
            IsPlayer = isPlayer;
            Policy = policy;
            AutomationEnabled = automationEnabled;
            MinimumOriginStock = minimumOriginStock;
            MinimumReturnStock = minimumReturnStock;
        }
    }
}
