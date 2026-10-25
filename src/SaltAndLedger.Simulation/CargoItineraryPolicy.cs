using System;
using System.Collections.Generic;
using System.Linq;

namespace SaltAndLedger.Simulation
{
    public enum CargoItineraryCommandKind { BuyFromMarket, SellToMarket, LoadFromCompanyTownStock, UnloadToCompanyTownStock }
    public enum CargoItineraryShortagePolicy { Skip, Wait }
    public enum CargoItineraryDecisionKind { Execute, Skip, Wait, Depart, ContinueSailing, Paused }
    public enum CargoItineraryBlockReason
    {
        None, Disabled, WrongPort, Sailing, DailyLimit, SourceStock, CargoFull, WarehouseFull,
        WarehouseReserve, CompanyFunds, CashReserve, ExpenditureLimit, MarketFunds, PriceLimit,
        MarketCapacity, AccountCapacity, MissingCompanyStock
    }

    /// <summary>One bounded order per visit. Quantities are whole-cargo multiples in physical subunits.</summary>
    public sealed class CargoItineraryCommand
    {
        public CargoItineraryCommandKind Kind { get; }
        public string GoodId { get; }
        public int MaximumQuantity { get; }
        public int MinimumQuantity { get; }
        public CargoItineraryShortagePolicy ShortagePolicy { get; }
        public long MarginalPriceLimit { get; }
        public long CashReserve { get; }
        public long MaximumExpenditure { get; }
        public int WarehouseReserve { get; }

        public CargoItineraryCommand(CargoItineraryCommandKind kind, string goodId, int maximumQuantity,
            int minimumQuantity = EconomicUnits.QuantityScale,
            CargoItineraryShortagePolicy shortagePolicy = CargoItineraryShortagePolicy.Skip,
            long marginalPriceLimit = 0, long cashReserve = 0, long maximumExpenditure = long.MaxValue,
            int warehouseReserve = 0)
        {
            if (!Enum.IsDefined(typeof(CargoItineraryCommandKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!Enum.IsDefined(typeof(CargoItineraryShortagePolicy), shortagePolicy)) throw new ArgumentOutOfRangeException(nameof(shortagePolicy));
            RuntimeIdentifier.Validate(goodId, nameof(goodId));
            if (maximumQuantity <= 0 || minimumQuantity <= 0 || minimumQuantity > maximumQuantity ||
                maximumQuantity % EconomicUnits.QuantityScale != 0 || minimumQuantity % EconomicUnits.QuantityScale != 0)
                throw new ArgumentOutOfRangeException(nameof(maximumQuantity), "Use positive whole cargo quantities; minimum cannot exceed maximum.");
            if (marginalPriceLimit < 0 || cashReserve < 0 || maximumExpenditure <= 0 || warehouseReserve < 0)
                throw new ArgumentOutOfRangeException(nameof(marginalPriceLimit));
            var market = kind == CargoItineraryCommandKind.BuyFromMarket || kind == CargoItineraryCommandKind.SellToMarket;
            if ((!market && marginalPriceLimit != 0) ||
                (kind != CargoItineraryCommandKind.BuyFromMarket && (cashReserve != 0 || maximumExpenditure != long.MaxValue)) ||
                (kind != CargoItineraryCommandKind.LoadFromCompanyTownStock && warehouseReserve != 0))
                throw new ArgumentException("Constraints must apply to this command's actual source and paying account.");
            Kind = kind; GoodId = goodId; MaximumQuantity = maximumQuantity; MinimumQuantity = minimumQuantity;
            ShortagePolicy = shortagePolicy; MarginalPriceLimit = marginalPriceLimit; CashReserve = cashReserve;
            MaximumExpenditure = maximumExpenditure; WarehouseReserve = warehouseReserve;
        }

        internal bool EquivalentTo(CargoItineraryCommand other) => Kind == other.Kind && GoodId == other.GoodId &&
            MaximumQuantity == other.MaximumQuantity && MinimumQuantity == other.MinimumQuantity &&
            ShortagePolicy == other.ShortagePolicy && MarginalPriceLimit == other.MarginalPriceLimit &&
            CashReserve == other.CashReserve && MaximumExpenditure == other.MaximumExpenditure && WarehouseReserve == other.WarehouseReserve;
    }

    public sealed class CargoItineraryStop
    {
        public string PortId { get; }
        public IReadOnlyList<CargoItineraryCommand> Commands { get; }
        public CargoItineraryStop(string portId, IEnumerable<CargoItineraryCommand> commands)
        {
            RuntimeIdentifier.Validate(portId, nameof(portId));
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            var values = commands.Take(9).ToArray();
            if (values.Length < 1 || values.Length > 8 || values.Any(c => c == null))
                throw new ArgumentException("Each stop requires 1–8 commands.", nameof(commands));
            PortId = portId; Commands = Array.AsReadOnly(values);
        }
    }

    public sealed class CargoItineraryPolicy
    {
        public const int CurrentVersion = 1;
        public const int MaximumDailyOperations = 8;
        public string Id { get; }
        public int Version { get; }
        public IReadOnlyList<CargoItineraryStop> Stops { get; }
        public CargoItineraryPolicy(string id, IEnumerable<CargoItineraryStop> stops, int version = CurrentVersion)
        {
            RuntimeIdentifier.Validate(id, nameof(id));
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version));
            if (stops == null) throw new ArgumentNullException(nameof(stops));
            var values = stops.Take(5).ToArray();
            if (values.Length < 2 || values.Length > 4 || values.Any(s => s == null))
                throw new ArgumentException("An itinerary requires 2–4 stops.", nameof(stops));
            if (values.Select(s => s.PortId).Distinct(StringComparer.Ordinal).Count() != values.Length ||
                values.Sum(s => s.Commands.Count) > 24)
                throw new ArgumentException("Use unique ports and at most 24 commands per itinerary.", nameof(stops));
            Id = id; Version = version; Stops = Array.AsReadOnly(values);
        }

        public bool EquivalentTo(CargoItineraryPolicy? other)
        {
            if (other == null || Id != other.Id || Version != other.Version || Stops.Count != other.Stops.Count) return false;
            for (var i = 0; i < Stops.Count; i++)
            {
                if (Stops[i].PortId != other.Stops[i].PortId || Stops[i].Commands.Count != other.Stops[i].Commands.Count) return false;
                for (var j = 0; j < Stops[i].Commands.Count; j++)
                    if (!Stops[i].Commands[j].EquivalentTo(other.Stops[i].Commands[j])) return false;
            }
            return true;
        }
    }

    /// <summary>The next unfinished instruction and the day's consumed execution budget, saved atomically.</summary>
    public sealed class CargoItineraryProgress
    {
        public int StopIndex { get; }
        public int ActionIndex { get; }
        public long Cycle { get; }
        public CargoItineraryDecisionKind LastDecision { get; }
        public CargoItineraryBlockReason LastReason { get; }
        public int LastQuantity { get; }
        public long OperationsDay { get; }
        public int OperationsUsed { get; }
        public CargoItineraryProgress(int stopIndex, int actionIndex = 0, long cycle = 0,
            CargoItineraryDecisionKind lastDecision = CargoItineraryDecisionKind.Paused,
            CargoItineraryBlockReason lastReason = CargoItineraryBlockReason.None, int lastQuantity = 0,
            long operationsDay = -1, int operationsUsed = 0)
        {
            if (stopIndex < 0 || stopIndex > 3 || actionIndex < 0 || actionIndex > 8 || cycle < 0 || lastQuantity < 0 ||
                lastQuantity % EconomicUnits.QuantityScale != 0 || operationsDay < -1 || operationsUsed < 0 ||
                operationsUsed > CargoItineraryPolicy.MaximumDailyOperations || (operationsDay == -1 && operationsUsed != 0))
                throw new ArgumentOutOfRangeException(nameof(stopIndex), "Invalid bounded itinerary progress.");
            if (!Enum.IsDefined(typeof(CargoItineraryDecisionKind), lastDecision) || !Enum.IsDefined(typeof(CargoItineraryBlockReason), lastReason))
                throw new ArgumentOutOfRangeException(nameof(lastDecision));
            StopIndex = stopIndex; ActionIndex = actionIndex; Cycle = cycle; LastDecision = lastDecision;
            LastReason = lastReason; LastQuantity = lastQuantity; OperationsDay = operationsDay; OperationsUsed = operationsUsed;
        }
    }

    public sealed class CargoItineraryDecision
    {
        public RegionalWorldSnapshot Before { get; }
        public string ShipId { get; }
        public CargoItineraryDecisionKind Kind { get; }
        public CargoItineraryBlockReason BlockReason { get; }
        public string Reason { get; }
        public int StopIndex { get; }
        public int ActionIndex { get; }
        public CargoItineraryCommand? Command { get; }
        public PortTradeCommand? Trade { get; }
        public IndustrialTransferCommand? Transfer { get; }
        public SailCommand? Departure { get; }
        public TradeAttempt? Preview { get; }
        public int SelectedQuantity => Trade?.Trade.Quantity ?? Transfer?.Quantity ?? 0;
        internal CargoItineraryDecision(RegionalWorldSnapshot before, string shipId, CargoItineraryDecisionKind kind,
            CargoItineraryBlockReason blockReason, string reason, int stopIndex, int actionIndex,
            CargoItineraryCommand? command = null, PortTradeCommand? trade = null,
            IndustrialTransferCommand? transfer = null, SailCommand? departure = null, TradeAttempt? preview = null)
        {
            Before = before; ShipId = shipId; Kind = kind; BlockReason = blockReason; Reason = reason;
            StopIndex = stopIndex; ActionIndex = actionIndex; Command = command;
            Trade = trade; Transfer = transfer; Departure = departure; Preview = preview;
        }
    }
}
