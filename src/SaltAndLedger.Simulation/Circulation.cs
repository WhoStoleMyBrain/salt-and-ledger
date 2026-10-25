using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace SaltAndLedger.Simulation
{
    public enum CirculationMode { None, FeesOnly, MunicipalOnly, Combined }
    public enum MunicipalReviewReason
    {
        AwaitingReview, Disabled, PayrollLevy, FoodRelief, NoProductivePayroll,
        HouseholdFoodReserveProtected, MunicipalPayrollReserveProtected, NoFoodStock, AccountsFunded
    }

    /// <summary>Versioned finite transfers; diagnostic modes never change the authored rates.</summary>
    public sealed class CirculationConfiguration
    {
        public const int CurrentVersion = 1;
        public const int ReviewIntervalDays = 4;
        public const int FeeDenominator = 50;
        public const int SurplusDenominator = 10;
        public const long MaximumFiscalTransfer = 120000;
        public int Version { get; }
        public CirculationMode Mode { get; }
        public bool FeesEnabled => Mode == CirculationMode.FeesOnly || Mode == CirculationMode.Combined;
        public bool MunicipalEnabled => Mode == CirculationMode.MunicipalOnly || Mode == CirculationMode.Combined;
        public CirculationConfiguration(int version = CurrentVersion, CirculationMode mode = CirculationMode.Combined)
        {
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(CirculationMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Version = version; Mode = mode;
        }
    }

    /// <summary>One bounded company/town prefix, shared by ship and warehouse local sales.</summary>
    public sealed class CommercialCirculationSnapshot
    {
        public string OwnerId { get; }
        public string PortId { get; }
        public long GrossLocalSales { get; }
        public long FeesPaid => GrossLocalSales / CirculationConfiguration.FeeDenominator;
        public long MarketFees => FeesPaid / 2;
        public long HouseholdFees => FeesPaid - MarketFees;
        public static string Key(string ownerId, string portId) => "company:" + ownerId + ":" + portId;
        internal CommercialCirculationSnapshot(string ownerId, string portId, long grossLocalSales = 0)
        {
            RuntimeIdentifier.Validate(ownerId, nameof(ownerId));
            RuntimeIdentifier.Validate(portId, nameof(portId));
            if (grossLocalSales < 0) throw new ArgumentOutOfRangeException(nameof(grossLocalSales));
            OwnerId = ownerId; PortId = portId; GrossLocalSales = grossLocalSales;
        }
    }

    public sealed class MunicipalCirculationSnapshot
    {
        public string PortId { get; }
        public long NextReviewDay { get; }
        public long LastReviewDay { get; }
        public long TotalLevy { get; }
        public long TotalRelief { get; }
        public long LastLevy { get; }
        public long LastRelief { get; }
        public long PayrollReserve { get; }
        public long DuePayroll { get; }
        public long FoodReserve { get; }
        public MunicipalReviewReason LastReason { get; }
        internal MunicipalCirculationSnapshot(string portId, long nextReviewDay, long lastReviewDay = 0,
            long totalLevy = 0, long totalRelief = 0, long lastLevy = 0, long lastRelief = 0,
            long payrollReserve = 0, long duePayroll = 0, long foodReserve = 0,
            MunicipalReviewReason lastReason = MunicipalReviewReason.AwaitingReview)
        {
            RuntimeIdentifier.Validate(portId, nameof(portId));
            if (nextReviewDay < 0 || lastReviewDay < 0 || totalLevy < 0 || totalRelief < 0 ||
                lastLevy < 0 || lastRelief < 0 || payrollReserve < 0 || duePayroll < 0 || foodReserve < 0 ||
                !Enum.IsDefined(typeof(MunicipalReviewReason), lastReason))
                throw new ArgumentOutOfRangeException(nameof(nextReviewDay));
            PortId = portId; NextReviewDay = nextReviewDay; LastReviewDay = lastReviewDay;
            TotalLevy = totalLevy; TotalRelief = totalRelief; LastLevy = lastLevy; LastRelief = lastRelief;
            PayrollReserve = payrollReserve; DuePayroll = duePayroll; FoodReserve = foodReserve; LastReason = lastReason;
        }
    }

    public sealed class CirculationSnapshot
    {
        public CirculationConfiguration Configuration { get; }
        public IReadOnlyDictionary<string, CommercialCirculationSnapshot> Commercial { get; }
        public IReadOnlyDictionary<string, MunicipalCirculationSnapshot> Towns { get; }
        internal CirculationSnapshot(CirculationConfiguration configuration,
            IEnumerable<CommercialCirculationSnapshot> commercial, IEnumerable<MunicipalCirculationSnapshot> towns)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Commercial = new ReadOnlyDictionary<string, CommercialCirculationSnapshot>(
                new SortedDictionary<string, CommercialCirculationSnapshot>(commercial.ToDictionary(
                    c => CommercialCirculationSnapshot.Key(c.OwnerId, c.PortId)), StringComparer.Ordinal));
            Towns = new ReadOnlyDictionary<string, MunicipalCirculationSnapshot>(
                new SortedDictionary<string, MunicipalCirculationSnapshot>(towns.ToDictionary(t => t.PortId), StringComparer.Ordinal));
        }
        public long MarketFeesReceived(string portId) => checked((long)Commercial.Values.Where(c => c.PortId == portId)
            .Aggregate(BigInteger.Zero, (sum, c) => sum + c.MarketFees));
        public long HouseholdFeesReceived(string portId) => checked((long)Commercial.Values.Where(c => c.PortId == portId)
            .Aggregate(BigInteger.Zero, (sum, c) => sum + c.HouseholdFees));
        internal static CirculationSnapshot Initial(CirculationConfiguration configuration,
            IEnumerable<string> owners, IEnumerable<string> ports, long day)
        {
            var portIds = ports.ToArray();
            return new CirculationSnapshot(configuration, owners.SelectMany(owner => portIds.Select(port =>
                new CommercialCirculationSnapshot(owner, port))), portIds.Select(port =>
                new MunicipalCirculationSnapshot(port, checked(day + CirculationConfiguration.ReviewIntervalDays))));
        }

        internal TradeAttempt Preview(string ownerId, string portId, long householdCash, TradeAttempt gross)
        {
            if (!Configuration.FeesEnabled || gross.Outcome != TradeOutcome.Traded || gross.Direction != TradeDirection.Sell)
                return gross;
            var previous = Commercial[CommercialCirculationSnapshot.Key(ownerId, portId)];
            var next = new CommercialCirculationSnapshot(ownerId, portId, checked(previous.GrossLocalSales + gross.ExecutedAmount));
            var fee = next.FeesPaid - previous.FeesPaid;
            var marketFee = next.MarketFees - previous.MarketFees;
            var householdFee = next.HouseholdFees - previous.HouseholdFees;
            _ = checked(householdCash + householdFee);
            return new TradeAttempt(new TradeCommand(gross.Direction, gross.GoodId, gross.Quantity), gross.Price, gross.Outcome,
                gross.MarketInventoryChange, gross.TraderInventoryChange,
                new CashChange(gross.MarketCashChange.Before, checked(gross.MarketCashChange.After + marketFee)),
                new CashChange(gross.TraderCashChange.Before, checked(gross.TraderCashChange.After - fee)),
                fee, marketFee, householdFee);
        }
    }
}
