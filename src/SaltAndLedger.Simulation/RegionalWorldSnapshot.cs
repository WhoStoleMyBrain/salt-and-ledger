using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum ScheduledProductionOutcome { NotDue, Disabled, Produced, InputShortage, OutputLimit, WarehouseCapacity }

    public sealed class ScheduledFacilitySnapshot
    {
        public ScheduledFacilityConfiguration Configuration { get; }
        public bool Enabled { get; }
        public long NextDueDay { get; }
        public ScheduledProductionOutcome LastOutcome { get; }
        internal ScheduledFacilitySnapshot(ScheduledFacilityConfiguration configuration, bool enabled,
            long nextDueDay, ScheduledProductionOutcome lastOutcome)
        {
            Configuration = configuration;
            Enabled = enabled;
            NextDueDay = nextDueDay;
            LastOutcome = lastOutcome;
        }
    }

    public sealed class ConsumptionSnapshot
    {
        public ConsumptionConfiguration Configuration { get; }
        public long NextDueDay { get; }
        public int LastUnmetQuantity { get; }
        public long TotalUnmetQuantity { get; }
        internal ConsumptionSnapshot(ConsumptionConfiguration configuration, long nextDueDay,
            int lastUnmetQuantity, long totalUnmetQuantity)
        {
            Configuration = configuration;
            NextDueDay = nextDueDay;
            LastUnmetQuantity = lastUnmetQuantity;
            TotalUnmetQuantity = totalUnmetQuantity;
        }
    }

    public sealed class RegionalPortSnapshot
    {
        public RegionalPortConfiguration Configuration { get; }
        public PortSnapshot Market { get; }
        public string Id => Configuration.Id;
        public string Name => Configuration.Name;
        public int X => Configuration.X;
        public int Y => Configuration.Y;
        public string Role => Configuration.Role;
        public IReadOnlyDictionary<string, int> Inventory => Market.Inventory;
        public long MarketCash => Market.MarketCash;
        public IReadOnlyDictionary<string, MarketQuote> Quotes => Market.Quotes;
        public IReadOnlyList<ScheduledFacilitySnapshot> Facilities { get; }
        public IReadOnlyList<ConsumptionSnapshot> Consumption { get; }
        internal RegionalPortSnapshot(RegionalPortConfiguration configuration, PortSnapshot market,
            IEnumerable<ScheduledFacilitySnapshot> facilities, IEnumerable<ConsumptionSnapshot> consumption)
        {
            Configuration = configuration;
            Market = market;
            Facilities = Array.AsReadOnly(facilities.ToArray());
            Consumption = Array.AsReadOnly(consumption.ToArray());
        }
    }

    public sealed class RegionalShipSnapshot
    {
        public RegionalShipConfiguration Configuration { get; }
        public ShipSnapshot Ship { get; }
        public string Id => Ship.Id;
        public string OwnerId => Configuration.OwnerId;
        public string Name => Configuration.Name;
        public bool IsPlayer => Configuration.IsPlayer;
        public long Cash { get; }
        public TradeRoutePolicy? Policy { get; }
        public bool AutomationEnabled { get; }
        public int VoyageDurationDays { get; }
        public IReadOnlyDictionary<string, int> Cargo => Ship.Cargo;
        public int CargoUnits => Ship.CargoUnits;
        public int CargoCapacity => Ship.CargoCapacity;
        public string? DockedPortId => Ship.DockedPortId;
        public string? OriginPortId => Ship.OriginPortId;
        public string? DestinationPortId => Ship.DestinationPortId;
        public int RemainingTravelDays => Ship.RemainingTravelTicks;
        internal RegionalShipSnapshot(RegionalShipConfiguration configuration, ShipSnapshot ship,
            long cash, TradeRoutePolicy? policy, bool automationEnabled, int voyageDurationDays)
        {
            Configuration = configuration;
            Ship = ship;
            Cash = cash;
            Policy = policy;
            AutomationEnabled = automationEnabled;
            VoyageDurationDays = voyageDurationDays;
        }
    }

    /// <summary>Detached state shared by the map, commands and deterministic experiments.</summary>
    public sealed class RegionalWorldSnapshot
    {
        public long Day { get; }
        public long Revision { get; }
        public IReadOnlyDictionary<string, RegionalPortSnapshot> Ports { get; }
        public IReadOnlyDictionary<string, RegionalShipSnapshot> Ships { get; }
        public IReadOnlyList<SeaRoute> Routes { get; }
        public IReadOnlyDictionary<string, CompanySnapshot> Companies { get; }
        public IReadOnlyDictionary<string, IndustryBlueprint> IndustryBlueprints { get; }
        public IReadOnlyDictionary<string, PrivateFacilitySnapshot> PrivateFacilities { get; }
        public ChallengeSnapshot? Challenge { get; }
        internal RegionalWorldSnapshot(long day, long revision, IEnumerable<RegionalPortSnapshot> ports,
            IEnumerable<RegionalShipSnapshot> ships, IEnumerable<SeaRoute> routes,
            IEnumerable<CompanySnapshot> companies, IEnumerable<IndustryBlueprint> industryBlueprints,
            IEnumerable<PrivateFacilitySnapshot> privateFacilities, ChallengeSnapshot? challenge)
        {
            Day = day;
            Revision = revision;
            Ports = new ReadOnlyDictionary<string, RegionalPortSnapshot>(
                new SortedDictionary<string, RegionalPortSnapshot>(ports.ToDictionary(p => p.Id), StringComparer.Ordinal));
            Ships = new ReadOnlyDictionary<string, RegionalShipSnapshot>(
                new SortedDictionary<string, RegionalShipSnapshot>(ships.ToDictionary(s => s.Id), StringComparer.Ordinal));
            Routes = Array.AsReadOnly(routes.OrderBy(r => r.Key, StringComparer.Ordinal).ToArray());
            Companies = new ReadOnlyDictionary<string, CompanySnapshot>(
                new SortedDictionary<string, CompanySnapshot>(companies.ToDictionary(c => c.Id), StringComparer.Ordinal));
            IndustryBlueprints = new ReadOnlyDictionary<string, IndustryBlueprint>(
                new SortedDictionary<string, IndustryBlueprint>(industryBlueprints.ToDictionary(b => b.Id), StringComparer.Ordinal));
            PrivateFacilities = new ReadOnlyDictionary<string, PrivateFacilitySnapshot>(
                new SortedDictionary<string, PrivateFacilitySnapshot>(privateFacilities.ToDictionary(f => f.Id), StringComparer.Ordinal));
            Challenge = challenge;
        }

        public SeaRoute GetRoute(string from, string to)
        {
            if (!Ports.ContainsKey(from) || !Ports.ContainsKey(to)) throw new KeyNotFoundException("Unknown route port.");
            return Routes.FirstOrDefault(r => (r.OriginPortId == from && r.DestinationPortId == to) ||
                (r.OriginPortId == to && r.DestinationPortId == from)) ??
                throw new InvalidOperationException("There is no direct sea connection between these ports.");
        }

        public int TravelDays(string shipId, string destinationPortId)
        {
            var ship = Ships[shipId];
            return GetRoute(ship.DockedPortId ?? throw new InvalidOperationException("Ship is already sailing."),
                destinationPortId).DurationDays(ship.Configuration.Speed);
        }

        public MarketPrice GetMarketQuote(string portId, string goodId)
        {
            var port = Ports[portId];
            return port.Quotes[goodId].AtStock(port.Inventory[goodId]);
        }

        /// <summary>Hypothetical remote preview; actual commands still require the matching dock.</summary>
        public TradeAttempt PreviewTrade(string shipId, string portId, TradeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var port = Ports[portId];
            var ship = Ships[shipId];
            return TradeCalculator.Preview(port.Quotes[command.GoodId], command, port.Inventory[command.GoodId],
                ship.Cargo[command.GoodId], port.MarketCash, ship.Cash, ship.CargoCapacity - ship.CargoUnits);
        }

        public IReadOnlyList<RegionalShipSnapshot> DockedShips(string portId)
        {
            if (!Ports.ContainsKey(portId)) throw new KeyNotFoundException("Unknown port.");
            return Array.AsReadOnly(Ships.Values.Where(s => s.DockedPortId == portId).ToArray());
        }

        public TradeAttempt PreviewWarehouseTrade(string ownerId, string facilityId, TradeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            var facility = OwnedFacility(ownerId, facilityId);
            var port = Ports[facility.PortId];
            return TradeCalculator.Preview(port.Quotes[command.GoodId], command, port.Inventory[command.GoodId],
                facility.Inventory[command.GoodId], port.MarketCash, Companies[ownerId].Cash,
                facility.Capacity - facility.StoredUnits);
        }

        public int MaximumTradeQuantity(string shipId, string portId, TradeDirection direction, string goodId)
        {
            var port = Ports[portId];
            var ship = Ships[shipId];
            return TradeQuantityCalculator.Maximum(port.Quotes[goodId], direction, port.Inventory[goodId],
                ship.Cargo[goodId], port.MarketCash, ship.Cash, ship.CargoCapacity - ship.CargoUnits);
        }

        public int MaximumWarehouseTradeQuantity(string ownerId, string facilityId, TradeDirection direction, string goodId)
        {
            var facility = OwnedFacility(ownerId, facilityId);
            var port = Ports[facility.PortId];
            return TradeQuantityCalculator.Maximum(port.Quotes[goodId], direction, port.Inventory[goodId],
                facility.Inventory[goodId], port.MarketCash, Companies[ownerId].Cash,
                facility.Capacity - facility.StoredUnits);
        }

        private PrivateFacilitySnapshot OwnedFacility(string ownerId, string facilityId)
        {
            var facility = PrivateFacilities[facilityId];
            if (facility.OwnerId != ownerId) throw new InvalidOperationException("The facility belongs to another company.");
            return facility;
        }
    }
}
