using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;

namespace SaltAndLedger.Simulation
{
    public enum ScheduledProductionOutcome { NotDue, Disabled, Produced, InputShortage, OutputLimit, WarehouseCapacity, UnderConstruction, PayrollShortage }

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
        public int LastDesiredQuantity { get; }
        public int LastConsumedQuantity { get; }
        public long LastPayment { get; }
        public int LastUnmetStock { get; }
        public int LastUnmetAffordability { get; }
        public long TotalDesiredQuantity { get; }
        public long TotalConsumedQuantity { get; }
        public long TotalPayment { get; }
        public long TotalUnmetStock { get; }
        public long TotalUnmetAffordability { get; }
        public int DemandRemainder { get; }
        public int BaselineDemandRemainder { get; }
        public int LastBaselineDesiredQuantity { get; }
        public bool ShouldSerializeDemandRemainder() => Configuration.Proportional;
        public bool ShouldSerializeBaselineDemandRemainder() => Configuration.Proportional;
        public bool ShouldSerializeLastBaselineDesiredQuantity() => Configuration.Proportional;
        public bool ShouldSerializeLastDesiredQuantity() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeLastConsumedQuantity() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeLastPayment() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeLastUnmetStock() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeLastUnmetAffordability() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeTotalDesiredQuantity() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeTotalConsumedQuantity() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeTotalPayment() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeTotalUnmetStock() => Configuration.Kind != ConsumptionKind.Legacy;
        public bool ShouldSerializeTotalUnmetAffordability() => Configuration.Kind != ConsumptionKind.Legacy;
        internal ConsumptionSnapshot(ConsumptionConfiguration configuration, long nextDueDay,
            int lastUnmetQuantity, long totalUnmetQuantity, int lastDesired = 0, int lastConsumed = 0,
            long lastPayment = 0, int unmetStock = 0, int unmetAffordability = 0, long totalDesired = 0,
            long totalConsumed = 0, long totalPayment = 0, long totalStock = 0, long totalAffordability = 0,
            int demandRemainder = 0, int baselineDemandRemainder = 0, int lastBaselineDesired = 0)
        {
            Configuration = configuration;
            NextDueDay = nextDueDay;
            LastUnmetQuantity = lastUnmetQuantity;
            TotalUnmetQuantity = totalUnmetQuantity;
            LastDesiredQuantity = lastDesired; LastConsumedQuantity = lastConsumed; LastPayment = lastPayment;
            LastUnmetStock = unmetStock; LastUnmetAffordability = unmetAffordability;
            TotalDesiredQuantity = totalDesired; TotalConsumedQuantity = totalConsumed; TotalPayment = totalPayment;
            TotalUnmetStock = totalStock; TotalUnmetAffordability = totalAffordability;
            DemandRemainder = demandRemainder; BaselineDemandRemainder = baselineDemandRemainder;
            LastBaselineDesiredQuantity = lastBaselineDesired;
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
        public CitySnapshot? City { get; }
        public bool ShouldSerializeCity() => City != null;
        internal RegionalPortSnapshot(RegionalPortConfiguration configuration, PortSnapshot market,
            IEnumerable<ScheduledFacilitySnapshot> facilities, IEnumerable<ConsumptionSnapshot> consumption,
            CitySnapshot? city = null)
        {
            Configuration = configuration;
            Market = market;
            Facilities = Array.AsReadOnly(facilities.ToArray());
            Consumption = Array.AsReadOnly(consumption.ToArray());
            City = city;
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
        public IndustrialRoutePolicy? IndustrialPolicy { get; }
        public IndustrialRouteStage IndustrialStage { get; }
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
            long cash, TradeRoutePolicy? policy, bool automationEnabled, int voyageDurationDays,
            IndustrialRoutePolicy? industrialPolicy = null,
            IndustrialRouteStage industrialStage = IndustrialRouteStage.AcquireOutbound)
        {
            Configuration = configuration;
            Ship = ship;
            Cash = cash;
            Policy = policy;
            IndustrialPolicy = industrialPolicy;
            IndustrialStage = industrialStage;
            AutomationEnabled = automationEnabled;
            VoyageDurationDays = voyageDurationDays;
        }
    }

    /// <summary>Detached state shared by the map, commands and deterministic experiments.</summary>
    public sealed partial class RegionalWorldSnapshot
    {
        public long Day { get; }
        public long Revision { get; }
        public RunDifficulty Difficulty { get; }
        public RunIdentity? RunIdentity { get; }
        public IReadOnlyDictionary<string, RegionalPortSnapshot> Ports { get; }
        public IReadOnlyDictionary<string, RegionalShipSnapshot> Ships { get; }
        public IReadOnlyList<SeaRoute> Routes { get; }
        public IReadOnlyDictionary<string, CompanySnapshot> Companies { get; }
        public IReadOnlyDictionary<string, IndustryBlueprint> IndustryBlueprints { get; }
        public IReadOnlyDictionary<string, PrivateFacilitySnapshot> PrivateFacilities { get; }
        public ChallengeSnapshot? Challenge { get; }
        public AccountingSnapshot Accounting { get; }
        public ExpandingEconomySnapshot? Economy { get; }
        public bool ShouldSerializeEconomy() => Economy != null;
        [JsonIgnore]
        public int QuantityScale => Economy == null ? 1 : 10000;
        [JsonIgnore]
        public int MoneyScale => Economy == null ? 1 : 10000;
        [JsonIgnore]
        public BigInteger RegionalCash => TotalCash - (Economy?.ExternalCash ?? 0);
        [JsonIgnore]
        public BigInteger TotalCash => Ports.Values.Aggregate(BigInteger.Zero,
            (sum, port) => sum + port.MarketCash + (port.City?.HouseholdCash ?? 0)) +
            Companies.Values.Aggregate(BigInteger.Zero, (sum, company) => sum + company.Cash) +
            (Economy?.TreasuryCash ?? 0) + (BigInteger)(Economy?.ExternalCash ?? 0);
        internal RegionalWorldSnapshot(long day, long revision, IEnumerable<RegionalPortSnapshot> ports,
            IEnumerable<RegionalShipSnapshot> ships, IEnumerable<SeaRoute> routes,
            IEnumerable<CompanySnapshot> companies, IEnumerable<IndustryBlueprint> industryBlueprints,
            IEnumerable<PrivateFacilitySnapshot> privateFacilities, ChallengeSnapshot? challenge,
            AccountingSnapshot accounting, RunDifficulty difficulty = RunDifficulty.Normal,
            RunIdentity? runIdentity = null, ExpandingEconomySnapshot? economy = null)
        {
            Day = day;
            Revision = revision;
            Difficulty = difficulty;
            RunIdentity = runIdentity;
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
            Accounting = accounting;
            Economy = economy;
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

        /// <summary>Current-state retail proposal; no schedule, reservation or future-income promise.</summary>
        public RegionalConsumption PreviewHouseholdConsumption(string portId, string ruleId)
        {
            var port = Ports[portId];
            var city = port.City ?? throw new InvalidOperationException("This historical town has no household account.");
            var need = port.Consumption.Single(c => c.Configuration.Id == ruleId);
            var food = port.Consumption.Single(c => c.Configuration.Kind == ConsumptionKind.Essential);
            return LivingRetail.Preview(port.Configuration, city, need.Configuration, port.Quotes, port.Inventory,
                port.MarketCash, need.DemandRemainder, food.DemandRemainder);
        }

        /// <summary>Dates only: eligibility, funds and site uniqueness are still checked when ordering.</summary>
        public ConstructionSchedule PreviewConstructionSchedule(string ownerId, string blueprintId)
        {
            if (!Companies.ContainsKey(ownerId)) throw new KeyNotFoundException("Unknown company.");
            var blueprint = IndustryBlueprints[blueprintId];
            var delay = Difficulty == RunDifficulty.SlowFoundations &&
                Ships.Values.Any(s => s.IsPlayer && s.OwnerId == ownerId) ? 2 : 0;
            return new ConstructionSchedule(Day, delay, blueprint.IntervalDays);
        }

        /// <summary>Hypothetical remote preview; actual commands still require the matching dock.</summary>
        public TradeAttempt PreviewTrade(string shipId, string portId, TradeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            ValidateWholeCargo(command.Quantity);
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
            var quantity = TradeQuantityCalculator.Maximum(port.Quotes[goodId], direction, port.Inventory[goodId],
                ship.Cargo[goodId], port.MarketCash, ship.Cash, ship.CargoCapacity - ship.CargoUnits);
            return quantity - quantity % QuantityScale;
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
